# 원본 정보 · 캡처 방식 · 신뢰도

## 원본

- 제목: **Enhance a top-down game with URP 2D lights | Unity at GDC 2023**
- URL: <https://www.youtube.com/watch?v=YhrwKF_i-BI>
- 채널: Unity (공식) · 업로드 2023-05-20 · 길이 20:28
- 발표자: **Matt Dondelinger**, Developer Advocate (Unity)
- 연계 e-book: <https://on.unity.com/42VbUzE> — *2D game art, animation, and lighting for artists*
- 소재: 당시 미공개였던 2D 샘플 게임(현 `Happy Harvest`). 다른 참고 데모로 **Lost Crypt**, **Dragon Crashers** 를 언급 (둘 다 Unity Asset Store 무료)

## 원본 챕터 목록 (동영상 설명란 원문)

```
0:00  Intro
1:16  2D e-book
2:05  Unity's 2D URP settings
4:10  Different ways to create Normal Maps for 2D
6:58  What are Mask maps in 2D
10:30 Performance/art/gameplay considerations
11:10 Creating ambient lighting
12:00 Creating a sunlight
12:29 Enabling depth effect with Normal Maps
13:02 Creating shadows with negative lighting
13:38 Creating blob shadows
14:13 Creating infinite shadow projection
14:48 Creating finite shadow projection
15:42 Creating shadows for buildings
16:45 More light and shadow possibilities
17:38 Day/night cycle
18:41 Notes on performance
19:20 New features for lights and shadows
```

이 챕터 18개 전부가 이 폴더의 문서에 매핑되어 있다 (README의 매핑 표 참조).

## 캡처 방식과 한계 (정직한 기록)

- **자동 생성 자막(transcript)을 프로그램으로 받아내지 못했다.** YouTube가 봇 방지로 `timedtext` / `get_transcript` 요청을 막아, 자막 패널이 비어 있는 상태로만 열렸다. 자막 서비스 사이트는 CAPTCHA 가 걸려 있어 우회하지 않았다.
- 대신 **영상을 브라우저에서 챕터·슬라이드 단위로 seek 하며 화면 캡처**해서, 슬라이드 본문·인스펙터 값·코드 스니펫을 직접 읽어 옮겼다. 캡처 시 화면에 뜬 자막 한두 줄도 함께 읽었다 (문서 안 인용의 출처).
- 확인한 슬라이드: 6, 9, 12, 13, 15, 16, 17, 19, 20, 21, 22, 23, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 39, 40, 42, 43 (총 43장 중 29장). 놓친 슬라이드는 대체로 앞 슬라이드의 반복이나 전환 장면이다.
- 문서에서 **`> *"…"*` 인용은 슬라이드 본문을 그대로 읽은 것**이고, 인스펙터 수치는 슬라이드 스크린샷에서 읽은 값이다. 작은 글씨 숫자(예: Falloff 0.494, Normalized Time 0.16)는 **±판독 오차가 있을 수 있다** — 값 자체보다 "이 정도 스케일"로 본다.
- 슬라이드 33의 `UpdateShadow` 코드는 캡처를 옮긴 재구성이다. **동작하는 코드가 아니라 설계 의도를 읽기 위한 것**이므로 그대로 복사해 쓰지 않는다.
- [08-new-features-and-unity6-delta.md](08-new-features-and-unity6-delta.md) 의 §B(Unity 6 차이)는 영상이 아니라 **우리 프로젝트의 URP 17.3 패키지 소스를 직접 읽어** 작성했다. enum 값과 프로퍼티 이름은 실측이다.
- [09-tunnel-crew-application.md](09-tunnel-crew-application.md) 의 현재 상태 진단은 `unity/TunnelCrew` 의 `Renderer2D.asset`, 스프라이트 `.meta`, `Assets/_Project` 코드를 직접 확인한 결과다.

## 자막 전문이 필요하면

로그인된 브라우저에서 영상 페이지 → 설명란 펼치기 → **"스크립트 표시"** 를 눌러 직접 읽는 편이 빠르다. (자동화로는 막혀 있다.)
