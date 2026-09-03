/* v7.9.1-lighting-develop — 포토샵식 블렌드 조명(LX) 주입 스크립트
   사용: node tools/inject-lighting-lx.mjs <src.html> <dst.html>
   바이트 단위 패치(latin1 왕복)로 CRLF/LF 혼용을 보존한다. 앵커는 정확히 1회 매치해야 한다. */
import fs from 'node:fs';

const [,, SRC, DST] = process.argv;
if (!SRC || !DST) { console.error('usage: node inject-lighting-lx.mjs <src> <dst>'); process.exit(1); }
const L1 = s => Buffer.from(s, 'utf8').toString('latin1');
let html = fs.readFileSync(SRC).toString('latin1');
let patches = 0;

function rep(anchor, replacement, opts = {}) {
  const a = opts.regex ? anchor : L1(anchor);
  const b = L1(replacement);
  if (opts.regex) {
    const re = new RegExp(a, 'g');
    const m = html.match(re);
    if (!m || m.length !== 1) throw new Error(`regex anchor matched ${m ? m.length : 0}x: ${anchor.slice(0, 80)}`);
    html = html.replace(new RegExp(a), b.replace(/\$/g, '$$$$'));
  } else {
    const i = html.indexOf(a);
    if (i < 0) throw new Error(`anchor not found: ${anchor.slice(0, 100)}`);
    if (html.indexOf(a, i + 1) >= 0) throw new Error(`anchor not unique: ${anchor.slice(0, 100)}`);
    html = html.slice(0, i) + b + html.slice(i + a.length);
  }
  patches++;
}
/* 앵커 뒤에 삽입 */
function after(anchor, text) { rep(anchor, anchor + text); }
function before(anchor, text) { rep(anchor, text + anchor); }
const NL = html.indexOf('\r\n') >= 0 ? '\r\n' : '\n';

/* ── 0. 빌드 라벨 ─────────────────────────────────────────────── */
rep('menuBuild">PROTOTYPE / 7.9.0<', 'menuBuild">PROTOTYPE / 7.9.1 LIGHTING DEVELOP (LX)<');

