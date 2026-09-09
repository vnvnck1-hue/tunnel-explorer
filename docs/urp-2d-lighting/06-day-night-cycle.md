# 06. 하루 주기(day/night cycle)로 전부 묶기

원본 구간 17:38–18:41 (슬라이드 38~40) · 챕터 *"Day/night cycle"*

슬라이드 38: **"Bringing it all together in a day/night cycle"**
슬라이드 39: **"Orchestrating everything"**

> *"Just like we did with the sun, we simulate moonlight with another big spot light. Both will change color and move following the color gradient and parameters of the script."*

## 구조

```
Main Camera
└── DayCycleHandler          ← Day Cycle Handler (Script)
    ├── DayLight   (Light 2D, 거대 Spot = 태양)
    └── NightLight (Light 2D, 거대 Spot = 달)
```

- **태양과 달을 같은 방식으로 만든다.** 둘 다 카메라 자식의 거대한 Spot 라이트 ([04-lights.md](04-lights.md) §2와 동일 트릭).
- 둘의 **색은 그라디언트에서, 움직임은 스크립트에서** 나온다.
- 슬라이드의 씬 뷰 두 컷: 낮에는 태양 라이트 원이 위쪽, 밤에는 달 라이트 원이 위쪽에 걸린다.

## Day Cycle Handler (Script) — 노출된 필드

| 필드 | 값 / 타입 | 역할 |
| --- | --- | --- |
| Lights Root | Transform (`DayCycleHandler`) | 회전시킬 부모 |
| **Day Light** | Light 2D (`DayLight`) | 태양 라이트 참조 |
| **Day Light Gradient** | Gradient | 시간에 따른 태양 색 (주황→흰→주황) |
| **Night Light** | Light 2D (`NightLight`) | 달 라이트 참조 |
| **Night Light Gradient** | Gradient | 시간에 따른 달 색 (청록 계열) |
| **Day Duration In Second** | `120` | 하루 = 실제 120초 |
| **Starting Time** | `30` | 시작 시각 |
| **Sun Height** | `0.6` | 태양 고도. 그림자 방향 계산에 들어감 |
| **Shadow Angle** | AnimationCurve | 시간→그림자 각도 |
| **Shadow Length** | AnimationCurve | 시간→그림자 길이 |
| **Test Time** | 슬라이더 | 에디터에서 시간대 스크럽 |

## 설계상 배울 점

1. **단일 시간 소스.** `ratio = (현재시각 / DayDuration)` 하나가 라이트 색, 라이트 각도, 그림자 각도, 그림자 길이, 셰이더 전역 `_LightDirection` 을 전부 구동한다. → [05-shadows.md](05-shadows.md) §4의 `UpdateShadow(float ratio)` 가 정확히 이 ratio를 받는다.
2. **색은 Gradient, 기하는 AnimationCurve.** 프로그래머가 수식으로 만드는 게 아니라 **아티스트가 에디터에서 그린다.** 낮/밤 무드를 코드 수정 없이 튜닝할 수 있다.
3. **`Test Time` 같은 에디터 스크럽 필드를 반드시 넣는다.** 하루가 120초라도, 검증할 때 원하는 시각으로 즉시 점프해야 한다. (슬라이드 36의 Light Interpolator에도 `Preview Time` 이 있다 — 같은 사상)
4. **하루 = 120초.** 게임플레이 리듬에 맞춘 값이다. 리얼타임 24시간을 그대로 따라가지 않는다.
5. 앰비언트 Global 라이트([04-lights.md](04-lights.md) §1)의 색도 이 사이클에 태운다 — 슬라이드 40 *"Overview of the effect"* 에서 밤 장면의 창문·가로등만 밝고 나머지는 짙은 청색으로 깔린다.

슬라이드 40 결과 화면에 대한 발표자 코멘트: *"lights kicking on it's really nice too"* — 밤이 되면 가로등·창문 라이트가 켜지는 연출까지 같은 사이클에서 트리거한다.
