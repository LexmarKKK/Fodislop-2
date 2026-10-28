#nullable enable

using Kern.Core.Interfaces;
using UnityEngine;
using Kern.Core.Lifecycle;


namespace Kern.Core.Rendering;

public sealed class GameplayCameraService : IGameplayCamera
{
    public Camera Camera { get; }

    public GameplayCameraService(Camera camera)
    {
        Camera = camera ?? throw new System.ArgumentNullException(nameof(camera));
    }
}
