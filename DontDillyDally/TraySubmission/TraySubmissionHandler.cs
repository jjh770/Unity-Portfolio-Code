/*
 * 역할: 트레이 제출 요청의 생성, 마스터 권위 검증, 응답 매칭과 타임아웃 복구를 담당합니다.
 * 핵심 설계: 요청마다 sequenceId를 부여하고 마스터의 실제 TrayItem 상태를 다시 읽어 지연 응답과 클라이언트 위조 상태를 방지합니다.
 */
using Cysharp.Threading.Tasks;
using DontDillyDally.Data;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Threading;
using UnityEngine;

namespace DontDillyDally.StageFlow
{
    /// <summary>
    /// 트레이 제출의 비동기 대기, 네트워크 분기, 트레이 리셋 동기화를 담당합니다.
    /// MonoBehaviour가 아닌 일반 C# 클래스입니다.
    /// </summary>
    public class TraySubmissionHandler : IDisposable
    {
        private const int SubmissionResponseTimeoutMs = 5000;

        private readonly StageFlowRpcHandler _rpc;
        private readonly Func<bool> _isGameOverCheck;

        private UniTaskCompletionSource<SubmittedTray> _traySubmissionTcs;
        // 현재 응답을 기다리는 트레이의 Photon ViewID입니다.
        private int _pendingSubmissionTrayViewId = -1;
        // 지연된 이전 응답을 구분하기 위한 현재 요청 식별자입니다.
        private int _pendingSubmissionSequenceId = -1;
        private int _nextSequenceId;
        private Action _onPendingSubmissionAccepted;
        private Action _onPendingSubmissionRejected;
        // 응답 제한 시간이 갱신될 때 이전 타임아웃 작업을 취소합니다.
        private CancellationTokenSource _pendingSubmissionTimeoutCts;

        /// <summary>가장 최근 트레이를 제출한 플레이어의 ActorNumber</summary>
        public int LastSubmitterActorNumber { get; private set; } = -1;

        public TraySubmissionHandler(StageFlowRpcHandler rpc, Func<bool> isGameOverCheck)
        {
            _rpc = rpc;
            _isGameOverCheck = isGameOverCheck;

            _rpc.OnTraySubmissionRequestedReceived += HandleTraySubmissionRequestedReceived;
            _rpc.OnTraySubmissionResponseReceived += HandleTraySubmissionResponseReceived;
        }

        public bool CanSubmit =>
            !_isGameOverCheck() &&
            _rpc != null &&
            _rpc.CurrentPhase.Value == EStagePhase.Playing;

        public bool IsWaitingForSubmission => _traySubmissionTcs != null;

        // ── 비동기 대기 ───────────────────────────────────────────────

        public async UniTask<SubmittedTray> WaitForSubmission(CancellationToken ct)
        {
            _traySubmissionTcs?.TrySetCanceled();
            _traySubmissionTcs = new UniTaskCompletionSource<SubmittedTray>();

            using (ct.Register(() => _traySubmissionTcs.TrySetCanceled()))
            {
                return await _traySubmissionTcs.Task;
            }
        }

        // ── 제출 요청 (외부 → 마스터) ─────────────────────────────────

        public bool RequestSubmission(TrayItem trayItem, Action onAccepted, Action onRejected = null)
        {
            if (trayItem == null)
            {
                Debug.LogWarning("[StageFlow] 제출할 트레이가 없습니다.");
                return false;
            }

            if (!CanSubmit)
            {
                Debug.LogWarning("[StageFlow] 지금은 트레이를 제출할 수 없는 상태입니다.");
                return false;
            }

            int trayViewId = trayItem.ViewId;
            if (trayViewId < 0)
            {
                Debug.LogWarning("[StageFlow] 제출할 트레이 ViewId가 올바르지 않습니다.");
                return false;
            }

            if (_pendingSubmissionTrayViewId >= 0)
            {
                Debug.LogWarning("[StageFlow] 이미 처리 중인 트레이 제출 요청이 있습니다.");
                return false;
            }

            int sequenceId = _nextSequenceId++;

            if (PhotonNetwork.IsMasterClient)
            {
                bool accepted = TryAcceptAuthoritative(
                    trayViewId,
                    sequenceId,
                    PhotonNetwork.LocalPlayer?.ActorNumber ?? -1,
                    PhotonNetwork.LocalPlayer);

                if (accepted)
                {
                    onAccepted?.Invoke();
                }
                else
                {
                    onRejected?.Invoke();
                }

                return accepted;
            }

            _pendingSubmissionTrayViewId = trayViewId;
            _pendingSubmissionSequenceId = sequenceId;
            _onPendingSubmissionAccepted = onAccepted;
            _onPendingSubmissionRejected = onRejected;
            ResetPendingSubmissionTimeout(trayViewId, sequenceId).Forget();
            _rpc.SubmitTrayRequest(trayViewId, sequenceId);
            return true;
        }

        // ── 마스터 측 수락 ────────────────────────────────────────────

        private void HandleTraySubmissionRequestedReceived(int trayViewId, int sequenceId, int submitterActorNumber)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            Player submitterPlayer = PhotonNetwork.CurrentRoom?.GetPlayer(submitterActorNumber);
            TryAcceptAuthoritative(trayViewId, sequenceId, submitterActorNumber, submitterPlayer);
        }

