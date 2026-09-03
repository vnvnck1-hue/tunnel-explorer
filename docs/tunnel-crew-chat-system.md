# 땅굴 크루 — 크루 채팅 (TEAM_CHAT_V1)

작성 2026-09-03 · 본선 `tunnel-crew-infinite-mode-v7.9.0.html` 에 주입 완료.

## 조작

| 키 | 동작 |
|---|---|
| **Enter** | 좌하단 채팅창 열기 (심층 플레이 중에만 · 모달/대화/탈출 배치 중엔 안 열림) |
| **Enter** (입력 중) | 전송. 빈 칸이면 그냥 닫기 |
| **Esc** (입력 중) | 취소 — 쓰던 내용은 다음에 열 때 복원 |

입력 중에는 WASD·E·Q·R·X·Space·G·V·Tab 등 본편 단축키가 전부 막힌다 (window 캡처 단계에서 `stopImmediatePropagation`). 열리는 순간 눌려 있던 이동키(`KEY`)와 마우스 홀드를 풀어 캐릭터가 미끄러지지 않는다. 핑 모듈은 `TCCHAT.open` 을 보고 G·V 를 무시한다.

## 표현 (LOL 채팅창 문법)

- **좌하단 로그** — 평소엔 최근 **3줄**, 각 줄은 10초 뒤 서서히 사라진다. Enter 로 열면 **8줄** 박스로 확장되고 입력창이 아래 붙는다. 이름은 좌석색(P1 노랑 · P2 민트 · P3 주황 · P4 보라).
- **말풍선** — 보낸 캐릭터 머리 위(명판 위) 크림색 말풍선, 최대 3줄 · 폭 210px, **5초** 뒤 0.5초 페이드. UI 캔버스(안개 위)에 그린다. 화면 밖 캐릭터는 그리지 않고, 안개 속(LOS 미확인) 동료의 말풍선도 위치 노출을 막기 위해 생략한다(로그에는 남는다).
- 위치는 캔버스 역할 카드 바로 위(`left:18px; bottom:104px`), 하단 중앙 키 가이드에 **ENTER 크루 채팅** 항목이 추가된다.

## 코옵

- 전송: `COOP.ws` 로 `{t:'chat', v:1, text, at}` — 서버(`coop/server.mjs`)의 `RELAY_TYPES` 에 `'chat'` 을 추가했다. 서버가 `from`(좌석)·`fromName`(닉네임)을 붙여 나머지 전원에게 릴레이한다.
- 수신: 제어문자 제거 · 80자 절단 · 좌석/이름 정리 후 로그 + 해당 피어 말풍선(`COOP.peers.get(seat)` 위치).
- 싱글에서는 좌석 `p1`, 이름 `나` 로 동작한다.

## 파일

| 파일 | 역할 |
|---|---|
| `chat/tc-chat.js` | 모듈 본체 (`window.TCCHAT`) — 수정은 여기서 |
| `chat/inject-chat.mjs` | 주입기. `node chat/inject-chat.mjs <대상.html>` — 마커 `TEAM_CHAT_INJECTED_V1` 이 있으면 본문만 교체(재주입) |
| `ping/tc-ping.js` | `inputOk()` 에 채팅 열림 가드 추가 → `node ping/inject-ping.mjs` 로 재주입함 |
| `coop/server.mjs` | `RELAY_TYPES` 에 `'chat'` |

## 검증 (2026-09-03, 코옵 서버 5488)

- 싱글: Enter → 입력 `wasd 안녕 크루! … gv` → Enter. 캐릭터 이동 0, `KEY` 비어 있음, 핑 미발동, 말풍선·로그 정상. 4번째 메시지부터 로그 3줄 유지, 열면 8줄 확장, Esc 초안 보존 확인.
- 코옵 2인(호스트 드릴러 · 게스트 스카우트): 양방향 릴레이, 닉네임·좌석색, 상대 화면에서 내 말풍선 표시 확인. 콘솔 에러 0 (404 는 새 닉네임의 `/meta/` 첫 조회로 기존 동작).

## 한글 IME 보정 (2026-09-03 추가)

