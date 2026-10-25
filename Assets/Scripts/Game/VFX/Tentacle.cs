#nullable enable

using UnityEngine;

namespace Kern.Game;

/// <summary>
///     Одна прядь хвоста робота: симуляция делегирована <see cref="TailChain" />, запись
///     ленты и клешни — <see cref="TentacleGeometry" />.
/// </summary>
/// <remarks>
///     Прядь знает свой индекс: от него зависит инерция, поэтому прядь 0 самая длинная,
///     а остальные короче и лежат внутри её. Отладочные режимы хвоста гоняют цепочку в
///     рамке, смещённой от робота; смещение хранится здесь и вычитается при записи
///     геометрии, чтобы полоса оставалась приваренной к корпусу.
/// </remarks>
public class Tentacle
{
    private readonly WorldEntityBatchRenderer _renderer;
    private readonly Texture2D _texture;
    private readonly TailChain _chain;
    private readonly Vector3[] _points = new Vector3[TailChain.PointCount];
    private Vector3 _originOffset;
    private bool _isActive = true;

    public Tentacle(
        WorldEntityBatchRenderer renderer,
        Texture2D texture,
        Vector3 startPosition,
        int strandIndex)
    {
        _renderer = renderer;
        _texture = texture;
        _chain = new TailChain(strandIndex, startPosition);

        _renderer.Register(this, _texture);
    }

    public bool IsActive => _isActive;

    /// <summary>Точка крепления в мире робота: рамка симуляции вычтена, как и при записи меша.</summary>
    public Vector3 RootPosition => _chain[0] - _originOffset;

    /// <summary>Кончик пряди в мире робота; клешня сидит чуть дальше, так что это консервативная граница.</summary>
    public Vector3 TipPosition => _chain[TailChain.PointCount - 1] - _originOffset;

    /// <summary>Растяжение пряди: дистанция от кончика до корня в рамке симуляции — в той же, в какой корень приходит в <see cref="Update"/>.</summary>
    public float TipDistanceTo(Vector3 root) => _chain.TipDistanceTo(root);

    internal Texture2D Texture => _texture;

    public bool IsSettled => _chain.IsSettled;

    public void SetActive(bool active)
    {
        if (_isActive == active)
        {
            return;
        }

        _isActive = active;
        _renderer.MarkDirty(_texture);
    }

    public void Snap(Vector3 position)
    {
        // Снап обязан сбрасывать и накопленное смещение рамки симуляции: иначе корни
        // прядей тут же утаскиваются далеко от робота, и хвост рисуется полосой,
        // торчащей от корпуса.
        _chain.Snap(position);
        _originOffset = Vector3.zero;
        _renderer.MarkDirty(_texture);
    }

    /// <param name="stretch">Общее на все пряди растяжение, прочитанное владельцем с пряди ноль.</param>
    /// <param name="originOffset">Смещение рамки симуляции (отладочные режимы хвоста); вычитается при записи.</param>
    public void Update(Vector3 rootPosition, float movementFactor, float deltaTime, float stretch, Vector3 originOffset)
    {
        if (!_isActive)
        {
            return;
        }

        _originOffset = originOffset;
        _chain.Step(rootPosition, movementFactor, deltaTime, stretch);
        _renderer.MarkDirty(_texture);
    }

    public void WriteGeometry(Vector3[] verts, Vector2[] uvs, int vertBase, Rect atlasRect)
    {
        for (int i = 0; i < _points.Length; i++)
        {
            _points[i] = _chain[i];
        }

        // Геометрия пишется в мире робота: накопленное смещение рамки симуляции
        // вычитается, чтобы полоса не отрывалась от корпуса.
        TentacleGeometry.WriteVertices(_points, atlasRect, verts, uvs, vertBase, _originOffset);
    }

    public void Destroy()
    {
        if (_renderer != null)
        {
            _renderer.Unregister(this, _texture);
        }
    }
}
