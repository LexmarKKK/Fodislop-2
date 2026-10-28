#nullable enable

using System;
using UnityEngine;

namespace Kern.AssetPipeline.Animation;

// Integral of straight linear RGB times coverage over world-cell area.
public static class SpriteEmissionIntegrator
{
    // Centroid is in local world-cell units, relative to the source rectangle center.
    public static SpriteEmissionFrame IntegrateFrame(Color32[] pixels, int textureWidth, RectInt rect, Vector2 worldSize)
    {
        if (textureWidth <= 0 || pixels.Length % textureWidth != 0 || rect.width <= 0 || rect.height <= 0 ||
            rect.x < 0 || rect.y < 0 || rect.xMax > textureWidth || rect.yMax > pixels.Length / textureWidth ||
            !float.IsFinite(worldSize.x) || !float.IsFinite(worldSize.y) || worldSize.x < 0f || worldSize.y < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(rect));
        }

        double r = 0, g = 0, b = 0, weightSum = 0, weightedX = 0, weightedY = 0;
        for (int y = rect.y; y < rect.yMax; y++)
        {
            for (int x = rect.x; x < rect.xMax; x++)
            {
                Color32 pixel = pixels[y * textureWidth + x];
                float alpha = pixel.a / 255f;
                float red = Mathf.GammaToLinearSpace(pixel.r / 255f) * alpha;
                float green = Mathf.GammaToLinearSpace(pixel.g / 255f) * alpha;
                float blue = Mathf.GammaToLinearSpace(pixel.b / 255f) * alpha;
                r += red; g += green; b += blue;
                double energy = red + green + blue;
                weightSum += energy;
                weightedX += energy * (x + 0.5);
                weightedY += energy * (y + 0.5);
            }
        }

        double weight = (double)worldSize.x * worldSize.y / ((double)rect.width * rect.height);
        Vector2 centroid = weightSum > 0
            ? new Vector2((float)(((weightedX / weightSum - rect.x) / rect.width - 0.5) * worldSize.x),
                (float)(((weightedY / weightSum - rect.y) / rect.height - 0.5) * worldSize.y))
            : Vector2.zero;
        return new SpriteEmissionFrame(
            new Color((float)(r * weight), (float)(g * weight), (float)(b * weight), 1f), centroid);
    }

    public static SpriteEmissionFrame[] Frames(Texture2D atlas, int width, int height, int count, float pixelsPerUnit)
    {
        if (!float.IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0f || width <= 0 || height <= 0 || count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));
        }

        Color32[] pixels = atlas.GetPixels32();
        var result = new SpriteEmissionFrame[count];
        int columns = atlas.width / width;
        if (columns <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
        var worldSize = new Vector2(width / pixelsPerUnit, height / pixelsPerUnit);
        for (int index = 0; index < count; index++)
        {
            result[index] = IntegrateFrame(pixels, atlas.width,
                new RectInt(index % columns * width, index / columns * height, width, height), worldSize);
        }
        return result;
    }
}