/* ── 1. LX 튠 오브젝트 + 헬퍼 (FOW 직전) ─────────────────────────── */
const LX_BLOCK = `
/* ══════════ LX — 포토샵식 블렌드 조명 실험 (v7.9.1-lighting-develop) ══════════
   ① 컬러 라이트맵(multiply) ② 대비(overlay/soft-light) ③ 핫코어(color-dodge/screen)
   ④ 구역 앰비언스(hue/color) ⑤ 스프라이트 명암(multiply/screen) + 벽 그림자·림 모드
   모든 수치는 F10 테스트 패널에서 실시간 조절 → JSON 내보내기. */
const LX_MODES=['normal','multiply','screen','overlay','soft-light','hard-light','color-dodge','color-burn','linear-dodge','lighten','darken','hue','saturation','color','luminosity'];
const LX_CANVAS_MODES=['source-over','multiply','screen','overlay','soft-light','hard-light','color-dodge','color-burn','lighter','lighten','darken','hue','saturation','color','luminosity'];
const LX_LIGHT_KINDS=['hero','flash','torch','cold','bio','boss','fx','exit'];
const LX_DEFAULT={
 on:true, showMask:false,
 lights:{ hero:{rgb:'#FFC46A',mul:1}, flash:{rgb:'#FFE2A8',mul:1}, torch:{rgb:'#FF8A2A',mul:1.1}, cold:{rgb:'#4FB4FF',mul:1.1},
          bio:{rgb:'#4CF0B0',mul:1}, boss:{rgb:'#FF4A2A',mul:1.2}, fx:{rgb:'#FFD24A',mul:1}, exit:{rgb:'#3AE6FF',mul:1} },
 lightmap:{on:true, mode:'multiply', opacity:1, dark:'#1A1030', bright:1.25, saturation:1.2, litColor:1, litClear:0},
 contrast:{on:true, mode:'overlay', opacity:.6, darkLevel:.34, litLevel:.62, colorize:.7},
 core:{on:true, mode:'color-dodge', opacity:.7, threshold:.72, softness:.2, strength:.9, tintMix:1},
 zone:{on:true, mode:'color', opacity:.22, litFade:.6, blendSec:1.2, colors:['#7A4A9A','#3A8A9A','#3A6A9A','#9A5A3A']},
 sprite:{shadeMode:'multiply', shadeRgb:'#3A2A6A', liteMode:'screen', rimMode:'lighter', shade:1, lite:1, rim:1},
 wall:{shadowMode:'multiply', shadowRgb:'#2A1A48', shadeMode:'multiply', shadeRgb:'#241640', rimMode:'screen', shadow:1, shade:1, rim:1}
};
const LX_PRESETS={
 '기본 (LX)':{},
 '기존 조명 재현':{lightmap:{mode:'normal',opacity:.67,dark:'#06040F',bright:1,saturation:0,litColor:0,litClear:1},contrast:{on:false},core:{on:false},zone:{on:false},
   sprite:{shadeMode:'source-over',shadeRgb:'#0A0616',liteMode:'source-over',rimMode:'lighter'},wall:{shadowMode:'source-over',shadowRgb:'#06030E',shadeMode:'source-over',shadeRgb:'#06030E',rimMode:'lighter'}},
 '따뜻한 갱도':{lights:{hero:{rgb:'#FFD08A'},torch:{rgb:'#FF9438'},cold:{rgb:'#7FB8FF'}},lightmap:{dark:'#1E1026',bright:1.2,saturation:1.15},contrast:{mode:'soft-light',opacity:.7,darkLevel:.3,litLevel:.66,colorize:.7},
   core:{mode:'color-dodge',opacity:.7,threshold:.78,strength:.8},zone:{mode:'color',opacity:.28,colors:['#9A5A3A','#7A4A9A','#6A4A3A','#9A6A3A']},sprite:{shadeRgb:'#4A2A5A'},wall:{shadowRgb:'#3A1A38',shadeRgb:'#2A1430'}},
 /* 화려함 기준점 — 색은 강하게, 빛 밝기는 1.0 이하로 눌러 디테일이 남는 선. bright>1.2 + 세기>1.5 는 광원 반경이 통째로 날아간다. */
 '화려함 A (오버레이)':{lights:{hero:{rgb:'#FFA030',mul:1},flash:{rgb:'#FFE060',mul:1},torch:{rgb:'#FF4A10',mul:1.4},cold:{rgb:'#1E9CFF',mul:1.5},bio:{rgb:'#30FF90',mul:1.4},boss:{rgb:'#FF2040',mul:1.6},fx:{rgb:'#FFE030',mul:1.3},exit:{rgb:'#00F0FF',mul:1.4}},
   lightmap:{mode:'multiply',opacity:1,dark:'#22104A',bright:1.0,saturation:1.6,litColor:1,litClear:0},
   contrast:{on:true,mode:'overlay',opacity:.7,darkLevel:.32,litLevel:.62,colorize:.8},
   core:{on:true,mode:'color-dodge',opacity:.7,threshold:.85,softness:.12,strength:.8,tintMix:1},
   zone:{on:true,mode:'screen',opacity:.15,litFade:.5,blendSec:1,colors:['#B020FF','#00C8E0','#FF3090','#FF8A00']},
   sprite:{shadeMode:'multiply',shadeRgb:'#4A20B0',liteMode:'color-dodge',rimMode:'color-dodge',shade:1,lite:1.3,rim:1.4},
   wall:{shadowMode:'multiply',shadowRgb:'#3A1070',shadeMode:'multiply',shadeRgb:'#2C0C60',rimMode:'color-dodge',shadow:1,shade:1,rim:1.4}},
 '화려함 B (하드라이트 펀치)':{lights:{hero:{rgb:'#FFA030',mul:1},flash:{rgb:'#FFE060',mul:1},torch:{rgb:'#FF4A10',mul:1.4},cold:{rgb:'#1E9CFF',mul:1.5},bio:{rgb:'#30FF90',mul:1.4},boss:{rgb:'#FF2040',mul:1.6},fx:{rgb:'#FFE030',mul:1.3},exit:{rgb:'#00F0FF',mul:1.4}},
   lightmap:{mode:'hard-light',opacity:1,dark:'#1C0C44',bright:.8,saturation:1.8,litColor:1,litClear:0},
   contrast:{on:true,mode:'overlay',opacity:.5,darkLevel:.32,litLevel:.62,colorize:.8},
   core:{on:true,mode:'color-dodge',opacity:.7,threshold:.85,softness:.12,strength:.8,tintMix:1},
   zone:{on:true,mode:'screen',opacity:.15,litFade:.5,blendSec:1,colors:['#B020FF','#00C8E0','#FF3090','#FF8A00']},
   sprite:{shadeMode:'multiply',shadeRgb:'#4A20B0',liteMode:'color-dodge',rimMode:'color-dodge',shade:1,lite:1.3,rim:1.4},
   wall:{shadowMode:'multiply',shadowRgb:'#3A1070',shadeMode:'multiply',shadeRgb:'#2C0C60',rimMode:'color-dodge',shadow:1,shade:1,rim:1.4}},
 '네온 결정':{lights:{hero:{rgb:'#C8F0FF'},torch:{rgb:'#FF7AC8'},cold:{rgb:'#5AF0E8'},bio:{rgb:'#8AFF9A'}},lightmap:{dark:'#0A1034',bright:1.25,saturation:1.3},contrast:{mode:'overlay',opacity:.6,darkLevel:.32,litLevel:.62,colorize:.8},
   core:{mode:'screen',opacity:.8,threshold:.72,softness:.2,strength:.9},zone:{mode:'hue',opacity:.4,colors:['#3A5AFF','#20C0C0','#A040FF','#FF4090']},sprite:{shadeRgb:'#1A2A6A',liteMode:'color-dodge'},wall:{shadowRgb:'#101A48',shadeRgb:'#0C1440',rimMode:'color-dodge'}}
};
function lxDeepMerge(dst,src){for(const k in src){const v=src[k];if(v&&typeof v==='object'&&!Array.isArray(v)){if(!dst[k]||typeof dst[k]!=='object')dst[k]={};lxDeepMerge(dst[k],v);}else dst[k]=Array.isArray(v)?v.slice():v;}return dst;}
const LX=lxDeepMerge({},LX_DEFAULT);
const _lxHex={};
function LX_hex(h){let c=_lxHex[h];if(c)return c;let r=255,g=255,b=255;
 if(typeof h==='string'&&/^#[0-9a-fA-F]{6}$/.test(h)){r=parseInt(h.slice(1,3),16);g=parseInt(h.slice(3,5),16);b=parseInt(h.slice(5,7),16);}
 c=_lxHex[h]={i:[r,g,b],f:[r/255,g/255,b/255],css:r+','+g+','+b};return c;}
function LX_css(h){return LX_hex(h).css;}
/* 광원 종류 → 0~1 색 + 세기 배율.
   색 피커의 명도는 세기로 넘긴다(어두운 색 = 약한 빛). 색은 최대 채널이 1 이 되도록 정규화해 마스크에 순수 색조만 쌓는다. */
function LX_lightF(kind){const l=LX.lights[kind]||LX.lights.hero;const f=LX_hex(l.rgb).f;
 const mx=Math.max(f[0],f[1],f[2],0.001);return [f[0]/mx,f[1]/mx,f[2]/mx,(l.mul==null?1:l.mul)*mx];}
/* 광원 종류 → 0~255 색 (LIT 재질 조명용) */
function LX_light255(kind){const l=LX.lights[kind]||LX.lights.hero;return LX_hex(l.rgb).i;}
function LX_modeIdx(m){const i=LX_MODES.indexOf(m);return i<0?0:i;}
/* 구역 앰비언스 색 — 시점 캐릭터 발밑 지층 밴드 색으로 blendSec 동안 보간 */
const _lxZone={cur:null,t:0};
function LX_zoneRgb(vc){
 const Z=LX.zone,cols=Z.colors&&Z.colors.length?Z.colors:['#7A4A9A'];
 let band=0;
 if(vc&&G.band&&typeof toCell==='function'&&typeof ci==='function'){const [c,r]=toCell(vc.x,vc.y);
  if(c>=0&&r>=0&&c<COLS&&r<ROWS){const b=G.band[ci(c,r)];if(Number.isFinite(b))band=b|0;}}
 const tgt=LX_hex(cols[((band%cols.length)+cols.length)%cols.length]).f;
 const now=performance.now();const dt=_lxZone.t?Math.min(.1,(now-_lxZone.t)/1000):1;_lxZone.t=now;
 if(!_lxZone.cur)_lxZone.cur=tgt.slice();
 const k=Z.blendSec>0.01?1-Math.exp(-dt/Z.blendSec):1;
 for(let i=0;i<3;i++)_lxZone.cur[i]+=(tgt[i]-_lxZone.cur[i])*k;
 return _lxZone.cur;}
window.TC_LX=LX;
`;
before('/* ══════════ WebGL FoW (seedloop fogLitMask pipeline) ══════════ */', LX_BLOCK.replace(/\n/g, NL));

