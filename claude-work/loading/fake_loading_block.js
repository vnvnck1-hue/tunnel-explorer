/* FAKE-LOADING-BLOCK:BEGIN */
/* ══════════════════════════════════════════════════════════════════
   LOAD7.8.2 — 페이크 로딩 화면 (모든 모드 공통)
   · 게임이 실제로 시작되는 관문(infStartRun · startCrewMission)을 런타임으로 감싼다.
     원본은 그대로 동기 실행(보스 랩·코옵처럼 시작 직후를 기대하는 호출자 보호)하고,
     그 위를 5초 동안 로딩 화면으로 덮은 채 월드 루프(loopStep)만 멈춘다.
   · 게이지는 매번 무작위로 짜인 '끊김 스케줄'(정지·급점프·느린 구간)을 따라가고,
     화면 갱신도 불규칙한 틱으로만 일어나서 실제 로딩처럼 버벅인다.
   · GIF 는 ImageDecoder 로 프레임을 뽑아 캔버스에 그린다 — 프레임 번호가 진행도에
     묶여 있어서 게이지가 멈추면 그림도 멈추고, 게이지가 튀면 그림도 튄다.
     ImageDecoder 가 없으면 <img> 로 자연 재생, GIF 파일이 없으면 캔버스 드릴 폴백.
   · 경로: TCLOAD.cfg.gif = 'assets/loading/loading.gif' (단일 파일 빌드의 자산 맵도 인식)
   · 콘솔 테스트: TCLOAD.show({noFreeze:true})
   ══════════════════════════════════════════════════════════════════ */
