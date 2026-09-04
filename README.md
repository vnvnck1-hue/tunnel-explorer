# Tunnel Explorer / 땅굴 크루

탑다운 2D 땅굴 탐험·채굴 웹 프로토타입과 **땅굴 크루** 코어루프 데모, 기획서 모음입니다.

## 빠른 실행

로컬에서 HTML을 정적 서빙한 뒤 브라우저로 엽니다.

```bash
# 예: Node
npx --yes serve . -p 5188
```

- 코어루프 데모: http://127.0.0.1:5188/demos/tunnel-crew-loop-demo.html  
- 파라미터 튜닝 데모: http://127.0.0.1:5188/demos/tunnel-explorer-demo.html  

또는 파일을 브라우저에 직접 열어도 됩니다 (일부 환경에서는 `file://` 제약이 있을 수 있음).

### LAN 코옵 (집 Wi‑Fi · Windows + Mac)

```bash
cd coop
npm install
npm start
```

호스트 PC에 표시되는 **LAN 주소**를 맥에서 열고, 방 코드로 참가합니다. 자세한 절차는 [`coop/README.md`](coop/README.md).

### AI 크루 (플레이어 AI)

직업 선택 화면에서 카드 옆 **`+ AI`** 를 눌러 AI 동료를 편성합니다. 서버도 코옵도 필요 없습니다.

```bash
python ai/inject-ai-crew.py
```

최신 게임 HTML에 AI를 주입한 `...-ai.html` 이 만들어집니다. 자세한 내용은 [`ai/README.md`](ai/README.md).

## 구성

| 경로 | 설명 |
|------|------|
| `demos/tunnel-crew-loop-demo.html` | **메인** — 역할 선택 → 채취 → 탈출 코어루프. 드릴러에 마이너 스프라이트 적용 |
| `demos/tunnel-explorer-demo.html` | FoW / LOS / 드릴·카메라 파라미터 튜닝 데모 |
| `demos/tunnel-explorer-demo-sprite*.html` | 캐릭터 스프라이트 시트 실험 (좌향 수정본 포함) |
| `demos/tunnel-explorer.html` | 탐험가 본체 HTML 변형 |
| `docs/tunnel-crew-gdd.md` | 땅굴 크루 게임 확장 기획서 (GDD v0.1) |
| `docs/tunnel-crew-main-game-structure.md` | **본편 누적 기준안** — 행성 원정, 런 성장, 영구 성장, 코옵 경제와 미결정 사항 |
| `docs/unity-port-plan.md` | **Unity 포팅 계획** — 기준 빌드 v7.9.2 동결, 3층 아키텍처, 추출 파이프라인, 마일스톤 M0~M8 |
| `docs/unity-port/` | 포팅 근거 코드 분석 3편 — 코어 시뮬레이션 / 엔티티·전투·AI / 셸·오디오·네트·자산 |
| `prototypes/` | SeedLoop FoW·트리, Hole-Is-Ours 등 선행 프로토 |

## 코어루프 데모 조작

- **WASD** 이동 · **마우스** 조준 · **클릭 홀드** 드릴/사격  
- **Space** 대시 · **F** 손전등 · **Q / E** 역할 스킬  
- 목표 광물 채취 후 **입구로 복귀**해 탈출  

역할: 드릴러 / 스카웃 / 엔지니어 / 거너  

## 라이선스

프로토타입·개인 작업용. 에셋·코드 재배포 정책은 추후 정리 예정입니다.
