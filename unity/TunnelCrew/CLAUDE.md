# TunnelCrew

## Unity MCP — 호출마다 `unity_instance` 를 붙인다

이 PC 에서는 Unity 에디터 2개(TunnelCrew / SlimeForge)가 상시 병렬로 떠 있다.
Unity MCP 는 앱 전역에 하나뿐이고 **모든 대화창이 그 하나를 공유**한다.
`set_active_instance` 에 의존하지 말 것. 세션 고정값은 앱 전역 공유라 작업 중에도
다른 창의 `set_active_instance` 나 연결 재수립에 의해 조용히 바뀐다
(2026-09-09 실측: SlimeForge 창의 `manage_editor play` 가 TunnelCrew 에디터에서 실행됨.
세션 시작 때 고정 + dataPath 검증을 통과했는데도 발생).

**규칙: 모든 `mcp__unityMCP__*` 호출에 `unity_instance: "TunnelCrew@5795f232"` 를 명시한다.**
per-call 라우팅은 세션 고정값을 건드리지 않고 다른 창이 가로챌 수도 없다.
`.claude/hooks/require-unity-instance.py` (PreToolUse 훅, `.claude/settings.json`) 가 이를 강제한다 —
`unity_instance` 가 없거나 다른 프로젝트를 가리키면 호출을 차단하고 올바른 ID 를 알려준다.
기대 인스턴스는 `~/.unity-mcp/unity-mcp-status-*.json` 의 `project_path` 로 역산한다(해시 하드코딩 아님).

인스턴스 식별값 (2026-09-09): `TunnelCrew@5795f232` (port 6401) / `SlimeForge@3fece294` (port 6400).

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