/* ── 2. FOW 내부 — 상태·헬퍼 ─────────────────────────────────────── */
after(" function u4(p,n,a,b,c,d){const l=gl.getUniformLocation(p,n);if(l)gl.uniform4f(l,a,b,c,d);}",
      NL + " function u3(p,n,a,b,c){const l=gl.getUniformLocation(p,n);if(l)gl.uniform3f(l,a,b,c);}");
after(" let VW=2,VH=2,maskTex=null,maskFbo=null,losTex=null,losW=0,losH=0,losUploadKey='';",
      NL + " let OW=2,OH=2,sceneTex=null;   /* LX: 출력 캔버스는 씬 해상도, 마스크는 LIGHTMAP_SCALE */");

/* ── 3. 스탬프 셰이더 — 광원 색을 RGB, 세기를 A 에 기록 ─────────────── */
rep("  'uniform float u_softMask;uniform float u_intensity;',",
    "  'uniform float u_softMask;uniform float u_intensity;uniform vec3 u_lightColor;',");
rep("  '  if(!lit)discard;gl_FragColor=vec4(vec3(u_intensity),1.0);',",
    "  '  if(!lit)discard;gl_FragColor=vec4(u_lightColor*u_intensity,u_intensity);',");
rep("  '  if(shade<0.02)discard;gl_FragColor=vec4(shade,shade,shade,1.0);}}'",
    "  '  if(shade<0.02)discard;gl_FragColor=vec4(u_lightColor*shade,shade);}}'");

/* 기존 안개 셰이더는 세기를 A 채널에서 읽는다 */
rep("  ' return texture2D(u_fogLitMaskTex,uv).r;}',", "  ' return texture2D(u_fogLitMaskTex,uv).a;}',");

