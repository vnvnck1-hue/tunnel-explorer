/* BOSS-BGM-BLOCK:BEGIN */
/* ══════════════════════════════════════════════
   보스 BGM — 보스 등장 연출과 함께 시작하는 전투 음악
   · 파일: assets/audio/bgm/boss-blood-ascendant.mp3 (HTML 내장 아님)
   · 기본 경로: fetch + Web Audio (loop, 앞뒤 무음 트림) · 폴백: <audio loop> (file://)
   · BGM_ROUTE 에 'boss' 경로를 런타임으로 얹는다 — useBoss() 가 앰비언스(AMBI)를 페이드아웃
     시키고, endBoss() 가 보스 이전 경로(땅굴 앰비언스)로 되돌린다.
   · 훅은 전부 런타임 래핑: infSpawnBoss(시작) · infBossDefeated(처치 → 페이드아웃)
     · infEndRun / infInitFloor(런 종료·층 전환 → 정리). 바이트 패치 없음.
   · 튠: DEMO.bossBgm=false 로 끄기 · DEMO.bossBgmGain 으로 레벨 · 콘솔 BOSS_BGM.report()
   ══════════════════════════════════════════════ */
const BOSS_BGM={
 url:'assets/audio/bgm/boss-blood-ascendant.mp3',
 level:.78,            /* 앰비언스 대비 곡 자체 레벨 — 음악 슬라이더(AU.vol.mus)에 곱해진다 */
 fadeIn:.55, fadeOut:2.8,
 bus:null, buf:null, pending:null, failed:false, loop:null,
 node:null,            /* Web Audio 재생 중 {s,g} */
 el:null, elK:0, elTimer:null,
 active:false, elMode:false, rev:0,

 cfg(){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  return {on:D.bossBgm!==false, gain:(D.bossBgmGain!=null?D.bossBgmGain:1)};
 },
 forceEl(){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  if(D.ambienceElement===true||D.bossBgmElement===true)return true;
  try{return location.protocol==='file:';}catch(e){return false;}
 },
 ensureBus(){
  AU.init(); if(!AU.ready)return null;
  if(!this.bus){
   this.bus=AU.ctx.createGain();
   this.bus.gain.value=this.busLevel();
   this.bus.connect(AU.mas);
  }
  return this.bus;
 },
 busLevel(){
  return Math.max(0.0001,(AU.muted?0:Math.max(0,AU.vol.mus))*this.level*this.cfg().gain);
 },
 elLevel(){
  if(AU.muted)return 0;
  const v=Math.max(0,AU.vol.master)*Math.max(0,AU.vol.mus)*this.level*this.cfg().gain;
  return Math.max(0,Math.min(1,v));
 },
 syncVolume(){
  if(this.bus){
   try{this.bus.gain.setTargetAtTime(this.busLevel(),AU.t(),.03);}catch(e){this.bus.gain.value=this.busLevel();}
  }
  if(this.el&&!this.elTimer){try{this.el.volume=this.elLevel()*this.elK;}catch(e){}}
 },

 /* 디코드 — 앞뒤 무음을 잘라 루프 지점으로 쓴다 (mp3 인코더 패딩 때문에 이음새가 비는 것 방지) */
 load(){
  if(!AU.ready)return Promise.resolve(false);
  if(this.buf)return Promise.resolve(true);
  if(this.failed||this.forceEl()){this.elMode=true;return Promise.resolve(false);}
  if(this.pending)return this.pending;
  return this.pending=fetch(this.url)
   .then(r=>{if(!r.ok)throw new Error(r.status);return r.arrayBuffer();})
   .then(ab=>new Promise((res,rej)=>{
     const p=AU.ctx.decodeAudioData(ab,b=>res(b),rej);
     if(p&&p.then)p.then(res,rej);
   }))
   .then(b=>{this.buf=b;this.loop=this.trim(b);this.pending=null;return true;})
   .catch(()=>{this.failed=true;this.elMode=true;this.pending=null;return false;});
 },
 trim(b){
  const ch=b.getChannelData(0),th=.012;let s=0,e=ch.length-1;
  while(s<e&&Math.abs(ch[s])<th)s++;
  while(e>s&&Math.abs(ch[e])<th)e--;
  const a=s/b.sampleRate,z=(e+1)/b.sampleRate;
  return (z-a>5)?{a,z}:{a:0,z:b.duration};
 },
 prefetch(){ if(AU.ready&&!this.forceEl()&&this.cfg().on)this.load(); },

 playing(){return this.active;},

 play(){
  if(!this.cfg().on)return;
  AU.init(); if(!AU.ready)return;
  if(this.active){                                   /* 이미 재생 중 — 볼륨만 맞추고 <audio> 는 재개 */
   this.syncVolume();
   if(this.el&&this.el.paused&&!this.node){try{const p=this.el.play();if(p&&p.catch)p.catch(()=>{});}catch(e){}}
   return;
  }
  this.active=true;const rev=++this.rev;
  this.load().then(ok=>{
   if(!this.active||rev!==this.rev)return;           /* 로딩 중 stop 됐거나 다시 시작됨 */
   const bus=ok?this.ensureBus():null;
   if(ok&&bus&&this.buf){                            /* ── Web Audio ── */
    const c=AU.ctx,t=AU.t(),b=this.buf;
    const s=c.createBufferSource(); s.buffer=b; s.loop=true;
    s.loopStart=this.loop.a; s.loopEnd=this.loop.z;
    const g=c.createGain();
    g.gain.setValueAtTime(.0001,t);
    g.gain.exponentialRampToValueAtTime(1,t+this.fadeIn);
    s.connect(g); g.connect(bus);
    s.start(t,this.loop.a);
    this.node={s,g};
    this.syncVolume();
   }else{                                            /* ── <audio> 폴백 ── */
    if(!this.el){
     const a=new Audio(); a.loop=true; a.preload='auto'; a.src=this.url;
     a.hidden=true; a.setAttribute('aria-hidden','true'); document.body.appendChild(a);
     this.el=a;
    }
    try{this.el.currentTime=0;}catch(e){}
    this.el.volume=0; this.elK=0;
    try{const p=this.el.play();if(p&&p.catch)p.catch(()=>{});}catch(e){}
    this.ramp(1,this.fadeIn);
   }
  });
 },
 ramp(to,sec,onDone){
  if(this.elTimer){clearInterval(this.elTimer);this.elTimer=null;}
  const from=this.elK,step=.06;let t=0;
  this.elTimer=setInterval(()=>{
   t+=step;const p=Math.min(1,t/Math.max(.05,sec));
   this.elK=from+(to-from)*p;
   try{if(this.el)this.el.volume=this.elLevel()*this.elK;}catch(e){}
   if(p>=1){clearInterval(this.elTimer);this.elTimer=null;if(onDone)onDone();}
  },60);
 },

 /* fade 초 동안 사그라든 뒤 정지. 이미 멈추는 중이면 다시 건드리지 않는다(짧은 페이드로 덮어쓰기 방지) */
 stop(fade){
  if(!this.active)return;
  this.active=false;this.rev++;
  const fo=(fade!=null)?fade:this.fadeOut;
  const n=this.node;this.node=null;
  if(n){
   const t=AU.t();
   try{
    n.g.gain.cancelScheduledValues(t);
    n.g.gain.setValueAtTime(Math.max(.0002,n.g.gain.value),t);
    n.g.gain.exponentialRampToValueAtTime(.0001,t+Math.max(.05,fo));
   }catch(e){}
   setTimeout(()=>{try{n.s.stop();}catch(e){}},(fo+.15)*1000);
  }
  if(this.el&&!this.el.paused){
   const el=this.el;
   this.ramp(0,fo,()=>{try{el.pause();el.currentTime=0;}catch(e){}});
  }
 },

 report(){
  return {protocol:location.protocol, active:this.active, elMode:this.elMode, failed:this.failed,
   decoded:!!this.buf, dur:this.buf?+this.buf.duration.toFixed(2):null, loop:this.loop,
   webAudio:!!this.node, bus:this.bus?+this.bus.gain.value.toFixed(3):null,
   el:this.el?{paused:this.el.paused,vol:+this.el.volume.toFixed(3),t:+this.el.currentTime.toFixed(1),err:this.el.error?this.el.error.code:null}:null,
   route:(typeof BGM_ROUTE!=='undefined')?BGM_ROUTE.current:null, prev:(typeof BGM_ROUTE!=='undefined')?BGM_ROUTE.prevRoute:null};
 }
};

