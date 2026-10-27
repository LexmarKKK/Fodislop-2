#nullable enable

using System;
using Kern.Core;
using Kern.Networking.Connection;


namespace Kern.Networking.Status;

public static class NetworkStatusEndpoint
{
    public static string FromSettings(ConnectionSettings settings) =>
        new UriBuilder(Uri.UriSchemeHttp, settings.ServerHost, settings.ApiPort,
            "api/status/online").Uri.AbsoluteUri;
}
