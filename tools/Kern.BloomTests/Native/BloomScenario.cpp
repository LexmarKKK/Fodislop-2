void expect(float actual,float expected,const char* reason) {
 if(!std::isfinite(actual) || std::fabs(actual-expected)>0.00002f) throw std::runtime_error(reason);
}
void setup(int w,int h,int ow,int oh) {
 _Scene.reset(w,h); _Glow.reset(w,h); _Output.reset(ow,oh);
 std::fill(_Glow.data.begin(),_Glow.data.end(),float4{1,1,1,1});
 _SourceTexelSize={1.f/w,1.f/h,(float)w,(float)h};
 _OutputSize={(float)ow,(float)oh,1.f/ow,1.f/oh};
 _SourceUv={1,1,0,0}; _GlowUv={1,1,0,0}; _Tint={1,1,1,1};
 _Threshold=1; _SoftKnee=0; _Radius=1.95f; _Scatter=.58f;
}
int main() {
 try {
  // Each of the sixteen source phases must preserve the same highlight energy.
  for(int phase=0;phase<16;phase++) {
   setup(4,4,1,1); _Scene.data[phase]={2,2,2,1}; Prefilter(uint3{0,0,0});
   expect(_Output.data[0].x,1.f/16,"isolated above-threshold pixel lost or phase-dependent");
  }
  // A coarse emission cell must not enable its non-emitting bright neighbor.
  setup(4,4,1,1); _Glow.reset(2,1); _Glow.data[0]={1,1,1,1}; _Glow.data[1]={0,0,0,1};
  _Scene.data[2]={64,64,64,1}; Prefilter(uint3{0,0,0});
  expect(_Output.data[0].x,0,"emission eligibility bled across a cell boundary");
  // Odd-sized viewport with a padded negative first source row/column.
  setup(3,3,1,1); _SourceUv={4.f/3,4.f/3,-1.f/3,-1.f/3};
  for(auto& pixel:_Scene.data) pixel={2,2,2,1}; Prefilter(uint3{0,0,0});
  expect(_Output.data[0].x,9.f/16,"padding must be black, not clamped edge duplicates");
  _Source.reset(9,9); _SourceTexelSize={1.f/9,1.f/9,9,9};
  _Source[int2{4,4}]={1,1,1,0};
  expect(Tent(float2{.5f,.5f},1).x,.25f,"tent impulse center");
  expect(Tent(float2{3.5f/9,4.5f/9},1).x,.125f,"tent impulse axis");
  expect(Tent(float2{3.5f/9,3.5f/9},1).x,.0625f,"tent impulse diagonal");
  std::fill(_Source.data.begin(),_Source.data.end(),float4{8,4,2,0});
  auto uniform=Tent(float2{.5f,.5f},1.95f);
  expect(uniform.x,8,"HDR energy normalization"); expect(uniform.y,4,"hue preservation");
  // Downsampling is an anti-alias filter, independent of artistic halo radius.
  _Source.reset(9,9); _Source[int2{4,4}]={1,1,1,0};
  _Output.reset(1,1); _OutputSize={1,1,1,1}; _SourceUv={1,1,0,0};
  _Radius=.2f; Downsample(uint3{0,0,0}); float narrow=_Output.data[0].x;
  _Radius=4; Downsample(uint3{0,0,0}); expect(_Output.data[0].x,narrow,"downsample changed with halo radius");
  std::cout << "16 phases, emission isolation, negative/odd bounds, independent tent weights, HDR and downsample invariance passed\n";
  return 0;
 } catch(const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