/* ── 4. LX 합성 셰이더 (기존 fogProg 뒤에 추가) ───────────────────────── */
const LX_PROG = `
 /* ── LX 합성 — 씬 텍스처를 읽어 포토샵 블렌드 수식으로 직접 합성한다 ── */
 const lxProg=program([
  'attribute vec2 a_pos;attribute vec2 a_uv;varying vec2 v_texcoord;',
  'void main(){v_texcoord=a_uv;gl_Position=vec4(a_pos,0.0,1.0);}'
 ].join(String.fromCharCode(10)),[
  '#ifdef GL_FRAGMENT_PRECISION_HIGH',
  'precision highp float;',
  '#else',
  'precision mediump float;',
  '#endif',
  'uniform sampler2D u_fogLitMaskTex;uniform sampler2D u_bayerTex;uniform sampler2D u_losTex;uniform sampler2D u_sceneTex;',
  'uniform vec2 u_worldSourceSize;uniform float u_lightSteps;uniform float u_showMask;',
  'uniform float u_losEnabled;uniform vec2 u_losMapSize;',
  'uniform vec2 u_camXY;uniform vec2 u_offXY;uniform float u_zoom;uniform float u_cell;uniform float u_cssScale;',
  'uniform float u_lmOn,u_lmMode,u_lmOpacity,u_lmBright,u_lmSat,u_lmLitCol,u_lmClear;uniform vec3 u_lmDark;',
  'uniform float u_ctOn,u_ctMode,u_ctOpacity,u_ctDark,u_ctLit,u_ctColorize;',
  'uniform float u_coOn,u_coMode,u_coOpacity,u_coThreshold,u_coSoft,u_coStrength,u_coTint;',
  'uniform float u_znOn,u_znMode,u_znOpacity,u_znLitFade;uniform vec3 u_znRgb;',
  'varying vec2 v_texcoord;',
  'vec2 sampleLosLayers(vec2 pixelPos){',
  ' if(u_losEnabled<0.5||u_losMapSize.x<1.0)return vec2(1.0);',
  ' vec2 world=u_camXY+(pixelPos/u_cssScale-u_offXY)/max(u_zoom,0.001);',
  ' vec2 tuv=vec2(world.x/u_cell,world.y/u_cell)/u_losMapSize;',
  ' if(tuv.x<0.0||tuv.y<0.0||tuv.x>1.0||tuv.y>1.0)return vec2(0.0);',
  ' vec2 d=vec2(1.35)/u_losMapSize;',
  ' vec2 s=texture2D(u_losTex,tuv).rg*0.24;',
  ' s+=(texture2D(u_losTex,tuv+vec2(d.x,0.0)).rg+texture2D(u_losTex,tuv-vec2(d.x,0.0)).rg+',
  '     texture2D(u_losTex,tuv+vec2(0.0,d.y)).rg+texture2D(u_losTex,tuv-vec2(0.0,d.y)).rg)*0.12;',
  ' s+=(texture2D(u_losTex,tuv+d).rg+texture2D(u_losTex,tuv-d).rg+',
  '     texture2D(u_losTex,tuv+vec2(d.x,-d.y)).rg+texture2D(u_losTex,tuv+vec2(-d.x,d.y)).rg)*0.07;',
  ' return clamp(s,0.0,1.0);}',
  'float edgeNoise(vec2 p){return fract(sin(dot(floor(p),vec2(127.1,311.7)))*43758.5453);}',
  /* ── 포토샵/W3C 블렌드 수식 ── */
  'float lum(vec3 c){return dot(c,vec3(0.3,0.59,0.11));}',
  'vec3 clipColor(vec3 c){float l=lum(c);float n=min(c.r,min(c.g,c.b));float x=max(c.r,max(c.g,c.b));',
  ' if(n<0.0)c=l+(c-l)*l/max(l-n,1e-5);if(x>1.0)c=l+(c-l)*(1.0-l)/max(x-l,1e-5);return c;}',
  'vec3 setLum(vec3 c,float l){return clipColor(c+(l-lum(c)));}',
  'float satOf(vec3 c){return max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));}',
  'vec3 setSat(vec3 c,float s){float mx=max(c.r,max(c.g,c.b)),mn=min(c.r,min(c.g,c.b));if(mx>mn)return (c-mn)*s/(mx-mn);return vec3(0.0);}',
  'vec3 blendMode(float m,vec3 b,vec3 s){',
  ' if(m<0.5)return s;',
  ' if(m<1.5)return b*s;',
  ' if(m<2.5)return b+s-b*s;',
  ' if(m<3.5)return mix(2.0*b*s,1.0-2.0*(1.0-b)*(1.0-s),step(vec3(0.5),b));',
  ' if(m<4.5){vec3 d=mix(((16.0*b-12.0)*b+4.0)*b,sqrt(b),step(vec3(0.25),b));',
  '  return mix(b-(1.0-2.0*s)*b*(1.0-b),b+(2.0*s-1.0)*(d-b),step(vec3(0.5),s));}',
  ' if(m<5.5)return mix(2.0*b*s,1.0-2.0*(1.0-b)*(1.0-s),step(vec3(0.5),s));',
  ' if(m<6.5)return min(vec3(1.0),b/max(1.0-s,vec3(1e-4)));',
  ' if(m<7.5)return 1.0-min(vec3(1.0),(1.0-b)/max(s,vec3(1e-4)));',
  ' if(m<8.5)return min(vec3(1.0),b+s);',
  ' if(m<9.5)return max(b,s);',
  ' if(m<10.5)return min(b,s);',
  ' if(m<11.5)return setLum(setSat(s,satOf(b)),lum(b));',
  ' if(m<12.5)return setLum(setSat(b,satOf(s)),lum(b));',
  ' if(m<13.5)return setLum(s,lum(b));',
  ' return setLum(b,lum(s));}',
  'void main(){vec2 pixelPos=v_texcoord*u_worldSourceSize;',
  ' vec4 m=texture2D(u_fogLitMaskTex,vec2(v_texcoord.x,1.0-v_texcoord.y));',
  ' vec3 base=texture2D(u_sceneTex,v_texcoord).rgb;',
  ' float lit=m.a;float mx=max(m.r,max(m.g,m.b));',
  ' vec3 tint=mx>0.002?m.rgb/mx:vec3(1.0);',
  ' tint=clamp(mix(vec3(1.0),tint,u_lmSat),0.0,1.0);',
  ' vec2 losLayers=sampleLosLayers(pixelPos);float visible=losLayers.r;float memory=losLayers.g;',
  ' vec2 world=u_camXY+(pixelPos/u_cssScale-u_offXY)/max(u_zoom,0.001);',
  ' float edge=4.0*visible*(1.0-visible);',
  ' visible=clamp(visible+(edgeNoise(world/u_cell*0.72)-0.5)*0.16*edge,0.0,1.0);',
  ' visible=smoothstep(0.035,0.88,visible);',
  ' memory=smoothstep(0.004,0.24,memory)*0.22;',
  ' float losLight=max(visible,memory*0.58);lit*=losLight;',
  ' float steps=max(u_lightSteps,2.0);',
  ' vec2 bayerUV=(mod(floor(world),4.0)+vec2(0.5))/4.0;',
  ' float dither=(texture2D(u_bayerTex,bayerUV).r-0.5)*(1.0/steps);',
  ' float q=floor(lit*steps+dither+0.0001)/max(steps-1.0,1.0);q=clamp(q,0.0,1.0);',
  ' float memoryMix=(1.0-visible)*smoothstep(0.01,0.20,memory);',
  ' float unseen=0.0;',
  ' if(u_losEnabled>0.5){float known=max(visible,memory*1.45);unseen=1.0-smoothstep(0.015,0.46,known);}',
  ' q*=(1.0-unseen);',
  ' if(u_showMask>0.5){gl_FragColor=vec4(tint*q,1.0);return;}',
  /* 미탐색은 더 어둡게, 탐색 잔상은 살짝 밝은 보라 공간감 */
  ' vec3 dark=mix(u_lmDark,u_lmDark*1.6,memoryMix);dark=mix(dark,dark*0.25,unseen);',
  ' vec3 col=base;',
  /* ① 컬러 라이트맵 */
  ' if(u_lmOn>0.5){vec3 c=mix(dark,tint*u_lmBright,q*u_lmLitCol);float a=u_lmOpacity*(1.0-q*u_lmClear);a=max(a,unseen*u_lmOpacity);',
  '  col=mix(col,blendMode(u_lmMode,col,clamp(c,0.0,1.0)),clamp(a,0.0,1.0));}',
  /* ② 대비 — 회색 0.5 가 중립, 어두운 곳은 눌리고 밝은 곳은 채도·대비 상승 */
  ' if(u_ctOn>0.5){float g=mix(u_ctDark,u_ctLit,q);vec3 c=mix(vec3(g),tint*g*2.0,u_ctColorize*q);',
  '  col=mix(col,blendMode(u_ctMode,col,clamp(c,0.0,1.0)),u_ctOpacity);}',
  /* ④ 구역 앰비언스 — 어두운 곳 위주(litFade) */
  ' if(u_znOn>0.5){float a=u_znOpacity*(1.0-q*u_znLitFade);col=mix(col,blendMode(u_znMode,col,u_znRgb),clamp(a,0.0,1.0));}',
  /* ③ 핫코어 — 광원 중심(세기≥threshold)만 태운다 */
  ' if(u_coOn>0.5){float k=smoothstep(u_coThreshold,u_coThreshold+max(u_coSoft,0.001),lit)*u_coStrength;',
  '  vec3 c=mix(vec3(1.0),tint,u_coTint)*k;col=mix(col,blendMode(u_coMode,col,clamp(c,0.0,1.0)),u_coOpacity);}',
  ' gl_FragColor=vec4(clamp(col,0.0,1.0),1.0);}'
 ].join(String.fromCharCode(10)));`;
