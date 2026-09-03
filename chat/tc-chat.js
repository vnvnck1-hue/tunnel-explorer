/* ══════════════════════════════════════════════════════════════════
   TEAM_CHAT_V1 — 크루 채팅 (LOL 식 좌하단 채팅창 + 머리 위 말풍선)
   ──────────────────────────────────────────────────────────────────
   조작   Enter = 채팅창 열기 · Enter = 전송(빈 칸이면 닫기) · Esc = 취소(입력 내용은 보관)
   표현   좌하단 로그 — 평소 최대 3줄(10초 뒤 서서히 사라짐) · 입력 중엔 8줄로 확장
          말풍선 — 보낸 캐릭터 머리 위(UI 캔버스, 안개 위) 5초 뒤 사라짐
   코옵   COOP.ws 로 {t:'chat', text} 전송 — 서버 RELAY_TYPES 에 'chat' 필요 (from/fromName 은 서버가 붙인다)
   훅     paintUI 를 감싼다(그리기). 키 입력은 window 캡처 단계에서 가로채 본편 단축키(WASD·E·Q·R·G…)를 막는다.
          본편 함수 본문은 건드리지 않는다. 핑 모듈(inputOk)은 TCCHAT.open 을 보고 스스로 비켜 준다.
   ══════════════════════════════════════════════════════════════════ */
