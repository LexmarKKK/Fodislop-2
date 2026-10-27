# Bloom kernel regressions

Run `python3 tools/Kern.BloomTests/Cli/run.py` from the repository root.

The runner executes the current production `WorldBloom.compute` functions after
syntax-only translation to C++, using the existing native texture backend.
Expected numbers are independent: isolated HDR pixel (2−1)/16, all sixteen source
phases, point emission eligibility, black padded bounds, tent impulse weights
1/4, 1/8 and 1/16, uniform HDR/color preservation and downsample radius invariance.
Four injected defects must fail. Temporary generated sources are disposable;
repository shader sources are never mutated by the runner.

All three HLSL entries are also checked by glslang with a standalone luminance
shell using the installed URP coefficients. This is a kernel/syntax regression,
not a Metal compiler or production-camera render test. The existing MainGame
`FrameBenchmarkPlayModeTests.MainGame_WorldBloomImage` observes the actual URP
production pass, material, scene, glow publication and camera; its expected
prefilter energy now thresholds the individual source pixel before averaging.
That production test requires a separately authorized Unity run.