after("  ' gl_FragColor=vec4(fogRgb,fogA);}'" + NL + " ].join(String.fromCharCode(10)));", LX_PROG.replace(/\n/g, NL));

/* ── 5. stamp() — 광원 색·세기 배율 ─────────────────────────────── */
rep(" function stamp(cx,cy,radius,heightRatio,nStrength,cone,soft,intensity){",
    " function stamp(cx,cy,radius,heightRatio,nStrength,cone,soft,intensity,kind){");
rep("u1(prog,'u_softMask',soft?1:0);u1(prog,'u_intensity',intensity==null?1:intensity);",
    "u1(prog,'u_softMask',soft?1:0);{const lc=LX_lightF(kind||'hero');u3(prog,'u_lightColor',lc[0],lc[1],lc[2]);u1(prog,'u_intensity',(intensity==null?1:intensity)*lc[3]);}");
rep("  if(soft&&blendMinMax){gl.enable(gl.BLEND);gl.blendEquation(blendMinMax.MAX_EXT);gl.blendFunc(gl.ONE,gl.ONE);}",
    "  /* RGB 는 색×세기 가산, A 는 세기 MAX — 겹치는 광원은 색이 섞이고 세기는 가장 밝은 값 */" + NL +
    "  if(soft&&blendMinMax){gl.enable(gl.BLEND);gl.blendEquationSeparate(gl.FUNC_ADD,blendMinMax.MAX_EXT);gl.blendFunc(gl.ONE,gl.ONE);}");
