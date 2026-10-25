#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI;

internal sealed class GlobalChatMessagePresenter
{
    // Порог «игрок у дна»: ~две строки сообщений, прощает дробные раскладки
    // и мелкий недокрут после предыдущей автопрокрутки.
    private const float BottomStickThreshold = 32f;

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

        // Липкое дно, как в старом клиенте (fixScroll): автопрокрутка только
        // если игрок и так был внизу. Позиция читается ДО добавления, по
        // последней завершённой раскладке; прокрутившего историю вверх игрока
        // к дну не дёргаем.
        bool stickToBottom = IsAtBottom();

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

        // ScrollTo при незавершённой раскладке сам откладывает прокрутку тик
        // за тиком до устояния layout (прямой scrollOffset клампится слайдером
        // по ещё не обновлённым highLimit и залипает) и отменяется, если
        // игрок начал скроллить сам.
        if (stickToBottom)
        {
            _scrollView.ScrollTo(label);
        }
    }

    private bool IsAtBottom()
    {
        if (_scrollView?.contentViewport == null)
        {
            return true;
        }

        float maxScroll = Mathf.Max(
            0f,
            _scrollView.contentContainer.layout.height - _scrollView.contentViewport.layout.height);
        return _scrollView.scrollOffset.y >= maxScroll - BottomStickThreshold;
    }

    // Принудительный доскролл к последнему сообщению: при открытии окна и
    // после загрузки истории. Сообщения, пришедшие в скрытое окно (при заходе
    // на сервер чат закрыт), прокрутить нельзя - layout для display: none не
    // считается, поэтому окно доскролливает при показе. Deferred-механизм
    // ScrollTo сам дождётся, когда раскладка устоится.
    public void ScrollToBottom()
    {
        if (_scrollView == null)
        {
            return;
        }

        VisualElement content = _scrollView.contentContainer;
        if (content.childCount == 0)
        {
            return;
        }

        _scrollView.ScrollTo(content[content.childCount - 1]);
    }
}
