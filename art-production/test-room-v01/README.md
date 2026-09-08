# Test Room V01 Art Package

지층 1의 작은 디오라마형 최종급 테스트 방을 위한 아트 생산 패키지다. 이 폴더는 아트 트랙의
작업 영역이며 Unity 코드·씬·프리팹은 포함하지 않는다.

현재 상태:

- 방 전체 키아트 1종: `concept`
- 모듈 키트 분해 보드 1종: `concept`
- 후면 산업 아치 1종: 640×512px, 5개 채널 승인 완료
- 바닥 기본 6종: 5개 채널 승인 완료
- 벽 정면 초기 생성 소스 3종: 규격 불일치로 `rejected`, 디자인 참고용 보존
- 벽 정면 A–F 6종: 128×128px, 동일 밴드 구조·반복 경계·5개 채널 승인 완료
- 벽 상단 A–F 6종과 연속 림 A: 128×128px, 반복 경계·5개 채널 승인 완료
- 기둥 정상·파손 2종: 256×512px, 5개 채널 승인 완료
- 작업등·경고등·결정등 3종: 256×256px, 발광 분리와 LightSocket 승인 완료
- 고장 난 대형 드릴 발전 설비 1종: 512×384px, 3×2셀, 5개 채널 승인 완료
- 레일·파이프·케이블 모듈 6종: 256×256px, 연결 포트와 5개 채널 승인 완료
- 장식 소품 10종: 256×256px, 5개 채널 승인 완료
- 전경 오클루더 4종: 512×256px, 5개 채널과 페이드 마스크 승인 완료
- 환경 VFX 4종: 먼지·저층 안개·광선·낙진 승인 완료
- Unity 인계 가능한 `approved` 자산: 51종(아치 1, 바닥 6, 벽 정면 6, 벽 상단 6, 림 1, 기둥 2, 조명 3, 대형 설비 1, 선형 모듈 6, 장식 10, 전경 4, VFX 4, 접촉 AO 1)
- 패키지 검사: manifest revision 17, 필수 채널 파일 235개, `PASS`
- Unity 실측 후속 상태: 장식 6종·선형 모듈 3종의 반투명 배경 재키잉을 revision 17에서
  완료했다. 반투명 비율 0.18~2.44%, 5채널 알파 동일성 검사를 통과했으며 Claude 재임포트
  대기 상태다. 상세 기준은 `process/incoming-fix-request-r17.md`를 따른다.

`concept/`는 화면 목표, `source/`는 생성 원본, `working/`은 클린업·채널 제작 중간물,
`approved/`는 구현 담당에게 넘길 수 있는 검수 완료본이다. 구현 담당은 `approved/`와
`metadata/manifest.json`만 직접 소비한다.

일반 보조 채널은 `tools/art/generate-test-room-material-maps.ps1`, 산업 아치는
`tools/art/finalize-test-room-arch.ps1`, 기둥은 `tools/art/finalize-test-room-pillars.ps1`,
조명은 `tools/art/finalize-test-room-lights.ps1`, 패키지 검수는
`tools/art/validate-test-room-package.ps1`로 재현한다. revision 17 알파 수정은
`tools/art/fix-test-room-alpha-r17.ps1`로 재현한다. 대형 설비는
`tools/art/finalize-test-room-hero.ps1`, 선형 모듈은
`tools/art/finalize-test-room-linear.ps1`, 장식 소품은
`tools/art/finalize-test-room-decorations.ps1`, 벽 변형은
`tools/art/finalize-test-room-wall-variants.ps1`로 배경 분리와 채널 제작을 다시 수행할 수 있다.
전경은 `tools/art/final-test-room-foreground.ps1`, VFX는
`tools/art/finalize-test-room-vfx.ps1`로 재현한다.

이 패키지의 생산 기록은 `process/` 한 곳에 모았다. 시작 문서는 `process/README.md`, 다른 환경에서
키아트 대화를 재개할 때는 `process/keyart-conversation-handoff.md`, 새 키아트의 최우선 시각 기준은
`process/primary-style-target-analysis.md`, 상세 규격은
`process/production-spec.md`, 생성 이력은 `process/generation-log.md`, Claude
구현 트랙 인계 절차는 `process/unity-handoff.md`다. 전체 기능 경계는
`docs/unity-visual-overhaul-functional-spec.md`를 따른다.