rep("  if(blendMinMax)gl.blendEquation(gl.FUNC_ADD);" + NL + "  return true;}",
    "  gl.blendEquationSeparate(gl.FUNC_ADD,gl.FUNC_ADD);" + NL + "  return true;}");

/* ── 6. resize() — 출력은 씬 해상도, 마스크는 축소 + LINEAR ───────────── */
rep("  fogCv.width=VW;fogCv.height=VH;" + NL + "  if(maskFbo)gl.deleteFramebuffer(maskFbo);",
    "  OW=Math.max(2,Math.round(cssW*dpr));OH=Math.max(2,Math.round(cssH*dpr));" + NL +
    "  fogCv.width=OW;fogCv.height=OH;" + NL + "  if(maskFbo)gl.deleteFramebuffer(maskFbo);");
rep("  maskTex=tex(VW,VH,null);maskFbo=fboWithColor(maskTex);}",
    "  maskTex=tex(VW,VH,null,{filter:gl.LINEAR});maskFbo=fboWithColor(maskTex);}");

/* ── 7. composite() — 마스크 클리어, 스탬프 종류, 최종 합성 분기 ─────────── */
rep("  gl.bindFramebuffer(gl.FRAMEBUFFER,maskFbo);gl.viewport(0,0,VW,VH);" + NL + "  gl.clearColor(0,0,0,1);gl.clear(gl.COLOR_BUFFER_BIT);",
    "  gl.bindFramebuffer(gl.FRAMEBUFFER,maskFbo);gl.viewport(0,0,VW,VH);" + NL + "  gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);");
rep("   stamp(sv[0],sv[1],ambientR*mul,heightRatio,nStrength,null,soft);",
    "   stamp(sv[0],sv[1],ambientR*mul,heightRatio,nStrength,null,soft,undefined,'hero');");
rep("    stamp(sv[0],sv[1],bodyR*mul,heightRatio,nStrength,null,soft);",
    "    stamp(sv[0],sv[1],bodyR*mul,heightRatio,nStrength,null,soft,undefined,'flash');");
rep("     {enabled:true,dirX:dirX,dirY:dirY,halfAngle:halfAngle},soft);}",
    "     {enabled:true,dirX:dirX,dirY:dirY,halfAngle:halfAngle},soft,undefined,'flash');}");
rep("   const f=fxLights[i];stamp(f[0],f[1],f[2],heightRatio,nStrength,null,soft,.72);}",
    "   const f=fxLights[i];stamp(f[0],f[1],f[2],heightRatio,nStrength,null,soft,.72,'fx');}");
rep("   const L=lampLights[i];stamp(L[1],L[2],L[3],heightRatio,nStrength,null,soft,L[0].bio ? 0.13 : undefined);}",
    "   const L=lampLights[i];stamp(L[1],L[2],L[3],heightRatio,nStrength,null,soft,L[0].bio ? 0.13 : undefined,L[0].bio?'bio':(L[0].flare?'torch':'cold'));}");
rep("   stamp(sv[0],sv[1],lr,bossTune('lightHeightRatio'),bossTune('lightNormalStrength'),null,true,bossTune('lightIntensity'));}",
    "   stamp(sv[0],sv[1],lr,bossTune('lightHeightRatio'),bossTune('lightNormalStrength'),null,true,bossTune('lightIntensity'),'boss');}");
rep("    stamp(sv[0],sv[1],40*zoomScale*dpr,heightRatio,nStrength,null,soft);}}",
    "    stamp(sv[0],sv[1],40*zoomScale*dpr,heightRatio,nStrength,null,soft,undefined,'exit');}}");

