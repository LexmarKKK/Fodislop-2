#nullable enable

using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using UnityEngine.UIElements;

namespace Kern.UI.ServerWindows;
public class PanelPacketBuilder : PacketUIBuilderBase<PanelPacket>
{
    protected override VisualElement BuildTyped(PanelPacket packet, PacketUIBuilder builder)
    {
        var element = new VisualElement();
        element.AddToClassList("sci-fi-panel");

        // Некликабельная панель не ловит мышь: рамка выбранного слота лежит
        // поверх кликабельной иконки и не должна съедать её клик.
        if (string.IsNullOrEmpty(packet.OnClickContext))
        {
            element.pickingMode = PickingMode.Ignore;
        }

        return element;
    }
}
