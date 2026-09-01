# 그레이박스 v7.7 new 안 4번(vitals 아이콘화) · 5번(스킬 슬롯)만 적용
import io, sys, os

PATH = r"C:\Users\vnvnc\Documents\ChatGPT\땅굴크루 만들기 0828\tunnel-crew-infinite-mode-v7.8.0.html"
s = io.open(PATH, encoding="utf-8", newline="").read()
orig = s

CRLF = "\r\n" in s

def rep(old, new, label):
    global s
    if CRLF:
        old = old.replace("\n", "\r\n"); new = new.replace("\n", "\r\n")
    if new in s:
        print("skip[%s] (already applied)" % label); return
    n = s.count(old)
    if n != 1:
        print("FAIL[%s]: %d matches" % (label, n)); sys.exit(1)
    s = s.replace(old, new, 1)
    print("ok  [%s]" % label)

# ─────────────────────────────── 1) CSS
CSS_ANCHOR = "#infAmmoRow{display:flex;align-items:center;gap:8px;margin-top:5px;color:#d8cce4;font-weight:900}#infAmmoText{min-width:76px;color:#ffd36e}.infReloadGauge{width:88px;height:5px;overflow:hidden;border-radius:999px;background:#2b2036}.infReloadGauge i{display:block;width:100%;height:100%;background:linear-gradient(90deg,#7febd0,#ffd36e);transition:width .06s linear}#infAmmoRow.reloading #infAmmoText{color:#7febd0;animation:infAmmoPulse .55s ease-in-out infinite}@keyframes infAmmoPulse{50%{opacity:.55}}"

CSS_NEW = """/* v7.8.0 UI 그레이박스 4번 — vitals 아이콘화: 초상 + HP 바 + 탄창 세그먼트, 숫자는 호버·변동시에만 */
#infHud .ihLeft{width:256px;padding:10px 12px;font-size:11px;line-height:1.4}
#infHud .ihvMain{display:flex;align-items:center;gap:9px}
#infHud .ihvPort{position:relative;flex:0 0 auto;width:48px;height:48px;display:grid;place-items:center;border-radius:12px;background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.13);overflow:hidden}
#infHud .ihvPort img{width:42px;height:42px;object-fit:contain}
#infHud .ihvRole{position:absolute;left:0;right:0;bottom:0;padding:1px 0 2px;background:rgba(6,3,12,.74);color:#cbbbdc;font:900 8px/1 inherit;text-align:center;letter-spacing:.02em}
#infHud .ihvCol{flex:1;min-width:0;display:flex;flex-direction:column;gap:5px}
#infHud .ihvBar{position:relative;height:9px;border-radius:999px;background:#2b2036;overflow:hidden}
#infHud .ihvBar i{display:block;height:100%;width:100%;border-radius:999px;background:linear-gradient(90deg,#7febd0,#a6ffe6);transition:width .12s linear}
#infHud .ihLeft.low .ihvHp i{background:linear-gradient(90deg,#ff5f7e,#ff9db0)}
#infHud .ihvSegs{display:flex;gap:2px;height:8px}
#infHud .ihvSegs i{flex:1 1 0;min-width:2px;border-radius:2px;background:#ffd36e;transition:background .1s,opacity .1s}
#infHud .ihvSegs i.off{background:#3a2c46}
#infHud .ihvRel{height:4px;opacity:0;transition:opacity .18s}
#infHud .ihvRel.reloading{opacity:1}
#infHud .ihvRel i{background:linear-gradient(90deg,#7febd0,#ffd36e);transition:width .06s linear}
#infHud .ihvDetail{display:flex;flex-wrap:wrap;gap:0 8px;max-height:0;margin-top:0;overflow:hidden;opacity:0;color:#a596b8;font:800 10px/1.5 ui-monospace,monospace;transition:max-height .18s,opacity .18s,margin-top .18s}
#infHud .ihLeft:hover .ihvDetail,#infHud .ihLeft.reveal .ihvDetail{max-height:44px;margin-top:6px;opacity:1}
#infHud #infHpText{color:#e6dcf0}
#infHud #infAmmoText{color:#ffd36e}
/* v7.8.0 UI 그레이박스 5번 — 우하단 스킬 슬롯: 키 내장 · 쿨다운 방사형 스윕 */
#infHud .ihSkills{position:absolute;right:calc(18px + env(safe-area-inset-right));bottom:calc(30px + env(safe-area-inset-bottom));display:flex;gap:7px;align-items:flex-end}
#infHud .ihSlot{position:relative;width:56px;height:60px;display:flex;flex-direction:column;align-items:center;gap:3px;padding:7px 4px 5px;border-radius:12px;background:rgba(12,8,22,.86);border:1px solid rgba(255,211,110,.3);overflow:hidden;transition:border-color .18s,opacity .18s}
#infHud .ihSlot b{position:relative;z-index:1;color:#ffd36e;font:900 11px/1 ui-monospace,monospace}
#infHud .ihSlot small{position:relative;z-index:1;color:#cbbbdc;font:800 9px/1.2 inherit;text-align:center;word-break:keep-all}
#infHud .ihSlotSweep{position:absolute;inset:0;background:conic-gradient(from -90deg,rgba(4,2,10,.74) calc(var(--cd,0) * 1%),transparent 0)}
#infHud .ihSlot.cool{border-color:rgba(255,255,255,.13)}
#infHud .ihSlot.cool b{color:#8a7a99}
#infHud .ihSlot.off{opacity:.4}
@media(max-width:900px){#infHud .ihSkills{gap:5px}#infHud .ihSlot{width:48px;height:54px}#infHud .ihLeft{width:224px}}
"""
rep(CSS_ANCHOR, CSS_ANCHOR + "\n" + CSS_NEW.rstrip("\n"), "css")

