#nullable enable

using System;
using Kern.Core;


namespace Kern.Diagnostics.Scenes;

public sealed class SceneContractException : InvalidOperationException
{
    public SceneContractException(string message)
        : base(message)
    {
    }

    public SceneContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