(function(){
'use strict';
if(window.TCLOAD)return;
var TCLOAD={VERSION:'LOAD7.8.2',active:false,freeze:false,
 cfg:{enabled:true,gif:'assets/loading/loading.gif',total:5000,fill:4550,hold:200,fade:250,art:210}};
window.TCLOAD=TCLOAD;

/* ── 스타일 ── */
var css=''+
'#tcFakeLoad{position:fixed;inset:0;z-index:880;display:none;width:100vw;height:100dvh;background:#050310;color:#f2e7ff;'+
 'font-family:"Pretendard","Malgun Gothic",sans-serif;place-items:center;overflow:hidden;pointer-events:auto;user-select:none;cursor:progress}'+
'#tcFakeLoad.on{display:grid}'+
'#tcFakeLoad .tclVig{position:absolute;inset:0;background:radial-gradient(ellipse at 50% 42%,rgba(90,60,140,.28),rgba(5,3,16,0) 55%),'+
 'radial-gradient(ellipse at 50% 100%,rgba(0,0,0,.55),rgba(0,0,0,0) 60%)}'+
'#tcFakeLoad .tclGrid{position:absolute;inset:0;opacity:.13;background-image:linear-gradient(rgba(190,160,255,.35) 1px,transparent 1px),'+
 'linear-gradient(90deg,rgba(190,160,255,.35) 1px,transparent 1px);background-size:44px 44px;mask-image:radial-gradient(ellipse at 50% 45%,#000 30%,transparent 78%);'+
 '-webkit-mask-image:radial-gradient(ellipse at 50% 45%,#000 30%,transparent 78%)}'+
'#tcFakeLoad .tclWrap{position:relative;display:flex;flex-direction:column;align-items:center;gap:14px;width:min(520px,86vw)}'+
'#tcFakeLoad .tclArt{position:relative;width:var(--tcl-art,210px);height:var(--tcl-art,210px);display:grid;place-items:center}'+
'#tcFakeLoad .tclArt canvas,#tcFakeLoad .tclArt img{display:block;width:100%;height:100%;object-fit:contain;image-rendering:auto;'+
 'filter:drop-shadow(0 10px 26px rgba(120,80,200,.35))}'+
'#tcFakeLoad .tclHead{display:flex;align-items:baseline;justify-content:space-between;width:100%;margin-top:6px}'+
'#tcFakeLoad .tclTitle{font-family:"ARCO",ui-monospace,monospace;font-weight:800;font-size:22px;letter-spacing:.14em;color:#ffd36e;text-shadow:0 0 18px rgba(255,211,110,.35)}'+
'#tcFakeLoad .tclTitle i{font-style:normal;display:inline-block;min-width:1.6em;text-align:left;color:#ffe7a8}'+
'#tcFakeLoad .tclPct{font-family:"ARCO",ui-monospace,monospace;font-weight:800;font-size:26px;color:#f2e7ff;font-variant-numeric:tabular-nums;min-width:4ch;text-align:right}'+
'#tcFakeLoad .tclBar{position:relative;width:100%;height:14px;border-radius:3px;background:rgba(255,255,255,.06);'+
 'border:1px solid rgba(190,160,255,.28);box-shadow:inset 0 1px 0 rgba(255,255,255,.05),0 0 0 3px rgba(5,3,16,.6)}'+
'#tcFakeLoad .tclFill{position:absolute;left:0;top:0;bottom:0;width:0%;border-radius:2px;'+
 'background:linear-gradient(90deg,#8f5fd0,#c7a0ff 60%,#ffd36e);box-shadow:0 0 14px rgba(199,160,255,.55)}'+
'#tcFakeLoad .tclFill:after{content:"";position:absolute;right:-1px;top:-3px;bottom:-3px;width:3px;background:#fff8e6;box-shadow:0 0 10px #ffd36e}'+
'#tcFakeLoad .tclTicks{position:absolute;inset:0;background-image:repeating-linear-gradient(90deg,transparent 0 calc(10% - 1px),rgba(5,3,16,.7) calc(10% - 1px) 10%);pointer-events:none}'+
'#tcFakeLoad .tclMsg{width:100%;display:flex;justify-content:space-between;font-size:13px;color:#b9a8d8;letter-spacing:.02em;min-height:1.4em}'+
'#tcFakeLoad .tclMsg b{font-weight:600;color:#e7dbff}'+
'#tcFakeLoad .tclMsg span{color:#8f7fb0;font-family:"ARCO",ui-monospace,monospace;font-size:11px;letter-spacing:.08em}'+
'#tcFakeLoad .tclSub{font-family:"ARCO",ui-monospace,monospace;font-size:11px;letter-spacing:.22em;color:#9a86b5;margin-top:-4px}'+
'#tcFakeLoad .tclTip{position:absolute;left:0;right:0;bottom:6vh;text-align:center;font-size:12px;color:#7f6fa0;letter-spacing:.02em}';
var st=document.createElement('style');st.id='tcFakeLoadCss';st.textContent=css;document.head.appendChild(st);

/* ── 진행 메시지(가짜 작업 목록) ── */
var MSGS=[[0,'지층 데이터 수신 중'],[12,'굴착 장비 점검'],[27,'터널 조명 예열'],[44,'크루 장비 배급'],[58,'광맥 지도 동기화'],
 [73,'보급선 연결 대기'],[87,'최종 안전 점검'],[100,'출격 준비 완료']];
var TIPS=['드릴은 과열되면 잠깐 멈춘다 — 열 게이지를 보며 끊어서 파자.','장악도 40% 가 되면 무언가가 깨어난다.',
 'G 키로 팀 핑을 찍어 크루에게 방향을 알릴 수 있다.','탈출 포트를 놓고 오면 유물은 두고 나가야 한다.',
 '기반암은 뚫리지 않는다 — 우회로를 찾자.','휴식 구간에서 장비를 정비하고 다음 심층으로 내려가자.'];

/* ── DOM ── */
var root=null,fill=null,pctEl=null,msgEl=null,msgIdx=null,dotsEl=null,subEl=null,tipEl=null,artBox=null,cv=null,g2=null,img=null;
function build(){
 if(root)return;
 root=document.createElement('div');root.id='tcFakeLoad';root.setAttribute('aria-hidden','true');
 root.innerHTML='<div class="tclVig"></div><div class="tclGrid"></div>'+
  '<div class="tclWrap"><div class="tclArt"></div>'+
  '<div class="tclSub">TUNNEL CREW</div>'+
  '<div class="tclHead"><div class="tclTitle">LOADING<i></i></div><div class="tclPct">0%</div></div>'+
  '<div class="tclBar"><div class="tclFill"></div><div class="tclTicks"></div></div>'+
  '<div class="tclMsg"><b></b><span></span></div></div>'+
  '<div class="tclTip"></div>';
 document.body.appendChild(root);
 fill=root.querySelector('.tclFill');pctEl=root.querySelector('.tclPct');msgEl=root.querySelector('.tclMsg b');msgIdx=root.querySelector('.tclMsg span');
 dotsEl=root.querySelector('.tclTitle i');subEl=root.querySelector('.tclSub');tipEl=root.querySelector('.tclTip');artBox=root.querySelector('.tclArt');
 root.addEventListener('contextmenu',function(e){e.preventDefault();});
}

/* ── GIF 프레임 디코드 (ImageDecoder) ── */
var GIF={state:'idle',frames:null,durMs:0,w:0,h:0,imgOk:null,url:''};
function gifUrl(){
 var p=TCLOAD.cfg.gif,m=window.__TC_ASSETS;
 if(m&&m[p])return m[p];
 return p;
}
function probeImg(){
 /* ImageDecoder 가 없거나 디코드에 실패했을 때 — <img> 로 존재 여부·폴백을 확보 */
 var probe=new Image();probe.decoding='async';
 probe.onload=function(){GIF.imgOk=probe;if(!GIF.w){GIF.w=probe.naturalWidth;GIF.h=probe.naturalHeight;}if(GIF.state==='loading')GIF.state='imgonly';};
 probe.onerror=function(){GIF.imgOk=false;if(GIF.state==='loading')GIF.state='missing';};
 probe.src=GIF.url;
}
function decodeGif(){
 if(GIF.state!=='idle')return;
 GIF.state='loading';GIF.url=gifUrl();
 if(!('ImageDecoder' in window)){probeImg();return;}
 fetch(GIF.url).then(function(r){if(!r.ok)throw new Error('http '+r.status);return r.arrayBuffer();}).then(function(buf){
  var dec=new ImageDecoder({data:buf,type:'image/gif',preferAnimation:true});
  return dec.tracks.ready.then(function(){
   var track=dec.tracks.selectedTrack,n=track?track.frameCount:0;
   if(!n)throw new Error('no frames');
   var frames=[],dur=0,i=0;
   function next(){
    if(i>=n)return frames;
    return dec.decode({frameIndex:i}).then(function(res){
     var vf=res.image;dur+=(vf.duration||100000)/1000;i++;
     return createImageBitmap(vf).then(function(bm){vf.close();frames.push(bm);return next();});
    });
   }
   return next().then(function(fr){GIF.frames=fr;GIF.durMs=dur;GIF.w=fr[0].width;GIF.h=fr[0].height;GIF.state='ready';try{dec.close();}catch(e){}});
  });
 }).catch(function(err){
  var msg=err&&err.message||'';
  if(/^http 4\d\d/.test(msg)){GIF.imgOk=false;GIF.state='missing';return;}   /* 파일 없음 — 캔버스 드릴 폴백 */
  if(window.console)console.warn('[LOAD7.8.2] GIF 프레임 디코드 실패 — <img> 폴백',msg);
  probeImg();
 });
}
/* GIF 교체(콘솔·툴용): TCLOAD.setGif('assets/loading/xxx.gif') — 다음 show() 부터 적용 */
TCLOAD.setGif=function(p){
 if(p)TCLOAD.cfg.gif=p;
 if(GIF.frames)for(var i=0;i<GIF.frames.length;i++)try{GIF.frames[i].close();}catch(e){}
 GIF.state='idle';GIF.frames=null;GIF.durMs=0;GIF.w=0;GIF.h=0;GIF.imgOk=null;GIF.url='';
 decodeGif();
};

/* ── 아트 그리기 ── */
var artMode='';
function setupArt(){
 var want=(GIF.state==='ready')?'frames':(GIF.state==='imgonly'&&GIF.imgOk)?'img':'canvas';
 if(want===artMode&&(img||g2))return;
 artMode=want;artBox.innerHTML='';artBox.style.setProperty('--tcl-art',TCLOAD.cfg.art+'px');
 img=null;cv=null;g2=null;lastFrame=-1;lastAng=null;
 if(want==='img'){img=document.createElement('img');img.alt='';img.src=GIF.url;artBox.appendChild(img);return;}
 cv=document.createElement('canvas');var dpr=Math.min(2,window.devicePixelRatio||1),S=Math.round(TCLOAD.cfg.art*dpr);cv.width=S;cv.height=S;artBox.appendChild(cv);g2=cv.getContext('2d');
}
var lastFrame=-1,lastAng=null;
function drawArt(f,tickN){
 if(!g2)return;
 var S=cv.width;
 if(GIF.state==='ready'&&GIF.frames&&GIF.frames.length){
  var n=GIF.frames.length,loops=Math.max(1,Math.min(6,Math.round(TCLOAD.cfg.fill/Math.max(200,GIF.durMs||1000))));
  var idx=Math.floor(Math.min(f,.9999)*n*loops)%n;
  if(idx===lastFrame)return;lastFrame=idx;
  var bm=GIF.frames[idx],sc=Math.min(S/bm.width,S/bm.height),w=bm.width*sc,h=bm.height*sc;
  g2.clearRect(0,0,S,S);g2.imageSmoothingEnabled=true;g2.drawImage(bm,(S-w)/2,(S-h)/2,w,h);
  return;
 }
 /* 폴백 — 드릴 헤드: 회전각이 진행도에 묶여 게이지와 같이 멈추고 튄다 */
 var ang=f*Math.PI*2*3;
 if(lastAng!==null&&Math.abs(ang-lastAng)<.001)return;lastAng=ang;
 var c=S/2,r=S*.30;g2.clearRect(0,0,S,S);
 g2.save();g2.translate(c,c);
 /* 회전 링(굴착 궤적) */
 g2.lineWidth=S*.018;g2.strokeStyle='rgba(143,95,208,.35)';g2.beginPath();g2.arc(0,0,r*1.32,0,Math.PI*2);g2.stroke();
 g2.strokeStyle='#ffd36e';g2.lineCap='round';g2.beginPath();g2.arc(0,0,r*1.32,ang-.9,ang);g2.stroke();
 g2.strokeStyle='#c7a0ff';g2.beginPath();g2.arc(0,0,r*1.32,ang+Math.PI-.5,ang+Math.PI);g2.stroke();
 g2.rotate(ang*.5);
 /* 드릴 헤드 — 톱니 원반 */
 g2.beginPath();for(var i=0;i<12;i++){var a0=i/12*Math.PI*2,a1=(i+.5)/12*Math.PI*2,a2=(i+1)/12*Math.PI*2;
  g2.lineTo(Math.cos(a0)*r,Math.sin(a0)*r);g2.lineTo(Math.cos(a1)*r*1.16,Math.sin(a1)*r*1.16);g2.lineTo(Math.cos(a2)*r,Math.sin(a2)*r);}
 g2.closePath();var grad=g2.createRadialGradient(-r*.3,-r*.3,r*.1,0,0,r*1.2);grad.addColorStop(0,'#5a3f86');grad.addColorStop(1,'#1c1230');
 g2.fillStyle=grad;g2.fill();g2.lineWidth=S*.012;g2.strokeStyle='#c7a0ff';g2.stroke();
 /* 중심 코어 + 나선 홈 */
 g2.fillStyle='#ffd36e';g2.beginPath();g2.arc(0,0,r*.22,0,Math.PI*2);g2.fill();
 g2.strokeStyle='rgba(255,211,110,.7)';g2.lineWidth=S*.014;
 for(var k=0;k<3;k++){g2.beginPath();var b0=k/3*Math.PI*2;g2.arc(0,0,r*.62,b0,b0+1.1);g2.stroke();}
 g2.restore();
 /* 파편 — 틱마다 위치가 바뀐다(버벅임 강조) */
 var seed=(tickN*7919)%1000;g2.fillStyle='rgba(199,160,255,.7)';
 for(var j=0;j<6;j++){var q=((seed+j*173)%1000)/1000,rr=r*1.5+q*r*.55,aa=ang+j*1.05+q*.6;g2.fillRect(c+Math.cos(aa)*rr-2,c+Math.sin(aa)*rr-2,4,4);}
}

/* ── 끊김 스케줄 ── */
function makeSchedule(fill){
 var n=9+Math.floor(Math.random()*3),dw=[],vw=[],i;
 for(i=0;i<n;i++){dw.push(.35+Math.random());vw.push(.3+Math.random()*.9);}
 /* 중간 정지 2곳 (각 0.5~0.9초쯤) — 서로 붙지 않게 */
 var used={},s1=2+Math.floor(Math.random()*(n-7)),s2=s1+2+Math.floor(Math.random()*(n-5-s1));   /* 2 ≤ s1 < s2-1 ≤ n-5 */
 [s1,s2].forEach(function(s){used[s]=1;vw[s]=0;dw[s]=.9+Math.random()*.7;});
 /* 고전적인 막판 정지(약 90%, 0.7~1초쯤) */
 vw[n-2]=0;dw[n-2]=1.3+Math.random()*.5;used[n-2]=1;
 /* 급점프 1곳 — 전체의 8~14% 를 한 번에 */
 var jI;do{jI=1+Math.floor(Math.random()*(n-3));}while(used[jI]);dw[jI]=.05;
 var share=function(idx,r){var others=0;for(var q=0;q<n;q++)if(q!==idx)others+=vw[q];vw[idx]=others*r/(1-r);};
 share(jI,.08+Math.random()*.06);
 /* 첫 구간은 짧고 빠르게 붙는다 (0.25~0.45초에 6~12%) */
 dw[0]=.5+Math.random()*.3;share(0,.06+Math.random()*.06);
 var sd=0,sv=0;for(i=0;i<n;i++){sd+=dw[i];sv+=vw[i];}
 var knots=[{t:0,p:0}],t=0,p=0;
 for(i=0;i<n;i++){t+=dw[i]/sd*fill;p+=vw[i]/sv*100;knots.push({t:t,p:p});}
 knots[knots.length-1].t=fill;knots[knots.length-1].p=100;
 return knots;
}
function progressAt(knots,t){
 if(t<=0)return 0;
 for(var i=1;i<knots.length;i++){var a=knots[i-1],b=knots[i];
  if(t<=b.t){var span=b.t-a.t;if(span<=0)return b.p;return a.p+(b.p-a.p)*((t-a.t)/span);}}
 return 100;
}

/* ── 입력 차단(로딩 중 키 입력이 게임·모달에 닿지 않게) ── */
function keyBlock(e){
 if(!TCLOAD.active)return;
 if(e.key==='F5'||e.key==='F11'||e.key==='F12')return;
 e.stopImmediatePropagation();e.preventDefault();
}
['keydown','keyup','keypress'].forEach(function(t){window.addEventListener(t,keyBlock,true);});

/* ── 월드 루프 정지 — loopStep 을 감싼다 ── */
(function(){
 if(typeof loopStep!=='function')return;
 var prev=loopStep;
 loopStep=function(now){
  if(TCLOAD.freeze){try{last=now;}catch(e){}return;}   /* dt 누적 방지(어차피 .05 로 클램프되지만) */
  return prev.apply(this,arguments);
 };
})();

/* ── 표시 ── */
var run=null;
function modeLabel(){
 try{
  if(typeof COOP!=='undefined'&&COOP.active)return 'CO-OP';
  if(typeof OBSERVER!=='undefined'&&OBSERVER.active)return 'OBSERVER';
  if(typeof INF!=='undefined'&&INF.active)return INF.testScene?'TEST SCENE':(INF.planetRun?'EXPEDITION':'INFINITE MODE');
  return 'MISSION';
 }catch(e){return 'TUNNEL CREW';}
}
function roleLabel(){
 try{var r=(typeof CREW!=='undefined'&&CREW.role)?CREW.role:null;return r&&r.name?String(r.name).toUpperCase():'';}catch(e){return '';}
}
TCLOAD.show=function(opts){
 opts=opts||{};
 if(!TCLOAD.cfg.enabled)return false;
 if(TCLOAD.active)return false;
 build();decodeGif();artMode='';setupArt();
 var cfg=TCLOAD.cfg,fillMs=cfg.fill,knots=makeSchedule(fillMs),t0=performance.now();
 TCLOAD.active=true;TCLOAD.freeze=!opts.noFreeze;
 root.style.opacity='1';root.classList.add('on');
 fill.style.width='0%';pctEl.textContent='0%';msgEl.textContent=MSGS[0][1]+'…';msgIdx.textContent='01 / '+String(MSGS.length-1).padStart(2,'0');
 var ml=modeLabel(),rl=roleLabel();subEl.textContent='TUNNEL CREW · '+ml+(rl?' · '+rl:'');
 tipEl.textContent='TIP · '+TIPS[Math.floor(Math.random()*TIPS.length)];
 drawArt(0,0);
 var nextTick=t0,tickN=0,shown=0,phase='fill',phaseT=0,done=false,raf=0,safety=0;
 function finish(){
  if(done)return;done=true;
  cancelAnimationFrame(raf);clearTimeout(safety);
  TCLOAD.freeze=false;TCLOAD.active=false;
  root.classList.remove('on');root.style.opacity='';
  if(opts.onDone)try{opts.onDone();}catch(e){}
 }
 run={finish:finish};
 function frame(now){
  if(done)return;
  raf=requestAnimationFrame(frame);
  var el=now-t0;
  if(phase==='fill'){
   /* 불규칙한 틱으로만 갱신 — 프레임 드랍처럼 보이게 */
   if(now>=nextTick){
    tickN++;
    var hiccup=Math.random()<.12;
    nextTick=now+(hiccup?220+Math.random()*260:45+Math.random()*150);
    var p=progressAt(knots,el);
    setupArt();                                                         /* 디코드가 뒤늦게 끝났으면 프레임 캔버스로 전환 */
    p=Math.floor(p*4)/4;                                                /* 0.25% 단위 계단 */
    if(p<shown)p=shown;shown=p;
    fill.style.width=p+'%';pctEl.textContent=Math.floor(p)+'%';
    var mi=0;for(var i=0;i<MSGS.length;i++)if(p>=MSGS[i][0])mi=i;
    msgEl.textContent=MSGS[mi][1]+(p>=100?'':'…');msgIdx.textContent=String(Math.min(mi+1,MSGS.length-1)).padStart(2,'0')+' / '+String(MSGS.length-1).padStart(2,'0');
    dotsEl.textContent=p>=100?'':'.'.repeat(1+(tickN%3));
    drawArt(p/100,tickN);
   }
   if(el>=fillMs){
    shown=100;fill.style.width='100%';pctEl.textContent='100%';msgEl.textContent=MSGS[MSGS.length-1][1];msgIdx.textContent=String(MSGS.length-1).padStart(2,'0')+' / '+String(MSGS.length-1).padStart(2,'0');dotsEl.textContent='';drawArt(1,tickN);
    phase='hold';phaseT=now;
    if(window.SFX&&SFX.ui)try{SFX.ui();}catch(e){}
   }
  }else if(phase==='hold'){
   if(now-phaseT>=cfg.hold){phase='fade';phaseT=now;TCLOAD.freeze=false;}   /* 페이드 동안 월드는 이미 돌아간다 */
  }else{
   var u=Math.min(1,(now-phaseT)/cfg.fade);
   root.style.opacity=String(1-u);
   if(u>=1)finish();
  }
 }
 raf=requestAnimationFrame(frame);
 safety=setTimeout(finish,cfg.total+2500);   /* 안전망 — 어떤 경우에도 화면을 돌려준다 */
 return true;
};
TCLOAD.hide=function(){if(run)run.finish();};
TCLOAD.gifState=function(){return {state:GIF.state,url:GIF.url,frames:GIF.frames?GIF.frames.length:0,durMs:Math.round(GIF.durMs),w:GIF.w,h:GIF.h};};

/* ── 시작 관문 래핑 ── */
if(typeof infStartRun==='function'){
 var prevInf=infStartRun;
 infStartRun=function(){var v=prevInf.apply(this,arguments);try{TCLOAD.show();}catch(e){TCLOAD.freeze=false;TCLOAD.active=false;}return v;};
}
if(typeof window.startCrewMission==='function'){
 var prevMission=window.startCrewMission;
 window.startCrewMission=function(){var v=prevMission.apply(this,arguments);
  try{if(typeof CREW!=='undefined'&&CREW.phase==='play')TCLOAD.show();}catch(e){TCLOAD.freeze=false;TCLOAD.active=false;}return v;};
}

/* GIF 미리 디코드 — 부팅과 겹치지 않게 조금 늦게 */
setTimeout(decodeGif,1500);
})();
/* FAKE-LOADING-BLOCK:END */
