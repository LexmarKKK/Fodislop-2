#nullable enable

using System.Collections.Generic;
using Kern.Effects;

namespace Kern.Tests.Effects;

internal sealed class RecordingEffectSink : IEffectSpriteSink
{
    private readonly List<EffectSpriteDraw> _draws = new();

    public IReadOnlyList<EffectSpriteDraw> Draws => _draws;

    public void Draw(in EffectSpriteDraw sprite) => _draws.Add(sprite);

    public void Clear() => _draws.Clear();
}
