#nullable enable

using System;
using Kern.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI;

public class Tooltip
{
    // Два уровня: обёртка (TemplateContainer) держит позицию в корне документа
    // и порядок отрисовки, внутренняя панель — видимость (класс is-hidden в
    // UXML висит именно на ней) и координаты у курсора.
    private VisualElement? _tooltipLayer;
    private VisualElement? _tooltipPanel;
    private Label? _tooltipLabel;
    private bool _isVisible;

    public void Initialize(UIDocument doc)
    {
        // Статическая структура (панель + лейбл) живёт в Tooltip.uxml;
        // здесь только клон и биндинг. Позиция и видимость — рантайм-состояние.
        VisualTreeAsset template = Resources.Load<VisualTreeAsset>(
            ProjectRuntimeContracts.ResourcePaths.TooltipUxml) ??
            throw new InvalidOperationException(
                "[Tooltip] Resources/UI/Overlays/Tooltip.uxml is required.");
        TemplateContainer tree = template.Instantiate();
        _tooltipLayer = tree;

        _tooltipPanel = tree.Q<VisualElement>("Tooltip") ??
            throw new InvalidOperationException(
                "[Tooltip] Tooltip panel is missing from Tooltip.uxml.");
        _tooltipLabel = tree.Q<Label>("TooltipLabel") ??
            throw new InvalidOperationException(
                "[Tooltip] TooltipLabel is missing from Tooltip.uxml.");

        doc.rootVisualElement.Add(tree);
    }

    public void Show(string text, Vector2 screenPos)
    {
        if (_tooltipLayer == null || _tooltipPanel == null || _tooltipLabel == null)
        {
            return;
        }

        _tooltipLabel.text = text;
        UIState.Show(_tooltipPanel);
        // Тултипы создаются в разном порядке относительно окон (тултип
        // палитры программатора — раньше её дерева), поэтому без подъёма
        // элемент рисуется под панелью и невидим.
        _tooltipLayer.BringToFront();
        _tooltipPanel.style.left = screenPos.x + 12;
        _tooltipPanel.style.top = screenPos.y + 12;
        _isVisible = true;
    }

    public void Hide()
    {
        if (_tooltipPanel == null || !_isVisible)
        {
            return;
        }

        UIState.Hide(_tooltipPanel);
        _isVisible = false;
    }

    public void UpdatePosition(Vector2 screenPos)
    {
        if (!_isVisible || _tooltipPanel == null)
        {
            return;
        }

        _tooltipPanel.style.left = screenPos.x + 12;
        _tooltipPanel.style.top = screenPos.y + 12;
    }

    public static void AttachTo(VisualElement element, Func<string> textProvider, Tooltip? tooltip)
    {
        if (tooltip == null)
        {
            return;
        }

        element.RegisterCallback<MouseEnterEvent>(evt =>
        {
            var screenPos = evt.mousePosition;
            tooltip.Show(textProvider(), screenPos);
        });

        element.RegisterCallback<MouseMoveEvent>(evt =>
        {
            tooltip.UpdatePosition(evt.mousePosition);
        });

        element.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            tooltip.Hide();
        });
    }
}
