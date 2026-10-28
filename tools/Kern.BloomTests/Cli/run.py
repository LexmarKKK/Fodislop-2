#!/usr/bin/env python3
"""Execute production bloom kernels on a native texture backend; not a Unity render."""
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
SHADER = ROOT / 'Assets/Resources/Shaders/PostProcessing/Bloom/WorldBloom.compute'
LUMINANCE = 'float Luminance(float3 c) { return dot(c, float3(0.2126729, 0.7151522, 0.0721750)); }'


def translate(shader):
    shader = re.sub(r'^#(?:include|pragma).*$', '', shader, flags=re.M)
    shader = re.sub(r'\[numthreads\([^]]+\)\]|\[unroll\]', '', shader)
    shader = shader.replace('uint3 id : SV_DispatchThreadID', 'uint3 id')
    shader = re.sub(r'(?:RW)?Texture2D<float4>', 'Texture', shader)
    shader = shader.replace('SamplerState sampler_LinearClamp;', '')
    shader = shader.replace('SamplerState sampler_PointClamp;', '')
    shader = shader.replace('_Glow.SampleLevel(sampler_PointClamp, glowUv, 0)', 'PointSample(_Glow, glowUv)')
    shader = shader.replace('(uint2)_OutputSize.xy', 'ConvertUint(_OutputSize.xy)')
    shader = shader.replace('(int2)_SourceTexelSize.zw', 'make_int2(_SourceTexelSize.zw)')
    shader = shader.replace('(int2)round(', 'make_int2(round(').replace('_SourceTexelSize.zw - 2.0);', '_SourceTexelSize.zw - 2.0));')
    shader = shader.replace('scenePixel + 0.5', 'make_float2(scenePixel) + 0.5')
    shader = shader.replace('id.xy + 0.5', 'make_float2(make_int2(id.xy)) + 0.5')
    shader = shader.replace('_Output[id.xy]', '_Output[make_int2(id.xy)]')
    shader = re.sub(r'\b(float[234]|int[234])\(', r'make_\1(', shader)
    shader = re.sub(r'(?<![\w.])(\d+\.\d+(?:e[+-]?\d+)?|\d+e[+-]?\d+)(?![\w.])', r'\1f', shader)
    return shader


def main():
    shader = SHADER.read_text()
    shim = (ROOT / 'tools/Kern.LightingTests/Native/Transport/Shims/NativeTransportShim.cpp').read_text()
    extras = '''
float dot(float3 a,float3 b) { return a.x*b.x+a.y*b.y+a.z*b.z; }
float Luminance(float3 c) { return dot(c,float3{0.2126729f,0.7151522f,0.0721750f}); }
uint2 ConvertUint(float2 v) { return __builtin_convertvector(v,uint2); }
float4 PointSample(const Texture& t,float2 uv) {
 return t.Load(int3{clamp(int(std::floor(uv.x*t.width)),0,t.width-1),clamp(int(std::floor(uv.y*t.height)),0,t.height-1),0});
}
'''
    scenario = (ROOT / 'tools/Kern.BloomTests/Native/BloomScenario.cpp').read_text()
    mutations = {
        'center-removed': lambda s: s.replace('4.0 * _Source.SampleLevel', '0.0 * _Source.SampleLevel'),
        'threshold-after-mixing': lambda s: s.replace('luminance - _Threshold', 'luminance - 4.0 * _Threshold'),
        'downsample-art-radius': lambda s: s.replace('zw, 1.0)', 'zw, _Radius)'),
        'glow-mask-bleed': lambda s: s.replace('_Glow.SampleLevel(sampler_PointClamp', '_Glow.SampleLevel(sampler_LinearClamp'),
    }
    with tempfile.TemporaryDirectory(prefix='kern-bloom-') as scratch:
        scratch = Path(scratch)
        for name, source in [('production', shader)] + [(name, mutate(shader)) for name, mutate in mutations.items()]:
            cpp = scratch / (name + '.cpp')
            binary = scratch / name
            cpp.write_text(shim + extras + translate(source) + scenario)
            subprocess.run(['clang++', '-std=c++20', '-O2', str(cpp), '-o', str(binary)], check=True)
            result = subprocess.run([str(binary)], capture_output=True, text=True)
            if (name == 'production') != (result.returncode == 0):
                raise RuntimeError(name + ': ' + result.stdout + result.stderr)
            print(name + ': PASS' + (' (mutation rejected)' if name != 'production' else ''))
        syntax = scratch / 'WorldBloom.compute'
        syntax.write_text(shader.replace('#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"', LUMINANCE))
        for kernel in ('Prefilter', 'Downsample', 'Upsample'):
            subprocess.run(['glslangValidator', '-D', '-V', '-S', 'comp', '-e', kernel,
                            str(syntax), '-o', str(scratch / (kernel + '.spv'))], check=True)
            print(kernel + ': HLSL syntax PASS (standalone luminance shell)')


if __name__ == '__main__':
    main()