- 조합 중인 키는 keydown 이 keyCode 229(`Process`)로 오고, 조합을 끝내는 **Enter/Space 는 두 번째 keydown 없이 keyup 만** 올 수 있다.
- Enter: 조합 중 keydown 이면 `enterPending` 만 세우고, 이어지는 keyup Enter 에서 전송한다 → 한 번의 Enter 로 조합 확정 + 전송.
- Space: keydown 에 `spaceDown` 시각을 기록하고, keyup Space 때 커서 앞에 공백이 없으면 `setRangeText` 로 직접 넣는다(IME·다른 핸들러가 삼킨 경우 보정). 정상 삽입됐으면 아무 것도 하지 않는다. 채팅을 열기 전부터 누르고 있던 Space 의 keyup 은 무시한다.
- 합성 이벤트 검증: keydown+keyup Space(삽입 없음) → 공백 1개 보정, keyup 단독 → 무변화, 조합 Enter keydown → 미전송 · keyup → 전송, 일반 Enter → 즉시 전송, 열기용 Enter 의 keyup → 무동작, 입력 중 W → `KEY` 누출 없음.
- 브라우저 자동화 도구는 Space 를 빈 키(key "")로만 보내 실제 IME 재현이 불가하다 — 실기기 한글 입력으로 재확인 필요.

## AI 크루 멘트 (2026-09-03 추가 · 검수 완료 10문장)

관전·동행 중 AI 크루가 팀 채팅에 던지는 문장. 로그 이름은 `AI 거너`처럼 역할명, 색은 회색(`#C9C9D6`), 말풍선은 사람과 동일(명판 위로 14px 더 올림). 코옵 전송은 하지 않는다(각 화면 로컬).

| 키 | 문장 | 트리거 | 긴급 |
|---|---|---|---|
| ore | 여기 광맥 있다, 이쪽으로 와 | `mineTarget` 이 새 대상으로 바뀔 때 | |
| reload | 총알 다 떨어졌어, 잠깐만 | 거너·스카우트 `reloadLeft` 0→양수 | |
| chased | 내 뒤에 벌레 붙었어 ㅋㅋ | 3.5칸 안 적 2마리 이상(크루별 30초 쿨) | |
| hard | 이 벽은 단단하네… 좀 걸린다 | `digging` 연속 3초 | |
| lowhp | 잠깐, 나 피 없어 | HP 30% 미만 진입 | ✓ |
| down | 야 누가 나 좀 일으켜줘 | 기절 진입 | ✓ |
| revived | 됐다, 살렸어 | 기절 해제 시 2.5칸 안 가장 가까운 AI(리더 부활 포함) | ✓ |
| boss | 보스다… 다들 흩어져 | `e.boss` 적 등장 | ✓ |
| escape | 탈출 포트 열렸어, 슬슬 가자 | `INF.escape.state` 가 placing 이외로 | |
| idle | 여긴 너무 조용한데, 더 내려갈까? | 20초 무전투·무채굴, 60초에 1회 | |

- 빈도: 전체 15~30초에 한 줄(`aiGapMin/Max`), 긴급 문장은 마지막 발화 6초 뒤부터(`aiGapUrgent`), 같은 문장 한 판 2회(`aiPerLine`), 같은 크루 20초(`aiMemberCd`). 간격 제한으로 거절되면 조건이 이어지는 동안 재시도한다(상태 플래그는 발화 성공 시에만 세움).
- 켜기/끄기: `TCCHAT.cfg.aiChat` = `always`(기본, AI 크루가 있으면 항상) · `observer`(관전 중만) · `off`.
- 틱은 paintUI 훅이 아니라 120ms 인터벌(`aiTick`)로 돈다. 디버그: `TCCHAT.ai`(상태) · `TCCHAT.aiSay(member, key)` · `TCCHAT.aiLines`.
- 검증(관전 모드 3인): ore 자연 발화, lowhp/down/revived 강제 상태 전이로 확인. 말풍선이 AI 명판 위에 뜨고 로그에 회색 이름으로 남는다.

## 디버그 API

`TCCHAT.say(text)` · `TCCHAT.simulateRemote(text, seat, name)` · `TCCHAT.openBox()` · `TCCHAT.closeBox(keepDraft)` · `TCCHAT.reset()` · 튠은 `TCCHAT.cfg`.

## 남은 일

- `build/TunnelCrew-v7.9.0/` 패키지는 채팅 주입 전 본선으로 만들어졌다. 배포하려면 `node tools/build-package.mjs --force` 로 다시 만들어야 한다(패키지 안 `coop/server.mjs` 도 갱신됨).
- AI 크루 멘트는 10문장 고정. 역할별 변형·추가 문장은 `AI_LINES` 에 키를 늘리고 `aiTick` 에 트리거를 붙인다.