# 등장 애니메이션 — ihKeys 옆에 ihSkills 추가
rep("#infHud.on .ihKeys{animation:tcFxIn .28s var(--tcFxEase) both;animation-delay:.09s}",
    "#infHud.on .ihKeys{animation:tcFxIn .28s var(--tcFxEase) both;animation-delay:.09s}\n#infHud.on .ihSkills{animation:tcFxIn .28s var(--tcFxEase) both;animation-delay:.09s}",
    "css-anim")

# ─────────────────────────────── 2) 마크업
OLD_VITALS = """ <div class="ihLeft"><strong id="infHpText">HP 100 / 100</strong><br><span id="infBuildText">드릴 ×1.00 · 총 ×1.00</span><div id="infAmmoRow"><span id="infAmmoText">탄창 12 / 12</span><span class="infReloadGauge"><i id="infReloadFill"></i></span></div></div>
 <div class="ihKeys" id="infKeyGuide"><b>LMB</b> 드릴　<b>RMB</b> 총<br><b>WASD</b> 이동　<b>SPACE</b> 대시</div>"""

NEW_VITALS = """ <!-- v7.8.0 UI 그레이박스 4번 — 좌하단 vitals 아이콘화 (숫자는 호버·피격·재장전·잔탄 위험시에만) -->
 <div class="ihLeft" id="infVitals">
  <div class="ihvMain">
   <span class="ihvPort"><img id="infVitalsBadge" src="assets/ui/role-badges/role-badge-driller.png" alt=""><span class="ihvRole" id="infVitalsRole">드릴러</span></span>
   <span class="ihvCol">
    <span class="ihvBar ihvHp"><i id="infHpFill"></i></span>
    <span class="ihvSegs" id="infAmmoSegs"></span>
    <span class="ihvBar ihvRel" id="infAmmoRow"><i id="infReloadFill"></i></span>
   </span>
  </div>
  <div class="ihvDetail"><b id="infHpText">HP 100 / 100</b><b id="infAmmoText">탄창 12 / 12</b><span id="infBuildText">드릴 ×1.00 · 총 ×1.00</span></div>
 </div>
 <div class="ihKeys" id="infKeyGuide"><b>LMB</b> 드릴　<b>RMB</b> 총<br><b>WASD</b> 이동　<b>SPACE</b> 대시</div>
 <!-- v7.8.0 UI 그레이박스 5번 — 우하단 스킬 슬롯 (역할별로 런타임 생성) -->
 <div class="ihSkills" id="infSkills" aria-hidden="true"></div>"""
rep(OLD_VITALS, NEW_VITALS, "markup")

# ─────────────────────────────── 3) 로직
rep(" const keys=infEl('infKeyGuide');if(keys)keys.innerHTML=role.keys;",
    " const keys=infEl('infKeyGuide');if(keys)keys.innerHTML=role.keys;\n infBuildSkillSlots(role);   /* v7.8.0 UI 5번 — 역할 스킬 슬롯 구성 */",
    "startrun")

