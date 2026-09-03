# -*- coding: utf-8 -*-
"""
대량 블록 파괴 렉 완화 패치 (v7.9.0 본편) — 재실행 가능(idempotent).

측정(2026-09-03, 390x800 캔버스, 블록 60개 동시 파괴):
  damage() 60회 = 20.7ms  ← 그중 infXpPop(DOM 쓰기 + offsetWidth 강제 리플로우) 10.8ms, SFX 노드 4ms
  파괴 직후 프레임 J.step 2.9ms (파편 1500·불꽃 1780·연기 1280 생성 후 상한까지 잘라냄)
  이후 프레임 J.draw 4~5ms (파편 170개 × 꼬리 3선 + 원 2 + 폴리곤 + 음영 2)

패치:
  1. PERF_XPPOP_COALESCE — XP 팝 DOM 갱신을 마이크로태스크에서 프레임당 1회로 합침
  2. PERF_BRK_BURST     — 180ms 창 안에 4개 이상 깨지면 사운드/히트스톱 중복 생략, 파티클 수 35%로
  3. PERF_J_SPAWN_CAP   — 파티클을 상한까지만 생성(수천 개 만들고 버리지 않음)
  4. PERF_CH_DENSE      — 파편이 72개 넘으면 꼬리·음영 생략하고 본체만 그림
  5. PERF_LIT_LQ        — 자동 저품질(fps<34)일 때 벽 조명 광원 1개·그림자 1단으로
"""
import sys, io, os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, 'tunnel-crew-infinite-mode-v7.9.0.html')
if len(sys.argv) > 1:
    TARGET = sys.argv[1]

with io.open(TARGET, 'r', encoding='utf-8', newline='') as f:
    src = f.read()
NL = '\r\n' if '\r\n' in src[:5000] else '\n'
orig = src
log = []

def sub_once(s, old, new, tag):
    old = old.replace('\n', NL); new = new.replace('\n', NL)
    if new in s:
        log.append('skip  %s (already applied)' % tag); return s
    n = s.count(old)
    if n != 1:
        raise SystemExit('ANCHOR %s matched %d times' % (tag, n))
    log.append('apply %s' % tag)
    return s.replace(old, new)

# ── 1. XP 팝 합치기 ─────────────────────────────────────────
if 'PERF_XPPOP_COALESCE' not in src:
    start_key = ('let _xpPopEl=null,_xpPopAt=0,_xpPopSum=0;' + NL + 'function infXpPop(gain){')
    end_key = NL + 'function infAwardXp(base,kind,opts){'
    a = src.find(start_key)
    b = src.find(end_key, a)
    if a < 0 or b < 0:
        raise SystemExit('ANCHOR xppop not found')
    new_fn = '''/* PERF_XPPOP_COALESCE — 블록이 한 프레임에 수십 개 깨지면 호출마다 DOM 쓰기와 offsetWidth 강제 리플로우가
   일어나 큰 스파이크가 났다. 획득량만 누적하고 실제 DOM 갱신은 마이크로태스크에서 프레임당 한 번만 한다. */
let _xpPopEl=null,_xpPopAt=0,_xpPopSum=0,_xpPopPending=0,_xpPopQueued=false;
function infXpPop(gain){
 _xpPopPending+=gain;
 if(_xpPopQueued)return;
 _xpPopQueued=true;
 if(typeof queueMicrotask==='function')queueMicrotask(infXpPopFlush);else Promise.resolve().then(infXpPopFlush);
}
function infXpPopFlush(){
 _xpPopQueued=false;
 const gain=_xpPopPending;_xpPopPending=0;
 if(!(gain>0))return;
 const hud=document.getElementById('infHud');if(!hud)return;
 const now=performance.now();
 if(_xpPopEl&&_xpPopEl.isConnected&&now-_xpPopAt<420){_xpPopSum+=gain;}
 else{
  if(_xpPopEl&&_xpPopEl.isConnected)_xpPopEl.remove();
  _xpPopEl=document.createElement('div');_xpPopEl.className='ihXpPop';hud.appendChild(_xpPopEl);
  _xpPopSum=gain;
 }
 _xpPopAt=now;
 _xpPopEl.textContent='+'+_xpPopSum;
 const pct=Math.max(1.5,Math.min(98.5,INF.xp/Math.max(1,INF.xpNeed)*100));
 _xpPopEl.style.left=pct+'%';
 _xpPopEl.style.animation='none';void _xpPopEl.offsetWidth;_xpPopEl.style.animation='';
 clearTimeout(_xpPopEl._rm);_xpPopEl._rm=setTimeout(()=>{if(_xpPopEl&&_xpPopEl.isConnected)_xpPopEl.remove();},960);
}'''.replace('\n', NL)
    src = src[:a] + new_fn + src[b:]
    log.append('apply PERF_XPPOP_COALESCE')
