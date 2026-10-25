#nullable enable

using MinesServer.Networking.Server.Packets.GUI.Components.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI.Builders;
public class SliderPacketBuilder : PacketUIBuilderBase<SliderPacket>
{
    protected override VisualElement BuildTyped(SliderPacket packet, PacketUIBuilder builder)
    {
        var slider = new Slider(packet.MinValue, packet.MaxValue)
        {
            name = packet.Name,
            value = Mathf.Clamp(packet.DefaultValue, packet.MinValue, packet.MaxValue),
        };
        slider.SetEnabled(packet.IsEnabled);

        if (packet.Step > 0f)
        {
            slider.RegisterValueChangedCallback(change =>
            {
                float steps = Mathf.Round((change.newValue - packet.MinValue) / packet.Step);
                float snapped = Mathf.Clamp(
                    packet.MinValue + steps * packet.Step,
                    packet.MinValue,
                    packet.MaxValue);
                if (!Mathf.Approximately(change.newValue, snapped))
                {
                    slider.SetValueWithoutNotify(snapped);
                }
            });
        }

        // Вид гасится правилами .packet-slider в SciFi.uss: сервер прислал
        // свой ползунок, стандартную отрисовку Unity надо убрать из-под него.
        slider.AddToClassList("packet-slider");

        // Name обязателен: ClickContextResolver.CollectInputValues собирает
        // значения контролов по element.name и отдаёт их в ElementClickPacket.
        // Ставится до любого раннего выхода — значение нулевого слайдера тоже
        // уходит с кликом кнопки.
        if (!string.IsNullOrEmpty(packet.Name))
        {
            slider.name = packet.Name;
        }

        // Кругляш-ручка виден только у живого слайдера (MaxValue > MinValue):
        // строка с нулевым диапазоном (кристаллов нет — двигать нечего)
        // остаётся голой линией (репорт: ручка нужна, если кристаллов > 0).
        VisualElement? dragger = slider.Q(className: "unity-base-slider__dragger");
        if (dragger == null)
        {
            Debug.LogWarning("[PacketUI] Slider dragger is unavailable in this UI Toolkit version.");
            return slider;
        }

        // Дорожка по умолчанию: тонкая линия во всю ширину слайдера. Кладётся
        // первым ребёнком, чтобы ручка с кнобом рисовались поверх неё. Свой
        // элемент, а не стандартный __tracker: позицию и цвет линии задаём
        // сами в SciFi.uss и не зависим от внутренней геометрии темы Unity.
        // Без линии виден один кноб — по набору пикселей слайдер не читается
        // (эталон вида — слайдеры legacy-клиента). Нужна обоим веткам ниже:
        // у нулевого слайдера линия — весь его вид.
        var track = new VisualElement();
        track.AddToClassList("packet-slider__track");
        track.pickingMode = PickingMode.Ignore;
        slider.Insert(0, track);

        if (packet.MaxValue > packet.MinValue)
        {
            slider.AddToClassList("packet-slider--filled");
        }
        else
        {
            // Нулевой диапазон: ручка не просто прозрачна, а мертва для
            // указателя. Прозрачный бокс остаётся зоной захвата, и тема
            // подсвечивает его при наведении квадратом; сам бокс при делении
            // на нулевой диапазон прибивает к правому концу линии (репорт:
            // «на слайдере можно навести в правый конец и там выделится
            // квадратик»). Дорожка живёт как у всех — наведение ярчит её
            // контур, двигать всё равно нечего.
            dragger.style.display = DisplayStyle.None;
            dragger.pickingMode = PickingMode.Ignore;
            return slider;
        }

        dragger.Clear();
        VisualElement knob = builder.Build(packet.Knob);
        // Класс для USS-зажима размера: инлайн-ширина из пакета иначе вылезает
        // за зону захвата ручки (см. .packet-slider__knob в SciFi.uss). Ручка
        // без арта пуста и невидима — резервного кругляша у слайдера нет.
        knob.AddToClassList("packet-slider__knob");
        dragger.Add(knob);
        return slider;
    }
}
