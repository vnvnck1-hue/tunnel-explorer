# Phase 0 — 기준 빌드 확정 + 현황 스냅샷

> 작성: 2026-08-31 01:15 · 세션: trend-dopamine 작업지시서 실행
> 기준 파일: `tunnel-crew-infinite-mode-v7.7.0-ui-develop.html` (사용자 직접 지정 — Phase 0.1의 diff 판정 생략)
> 작업 파일: `tunnel-crew-infinite-mode-v7.8.0-dopamine.html` (복사본, 16,183,812 bytes)

## 0.1 기준 파일 판정

사용자가 발주 시 `v7.7.0-ui-develop`을 명시 지정. v7.7.0 본선(16,176,920B)보다 6,892B 크며 UI 개선분을 포함. 이후 모든 작업은 `v7.8.0-dopamine` 복사본에서 진행.

## 0.2 시스템 인벤토리 (코드 확인 기준)

| 시스템 | 규모 | 코드 근거 (v7.8.0-dopamine.html) |
|---|---|---|
| 직업 | 4종 (드릴러/거너/스카우트/엔지니어) | `INF_ROLES` L9883, 직업별 지형·전투 장비 분리 |
| 런 특성 카드 | 직업 전용 8종×4 + 노드 해금 전용 16종 + 공용(u_*) 25종 | `INF_TRAITS` L10090–10170, 티어 1~4 (일반/희귀/영웅/전설) |
| 특성 티어 추첨 | 피티 시스템 존재 (`INF.pity`) | `infRollTier` L10279 |
| 영구 노드 | `INF_PERMANENT_NODES` L9859 — 클러스터·직업 허브·발견 시퀀스 포함 성장 지도 | 정산 화면에서 구매, §6.4.3 발견 연출 구현됨 |
| 유물(Relic) | 33종 (일반 7, 희귀 6, 원소 4계열×3, 전설 8) + 원소 공명 | `INF_RELICS` L12095–12136, 소켓 4(+왕관 5) |
| 보스 | 심층별 티어 (`INF_BOSS_TIER` L11178), 장갑·벽 소환·패턴 3종(field/wave/prison) | `infBossStartPattern` L11230 |
| 지층 구조 | `infStratumCount`/`infIsAbyss` L10873 — 지층별 벽·적 HP 배율, 장악도(dominance) 목표 달성 시 보스 소환 | L10612 |
| 탈출 | 탈출 포트 소환·배치·카운트다운 (`INF_ESCAPE` L11422, `infUpdateEscape` L11480) | 코옵 동기화 포함 |
| XP 관문 | 단일 관문 `infAwardXp` L10656 — 직업별 가중치 테이블, 지층당 트리클 상한 60 | `INF_XP_TABLE`/`INF_XP_WEIGHT` L10627 |
| AI 크루 | `AICREW.onRunStart/onRunEnd` 훅, crew-ai.js 주입 경로 | L10346, L12770 |
| 정산 화면 | 요약(ledger 4칸) / 성장 지도 / 유물 보관고 3뷰 | `infPaintSettlement` L11870 |
| 사운드 어휘 | tick, ore, brk, dig, exit, res, cache, descend, ui, dawn, back, buy, reload, ready, fail, timeout, dash, start | 전역 SFX 객체 |
| 연출(주스) | J.text / J.ring / J.burst / J.kick / J.spikes, toast() | 전역 J 객체 |
| 테스트 패널 | `?tcTest` 쿼리로 활성 — LEVEL/CORE/BOSS/RETURN/END 등 강제 트리거 | `infInstallTestPanel` L12817 |

## 0.3 실플레이 계측

**미실시 (마감 제약).** 사용자 지정 마감(04:00)까지 약 3시간 — 계측 1회(원정 풀사이클 자동 스텝핑)에 30~40분 소요 예상되어, 구현 검증용 스텝핑(Phase 6 ⑤)으로 대체하고 전체 계측은 다음 세션 과제로 이월. → 질문 대기 목록에 기재.

## 0.4 코드로 확인한 "도파민 관점" 현황

- **정산 요약**: 숫자 4칸이 innerHTML로 **한 번에** 뜬다. 카운트업·순차 공개 없음 (`infPaintSettlement` L11882 — 즉시 대입).
- **런 종료(infEndRun L12768)**: 도달 심층·보스·블록·코어만 표기. **개인 활약 기록(최대 콤보, 클러치, 웃긴 사인) 없음.** 실패 시 "다음 런에서는 모든 런 성장이 초기화됩니다" — 실패가 순수 손실로만 서술됨.
- **희귀 광맥 보상**: `rare` 파괴 시 코어 +1(+보너스 확률) 고정 — **분산 없음, 잭팟 순간 없음** (L10600). 유일한 변동성은 `coreBonusChance`(노드 투자)와 유물 드랍(`infRelicOnRare`).
- **연속 굴착**: 블록 카운터 기반 트리거(5개마다 파편 등)는 있으나 **콤보/피버 같은 흐름 보상 없음.** `focusDrill`(같은 벽 집중)만 존재.
- **탈출**: 카운트다운·탑승 구현. 배당·리스크 베팅 요소 없음.
