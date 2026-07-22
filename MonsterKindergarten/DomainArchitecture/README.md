# DomainArchitecture

저장 형식과 Unity UI에서 분리된 도메인 객체가 게임의 핵심 규칙을 보유하고, Manager가 도메인 간 협력과 Repository 연결을 담당합니다.

## 파일

| 파일 | 역할 |
|---|---|
| `Currency.cs` | 음수 금지, 통일된 표시, 연산과 비교 규칙을 가진 재화 값 객체입니다. |
| `CurrencyManager.cs` | 재화 CRUD, 변경 이벤트와 Repository 저장을 조정합니다. |
| `Upgrade.cs` | 비용·효과·최대 레벨·레벨업 규칙을 가진 업그레이드 도메인입니다. |
| `UpgradeManager.cs` | 업그레이드와 재화 도메인의 협력, 저장과 이벤트를 관리합니다. |
| `SlimeStatus.cs` | 최고 해금 등급과 등급별 활성 개수의 불변 규칙을 관리합니다. |
| `SlimeManager.cs` | 스펙 데이터, 런타임 상태와 Repository를 연결하고 머지 결과를 저장합니다. |

## 계층

```text
Repository: 저장 방식
    ↑
Manager: 도메인 간 협력, 이벤트, Unity 생명주기
    ↓
Domain: 핵심 데이터와 규칙
```

## 설계 포인트

- `Upgrade`는 재화 시스템을 직접 참조하지 않습니다. 두 도메인의 협력은 `UpgradeManager`에서 처리합니다.
- 잘못된 값은 도메인 생성 시점 또는 상태 변경 시점에 즉시 거부합니다.
- 저장용 DTO와 런타임 도메인 객체를 분리해 저장 형식이 게임 규칙을 침범하지 않도록 합니다.
