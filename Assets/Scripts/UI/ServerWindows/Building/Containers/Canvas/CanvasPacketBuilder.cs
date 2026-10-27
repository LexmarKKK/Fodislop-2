#nullable enable

using System;
using System.Globalization;
using MinesServer.Networking.Server.Packets.GUI.Components;
using MinesServer.Networking.Server.Packets.GUI.Components.Containers;
using UnityEngine.UIElements;

namespace Kern.UI.ServerWindows;
public class CanvasPacketBuilder : PacketUIBuilderBase<CanvasPacket>
{
    protected override VisualElement BuildTyped(CanvasPacket packet, PacketUIBuilder builder)
    {
        var element = new VisualElement();
        element.AddToClassList("rel");
        const string heightKey = "PacketUI.CanvasHeight";
        // В legacy css «canv-h=0» — легальное «высота по содержимому»: страница
        // рынка несёт ровно это значение. Раньше неположительная высота считалась
        // ошибкой пакета и роняла открытие всего окна вместе с сессией (репорт:
        // выкидывало в главное меню при заходе в маркет). Высоту по содержимому
        // канвас держит сам — minHeight по самому нижнему ребёнку (см. ниже).
        if (AttachedProperties.TryGetFloat(packet, heightKey, out float parsed) && parsed > 0f)
        {
            element.style.height = parsed;
        }

        builder.AddChildren(element, packet);

        // Дети канваса позиционируются абсолютно (Canvas.X/Y) и выпадают из
        // потока: без резерва высоты контейнер схлопывается и контент после
        // него наезжает. Держим minHeight по самому нижнему элементу.
        float bottom = 0f;
        foreach (IGUIComponentPacket child in packet.Children)
        {
            if (AttachedProperties.TryGetFloat(child, "Canvas.Y", out float y))
            {
                float childHeight = AttachedProperties.TryGetFloat(child, "Canvas.Height", out float h) ? h : 20f;
                bottom = Math.Max(bottom, y + childHeight);
            }
        }

        if (bottom > 0f)
        {
            element.style.minHeight = bottom;
        }

        return element;
    }
}
