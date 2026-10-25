#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Core.Lifecycle;
using UnityEngine;

namespace Kern.Game;

public sealed class RobotVisuals
{
    private readonly Transform _transform;
    private readonly bool _isLocalPlayer;
    private WorldEntityBatchRenderer _entityBatchRenderer = null!;
    private WorldEntityBatchRenderer.SpriteHandle? _bodyBatchHandle;
    private WorldEntityBatchRenderer.SpriteHandle? _clanBatchHandle;
    private Transform? _clanTransform;
    private Sprite? _skinSprite;
    private Sprite? _clanSprite;
    private RobotAura? _aura;
    private Tentacle[]? _tentacles;
    private bool _tentaclesSettled;
    private Vector3 _lastTentacleRootPosition;
    private float _lastTentacleRotation;
    private IRuntimeDebugSettings? _debugSettings;
    private Vector3 _forcedOriginOffset;

    public Sprite? SkinSprite => _skinSprite;
    public Transform? ClanTransform => _clanTransform;
    public bool TentaclesSettled => _tentaclesSettled;

    /// <summary>Число прядей хвоста: вдвое больше легаси-четырёх; добавленные короче и едут внутри отрезка пряди ноль.</summary>
    public const int StrandCount = 8;

    public void SetDebugSettings(IRuntimeDebugSettings? settings) => _debugSettings = settings;

    // Отладочные режимы хвоста (F2) подменяют скорость корня, который получает цепочка:
    // режим задаёт скорость, смещение копится вдоль взгляда, а при записи геометрии
    // вычитается обратно - хвост держит длину, не отклеиваясь от корпуса.
    public static float ForcedTailSpeed(TailMotionDebugMode mode) => mode switch
    {
        TailMotionDebugMode.Minimal => ProjectRuntimeContracts.Movement.RobotMoveSpeed * 0.5f,
        TailMotionDebugMode.Medium => ProjectRuntimeContracts.Movement.RobotMoveSpeed,
        TailMotionDebugMode.Maximum => ProjectRuntimeContracts.Movement.RobotMoveSpeed * 4f,
        _ => 0f,
    };

    public static Vector3 AdvanceForcedOffset(Vector3 offset, Transform transform, float speed, float deltaTime)
    {
        if (speed <= 0f || transform == null)
        {
            return offset;
        }

        // Роботы вращаются вокруг Z, поэтому направление взгляда - Transform.up:
        // у Transform.forward при нулевом угле в плоскости игры не остаётся ничего.
        Vector3 facing = transform.up;
        facing.z = 0f;
        if (facing.sqrMagnitude < 1e-10f)
        {
            return offset;
        }

        return offset + (facing.normalized * (speed * deltaTime));
    }

    public RobotVisuals(Transform transform, bool isLocalPlayer)
    {
        _transform = transform;
        _isLocalPlayer = isLocalPlayer;
    }

    public void Initialize(WorldEntityBatchRenderer entityBatchRenderer, Transform? clanTransform)
    {
        _entityBatchRenderer = entityBatchRenderer;
        _clanTransform = clanTransform;
        EnsureBatchHandles();
    }

    public void EnsureBatchHandles()
    {
        if (_entityBatchRenderer == null)
        {
            return;
        }

        _bodyBatchHandle ??= _entityBatchRenderer.RegisterSprite(_transform, 0);
        if (_clanTransform != null)
        {
            _clanBatchHandle ??= _entityBatchRenderer.RegisterSprite(_clanTransform, 100);
        }
    }

    public void SetBodyVisible(bool visible)
    {
        EnsureBatchHandles();
        _bodyBatchHandle?.SetEnabled(visible);
    }
    public void SetColor(Color color)
    {
        _bodyBatchHandle?.SetColor(color);
    }

    public void SetClanSprite(Sprite? sprite)
    {
        EnsureBatchHandles();
        if (_clanSprite != null)
        {
            Object.Destroy(_clanSprite);
        }

        _clanSprite = sprite;
        if (_clanBatchHandle != null)
        {
            _entityBatchRenderer.SetSprite(_clanBatchHandle, sprite);
            _clanBatchHandle.SetEnabled(sprite != null);
        }
    }

    public void SetSkinSprite(Sprite? sprite)
    {
        EnsureBatchHandles();
        if (_skinSprite != null)
        {
            Object.Destroy(_skinSprite);
        }

        _skinSprite = sprite;
        if (_bodyBatchHandle != null)
        {
            _entityBatchRenderer.SetSprite(_bodyBatchHandle, sprite);
            if (!_isLocalPlayer)
            {
                _bodyBatchHandle.SetEnabled(sprite != null);
            }
        }
    }

    public void CreateTentacles(Texture2D tailTexture, Vector3 position)
    {
        ClearTentacles();
        if (_entityBatchRenderer == null)
        {
            return;
        }

        _tentacles = new Tentacle[StrandCount];
        _tentaclesSettled = false;
        for (int i = 0; i < StrandCount; i++)
        {
            _tentacles[i] = new Tentacle(
                _entityBatchRenderer,
                tailTexture,
                position,
                strandIndex: i);
        }
    }

