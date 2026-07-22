# StageFlow

컷씬에서 Gameplay 씬으로 넘어가는 동안 런타임 데이터를 준비하고, 모든 클라이언트가 핵심 RPC를 수신했는지 확인한 뒤 스테이지를 초기화하는 코드입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`StagePreloader.cs`](./StagePreloader.cs) | 역할 배정과 환자·질병 데이터 생성을 컷씬 중 선행 처리하고 완료 상태를 노출합니다. |
| [`StageFlowBootstrapper.cs`](./StageFlowBootstrapper.cs) | 씬 전환 사이에서 데이터를 보존하고 Gameplay의 `StageFlowManager` 준비를 기다린 뒤 초기화합니다. |
| [`StageRpcAckCoordinator.cs`](./StageRpcAckCoordinator.cs) | RPC 전파 후 클라이언트별 ACK를 추적하고, 최초 타임아웃 이후에도 백그라운드 재전송을 수행합니다. |

## 실행 흐름

```text
Cutscene 진입
→ StagePreloader 초기화
→ 역할 배정 및 환자 데이터 병렬 준비
→ Gameplay 씬 전환
→ 마스터가 스테이지 네트워크 프리팹 생성
→ DataPrep 및 StageFlowManager 준비 대기
→ StageFlowManager 초기화
→ StageFlowReady 이벤트 발행
```

## 설계 포인트

- 컷씬 재생 시간과 외부 데이터 생성 시간이 다르므로, 씬 전환과 데이터 준비를 분리해 기다립니다.
- Unity 씬 이벤트와 프로젝트 씬 로더 이벤트를 함께 사용해 비마스터의 WaitingRoom 복귀에서도 정리를 보장합니다.
- ACK 최초 대기가 실패해도 전체 진행을 무기한 막지 않고, 게임은 진행하면서 미응답 대상에 재동기화를 시도합니다.
