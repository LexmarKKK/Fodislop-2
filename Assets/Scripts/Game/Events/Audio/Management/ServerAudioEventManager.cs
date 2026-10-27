#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kern.Audio.Core;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Core.Lifecycle;
using Kern.Game;
using Kern.World;
using Kern.World.Terrain;
using MinesServer.Networking.Server.Packets.World;
using UnityEngine;
using VContainer;

using Kern.Game.Events.VisualEffects;
using Kern.Game.Player.Cameras;
using Kern.Game.Rendering;
using Kern.Game.VisualEffects.Cells;
using Kern.Game.VisualEffects.Crystals;
using Kern.Game.VisualEffects.Pooling;
using Kern.Game.VisualEffects.Sprites;
using Kern.Game.VisualEffects.Weapons;

namespace Kern.Game.Events.Audio
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Kern.Game.Managers", sourceAssembly: null, sourceClassName: "ServerAudioEventManager")]
    [DefaultExecutionOrder(100)]
    public class ServerAudioEventManager : MonoBehaviour, IServerAudioService, IServerVFXService
    {
        private const string TAG = "[ServerAudioEventManager]";
        // Select the crystal collection visual here. Set to CurrentAssets to
        // use the existing standalone crystal sprites instead.
        private const CrystalCollectionVisualVariant CrystalCollectionVariant =
            CrystalCollectionVisualVariant.SourceMineralAtlas;
        private const int SourceCrystalSortingOrder = 5; // SpriteRenderer.m_SortingOrder in the source prefab.

        private const string MusicEventName = "music/evil_huge";
        private readonly List<IServerWorldEffect> _activeEffects = new();
        private readonly Dictionary<(global::MinesServer.Data.SFX Effect, ushort Bot, ushort X, ushort Y), float> _lastSfxTimes = new();
        private readonly Dictionary<ushort, SpriteAnimationWorldEffect> _healingEffects = new();
        private SpriteAnimationWorldEffect? _heldHealEffect;
        [Inject]
        private IWorldLabels _worldLabels = null!;
        [Inject]
        private Kern.Game.Player.Cameras.CameraFollow _cameraFollow = null!;
        private IAudioPlaybackHandle? _currentMusicHandle;
        private bool _isMusicStarting;

        [Inject]
        private IVFXService _vfxService = null!;

        [Inject]
        private IRobotService _robotService = null!;

        [Inject]
        private IAudioSystem _audioSystem = null!;

        [Inject]
        private CellDestructionClip _cellDestructionClip = null!;
        [Inject]
        private SpriteAnimationLibrary _spriteAnimations = null!;
        [Inject]
        private Kern.World.Lighting.LightingEngine _lighting = null!;

        [Inject]
        private IAssetLoader _assetLoader = null!;

        [Inject]
        private MapManager _mapManager = null!;

        [Inject]
        private VFXPool _vfxPool = null!;
        [Inject]
        private IAsyncOperationSupervisor _operations = null!;
        [Inject]
        private ILocalPlayerState _localPlayer = null!;
        [Inject]
        private WorldEntityBatchRenderer _entityBatchRenderer = null!;
        [Inject]
        private ISceneObjectFactory _sceneObjects = null!;

        public void PlayEffect(AudioPacket packet)
        {
            if (packet.EffectType == global::MinesServer.Data.SFX.Music)
            {
                if (_isMusicStarting || (_currentMusicHandle != null && _currentMusicHandle.IsPlaying))
                {
                    return;
                }

                _isMusicStarting = true;
                _operations.Run("play_server_music", PlayMusicWhenAudioReadyAsync);
                return;
            }

            var sfxKey = (packet.EffectType, packet.TargetBotId, packet.X, packet.Y);
            bool isCellDestruction = CellDestructionEffect.IsCellDestruction(packet.EffectType);
            if (!isCellDestruction && _lastSfxTimes.TryGetValue(sfxKey, out float lastTime) && Time.time - lastTime < 0.04f)
            {
                return;
            }

            // В фоне (MuteInBackground) мастер-шина FMOD на паузе: голос,
            // запущенный на запаузенной шине, не завершается и копится, а при
            // возврате фокуса все накопленные голоса снимаются с паузы разом
            // и наслаиваются. Звук, случившийся в фоне, всё равно не был
            // слышен — отбрасываем его сразу, а не отыгрываем потом.
            if (!Application.isFocused && _audioSystem.IsBackgroundMuted)
            {
                return;
            }

            _lastSfxTimes[sfxKey] = Time.time;
            if (_lastSfxTimes.Count > 128)
            {
                _lastSfxTimes.Clear();
            }

            // Destroy and Geology correspond to legacy CellDestructionD/G.
            // Their protocol has no separate VFX identifier: play exactly one
            // shared animation at the packet cell, ignoring robot facing.
            if (isCellDestruction)
            {
                _activeEffects.Add(new CellDestructionEffect(packet.EffectType,
                    WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight),
                    _cellDestructionClip, _sceneObjects, _lighting));
            }

            var effect = new ServerAudioEvent(
                packet,
                slot: null,
                _robotService,
                _localPlayer,
                _audioSystem,
                _assetLoader,
                _mapManager,
                _vfxPool,
                _operations);
            _activeEffects.Add(effect);
        }

        public void PlayEffect(global::MinesServer.Data.SFX effectType, ushort x, ushort y, ushort targetBotId = 0, int param = 0) =>
            PlayEffect(new AudioPacket(effectType, targetBotId, x, y, Array.Empty<MinesServer.Networking.Shared.Packets.StringPairPacket>()));

        public void PlayEffect(global::MinesServer.Data.VFX effectType, ushort x, ushort y, ushort targetBotId = 0, int param = 0) =>
            PlayEffect(new VFXPacket(effectType, targetBotId, x, y, Array.Empty<MinesServer.Networking.Shared.Packets.StringPairPacket>()));

        public void PlayEffect(VFXPacket packet)
        {
            if (packet.EffectType == global::MinesServer.Data.VFX.Crystal)
            {
                // Preserve the existing crystal flight/digits; the source flash
                // is a separate animation at the mined cell.
                _activeEffects.Add(new SpriteAnimationWorldEffect("CrystallSparkle",
                    WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight),
                    _spriteAnimations, _sceneObjects));
            }

            if (TryPlaySpriteAnimationEffect(packet))
            {
                return;
            }

            IVFXSlot? slot = packet.EffectType == global::MinesServer.Data.VFX.Crystal &&
                CrystalCollectionVariant == CrystalCollectionVisualVariant.SourceMineralAtlas
                ? _vfxService.Acquire(SourceCrystalSortingOrder)
                : _vfxService.Acquire();

            var effect = new ServerVFXEvent(
                packet,
                slot,
                _robotService,
                _assetLoader,
                _mapManager,
                _vfxPool,
                _operations,
                _entityBatchRenderer,
                _spriteAnimations,
                CrystalCollectionVariant,
                _sceneObjects,
                _worldLabels,
                lighting: _lighting);
            _activeEffects.Add(effect);
        }

        public void PlayBoundaryDig(int serverX, int serverY, global::MinesServer.Data.Direction direction)
        {
            float angle = direction switch
            {
                global::MinesServer.Data.Direction.Up => 0f,
                global::MinesServer.Data.Direction.Right => -90f,
                global::MinesServer.Data.Direction.Down => 180f,
                global::MinesServer.Data.Direction.Left => 90f,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
            var packet = new VFXPacket(global::MinesServer.Data.VFX.Bz, 0, 0, 0,
                Array.Empty<MinesServer.Networking.Shared.Packets.StringPairPacket>());
            _activeEffects.Add(new ServerVFXEvent(
                packet, _vfxService.Acquire(), _robotService, _assetLoader, _mapManager,
                _vfxPool, _operations, _entityBatchRenderer, _spriteAnimations,
                CrystalCollectionVariant, _sceneObjects, _worldLabels,
                new Vector2Int(serverX, serverY), angle, _lighting));
        }

        public void SetLocalHealHeld(bool held)
        {
            if (!held)
            {
                _heldHealEffect?.Dispose();
                _heldHealEffect = null;
                return;
            }

            if (_heldHealEffect != null && !_heldHealEffect.IsDisposed)
            {
                return;
            }

            var player = _localPlayer.Current;
            if (player == null)
            {
                return;
            }

            ushort botId = checked((ushort)player.BotId);
            if (!_healingEffects.TryGetValue(botId, out SpriteAnimationWorldEffect healing) || healing.IsDisposed)
            {
                healing = new SpriteAnimationWorldEffect("Heal", player.transform.position,
                    _spriteAnimations, _sceneObjects, player.transform, lighting: _lighting);
                _healingEffects[botId] = healing;
                _activeEffects.Add(healing);
            }

            healing.Hold();
            _heldHealEffect = healing;
        }

        private bool TryPlaySpriteAnimationEffect(VFXPacket packet)
        {
            // Effects.Create(Hurt): only the local target starts TpCamera shake.
            if (packet.EffectType == global::MinesServer.Data.VFX.Hurt &&
                _localPlayer.Current is { } player && player.BotId == packet.TargetBotId)
            {
                _cameraFollow.TriggerHurtShake();
            }

            if (packet.EffectType == global::MinesServer.Data.VFX.GunShot)
            {
                if (_robotService.TryGetRobot(packet.TargetBotId, out IRobotView? target) && target != null)
                {
                    Vector3 origin = WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight);
                    _activeEffects.Add(new GunShotWorldEffect(origin, target.transform.position,
                        _spriteAnimations, _sceneObjects, unchecked((uint)Time.frameCount ^ packet.TargetBotId)));
                }

                return true;
            }

            if (packet.EffectType is global::MinesServer.Data.VFX.Pull or global::MinesServer.Data.VFX.Push)
            {
                if (!_robotService.TryGetRobot(packet.TargetBotId, out IRobotView? robot) || robot == null)
                {
                    return true;
                }

                Vector3 cellPosition = WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight);
                bool isPull = packet.EffectType == global::MinesServer.Data.VFX.Pull;
                Vector3 start = isPull ? robot.transform.position : cellPosition;
                Vector3 end = isPull ? cellPosition : robot.transform.position;
                _activeEffects.Add(new SpriteAnimationWorldEffect("Arrow", start, _spriteAnimations, _sceneObjects,
                    destinationPosition: end));
                return true;
            }

            if (packet.EffectType == global::MinesServer.Data.VFX.BombTick)
            {
                Vector3 bombPosition = WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight);
                _activeEffects.Add(new SpriteAnimationWorldEffect(BombClip(ReadBombType(packet)), bombPosition,
                    _spriteAnimations, _sceneObjects));
                return true;
            }

            // Only existing visual events are consumed. Audio packets never
            // manufacture Hurt/Heal/etc., and Bz keeps its already ported spark.
            string? id = packet.EffectType switch
            {
                global::MinesServer.Data.VFX.Hurt => "Hurt",
                global::MinesServer.Data.VFX.Heal => "Heal",
                global::MinesServer.Data.VFX.Death => "Explosion",
                global::MinesServer.Data.VFX.Boom => "Boom",
                global::MinesServer.Data.VFX.NoHp => "RobotSmoke",
                global::MinesServer.Data.VFX.NoHpSmall => "RobotSmoke",
                global::MinesServer.Data.VFX.Smoke => "Smoke0",
                global::MinesServer.Data.VFX.Volcano => "Smoke0",
                global::MinesServer.Data.VFX.TpOut => "Tpo",
                // Raw ResourcesManager.tpi PPtr resolves to Respio (path id 1207).
                global::MinesServer.Data.VFX.TpIn => "Respio",
                _ => null,
            };
            if (id == null) { return false; }
            Vector3 position = WorldCoordinateConverter.ServerToUnityPos(packet.X, packet.Y, _mapManager.WorldHeight);
            Transform? follow = null;
            if (packet.TargetBotId != 0 && (id is "Hurt" or "Heal" or "RobotSmoke"))
            {
                if (!_robotService.TryGetRobot(packet.TargetBotId, out IRobotView? robot) || robot == null)
                {
                    return true; // No robot: do not create a phantom entity for an effect.
                }

                position = robot.transform.position;
                if (id is "Hurt" or "Heal") { follow = robot.transform; }
            }

            if (id == "Heal" && _healingEffects.TryGetValue(packet.TargetBotId, out SpriteAnimationWorldEffect healing) && !healing.IsDisposed)
            {
                healing.Refresh();
                return true;
            }

            int count = packet.EffectType == global::MinesServer.Data.VFX.Volcano ? 6 : 1;
            for (int index = 0; index < count; index++)
            {
                string clip = packet.EffectType == global::MinesServer.Data.VFX.Volcano
                    ? "Smoke" + UnityEngine.Random.Range(0, 3)
                    : id;
                int boomType = packet.EffectType == global::MinesServer.Data.VFX.Boom ? ReadBombType(packet) : 0;
                var effect = new SpriteAnimationWorldEffect(clip, position, _spriteAnimations, _sceneObjects, follow, boomType,
                    lighting: _lighting);
                _activeEffects.Add(effect);
                if (id == "Heal") { _healingEffects[packet.TargetBotId] = effect; }
            }

            return true;
        }

        private static string BombClip(int bombType) => bombType switch
        {
            0 => "BombPlasma",
            1 => "BombProton",
            2 => "BombDischarge",
            _ => throw new ArgumentOutOfRangeException(nameof(bombType), bombType, "Unknown bomb type."),
        };

        private static int ReadBombType(VFXPacket packet)
        {
            foreach (var parameter in packet.Parameters)
            {
                if (!parameter.Key.Equals("bombType", StringComparison.OrdinalIgnoreCase) &&
                    !parameter.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = parameter.Value.Trim();
                if (int.TryParse(value, out int numericType))
                {
                    return numericType;
                }

                return value.ToLowerInvariant() switch
                {
                    "plasma" => 0,
                    "proton" => 1,
                    "discharge" => 2,
                    _ => throw new FormatException($"Unknown bomb type '{parameter.Value}'."),
                };
            }

            // BoomScript's default enum value is Plasma (0).
            return 0;
        }

        private async UniTask PlayMusicWhenAudioReadyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _audioSystem.WaitUntilBanksReadyAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (_currentMusicHandle != null && _currentMusicHandle.IsPlaying)
                {
                    return;
                }

                StopMusic(0f);
                _currentMusicHandle = _audioSystem.Play2D(MusicEventName, AudioLayer.MusicDefault());
                if (_currentMusicHandle == null)
                {
                    Debug.LogWarning($"{TAG} Музыка '{MusicEventName}' не запустилась.");
                }
            }
            finally
            {
                _isMusicStarting = false;
            }
        }

        private void StopMusic(float fadeOut = 0.5f)
        {
            if (_currentMusicHandle != null)
            {
                if (_currentMusicHandle.IsPlaying)
                {
                    _currentMusicHandle.Stop(fadeOut);
                }

                _currentMusicHandle = null;
            }
        }

        public void ClearAllEffects()
        {
            StopMusic();
            int count = _activeEffects.Count;
            foreach (var effect in _activeEffects)
            {
                effect.Dispose();
            }

            _activeEffects.Clear();
            _healingEffects.Clear();
            _heldHealEffect = null;
            if (_cameraFollow != null)
            {
                _cameraFollow.ClearHurtShake();
            }
            if (count > 0)
            {
                Debug.Log($"{TAG} Cleared {count} active effects");
            }
        }

        protected void OnDestroy()
        {
            ClearAllEffects();
            _audioSystem?.StopBus(AudioBusType.Music, 0.2f);
        }

        protected void Update()
        {
            if (_activeEffects.Count == 0)
            {
                return;
            }

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                effect.Update();
                if (effect.IsDisposed)
                {
                    _activeEffects.RemoveAt(i);
                }
            }
        }
    }
}
