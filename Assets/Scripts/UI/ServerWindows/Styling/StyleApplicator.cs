#nullable enable

using MinesServer.Networking.Server.Packets.GUI;
using MinesServer.Networking.Server.Packets.GUI.Components;
using UnityEngine;
using UnityEngine.UIElements;

using Kern.UI.Controls;

namespace Kern.UI.ServerWindows;

public static class StyleApplicator
{
    public static void ApplyStyles(VisualElement element, IGUIComponentPacket packet)
    {
        if (packet.Style is null)
        {
            return;
        }

        // UILine красит себя сам по GUIStylePacket.Background (см.
        // LinePacketBuilder): фоновая заливка бокса здесь превратила бы
        // линию в залитый прямоугольник.
        if (element is UILine)
        {
            return;
        }

        var style = packet.Style.Value;

        // Заливка фона — цвет линии, плашки уровня, подложки карточек.
        // Прозрачный (не заданный сервером) цвет не перекрывает тему USS.
        if (style.Background.A > 0)
        {
            element.style.backgroundColor = ToUnityColor(style.Background);
        }

        if (style.BorderWidth > 0)
        {
            element.style.borderTopWidth = style.BorderWidth;
            element.style.borderBottomWidth = style.BorderWidth;
            element.style.borderLeftWidth = style.BorderWidth;
            element.style.borderRightWidth = style.BorderWidth;

            if (style.Border.A > 0)
            {
                Color border = ToUnityColor(style.Border);
                element.style.borderTopColor = border;
                element.style.borderBottomColor = border;
                element.style.borderLeftColor = border;
                element.style.borderRightColor = border;
            }
        }

        ApplyMargins(style.Margin,
            left => element.style.marginLeft = left,
            top => element.style.marginTop = top,
            right => element.style.marginRight = right,
            bottom => element.style.marginBottom = bottom);

        ApplyMargins(style.Padding,
            left => element.style.paddingLeft = left,
            top => element.style.paddingTop = top,
            right => element.style.paddingRight = right,
            bottom => element.style.paddingBottom = bottom);
    }

    private static Color ToUnityColor(System.Drawing.Color color) =>
        new Color32(color.R, color.G, color.B, color.A);

    private static void ApplyMargins(
        Margins margins,
        System.Action<int> left,
        System.Action<int> top,
        System.Action<int> right,
        System.Action<int> bottom)
    {
        left(margins.Left);
        top(margins.Top);
        right(margins.Right);
        bottom(margins.Bottom);
    }
}
