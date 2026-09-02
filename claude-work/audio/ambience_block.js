/* AMBIENCE-BLOCK:BEGIN */
/* ══════════════════════════════════════════════
   앰비언스 BGM — 로비/땅굴 환경음 레이어 재생기
   · 파일은 assets/audio/ambience/ 에서 읽는다 (HTML 내장 아님)
   · 기본 경로: fetch + Web Audio AudioBufferSourceNode(loop) — 루프 이음새가 없다
   · 폴백 경로: <audio loop> — file:// 로 직접 열면 fetch 가 CORS 로 막히기 때문에 필요하다
   · 한 세트에 여러 레이어를 넣으면 동시에 겹쳐 재생된다 (땅굴 = 던전 + 동굴 2겹)
   · 기존 LOBBY_MUSIC / PURPLE_MUSIC 의 play/pause 를 이쪽으로 넘겨서
     BGM_ROUTE 호출부는 하나도 건드리지 않는다
   ══════════════════════════════════════════════ */
const AMBI={
 sets:{
  /* 메인 로비 */
  lobby:[{url:'assets/audio/ambience/lobby-cave.webm', g:1.00}],
  /* 땅굴 — 두 장을 동시에 깐다. 길이가 서로 달라서 루프 주기가 겹치지 않는다 */
  tunnel:[{url:'assets/audio/ambience/tunnel-dungeon.ogg',     g:1.19},
          /* 원본 녹음 레벨이 매우 낮아서 크게 올려야 두 겹이 대등하게 들린다 (실측 보정) */
          {url:'assets/audio/ambience/tunnel-cave-stereo.ogg', g:4.48}]
 },
 bus:null, buf:{}, live:{}, pending:{}, failed:{}, els:{}, elMode:false,
 fadeIn:1.4, fadeOut:0.9,

 cfg(){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  return {on:D.ambience!==false, gain:(D.ambienceGain!=null?D.ambienceGain:1)};
 },
 /* file:// 에서는 fetch 가 막히므로 처음부터 <audio> 경로로 간다 */
 forceEl(){
  const D=(typeof DEMO!=='undefined')?DEMO:{};
  if(D.ambienceElement===true)return true;
  try{return location.protocol==='file:';}catch(e){return false;}
 },
 ensureBus(){
  AU.init();
  if(!AU.ready)return null;
  if(!this.bus){
   this.bus=AU.ctx.createGain();
   this.bus.gain.value=this.busLevel();
   this.bus.connect(AU.mas);          /* 뮤트/마스터는 mas 가 처리, 음악 슬라이더는 여기서 */
  }
  return this.bus;
 },
 busLevel(){
  return Math.max(0.0001,(AU.muted?0:Math.max(0,AU.vol.mus))*this.cfg().gain);
 },
 /* <audio> 경로는 volume 이 0~1 로 제한된다.
    레이어 최대 게인으로 정규화해서 레이어 간 밸런스만은 유지한다. */
 elLevel(key,l){
  if(AU.muted)return 0;
  const mx=Math.max.apply(null,this.sets[key].map(x=>x.g))||1;
  const v=(l.g/mx)*Math.max(0,AU.vol.master)*Math.max(0,AU.vol.mus)*this.cfg().gain;
  return Math.max(0,Math.min(1,v));
 },
 syncVolume(){
  if(this.bus){
   try{this.bus.gain.setTargetAtTime(this.busLevel(),AU.t(),.03);}catch(e){this.bus.gain.value=this.busLevel();}
  }
  for(const key in this.els)
   for(const e of this.els[key]) if(e.el&&!e.fading) e.el.volume=this.elLevel(key,e.l)*e.k;
 },

 /* 디코드 — 세트 단위. 실패한 레이어는 <audio> 폴백으로 넘어간다 */
 load(key){
  const set=this.sets[key];
  if(!set||!AU.ready)return Promise.resolve(false);
  if(this.forceEl()){this.elMode=true;return Promise.resolve(false);}
  if(this.pending[key])return this.pending[key];
  const jobs=set.map(l=>{
   if(this.buf[l.url]||this.failed[l.url])return Promise.resolve();
   return fetch(l.url)
    .then(r=>{if(!r.ok)throw new Error(r.status);return r.arrayBuffer();})
    .then(ab=>new Promise((res,rej)=>{
      const p=AU.ctx.decodeAudioData(ab,b=>{this.buf[l.url]=b;res();},rej);
      if(p&&p.then)p.then(b=>{this.buf[l.url]=b;res();},rej);
    }))
    .catch(()=>{this.failed[l.url]=true;this.elMode=true;});
  });
  return this.pending[key]=Promise.all(jobs).then(()=>{this.pending[key]=null;return true;});
 },
 /* 로비가 준비되면 땅굴도 미리 받아둔다 — 출격 순간 무음 구간이 생기지 않게 */
 prefetch(){ if(AU.ready&&!this.forceEl())this.load('lobby').then(()=>this.load('tunnel')); },

 playing(key){return !!this.live[key];},

 play(key){
  if(!this.cfg().on){this.stopAll();return;}
  AU.init(); if(!AU.ready)return;
  if(this.live[key]){this.syncVolume();return;}      /* 이미 재생 중이면 그대로 둔다 */
  for(const k in this.live) if(k!==key) this.stop(k);
  this.live[key]=[];                                  /* 중복 진입 방지 플래그 선점 */
  this.load(key).then(()=>{
   if(!this.live[key]||this.live[key].length)return;   /* 로딩 중 stop 됐거나 이미 시작됨 */
   const nodes=[];
   const bus=this.ensureBus();
   for(const l of this.sets[key]){
    const b=this.buf[l.url];
    if(b&&bus){                                       /* ── Web Audio 경로 ── */
     const c=AU.ctx,t=AU.t();
     const s=c.createBufferSource(); s.buffer=b; s.loop=true;
     const g=c.createGain();
     g.gain.setValueAtTime(.0001,t);
     g.gain.exponentialRampToValueAtTime(Math.max(.0002,l.g),t+this.fadeIn);
     s.connect(g); g.connect(bus);
     /* 레이어마다 시작 위치를 흩어서 두 장이 같은 지점에서 함께 돌지 않게 */
     s.start(t, Math.random()*b.duration);
     nodes.push({s,g});
    }else{                                            /* ── <audio> 폴백 ── */
     const e=this.el(key,l);
     if(e)nodes.push({e});
    }
   }
   if(!nodes.length){delete this.live[key];return;}    /* 전부 실패 */
   this.live[key]=nodes;
   this.syncVolume();
  });
 },

 /* <audio> 엘리먼트 확보 + 페이드인 재생 */
 el(key,l){
  this.els[key]=this.els[key]||[];
  let e=this.els[key].find(x=>x.l===l);
  if(!e){
   const a=new Audio();
   a.loop=true; a.preload='auto'; a.src=l.url;
   a.hidden=true; a.setAttribute('aria-hidden','true');
   document.body.appendChild(a);
   e={el:a,l:l,k:0,fading:null};
   this.els[key].push(e);
  }
  const target=this.elLevel(key,l);
  e.el.volume=0; e.k=0;
  try{const p=e.el.play(); if(p&&p.catch)p.catch(()=>{});}catch(err){}
  this.ramp(key,e,1,this.fadeIn);
  return e;
 },
 /* volume 은 AudioParam 이 아니라 자동 램프가 없다 — 타이머로 직접 민다 */
 ramp(key,e,to,sec,onDone){
  if(e.fading){clearInterval(e.fading);e.fading=null;}
  const step=60/1000, from=e.k; let t=0;
  e.fading=setInterval(()=>{
   t+=step;
   const p=Math.min(1,t/Math.max(.05,sec));
   e.k=from+(to-from)*p;
   try{e.el.volume=this.elLevel(key,e.l)*e.k;}catch(err){}
   if(p>=1){clearInterval(e.fading);e.fading=null;if(onDone)onDone();}
  },60);
 },

 stop(key){
  const nodes=this.live[key];
  delete this.live[key];
  if(!nodes||!nodes.length)return;
  const t=AU.t(),fo=this.fadeOut;
  for(const n of nodes){
   if(n.g){
    try{
     n.g.gain.cancelScheduledValues(t);
     n.g.gain.setValueAtTime(Math.max(.0002,n.g.gain.value),t);
     n.g.gain.exponentialRampToValueAtTime(.0001,t+fo);
    }catch(e){}
    setTimeout(()=>{try{n.s.stop();}catch(e){}},(fo+.15)*1000);
   }else if(n.e){
    this.ramp(key,n.e,0,fo,()=>{try{n.e.el.pause();n.e.el.currentTime=0;}catch(err){}});
   }
  }
 },
 stopAll(){ for(const k in this.live) this.stop(k); },

 /* 진단용 — 콘솔에서 AMBI.report() */
 report(){
  const r={protocol:location.protocol, elMode:this.elMode, ctx:AU.ctx&&AU.ctx.state,
   muted:AU.muted, mus:AU.vol.mus, master:AU.vol.master,
   decoded:Object.keys(this.buf), failed:Object.keys(this.failed),
   live:Object.keys(this.live), bus:this.bus?+this.bus.gain.value.toFixed(3):null};
  for(const k in this.els) r['el_'+k]=this.els[k].map(e=>({src:e.el.src.split('/').pop(),
   paused:e.el.paused, vol:+e.el.volume.toFixed(3), err:e.el.error?e.el.error.code:null}));
  return r;
 }
};

