#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI;

internal sealed class GlobalChatMessagePresenter
{
    private readonly ChatMessageHistory _history = new();
    private ScrollView? _scrollView;
    private Action<long>? _messageClicked;

    // Строку сообщения можно кликнуть (как в легаси-чате): клик по чужой строке
    // открывает серверное окно отправителя (кнопка «Личное сообщение»).
    // По ревью PR передаётся MessageId — отправитель резолвится сервером
    // по идентификатору сообщения, а не по PlayerId.
    public void Attach(
        ScrollView? scrollView,
        Action<long>? messageClicked = null)
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

    public void Add(string formattedMessage, long messageId = 0)
    {
        _history.Add(formattedMessage, messageId);
        AppendVisible(formattedMessage, messageId);
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
        foreach ((string formatted, long messageId) in _history.GetMessages())
        {
            AppendVisible(formatted, messageId);
        }
    }

    private void AppendVisible(string formattedMessage, long messageId)
    {
        if (_scrollView == null)
        {
            return;
        }

        VisualElement line;
        if (messageId > 0 && _messageClicked != null)
        {
            Action<long> clicked = _messageClicked;
            var wrapper = new VisualElement();
            wrapper.AddToClassList("gchat-message--clickable");
            wrapper.RegisterCallback<ClickEvent>(_ => clicked(messageId));
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
