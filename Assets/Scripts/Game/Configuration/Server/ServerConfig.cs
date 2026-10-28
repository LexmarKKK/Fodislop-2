#nullable enable

using System;
using Kern.Core;
using Kern.Core.Interfaces;

namespace Kern.Game.Configuration;

// Чистый сервис контейнера: ни рендера, ни transform (docs/architecture/SCENE_STANDARD.md §1).
public sealed class ServerConfig : IServerConfig
{
    public bool IsInitialized => true;

    public event Action? OnInitialized
    {
        add => value?.Invoke();
        remove { }
    }

    public float DigCooldown { get; } = GameplayActionDefaults.DefaultDigCooldown;

    public int MaxGlobalChatLength { get; } = ChatProtocolLimits.MaximumGlobalChatLength;

    public int MaxLocalChatLength { get; } = ChatProtocolLimits.MaximumLocalChatLength;
}
