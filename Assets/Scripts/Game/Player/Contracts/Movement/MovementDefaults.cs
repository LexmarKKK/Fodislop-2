#nullable enable

namespace Kern.Core;

public static class MovementDefaults
{
    public const float RobotMoveSpeed = 15f;

    // Минимальный интервал между RotatePacket: поворот отправляется сразу
    // при смене направления ввода, а этот пол лишь гасит дрожь
    // аналогового ввода (мышь/стик) у диагонали квантования направлений,
    // чтобы не флудить сервером пакетами туда-обратно.
    public const float MinRotateInterval = 0.1f;
}
