# Background Upgrade Lab

리서치 `background-visual-upgrade-research.md`의 첫 silhouette A/B/C 스파이크를 실행하는 26×20셀 테스트씬이다. 중앙 동굴, 왼쪽 수정 공동, 오른쪽 작업 구역을 연결했다. 드릴러는 본편 캐릭터 시트와 `PlayerView`, `LabPlayer`를 사용한다.

## 실행

- 브랜치: `codex/background-upgrade-lab`
- 씬: `unity/TunnelCrew/Assets/_Project/Scenes/BackgroundUpgradeLab.unity`
- Unity 메뉴: `Tunnel Crew > Background Upgrade Lab > Create or Open`
- Play 후 Game View에 포커스를 두고 WASD 또는 방향키로 이동한다.

| 조작 | 동작 |
|---|---|
| WASD / 방향키 | 이동, 실제 벽 충돌 |
| 마우스 / F | 조준 / 손전등 |
| 1 | A: 기존 타일 벽 |
| 2 | B: 타일 벽 + 멀티셀 암반·수정·파이프·레일 |
| 3 | C: 유기적 contour 메시 + 동일 장식 |
| 좌클릭 3회 | 가까운 벽 채굴 |
| 우클릭 | 가까운 빈 셀에 벽 복구 |
| M | 벽 재질의 월드 공간 매크로 명암 변화 켜기/끄기 |
| V | 전경·저층 안개 켜기/끄기 |
| G | 실제 게임 격자 겹쳐 보기 |
| Tab | 방 전체 보기 / 이동 시점 |
| - / + | 축소 / 확대 |
| R | 중앙 시작점으로 이동 |

## 구현 범위

`OrganicLabGeometry`는 기존 `WallContourTracer`의 외곽 고리를 셀당 4구간으로 나누고, 최대 0.18셀 이내에서 표면 위치만 변형한다. 동일 경계 샘플을 윗면·베벨·정면 skirt가 공유한다. 원본 고체 필드와 이동 충돌은 기존 랩 경로에 남는다. 오목한 방, 내부 벽 섬, 지도 테두리를 지원한다.

`BackgroundLabRock.shader`는 프로젝트의 2D 조명·노멀·AO 처리 함수를 재사용한다. 타일 테두리가 아닌 아틀라스 내부를 월드 좌표로 샘플링하고, 큰 범위의 명암 변화를 덧붙인다. 노멀·AO도 같은 UV를 사용한다. M은 매크로 명암만 토글하며, 월드 UV 샘플링은 유지한다.

벽은 4×1셀 크기로 나누어 정렬·페이드하며, 캐릭터를 가리는 윗면과 큰 소품은 반투명해진다. 바닥은 채굴 대상과 캐릭터가 잘 보이도록 기존 TestRoom 바닥 자산을 사용한다. A/B/C는 동일한 바닥·광원·게임 격자를 공유한다. B/C의 장식과 안개는 단계의 일부다.

수정과 작업등은 기존 아트/재질을 사용하고, 큰 암반·파이프·보강재·레일은 기존 TestRoom 키트로 구성한다. 장식은 시각 비교용이며 별도 채굴·상호작용 대상이 아니다. 이동 통로의 판정은 고체 필드가 담당한다.

검증 때 화면 왜곡이 비교를 방해하지 않도록 이 씬에서 CRT 효과를 런타임에 끄고, 씬을 나가면 이전 활성 상태를 돌려놓는다. 저장된 CRT 사용자 설정은 변경하지 않는다.

## 실험의 한계

- 연구의 첫 단계 스파이크다. 전면 3D, 실시간 3D 세트피스, 3D 프록시 조명은 이 씬의 구현 범위가 아니다.
- 채굴할 때 작은 방의 contour·메시·장식을 동기적으로 다시 만든다. 본편 대형 맵용 dirty 청크 최적화는 후속 작업이다.
- HUD의 rebuild 시간은 메시·장식 생성의 CPU 시간이다. GPU 시간·전체 프레임 시간 또는 목표 기기 성능 인증을 의미하지 않는다.
- 정적 Light2D 그림자는 게임 격자 외곽을 사용한다. 렌더 외곽선과 최대 0.18셀 편차가 있다.
- 새 방향별 corner/shoulder 아트를 제작한 결과가 아니라, 기존 키트와 메시 기법의 조합을 평가한다.

## 검증 도구

- `BackgroundUpgradeLabTests`: 방 연결성, 오목 고리·대각 접촉·지도 경계의 유한성/결정성/변형 한계, 채굴 후 복구의 동일성.
- Editor Play Mode: `BackgroundLabSmoke.Run(lab)` 코루틴은 가상 Input System 장치로 실제 WASD·마우스 입력을 보낸다. 이동, 벽 충돌, 3타 채굴, contour 갱신, 열린 통로 이동, 복구, A/B/C 키 전환을 검증하고 장치를 제거한다.
- 검증 결과와 캡처 경로는 검증 완료 후 아래에 기록한다.

## 검증 결과

2026-09-14, TunnelCrew 6000.3.15f1.

- EditMode: `BackgroundUpgradeLabTests` 5/5 통과. 작업 ID `7992d105fb9141899a22d4bd85badadc`.
- Play 입력 스모크: PASS. 실제 Input System 키보드로 이동 → 벽에 정지 → 마우스 3타 채굴 → contour 재생성 → 열린 칸 통과 → 복구 → 1/2/3 키 전환을 모두 확인했다.
- 전경 검증: 드릴러를 내부 벽 섬 뒤 `(13.5, 12.5)`에 놓았을 때 관련 메시 2개의 알파가 낮아져 캐릭터가 보이는 것을 캡처로 확인했다.
- 최종 Play 콘솔 오류 0건. `BackgroundLabRock` ShaderUtil 메시지 0건.
- 최종 27,160 vertices. 채굴/복구 후 마지막 rebuild 10.07ms(Editor 단일 관측치). 전경 검증 구도의 전체 batches는 181. 생산 환경 성능 보장은 아니다.
- A/B/C 동일 시작점 캡처와 방 전체·전경 페이드 캡처를 `screenshots/background-upgrade-lab/`에 보관했다. 검증 스크린샷은 저장소 정책에 따라 Git에는 넣지 않는다.
- CLI: 새 C# 코드·문서 diff 검사. Unity가 자동으로 기록한 YAML의 빈 값 뒤 공백은 그대로 유지한다.

### 작업 분리

공유 작업 폴더에서 FTUE 작업이 다른 브랜치를 사용했으므로, 별도 Git 인덱스로 이번 작업 파일만 `codex/background-upgrade-lab`에 커밋했다. FTUE·투사체 변경은 이 브랜치에 포함하지 않았다. 본편 씬·빌드 설정을 변경하지 않았다.

실행 도중 발견한 보조 수정 두 가지는 배경 브랜치의 파일 범위와 분리했다. `.codex/hooks/require-unity-instance.ps1`의 UTF-8 표준입력 처리를 보정했으며 인스턴스 검사/거부 규칙은 유지했다. 다른 작업의 `DynamicDialogueText.ValueAfterEquals`에는 미할당 `out` 인자 초기값 한 줄을 추가해 그 작업의 컴파일을 복구했다.
