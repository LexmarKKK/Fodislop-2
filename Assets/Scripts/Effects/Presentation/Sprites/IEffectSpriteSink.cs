#nullable enable

namespace Kern.Effects;

/// <summary>Receives simulated sprites. A renderer adapter implements this over the game's sprite batch.</summary>
public interface IEffectSpriteSink
{
    void Draw(in EffectSpriteDraw sprite);
}
