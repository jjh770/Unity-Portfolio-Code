# SkillSystem

스킬 레벨에 따라 근접 범위 공격, 단일 투사체, 3방향 투사체가 순차적으로 열리는 플레이어 스킬 코드입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`PlayerSkillController.cs`](./PlayerSkillController.cs) | 해금·레벨·쿨타임·상태 전환과 애니메이션 이벤트를 관리합니다. |
| [`PlayerSkillRange.cs`](./PlayerSkillRange.cs) | 근접 범위 판정과 레벨별 투사체 생성 구성을 담당합니다. |
| [`SkillProjectile.cs`](./SkillProjectile.cs) | 설정 구조체를 받아 이동·범위 판정·중복 타격 방지·풀 반환을 수행합니다. |

## 실행 흐름

```text
스킬 입력
→ 사용 가능 상태 및 쿨타임 검사
→ Skill 상태 전환
→ 애니메이션 이벤트
→ 근접 판정 / 투사체 발사
→ 피해 적용
→ 애니메이션 종료 후 Idle 복귀
```

## 설계 포인트

- 한 번의 공격에서 같은 Collider가 여러 번 감지돼도 `HashSet`으로 중복 피해를 막습니다.
- 투사체의 런타임 속성은 `ProjectileConfig`로 한 번에 전달합니다.
- 생성 방식은 풀에 위임하여 발사 로직과 생명주기 관리를 분리합니다.
