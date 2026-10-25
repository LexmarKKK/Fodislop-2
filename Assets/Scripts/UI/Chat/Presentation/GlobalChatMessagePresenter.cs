#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI;

internal sealed class GlobalChatMessagePresenter
{
    private readonly ChatMessageHistory _history = new();
    private ScrollView? _scrollView;
    private Action<int>? _messageClicked;

    // Строку сообщения можно кликнуть (как в легаси-чате): клик по чужой строке
    // открывает серверное окно отправителя (кнопка «Личное сообщение»).
    public void Attach(
        ScrollView? scrollView,
        Action<int>? messageClicked = null)
    {
        _scrollView = scrollView;
        _messageClicked = messageClicked;
        Render();
    }

    public void Detach()
    {
        _scrollView = null;
        _messageClicked = null;
    }

    public void Add(string formattedMessage, int playerId = 0)
    {
        _history.Add(formattedMessage, playerId);
        AppendVisible(formattedMessage, playerId);
    }

    public void Clear()
    {
        _history.Clear();
        Render();
    }

    public void Render()
    {
        if (_scrollView == null)
        {
            return;
        }

        _scrollView.Clear();
        foreach ((string formatted, int playerId) in _history.GetMessages())
        {
            AppendVisible(formatted, playerId);
        }
    }

    private void AppendVisible(string formattedMessage, int playerId)
    {
        if (_scrollView == null)
        {
            return;
        }

        VisualElement line;
        if (playerId > 0 && _messageClicked != null)
        {
            Action<int> clicked = _messageClicked;
            var wrapper = new VisualElement();
            wrapper.AddToClassList("gchat-message--clickable");
            wrapper.RegisterCallback<ClickEvent>(_ => clicked(playerId));
            _scrollView.Add(wrapper);
            line = wrapper;
        }
        else
        {
            line = _scrollView;
        }

        var label = new Label(formattedMessage);
        label.AddToClassList("gchat-message");
        line.Add(label);
        while (_scrollView.childCount > ChatMessageHistory.MaxMessages)
        {
            _scrollView.RemoveAt(0);
        }

        _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
    }
}
