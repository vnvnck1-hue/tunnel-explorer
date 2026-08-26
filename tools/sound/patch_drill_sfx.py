# -*- coding: utf-8 -*-
"""
땅굴 크루 — 드릴 사운드를 절차적 합성에서 실제 녹음 샘플(3-파트)로 교체하는 패치.

원본: mixkit-garage-pneumatic-screwer-817.wav
  attack  93.0 ~ 198.9 ms  (105.9 ms)
  loop   198.9 ~ 274.9 ms  ( 76.0 ms, crossfade 38ms)
  release 274.9 ~ 780.0 ms (505.1 ms)
  rate 0.67x / lowpass 1300Hz / gain +0.5dB (loop +1.0dB) 는 wav 에 이미 반영됨
  런타임 적용: wobble 118cent @ 1.6Hz, release crossfade 2ms, heat 피치 상승

기존 SFX.drillHum(on) / SFX.drillHeat(h) API 를 그대로 유지한다.
호출부(수십 곳) 수정 없이 내부 구현만 교체하고, 샘플 디코딩 실패/로딩 전에는
원래 절차적 합성(drillHumProc / drillHeatProc)으로 폴백한다.
"""
import base64, io, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, 'tunnel-crew-infinite-mode-v7.1.3-sound-polish.html')
SFXDIR = os.path.join(ROOT, 'assets', 'sfx')