/* ── 오디오 잠금 해제 ──────────────────────────
   브라우저 자동재생 정책상 사용자 제스처 전에는 AudioContext 가 suspended 라 소리가 안 난다.
   기존 코드는 메뉴 버튼을 눌러 crewEnsureBgm() 이 돌 때까지 BGM 을 시작하지 않았다.
   → 로드 직후 한 번 시도하고(자동재생이 허용된 환경이면 그대로 시작),
     막혔으면 첫 입력(클릭/키/터치/휠)에서 다시 시도한다.
   저장된 음소거 설정은 존중한다 — 음소거 상태면 컨텍스트만 깨우고 BGM 은 켜지 않는다. */
const AUDIO_UNLOCK={
 evs:['pointerdown','keydown','touchstart','wheel'],
 handler:null, started:false,
 start(){
  if(AU.muted)return;                       /* 사용자가 꺼둔 상태면 건드리지 않는다 */
  if(BGM_ROUTE.current==='none')BGM_ROUTE.useLobby();
  else BGM_ROUTE.ensure();
  this.started=true;
 },
 kick(){
  AU.init();
  if(!AU.ready)return;
  const p=AU.resume();
  if(p&&p.then)p.then(()=>this.after(),()=>{}); else this.after();
 },
 after(){
  this.start();
  if(AU.ctx&&AU.ctx.state==='running')this.unbind();
 },
 bind(){
  if(this.handler)return;
  this.handler=()=>this.kick();
  this.evs.forEach(e=>window.addEventListener(e,this.handler,{capture:true,passive:true}));
 },
 unbind(){
  if(!this.handler)return;
  this.evs.forEach(e=>window.removeEventListener(e,this.handler,{capture:true}));
  this.handler=null;
 }
};
(function(){
 AUDIO_UNLOCK.bind();
 const boot=()=>AUDIO_UNLOCK.kick();
 if(document.readyState==='complete')setTimeout(boot,0);
 else window.addEventListener('load',boot,{once:true});
})();

/* 기존 두 BGM 객체를 앰비언스로 갈아끼운다 — BGM_ROUTE 쪽 코드는 그대로 쓴다.
   내장 base64(LOBBY_BGM_DATA / PURPLE_BGM_DATA)는 더 이상 로드되지 않는다. */
(function(){
 const swap=(obj,key)=>{
  obj.init=function(){return true;};
  obj.syncVolume=function(){AMBI.syncVolume();};
  obj.play=function(){
   AU.init(); if(!AU.ready)return Promise.resolve(false);
   MUS.setExternal(true);
   this.active=true; this.hasPlayed=true;
   AMBI.play(key);
   return Promise.resolve(true);
  };
  obj.pause=function(){ this.active=false; AMBI.stop(key); };
 };
 swap(LOBBY_MUSIC,'lobby');
 swap(PURPLE_MUSIC,'tunnel');
 /* AU 준비되는 즉시 두 세트 모두 미리 디코드 */
 const _init=AU.init;
 AU.init=function(){_init.call(this); if(this.ready)AMBI.prefetch();};
})();
/* AMBIENCE-BLOCK:END */
