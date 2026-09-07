# prototype-html — HTML 프로토타입 아카이브 (동결)

**이 폴더의 HTML 은 더 이상 개발 대상이 아니다.** 본편 개발은 `unity/TunnelCrew/` 의
Unity 프로젝트에서만 진행한다. 여기 있는 파일들은 Unity 포팅의 **원본 레퍼런스**로만 쓴다
(수치·연출·UI 배치·손맛 비교, 자산/튜닝 추출).

## 구성

| 경로 | 내용 |
|---|---|
| `latest/` | 마지막 HTML 본선 버전들. **기준 빌드 = `tunnel-crew-infinite-mode-v7.9.2.html`** (Unity 포팅 동결 기준) |
| `archive/` | 날짜별 과거 버전 백업 (`backup-20260826` · `backup-20260828` · `backup-20260831`) |
| `demos/` | 코어루프·FoW·라이팅·UI 그레이박스 등 실험 데모 |
| `early/` | SeedLoop FoW·트리, Hole-Is-Ours 등 선행 프로토 |

## 실행 방법

HTML 은 자산을 **저장소 루트 기준**(`assets/…`, `tunnel_crew_tile_resources_v1/…`)으로
참조한다. 그래서 반드시 **저장소 루트에서 정적 서버를 띄우고** 하위 경로로 연다.

```bash
node coop/server.mjs
```

- 본선: http://127.0.0.1:5188/prototype-html/latest/tunnel-crew-infinite-mode-v7.9.2.html
- 데모: http://127.0.0.1:5188/prototype-html/demos/tunnel-crew-loop-demo.html

저장소의 서버들(`coop/server.mjs`, `tools/local-preview-server.mjs`, `.claude/serve.mjs`)은
`/prototype-html/<하위>/assets/…` 요청을 못 찾으면 루트에서 다시 찾도록 되어 있어
이 폴더로 옮긴 뒤에도 자산이 그대로 로드된다. 다른 정적 서버(`npx serve` 등)를 쓰면
자산 404 가 날 수 있다.

## 도구 기본 원본

`tools/build-package.mjs` · `tools/build-single-html.mjs` 는 인자가 없으면
`prototype-html/latest/` 의 가장 높은 `vX.Y.Z` 를 고른다.
`tools/unity-export/*` 의 기본 원본도 `prototype-html/latest/…v7.9.2.html` 이다.
