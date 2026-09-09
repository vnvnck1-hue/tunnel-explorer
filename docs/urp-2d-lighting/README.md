# URP 2D 라이팅 기법 정리 (Unity at GDC 2023)

원본: **"Enhance a top-down game with URP 2D lights | Unity at GDC 2023"**
<https://www.youtube.com/watch?v=YhrwKF_i-BI> · Unity 공식 채널 · 20:28 · 2023-05-20
발표: Matt Dondelinger (Developer Advocate, Unity)
연계 e-book: <https://on.unity.com/42VbUzE> (*2D game art, animation, and lighting for artists*)

톱다운 2D 샘플 게임(현 `Happy Harvest`)에서 실제로 쓴 URP 2D 라이팅/섀도우 기법을 슬라이드 단위로 훑는 세션이다.
이 폴더는 그 세션에서 나온 **모든 기법**을 항목별로 쪼개 정리한 것이고, 땅굴 크루 Unity 포팅의 라이팅 환경을 세팅할 때 기준 문서로 쓴다.

## 문서 구성

| 파일 | 내용 | 원본 구간 |
| --- | --- | --- |
| [01-project-setup.md](01-project-setup.md) | 2D (URP) 템플릿, Renderer 2D Data, Blend Style 슬롯 설계 | 2:05–4:10 |
| [02-normal-maps.md](02-normal-maps.md) | 2D 노멀맵 5가지 제작법, 3각도 핸드페인팅, 임포트 설정 | 4:10–6:58 |
| [03-mask-maps.md](03-mask-maps.md) | 마스크맵 원리, 채널 분리(캐릭터 G / 소품 R), 림라이트 | 6:58–10:30 |
| [04-lights.md](04-lights.md) | 앰비언트 글로벌 라이트, 2D "태양광", 노멀맵으로 입체감 | 11:10–13:02 |
| [05-shadows.md](05-shadows.md) | 네거티브 라이팅 그림자, 블롭 섀도우, 무한/유한 투영, 건물 실루엣 보간 | 13:02–16:45 |
| [06-day-night-cycle.md](06-day-night-cycle.md) | DayCycleHandler로 전부 오케스트레이션 | 17:38–18:41 |
| [07-performance.md](07-performance.md) | 성능 6대 규칙 + Renderer 2D 수치 | 18:41–19:20 |
| [08-new-features-and-unity6-delta.md](08-new-features-and-unity6-delta.md) | 발표 당시 신기능 + Unity 6 / URP 17.3 기준 차이 | 19:20–20:28 |
| [09-tunnel-crew-application.md](09-tunnel-crew-application.md) | 땅굴 크루 프로젝트에 어떻게 적용할지 (현재 설정 진단 포함) | — |
| [source-notes.md](source-notes.md) | 원본 챕터 목록, 캡처 방식, 신뢰도 표기 | — |

## 한 장 요약 — 세션의 핵심 주장 6개

1. **2D 라이팅은 "빛을 더하는 것"만이 아니다.** Blend Style(Multiply / Additive / Subtractive)과 Overlap Operation을 조합하면 같은 Light 2D 컴포넌트로 그림자·마스크·림라이트·틴트를 전부 만든다.
2. **노멀맵이 톱다운 2D의 입체감 90%를 만든다.** 스프라이트 Secondary Texture 로 붙이고, 라이트마다 Normal Maps Quality를 켜야 실제로 반응한다.
3. **마스크맵은 "이 라이트가 어디에 영향을 줄지"를 픽셀 단위로 고르는 스위치다.** 채널을 나눠 캐릭터용/소품용 라이트를 완전히 분리한다.
4. **그림자는 대부분 진짜 섀도우 캐스터가 아니라 어두운 라이트다.** Multiply 블렌드 + Alpha Blend overlap 으로 "네거티브 라이팅"을 만든다.
5. **투영 그림자는 상황별로 4가지 해법을 쓴다.** 무한 투영(ShadowCaster2D) / 스크립트로 늘리는 블롭 / freeform 라이트 프레임 보간 / 단순 블롭.
6. **전부 하나의 시간축(day/night)에 묶는다.** 태양·달 스팟 라이트를 카메라에 붙이고, 그라디언트와 커브로 색·각도·길이를 시간비율(ratio)로 평가한다.

> 문서 안의 인스펙터 수치는 발표 슬라이드에서 읽은 **샘플 게임의 실제 값**이다. 우리 프로젝트에 그대로 넣을 값이 아니라 "이 정도 스케일"의 기준점으로 본다.
