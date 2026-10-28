#nullable enable

using System;


namespace Kern.Core;

[Serializable]
public sealed class ConnectionSettings
{
    [SettingUnbounded("Тумблер штатной заглушки транспорта.")]
    [SettingConsumer(SettingConsumerTarget.NetworkService, "ConnectionManager transport selector")]
    public bool UseDummyConnection = ClientConfigurationDefaults.DefaultUseDummyConnection;

    [SettingUnbounded("Адрес сервера — строка, а не величина.")]
    [SettingConsumer(SettingConsumerTarget.NetworkService, "ConnectionManager host endpoint")]
    public string ServerHost = ClientConfigurationDefaults.DefaultServerHost;

    [SettingRange(1f, 65535f)]
    [SettingConsumer(SettingConsumerTarget.NetworkService, "ConnectionManager port endpoint")]
    public int ServerPort = ClientConfigurationDefaults.DefaultServerPort;

    [SettingRange(1f, 65535f)]
    [SettingConsumer(SettingConsumerTarget.NetworkService, "NetworkStatusEndpoint.FromSettings")]
    public int ApiPort = ClientConfigurationDefaults.DefaultApiPort;
}
