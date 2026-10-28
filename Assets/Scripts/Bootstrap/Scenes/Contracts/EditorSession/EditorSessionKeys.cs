#nullable enable

namespace Kern.Core;

public static class EditorSessionKeys
{
    // Сцена, из которой нажали Play: редактор кладёт её сюда, Bootstrap забирает.
    public const string PlayModeTargetScene = "Kern.PlayModeTargetScene";
}