(function () {
  'use strict';
  if (window.TCCHAT) return;

  const CFG = {
    maxLen: 80,           /* 한 메시지 최대 글자 */
    sendGapMs: 250,       /* 연속 전송 최소 간격 */
    bubbleSec: 5, bubbleFade: 0.5, bubblePop: 0.14, bubbleMaxW: 210, bubbleLines: 3, bubbleLift: 38,
    idleLines: 3, openLines: 8, idleFadeSec: 10, histMax: 80,
    /* AI 크루 멘트 — 'always' = AI 크루가 있으면 항상 · 'observer' = 관전 중에만 · 'off' */
    aiChat: 'always', aiGapMin: 15, aiGapMax: 30, aiGapUrgent: 6, aiPerLine: 2, aiMemberCd: 20, aiIdleSec: 20, aiIdleCd: 60,
  };
  const SEAT_COL = { p1: '#FFD36E', p2: '#7FEBD0', p3: '#FF8D72', p4: '#C7A0FF' };
  const FONT = 'Pretendard,"Malgun Gothic",sans-serif';

  const P = {
    ver: 1, open: false, debug: false,
    log: [],                 /* {seat,name,text,t,local} */
    bubbles: new Map(),      /* seat -> {text,t,name} */
    draft: '', lastSend: 0, _ws: null, el: null, input: null, logEl: null,
    trace: [],               /* 최근 키·조합 이벤트 (진단용) — TCCHAT.dump() */
    cfg: CFG,
  };
  window.TCCHAT = P;

  const now = () => performance.now() / 1000;
  const $ = (id) => document.getElementById(id);
  const ready = () => (typeof G !== 'undefined') && (typeof CREW !== 'undefined') && (typeof SCENE !== 'undefined');
  const playing = () => ready() && SCENE === 'depths' && CREW.phase === 'play';
  const coopOn = () => (typeof COOP !== 'undefined') && COOP.active;
  const mySeat = () => (coopOn() && COOP.seat) || 'p1';
  const myName = () => (coopOn() && COOP.nick) || '나';
  const seatCol = (seat) => (typeof seat === 'string' && seat.startsWith('ai:')) ? '#C9C9D6' : (SEAT_COL[seat] || '#f6efff');
  const seen = (x, y) => ((typeof LOS !== 'undefined') && typeof LOS.seenAt === 'function' ? !!LOS.seenAt(x, y) : true);
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const clean = (s) => String(s == null ? '' : s).replace(/[\x00-\x1f\x7f]/g, ' ').replace(/\s+/g, ' ').trim();

  function modalOpen() {
    for (const id of ['infLevelModal', 'infSettlementModal']) { const el = $(id); if (el && el.classList.contains('on')) return true; }
    return ((typeof STARMAP !== 'undefined') && STARMAP.open) || ((typeof DLG !== 'undefined') && DLG.on);
  }
  function canOpen(e) {
    if (!playing() || modalOpen()) return false;
    if ((typeof FX !== 'undefined') && FX && FX.active) return false;
    const tg = e && e.target, tag = tg && tg.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || tag === 'BUTTON' || (tg && tg.isContentEditable)) return false;
    if ((typeof TCPING !== 'undefined') && TCPING.hold) return false;
    return true;
  }

  /* ══════════ DOM — 좌하단 로그 + 입력창 ══════════ */
  const CSS = `
#tcChat{position:absolute;left:18px;bottom:104px;width:min(340px,calc(100% - 28px));z-index:40;pointer-events:none;font-family:${FONT};display:none}
#tcChat.show{display:block}
#tcChat .tcChatLog{display:flex;flex-direction:column;gap:3px;align-items:flex-start}
#tcChat .tcChatLine{max-width:100%;font-size:12px;line-height:16px;color:#f6efff;padding:2px 8px;border-radius:6px;background:rgba(12,8,22,.62);word-break:break-all;opacity:1;transition:opacity .9s ease}
#tcChat .tcChatLine.old{opacity:0}
#tcChat .tcChatName{font-weight:900;margin-right:6px}
#tcChat .tcChatText{font-weight:600}
#tcChat.open .tcChatLog{width:100%;box-sizing:border-box;min-height:calc(8 * 19px + 12px);justify-content:flex-end;padding:6px 8px;gap:3px;border-radius:10px 10px 0 0;background:rgba(12,8,22,.82);border:1px solid rgba(255,211,110,.28);border-bottom:none}
#tcChat.open .tcChatLine{background:none;padding:1px 2px;opacity:1;transition:none}
#tcChat .tcChatIn{display:none;pointer-events:auto;border-radius:0 0 10px 10px;background:rgba(12,8,22,.88);border:1px solid rgba(255,211,110,.28);border-top:1px solid rgba(255,211,110,.14);padding:5px 8px;align-items:center;gap:6px}
#tcChat.open .tcChatIn{display:flex}
#tcChat .tcChatIn .tcChatTo{font-size:11px;font-weight:900;color:#ffd36e;white-space:nowrap}
#tcChat .tcChatIn input{flex:1;min-width:0;background:transparent;border:none;outline:none;color:#f6efff;font:600 13px ${FONT};padding:2px 0}
#tcChat .tcChatIn input::placeholder{color:rgba(246,239,255,.38)}
#tcChat .tcChatIn .tcChatCnt{font:700 10px ui-monospace,monospace;color:rgba(246,239,255,.45)}
#tcChat .tcChatEmpty{font-size:11px;color:rgba(246,239,255,.45);padding:1px 2px}
`;
  function trace(e) {
    const el = P.input;
    P.trace.push({ t: Math.round(now() * 1000) % 100000, type: e.type, key: e.key, code: e.code, kc: e.keyCode, comp: e.isComposing, data: e.data, it: e.inputType, def: e.defaultPrevented, val: el ? el.value : '', sel: el ? el.selectionStart : -1, act: document.activeElement === el ? 'in' : (document.activeElement && document.activeElement.tagName) });
    if (P.trace.length > 80) P.trace.shift();
  }
  function ensureDom() {
    if (P.el && P.el.isConnected) return true;
    const host = $('app') || document.body; if (!host) return false;
    if (!$('tcChatStyle')) { const st = document.createElement('style'); st.id = 'tcChatStyle'; st.textContent = CSS; document.head.appendChild(st); }
    const el = document.createElement('div'); el.id = 'tcChat'; el.setAttribute('aria-live', 'polite');
    el.innerHTML = '<div class="tcChatLog"></div><div class="tcChatIn"><span class="tcChatTo">[팀]</span><input type="text" autocomplete="off" spellcheck="false" placeholder="메시지 입력… (Enter 전송 · Esc 취소)"><span class="tcChatCnt"></span></div>';
    host.appendChild(el);
    P.el = el; P.logEl = el.querySelector('.tcChatLog'); P.input = el.querySelector('input');
    P.input.maxLength = CFG.maxLen;
    P.input.addEventListener('input', paintCount);
    for (const t of ['compositionstart', 'compositionupdate', 'compositionend', 'beforeinput', 'input', 'focus', 'blur']) P.input.addEventListener(t, (e) => trace(e));
    /* IME 가 Space 로 조합을 끝냈는데 공백이 안 들어온 경우 — keyup 이 오지 않는 IME 를 위한 2차 보정 */
    P.input.addEventListener('compositionend', () => { const at = P.spaceDown; if (!at) return; setTimeout(() => { if (P.open && P.spaceDown === at && now() - at < 1.5 && document.activeElement === P.input && !caretHasSpace()) { P.spaceDown = 0; insertSpace(); trace({ type: 'fix:compositionend-space' }); } }, 30); });
    P.input.addEventListener('blur', () => { if (P.open) setTimeout(() => { if (P.open && document.activeElement !== P.input) close(true); }, 0); });
    /* 채팅창 위 포인터 입력은 게임으로 새지 않는다 */
    for (const ev of ['pointerdown', 'pointerup', 'mousedown', 'mouseup', 'click', 'contextmenu', 'wheel']) el.addEventListener(ev, (e) => e.stopPropagation());
    renderLog();
    return true;
  }
  function paintCount() { const c = P.el && P.el.querySelector('.tcChatCnt'); if (c) c.textContent = P.input.value.length + '/' + CFG.maxLen; }
  function lineEl(m) {
    const d = document.createElement('div'); d.className = 'tcChatLine';
    const n = document.createElement('span'); n.className = 'tcChatName'; n.style.color = seatCol(m.seat); n.textContent = m.name;
    const t = document.createElement('span'); t.className = 'tcChatText'; t.textContent = m.text;
    d.appendChild(n); d.appendChild(t); d._t = m.t;
    return d;
  }
  function renderLog() {
    if (!P.logEl) return;
    const n = P.open ? CFG.openLines : CFG.idleLines;
    const items = P.log.slice(-n);
    P.logEl.textContent = '';
    if (P.open && !items.length) { const e = document.createElement('div'); e.className = 'tcChatEmpty'; e.textContent = '아직 대화가 없다. 크루에게 한마디 건네 보자.'; P.logEl.appendChild(e); }
    for (const m of items) P.logEl.appendChild(lineEl(m));
    tickFade();
  }
  function tickFade() {
    if (!P.logEl || P.open) return;
    const t = now();
    for (const d of P.logEl.children) d.classList.toggle('old', d._t != null && t - d._t > CFG.idleFadeSec);
  }

  /* ══════════ 열기 · 닫기 · 전송 ══════════ */
  function open() {
    if (P.open || !ensureDom()) return;
    P.open = true;
    if (typeof KEY !== 'undefined' && KEY && typeof KEY.clear === 'function') KEY.clear();   /* 눌려 있던 이동키를 푼다 */
    if (ready() && G.mouse) { G.mouse.down = false; G.mouse.drillDown = false; G.mouse.gunDown = false; }
    P.el.classList.add('show', 'open');
    P.input.value = P.draft; paintCount(); renderLog();
    P.input.focus(); try { P.input.setSelectionRange(P.input.value.length, P.input.value.length); } catch (e) {}
  }
  function close(keepDraft) {
    if (!P.open) return;
    P.draft = keepDraft ? P.input.value : '';
    P.open = false;
    P.el.classList.remove('open');
    if (document.activeElement === P.input) P.input.blur();
    renderLog();
  }
  function submit() {
    const text = clean(P.input.value).slice(0, CFG.maxLen);
    P.input.value = '';
    if (!text) { close(false); return; }
    const tms = Date.now();
    if (tms - P.lastSend < CFG.sendGapMs) { close(false); return; }
    P.lastSend = tms;
    post({ seat: mySeat(), name: myName(), text, local: true });
    wsSend({ t: 'chat', v: P.ver, text, at: tms });
    try { if ((typeof SFX !== 'undefined') && typeof SFX.ui === 'function') SFX.ui(); } catch (e) {}
    close(false);
  }
  function post(m) {
    const msg = { seat: m.seat, name: m.name, text: m.text, t: now(), local: !!m.local };
    P.log.push(msg); if (P.log.length > CFG.histMax) P.log.shift();
    P.bubbles.set(msg.seat, { text: msg.text, t: msg.t, name: msg.name });
    ensureDom(); renderLog();
    return msg;
  }

  /* ══════════ 코옵 ══════════ */
  function wsSend(obj) { try { if (coopOn() && COOP.ws && COOP.ws.readyState === 1) COOP.ws.send(JSON.stringify(obj)); } catch (e) {} }
  function watchSocket() {
    if (!(typeof COOP !== 'undefined') || !COOP.ws || COOP.ws === P._ws) return;
    P._ws = COOP.ws;
    COOP.ws.addEventListener('message', (ev) => { let m; try { m = JSON.parse(ev.data); } catch (e) { return; } if (m && m.t === 'chat') onRemote(m); });
  }
  function onRemote(m) {
    const text = clean(m.text).slice(0, CFG.maxLen); if (!text) return;
    const seat = typeof m.from === 'string' ? m.from : 'p?';
    if (seat === mySeat()) return;   /* 내 메시지의 에코는 서버가 보내지 않지만 안전하게 */
    const peer = coopOn() && COOP.peers ? COOP.peers.get(seat) : null;
    const name = clean(m.fromName || (peer && peer.name) || seat.toUpperCase()).slice(0, 16) || seat.toUpperCase();
    post({ seat, name, text, local: false });
  }

  /* ══════════ 키 입력 — window 캡처 단계에서 가로챈다 ══════════
     한글 IME 주의: 조합 중 키는 keydown 이 keyCode 229('Process') 로 오고, 조합을 끝내는 Enter/Space 는
     두 번째 keydown 없이 keyup 만 온다. 그래서 Enter 전송·Space 삽입은 keyup 에서 한 번 더 보증한다. */
  const isSpace = (e) => e.code === 'Space' || e.key === ' ' || e.key === 'Spacebar';
  const isEnter = (e) => e.code === 'Enter' || e.code === 'NumpadEnter' || e.key === 'Enter';
  function caretHasSpace() {
    const el = P.input, i = el.selectionStart == null ? el.value.length : el.selectionStart;
    return i > 0 && el.value[i - 1] === ' ';
  }
  function insertSpace() {
    const el = P.input;
    if (el.value.length >= CFG.maxLen) return;
    const i = el.selectionStart == null ? el.value.length : el.selectionStart, j = el.selectionEnd == null ? i : el.selectionEnd;
    try { el.setRangeText(' ', i, j, 'end'); } catch (e) { el.value = el.value.slice(0, i) + ' ' + el.value.slice(j); }
    paintCount();
  }
  addEventListener('keydown', (e) => {
    if (P.open) {
      trace(e);
      const composing = e.isComposing || e.keyCode === 229;
      if (e.key === 'Escape') { e.preventDefault(); close(true); }
      else if (isEnter(e)) { if (composing) P.enterPending = now(); else { e.preventDefault(); submit(); } }
      else if (isSpace(e)) { P.spaceDown = now(); }
      e.stopImmediatePropagation();   /* 본편 단축키(WASD·E·Q·R·X·Space·G·V·Tab…) 차단. 기본 동작(글자 입력)은 살린다 */
      return;
    }
    if (e.key === 'Enter' && !e.ctrlKey && !e.altKey && !e.metaKey && !e.repeat && canOpen(e)) {
      e.preventDefault(); e.stopImmediatePropagation(); open();
    }
  }, { capture: true });
  addEventListener('keyup', (e) => {
    if (!P.open) return;
    trace(e);
    e.stopImmediatePropagation();
    const t = now();
    /* 조합 중 Enter — IME 가 조합만 끝내고 두 번째 keydown 을 주지 않으면 여기서 전송한다 */
    if (isEnter(e) && P.enterPending && t - P.enterPending < 1.5) { P.enterPending = 0; e.preventDefault(); submit(); return; }
    /* Space — 눌렀는데 커서 앞에 공백이 생기지 않았으면(IME·다른 핸들러가 삼킨 경우) 직접 넣는다 */
    if (isSpace(e) && P.spaceDown && t - P.spaceDown < 1.5) {
      P.spaceDown = 0;
      if (document.activeElement === P.input && !caretHasSpace()) { insertSpace(); trace({ type: 'fix:keyup-space' }); }
    }
  }, { capture: true });
  addEventListener('keypress', (e) => { if (P.open) { trace(e); e.stopImmediatePropagation(); } }, { capture: true });

  /* ══════════ 말풍선 — paintUI 뒤, UI 캔버스(안개 위) ══════════ */
  function wrapText(c, txt, maxW) {
    const out = []; let cur = '';
    for (const ch of txt) { const t = cur + ch; if (c.measureText(t).width > maxW && cur) { out.push(cur); cur = ch; } else cur = t; }
    if (cur) out.push(cur);
    if (out.length > CFG.bubbleLines) { out.length = CFG.bubbleLines; out[CFG.bubbleLines - 1] = out[CFG.bubbleLines - 1].replace(/.$/, '') + '…'; }
    return out;
  }
  function bubblePath(c, x, y, w, h, tx, r) {
    /* 둥근 사각형 + 아래쪽 꼬리(tx 는 꼬리 끝 x, 꼬리 끝은 y+h+9) */
    const t0 = clamp(tx, x + r + 8, x + w - r - 8);
    c.beginPath();
    c.moveTo(x + r, y); c.lineTo(x + w - r, y); c.arcTo(x + w, y, x + w, y + r, r);
    c.lineTo(x + w, y + h - r); c.arcTo(x + w, y + h, x + w - r, y + h, r);
    c.lineTo(t0 + 7, y + h); c.lineTo(tx, y + h + 9); c.lineTo(t0 - 7, y + h);
    c.lineTo(x + r, y + h); c.arcTo(x, y + h, x, y + h - r, r);
    c.lineTo(x, y + r); c.arcTo(x, y, x + r, y, r); c.closePath();
  }
  function charPos(seat) {
    if (typeof seat === 'string' && seat.startsWith('ai:')) {
      if (!(typeof AICREW !== 'undefined') || !AICREW.members) return null;
      const m = AICREW.members.find((x) => 'ai:' + x.id === seat);
      return m && isFinite(m.x) ? { x: m.x, y: m.y, mine: false } : null;
    }
    if (seat === mySeat()) return (G.sh && isFinite(G.sh.x)) ? { x: G.sh.x, y: G.sh.y, mine: true } : null;
    if (!coopOn() || !COOP.peers) return null;
    const p = COOP.peers.get(seat);
    if (!p || !isFinite(p.x) || !isFinite(p.y) || p.status === 'escaped' || p.dropped) return null;
    return { x: p.x, y: p.y, mine: false };
  }
  function drawBubbles(c) {
    const t = now(), z = (G.Z || 1), R = (typeof R_SHELLY !== 'undefined') ? R_SHELLY : 25;
    const reduced = (typeof CREW_SETTINGS !== 'undefined') && CREW_SETTINGS.reducedMotion;
    for (const [seat, b] of P.bubbles) {
      const age = t - b.t, left = CFG.bubbleSec - age;
      if (left <= 0) { P.bubbles.delete(seat); continue; }
      const pos = charPos(seat); if (!pos) continue;
      if (!pos.mine && !seen(pos.x, pos.y)) continue;   /* 안개 속 동료의 위치를 말풍선으로 노출하지 않는다 */
      const s = w2s(pos.x, pos.y), sx = s[0], sy = s[1];
      if (sx < -R * z || sx > LW + R * z || sy < 0 || sy > LH + R * z) continue;   /* 화면 밖 캐릭터의 말풍선은 모서리에 붙이지 않는다 */
      const alpha = left < CFG.bubbleFade ? Math.max(0, left / CFG.bubbleFade) : 1;
      const pop = reduced ? 1 : age < CFG.bubblePop ? 0.6 + 0.4 * Math.sin((age / CFG.bubblePop) * Math.PI / 2) : 1;
      c.save();
      c.font = '800 12px ' + FONT; c.textBaseline = 'middle'; c.textAlign = 'center';
      const lines = wrapText(c, b.text, CFG.bubbleMaxW);
      let w = 0; for (const l of lines) w = Math.max(w, c.measureText(l).width);
      w += 22; const lh = 16, h = lines.length * lh + 12;
      const tailY = sy - (R + CFG.bubbleLift + (seat.startsWith('ai:') ? 14 : 0)) * z;         /* 꼬리 끝 — 명판 위 */
      const bx = clamp(sx - w / 2, 8, LW - w - 8), by = Math.max(8, tailY - 9 - h);
      c.globalAlpha = alpha;
      c.translate(sx, tailY); c.scale(pop, pop); c.translate(-sx, -tailY);
      /* 그림자 → 본체 → 테두리(좌석색 살짝) */
      c.fillStyle = 'rgba(20,10,14,.45)'; bubblePath(c, bx + 2, by + 3, w, h, sx + 2, 9); c.fill();
      c.fillStyle = '#FFF6E0'; bubblePath(c, bx, by, w, h, sx, 9); c.fill();
      c.lineWidth = 2.5; c.strokeStyle = '#3A2418'; c.lineJoin = 'round'; bubblePath(c, bx, by, w, h, sx, 9); c.stroke();
      c.lineWidth = 1; c.strokeStyle = seatCol(seat); c.globalAlpha = alpha * 0.9; bubblePath(c, bx + 2.5, by + 2.5, w - 5, h - 5, sx, 7); c.stroke();
      c.globalAlpha = alpha; c.fillStyle = '#3A2418';
      lines.forEach((l, i) => c.fillText(l, bx + w / 2, by + 6 + lh * (i + 0.5) + 0.5));
      c.restore();
    }
  }
  function drawAll() {
    if (!ready() || SCENE !== 'depths') return;
    const c = (typeof ux !== 'undefined' && ux) ? ux : cx;
    if (!c || typeof w2s !== 'function') return;
    watchSocket();
    if (P.bubbles.size) { c.save(); c.setTransform(DPR, 0, 0, DPR, 0, 0); drawBubbles(c); c.restore(); }
  }

  /* ══════════ AI 크루 멘트 — 관전·동행 중 크루끼리 떠드는 느낌 ══════════
     상태 전이를 보고 승인된 10개 문장을 낸다. 전체 15~30초에 한 줄(긴급은 6초), 같은 문장은 한 판에 2번,
     같은 크루는 20초에 한 번. 코옵 전송은 하지 않는다(각자 화면에서 로컬로만 — 중복 방지). */
  const AI_LINES = {
    ore:     { text: '여기 광맥 있다, 이쪽으로 와', urgent: false },
    reload:  { text: '총알 다 떨어졌어, 잠깐만', urgent: false },
    chased:  { text: '내 뒤에 벌레 붙었어 ㅋㅋ', urgent: false },
    hard:    { text: '이 벽은 단단하네… 좀 걸린다', urgent: false },
    lowhp:   { text: '잠깐, 나 피 없어', urgent: true },
    down:    { text: '야 누가 나 좀 일으켜줘', urgent: true },
    revived: { text: '됐다, 살렸어', urgent: true },
    boss:    { text: '보스다… 다들 흩어져', urgent: true },
    escape:  { text: '탈출 포트 열렸어, 슬슬 가자', urgent: false },
    idle:    { text: '여긴 너무 조용한데, 더 내려갈까?', urgent: false },
  };
  const AI_ROLE_KO = { driller: '드릴러', gunner: '거너', scout: '스카우트', engineer: '엔지니어' };
  const A = { on: false, nextAt: 0, lastSayAt: -1e9, used: {}, lastAct: 0, lastIdle: 0, bossSeen: false, escapeSeen: false, leaderDown: false, mem: new Map(), t: 0 };
  const rnd = (a, b) => a + Math.random() * (b - a);
  function aiEnabled() {
    if (CFG.aiChat === 'off' || !playing()) return false;
    if (!(typeof AICREW !== 'undefined') || !AICREW.members || !AICREW.members.length) return false;
    if (CFG.aiChat === 'observer') { const O = window.OBS || window.OBSERVER; return !!(O && O.active); }
    return true;
  }
  function aiSay(m, key) {
    const L = AI_LINES[key]; if (!L) return false;
    const t = now();
    if ((A.used[key] || 0) >= CFG.aiPerLine) return false;
    /* 전체 간격 15~30초. 긴급 문장(기절·저체력·부활·보스)은 마지막 발화 후 6초만 지나면 낸다 */
    if (t < A.nextAt && !(L.urgent && t - A.lastSayAt >= CFG.aiGapUrgent)) return false;
    const st = aiState(m); if (t - st.lastSay < CFG.aiMemberCd && !L.urgent) return false;
    A.used[key] = (A.used[key] || 0) + 1; A.nextAt = t + rnd(CFG.aiGapMin, CFG.aiGapMax); A.lastSayAt = t; st.lastSay = t;
    post({ seat: 'ai:' + m.id, name: 'AI ' + (AI_ROLE_KO[m.roleId] || m.roleId || '크루'), text: L.text, local: false });
    return true;
  }
  function aiState(m) {
    let st = A.mem.get(m.id);
    if (!st) { st = { lastSay: -1e9, mine: null, reload: false, chased: -1e9, dig: 0, low: false, down: false }; A.mem.set(m.id, st); }
    return st;
  }
  function aiReset() {
    A.nextAt = now() + rnd(6, 12); A.lastSayAt = -1e9; A.used = {}; A.lastAct = now(); A.lastIdle = now(); A.bossSeen = false; A.escapeSeen = false;
    A.leaderDown = !!(G && G.downed); A.mem = new Map();
  }
  function aiTick() {
    const on = aiEnabled();
    if (on !== A.on) { A.on = on; if (on) aiReset(); }
    if (!on) return;
    const t = now(), dt = A.t ? Math.min(0.1, t - A.t) : 0; A.t = t;
    const M = AICREW.members, C = (typeof CELL !== 'undefined') ? CELL : 50;
    const enemies = (G.enemies || []).filter((e) => e.hp > 0);
    const alive = M.filter((m) => !m.down);
    const pick = () => alive.length ? alive[Math.floor(Math.random() * alive.length)] : null;
    let action = false;
    /* 8) 보스 등장 */
    const boss = enemies.some((e) => e.boss);
    if (boss && !A.bossSeen) { const m = pick(); if (m && aiSay(m, 'boss')) A.bossSeen = true; }
    if (!boss) A.bossSeen = false;
    /* 9) 탈출 포트 설치 완료 */
    const I = (typeof INF !== 'undefined' && INF.active) ? INF : null;
    const esc = !!(I && I.escape && I.escape.state && I.escape.state !== 'placing');
    if (esc && !A.escapeSeen) { const m = pick(); if (m && aiSay(m, 'escape')) A.escapeSeen = true; }
    if (!esc) A.escapeSeen = false;
    /* 7) 리더 부활 — 가장 가까운 AI 가 말한다 */
    const ld = !!G.downed;
    if (A.leaderDown && !ld && G.sh) { const m = nearestAi(G.sh.x, G.sh.y, C * 2.5, null); if (m) aiSay(m, 'revived'); }
    A.leaderDown = ld;
    for (const m of M) {
      const st = aiState(m);
      /* 6) 기절 → 7) 부활(다른 AI 가 근처면 그 AI 가 "살렸어") */
      if (m.down) { if (!st.downSaid && aiSay(m, 'down')) st.downSaid = true; if (!st.down) st.down = true; }
      else if (st.down) { st.down = false; st.downSaid = false; const r = nearestAi(m.x, m.y, C * 2.5, m); if (r) aiSay(r, 'revived'); }
      if (m.down) continue;
      /* 1) 새 광맥 채굴 시작 */
      const mt = m.mineTarget || null;
      if (mt && mt !== st.mine && aiSay(m, 'ore')) st.mine = mt;
      if (!mt) st.mine = null;
      /* 2) 재장전 시작 */
      const rl = (m.reloadLeft || 0) > 0;
      if (rl && !st.reload && (m.roleId === 'gunner' || m.roleId === 'scout')) aiSay(m, 'reload');
      st.reload = rl;
      /* 3) 적 2마리 이상 근접 */
      let near = 0; for (const e of enemies) if (Math.hypot(e.x - m.x, e.y - m.y) < C * 3.5) near++;
      if (near >= 2 && t - st.chased > 30 && aiSay(m, 'chased')) st.chased = t;
      if (near) action = true;
      /* 4) 단단한 벽 3초 이상 */
      if (m.digging) { st.dig += dt; action = true; if (st.dig >= 3 && !st.hardSaid && aiSay(m, 'hard')) st.hardSaid = true; }
      else { st.dig = 0; st.hardSaid = false; }
      /* 5) 저체력 진입 */
      const low = m.hpMax > 0 && m.hp / m.hpMax < 0.3;
      if (low && !st.lowSaid && aiSay(m, 'lowhp')) st.lowSaid = true;
      if (!low) st.lowSaid = false;
      st.low = low;
      if ((m.gunCd || 0) > 0) action = true;
    }
    /* 10) 한산함 — 20초 무전투·무채굴이면 60초에 한 번 */
    if (action || boss) A.lastAct = t;
    if (t - A.lastAct > CFG.aiIdleSec && t - A.lastIdle > CFG.aiIdleCd) { A.lastIdle = t; const m = pick(); if (m) aiSay(m, 'idle'); }
  }
  function nearestAi(x, y, r, except) {
    let best = null, bd = r;
    for (const m of AICREW.members) { if (m === except || m.down) continue; const d = Math.hypot(m.x - x, m.y - y); if (d < bd) { bd = d; best = m; } }
    return best;
  }

  /* ══════════ 표시 상태 · 학습 · 훅 ══════════ */
  function syncShow() {
    if (!ensureDom()) return;
    const on = playing();
    P.el.classList.toggle('show', on);
    if (!on && P.open) close(true);
    tickFade();
  }
  function keyGuide() {
    const el = $('infKeyGuide'); if (!el || el.querySelector('.tcChatKeys')) return;
    el.insertAdjacentHTML('beforeend', '<br><span class="tcChatKeys"><b>ENTER</b> 크루 채팅</span>');
  }
  function hook() {
    if (typeof paintUI === 'function' && !paintUI._tcChat) {
      const prev = paintUI;
      paintUI = function () { prev.apply(this, arguments); try { drawAll(); } catch (e) { if (P.debug) console.error('[TCCHAT]', e); } };
      paintUI._tcChat = true;
    }
    watchSocket(); keyGuide(); syncShow();
  }
  function boot() { hook(); setInterval(hook, 1000); setInterval(tickFade, 500); setInterval(() => { try { aiTick(); } catch (e) { if (P.debug) console.error('[TCCHAT ai]', e); } }, 120); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();

  /* 디버그/테스트용 API */
  P.say = (text) => { const m = post({ seat: mySeat(), name: myName(), text: clean(text).slice(0, CFG.maxLen), local: true }); wsSend({ t: 'chat', v: P.ver, text: m.text, at: Date.now() }); return m; };
  P.simulateRemote = (text, seat, name) => onRemote({ t: 'chat', from: seat || 'p2', fromName: name || '테스트', text });
  P.openBox = open; P.closeBox = close;
  P.ai = A; P.aiSay = aiSay; P.aiLines = AI_LINES;
  P.dump = () => { try { console.table(P.trace); } catch (e) {} return JSON.stringify(P.trace); };
  P.reset = () => { P.log = []; P.bubbles.clear(); P.draft = ''; renderLog(); };
})();