        /// <summary>
        /// 마스터가 발신자와 트레이 소유권을 검증하고 권위 있는 제출 스냅샷을 만듭니다.
        /// </summary>
        private bool TryAcceptAuthoritative(int trayViewId, int sequenceId, int submitterActorNumber, Player submitterPlayer)
        {
            bool accepted = false;

            // 제출 판정은 제출자가 보내온 JSON이 아니라,
            // 마스터가 실제 트레이 오브젝트에서 읽은 최신 스냅샷을 기준으로 합니다.
            if (!TryResolveAuthoritativeTraySnapshot(trayViewId, submitterActorNumber, out SubmittedTray tray))
            {
                _rpc?.SendTraySubmissionResponse(submitterPlayer, trayViewId, sequenceId, false);
                return false;
            }

            if (_traySubmissionTcs == null)
            {
                Debug.LogWarning("[StageFlow] 현재는 트레이 제출을 기다리고 있지 않습니다.");
                _rpc?.SendTraySubmissionResponse(submitterPlayer, trayViewId, sequenceId, false);
                return false;
            }

            LastSubmitterActorNumber = submitterActorNumber;
            accepted = _traySubmissionTcs.TrySetResult(tray);
            _rpc?.SendTraySubmissionResponse(submitterPlayer, trayViewId, sequenceId, accepted);
            return accepted;
        }

        /// <summary>
        /// PhotonView에서 현재 TrayItem을 조회해 실제 재료 상태를 SubmittedTray로 변환합니다.
        /// </summary>
        private static bool TryResolveAuthoritativeTraySnapshot(int trayViewId, int submitterActorNumber, out SubmittedTray tray)
        {
            tray = null;

            PhotonView trayView = PhotonView.Find(trayViewId);
            if (trayView == null || !trayView.TryGetComponent(out TrayItem trayItem))
            {
                Debug.LogWarning($"[StageFlow] 제출된 트레이를 찾을 수 없습니다. ViewId={trayViewId}");
                return false;
            }

            // 제출 요청자가 실제로 해당 트레이를 들고 있는지 검증합니다.
            if (trayView.TryGetComponent(out HoldableItem holdable)
                && holdable.HolderActorNumber != submitterActorNumber)
            {
                Debug.LogWarning($"[StageFlow] 트레이를 들고 있는 플레이어가 아닙니다. " +
                    $"ViewId={trayViewId} | Holder={holdable.HolderActorNumber} | Submitter={submitterActorNumber}");
                return false;
            }

            tray = trayItem.GetTraySnapshot();
            if (tray == null)
            {
                Debug.LogWarning($"[StageFlow] 트레이 스냅샷 생성에 실패했습니다. ViewId={trayViewId}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ViewID와 sequenceId가 현재 대기 요청과 일치할 때만 콜백을 완료합니다.
        /// </summary>
        private void HandleTraySubmissionResponseReceived(int trayViewId, int sequenceId, bool accepted)
        {
            if (_pendingSubmissionTrayViewId != trayViewId || _pendingSubmissionSequenceId != sequenceId)
            {
                return;
            }

            Action callback = accepted
                ? _onPendingSubmissionAccepted
                : _onPendingSubmissionRejected;

            ClearPendingSubmission();
            callback?.Invoke();
        }

        private void ClearPendingSubmission()
        {
            _pendingSubmissionTimeoutCts?.Cancel();
            _pendingSubmissionTimeoutCts?.Dispose();
            _pendingSubmissionTimeoutCts = null;
            _pendingSubmissionTrayViewId = -1;
            _pendingSubmissionSequenceId = -1;
            _onPendingSubmissionAccepted = null;
            _onPendingSubmissionRejected = null;
        }

        /// <summary>
        /// 응답이 오지 않을 경우 대기 상태를 해제하고 거절 경로로 복구합니다.
        /// </summary>
        private async UniTaskVoid ResetPendingSubmissionTimeout(int trayViewId, int sequenceId)
        {
            _pendingSubmissionTimeoutCts?.Cancel();
            _pendingSubmissionTimeoutCts?.Dispose();
            _pendingSubmissionTimeoutCts = new CancellationTokenSource();
            CancellationToken token = _pendingSubmissionTimeoutCts.Token;

            try
            {
                await UniTask.Delay(SubmissionResponseTimeoutMs, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_pendingSubmissionTrayViewId != trayViewId || _pendingSubmissionSequenceId != sequenceId)
            {
                return;
            }

            Debug.LogWarning($"[StageFlow] 트레이 제출 응답이 제한 시간 내에 도착하지 않았습니다. ViewId={trayViewId} | Seq={sequenceId}");
            Action rejectedCallback = _onPendingSubmissionRejected;
            ClearPendingSubmission();
            rejectedCallback?.Invoke();
        }

        // ── 정리 ──────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_rpc != null)
            {
                _rpc.OnTraySubmissionRequestedReceived -= HandleTraySubmissionRequestedReceived;
                _rpc.OnTraySubmissionResponseReceived -= HandleTraySubmissionResponseReceived;
            }

            ClearPendingSubmission();
        }
    }
}