/* ── BGM_ROUTE 에 'boss' 경로 얹기 ─────────────────────────────
   기존 세 경로(procedural/lobby/purple)로 갈아타면 보스 BGM 은 자동으로 꺼진다.
   ensure()(탭 복귀·포커스)는 'boss' 상태를 그대로 유지한다. */
(function(){
 const R=BGM_ROUTE;
 R.prevRoute=null;
 ['claimProcedural','useLobby','usePurple'].forEach(name=>{
  const orig=R[name];
  R[name]=function(){
   if(this.current==='boss'){this.prevRoute=null;BOSS_BGM.stop(.6);}
   if(this.bossBackTimer){clearTimeout(this.bossBackTimer);this.bossBackTimer=null;}
   const out=orig.apply(this,arguments);
   if(name==='usePurple')BOSS_BGM.prefetch();        /* 땅굴에 들어오면 보스 곡을 미리 디코드 */
   return out;
  };
 });
 R.bossBackTimer=null;
 R.useBoss=function(){
  if(this.current==='boss'){
   if(this.bossBackTimer){clearTimeout(this.bossBackTimer);this.bossBackTimer=null;}   /* 페이드아웃 대기 중 재등장(보스 랩) */
   BOSS_BGM.play();return;
  }
  if(!BOSS_BGM.cfg().on)return;
  this.prevRoute=this.current;
  this.current='boss';const rev=++this.revision;
  LOBBY_MUSIC.pause();PURPLE_MUSIC.pause();MUS.setExternal(true);   /* 앰비언스 페이드아웃 → 보스 곡 페이드인 */
  BOSS_BGM.play();
  const p=AU.resume();
  if(p&&typeof p.then==='function')p.then(()=>{
   if(this.current==='boss'&&this.revision===rev)BOSS_BGM.play();
  }).catch(()=>{});
 };
 /* opt.fade: 보스 곡 페이드아웃(초) · opt.resumeAfter: 이전 BGM 복귀까지 대기(초, 기본 fade-0.6) */
 R.endBoss=function(opt){
  if(this.current!=='boss'||this.bossBackTimer)return;   /* 이미 페이드아웃 대기 중이면 중복 호출 무시 */
  opt=opt||{};
  const fade=(opt.fade!=null)?opt.fade:BOSS_BGM.fadeOut;
  const prev=this.prevRoute||'purple';this.prevRoute=null;
  BOSS_BGM.stop(fade);
  const back=()=>{
   this.bossBackTimer=null;
   if(this.current!=='boss')return;                 /* 대기 중 다른 경로로 이미 갈아탔다 */
   if(prev==='lobby')this.useLobby();
   else if(prev==='procedural')this.claimProcedural();
   else this.usePurple(false);
  };
  const wait=(opt.resumeAfter!=null)?opt.resumeAfter:Math.max(0,fade-.6);
  if(wait>0)this.bossBackTimer=setTimeout(back,wait*1000);else back();
 };
 const _ensure=R.ensure;
 R.ensure=function(){
  if(document.hidden)return;
  if(this.current==='boss'){this.useBoss();return;}
  return _ensure.apply(this,arguments);
 };
 /* 음악 슬라이더·뮤트는 AMBI.syncVolume 을 타고 들어온다 */
 if(typeof AMBI!=='undefined'){
  const _sync=AMBI.syncVolume;
  AMBI.syncVolume=function(){_sync.apply(this,arguments);BOSS_BGM.syncVolume();};
 }
})();

