# 엔지니어 캐릭터 + 장비 레이어 구조

## 노드 구조

- EngineerRoot: 월드 위치, 이동, 8방향 판정 담당
  - EquipmentBack: NW/N/NE 방향 장비 렌더링
  - BodySprite: 128x128 캐릭터 본체
  - EquipmentFront: E/SE/S/SW/W 방향 장비 렌더링

## 피벗

- BodySprite: 로컬 (64, 110), Unity normalized (0.5, 0.140625)
- Equipment: 로컬 (96, 150), Unity normalized (0.5, 0.21875)
- 두 피벗은 EngineerRoot의 같은 월드 좌표에 배치합니다.

## 재생 규칙

- 이동 중 BodySprite만 idle/walk를 재생합니다.
- 플랫폼 사용 시 BodySprite는 idle을 유지하고 Equipment 레이어가 deploy 01~04를 11fps로 재생합니다.
- 방향이 바뀌면 두 레이어의 방향을 같은 프레임에서 함께 갱신합니다.
- 장비가 비활성일 때 EquipmentBack/Front를 숨깁니다.