FNS = """/* v7.8.0 UI 그레이박스 4번 — vitals 아이콘화: 바·세그먼트로 읽고, 숫자는 필요할 때만 편다 */
function infPaintVitals(role){
 const box=infEl('infVitals');if(!box)return;
 const badge=infEl('infVitalsBadge');
 if(badge&&badge.dataset.role!==role.id){badge.dataset.role=role.id;badge.src='assets/ui/role-badges/role-badge-'+role.id+'.png';}
 const nm=infEl('infVitalsRole');if(nm&&nm.textContent!==role.name)nm.textContent=role.name;
 const hp=Math.max(0,G.php),hpMax=Math.max(1,G.phpMax),hpP=Math.min(1,hp/hpMax);
 infEl('infHpFill').style.width=(hpP*100).toFixed(1)+'%';box.classList.toggle('low',hpP<.35);
 const segs=infEl('infAmmoSegs'),mag=Math.max(1,INF.magSize|0),ammo=Math.max(0,Math.min(mag,INF.ammo|0));
 if(segs.childElementCount!==mag)segs.innerHTML='<i></i>'.repeat(mag);   /* 탄창 크기가 바뀔 때만 재생성 */
 for(let i=0;i<mag;i++)segs.children[i].classList.toggle('off',i>=ammo);
 const reloading=INF.reloadLeft>0,reloadP=reloading?1-INF.reloadLeft/Math.max(.01,INF.reloadDuration):0;
 infEl('infAmmoRow').classList.toggle('reloading',reloading);
 infEl('infReloadFill').style.width=(reloadP*100).toFixed(1)+'%';
 const now=(typeof performance!=='undefined'?performance.now():0);
 if(INF.__vitalHp===undefined)INF.__vitalHp=hp;
 if(hp<INF.__vitalHp-.01)INF.__vitalRevealUntil=now+1700;   /* 피격 직후 1.7초만 숫자 노출 */
 INF.__vitalHp=hp;
 box.classList.toggle('reveal',reloading||hpP<.35||ammo<=Math.max(1,Math.round(mag*.25))||now<(INF.__vitalRevealUntil||0));
 infEl('infHpText').textContent='HP '+Math.ceil(hp)+' / '+Math.ceil(hpMax);
 infEl('infAmmoText').textContent=reloading?'재장전 '+INF.reloadLeft.toFixed(1)+'초':'탄창 '+ammo+' / '+mag;
}
/* v7.8.0 UI 그레이박스 5번 — 우하단 스킬 슬롯: 키를 슬롯 안에 넣고 쿨다운은 방사형 스윕 */
function infBuildSkillSlots(role){
 const box=infEl('infSkills');if(!box)return;
 const slots=[{k:'rmb',key:'RMB',n:role.combat||'사격'},{k:'q',key:'Q',n:role.qLabel||'역할 스킬'}];
 if(role.eLabel)slots.push({k:'e',key:'E',n:role.eLabel});
 slots.push({k:'dash',key:'SPACE',n:'대시'});
 box.innerHTML=slots.map(s=>'<span class="ihSlot" data-slot="'+s.k+'"><i class="ihSlotSweep"></i><b>'+s.key+'</b><small>'+s.n+'</small></span>').join('');
 box.dataset.role=role.id;
}
function infPaintSkills(role){
 const box=infEl('infSkills');if(!box)return;
 if(box.dataset.role!==role.id)infBuildSkillSlots(role);
 const set=(k,frac,off)=>{const el=box.querySelector('[data-slot="'+k+'"]');if(!el)return;
  const p=Math.max(0,Math.min(100,frac*100));
  el.style.setProperty('--cd',p.toFixed(1));el.classList.toggle('cool',p>.5);el.classList.toggle('off',!!off);};
 set('rmb',INF.reloadLeft>0?INF.reloadLeft/Math.max(.01,INF.reloadDuration):0,false);
 set('q',CREW.qCd>0?CREW.qCd/Math.max(.01,role.qCd||1):0,false);
 if(role.eLabel){
  if(role.id==='gunner')set('e',0,!(INF.breakerCharges&&INF.breakerCharges.length));   /* 거너 E는 부착된 파쇄탄이 있을 때만 */
  else set('e',CREW.eCd>0?CREW.eCd/Math.max(.01,role.eCd||1):0,false);
 }
 const dashMax=(typeof TE!=='undefined'&&TE.dashCd)||1;
 set('dash',G.dash&&G.dash.cd>0?G.dash.cd/Math.max(.01,dashMax):0,false);
}
function infPaint(){"""
rep("function infPaint(){", FNS, "functions")

OLD_PAINT = """ infEl('infHpText').textContent=role.name+' · HP '+Math.ceil(G.php)+' / '+Math.ceil(G.phpMax);infEl('infBuildText').textContent=terrainStatus+' · '+INF.gunMode+' ×'+(INF.gunMul*role.gunMul).toFixed(2)+' · '+INF.shots+'발';
 const reloading=INF.reloadLeft>0,ammoRow=infEl('infAmmoRow'),reloadP=reloading?1-INF.reloadLeft/Math.max(.01,INF.reloadDuration):INF.ammo/Math.max(1,INF.magSize);ammoRow.classList.toggle('reloading',reloading);infEl('infAmmoText').textContent=reloading?'재장전 '+INF.reloadLeft.toFixed(1)+'초':'탄창 '+INF.ammo+' / '+INF.magSize;infEl('infReloadFill').style.width=Math.max(0,Math.min(100,reloadP*100))+'%';"""
NEW_PAINT = """ infEl('infBuildText').textContent=terrainStatus+' · '+INF.gunMode+' ×'+(INF.gunMul*role.gunMul).toFixed(2)+' · '+INF.shots+'발';
 infPaintVitals(role);infPaintSkills(role);"""
rep(OLD_PAINT, NEW_PAINT, "paint")

# ─────────────────────────────── 4) UI 랩 모듈 등록
rep("   {id:'ihKeys',sel:'.ihKeys',name:'키 가이드 (Tab)'}]},",
    "   {id:'ihKeys',sel:'.ihKeys',name:'키 가이드 (Tab)'},\n   {id:'ihSkills',sel:'.ihSkills',name:'스킬 슬롯 (우하단)'}]},",
    "uilab")

if s == orig:
    print("no change"); sys.exit(1)
io.open(PATH, "w", encoding="utf-8", newline="").write(s)
print("written")