def b64(name):
    with open(os.path.join(SFXDIR, name), 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')

WAV = {'start': b64('drill_start.wav'),
       'loop':  b64('drill_loop.wav'),
       'rel':   b64('drill_release.wav')}

# ── 1. DEMO 튜너블 ────────────────────────────────────────────────────────────
A_DEMO = "  drillHeatWobble:46,\n"
N_DEMO = A_DEMO + """  /* 드릴 사운드 — 실제 녹음 샘플(ATTACK/LOOP/RELEASE) */
  drillSfxOn:true,          /* false 면 기존 절차적 험으로 되돌림 */
  drillSfxGain:0.28,        /* dig 카테고리 배율에 곱해지는 기본 게인 */
  drillSfxWobble:118,       /* 상시 떨림 깊이 (cent) */
  drillSfxWobbleHz:1.6,     /* 상시 떨림 속도 (Hz) */
  drillSfxRelXf:2,          /* 루프→릴리즈 크로스페이드 (ms) */
  drillSfxStopGrace:80,     /* 이 시간 안에 다시 드릴하면 릴리즈 취소 (ms) */
"""

# ── 2. AU.init 에서 디코딩 선행 ────────────────────────────────────────────────
A_INIT = "  this.ready=true;\n  MUS.start();"
N_INIT = ("  this.ready=true;\n"
          "  try{if(typeof DRILL_SMP!=='undefined')DRILL_SMP.load();}catch(e){}\n"
          "  MUS.start();")

# ── 3. DRILL_SMP 모듈 ─────────────────────────────────────────────────────────
MODULE = """/* ══════════════════════════════════════════════
   드릴 사운드 — 실제 녹음 샘플 3-파트 재생기
   ATTACK(1회) → LOOP(홀드 중 무한) → RELEASE(1회)
   원본 mixkit-garage-pneumatic-screwer-817.wav 에서
   attack 93.0~198.9ms / loop 198.9~274.9ms(XF 38ms) / release 274.9~780.0ms
   rate 0.67x · lowpass 1300Hz · gain +0.5dB(loop +1.0dB) 는 wav 에 반영됨
   ══════════════════════════════════════════════ */
const DRILL_SMP={
 wav:{start:'@@START@@',loop:'@@LOOP@@',rel:'@@REL@@'},
 buf:{start:null,loop:null,rel:null},
 st:'idle',            /* idle | play | stopping */
 nd:null, h:0, loaded:false, failed:false, loading:false, relT:0, endT:0,
 cfg(){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  return {on:D.drillSfxOn!==false,
   gain:D.drillSfxGain!=null?D.drillSfxGain:0.28,
   wob:D.drillSfxWobble!=null?D.drillSfxWobble:118,
   wobHz:D.drillSfxWobbleHz!=null?D.drillSfxWobbleHz:1.6,
   relXf:D.drillSfxRelXf!=null?D.drillSfxRelXf:2,
   grace:D.drillSfxStopGrace!=null?D.drillSfxStopGrace:80};
 },
 usable(){return this.loaded&&!this.failed&&this.cfg().on;},
 active(){return this.st==='play';},
 load(){
  if(this.loading||this.loaded||this.failed||!AU.ready)return;
  this.loading=true;
  const dec=k=>new Promise((res,rej)=>{
   let done=false;
   const ok=b=>{if(done)return;done=true;this.buf[k]=b;res();};
   const no=e=>{if(done)return;done=true;rej(e);};
   try{
    const s=atob(this.wav[k]),n=s.length,u=new Uint8Array(n);
    for(let i=0;i<n;i++)u[i]=s.charCodeAt(i);
    const p=AU.ctx.decodeAudioData(u.buffer,ok,no);
    if(p&&p.then)p.then(ok,no);
   }catch(e){no(e);}
  });
  Promise.all([dec('start'),dec('loop'),dec('rel')])
   .then(()=>{this.loaded=true;this.loading=false;this.wav=null;})
   .catch(()=>{this.failed=true;this.loading=false;});
 },
 /* 열(0~1) → detune(cent). 기존 절차적 험과 동일한 곡선 1+amt*h^curve 를 cent 로 환산 */
 heatCents(h){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  const amt=D.drillHeatPitch!=null?D.drillHeatPitch:.62;
  const cv=D.drillHeatPitchCurve!=null?D.drillHeatPitchCurve:1.25;
  const hv=Math.max(0,Math.min(1,h||0));
  return 1200*Math.log(1+amt*Math.pow(hv,cv))/Math.LN2;
 },
 lvl(){return Math.max(.0001,this.cfg().gain*AU.catMul('dig')*(1+.25*this.h));},
 set(on){on?this.play():this.release();},
 play(){
  if(!this.usable())return;
  if(this.relT){clearTimeout(this.relT);this.relT=0;}   /* 대기 중이던 릴리즈 취소 */
  if(this.st==='play')return;
  if(this.st==='stopping')this.kill(.03);               /* 릴리즈 중 재시작 */
  const c=AU.ctx,t0=AU.t()+.005,cf=this.cfg(),cents=this.heatCents(this.h);
  const g=c.createGain();g.gain.value=this.lvl();g.connect(AU.sg);
  const a=c.createBufferSource();a.buffer=this.buf.start;a.connect(g);
  const l=c.createBufferSource();l.buffer=this.buf.loop;l.loop=true;l.connect(g);
  try{a.detune.value=cents;l.detune.value=cents;}catch(e){}
  const o=c.createOscillator(),og=c.createGain();
  o.frequency.value=cf.wobHz;og.gain.value=cf.wob;
  o.connect(og);try{og.connect(l.detune);}catch(e){}
  a.start(t0);
  l.start(t0+this.buf.start.duration);                 /* attack_to_loop = 0ms */
  o.start(t0);
  this.nd={g:g,a:a,l:l,o:o,og:og,rel:null};this.st='play';
 },
 release(){
  if(this.st!=='play'||this.relT)return;
  /* 한 프레임짜리 끊김으로 어택이 다시 튀지 않게 유예를 둔다 */
  this.relT=setTimeout(()=>{this.relT=0;this._rel();},Math.max(0,this.cfg().grace));
 },
 _rel(){
  if(this.st!=='play'||!this.nd)return;
  const c=AU.ctx,t=AU.t(),cf=this.cfg(),rx=Math.max(.001,cf.relXf/1000),n=this.nd;
  try{
   n.g.gain.cancelScheduledValues(t);
   n.g.gain.setValueAtTime(Math.max(.0001,n.g.gain.value),t);
   n.g.gain.linearRampToValueAtTime(.0001,t+rx);
  }catch(e){}
  ['a','l','o'].forEach(k=>{if(n[k]){try{n[k].stop(t+rx+.02);}catch(e){}}});
  const rg=c.createGain();
  rg.gain.setValueAtTime(.0001,t);
  rg.gain.linearRampToValueAtTime(this.lvl(),t+rx);
  rg.connect(AU.sg);
  const r=c.createBufferSource();r.buffer=this.buf.rel;
  try{r.detune.value=this.heatCents(this.h);}catch(e){}
  r.connect(rg);r.start(t);
  this.nd={g:rg,a:null,l:null,o:null,og:null,rel:r};
  this.st='stopping';
  if(this.endT)clearTimeout(this.endT);
  this.endT=setTimeout(()=>{this.endT=0;this.st='idle';this.nd=null;},
   (this.buf.rel.duration+rx+.05)*1000);
 },
 kill(fade){
  const n=this.nd;
  if(this.endT){clearTimeout(this.endT);this.endT=0;}
  if(n){
   const t=AU.t(),f=Math.max(.005,fade||.02);
   try{
    n.g.gain.cancelScheduledValues(t);
    n.g.gain.setValueAtTime(Math.max(.0001,n.g.gain.value),t);
    n.g.gain.linearRampToValueAtTime(.0001,t+f);
   }catch(e){}
   ['a','l','o','rel'].forEach(k=>{if(n[k]){try{n[k].stop(t+f+.02);}catch(e){}}});
  }
  this.nd=null;this.st='idle';
 },
 /* 열 피드백 — 피치(detune) + 떨림 + 게인 */
 heat(h){
  const hv=Math.max(0,Math.min(1,h||0));
  if(Math.abs(hv-this.h)<.004)return;
  this.h=hv;
  if(this.st!=='play'||!this.nd)return;
  const D=(typeof DEMO!=='undefined')?DEMO:{},cf=this.cfg(),t=AU.t(),n=this.nd;
  const wb=D.drillHeatWobble!=null?D.drillHeatWobble:46;
  const set=(p,v)=>{if(!p)return;try{p.setTargetAtTime(v,t,.05);}catch(e){try{p.value=v;}catch(e2){}}};
  const cents=this.heatCents(hv);
  if(n.l)set(n.l.detune,cents);
  if(n.a)set(n.a.detune,cents);
  if(n.o)set(n.o.frequency,cf.wobHz+14*hv);
  if(n.og)set(n.og.gain,cf.wob+wb*hv*hv);
  set(n.g.gain,this.lvl());
 }
};
"""
MODULE = (MODULE.replace('@@START@@', WAV['start'])
                .replace('@@LOOP@@',  WAV['loop'])
                .replace('@@REL@@',   WAV['rel']))

A_MOD = "const SFX={\n"
N_MOD = MODULE + A_MOD

# ── 4. drillHum / drillHeat 디스패처 + 기존 구현 rename ────────────────────────
A_HUM = " _drill:null,\n drillHum(on){"
N_HUM = """ /* 샘플(DRILL_SMP) 우선, 로딩 전/디코딩 실패/OFF 시 절차적 합성으로 폴백 */
 drillHum(on){
  AU.init();if(!AU.ready)return;
  const S=(typeof DRILL_SMP!=='undefined')?DRILL_SMP:null;
  if(S&&S.usable()){
   if(this._drill)this.drillHumProc(false);   /* 폴백으로 돌던 험은 정리 */
   S.set(on);return;
  }
  if(S&&S.active())S.set(false);
  this.drillHumProc(on);
 },
 drillHeat(h){
  const S=(typeof DRILL_SMP!=='undefined')?DRILL_SMP:null;
  if(S&&S.usable()&&S.active()){S.heat(h);return;}
  this.drillHeatProc(h);
 },
 _drill:null,
 drillHumProc(on){"""

A_HEAT = " /* 드릴 열(0~1)에 따라 엔진 회전수(피치)를 올린다 — 과부하 피드백 */\n drillHeat(h){"
N_HEAT = " /* 드릴 열(0~1)에 따라 엔진 회전수(피치)를 올린다 — 과부하 피드백 */\n drillHeatProc(h){"

PATCHES = [('DEMO 튜너블', A_DEMO, N_DEMO),
           ('AU.init 프리로드', A_INIT, N_INIT),
           ('DRILL_SMP 모듈', A_MOD, N_MOD),
           ('drillHum 디스패처', A_HUM, N_HUM),
           ('drillHeat rename', A_HEAT, N_HEAT)]

with io.open(TARGET, encoding='utf-8') as f:
    src = f.read()

if 'DRILL_SMP' in src:
    sys.exit('이미 패치된 파일입니다. 중단.')

for label, a, n in PATCHES:
    c = src.count(a)
    if c != 1:
        sys.exit('앵커 실패 [%s]: %d 건 발견 (1건이어야 함)' % (label, c))
    src = src.replace(a, n, 1)
    print('  ok  %s' % label)

with io.open(TARGET, 'w', encoding='utf-8', newline='') as f:
    f.write(src)
print('패치 완료 — %s (%.2f MB)' % (os.path.basename(TARGET), os.path.getsize(TARGET) / 1048576.0))
