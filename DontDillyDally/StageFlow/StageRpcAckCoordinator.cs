/*
 * 역할: 스테이지 핵심 RPC 전파 후 클라이언트별 ACK를 추적하고 미응답 대상에 재전송합니다.
 * 핵심 설계: 최초 ACK 타임아웃은 전체 진행을 무기한 차단하지 않으며, 백그라운드 재동기화로 복구를 계속합니다.
 */
using Cysharp.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DontDillyDally.StageFlow
{
    // RPC 전파 후 다른 클라이언트의 ACK를 기다리고, 미응답 대상은 백그라운드에서 재동기화를 이어갑니다.
    public sealed class StageRpcAckCoordinator
    {
        // 백그라운드 RPC 재전송 사이의 대기 시간입니다.
        private readonly int _retryDelayMs;
        // 게임 진행 후 허용할 최대 백그라운드 재동기화 횟수입니다.
        private readonly int _maxRetryCount;

        public StageRpcAckCoordinator(int retryDelayMs, int maxRetryCount = 10)
        {
            _retryDelayMs = retryDelayMs;
            _maxRetryCount = Mathf.Max(0, maxRetryCount);
        }

        // 첫 대기 구간 안에 모든 ACK를 받으면 true를 반환합니다.
        // 타임아웃이 나더라도 false를 반환한 뒤 백그라운드 재전송을 이어갑니다.
        public async UniTask<bool> BroadcastAndWaitAck(
            Action broadcast,
            Action<Action<int>> subscribe,
            Action<Action<int>> unsubscribe,
            string label,
            int timeoutMs,
            CancellationToken ct)
        {
            int otherPlayerCount = PhotonNetwork.PlayerList.Length - 1;
            if (otherPlayerCount <= 0)
            {
                Debug.Log($"[StageFlow]   {label}: 다른 플레이어 없음, ACK 생략");
                broadcast();
                return true;
            }

            HashSet<int> pendingActors = new HashSet<int>();
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (!player.IsLocal)
                {
                    pendingActors.Add(player.ActorNumber);
                }
            }

            UniTaskCompletionSource allAckTcs = new UniTaskCompletionSource();
            bool unsubscribed = false;

            void OnAck(int actorNumber)
            {
                if (!pendingActors.Remove(actorNumber))
                {
                    return;
                }

                Debug.Log($"[StageFlow]   {label} ACK: Actor {actorNumber} (남은 {pendingActors.Count}명)");
                if (pendingActors.Count == 0)
                {
                    allAckTcs.TrySetResult();
                }
            }

            void Cleanup()
            {
                if (unsubscribed)
                {
                    return;
                }

                unsubscribed = true;
                unsubscribe(OnAck);
            }

            async UniTaskVoid ContinueRetryAsync()
            {
                try
                {
                    for (int retryCount = 1; retryCount <= _maxRetryCount && pendingActors.Count > 0; retryCount++)
                    {
                        await UniTask.Delay(_retryDelayMs, cancellationToken: ct);
                        broadcast();

                        bool completed = await UniTask.WhenAny(
                            allAckTcs.Task,
                            UniTask.Delay(timeoutMs, cancellationToken: ct)) == 0;

                        if (completed)
                        {
                            Debug.Log($"[StageFlow]   {label}: 백그라운드 재동기화 완료");
                            return;
                        }

                        Debug.LogWarning($"[StageFlow]   {label}: ACK 재시도 진행 중 ({retryCount}/{_maxRetryCount}) - 미응답 Actor: {string.Join(", ", pendingActors)}");
                    }

                    if (pendingActors.Count > 0)
                    {
                        Debug.LogError($"[StageFlow]   {label}: ACK 재시도 종료 ({pendingActors.Count}명 미응답) - 미응답 Actor: {string.Join(", ", pendingActors)}");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    Cleanup();
                }
            }

            subscribe(OnAck);

            try
            {
                broadcast();

                bool completed = await UniTask.WhenAny(
                    allAckTcs.Task,
                    UniTask.Delay(timeoutMs, cancellationToken: ct)) == 0;

                if (completed)
                {
                    Debug.Log($"[StageFlow]   {label}: 모든 클라이언트 확인 완료");
                    Cleanup();
                    return true;
                }

                if (_maxRetryCount <= 0)
                {
                    Debug.LogError($"[StageFlow]   {label}: ACK 타임아웃 ({pendingActors.Count}명 미응답) - 재시도 비활성화. 미응답 Actor: {string.Join(", ", pendingActors)}");
                    Cleanup();
                    return false;
                }

                Debug.LogWarning($"[StageFlow]   {label}: ACK 타임아웃 ({pendingActors.Count}명 미응답) - 게임은 계속 진행하며 백그라운드 재동기화를 시작합니다. 미응답 Actor: {string.Join(", ", pendingActors)}");
                ContinueRetryAsync().Forget();
                return false;
            }
            catch
            {
                Cleanup();
                throw;
            }
        }
    }
}
