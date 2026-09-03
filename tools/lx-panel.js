/* ══════════ LX 테스트 패널 — F10 (또는 ?lx) ══════════
   LX / LIT_TUNE / TE 의 조명 수치를 실시간으로 조절하고 JSON 으로 내보낸다.
   붙여넣기 → 적용으로 JSON 을 되돌려 넣을 수 있다. localStorage(tc_lx_v791)에 자동 저장. */
(function(){
 if(typeof LX==='undefined'||typeof LX_DEFAULT==='undefined')return;
 const LS_KEY='tc_lx_v791b';   /* 기본값이 바뀌면 키를 올려 예전 저장값이 덮어쓰지 않게 한다 */
 const TE_KEYS=['ambient','flashRange','halfAngle','heightRatio','nStrength','fogDensity','lightSteps','softMask','flashlight','breathe'];
 const $=(t,cls,txt)=>{const e=document.createElement(t);if(cls)e.className=cls;if(txt!=null)e.textContent=txt;return e;};
 const css=`
#lxPanel{position:fixed;top:8px;right:8px;bottom:8px;width:330px;z-index:9000;overflow:auto;background:rgba(14,10,22,.94);color:#E8E0F0;
 font:11px/1.35 Pretendard,'Malgun Gothic',Consolas,monospace;border:1px solid #4A3A6A;border-radius:8px;padding:8px 10px 14px;box-shadow:0 8px 30px rgba(0,0,0,.6);scrollbar-width:thin}
#lxPanel h3{margin:0 0 6px;font-size:13px;color:#FFD696;display:flex;justify-content:space-between;align-items:center}
#lxPanel h3 small{font-weight:400;color:#9A8AB8;font-size:10px}
#lxPanel .lxStats{color:#8FCBFF;font-size:10px;margin-bottom:6px;white-space:pre}
#lxPanel .lxBar{display:flex;gap:4px;flex-wrap:wrap;margin:4px 0 8px}
#lxPanel button,#lxPanel select{background:#2A1F44;color:#F0E8FF;border:1px solid #5A4A80;border-radius:4px;padding:3px 7px;font:inherit;cursor:pointer}
#lxPanel button:hover{background:#3A2C5C}
#lxPanel button.lxOn{background:#5A3A9A;border-color:#9A7ADA}
#lxPanel details{border-top:1px solid #2E2446;padding:4px 0 6px}
#lxPanel summary{cursor:pointer;color:#C8B8FF;font-weight:700;padding:3px 0;user-select:none}
#lxPanel .lxRow{display:grid;grid-template-columns:96px 1fr 44px;gap:6px;align-items:center;margin:2px 0}
#lxPanel .lxRow label{color:#B8ACD0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#lxPanel .lxRow input[type=range]{width:100%;margin:0;accent-color:#B48CFF}
#lxPanel .lxRow input[type=color]{width:100%;height:20px;padding:0;border:1px solid #5A4A80;background:none;border-radius:3px}
#lxPanel .lxRow input[type=checkbox]{justify-self:start;accent-color:#B48CFF}
#lxPanel .lxRow select{width:100%;padding:2px 4px}
#lxPanel .lxRow output{text-align:right;color:#FFD696;font-variant-numeric:tabular-nums}
#lxPanel .lxSub{color:#8FCBFF;margin:6px 0 2px;font-size:10px;letter-spacing:.04em}
#lxPanel textarea{width:100%;height:150px;box-sizing:border-box;background:#0C0814;color:#D8F0C8;border:1px solid #3A2E5A;border-radius:4px;
 font:10px/1.3 Consolas,monospace;padding:6px;resize:vertical;white-space:pre;overflow:auto}
#lxPanel .lxHint{color:#7A6E98;font-size:10px;margin-top:4px}
#lxPanel .lxMsg{color:#7CE8C8;font-size:10px;min-height:12px;margin-top:3px}`;
 const style=document.createElement('style');style.textContent=css;document.head.appendChild(style);

 const panel=$('div');panel.id='lxPanel';panel.hidden=true;document.body.appendChild(panel);
 /* 패널 안 키 입력은 게임으로 흘려보내지 않는다 */
 for(const ev of ['keydown','keyup','keypress'])panel.addEventListener(ev,e=>{if(e.key!=='F10'&&e.key!=='Escape')e.stopPropagation();});
 for(const ev of ['pointerdown','pointerup','mousedown','mouseup','click','wheel','contextmenu'])panel.addEventListener(ev,e=>e.stopPropagation());

 const openState={};
 let msgEl=null,jsonEl=null,statsEl=null,dirtyT=0;

 /* ── 데이터 ── */
 function pick(o,keys){const r={};for(const k of keys)if(o[k]!==undefined)r[k]=o[k];return r;}
 function exportObj(){return {version:'7.9.1-lx',lx:JSON.parse(JSON.stringify(LX)),lit:JSON.parse(JSON.stringify(LIT_TUNE)),te:pick(TE,TE_KEYS)};}
 function applyObj(o){
  if(!o||typeof o!=='object')throw new Error('object 아님');
  if(o.lx)lxDeepMerge(LX,o.lx);
  if(o.lit)for(const k in o.lit)if(k in LIT_TUNE&&Number.isFinite(+o.lit[k]))LIT_TUNE[k]=+o.lit[k];
  if(o.te)for(const k of TE_KEYS)if(o.te[k]!==undefined)TE[k]=o.te[k];
  syncLegacyPanel();
 }
 function syncLegacyPanel(){try{if(typeof syncDemoUI==='function')syncDemoUI();}catch(e){}}
 function save(){try{localStorage.setItem(LS_KEY,JSON.stringify(exportObj()));}catch(e){}}
 function load(){try{const s=localStorage.getItem(LS_KEY);if(s){applyObj(JSON.parse(s));return true;}}catch(e){}return false;}
 function resetAll(){
  for(const k in LX)delete LX[k];lxDeepMerge(LX,LX_DEFAULT);
  if(window.TC_LIT_TUNE_DEFAULT)Object.assign(LIT_TUNE,window.TC_LIT_TUNE_DEFAULT);
  if(window.TC_TE_LIGHT_DEFAULT)Object.assign(TE,window.TC_TE_LIGHT_DEFAULT);
  try{localStorage.removeItem(LS_KEY);}catch(e){}
  syncLegacyPanel();
 }
 /* 최초 기본값 스냅샷 — 리셋용 */
 if(!window.TC_LIT_TUNE_DEFAULT)window.TC_LIT_TUNE_DEFAULT=JSON.parse(JSON.stringify(LIT_TUNE));
 if(!window.TC_TE_LIGHT_DEFAULT)window.TC_TE_LIGHT_DEFAULT=pick(TE,TE_KEYS);

 function changed(){save();scheduleJson();}
 function scheduleJson(){if(dirtyT)return;dirtyT=setTimeout(()=>{dirtyT=0;if(jsonEl&&document.activeElement!==jsonEl)jsonEl.value=JSON.stringify(exportObj(),null,1);},80);}
 function msg(t){if(msgEl){msgEl.textContent=t;clearTimeout(msgEl._t);msgEl._t=setTimeout(()=>{msgEl.textContent='';},2600);}}

 /* ── 컨트롤 생성 ── */
 function row(label,ctrl,out){const r=$('div','lxRow');const l=$('label',null,label);l.title=label;r.appendChild(l);r.appendChild(ctrl);r.appendChild(out||$('span'));return r;}
 function fRange(obj,key,label,min,max,step){
  const inp=$('input');inp.type='range';inp.min=min;inp.max=max;inp.step=step==null?((max-min)>20?1:.01):step;
  const out=$('output');const fmt=v=>(+v).toFixed(inp.step>=1?0:(inp.step>=.1?1:2));
  inp.value=obj[key]==null?min:obj[key];out.value=fmt(inp.value);
  inp.addEventListener('input',()=>{obj[key]=+inp.value;out.value=fmt(inp.value);changed();});
  return row(label,inp,out);}
 function fColor(obj,key,label){
  const inp=$('input');inp.type='color';inp.value=/^#[0-9a-fA-F]{6}$/.test(obj[key]||'')?obj[key].toUpperCase():'#FFFFFF';
  const out=$('output');out.value=inp.value.slice(1);
  inp.addEventListener('input',()=>{obj[key]=inp.value.toUpperCase();out.value=inp.value.slice(1);changed();});
  return row(label,inp,out);}
 function fBool(obj,key,label){
  const inp=$('input');inp.type='checkbox';inp.checked=!!obj[key];
  inp.addEventListener('change',()=>{obj[key]=inp.checked;changed();});
  return row(label,inp);}
 function fSelect(obj,key,label,opts){
  const sel=$('select');for(const o of opts){const op=$('option',null,o);op.value=o;sel.appendChild(op);}
  if(opts.indexOf(obj[key])<0){const op=$('option',null,obj[key]);op.value=obj[key];sel.appendChild(op);}
  sel.value=obj[key];
  sel.addEventListener('change',()=>{obj[key]=sel.value;changed();});
  return row(label,sel);}
 function section(title,build,openDefault){
  const d=$('details');const s=$('summary',null,title);d.appendChild(s);
  d.open=openState[title]==null?!!openDefault:openState[title];
  d.addEventListener('toggle',()=>{openState[title]=d.open;});
  build(d);panel.appendChild(d);return d;}
 const sub=(d,t)=>d.appendChild($('div','lxSub',t));

 const KIND_KO={hero:'히어로 램프',flash:'손전등',torch:'횃불·플레어',cold:'결정 등불',bio:'식생',boss:'보스',fx:'투사체 FX',exit:'출구'};
 const BLEND_KO=m=>m;

 function build(){
  panel.innerHTML='';
  const h=$('h3');h.appendChild($('span',null,'LX 조명 실험'));const sm=$('small',null,'F10 닫기 · v7.9.1-lighting-develop');h.appendChild(sm);panel.appendChild(h);
  statsEl=$('div','lxStats','');panel.appendChild(statsEl);

  const bar=$('div','lxBar');
  const onBtn=$('button',LX.on?'lxOn':'','LX '+(LX.on?'ON':'OFF'));onBtn.title='끄면 기존 안개 셰이더(구 조명)로 그려 A/B 비교';
  onBtn.addEventListener('click',()=>{LX.on=!LX.on;onBtn.className=LX.on?'lxOn':'';onBtn.textContent='LX '+(LX.on?'ON':'OFF');changed();});
  bar.appendChild(onBtn);
  const maskBtn=$('button',LX.showMask?'lxOn':'','마스크 보기');
  maskBtn.addEventListener('click',()=>{LX.showMask=!LX.showMask;maskBtn.className=LX.showMask?'lxOn':'';changed();});
  bar.appendChild(maskBtn);
  const alBtn=$('button',(typeof OPT!=='undefined'&&OPT.alphaLight)?'lxOn':'','재질 조명');alBtn.title='스프라이트·벽 알파 라이팅(LIT) 토글';
  alBtn.addEventListener('click',()=>{if(typeof OPT==='undefined')return;OPT.alphaLight=!OPT.alphaLight;alBtn.className=OPT.alphaLight?'lxOn':'';});
  bar.appendChild(alBtn);
  const pre=$('select');{const op=$('option',null,'프리셋…');op.value='';pre.appendChild(op);}
  for(const k in LX_PRESETS){const op=$('option',null,k);op.value=k;pre.appendChild(op);}
  pre.addEventListener('change',()=>{const k=pre.value;if(!k)return;for(const kk in LX)delete LX[kk];lxDeepMerge(LX,LX_DEFAULT);lxDeepMerge(LX,LX_PRESETS[k]);changed();build();msg('프리셋 적용: '+k);});
  bar.appendChild(pre);
  const rs=$('button',null,'리셋');rs.addEventListener('click',()=>{resetAll();build();msg('기본값으로 리셋');});bar.appendChild(rs);
  panel.appendChild(bar);

  section('광원 색 · 세기',d=>{for(const k of LX_LIGHT_KINDS){const L=LX.lights[k]||(LX.lights[k]={rgb:'#FFFFFF',mul:1});
   d.appendChild(fColor(L,'rgb',KIND_KO[k]||k));d.appendChild(fRange(L,'mul','  └ 세기 ×',0,4,.05));}
   d.appendChild($('div','lxHint','색 피커의 명도도 세기에 반영된다(어두운 색 = 약한 빛). 색을 진하게 고를수록 티가 난다.'));},true);

  section('① 컬러 라이트맵',d=>{const M=LX.lightmap;
   d.appendChild(fBool(M,'on','켜기'));d.appendChild(fSelect(M,'mode','블렌드',LX_MODES));d.appendChild(fRange(M,'opacity','불투명도',0,1));
   d.appendChild(fColor(M,'dark','어둠 색'));d.appendChild(fRange(M,'bright','빛 밝기',0,3));d.appendChild(fRange(M,'saturation','빛 채도',0,3));
   if(M.litColor==null)M.litColor=1;
   d.appendChild(fRange(M,'litColor','광원색 반영',0,1));d.appendChild(fRange(M,'litClear','밝은 곳 투명화',0,1));
   d.appendChild($('div','lxHint','multiply: 씬 × (어둠색→광원색). 광원색 반영 0 + 밝은 곳 투명화 1 + normal = 기존 안개.'));},true);

  section('② 대비 (Overlay / Soft Light)',d=>{const C=LX.contrast;
   d.appendChild(fBool(C,'on','켜기'));d.appendChild(fSelect(C,'mode','블렌드',LX_MODES));d.appendChild(fRange(C,'opacity','불투명도',0,1));
   d.appendChild(fRange(C,'darkLevel','어둠 회색',0,1));d.appendChild(fRange(C,'litLevel','빛 회색',0,1));d.appendChild(fRange(C,'colorize','광원색 섞기',0,1));
   d.appendChild($('div','lxHint','회색 0.5 가 중립. 어둠<0.5 로 눌리고, 빛>0.5 로 대비·채도가 오른다.'));});

  section('③ 핫코어 (Color Dodge / Screen)',d=>{const K=LX.core;
   d.appendChild(fBool(K,'on','켜기'));d.appendChild(fSelect(K,'mode','블렌드',LX_MODES));d.appendChild(fRange(K,'opacity','불투명도',0,1));
   d.appendChild(fRange(K,'threshold','시작 세기',0,1));d.appendChild(fRange(K,'softness','부드러움',0,.6));d.appendChild(fRange(K,'strength','강도',0,4));
   d.appendChild(fRange(K,'tintMix','광원색 비율',0,1));
   d.appendChild($('div','lxHint','광원 세기가 시작값을 넘는 중심부만 태운다. 시작값을 내리면 넓게 번진다.'));});

  section('④ 구역 앰비언스 (Hue / Color)',d=>{const Z=LX.zone;
   d.appendChild(fBool(Z,'on','켜기'));d.appendChild(fSelect(Z,'mode','블렌드',LX_MODES));d.appendChild(fRange(Z,'opacity','불투명도',0,1));
   d.appendChild(fRange(Z,'litFade','밝은 곳 감쇠',0,1));d.appendChild(fRange(Z,'blendSec','전환 시간(s)',0,5,.1));
   d.appendChild($('div','lxHint','hue/color 는 밝기를 유지해 어두운 던전에서는 은은하다. 확실히 보려면 screen·soft-light·multiply 로 바꿔 본다.'));
   sub(d,'지층 밴드별 색 (시점 캐릭터 발밑 밴드)');
   if(!Array.isArray(Z.colors))Z.colors=LX_DEFAULT.zone.colors.slice();
   for(let i=0;i<Z.colors.length;i++)d.appendChild(fColor(Z.colors,i,'밴드 '+i));});

  section('⑤ 스프라이트 명암',d=>{const S=LX.sprite;
   d.appendChild(fSelect(S,'shadeMode','그늘 블렌드',LX_CANVAS_MODES));d.appendChild(fColor(S,'shadeRgb','그늘 색'));d.appendChild(fRange(S,'shade','그늘 ×',0,2));
   d.appendChild(fSelect(S,'liteMode','빛면 블렌드',LX_CANVAS_MODES));d.appendChild(fRange(S,'lite','빛면 ×',0,2));
   d.appendChild(fSelect(S,'rimMode','림 블렌드',LX_CANVAS_MODES));d.appendChild(fRange(S,'rim','림 ×',0,2));
   d.appendChild($('div','lxHint','기존 방식 = 그늘 source-over(#0A0616) · 빛면 source-over · 림 lighter'));});

  section('벽 그림자 · 음영 · 림',d=>{const W=LX.wall;
   d.appendChild(fSelect(W,'shadowMode','바닥 그림자',LX_CANVAS_MODES));d.appendChild(fColor(W,'shadowRgb','  └ 색'));d.appendChild(fRange(W,'shadow','  └ ×',0,2));
   d.appendChild(fSelect(W,'shadeMode','벽면 음영',LX_CANVAS_MODES));d.appendChild(fColor(W,'shadeRgb','  └ 색'));d.appendChild(fRange(W,'shade','  └ ×',0,2));
   d.appendChild(fSelect(W,'rimMode','모서리 림',LX_CANVAS_MODES));d.appendChild(fRange(W,'rim','  └ ×',0,2));});

  section('재질 조명 수치 (LIT_TUNE)',d=>{const T=LIT_TUNE;
   sub(d,'스프라이트');
   d.appendChild(fRange(T,'spRim','림 세기',0,1));d.appendChild(fRange(T,'spRimPx','림 두께 px',0,10,.1));d.appendChild(fRange(T,'spLite','빛면',0,1.5));
   d.appendChild(fRange(T,'spShade','그늘',0,2));d.appendChild(fRange(T,'spAmb','앰비언트 그늘',0,2));
   sub(d,'벽');
   d.appendChild(fRange(T,'wRim','림',0,1));d.appendChild(fRange(T,'wRimE','림 폭',0,.2,.002));d.appendChild(fRange(T,'wShade','음영',0,2.5));d.appendChild(fRange(T,'wShadeE','음영 폭',0,.2,.002));
   d.appendChild(fRange(T,'wShadow','그림자',0,2.5));d.appendChild(fRange(T,'wShadowE','그림자 길이',0,.4,.002));d.appendChild(fRange(T,'wSteps','그림자 단계',1,6,1));
   d.appendChild(fRange(T,'wRes','해상도',.2,1,.05));d.appendChild(fRange(T,'wLights','광원 수',1,6,1));});

  section('기존 FoW 수치 (TE)',d=>{
   d.appendChild(fRange(TE,'ambient','앰비언트 반경',60,400,1));d.appendChild(fRange(TE,'flashRange','손전등 거리',80,600,1));d.appendChild(fRange(TE,'halfAngle','손전등 반각°',10,60,1));
   d.appendChild(fRange(TE,'heightRatio','광원 높이비',.05,2,.01));d.appendChild(fRange(TE,'nStrength','노멀 강도',0,.5,.005));
   d.appendChild(fRange(TE,'fogDensity','어둠 농도(구)',0,1));d.appendChild(fRange(TE,'lightSteps','밝기 단계',2,16,1));
   d.appendChild(fBool(TE,'softMask','소프트 마스크'));d.appendChild(fBool(TE,'flashlight','손전등'));d.appendChild(fBool(TE,'breathe','호흡 펄스'));});

  section('JSON 내보내기 / 가져오기',d=>{
   const b=$('div','lxBar');
   const cp=$('button',null,'JSON 복사');cp.addEventListener('click',async()=>{jsonEl.value=JSON.stringify(exportObj(),null,1);
    try{await navigator.clipboard.writeText(jsonEl.value);msg('클립보드에 복사됨');}catch(e){jsonEl.focus();jsonEl.select();msg('Ctrl+C 로 복사하세요');}});
   const ap=$('button',null,'붙여넣은 JSON 적용');ap.addEventListener('click',()=>{try{applyObj(JSON.parse(jsonEl.value));save();build();msg('JSON 적용됨');}catch(e){msg('JSON 오류: '+e.message);}});
   const dl=$('button',null,'파일로 저장');dl.addEventListener('click',()=>{const a=document.createElement('a');a.href='data:application/json;charset=utf-8,'+encodeURIComponent(JSON.stringify(exportObj(),null,1));
    a.download='tc-lighting-lx-'+new Date().toISOString().slice(0,19).replace(/[:T]/g,'-')+'.json';document.body.appendChild(a);a.click();a.remove();msg('다운로드');});
   b.appendChild(cp);b.appendChild(ap);b.appendChild(dl);d.appendChild(b);
   jsonEl=$('textarea');jsonEl.spellcheck=false;jsonEl.value=JSON.stringify(exportObj(),null,1);d.appendChild(jsonEl);
   msgEl=$('div','lxMsg');d.appendChild(msgEl);
   d.appendChild($('div','lxHint','슬라이더·색·드롭다운은 즉시 화면에 반영되고 JSON 이 자동 갱신된다. 텍스트를 직접 고친 경우에만 "붙여넣은 JSON 적용"을 누른다. 이 JSON 을 그대로 전달하면 본편에 이식한다.'));},true);
 }

 function tick(){
  if(!panel.hidden&&statsEl){const s=window.TC_LIGHTING_STATS||{};
   statsEl.textContent='FoW '+(s.frameMsAvg||0).toFixed(2)+' ms  stamps '+(s.stamps||0)+'  fx '+(s.fxUsed||0)+'/'+(s.fxCandidates||0)+'  lamps '+(s.lampUsed||0)+'/'+(s.lampCandidates||0)
    +'\n'+(LX.on?'LX 합성 · ':'구 안개 셰이더 · ')+(typeof OPT!=='undefined'&&OPT.alphaLight?'재질 조명 ON':'재질 조명 OFF');}
  requestAnimationFrame(tick);}
 requestAnimationFrame(tick);

 function toggle(force){const show=force==null?panel.hidden:!!force;if(show){build();panel.hidden=false;}else panel.hidden=true;}
 window.addEventListener('keydown',e=>{if(e.key==='F10'&&!e.shiftKey){e.preventDefault();e.stopImmediatePropagation();toggle();}},true);
 window.TC_LX_PANEL={toggle,exportObj,applyObj,reset:resetAll};

 const loaded=load();
 if(/[?&]lx(=|&|$)/.test(location.search))toggle(true);
 if(loaded)console.info('[LX] 저장된 조명 수치 복원 (localStorage '+LS_KEY+')');
 console.info('[LX] F10 — 조명 실험 패널');
})();
