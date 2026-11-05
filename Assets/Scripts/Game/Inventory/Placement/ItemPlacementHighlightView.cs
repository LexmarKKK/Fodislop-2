#nullable enable

using System;
using Kern.Core.Interfaces;
using Kern.Core.Models;
using Kern.World;
using MinesServer.Data;
using UnityEngine;

namespace Kern.Game.Inventory;

/// <summary>
/// Превью установки выбранного предмета: сетка клеток перед роботом по
/// геометрии из SelectItemPacket (18). Клетки переводятся в мировые
/// координаты через WorldCoordinateConverter (инвариант проекта: Unity-Y
/// перевёрнут относительно серверной) и рисуются на Z-слое робота unlit-
/// материалом (как клик-маршрут), чтобы читаться в тёмных глубинах.
/// Предметы с нулевой геометрией (бомбы) превью не имеют; дороги паков
/// сервер в битмаску не включает — голограмма показывает только здание.
/// </summary>
public sealed class ItemPlacementHighlightView : MonoBehaviour
{
    // Чуть ближе к камере, чем робот (тот же сдвиг, что у клик-маршрута),
    // чтобы клетки читались поверх террейна, но не перекрывали робота.
    private const float ZOffset = -0.05f;
    // Самая крупная сетка пака — Market 9×9=72 клетки без дорог; запас до 128.
    private const int MaxCells = 128;
    private const float CellGap = 0.9f;
    private static readonly Color CellColor = new(0.35f, 0.85f, 0.4f, 0.5f);

    private IInventoryModel? _model;
    private ILocalPlayerState? _localPlayer;
    private IMapDataProvider? _map;
    private readonly SpriteRenderer[] _cells = new SpriteRenderer[MaxCells];
    private ItemPlacement? _drawnPlacement;
    private Vector2Int _drawnOrigin;
    private Direction _drawnDirection;

    public void Bind(IInventoryModel model, ILocalPlayerState localPlayer, IMapDataProvider map)
    {
        _model = model;
        _localPlayer = localPlayer;
        _map = map;
        _model.OnPlacementChanged += MarkDirty;
        _model.OnSelectedChanged += HandleSelectedChanged;
        Refresh();
    }

    private void Awake()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        Sprite cellSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        Material? cellMaterial = null;
        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit != null)
        {
            cellMaterial = new Material(unlit) { color = CellColor };
        }

        for (int index = 0; index < _cells.Length; index++)
        {
            var cellObject = new GameObject($"Cell{index}");
            cellObject.transform.SetParent(transform, false);
            cellObject.transform.localScale = new Vector3(CellGap, CellGap, 1f);
            SpriteRenderer renderer = cellObject.AddComponent<SpriteRenderer>();
            renderer.sprite = cellSprite;
            if (cellMaterial != null)
            {
                renderer.sharedMaterial = cellMaterial;
            }

            renderer.color = CellColor;
            renderer.enabled = false;
            _cells[index] = renderer;
        }
    }

    private void OnDestroy()
    {
        if (_model != null)
        {
            _model.OnPlacementChanged -= MarkDirty;
            _model.OnSelectedChanged -= HandleSelectedChanged;
        }
    }

    // Направление робота меняется и без перемещения: обновляем каждый кадр,
    // перерисовка срабатывает только при реальном изменении входов.
    private void Update() => Refresh();

    private void MarkDirty() => Refresh();

    private void HandleSelectedChanged(ItemType? _) => Refresh();

    private void Refresh()
    {
        ItemPlacement? placement = _model?.Placement;
        ILocalPlayer? localPlayer = _localPlayer?.Current;
        if (placement is null || localPlayer is null || !localPlayer.HasServerPosition || _map is null)
        {
            if (_drawnPlacement != null)
            {
                HideAll();
                _drawnPlacement = null;
            }

            return;
        }

        Vector2Int origin = localPlayer.Position;
        Direction direction = localPlayer.LastDirection;
        if (placement == _drawnPlacement && origin == _drawnOrigin && direction == _drawnDirection)
        {
            return;
        }

        _drawnPlacement = placement;
        _drawnOrigin = origin;
        _drawnDirection = direction;
        Draw(placement, localPlayer, origin, direction);
    }

    private void Draw(ItemPlacement placement, ILocalPlayer localPlayer, Vector2Int origin, Direction direction)
    {
        // Точка установки (якорь) — на Distance перед роботом по фактическому
        // взгляду (ровно так сервер считает DirectionalCoords при установке).
        // Данных якоря внутри сетки пакет не несёт, а в items.json он у всех
        // паков близок к центру, поэтому сетка центрируется на точке якоря.
        // Rotating=false (весь текущий контент): сетка рисуется в канонической
        // ориентации «на север», как в Rows items.json — сервер паки не вращает.
        int worldHeight = _map!.WorldHeight;
        float viewZ = localPlayer.transform.position.z + ZOffset;
        Vector2Int facing = Facing(direction);
        int centerX = (placement.Width - 1) / 2;
        int centerY = (placement.Height - 1) / 2;
        // Плоский цикл по всем слотам: у меньшей сетки слоты за её пределами
        // обязаны гаситься, иначе после крупного пака остаются призрачные клетки.
        for (int flat = 0; flat < _cells.Length; flat++)
        {
            SpriteRenderer renderer = _cells[flat];
            bool active = flat < placement.Cells.Count && placement.Cells[flat];
            if (!active)
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                }

                continue;
            }

            int row = flat / placement.Width;
            int column = flat % placement.Width;
            Vector2Int local = placement.Rotating
                ? RotateWithFacing(column - centerX, row - centerY, facing)
                : new Vector2Int(column - centerX, row - centerY);
            Vector2Int serverCell = origin + facing * placement.Distance + local;
            renderer.transform.position =
                WorldCoordinateConverter.ServerToUnityPos(serverCell.x, serverCell.y, worldHeight, viewZ);
            if (!renderer.enabled)
            {
                renderer.enabled = true;
            }
        }
    }

    // Поворот локального смещения (вправо, вперёд-от-робота) под направление
    // взгляда: вперёд ложится на вектор взгляда, вправо — на правую руку
    // (поворот вектора взгляда на 90° по часовой в экранных координатах).
    private static Vector2Int RotateWithFacing(int rightOffset, int forwardOffset, Vector2Int facing) =>
        new(
            facing.x * forwardOffset - facing.y * rightOffset,
            facing.y * forwardOffset + facing.x * rightOffset);

    private static Vector2Int Facing(Direction direction) => direction switch
    {
        Direction.Up => new Vector2Int(0, -1),
        Direction.Down => new Vector2Int(0, 1),
        Direction.Left => new Vector2Int(-1, 0),
        Direction.Right => new Vector2Int(1, 0),
        _ => new Vector2Int(0, 1),
    };

    private void HideAll()
    {
        foreach (SpriteRenderer renderer in _cells)
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
            }
        }
    }
}