const FINAL_PASS = `  gl.bindFramebuffer(gl.FRAMEBUFFER,null);gl.viewport(0,0,OW,OH);
  gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
  const outS=OW/Math.max(1,LW);
  if(LX.on){
   /* LX — 씬을 텍스처로 올려 셰이더에서 블렌드 합성. 출력은 불투명(씬 전체를 덮는다) */
   if(!sceneTex)sceneTex=tex(1,1,null,{filter:gl.LINEAR});
   gl.activeTexture(gl.TEXTURE3);gl.bindTexture(gl.TEXTURE_2D,sceneTex);
   gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,0);
   try{gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,cv);}catch(err){LX.on=false;console.warn('LX scene upload failed',err);}
   bindFullscreen(lxProg);
   gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,maskTex);usamp(lxProg,'u_fogLitMaskTex',0);
   gl.activeTexture(gl.TEXTURE1);gl.bindTexture(gl.TEXTURE_2D,bayerTex);usamp(lxProg,'u_bayerTex',1);
   gl.activeTexture(gl.TEXTURE2);gl.bindTexture(gl.TEXTURE_2D,losOn&&losTex?losTex:bayerTex);usamp(lxProg,'u_losTex',2);
   usamp(lxProg,'u_sceneTex',3);
   u2(lxProg,'u_worldSourceSize',OW,OH);u1(lxProg,'u_lightSteps',TE.lightSteps);
   u1(lxProg,'u_showMask',(LX.showMask||DEMO.showMask)?1:0);
   u1(lxProg,'u_losEnabled',losOn?1:0);
   u2(lxProg,'u_losMapSize',losOn?LOS.w:1,losOn?LOS.h:1);
   u2(lxProg,'u_camXY',G.camX,G.camY);u2(lxProg,'u_offXY',G.offX+shx,G.offY+shy);
   u1(lxProg,'u_zoom',G.Z);u1(lxProg,'u_cell',CELL);u1(lxProg,'u_cssScale',outS);
   {const M=LX.lightmap,d=LX_hex(M.dark).f;
    u1(lxProg,'u_lmOn',M.on?1:0);u1(lxProg,'u_lmMode',LX_modeIdx(M.mode));u1(lxProg,'u_lmOpacity',M.opacity);
    u1(lxProg,'u_lmBright',M.bright);u1(lxProg,'u_lmSat',M.saturation);u1(lxProg,'u_lmLitCol',M.litColor==null?1:M.litColor);u1(lxProg,'u_lmClear',M.litClear);u3(lxProg,'u_lmDark',d[0],d[1],d[2]);}
   {const C=LX.contrast;
    u1(lxProg,'u_ctOn',C.on?1:0);u1(lxProg,'u_ctMode',LX_modeIdx(C.mode));u1(lxProg,'u_ctOpacity',C.opacity);
    u1(lxProg,'u_ctDark',C.darkLevel);u1(lxProg,'u_ctLit',C.litLevel);u1(lxProg,'u_ctColorize',C.colorize);}
   {const K=LX.core;
    u1(lxProg,'u_coOn',K.on?1:0);u1(lxProg,'u_coMode',LX_modeIdx(K.mode));u1(lxProg,'u_coOpacity',K.opacity);
    u1(lxProg,'u_coThreshold',K.threshold);u1(lxProg,'u_coSoft',K.softness);u1(lxProg,'u_coStrength',K.strength);u1(lxProg,'u_coTint',K.tintMix);}
   {const Z=LX.zone,z=LX_zoneRgb(vc);
    u1(lxProg,'u_znOn',Z.on?1:0);u1(lxProg,'u_znMode',LX_modeIdx(Z.mode));u1(lxProg,'u_znOpacity',Z.opacity);
    u1(lxProg,'u_znLitFade',Z.litFade);u3(lxProg,'u_znRgb',z[0],z[1],z[2]);}
   gl.disable(gl.BLEND);
   gl.drawArrays(gl.TRIANGLES,0,6);
  }else{
   bindFullscreen(fogProg);
   gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,maskTex);usamp(fogProg,'u_fogLitMaskTex',0);
   gl.activeTexture(gl.TEXTURE1);gl.bindTexture(gl.TEXTURE_2D,bayerTex);usamp(fogProg,'u_bayerTex',1);
   gl.activeTexture(gl.TEXTURE2);gl.bindTexture(gl.TEXTURE_2D,losOn&&losTex?losTex:bayerTex);usamp(fogProg,'u_losTex',2);
   u2(fogProg,'u_worldSourceSize',OW,OH);u2(fogProg,'u_fogLitMaskSize',OW,OH);
   u1(fogProg,'u_fogLitMaskEnabled',1);
   u4(fogProg,'u_fogColor',FOG_RGB[0],FOG_RGB[1],FOG_RGB[2],1);
   u1(fogProg,'u_fogDensity',DEMO.showMask?0:TE.fogDensity);u1(fogProg,'u_lightSteps',TE.lightSteps);
   u1(fogProg,'u_losEnabled',losOn?1:0);
   u2(fogProg,'u_losMapSize',losOn?LOS.w:1,losOn?LOS.h:1);
   u2(fogProg,'u_camXY',G.camX,G.camY);u2(fogProg,'u_offXY',G.offX+shx,G.offY+shy);
   u1(fogProg,'u_zoom',G.Z);u1(fogProg,'u_cell',CELL);u1(fogProg,'u_cssScale',outS);
   gl.enable(gl.BLEND);gl.blendEquation(gl.FUNC_ADD);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);
   gl.drawArrays(gl.TRIANGLES,0,6);gl.disable(gl.BLEND);
  }`;
rep("  gl\\.bindFramebuffer\\(gl\\.FRAMEBUFFER,null\\);gl\\.viewport\\(0,0,VW,VH\\);[\\s\\S]*?gl\\.drawArrays\\(gl\\.TRIANGLES,0,6\\);gl\\.disable\\(gl\\.BLEND\\);(?=\\r?\\n  const frameMs=)",
    FINAL_PASS.replace(/\n/g, NL), { regex: true });

/* ── 8. LIT — 광원 색을 LX 에서, 스프라이트/벽 블렌드 모드 ────────────── */
rep(" const lcol=kind=>kind==='cold'?COOL:WARM;", " const lcol=kind=>LX_light255(kind);   /* LX: 광원 종류별 색은 패널에서 */");
rep("  else{info.r=WARM[0];info.g=WARM[1];info.b=WARM[2];}", "  else{const hc=lcol('hero');info.r=hc[0];info.g=hc[1];info.b=hc[2];}");