    public void ClearTentacles()
    {
        if (_tentacles != null)
        {
            foreach (var tentacle in _tentacles)
            {
                tentacle?.Destroy();
            }

            _tentacles = null;
        }
    }

    public void SetTentaclesActive(bool active)
    {
        if (_tentacles == null)
        {
            return;
        }

        foreach (Tentacle? tentacle in _tentacles)
        {
            tentacle?.SetActive(active);
        }
    }

    public void SnapTentacles(Vector3 position)
    {
        if (_tentacles != null)
        {
            _tentaclesSettled = false;

            // Снап сбрасывает и накопленное смещение рамки симуляции, иначе корни
            // прядей утащит далеко от робота и хвост нарисуется торчащей полосой.
            _forcedOriginOffset = Vector3.zero;
            foreach (Tentacle? tentacle in _tentacles)
            {
                tentacle?.Snap(position);
            }
        }
    }

    public bool AreTentaclesSettled()
    {
        if (_tentacles == null)
        {
            return true;
        }

        foreach (Tentacle? tentacle in _tentacles)
        {
            if (tentacle != null && !tentacle.IsSettled)
            {
                return false;
            }
        }

        return true;
    }

    public void UpdateTentacles(Vector3 rootPosition, float movementFactor, float deltaTime)
    {
        if (_tentacles == null)
        {
            return;
        }

        float forcedSpeed = ForcedTailSpeed(
            _debugSettings?.TailMotionDebugMode ?? TailMotionDebugMode.Normal);
        if (forcedSpeed > 0f)
        {
            _forcedOriginOffset = AdvanceForcedOffset(
                _forcedOriginOffset, _transform, forcedSpeed, deltaTime);
        }
        else if (_forcedOriginOffset.sqrMagnitude > 0f)
        {
            // Возврат в обычный режим: рамка симуляции снова совпадает с роботом.
            _forcedOriginOffset = Vector3.zero;
        }

        Vector3 simulationRoot = rootPosition + _forcedOriginOffset;

        // Растяжение читается с пряди ноль и раздаётся всем прядям, как в легаси-клиенте.
        float stretch = _tentacles[0]?.TipDistanceTo(simulationRoot) ?? 0f;

        foreach (var tentacle in _tentacles)
        {
            tentacle?.Update(simulationRoot, movementFactor, deltaTime, stretch, _forcedOriginOffset);
        }
    }

    public void UpdateMotion(Vector3 position, float rotationAngle, float movementFactor, float deltaTime, bool bodySettled)
    {
        if (_tentacles == null)
        {
            _tentaclesSettled = true;
            return;
        }

        if (bodySettled && _tentaclesSettled)
        {
            return;
        }

        if (bodySettled)
        {
            UpdateTentacles(position, 0f, deltaTime);
            _tentaclesSettled = AreTentaclesSettled();
            return;
        }

        bool tentacleStateChanged =
            !_tentaclesSettled ||
            (position - _lastTentacleRootPosition).sqrMagnitude > 1e-8f ||
            Mathf.Abs(Mathf.DeltaAngle(_lastTentacleRotation, rotationAngle)) > 0.001f ||
            movementFactor > 0.0001f;

        if (tentacleStateChanged)
        {
            UpdateTentacles(position, movementFactor, deltaTime);
            _tentaclesSettled = AreTentaclesSettled();
            _lastTentacleRootPosition = position;
            _lastTentacleRotation = rotationAngle;
        }
    }

    public void SetAuraWanted(bool wanted, ISceneObjectFactory? sceneObjects)
    {
        if (!wanted && _aura == null)
        {
            return;
        }

        _aura ??= new RobotAura(_transform);
        _aura.SetWanted(wanted, _entityBatchRenderer, sceneObjects);
    }

    public void TickAura(float deltaTime) => _aura?.Tick(deltaTime);

    public Transform EnsureClanIcon(ISceneObjectFactory sceneObjects, uint botId)
    {
        if (_clanTransform == null)
        {
            Transform? existingClan = _transform.Find("ClanIcon");
            GameObject clanGo = existingClan != null
                ? existingClan.gameObject
                : (sceneObjects != null
                    ? sceneObjects.Create("ClanIcon", RuntimeOwner.Robots)
                    : throw new System.InvalidOperationException(
                        $"[Robot] ISceneObjectFactory was not injected before creating ClanIcon for bot {botId}."));
            clanGo.transform.SetParent(_transform, worldPositionStays: false);
            _clanTransform = clanGo.transform;
            _clanTransform.localScale = Vector3.one * 0.8f;
        }

        return _clanTransform;
    }

    public void Destroy()
    {
        _aura?.Destroy();
        _aura = null;
        _entityBatchRenderer?.UnregisterSprite(_bodyBatchHandle);
        _entityBatchRenderer?.UnregisterSprite(_clanBatchHandle);
        _bodyBatchHandle = null;
        _clanBatchHandle = null;

        if (_skinSprite != null)
        {
            Object.Destroy(_skinSprite);
            _skinSprite = null;
        }

        if (_clanSprite != null)
        {
            Object.Destroy(_clanSprite);
            _clanSprite = null;
        }

        ClearTentacles();
    }
}
