# -*- coding: utf-8 -*-
import base64, os, io, json

ROOT = r"C:\Users\vnvnc\Documents\ChatGPT\땅굴크루 만들기 0828\assets\sfx\kenney-candidates"

# event -> (category, gain, pitch-jitter, [relative files])
BANK = {
 "dig":       ("dig",    0.075, 0.10, ["01-dig-drill/impactMining_000.ogg","01-dig-drill/impactMining_001.ogg",
                                      "01-dig-drill/impactMining_002.ogg","01-dig-drill/impactMining_003.ogg",
                                      "01-dig-drill/impactMining_004.ogg"]),
 "brk":       ("brk",    0.28, 0.08, ["01-dig-drill/stonesHit1.ogg","01-dig-drill/stonesHit2.ogg",
                                      "01-dig-drill/impactPlate_heavy_000.ogg"]),
 # 총격 전용. SFX.tick 은 룬조각 드롭·특성카드 클릭에도 쓰이는 범용 블립이라 건드리지 않는다.
 "shot":      ("combat", 0.11, 0.07, ["03-gun/laserSmall_000.ogg","03-gun/laserSmall_002.ogg",
                                      "03-gun/laserSmall_004.ogg"]),
 # 룬조각/보급품 드롭 — 돌 부서지는 소리 위에 얹히는 조용한 플럭
 "shard":     ("loot",   0.07, 0.06, ["05-ore-resource/pluck_002.ogg"]),
 "reload":    ("combat", 0.14, 0.04, ["03-gun/metalClick.ogg"]),
 "reloadDone":("combat", 0.09, 0.03, ["03-gun/metalLatch.ogg"]),
 "growl":     (None,     0.15, 0.16, ["04-monster-combat/creature1.ogg","04-monster-combat/creature3.ogg",
                                      "04-monster-combat/creature5.ogg"]),
 # 분홍 광석(gem/crys/ore) 벽돌 파쇄 — 결정이 깨지는 유리질
 "orebrk":    ("brk",    0.16, 0.07, ["15-ore-crystal/impactGlass_light_000.ogg",
                                      "15-ore-crystal/impactGlass_light_002.ogg",
                                      "15-ore-crystal/impactGlass_light_004.ogg"]),
 # 적 처치 — 젖은 파열음(무거운 소프트 임팩트) + 점액 꼬리
 "kill":      ("combat", 0.11, 0.10, ["16-enemy-death/impactSoft_medium_000.ogg",
                                      "16-enemy-death/impactSoft_medium_002.ogg",
                                      "16-enemy-death/impactSoft_medium_003.ogg",
                                      "16-enemy-death/impactSoft_medium_004.ogg"]),
 "killwet":   ("combat", 0.055, 0.14, ["16-enemy-death/slime_000.ogg"]),
 "res":       ("loot",   0.06, 0.09, ["05-ore-resource/pluck_001.ogg","05-ore-resource/pluck_002.ogg"]),
 "ui":        ("ui",     0.09, 0.05, ["07-ui/click_003.ogg"]),
 # 메인 로비 메뉴 전용 - 호버는 click_003, 클릭은 click_001
 "hover":     ("ui",     0.05, 0.04, ["07-ui/click_003.ogg"]),
 "menuclick": ("ui",     0.09, 0.03, ["07-ui/click_001.ogg"]),
 "back":      ("ui",     0.075, 0.04, ["07-ui/back_001.ogg"]),
 "pick":      ("ui",     0.055, 0.04, ["07-ui/select_002.ogg"]),
 "deploy":    ("ui",     0.055, 0.03, ["07-ui/confirmation_001.ogg"]),
 # 특성 카드 — 등장 bookFlip2 / 선택 maximize_003
 "cardflip":  ("ui",     0.16, 0.05, ["11-shop-buy/bookFlip2.ogg"]),
 "cardpick":  ("ui",     0.07, 0.02, ["11-shop-buy/maximize_003.ogg"]),
 # 크루 발소리·장비
 "step":      (None,     0.06, 0.08, ["12-crew-footstep/footstep_concrete_000.ogg",
                                      "12-crew-footstep/footstep_concrete_001.ogg",
                                      "12-crew-footstep/footstep_concrete_002.ogg",
                                      "12-crew-footstep/footstep_concrete_003.ogg"]),
 "stepcrew":  (None,     0.032, 0.10, ["12-crew-footstep/footstep00.ogg",
                                      "12-crew-footstep/footstep03.ogg",
                                      "12-crew-footstep/footstep07.ogg"]),
 "cloth":     (None,     0.033, 0.12, ["12-crew-footstep/cloth1.ogg","12-crew-footstep/cloth3.ogg"]),
 "gear":      (None,     0.13, 0.06, ["12-crew-footstep/pickup1.ogg","12-crew-footstep/setDown1.ogg"]),
 "ovl":       ("dig",    0.20, 0.02, ["02-drill-engine/phaserDown1.ogg"]),
 "ovl2":      ("dig",    0.24, 0.02, ["02-drill-engine/lowFrequency_explosion_000.ogg"]),
}

