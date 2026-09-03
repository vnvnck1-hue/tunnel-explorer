# 땅굴 크루 프로젝트 작업 지침

- 사용자가 지정한 파일을 기준으로 작업하고, 별도 버전 파일을 만들지 않고 해당 단일 파일을 계속 수정한다.
- 백업 파일은 사용자가 명시적으로 요청한 경우에만 만든다.
- 기능을 추가하거나 수정한 뒤에는 가능하면 로컬 브라우저에서 핵심 흐름을 확인하고, 변경 파일과 검증 결과를 작업 인계에 남긴다.

## 빌드 지침

### 용어 — 사용자가 어떤 빌드를 말하는지

| 사용자 표현 | 뜻 | 명령 | 결과물 |
|---|---|---|---|
| "빌드해줘" (수식어 없음) | **스탠드얼론 배포 패키지** — 모든 컨텐츠 포함 | `node tools/build-package.mjs` | `build/TunnelCrew-vX.Y.Z/` + 동명 `.zip` |
| "단일 빌드" · "단일 파일로" · "html 하나로" | **HTML 단일 파일** — 자산 전부 인라인 | `node tools/build-single-html.mjs` | `build/tunnel-crew-infinite-mode-vX.Y.Z-single.html` |

- 원본은 사용자가 지정한 HTML. 지정이 없으면 두 빌더 모두 프로젝트 루트의 **가장 높은 버전** `tunnel-crew-infinite-mode-vX.Y.Z.html` 을 자동으로 고른다. 다른 파일을 원본으로 쓰려면 첫 인자로 경로를 준다.
- 두 빌드는 본선 HTML 을 수정하지 않는다. 결과물만 `build/` 에 생긴다.

### 스탠드얼론 패키지 ("빌드")

- `tools/build-package.mjs` 가 만든다: 게임 HTML · `assets/`(psd 제외, 컷씬은 참조 시만) · `tunnel_crew_tile_resources_v1/` · `monster_assets_v1.5.4/frames/` · `coop/`(server.mjs 의 `GAME_HTML` 을 새 파일명으로 교체, `saves/` 비움) · `node/node.exe`(이전 패키지에서 복사) · `START.bat`(버전 배너 자동) · `실행안내.md`.
- 같은 버전 폴더가 이미 있으면 멈춘다. 다시 만들 때는 `--force`.
- zip 은 스크립트가 **bsdtar(`C:\Windows\System32\tar.exe -a -cf`)** 로 만든다. PowerShell `Compress-Archive` 는 한글 파일명과 경로 구분자를 깨뜨리므로 절대 쓰지 않는다.
- 빌드 후 스크립트의 `[누락]` 이 비어 있어야 한다(몬스터 frames · ambience · bgm · 타일 바이옴이 필수 검사 대상).
- 검증: `.claude/launch.json` 의 `dist-server` 를 새 패키지로 돌려(포트 5288) 브라우저에서 타이틀 → 미션 진입까지 확인하고 404 · 콘솔 에러가 0 인지 본다. 개발용 코옵 서버(5188)가 떠 있으면 START.bat 이 다른 포트로 비켜가므로 검증은 항상 dist-server 로 한다.

### HTML 단일 파일 ("단일 빌드")

- `tools/build-single-html.mjs` 가 만든다. 참조 자산을 data URI 로 인라인하고 `<meta charset>` 직후에 자산 맵 + shim(Image/Media `src` · `setAttribute` · `innerHTML` · CSS `url()` · `fetch` · XHR · `Audio` 가로채기)을 넣는다. 더블클릭(file://)으로 바로 실행된다.
- **결과물은 100MB 이하여야 한다.** 빌더가 마지막 줄에 `용량 : NN MB ≤ 100 MB ✓` 를 찍는다. 초과하면 아래 다이어트가 제대로 돌았는지 먼저 본다. 이를 위해 단일 빌드에는 항상 두 단계가 들어간다(본선·원본 자산은 건드리지 않고 결과물 안에서만 적용):
  1. **중복 제거** — 마크업의 `src="assets/…"` 를 data URI 로 직접 치환하지 않고 `data-tc-src` 로 바꿔 두면, shim 의 MutationObserver 가 요소가 파싱되는 즉시 자산 맵에서 채운다. 같은 파일이 맵과 마크업에 두 번 들어가던 것(키아트 5MB 등 11MB) 을 없앤다.
  2. **PNG → 무손실 WebP** — `tools/webp-lossless.py`(Pillow) 로 변환해 파일마다 PNG 와 WebP 중 작은 쪽만 인라인한다. 픽셀은 동일하고 맵 키(경로)는 원래 `.png` 그대로라 게임 코드는 영향이 없다. 변환 결과는 `build/.single-cache/` 에 캐시된다. 손실 압축·해상도 축소는 하지 않는다(필요해지면 옵션으로 추가). Pillow 가 없으면 경고 후 PNG 로 빌드되므로 리포트의 `WebP :` 줄을 확인한다. `--no-webp` 로 끌 수 있다.
- **서버가 필요한 컨텐츠는 빼되, 메인 메뉴에서 잠금(locked · disabled) 상태로 남긴다.** 현재는 LAN 코옵 하나다. 버튼은 `modeBtn locked` + `lockedTag "단일 파일 미지원"` 으로 표시되고 `/coop/client.js` 태그는 제거된다. 코옵처럼 서버가 필요한 기능이 새로 생기면 같은 방식으로 잠근다.
- 새 자산 경로가 확장자 없는 루트 상수로 조립되면(`TILE_RESOURCE_ROOT` 처럼) 토큰 스캔에 잡히지 않는다. 그런 자산이 추가되면 빌더의 동적 폴더 규칙에 넣는다.
- 검증: 단일 HTML **하나만** 들어 있는 격리 폴더를 정적 서버(`npx http-server`)로 띄워 열고, 게임 진입 후 `performance.getEntriesByType('resource')` 에 `data:` 가 아닌 항목이 0 개인지 본다(외부 요청이 하나라도 있으면 인라인 누락). 리포트의 `[치환 실패]` 도 비어 있어야 한다.

### Windows 실행 파일

- Windows 게임 실행 파일(`.exe`)을 빌드할 때는 반드시 `assets/app-icon-dragon.ico`를 실행 파일 아이콘으로 사용한다.
- 빌드 도구의 아이콘 설정은 저장소 원본 경로의 위 파일을 가리키도록 유지하고, 별도의 임시 아이콘이나 기본 아이콘으로 대체하지 않는다.
- 빌드 완료 후 Windows 탐색기와 작업 표시줄에서 용 보스 얼굴 아이콘이 정상적으로 표시되는지 확인하고, 검증 결과를 작업 인계에 남긴다.