/* ── 게임 훅 (런타임 래핑) ───────────────────────────────────── */
(function(){
 const wrap=(name,fn)=>{
  if(typeof window[name]!=='function')return;
  const orig=window[name];
  window[name]=function(){return fn.call(this,orig,arguments);};
 };
 /* 등장 — 등장 연출(TCBOSSFX)이 같은 훅에서 시작하므로 음악도 같은 프레임에 들어간다 */
 wrap('infSpawnBoss',function(orig,args){
  const out=orig.apply(this,args);
  try{if(typeof INF!=='undefined'&&INF.bossActive)BGM_ROUTE.useBoss();}catch(e){}
  return out;
 });
 /* 처치 — 죽음 시네마틱(3.7s)이 도는 동안 곡이 사그라들고, 끝날 무렵 땅굴 앰비언스가 돌아온다 */
 wrap('infBossDefeated',function(orig,args){
  const e=args[0];
  const was=typeof INF!=='undefined'&&INF.active&&INF.bossActive&&e&&e.boss;
  const out=orig.apply(this,args);
  try{if(was)BGM_ROUTE.endBoss({fade:3.0,resumeAfter:2.6});}catch(err){}
  return out;
 });
 /* 런 종료(사망·생환 정산) — 보스전 중이었으면 빠르게 정리 */
 wrap('infEndRun',function(orig,args){
  const out=orig.apply(this,args);
  try{if(typeof CREW!=='undefined'&&CREW.phase==='infiniteResult')BGM_ROUTE.endBoss({fade:1.4});}catch(e){}
  return out;
 });
 /* 층 전환·재시작 — 보스 상태가 리셋된다 */
 wrap('infInitFloor',function(orig,args){
  try{BGM_ROUTE.endBoss({fade:1.0});}catch(e){}
  return orig.apply(this,args);
 });

 /* 상태 안전망 — 코옵 게스트는 보스가 infSpawnBoss 를 거치지 않고 호스트 스냅샷(퍼펫)으로
    도착하므로 함수 훅이 안 걸린다. 보스 생존 상태를 주기적으로 보고 경로를 맞춘다.
    호스트에서는 훅이 먼저 처리하므로 여기서는 거의 아무 일도 하지 않는다. */
 setInterval(function(){
  try{
   if(typeof INF==='undefined'||typeof BGM_ROUTE==='undefined'||typeof CREW==='undefined')return;
   const R=BGM_ROUTE,b=INF.boss;
   const alive=INF.active&&INF.bossActive&&b&&b.hp>0&&!b.bossDying;
   if(alive){
    if(R.current!=='boss'&&!R.bossBackTimer&&(CREW.phase==='play'||CREW.phase==='bossIntro'))R.useBoss();
   }else if(R.current==='boss'&&!R.bossBackTimer){
    /* 보스가 사라졌는데 곡이 남아 있다 — 게스트 처치(스냅샷에서 빠짐)·런 종료 등 */
    R.endBoss({fade:3.0,resumeAfter:2.6});
   }
  }catch(e){}
 },200);
})();
/* BOSS-BGM-BLOCK:END */