else:
    log.append('skip  PERF_XPPOP_COALESCE (already applied)')

# ── 2. 대량 파괴 창 ─────────────────────────────────────────
src = sub_once(src,
 ' function damage(c,r,d,hx,hy,quiet){\n',
 '''/* PERF_BRK_BURST — 180ms 창 안에서 깨진 블록 수. 폭발·연쇄 붕괴처럼 한 번에 수십 개가 깨질 때
   사운드 노드·히트스톱·파티클이 블록 수만큼 쌓여 프레임이 튀는 것을 막는다.
   창은 첫 파괴 시점에 고정되므로 드릴로 천천히 연달아 깨는 일반 굴착에는 영향이 없다. */
let _brkBurstN=0,_brkBurstAt=0;
function brkBurstTick(){const n=performance.now();if(n-_brkBurstAt>180){_brkBurstAt=n;_brkBurstN=0;}return ++_brkBurstN;}
 function damage(c,r,d,hx,hy,quiet){
''', 'PERF_BRK_BURST:def')

src = sub_once(src,
 '''  const nChk=oreCol?(DEMO.brkOreChunks|0):(DEMO.brkChunks|0);
  if(nChk>0)J.chunks(x,y,nChk,oreCol?[oreCol,pal[2],pal[0]]:pal,DEMO.brkChunkSp+cb*140,hx,hy);
  /* ── 바닥에 남는 파편 — 관성으로 흩어져 한동안 그대로 있다 ── */
  const nRub=(DEMO.brkRubble|0)+((Math.random()*(DEMO.brkRubbleRand+1))|0)+(oreCol?2:0);
  if(nRub>0)spawnRubble(x,y,oreCol?[pal[1],pal[2],oreCol]:[pal[0],pal[1],pal[2]], nRub, hx, hy);
  if(DEMO.brkBurst>0)J.burst(x,y,DEMO.brkBurst|0,[pal[1],pal[2],'rgba(255,235,205,.7)'],DEMO.brkBurstSp);
  if(DEMO.brkSmoke>0)J.smoke(x,y,DEMO.brkSmoke|0,oreCol?'#FFE9C9':pal[2],70);
''',
 '''  const brkBurst=brkBurstTick(),burstCut=brkBurst>3?.35:1;   /* PERF_BRK_BURST — 대량 파괴 중엔 블록당 파티클 35% */
  const nChk=Math.ceil((oreCol?(DEMO.brkOreChunks|0):(DEMO.brkChunks|0))*burstCut);
  if(nChk>0)J.chunks(x,y,nChk,oreCol?[oreCol,pal[2],pal[0]]:pal,DEMO.brkChunkSp+cb*140,hx,hy);
  /* ── 바닥에 남는 파편 — 관성으로 흩어져 한동안 그대로 있다 ── */
  const nRub=Math.ceil(((DEMO.brkRubble|0)+((Math.random()*(DEMO.brkRubbleRand+1))|0)+(oreCol?2:0))*burstCut);
  if(nRub>0)spawnRubble(x,y,oreCol?[pal[1],pal[2],oreCol]:[pal[0],pal[1],pal[2]], nRub, hx, hy);
  if(DEMO.brkBurst>0)J.burst(x,y,Math.ceil((DEMO.brkBurst|0)*burstCut),[pal[1],pal[2],'rgba(255,235,205,.7)'],DEMO.brkBurstSp);
  if(DEMO.brkSmoke>0)J.smoke(x,y,Math.ceil((DEMO.brkSmoke|0)*burstCut),oreCol?'#FFE9C9':pal[2],70);
''', 'PERF_BRK_BURST:particles')

src = sub_once(src,
 '''  if(oreCol)(SFX.oreBreak?SFX.oreBreak():SFX.ore());else SFX.brk();
  J.stop(oreCol?78:hard?56:40);
''',
 '''  /* PERF_BRK_BURST — 같은 창에서 사운드는 4개, 히트스톱은 3개까지만 (그 이상은 겹쳐 쌓이기만 한다) */
  if(brkBurst<=4){if(oreCol)(SFX.oreBreak?SFX.oreBreak():SFX.ore());else SFX.brk();}
  if(brkBurst<=3)J.stop(oreCol?78:hard?56:40);
''', 'PERF_BRK_BURST:sfx')

