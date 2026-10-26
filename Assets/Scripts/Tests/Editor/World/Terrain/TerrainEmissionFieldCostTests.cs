#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Kern.World;
using Kern.Core.Interfaces;
using Cysharp.Threading.Tasks;
using Kern.World.Terrain;
using MinesServer.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Kern.Tests.World;

[TestFixture]
[Category("GPU")]
public sealed class TerrainEmissionFieldCostTests
{
    [Test]
    public void ProductionEmission_ReportsDenseFieldRebuildCost() => RunSourceComparison("Green");

    [TestCase("Blue")]
    [TestCase("VolcanoBackground")]
    [TestCase("DeepRainbowRock")]
    public void ProductionEmission_ComparesAdditionalSourceSheets(string sourceName) => RunSourceComparison(sourceName);

    private static void RunSourceComparison(string sourceName)
    {
        CellType sourceType = Enum.Parse<CellType>(sourceName);
        const int Cells = 32;
        const int Repetitions = 8;
        var owned = new List<Object>();
        using var data = new TerrainCellBuffers();
        using var carrier = new TerrainCellIdMesh();
        try
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Custom/Terrain");
            Assert.That(shader, Is.Not.Null);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/Cells/{sourceName}.png");
            Assert.That(source, Is.Not.Null);
            // A disposable shader mutation reconstructs the previous single-
            // sample field output. It never changes the production shader file.
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "Shaders/Terrain/Terrain.shader"));
            int start = text.IndexOf("                float4 sourceEmission = 0.0;", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = text.IndexOf("                surfaceAlbedo *= TerrainRimBevel(surface);", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            text = text[..start] + "                float4 sourceEmission = float4(surfaceAlbedo, 1.0);\n" + text[end..];
            text = text.Replace("#include \"TerrainTileAddressing.hlsl\"",
                "#include \"Assets/Shaders/Terrain/TerrainTileAddressing.hlsl\"", StringComparison.Ordinal);
            text = text.Replace("Shader \"Universal Render Pipeline/Custom/Terrain\"",
                "Shader \"Hidden/Kern/EmissionSingleSampleBaseline\"", StringComparison.Ordinal);
            Shader baselineShader = ShaderUtil.CreateShaderAsset(text, compileInitialShaderVariants: false);
            owned.Add(baselineShader);
            Assert.That(baselineShader.isSupported, Is.True);
            var current = new Material(shader);
            var baseline = new Material(baselineShader);
            var adaptive = new Material(shader);
            var local = new Material(shader);
            var chroma = new Material(shader);
            owned.Add(local);
            owned.Add(chroma);
            owned.Add(adaptive);
            owned.Add(current);
            owned.Add(baseline);
            foreach (Material material in new[] { current, baseline, adaptive, local, chroma })
            {
                TerrainMaterialTuning.Apply(material, enableRim: false);
                // Keep the authored surface model but remove moving glints from
                // the controlled emission comparison. Runtime tuning is untouched.
                material.SetFloat("_FacetedGlintStrength", 0f);
                material.SetTexture("_TerrainAtlas0", source);
                material.SetTexture("_TerrainDecalAtlas", Texture2D.blackTexture);
                material.SetTexture("_TerrainDecalRockAtlas", Texture2D.blackTexture);
            }

            TerrainEmissionDebugState.Apply(current, TerrainEmissionMethod.FixedThreshold);
            TerrainEmissionDebugState.Apply(baseline, TerrainEmissionMethod.Original);
            using var adaptiveCache = PrepareCache(adaptive, TerrainEmissionMethod.AdaptiveTexture);
            using var localCache = PrepareCache(local, TerrainEmissionMethod.LocalDetail);
            using var chromaCache = PrepareCache(chroma, TerrainEmissionMethod.ChromaSeparation);
            data.EnsureCapacity(Cells, Cells);
            data.SetType(sourceType, TerrainCellData.PackType(new TerrainTypeFields(
                Type: sourceType, Block: BlockRegistry.Get(sourceType), Slot: 0,
                AtlasRect: new Vector4(0, 0, 1, 1), TileSize: 32f / source.width,
                FrameCount: 1, FrameHeightTiles: source.height / 32f,
                OpaqueOwn: true, OpaqueAny: true, HasTileGroup: false, TileGroupId: 0)));
            for (int y = 0; y < Cells; y++)
            {
                for (int x = 0; x < Cells; x++)
                {
                    data.SetCell(1184 + x, 40000 + y, TerrainCellData.PackCell(sourceType));
                }
            }

            data.MarkAllDirty();
            data.Apply();
            data.BindGlobals(1f, 1184, 40000, 65535, 65535, TerrainCellFormat.DistortionStyleOff);
            Shader.SetGlobalVector(TerrainCellBuffers.ViewOffsetId, Vector4.zero);
            carrier.EnsureSize(Cells, Cells, 1f);
            var materialField = new RenderTexture(Cells * 2, Cells * 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var glowField = new RenderTexture(Cells * 2, Cells * 2, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            owned.Add(materialField);
            owned.Add(glowField);
            Assert.That(materialField.Create(), Is.True);
            Assert.That(glowField.Create(), Is.True);
            var manager = new TerrainMeshManager();
            foreach (Material fieldMaterial in new[] { baseline, current, adaptive, local, chroma })
            {
                ShaderUtil.CompilePass(fieldMaterial, fieldMaterial.FindPass("LightingMaterialField"), forceSync: true);
                Assert.That(ShaderUtil.ShaderHasError(fieldMaterial.shader), Is.False);
            }
            using var oldCommands = Record(baseline);
            using var newCommands = Record(current);
            using var adaptiveCommands = Record(adaptive);
            using var localCommands = Record(local);
            using var chromaCommands = Record(chroma);
            Complete(oldCommands);
            Assert.That(ShaderUtil.ShaderHasError(baselineShader), Is.False);
            Complete(newCommands);
            Complete(adaptiveCommands);
            Complete(localCommands);
            Complete(chromaCommands);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var before = new double[3];
            var after = new double[3];
            var cached = new double[3];
            var localTimes = new double[3];
            var chromaTimes = new double[3];
            for (int run = 0; run < 3; run++)
            {
                before[run] = Complete(oldCommands);
                after[run] = Complete(newCommands);
                cached[run] = Complete(adaptiveCommands);
                localTimes[run] = Complete(localCommands);
                chromaTimes[run] = Complete(chromaCommands);
            }

            Array.Sort(before);
            Array.Sort(after);
            Array.Sort(cached);
            Array.Sort(localTimes);
            Array.Sort(chromaTimes);
            string report = FormattableString.Invariant(
                $"1024 dense {sourceName} cells, 2 field pixels/cell, 8 draws/batch; median completed CPU+GPU work per draw: single-sample={before[1]:F4} ms, fixed-threshold={after[1]:F4} ms, adaptive-cached={cached[1]:F4} ms, local-detail={localTimes[1]:F4} ms, chroma={chromaTimes[1]:F4} ms; fixed delta={after[1] - before[1]:F4} ms, adaptive delta={cached[1] - before[1]:F4} ms. Includes submission/readback synchronization, not a GPU-only timestamp.");
            TestContext.WriteLine(report);
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, $"kern-{sourceName}-emission-field-cost.txt"), report);
            Texture2D readback = RuntimeTextureFactory.CreateRGBAHalfNoMip(Cells * 2, Cells * 2,
                "GreenSourceEmissionField", RuntimeTextureColorSpace.Linear, FilterMode.Point, TextureWrapMode.Clamp);
            owned.Add(readback);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = glowField;
                readback.ReadPixels(new Rect(0, 0, Cells * 2, Cells * 2), 0, 0);
                readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            float green = 0f;
            float blue = 0f;
            foreach (Color pixel in readback.GetPixels())
            {
                green += pixel.g;
                blue += pixel.b;
            }
            if (sourceName == "Green")
            {
                Assert.That(green, Is.GreaterThan(blue * 2f), "The actual Green sheet must produce green emission.");
            }
            File.WriteAllBytes(Path.Combine(Application.temporaryCachePath, $"kern-{sourceName}-source-emission-field.png"), readback.EncodeToPNG());

            // Actual source-sheet camera captures isolate visible emission
            // from the transport multiplier without editing the imported art.
            GameObject meshObject = new("Emission source-sheet capture");
            GameObject cameraObject = new("Emission source-sheet camera");
            owned.Add(meshObject);
            owned.Add(cameraObject);
            meshObject.layer = 31;
            meshObject.AddComponent<MeshFilter>().sharedMesh = carrier.Mesh;
            MeshRenderer surfaceRenderer = meshObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = current;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(5f, 5f, -10f);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var surfaceTarget = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            owned.Add(surfaceTarget);
            Assert.That(surfaceTarget.Create(), Is.True);
            Texture2D surfacePixels = RuntimeTextureFactory.CreateRGBAHalfNoMip(320, 320,
                "GreenSourceSheetRadiance", RuntimeTextureColorSpace.Linear, FilterMode.Point, TextureWrapMode.Clamp);
            Texture2D incident = RuntimeTextureFactory.CreateRGBAHalfNoMip(1, 1,
                "EmissionIncidentControl", RuntimeTextureColorSpace.Linear, FilterMode.Point, TextureWrapMode.Clamp);
            owned.Add(surfacePixels);
            owned.Add(incident);
            incident.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1f));
            incident.Apply(false, false);
            Texture? previousLight = Shader.GetGlobalTexture("_WorldLightTexture");
            Texture? previousAo = Shader.GetGlobalTexture("_WorldAmbientOcclusionTexture");
            Vector4 previousRect = Shader.GetGlobalVector("_WorldLightRect");
            Vector4 previousSize = Shader.GetGlobalVector("_WorldLightTextureSize");
            bool previousKeyword = Shader.IsKeywordEnabled("KERN_WORLD_LIGHTING");
            try
            {
                Shader.SetGlobalTexture("_WorldLightTexture", incident);
                Shader.SetGlobalTexture("_WorldAmbientOcclusionTexture", Texture2D.blackTexture);
                Shader.SetGlobalVector("_WorldLightRect", new Vector4(0, 0, Cells, Cells));
                Shader.SetGlobalVector("_WorldLightTextureSize", Vector4.one);
                Shader.EnableKeyword("KERN_WORLD_LIGHTING");
                float controlledPeak = Capture(current, 1f, $"kern-{sourceName}-method-fixed.png");
                float excessivePeak = Capture(current, 12f, $"kern-{sourceName}-surface-emission-12.png");
                Capture(baseline, 1f, $"kern-{sourceName}-method-original.png");
                Capture(adaptive, 1f, $"kern-{sourceName}-method-adaptive.png");
                Capture(local, 1f, $"kern-{sourceName}-method-local.png");
                Capture(chroma, 1f, $"kern-{sourceName}-method-chroma.png");
                Shader.SetGlobalTexture("_WorldLightTexture", Texture2D.blackTexture);
                foreach ((Material material, string methodName) in new[]
                {
                    (baseline, "original"), (current, "fixed"), (adaptive, "adaptive"),
                    (local, "local"), (chroma, "chroma"),
                })
                {
                    Capture(material, 1f, $"kern-{sourceName}-emission-only-{methodName}.png", allowZero: true);
                }
                if (sourceName == "Green")
                {
                    Assert.That(controlledPeak, Is.GreaterThan(0.5f).And.LessThan(2f));
                    Assert.That(excessivePeak, Is.GreaterThan(controlledPeak * 4f));
                }
            }
            finally
            {
                current.SetFloat("_SurfaceEmissionStrength", TerrainConfigHolder.SurfaceEmissionStrength);
                Shader.SetGlobalTexture("_WorldLightTexture", previousLight);
                Shader.SetGlobalTexture("_WorldAmbientOcclusionTexture", previousAo);
                Shader.SetGlobalVector("_WorldLightRect", previousRect);
                Shader.SetGlobalVector("_WorldLightTextureSize", previousSize);
                if (!previousKeyword)
                {
                    Shader.DisableKeyword("KERN_WORLD_LIGHTING");
                }
            }

            float Capture(Material surfaceMaterial, float strength, string filename, bool allowZero = false)
            {
                surfaceRenderer.sharedMaterial = surfaceMaterial;
                surfaceMaterial.SetFloat("_SurfaceEmissionStrength", strength);
                camera.targetTexture = surfaceTarget;
                ShaderUtil.CompilePass(surfaceMaterial, surfaceMaterial.FindPass("Universal2D"), forceSync: true);
                Assert.That(ShaderUtil.ShaderHasError(surfaceMaterial.shader), Is.False);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = surfaceTarget });
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = surfaceTarget;
                    surfacePixels.ReadPixels(new Rect(0, 0, 320, 320), 0, 0);
                    surfacePixels.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }
                float peak = 0f;
                foreach (Color pixel in surfacePixels.GetPixels())
                {
                    peak = Mathf.Max(peak, pixel.maxColorComponent);
                }
                File.WriteAllBytes(Path.Combine(Application.temporaryCachePath, filename), surfacePixels.EncodeToPNG());
                Assert.That(float.IsNaN(peak) || float.IsInfinity(peak), Is.False);
                if (!allowZero)
                {
                    Assert.That(peak, Is.GreaterThan(0f), $"{sourceName} {TerrainEmissionDebugState.ReadMaterial(surfaceMaterial)} capture is empty.");
                }
                TestContext.WriteLine($"Actual {sourceName} sheet method={TerrainEmissionDebugState.ReadMaterial(surfaceMaterial)}, strength={strength}, peak scene-linear={peak}.");
                return peak;
            }

            AdaptiveTerrainEmissionCache PrepareCache(Material material, TerrainEmissionMethod method)
            {
                var cache = new AdaptiveTerrainEmissionCache(method);
                try
                {
                    var textures = new SourceTextureFixture(source, sourceType);
                    var timer = Stopwatch.StartNew();
                    cache.EnsureSources(textures.GetAllAtlases(), textures);
                    cache.Poll();
                    // Fixture-only synchronization. Production preparation uses
                    // asynchronous polling; its compute pass/resources are unchanged.
                    AsyncGPUReadback.WaitAllRequests();
                    while (cache.PendingCount > 0 && timer.Elapsed.TotalSeconds < 15)
                    {
                        cache.Poll();
                        Assert.That(cache.Error, Is.Null);
                        System.Threading.Thread.Yield();
                    }
                    Assert.That(cache.PendingCount, Is.Zero);
                    Assert.That(cache.PreparedCount, Is.EqualTo(1));
                    Assert.That(cache.SourceDispatchCount, Is.EqualTo(1));
                    Assert.That(cache.Bind(material), Is.True);
                    TerrainEmissionDebugState.Apply(material, method);
                    TestContext.WriteLine($"{sourceName} {method}: one-time source extraction/analysis/publication {timer.Elapsed.TotalMilliseconds:F3} ms; GPU samples capacity {cache.SampleCapacity}, source texels {cache.SourceTexelCount}.");
                    return cache;
                }
                catch
                {
                    cache.Dispose();
                    throw;
                }
            }

            CommandBuffer Record(Material material)
            {
                var commands = new CommandBuffer { name = "Terrain source emission cost" };
                for (int repeat = 0; repeat < Repetitions; repeat++)
                {
                    manager.RenderLightingMaterialFields(commands, materialField, glowField,
                        new Vector4(0, 0, Cells, Cells), Matrix4x4.identity,
                        new[] { material }, carrier.Mesh, Vector4.zero);
                }
                return commands;
            }

            double Complete(CommandBuffer commands)
            {
                var timer = Stopwatch.StartNew();
                Graphics.ExecuteCommandBuffer(commands);
                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(glowField);
                request.WaitForCompletion();
                timer.Stop();
                Assert.That(request.hasError, Is.False);
                return timer.Elapsed.TotalMilliseconds / Repetitions;
            }
        }
        finally
        {
            foreach (Object value in owned)
            {
                Object.DestroyImmediate(value);
            }
        }
    }
    private sealed class SourceTextureFixture : ITextureService, IAtlasDescriptor
    {
        private readonly Texture2D _texture;
        private readonly CellType _type;
        public SourceTextureFixture(Texture2D texture, CellType type)
        {
            _texture = texture;
            _type = type;
        }
        public event Action<string, Texture2D>? OnTextureLoaded
    {
        add
        {
        }
        remove
        {
        }
    }
        public int PendingCellTextureRequests => 0;
        public Texture2D Texture => _texture;
        public int Size => _texture.width;
        public bool ContainsCell(CellType value) => value == _type;
        public bool IsFullyOpaque(CellType value) => value == _type;
        public Vector4 GetCellFrameRect(CellType value) => value == _type ? new Vector4(0, 0, 1, 1) : Vector4.zero;
        public int GetAnimationFrameCount(CellType value) => 1;
        public IReadOnlyList<IAtlasDescriptor> GetAllAtlases() => new IAtlasDescriptor[] { this };
        public void RequestTexture(CellType value) => throw new NotSupportedException();
        public AtlasCoordinate GetCellTextureCoordinate(CellType value) => throw new NotSupportedException();
        public UniTask<AtlasCoordinate> GetCellTextureCoordinate(CellType value, int x, int y) => throw new NotSupportedException();
        public Texture2D? PrismaticFlowMapTexture => null;
        public Texture2D? FlowMapTexture => null;
        public Texture2D? TerrainDecalAtlasTexture => null;
        public Texture2D? TerrainDecalRockAtlasTexture => null;
        public string GetCacheStats() => "Production source-sheet comparison fixture";
        public void FlushDirtyAtlases()
        {
        }
        public void Clear() => throw new NotSupportedException();
    }

}