const SPR_OLD_START = "  sa.globalCompositeOperation='source-atop';" + NL + "  const shade=T.spShade*(0.35+0.65*I)+T.spAmb*(1-I);";
const SPR_NEW = `  /* ⑤ LX — 그늘은 multiply(채도 유지), 빛 받는 면은 screen/dodge. 블렌드 모드는 투명 영역에도
     칠해지므로 마지막에 원본 실루엣으로 다시 자른다(destination-in). */
  const S=LX.sprite, sc=LX_css(S.shadeRgb);
  const shade=(T.spShade*(0.35+0.65*I)+T.spAmb*(1-I))*(S.shade==null?1:S.shade);
  if(shade>0.002){
   sa.globalCompositeOperation=S.shadeMode||'multiply';
   const gr=sa.createLinearGradient(cxp+lx*R,cyp+ly*R,cxp-lx*R,cyp-ly*R);
   gr.addColorStop(0,'rgba('+sc+',0)');
   gr.addColorStop(0.45,'rgba('+sc+','+(shade*0.22).toFixed(3)+')');
   gr.addColorStop(1,'rgba('+sc+','+Math.min(.94,shade).toFixed(3)+')');
   sa.fillStyle=gr; sa.fillRect(0,0,W,H);
  }
  const lite=T.spLite*I*(S.lite==null?1:S.lite);
  if(lite>0.002){
   sa.globalCompositeOperation=S.liteMode||'screen';
   const gr=sa.createLinearGradient(cxp+lx*R,cyp+ly*R,cxp-lx*R*0.2,cyp-ly*R*0.2);
   gr.addColorStop(0,'rgba('+col+','+Math.min(.9,lite).toFixed(3)+')');
   gr.addColorStop(0.6,'rgba('+col+','+(lite*0.22).toFixed(3)+')');
   gr.addColorStop(1,'rgba('+col+',0)');
   sa.fillStyle=gr; sa.fillRect(0,0,W,H);
  }
  sa.globalCompositeOperation='destination-in'; sa.drawImage(img,pad,pad,nw,nh);
  /* 림 — 실루엣에서 "광원 반대로 민 실루엣"을 빼면 빛 쪽 초승달만 남는다 */
  const rim=T.spRim*I*(S.rim==null?1:S.rim);`;
/* 원본: source-atop 부터 rim 계산 직전까지를 통째로 교체 */
rep("  sa\\.globalCompositeOperation='source-atop';\\r?\\n  const shade=T\\.spShade[\\s\\S]*?const rim=T\\.spRim\\*I;",
    SPR_NEW.replace(/\n/g, NL), { regex: true });
rep("   sa.globalCompositeOperation='lighter';" + NL + "   sa.drawImage(scB,0,0,W,H,0,0,W,H);",
    "   sa.globalCompositeOperation=S.rimMode||'lighter';" + NL + "   sa.drawImage(scB,0,0,W,H,0,0,W,H);");

/* 벽 — 그림자/음영 색·모드, 림 모드 */
rep(" function pass(g,mode,sel){" + NL + "  const P2=PASS[mode];",
    " function pass(g,mode,sel){" + NL + "  const P2=PASS[mode], W2=LX.wall;");
rep("   const amt=T[P2.amt]*L[3];" + NL + "   if(amt<=0.004)continue;" + NL + "   const col=P2.dark?'6,3,14':lcol(L[4]).join(',');",
    "   const amt=T[P2.amt]*L[3]*(W2[mode]==null?1:W2[mode]);" + NL + "   if(amt<=0.004)continue;" + NL +
    "   const col=P2.dark?LX_css(mode==='shadow'?W2.shadowRgb:W2.shadeRgb):lcol(L[4]).join(',');");
rep("    if(!P2.dark)g.globalCompositeOperation='lighter';" + NL + "    g.drawImage(tmpCv,0,0,tw,th,0,0,CW,CH);",
    "    g.globalCompositeOperation=(mode==='shadow'?W2.shadowMode:(mode==='shade'?W2.shadeMode:W2.rimMode))||'source-over';" + NL + "    g.drawImage(tmpCv,0,0,tw,th,0,0,CW,CH);");

/* ── 9. 식생 글리머는 UI 캔버스에 — LX 출력이 불투명해 스테이지 위 그림이 가려진다 ── */
rep(" drawVegetationDarkGlimmer(shx,shy);" + NL + " paintUI(shx,shy);", " paintUI(shx,shy);   /* LX: 식생 글리머는 paintUI 안(ux)에서 */");
rep(" const prev=cx; cx=ux;" + NL + " if(G.tint>0){cx.globalAlpha=G.tint*.09;",
    " const prev=cx; cx=ux;" + NL + " if(SCENE==='depths')drawVegetationDarkGlimmer(shx,shy);" + NL + " if(G.tint>0){cx.globalAlpha=G.tint*.09;");
rep(" cx.save();cx.setTransform(1,0,0,1,0,0);cx.globalCompositeOperation='lighter';" + NL + " for(const q of items){const x=(q.x-G.camX)*G.Z+G.offX+shx",
    " cx.save();cx.setTransform(DPR,0,0,DPR,0,0);cx.globalCompositeOperation='lighter';" + NL + " for(const q of items){const x=(q.x-G.camX)*G.Z+G.offX+shx");

/* ── 10. 테스트 패널 (F10) ───────────────────────────────────────── */
const PANEL = fs.readFileSync(new URL('./lx-panel.js', import.meta.url), 'utf8');
rep("</body></html>", "<script>" + NL + PANEL.replace(/\r?\n/g, NL) + NL + "</script>" + NL + "</body></html>");

fs.writeFileSync(DST, Buffer.from(html, 'latin1'));
console.log(`ok: ${patches} patches → ${DST} (${(fs.statSync(DST).size / 1048576).toFixed(1)} MB)`);
