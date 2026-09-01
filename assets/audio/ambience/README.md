# 앰비언스 BGM

로비/땅굴 환경음. 재생기는 `claude-work/audio/ambience_block.js` (`const AMBI`),
본편 반영은 `python claude-work/audio/apply-kenney-sfx.py`.

| 파일 | 용도 | 길이 | 레이어 게인 | 출처 |
|---|---|---|---|---|
| `lobby-cave.webm` | 메인 로비 | 76.8s | 1.00 | `770379__hushless__cave-ambience-loop.wav` |
| `tunnel-dungeon.ogg` | 땅굴 (1층) | 94.3s | 0.85 | `dungeon_ambient_1.ogg` |
| `tunnel-cave-stereo.ogg` | 땅굴 (2층) | 57.0s | **3.20** | `553080__nox_sound__ambiance_atmosphere_cave_loop_stereo.ogg` |

땅굴은 두 파일을 **동시에** 재생한다. 길이가 94.3s / 57.0s 로 서로 달라서
합쳐진 주기가 길고, 재생 시작 위치도 매번 랜덤이라 반복감이 잘 안 느껴진다.

`tunnel-cave-stereo` 의 게인이 3.20 으로 큰 것은 원본 녹음 레벨이 매우 낮기 때문이다
(단독 RMS 0.0037 vs dungeon 0.0229). 실측해서 두 겹이 대등하게 들리도록 맞췄다.

## 왜 `<audio>` 가 아니라 Web Audio 인가

기존 `LOBBY_MUSIC` / `PURPLE_MUSIC` 은 `<audio loop>` + base64 data URL 이었다.
`<audio>` 의 loop 는 이음새에서 미세한 끊김이 생겨서 환경음에는 부적합하고,
두 장을 겹칠 때 볼륨/페이드를 따로 못 잡는다.
`AudioBufferSourceNode(loop=true)` 는 이음새가 없고 레이어별 게인·크로스페이드가 된다.

`BGM_ROUTE` 쪽 호출부는 하나도 안 건드렸다. `LOBBY_MUSIC` / `PURPLE_MUSIC` 의
`play` / `pause` / `syncVolume` / `init` 만 AMBI 로 갈아끼웠다.

- 마스터/뮤트: `AU.mas` 가 처리 (AMBI 버스가 mas 에 연결)
- 음악 볼륨 슬라이더: AMBI 버스 게인이 `AU.vol.mus` 를 따라감
- 절차적 BGM(`MUS`)은 기존대로 `MUS.setExternal(true)` 로 음소거

## file:// 로 직접 열면

`fetch()` 는 `file://` 에서 CORS 로 막힌다. 그래서 HTML 을 더블클릭해서 열면
Web Audio 경로가 통째로 실패한다(예전 BGM 은 base64 내장이라 이 문제가 없었다).

그래서 `location.protocol==='file:'` 이면 자동으로 **`<audio loop>` 폴백**으로 간다.
루프 이음새가 약간 생기고, `<audio>.volume` 이 0~1 로 제한돼서
cave 레이어의 3.20 게인을 그대로 못 준다(레이어 최대값으로 정규화해 밸런스만 유지).
제대로 들으려면 서버로 띄우는 쪽을 권한다:

```bash
npx --yes http-server -c-1 .
```

진단은 콘솔에서 `AMBI.report()` — 프로토콜, 디코드 성공/실패 목록,
`<audio>` 엘리먼트별 재생 상태와 볼륨, 에러 코드를 한 번에 보여준다.
`DEMO.ambienceElement=true` 로 폴백 경로를 강제로 재현할 수 있다.

## 로비 진입 즉시 재생 (자동재생 잠금 해제)

브라우저 자동재생 정책상 사용자 제스처 전에는 `AudioContext` 가 `suspended` 라 소리가 안 난다.
원래 코드는 메뉴 버튼을 눌러 `crewEnsureBgm()` 이 돌 때까지 BGM 을 시작하지 않아서,
로비에 들어와도 아무것도 안 누르면 무음이었다.

`AUDIO_UNLOCK` 이 두 단계로 처리한다:

1. `window load` 직후 한 번 시도 — 자동재생이 허용된 환경(로컬 서버 등)이면 그대로 시작
2. 막혔으면 첫 입력(`pointerdown`/`keydown`/`touchstart`/`wheel`)에서 재시도, 성공하면 리스너 해제

저장된 음소거 설정은 존중한다. `AU.muted` 면 컨텍스트만 깨우고 BGM 은 켜지 않는다.

기본 설정값은 `CREW_SETTINGS` — `muted:false, master:.9, mus:.93, sfx:1`.
예전에 껐던 게 `localStorage` 에 남아 있으면 여전히 무음이다.
그 경우 로비 하단 `SOUND · OFF` 버튼을 누르거나
`localStorage.removeItem('tunnel_crew_settings_v1')` 후 새로고침하면 기본값으로 돌아간다.

## 전환

| 시점 | 세트 |
|---|---|
| 메인 메뉴 / 로비 (`BGM_ROUTE.useLobby`) | `lobby` |
| 무한 모드 출격 후 (`BGM_ROUTE.usePurple`) | `tunnel` (2겹) |
| 그 외 (`claimProcedural`) | 앰비언스 정지, 기존 절차 BGM |

페이드 인 1.4초 / 아웃 0.9초로 교차한다.

## 조정

```js
DEMO.ambience = false;      // 앰비언스 끄기 (기존 절차 BGM 로 안 돌아가고 무음)
DEMO.ambienceGain = 0.6;    // 전체 게인 배수
AMBI.sets.tunnel[1].g = 4;  // 레이어별 게인 (런타임 즉시 반영은 재생 재시작 후)
```

## 남은 것

원래 BGM base64 상수 `LOBBY_BGM_DATA`(4.3MB) / `PURPLE_BGM_DATA`(6.7MB) 는
더 이상 로드되지 않지만 HTML 안에 그대로 남아 있다. 되돌릴 여지를 남겨둔 것이고,
확정되면 지워서 HTML 을 11MB 줄일 수 있다.
