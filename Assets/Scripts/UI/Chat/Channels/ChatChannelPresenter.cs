#nullable enable

using Kern.Core.Localization;
using UnityEngine.UIElements;

namespace Kern.UI.Chat;

internal static class ChatChannelPresenter
{
    // Заголовок ленты — имя активного чата; дефолт — «Глобальный» (FED),
    // для остальных каналов/приватов берётся серверное имя из ChatListPacket.
    public static void UpdatePresentation(
        Label? header,
        Button? globalButton,
        ILocalizationService? loc,
        string? activeChatName = null)
    {
        if (header != null && loc != null)
        {
            header.text = string.IsNullOrEmpty(activeChatName)
                ? loc.Get("chat.channel.global")
                : activeChatName;
        }

        globalButton?.EnableInClassList("gchat-channel-button--active", false);
    }
}
