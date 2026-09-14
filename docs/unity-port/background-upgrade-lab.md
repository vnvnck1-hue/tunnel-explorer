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
# 본편 적용 (2026-09-14)

`RunBootstrap._organicEnvironment` 기본값을 켰다. 본편 `Run.unity`에서 출격하면
`EnvironmentChunkRenderer`가 `OrganicEnvironmentRenderer`를 만든다. HTML 프로토타입과 게임 판정 코드는 변경하지 않았다.

- 셀 충돌/채굴은 기존 Sim 격자 그대로. 표시 외곽만 최대 0.18셀 이동한다.
- 동일 월드 좌표와 인접 4셀에서 외곽점을 계산하므로 구역 경계를 독립 갱신해도 같은 정점을 만든다.
- 벽 상단·정면·테두리·접촉 그림자와 드문 암석 어깨 장식을 추가했다. 지층별 기존 암석 채널을 월드 좌표로 샘플링한다.
- 광맥·균열 오버레이와 보스 벽 전용 타일은 유지한다. 전경 메시/장식 알파는 기존 오클루더를 따른다.
- 기존 16×16 dirty 구역을 재사용한다. 유기적 메시 모드에서는 프레임당 최대 2구역, `FlushDirty`는 즉시 전부 처리한다.
- `Resources/Visual/OrganicEnvironmentStyle.asset`이 셰이더/장식을 참조하여 플레이어 빌드 포함 경로를 확보한다. 자산 또는 셰이더가 없으면 타일 벽으로 폴백한다.
- 랩의 고정 배치(수정방/배관방/레일), 고정 조명, 안개 카드, CRT 해제는 본편에 복사하지 않았다. 본편 바닥·시야 어둠·조명·CRT는 그대로다.

## 본편 검증

- Unity `6000.3.15f1`, `TunnelCrew@5795f232`의 Play Mode에서 확인.
- EditMode: `OrganicWallSamplingTests`, `BackgroundUpgradeLabTests`, `SurfaceTopologyTests`, `DepthSortTests` **36/36 통과**. job `473a7d54612a4995b4181eeb53a1d517`.
- 실제 본편 `WorldGrid.Damage` 이벤트 경로: (31,45) 손상 단계 2 → 파괴 → 표면 제거 → 타일 복구 확인. 전체 25구역 중 2구역만 재생성. 입력 장치 클릭 자동화가 아니라 실제 월드 이벤트 통합 검사다.
- 보스 집합 등록 + `SetTile` 경로에서 BossWall 표면 플래그와 전용 cap 타일 유지 확인.
- 깊이 1/2/3/4의 `EnterDepth` + 본편 `RebindWorld`로 각 키트/아틀라스 교체 및 화면 확인. 하강 버튼/보스 진행 전체를 자동화한 검사는 아니다.
- 전경 뒤에 플레이어를 배치했을 때 메시 알파 0.34, 캐릭터 가시성 확인. 반복 지층 교체 뒤 유기적 렌더러는 1개.
- 관측값: 80×72맵, 25구역, 약 151,216정점. 단일 구역 재생성 한 표본 4.71ms, 전경 화면 117 batches. Editor 관측이며 배포 빌드 FPS 보장은 아니다.
- 새 셰이더 메시지 0, 최종 본편 Console 오류 0. 캡처 도구 사용 중 기존 AudioListener 경고는 있었으며 본 작업에서 오디오 설정은 변경하지 않았다.
- 스크린샷: `unity/TunnelCrew/Captures/BackgroundMain/`의 `A-main-tiles.png`, `C-main-organic.png`, `stratum-2.png`, `stratum-3.png`, `abyss.png`, `foreground-fade.png` (Git 제외).
- CLI `git diff --check` 통과. 독립 플레이어 빌드는 이번 검증 범위에 포함하지 않았다.

## 비교/되돌리기

Run 씬의 RunBootstrap 인스펙터에서 **Organic Environment**를 끄고 Play Mode를 다시 시작하면 기존 타일 벽이다. 디버그 숫자키 1/2/3은 본편에서 직업 전환이므로 랩의 비교키를 이식하지 않았다.
