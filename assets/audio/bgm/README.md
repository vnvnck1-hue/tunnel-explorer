# 보스 BGM

보스 등장 연출과 함께 시작하는 전투 음악. 재생기는 `claude-work/audio/boss_bgm_block.js` (`const BOSS_BGM`),
본편 반영은 `node claude-work/audio/inject-boss-bgm.mjs <대상.html>` (제자리 재주입 · `BOSS-BGM-BLOCK` 마커 사이만 교체).

| 파일 | 용도 | 길이 | 레벨 | 출처 |
|---|---|---|---|---|
| `boss-blood-ascendant.mp3` | 보스전 (등장 → 처치) | 141.2s · 루프 | 0.78 × 음악 슬라이더 | davidjbarrios "Blood Ascendant" (Epic Boss Battle Instrumental Metal) |

## 동작

- **시작**: `infSpawnBoss` 직후 → `BGM_ROUTE.useBoss()`. 땅굴 앰비언스(AMBI tunnel)는 0.9s 페이드아웃,
  보스 곡은 0.55s 페이드인. 등장 시네마틱(tcBossFxJs)과 같은 프레임에 들어간다.
- **처치**: `infBossDefeated` 호출 순간(= 죽음 시네마틱 시작) 3.0s 페이드아웃, 2.6s 뒤 땅굴 앰비언스 복귀.
  시네마틱(3.7s)이 끝날 무렵 앰비언스가 돌아온다.
- **런 종료 / 층 전환 / 메뉴 복귀**: `infEndRun`·`infInitFloor` 훅과 `BGM_ROUTE.useLobby/usePurple/claimProcedural`
  래핑이 곡을 정리한다.
- **코옵 게스트**: 보스가 호스트 스냅샷(퍼펫)으로 도착해 `infSpawnBoss` 를 거치지 않으므로, 200ms 폴러가
  `INF.bossActive && INF.boss.hp>0` 를 보고 시작/정리한다. 호스트에서는 훅이 먼저 처리한다.
- 탭 복귀·포커스(`BGM_ROUTE.ensure`)는 보스 경로를 유지한다. 뮤트·음악 슬라이더는 `AMBI.syncVolume` 을 타고 반영된다.

## 경로

- 기본: `fetch` + Web Audio `AudioBufferSourceNode(loop)`. 디코드 후 앞뒤 무음(진폭 < 0.012)을 잘라
  `loopStart/loopEnd` 로 쓴다 (mp3 인코더 패딩으로 루프 이음새가 비는 것 방지). 땅굴 진입(`usePurple`) 시 미리 디코드.
- 폴백: `file://` 로 직접 열면 `<audio loop>` 로 간다 (앰비언스와 동일 정책).

## 튠 / 진단

- `DEMO.bossBgm=false` 끄기 · `DEMO.bossBgmGain` 레벨 배율 · `DEMO.bossBgmElement=true` 폴백 강제
- 콘솔 `BOSS_BGM.report()` — 경로, 디코드·루프 지점, 버스 게인, `<audio>` 상태, 현재 BGM 경로
