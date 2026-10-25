#nullable enable

using System.Collections.Generic;

namespace Kern.UI;

internal sealed class ChatMessageHistory
{
    public const int MaxMessages = 20;

    private readonly List<(string Formatted, int PlayerId)> _messages = new();

    public void Add(string formattedMessage, int playerId = 0)
    {
        _messages.Add((formattedMessage, playerId));
        while (_messages.Count > MaxMessages)
        {
            _messages.RemoveAt(0);
        }
    }

    public void Clear()
    {
        _messages.Clear();
    }

    public IReadOnlyList<(string Formatted, int PlayerId)> GetMessages() => _messages;
}
