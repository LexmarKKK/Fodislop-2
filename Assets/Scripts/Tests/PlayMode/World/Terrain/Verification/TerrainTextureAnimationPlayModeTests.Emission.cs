#nullable enable

using System;
using System.Collections;
using Kern.AssetPipeline.Loading;
using Kern.World;
using Kern.World.Terrain;
using MinesServer.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Kern.Tests.PlayMode;

public sealed partial class TerrainTextureAnimationPlayModeTests
{
    [UnityTest]
    public IEnumerator ProductionSurfaceEmission_PreservesPrismaticAndBlinkingAnimation()
    {
        Material material = Own(new Material(Shader.Find(TerrainShaderName)));
        Texture2D source = CreateSolidTexture(new Color(0.8f, 0.8f, 0.8f, 1f), 32);
        material.SetTexture("_TerrainAtlas0", source);
        material.SetTexture("_TerrainDecalAtlas", CreateSolidTexture(Color.clear));
        material.SetTexture("_TerrainDecalRockAtlas", CreateSolidTexture(Color.clear));
        material.SetTexture("_PrismaticFlowMap", CreateSolidTexture(Color.red));
        material.SetFloat("_GroundDecalStrength", 0f);
        material.SetFloat("_RockDecalStrength", 0f);
        material.SetFloat("_PrismaticPhaseSpeed", 1f);
        material.SetFloat("_BlinkingSpeedScale", 1f);
        material.SetFloat("_SurfaceEmissionStrength", 1f);
        material.SetColor("_PrismaticTintA", new Color(0.2f, 1f, 0.2f, 1f));
        material.SetFloat("_TerrainRimEnabled", 0f);
        Mesh mesh = Own(CreateTerrainAnimationQuad());
        RenderTexture target = Own(new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear));
        Assert.That(target.Create(), Is.True);
        _readback = Own(RuntimeTextureFactory.CreateRGBAHalfNoMip(
            1, 1, "AnimatedEmission", RuntimeTextureColorSpace.Linear, FilterMode.Point, TextureWrapMode.Clamp));
        Texture? previousLight = Shader.GetGlobalTexture("_WorldLightTexture");
        Vector4 previousRect = Shader.GetGlobalVector("_WorldLightRect");
        Vector4 previousSize = Shader.GetGlobalVector("_WorldLightTextureSize");
        try
        {
            // Zero incident light isolates self emission in the actual visible pass.
            Shader.EnableKeyword("KERN_WORLD_LIGHTING");
            Shader.SetGlobalTexture("_WorldLightTexture", Texture2D.blackTexture);
            Shader.SetGlobalVector("_WorldLightRect", new Vector4(0, 0, 2, 2));
            Shader.SetGlobalVector("_WorldLightTextureSize", Vector4.one);
            foreach (CellSurfaceEffect effect in new[] { CellSurfaceEffect.Prismatic, CellSurfaceEffect.Plain })
            {
                _cellData?.Dispose();
                UploadSingleCell(new TerrainTypeFields(
                    Type: CellType.Green,
                    Block: new BlockDefinition(DrawLayer: CellDrawLayer.Background, Emission: true,
                        Outline: CellOutline.Pliant, TextureAnchor: CellTextureAnchor.Cell,
                        ShaderAnimation: effect == CellSurfaceEffect.Plain ? CellAnimationType.Blinking : CellAnimationType.None,
                        FrameAnimationSpeed: 10f, SurfaceEffect: effect, SurfaceEffectPalette: 1,
                        DecalAtlas: CellDecalAtlas.None, ReliefGroup: 0, MapColor: default),
                    Slot: 0, AtlasRect: new Vector4(0, 0, 1, 1), TileSize: 1f,
                    FrameCount: 1, FrameHeightTiles: 1f, OpaqueOwn: true, OpaqueAny: true,
                    HasTileGroup: false, TileGroupId: 0), 0, 1);
                float smallest = float.MaxValue;
                float largest = 0f;
                for (int frame = 0; frame < 10; frame++)
                {
                    Color actual = RenderAndReadCenter(mesh, material, material.FindPass("Universal2D"), target);
                    double phase = Time.time * 10.0;
                    Color expected;
                    if (effect == CellSurfaceEffect.Prismatic)
                    {
                        // Independent original gamma-domain oracle: red flow has hue 0.
                        double wave = (Math.Sin(-phase) + 1.0) / 2.0;
                        double body = wave * wave * wave;
                        double tint = body * 0.2 * 0.2 * 0.2;
                        expected = new Color((float)(0.8 * (1.0 - body) + 0.2 * tint),
                            (float)(0.8 * (1.0 - body) + tint),
                            (float)(0.8 * (1.0 - body) + 0.2 * tint)).linear;
                    }
                    else
                    {
                        // Cell hash at (0,0) is zero, so blinking phase offset is zero.
                        expected = new Color(0.8f, 0.8f, 0.8f).linear * (float)((1.0 + Math.Sin(phase)) / 2.0);
                    }

                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.025f), $"{effect}, phase={phase}");
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.025f), $"{effect}, phase={phase}");
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.025f), $"{effect}, phase={phase}");
                    smallest = Mathf.Min(smallest, actual.g);
                    largest = Mathf.Max(largest, actual.g);
                    yield return new WaitForSeconds(0.05f);
                }

                Assert.That(largest - smallest, Is.GreaterThan(0.1f), $"{effect}: self emission must animate.");
            }
        }
        finally
        {
            Shader.SetGlobalTexture("_WorldLightTexture", previousLight);
            Shader.SetGlobalVector("_WorldLightRect", previousRect);
            Shader.SetGlobalVector("_WorldLightTextureSize", previousSize);
        }
    }
}
