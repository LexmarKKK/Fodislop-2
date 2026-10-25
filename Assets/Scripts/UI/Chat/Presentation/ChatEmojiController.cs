#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI;

// Пикер смайлов: кнопка рядом с выбором цвета открывает сетку; клик вставляет
// смайл в позицию каретки поля ввода. Рецепт — ChatColorController (кнопка +
// скрываемая сетка в нижнем ряду).
internal sealed class ChatEmojiController
{
    private readonly TextField? _input;
    private readonly VisualElement? _emojiGrid;

    // Сообщает, что попап открылся (GlobalChatUI закрывает им второй попап,
    // чтобы пикеры цвета и смайлов не висели друг на друге).
    public event Action? Opened;

    public ChatEmojiController(TextField? input, Button? emojiButton, VisualElement? emojiGrid)
    {
        _input = input;
        _emojiGrid = emojiGrid;

        if (emojiButton != null)
        {
            emojiButton.clicked += ToggleGrid;
            // Глиф смайла рисуется через фолбэк унаследованного лица (Exo2 →
            // системный emoji-шрифт) — тот же путь, которым смайлы работают
            // в поле ввода. Прямая подмена шрифта здесь не рендерится.
            emojiButton.text = "\U0001F642";
        }

        if (_emojiGrid != null)
        {
            _emojiGrid.style.display = DisplayStyle.None;
            foreach (string emoji in ChatEmojiCatalog.Emojis)
            {
                string captured = emoji;
                var button = new Button(() => Insert(captured)) { text = emoji };
                button.AddToClassList("gchat-emoji-item");
                _emojiGrid.Add(button);
            }
        }
    }

    private void ToggleGrid()
    {
        if (_emojiGrid == null)
        {
            return;
        }

        bool opening = _emojiGrid.style.display == DisplayStyle.None;
        _emojiGrid.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;
        if (opening)
        {
            Opened?.Invoke();
        }
    }

    private void Insert(string emoji)
    {
        if (_input == null)
        {
            return;
        }

        // Позиция вставки — у каретки; без фокуса индексы могут протухнуть,
        // поэтому клампим в длину значения (протухшее = последний ввод).
        string value = _input.value ?? string.Empty;
        int position = Mathf.Clamp(Mathf.Min(_input.cursorIndex, _input.selectIndex), 0, value.Length);

        string next = value.Insert(position, emoji);
        if (_input.maxLength > 0 && next.Length > _input.maxLength)
        {
            // Не влезает в лимит сообщения — молча пропускаем.
            return;
        }

        int caret = position + emoji.Length;
        _input.value = next;
        _input.Focus();
        _input.cursorIndex = caret;
        _input.selectIndex = caret;

        // Программный Focus и смена value пере-sync'ают каретку TextField
        // отложенно и затирают выставленную позицию: каретка откатывалась
        // к точке вставки (слева от смайла). Повторяем на следующем тике —
        // она гарантированно встаёт справа от вставленного.
        _input.schedule.Execute(() =>
        {
            _input.cursorIndex = caret;
            _input.selectIndex = caret;
        });
        CloseGrid();
    }

    public void CloseGrid()
    {
        if (_emojiGrid != null)
        {
            _emojiGrid.style.display = DisplayStyle.None;
        }
    }
}
