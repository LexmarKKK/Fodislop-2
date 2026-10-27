#nullable enable

namespace Kern.Core;

public static class ChatProtocolLimits
{
    public const int MaximumGlobalChatLength = 256;
    public const int MaximumLocalChatLength = 256;

    // Тег глобального канала обязан совпадать с тем, который сервер отдаёт
    // в ChatMessageListPacket.Tag и ищет в QueryChatHistoryPacket.Tag.
    // Сервер засевает единственный глобальный канал с Tag = "FED"
    // (World.CheckGlobalChats) и шлёт ответы с этим же тегом, поэтому
    // клиент, запрашивающий историю под другим тегом, получает отказ по
    // фильтру и остаётся с пустым окном чата.
    public const string GlobalChannelTag = "FED";
}