out = io.StringIO()
out.write("/* ══════════════════════════════════════════════\n")
out.write("   Kenney 샘플 뱅크 (CC0) — 절차 합성 위에 얹는 원샷 샘플 레이어\n")
out.write("   출처: Kenney Game Assets All-in-1 3.7.0 / Audio\n")
out.write("   원본 후보 전체는 assets/sfx/kenney-candidates/ 참고\n")
out.write("   SMP.on=false 로 끄면 전부 기존 절차 합성으로 되돌아간다.\n")
out.write("   ══════════════════════════════════════════════ */\n")
out.write("const SMP={\n")
out.write(" on:true, ready:false, loading:false, failed:false,\n")
out.write(" bank:{\n")
rows = []
for k,(cat,g,jit,files) in BANK.items():
    srcs = []
    for f in files:
        p = os.path.join(ROOT, f.replace("/", os.sep))
        b64 = base64.b64encode(open(p,"rb").read()).decode("ascii")
        srcs.append("'data:audio/ogg;base64,"+b64+"'")
    rows.append("  %s:{cat:%s,g:%s,jit:%s,buf:null,src:[%s]}" % (
        k, ("'"+cat+"'") if cat else "null", g, jit, ",".join(srcs)))
out.write(",\n".join(rows))
out.write("\n },\n")
out.write("""
 /* AU.init() 직후 1회 — 전 샘플 비동기 디코드. 실패해도 절차 합성이 그대로 남는다. */
 load(){
  if(this.loading||this.ready||this.failed||!AU.ready)return;
  this.loading=true;
  const dec=src=>new Promise(res=>{
   try{
    const s=atob(src.split(',').pop()),n=s.length,u=new Uint8Array(n);
    for(let i=0;i<n;i++)u[i]=s.charCodeAt(i);
    const ok=b=>res(b), no=()=>res(null);
    const p=AU.ctx.decodeAudioData(u.buffer,ok,no);
    if(p&&p.then)p.then(ok,no);
   }catch(e){res(null);}
  });
  const jobs=[];
  for(const k in this.bank){
   const e=this.bank[k];
   jobs.push(Promise.all(e.src.map(dec)).then(bs=>{
    e.buf=bs.filter(Boolean);
    e.src=null;                                  /* base64 원문 해제 — 메모리 회수 */
   }));
  }
  Promise.all(jobs).then(()=>{this.ready=true;this.loading=false;})
   .catch(()=>{this.failed=true;this.loading=false;});
 },
 /* DEMO 로 개별 이벤트만 절차 합성으로 되돌릴 수 있게 */
 enabled(k){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  if(D.kenneySfx===false||!this.on)return false;
  if(D.kenneySfxOff&&D.kenneySfxOff.indexOf(k)>=0)return false;
  return true;
 },
 has(k){
  if(!AU.ready||AU.muted||!this.ready||!this.enabled(k))return false;
  const e=this.bank[k];return !!(e&&e.buf&&e.buf.length);
 },
 /* o.g:게인 배수, o.at:지연(초), o.rate:재생속도 배수 */
 play(k,o){
  if(!this.has(k))return false;
  o=o||{};
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  const e=this.bank[k],c=AU.ctx,t0=AU.t()+(o.at||0);
  const b=e.buf[(Math.random()*e.buf.length)|0];
  const s=c.createBufferSource();s.buffer=b;
  const jit=o.jit==null?e.jit:o.jit;
  s.playbackRate.value=Math.max(.25,(o.rate||1)*(1+(Math.random()*2-1)*jit));
  const gv=(D.kenneySfxGain&&D.kenneySfxGain[k]!=null?D.kenneySfxGain[k]:e.g)
           *(o.g==null?1:o.g)*AU.catMul(e.cat);
  const g=c.createGain();g.gain.value=Math.max(.0002,gv);
  let node=s;
  if(o.lp){const f=c.createBiquadFilter();f.type='lowpass';f.frequency.value=o.lp;node.connect(f);node=f;}
  node.connect(g);g.connect(AU.sg);
  s.start(t0);
  try{s.stop(t0+b.duration/s.playbackRate.value+.05);}catch(err){}
  return true;
 }
};
/* 효과음 전체 게인 -50%. catMul() 을 타는 모든 소리(절차 합성·샘플·드릴)에 한 번에 걸린다.
   AU.vol.sfx 는 설정창 슬라이더가 쓰는 값이라 건드리지 않는다. */
AU.sfxMix=1.5;

/* 메인 로비 메뉴 전용 효과음 — 호버 click_003 / 클릭 click_001.
   메뉴 버튼의 기존 click 핸들러가 SFX.ui() 를 부르므로, 캡처 단계에서 먼저 "예약"해 두고
   뒤이어 오는 SFX.ui() 가 그 예약을 소비해 click_001 로 바뀐다(소리 두 번 나는 것 방지).
   핸들러가 SFX.ui() 를 부르지 않는 버튼이면 짧은 타이머가 대신 울려준다. */
const MENU_SFX={
 sel:'#crewMenu .modeBtn,#crewMenu .menuTools button',
 armed:0, timer:0, last:null,
 btn(t){
  if(!t||!t.closest)return null;
  const b=t.closest(this.sel);
  if(!b||b.disabled||b.offsetParent===null)return null;   /* 숨겨진 메뉴는 무시 */
  return b;
 },
 fire(){this.armed=0;if(this.timer){clearTimeout(this.timer);this.timer=0;}
  if(!SMP.play('menuclick'))return false;return true;},
 /* SFX.ui() 가 호출되면 예약을 가로챈다 */
 consume(){
  if(!this.armed||performance.now()-this.armed>400)return false;
  return this.fire();
 },
 bind(){
  document.addEventListener('pointerover',e=>{
   const b=this.btn(e.target);
   if(!b){this.last=null;return;}
   if(b===this.last)return;
   this.last=b;
   if(!thr('menuHover',45))return;
   SMP.play('hover');
  },true);
  document.addEventListener('click',e=>{
   if(!this.btn(e.target))return;
   this.armed=performance.now();
   if(this.timer)clearTimeout(this.timer);
   this.timer=setTimeout(()=>{this.timer=0;if(this.armed)this.fire();},90);
  },true);
 }
};
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>MENU_SFX.bind());
else MENU_SFX.bind();

/* 발소리 — 프레임/속도에 흔들리지 않게 "이동 거리"로 보폭을 만든다.
   플레이어: playerMoveDustTick(dt,vx,vy) 을 감싸 거리를 누적한다.
   AI 크루 : crew-ai 쪽은 건드리지 않고 공용 spawnMoveDust() 만 감싼다.
             인원수만큼 발소리가 쏟아지지 않게 전체 스로틀로 묶어 "무리의 발소리"로 들리게 한다. */
const STEPS={
 stride:60, acc:0,     /* 보폭(px) — 60 이면 기본 이동속도에서 초당 2.7보 (실측) */
 bind(){
  if(typeof playerMoveDustTick==='function'&&!playerMoveDustTick.__step){
   const _p=playerMoveDustTick;
   const f=function(dt,vx,vy){
    _p(dt,vx,vy);
    try{
     if(G.downed)return;
     if(G.dash&&G.dash.active){STEPS.acc=STEPS.stride*.5;return;}   /* 대시 중엔 발이 안 닿는다 */
     const sp=Math.hypot(vx,vy);
     if(sp<=1)return;
     STEPS.acc+=sp*dt;
     if(STEPS.acc>=STEPS.stride){STEPS.acc=0;SFX.step();}
    }catch(e){}
   };
   f.__step=1; window.playerMoveDustTick=f;
  }
  if(typeof spawnMoveDust==='function'&&!spawnMoveDust.__step){
   const _s=spawnMoveDust;
   const g=function(x,y,vx,vy,rad){
    _s(x,y,vx,vy,rad);
    try{
     const R=(typeof R_SHELLY!=='undefined')?R_SHELLY:16;
     if(Math.hypot(x-G.sh.x,y-G.sh.y)<=R*1.2)return;                /* 플레이어 본인 먼지는 위에서 처리 */
     if(!thr('crewStep',215+Math.random()*130))return;
     SFX.stepCrew();
    }catch(e){}
   };
   g.__step=1; window.spawnMoveDust=g;
  }
 }
};
if(document.readyState==='complete')STEPS.bind();
else window.addEventListener('load',()=>STEPS.bind(),{once:true});

/* AU.init 이 끝나면 샘플 디코드를 걸어둔다 (기존 init 본문은 건드리지 않음) */
(function(){
 const _init=AU.init;
 AU.init=function(){_init.call(this);
  if(!this.ready)return;
  SMP.load();
  if(typeof KENNEY_DRILL!=='undefined'&&KENNEY_DRILL.on())KENNEY_DRILL.load();
 };
})();

/* ── SFX 이벤트별 샘플 우선 / 절차 합성 폴백 ──
   기존 메서드를 클로저 P 에 보존한 뒤, 샘플이 없으면 원본을 그대로 호출한다.
   스로틀(thr)은 샘플 경로에서만 소비한다 — 폴백 원본이 자체 스로틀을 갖고 있어서
   래퍼에서 먼저 소비하면 원본이 항상 막힌다. */
(function(){
 const P={};
 const wrap=(name,fn)=>{P[name]=SFX[name].bind(SFX);SFX[name]=fn;};

 /* 채굴 타격 — impactMining 5종 랜덤. 스로틀은 기존과 동일(70ms) */
 wrap('dig',function(){
  if(SMP.has('dig')){if(!thr('dig',70))return;SMP.play('dig');return;}
  P.dig();                                       /* 폴백 경로의 스로틀은 원본이 직접 건다 */
 });
 /* 벽 파괴 — 돌무더기 붕괴 + 기존 저역 임팩트를 살짝 겹쳐 무게를 유지 */
 wrap('brk',function(){
  if(SMP.play('brk')){AU.tone(92,.20,{type:'sine',g:.10,slide:42,lp:240,cat:'brk'});return;}
  P.brk();
 });
 /* 사격 — SFX.tick 을 덮지 않고 전용 SFX.shot 을 새로 만든다.
    tick 은 룬조각 드롭·특성 카드 클릭에도 쓰이는 범용 블립이라 총소리를 얹으면 안 된다. */
 SFX.shot=function(){
  if(SMP.play('shot'))return;
  P.tick();                                      /* 폴백은 기존 tick 톤 그대로 */
 };
 /* 분홍 광석 벽돌 파쇄 — SFX.ore 는 유물 발견·빙결·레벨업 카드에도 쓰이므로 파쇄만 따로 뺀다 */
 SFX.oreBreak=function(){
  if(SMP.play('orebrk')){
   AU.tone(78,.26,{type:'sine',g:.13,slide:48,lp:220,cat:'brk'});      /* 무게용 저역 */
   AU.tone(196,.20,{type:'triangle',g:.05,lp:700,cat:'brk'});
   return;
  }
  SFX.ore();
 };
 /* 적 처치 — 벌레가 터지는 젖은 파열음. 기존에는 광맥 발견음(SFX.ore)을 공유하고 있었다 */
 SFX.kill=function(){
  if(SMP.play('kill')){
   SMP.play('killwet',{at:.015,g:.8,rate:1.25});                       /* 점액 꼬리 */
   AU.tone(64,.16,{type:'sine',g:.09,slide:38,lp:180,cat:'combat'});   /* 내장 저역 */
   return;
  }
  SFX.ore();
 };
 /* 특성 카드 — 한 장씩 나타날 때 / 고를 때 */
 SFX.cardFlip=function(){ if(SMP.play('cardflip'))return; P.tick(); };
 SFX.cardPick=function(){ if(SMP.play('cardpick'))return; P.buy(); };
 /* 발소리 — 보폭은 STEPS 가 이동 거리로 만든다. 여기선 소리만 낸다 */
 SFX.step=function(){
  if(!SMP.play('step',{rate:.95+Math.random()*.12}))return;
  if(Math.random()<.18)SMP.play('cloth',{at:.02,g:.65});      /* 가끔 장비 스침 */
 };
 SFX.stepCrew=function(){ SMP.play('stepcrew',{rate:.94+Math.random()*.15}); };
 /* 대시/그래플 — 원래 SFX.dash 는 정의가 없어서 무음이었다. 장비 소리로 채운다 */
 SFX.dash=function(){ if(SMP.play('gear'))SMP.play('cloth',{at:.03,g:.8}); };
 /* 룬조각·보급품 드롭 — 조용한 플럭 + 낮은 배음. 벽 파괴음 위에 얹히므로 앞에 나서지 않게 */
 SFX.shard=function(){
  if(SMP.play('shard')){AU.tone(196,.16,{type:'sine',g:.035,lp:520,cat:'loot'});return;}
  AU.tone(523,.14,{type:'sine',g:.05,lp:1500,cat:'loot'});
  AU.tone(784,.11,{type:'sine',g:.028,at:.035,lp:1700,cat:'loot'});
  AU.tone(196,.16,{type:'sine',g:.035,lp:520,cat:'loot'});
 };
 wrap('reload',function(manual){
  if(SMP.has('reload')){
   if(!thr('reload',120))return;
   SMP.play('reload');
   if(manual)SMP.play('reload',{at:.13,g:.7,rate:1.15});
   return;
  }
  P.reload(manual);
 });
 wrap('reloadDone',function(){
  if(SMP.play('reloadDone'))return;
  P.reloadDone();
 });
 /* 적 울음 — vol(0.02~0.22)을 그대로 게인 배수로 넘긴다 */
 wrap('growl',function(vol){
  AU.init();
  if(SMP.has('growl')){
   if(!thr('growl',420))return;
   const v=Math.max(.02,Math.min(.22,vol==null?.1:vol));
   SMP.play('growl',{g:v*3.4,rate:.72+Math.random()*.16,lp:1400});
   return;
  }
  P.growl(vol);
 });
 wrap('res',function(){
  if(SMP.has('res')){if(!thr('res',60))return;SMP.play('res');return;}
  P.res();
 });
 /* UI */
 wrap('ui',function(){
  if(MENU_SFX.consume())return;                  /* 로비 메뉴 클릭이면 click_001 로 대체 */
  if(SMP.play('ui'))return;
  P.ui();
 });
 wrap('back',function(){if(SMP.play('back'))return;P.back();});
 wrap('pick',function(){if(SMP.play('pick'))return;P.pick();});
 wrap('deploy',function(){if(SMP.play('deploy'))return;P.deploy();});
 /* 드릴 과부하 — 회전수 급락 + 저역 배출 2겹 */
 wrap('drillOverload',function(){
  AU.init();if(!AU.ready)return;
  if(SMP.has('ovl')){
   SMP.play('ovl');
   SMP.play('ovl2',{at:.06});
   AU.tone(88,.34,{type:'sine',g:.10,slide:44,lp:220,at:.02,cat:'dig'});
   return;
  }
  P.drillOverload();
 });
})();

/* ── 드릴 엔진 루프 (Kenney engineCircular) — 옵트인 ──
   기본값은 기존 폴리싱 3-파트 DRILL_SMP 유지.
   DEMO.drillKenneyLoop=true 로 켜면 이쪽으로 전환된다.
   base64 내장 대신 파일을 fetch 하므로 HTML 용량은 늘지 않는다. */
const KENNEY_DRILL={
 url:'assets/sfx/kenney-candidates/02-drill-engine/engineCircular_002.ogg',
 buf:null, nd:null, loading:false, failed:false, h:0, want:false,
 on(){const D=(typeof DEMO!=='undefined')?DEMO:{};return D.drillKenneyLoop===true;},
 load(){
  if(this.buf||this.loading||this.failed||!AU.ready)return;
  this.loading=true;
  const done=b=>{this.buf=b;this.loading=false;
   if(this.want&&this.on())this.play();};        /* 로딩 중 눌린 홀드를 이어받는다 */
  const no=()=>{this.failed=true;this.loading=false;};
  fetch(this.url).then(r=>{if(!r.ok)throw 0;return r.arrayBuffer();})
   .then(ab=>{const p=AU.ctx.decodeAudioData(ab,done,no);if(p&&p.then)p.then(done,no);})
   .catch(no);
 },
 usable(){return this.on()&&!this.failed&&!!this.buf;},
 active(){return !!this.nd;},
 lvl(){const D=(typeof DEMO!=='undefined')?DEMO:{};
  return Math.max(.0001,(D.drillKenneyGain!=null?D.drillKenneyGain:.30)*AU.catMul('dig'));},
 /* 아직 디코드 전이면 want 만 기록하고 로딩을 건다 — 완료 시 play() 로 이어진다 */
 set(on){
  this.want=!!on;
  if(!this.on()||this.failed)return;
  if(!this.buf){this.load();return;}
  on?this.play():this.stop();
 },
 play(){
  if(this.nd||!this.usable())return;
  const c=AU.ctx,t=AU.t();
  const s=c.createBufferSource();s.buffer=this.buf;s.loop=true;
  const lp=c.createBiquadFilter();lp.type='lowpass';lp.frequency.value=2200;
  const g=c.createGain();
  g.gain.setValueAtTime(.0001,t);
  g.gain.exponentialRampToValueAtTime(this.lvl(),t+.10);
  s.connect(lp);lp.connect(g);g.connect(AU.sg);
  s.start(t);
  this.nd={s,g,lp};this.h=0;
 },
 stop(){
  const n=this.nd;if(!n)return;
  const t=AU.t();
  try{n.g.gain.cancelScheduledValues(t);
   n.g.gain.setValueAtTime(Math.max(.0002,n.g.gain.value),t);
   n.g.gain.exponentialRampToValueAtTime(.0001,t+.14);}catch(e){}
  setTimeout(()=>{try{n.s.stop();}catch(e){}},220);
  this.nd=null;
 },
 /* 열 → 재생속도(회전수) + 개방감 */
 heat(h){
  const n=this.nd;if(!n)return;
  const hv=Math.max(0,Math.min(1,h||0));
  if(Math.abs(hv-this.h)<.004)return;
  this.h=hv;
  const D=(typeof DEMO!=='undefined')?DEMO:{},t=AU.t();
  const amt=D.drillKenneyPitch!=null?D.drillKenneyPitch:.55;
  const set=(p,v)=>{try{p.setTargetAtTime(v,t,.05);}catch(e){try{p.value=v;}catch(e2){}}};
  set(n.s.playbackRate,1+amt*Math.pow(hv,1.25));
  set(n.lp.frequency,2200*(1+1.1*hv));
 }
};
(function(){
 const pHum=SFX.drillHum.bind(SFX), pHeat=SFX.drillHeat.bind(SFX);
 SFX.drillHum=function(on){
  AU.init();if(!AU.ready)return;
  if(KENNEY_DRILL.on()&&!KENNEY_DRILL.failed){
   if(typeof DRILL_SMP!=='undefined'&&DRILL_SMP.active())DRILL_SMP.set(false);
   if(this._drill)this.drillHumProc(false);
   KENNEY_DRILL.set(on);return;                  /* 로드 실패 시에만 아래 기존 경로로 되돌아간다 */
  }
  if(KENNEY_DRILL.active())KENNEY_DRILL.stop();
  pHum(on);
 };
 SFX.drillHeat=function(h){
  if(KENNEY_DRILL.active()){KENNEY_DRILL.heat(h);return;}
  pHeat(h);
 };
})();
""")

BEGIN = "/* KENNEY-SFX-BLOCK:BEGIN */"
END = "/* KENNEY-SFX-BLOCK:END */"
body = BEGIN + chr(10) + out.getvalue().rstrip(chr(10)) + chr(10) + END + chr(10)
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "smp_block.js"), "w", encoding="utf-8").write(body)
out = io.StringIO(body)
print("bytes:", len(out.getvalue()))
