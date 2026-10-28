#nullable enable

using MinesServer.Data;
using UnityEngine;

namespace Kern.World;

/// <summary>Тип клетки из cells.json: каждое поле — один ключ.</summary>
///
/// Имена и порядок совпадают с ключами cells.json; порядок — ход кадра:
/// слой, геометрия, текстура, цвет, декаль, кайма, свет, карта. Виртуальное
/// (тень и масса — от переднего плана, цвет блика — из альбедо) считает шейдер.
public readonly record struct BlockDefinition(
    // Слой отрисовки: передний план, фон или подложка под передним планом.
    CellDrawLayer DrawLayer,
    // Контур: гибкий, волнистый, жёсткий квадрат, капля, стена, угол, дверь.
    CellOutline Outline,
    // Откуда координаты текстуры: своя на клетку или общая по миру.
    CellTextureAnchor TextureAnchor,
    // Вид шейдерной анимации цвета: мигание, мерцание, радуга.
    CellAnimationType ShaderAnimation,
    // Кадров ленты текстуры в секунду.
    float FrameAnimationSpeed,
    // Эффект поверхности: расплав, грани, радужный кристалл.
    CellSurfaceEffect SurfaceEffect,
    // Палитра радужного эффекта поверхности.
    byte SurfaceEffectPalette,
    // Атлас декалей поверх клетки: без декалей, земля, камень.
    CellDecalAtlas DecalAtlas,
    // Масса для тёмной каймы по краю: 0 — без каймы; соседи одной ненулевой
    // массы — одно тело, между ними каймы нет.
    byte ReliefGroup,
    // Светится ли клетка.
    bool Emission,
    // Цвет клетки на карте.
    Color32 MapColor)
{
    // Передний план непроходим, фон проходим.
    public bool IsPassable => DrawLayer != CellDrawLayer.Foreground;
}
