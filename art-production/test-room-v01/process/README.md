# Test Room V01 아트 생산 프로세스

이 폴더가 테스트 방 V01 아트 생산과 Unity 인계 기록의 단일 진입점이다. 다른 `docs/` 문서와
혼동하지 않도록 패키지별 기록을 이 위치에 모은다.

## 문서 순서

1. `keyart-conversation-handoff.md` — 다른 환경에서 새 키아트 대화를 즉시 이어가기 위한 현재 맥락
2. `primary-style-target-analysis.md` — 새 키아트의 주 레퍼런스, 화면·스타일·색·조명·Unity 재현 분석
3. `production-spec.md` — 해상도, 채널, 피벗, footprint, 명명 규칙, 승인 기준과 생산 절차
4. `generation-log.md` — 생성 모드, 프롬프트, 원본·결과 경로, 선택·폐기 이유와 후처리 이력
5. `unity-handoff.md` — Claude가 소비할 경로, Unity 임포트 규칙, 조립 순서와 다음 수정 게이트
6. `incoming-fix-request-r17.md` — Claude의 Unity 실측에서 발견된 후속 수정 요청과 합격 기준

## 기계 판독 기준

- 승인 자산 계약: `../metadata/manifest.json`
- 구현 입력: `../approved/`
- 현재 납품 기준: manifest revision 17, 승인 자산 51종, 필수 채널 파일 235개
- 검증 명령: `& tools/art/validate-test-room-package.ps1`

## 현재 후속 상태

Claude의 Unity 임포트 검사에서 발견된 9개 자산의 반투명 배경 잔여는 revision 17에서
수정했다. 45개 채널이 동일 알파를 공유하며 자산별 반투명 비율은 0.18~2.44%다. 다음 게이트는
Claude의 재검사·재임포트 결과이며, 상세 요청과 처리 판정은 `incoming-fix-request-r17.md`에 남긴다.

## 재현 도구

실행 스크립트는 저장소 공용 도구 규칙에 따라 `tools/art/`에 유지한다. 문서를 복제하거나
스크립트를 이 폴더에 복사하지 않고 아래 원본만 사용한다.

- `generate-test-room-material-maps.ps1`
- `finalize-test-room-arch.ps1`
- `finalize-test-room-pillars.ps1`
- `finalize-test-room-lights.ps1`
- `finalize-test-room-hero.ps1`
- `finalize-test-room-linear.ps1`
- `finalize-test-room-decorations.ps1`
- `final-test-room-foreground.ps1`
- `finalize-test-room-vfx.ps1`
- `finalize-test-room-wall-variants.ps1`
- `fix-test-room-alpha-r17.ps1`
- `validate-test-room-package.ps1`

## 변경 규칙

규격 변경은 `production-spec.md`, 생성·후처리 변경은 `generation-log.md`, 구현 계약 변경은
`unity-handoff.md`와 manifest를 같은 작업에서 갱신한다. 승인본을 직접 덮어쓰고 기록을 생략하지
않으며, 다음 라운드는 Claude의 실제 Unity 비교 캡처를 입력으로 시작한다.