# ── 3. 파티클 생성 상한 ──────────────────────────────────────
src = sub_once(src,
 'const J={p:[],t:[],ch:[],rg:[],fl:[],af:[],sp:[],sm:[],bd:[],sq:[],sr:[],dm:[],shake:0,sdx:0,sdy:0,hs:0,\n',
 '''/* PERF_J_SPAWN_CAP — step()의 상한(불꽃 220·파편 170·연기 320)까지만 생성한다.
   전에는 대량 파괴 한 프레임에 수천 개를 만들어 놓고 step()에서 잘라내 GC·정렬 비용만 냈다. */
function jSpawnRoom(list,n,cap){const room=cap-list.length;return room<=0?0:(n<=room?n:room);}
const J={p:[],t:[],ch:[],rg:[],fl:[],af:[],sp:[],sm:[],bd:[],sq:[],sr:[],dm:[],shake:0,sdx:0,sdy:0,hs:0,
''', 'PERF_J_SPAWN_CAP:def')
src = sub_once(src,
 ' burst(x,y,n,col,sp){for(let i=0;i<n;i++){',
 ' burst(x,y,n,col,sp){n=jSpawnRoom(this.p,n,220);for(let i=0;i<n;i++){', 'PERF_J_SPAWN_CAP:burst')
src = sub_once(src,
 ' chunks(x,y,n,cols,sp,dx,dy){const bias=(dx||dy);',
 ' chunks(x,y,n,cols,sp,dx,dy){const bias=(dx||dy);n=jSpawnRoom(this.ch,n,170);', 'PERF_J_SPAWN_CAP:chunks')
src = sub_once(src,
 ' smoke(x,y,n,col,sp){for(let i=0;i<n;i++){',
 ' smoke(x,y,n,col,sp){n=jSpawnRoom(this.sm,n,320);for(let i=0;i<n;i++){', 'PERF_J_SPAWN_CAP:smoke')

# ── 4. 파편 밀집 시 단순 그리기 ──────────────────────────────
src = sub_once(src,
 '''  for(const q of this.ch){
   const al=Math.min(1,q.life*1.4);
   if(q.tail&&q.tr){
''',
 '''  const chDense=this.ch.length>72;   /* PERF_CH_DENSE — 파편이 많으면 꼬리·음영 생략, 본체만 */
  for(const q of this.ch){
   const al=Math.min(1,q.life*1.4);
   if(!chDense&&q.tail&&q.tr){
''', 'PERF_CH_DENSE:tail')
src = sub_once(src,
 '''   /* 아래쪽 음영 → 위쪽 하이라이트 */
   cx.fillStyle='rgba(28,15,6,.24)';
   cx.beginPath();cx.ellipse(R2*.18,R2*.28,R2*.66,R2*.46,0,0,7);cx.fill();
   cx.fillStyle='rgba(255,246,228,.30)';
   cx.beginPath();cx.ellipse(-R2*.20,-R2*.26,R2*.44,R2*.30,-.5,0,7);cx.fill();
   cx.restore();}
''',
 '''   /* 아래쪽 음영 → 위쪽 하이라이트 (PERF_CH_DENSE — 밀집 시 생략) */
   if(!chDense){
   cx.fillStyle='rgba(28,15,6,.24)';
   cx.beginPath();cx.ellipse(R2*.18,R2*.28,R2*.66,R2*.46,0,0,7);cx.fill();
   cx.fillStyle='rgba(255,246,228,.30)';
   cx.beginPath();cx.ellipse(-R2*.20,-R2*.26,R2*.44,R2*.30,-.5,0,7);cx.fill();}
   cx.restore();}
''', 'PERF_CH_DENSE:shade')

# ── 5. 자동 저품질(OPT.lq) 시 벽 조명 패스 축소 ──────────────────
#   loopStep 의 자동 품질은 fps<34 이면 OPT.lq 를 켜지만 LIT 벽 조명(광원 2 × 그림자 3단 + 음영 + 림,
#   각 전체화면 drawImage 3장)은 이를 무시했다. 1080p 측정: wLights 2→1 = -3.5ms, wSteps 3→1 = -3ms.
src = sub_once(src,
 "  const steps=mode==='shadow'?Math.max(1,T.wSteps|0):1;",
 "  const steps=mode==='shadow'?(OPT.lq?1:Math.max(1,T.wSteps|0)):1;   /* PERF_LIT_LQ */", 'PERF_LIT_LQ:steps')
src = sub_once(src,
 "  return out.slice(0,Math.max(1,T.wLights|0));",
 "  return out.slice(0,Math.max(1,OPT.lq?1:T.wLights|0));   /* PERF_LIT_LQ — 저품질이면 가장 밝은 광원 하나만 */", 'PERF_LIT_LQ:lights')

if src != orig:
    with io.open(TARGET, 'w', encoding='utf-8', newline='') as f:
        f.write(src)
    log.append('WROTE %s' % TARGET)
else:
    log.append('no changes')
print('\n'.join(log))
