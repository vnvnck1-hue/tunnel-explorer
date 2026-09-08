# TunnelCrew

## Unity MCP — 작업 전 반드시 인스턴스 고정

이 PC 에서는 Unity 에디터 2개(TunnelCrew / SlimeForge)가 상시 병렬로 떠 있다.
Unity MCP 는 앱 전역에 하나뿐이고 **모든 대화창이 그 하나를 공유**한다.
새 대화는 고정값이 비어 있어서 "가장 마지막에 켠 에디터"로 조용히 라우팅된다.
대화창을 프로젝트별로 나눠도 이건 갈라지지 않는다.

**따라서 이 프로젝트에서 Unity MCP 도구를 처음 쓰기 전에 반드시:**

1. 고정한다 — `set_active_instance("5795f232")` (= 정션 경로로 연 TunnelCrew)
2. **검증한다** — `execute_code` 로 `UnityEngine.Application.dataPath` 를 찍어
   `C:/Users/Loadcomplete/TunnelCrew/Assets` 인지 확인한다. 다르면 중단하고 보고할 것.

주의:
- **여는 경로에 따라 해시가 달라진다.** 정션(`C:\Users\Loadcomplete\TunnelCrew`)으로 열면
  `5795f232`, 실제 경로로 열면 `2105473d` 가 된다. 반드시 정션으로 열 것
  (한글 경로 cp949 문제로 실제 경로로 열면 MCP 가 인식하지 못한다).
- **포트 번호로 지정하지 말 것.** 재시작마다 바뀐다. 해시는 안 바뀐다.
- `~/.unity-mcp/unity-mcp-status-*.json` 의 `"ready"` 를 믿지 말 것.
  에디터가 죽어도 1시간 넘게 그대로 남아 있었다.

## 에디터 버전

**Unity 6000.3.15f1** — SlimeForge(6000.0.69f1)와 다르다. 절대 섞지 말 것.
`C:\Users\Loadcomplete\Desktop\open-tunnelcrew.bat` 로만 연다(로그 분리 포함).

## 참고 문서

`docs/unity-dual-editor-build-stability.md` — 에디터 2개 동시 실행 시 빌드 백엔드 중단 대응.
