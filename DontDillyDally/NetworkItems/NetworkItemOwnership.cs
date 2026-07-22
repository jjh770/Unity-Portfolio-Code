/*
 * 역할: 네트워크 아이템의 PhotonView 소유권 요청, 반환, 타임아웃과 이전 결과 콜백을 관리합니다.
 * 핵심 설계: 소유권 이전과 보유 상태 전파 사이의 경쟁 구간을 별도 승인 Actor 상태로 방어합니다.
 */
using Photon.Pun;
using Photon.Realtime;
using System;
using UnityEngine;

namespace DontDillyDally.Data
{
    /// <summary>
    /// 아이템 상호작용에 필요한 소유권 수명주기와 동시 요청 검증을 담당합니다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(NetworkItemState))]
    public class NetworkItemOwnership : MonoBehaviour, IPunOwnershipCallbacks
    {
        private const int NoActorNumber = -1;

        private PhotonView _photonView;
        private NetworkItemState _itemState;
        private ItemObject _itemObject;
        private HoldableItem _holdableItem;
        // 로컬에서 소유권 응답을 기다리는 동안 중복 요청을 차단합니다.
        private bool _isOwnershipRequestPending;
        // 소유권 이전 직후 Hold 상태가 전파되기 전에도 다른 요청을 거절하기 위한 승인 Actor입니다.
        private int _grantedOwnerActorNumber = NoActorNumber;

        private Action _onOwnershipAcquired;
        private Action _onOwnershipFailed;
        // 응답이 오지 않는 소유권 요청을 복구하기 위한 제한 시간입니다.
        private float _pendingTimeout;
        private float _pendingElapsed;
        private bool _hasPendingCallback;

        public event Action<NetworkItemOwnership> OwnershipAcquiredLocally;

        public bool HasLeftSource => _itemState != null && _itemState.HasLeftSource;
        public bool IsHeld => _holdableItem != null && _holdableItem.IsInteracting;
        public int HolderActorNumber => _holdableItem != null ? _holdableItem.HolderActorNumber : -1;
        public bool IsOwnedLocally => _photonView != null && _photonView.IsMine;
        public PhotonView PhotonView => _photonView;
        public NetworkItemState State => _itemState;

        private void Awake()
        {
            _photonView = GetComponent<PhotonView>();
            _itemState = GetComponent<NetworkItemState>();
            _itemObject = GetComponent<ItemObject>();
            _holdableItem = GetComponent<HoldableItem>();

            if (_itemState == null)
                _itemState = gameObject.AddComponent<NetworkItemState>();
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
            _grantedOwnerActorNumber = NoActorNumber;
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            CancelPendingRequest();
        }

        private void Update()
        {
            if (!_hasPendingCallback)
                return;

            _pendingElapsed += Time.deltaTime;
            if (_pendingElapsed >= _pendingTimeout)
            {
                InvokePendingFailed();
            }
        }

        /// <summary>
        /// Controller(Master)가 자신의 소유권을 사용 중으로 잠급니다.
        /// 잠금 중에는 다른 플레이어의 소유권 요청이 거부됩니다.
        /// </summary>
        public void LockOwnershipOnController()
        {
            if (_photonView == null || !_photonView.AmController)
                return;

            if (_photonView.Owner != null)
            {
                _grantedOwnerActorNumber = _photonView.Owner.ActorNumber;
            }
            else if (PhotonNetwork.LocalPlayer != null)
            {
                // Room object (Owner == null): 마스터 자신의 ActorNumber로 락을 건다
                _grantedOwnerActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            }
        }

        public void UnlockOwnershipOnController()
        {
            if (_photonView != null && _photonView.AmController)
            {
                _grantedOwnerActorNumber = NoActorNumber;
            }
        }

        /// <summary>
        /// 아이템의 소유권을 MasterClient에게 반환합니다.
        /// 모든 안전 검사를 포함하므로 어디서든 안전하게 호출할 수 있습니다.
        /// </summary>
        public static bool ReturnOwnershipToMaster(PhotonView photonView)
        {
            if (photonView == null)
                return false;
            if (!PhotonNetwork.InRoom)
                return false;
            if (!photonView.IsMine)
                return false;
            if (PhotonNetwork.MasterClient == null)
                return false;
            if (PhotonNetwork.IsMasterClient)
                return false;

            photonView.TransferOwnership(PhotonNetwork.MasterClient);
            return true;
        }

        /// <summary>
        /// 소유권을 요청하고, 결과를 콜백으로 받습니다.
        /// 이미 소유 중이면 즉시 onAcquired를 호출합니다.
        /// </summary>
        public void RequestOwnershipWithCallback(Action onAcquired, Action onFailed, float timeout = 2f)
        {
            CancelPendingRequest();

            if (IsOwnedLocally)
            {
                onAcquired?.Invoke();
                return;
            }

            _onOwnershipAcquired = onAcquired;
            _onOwnershipFailed = onFailed;
            _pendingTimeout = timeout;
            _pendingElapsed = 0f;
            _hasPendingCallback = true;

            TryAcquireOrRequestOwnership();
        }

        /// <summary>
        /// 진행 중인 콜백 기반 소유권 요청을 취소합니다.
        /// 콜백은 호출되지 않습니다.
        /// </summary>
        public void CancelPendingRequest()
        {
            _isOwnershipRequestPending = false;
            _onOwnershipAcquired = null;
            _onOwnershipFailed = null;
            _hasPendingCallback = false;
            _pendingElapsed = 0f;
            _pendingTimeout = 0f;
        }

        public bool TryAcquireOrRequestOwnership()
        {
            if (_photonView == null || PhotonNetwork.LocalPlayer == null)
                return false;

            if (_photonView.IsMine)
                return true;

            if (_isOwnershipRequestPending)
                return false;

            _isOwnershipRequestPending = true;
            _photonView.RequestOwnership();
            return false;
        }

        public void NotifyHoldStarted()
        {
            _itemObject?.SetAsInteractableItem();

            if (_photonView != null && PhotonNetwork.InRoom)
                _photonView.RPC(nameof(RPC_SetAsInteractableItem), RpcTarget.Others);
        }

        public void MarkLeftSource()
        {
            _itemState?.MarkLeftSource();
        }

        public void ResetSourceState()
        {
            _itemState?.ResetSourceState();
        }

        public void NotifyLeftSource()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                MarkLeftSource();
                return;
            }

            if (_photonView == null)
                return;

            _photonView.RPC(nameof(RPC_MarkLeftSource), RpcTarget.MasterClient);
        }

        [PunRPC]
        public void RPC_MarkLeftSource()
        {
            MarkLeftSource();
        }

        [PunRPC]
        private void RPC_SetAsInteractableItem()
        {
            _itemObject?.SetAsInteractableItem();
        }

        /// <summary>
        /// 마스터가 현재 보유자와 이미 승인된 요청자를 확인해 동시 획득 요청을 중재합니다.
        /// </summary>
        public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
        {
            if (targetView != _photonView)
                return;

            if (!_photonView.AmController)
                return;

            if (IsHeld && requestingPlayer.ActorNumber != HolderActorNumber)
                return;

            // 소유권이 부여되었지만 hold가 아직 확인되지 않은 경우, 다른 플레이어의 요청 거부
            if (_grantedOwnerActorNumber != NoActorNumber && !IsHeld)
            {
                // 부여 대상이 더 이상 소유자가 아니면 (소유권 반환됨) 초기화
                if (_photonView.Owner == null || _photonView.Owner.ActorNumber != _grantedOwnerActorNumber)
                {
                    _grantedOwnerActorNumber = NoActorNumber;
                }
                else if (requestingPlayer.ActorNumber != _grantedOwnerActorNumber)
                {
                    return;
                }
            }

            _grantedOwnerActorNumber = requestingPlayer.ActorNumber;
            targetView.TransferOwnership(requestingPlayer);
        }

        /// <summary>
        /// 이전 결과가 로컬 요청과 일치할 때 성공 콜백을 완료하고 물리 상태를 복구합니다.
        /// </summary>
        public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
        {
            if (targetView != _photonView)
                return;

            _isOwnershipRequestPending = false;

            // 소유권이 Controller(Master)에게 돌아오면 부여 추적 초기화
            if (_photonView.AmController && _photonView.IsMine)
            {
                _grantedOwnerActorNumber = NoActorNumber;
            }

            if (_photonView.IsMine)
            {
                // 소유권 이전 시점에 직렬화 패킷 순서 역전으로 인한
                // 콜라이더/IsKinematic 상태 고착 방지
                _holdableItem?.EnsureIdlePhysicsState();

                InvokePendingAcquired();
                OwnershipAcquiredLocally?.Invoke(this);
            }
        }

        public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
        {
            if (targetView != _photonView)
                return;

            if (PhotonNetwork.LocalPlayer != null &&
                senderOfFailedRequest != null &&
                senderOfFailedRequest.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                _isOwnershipRequestPending = false;
                InvokePendingFailed();
            }
        }

        private void InvokePendingAcquired()
        {
            if (!_hasPendingCallback)
                return;

            Action callback = _onOwnershipAcquired;
            CancelPendingRequest();
            callback?.Invoke();
        }

        private void InvokePendingFailed()
        {
            if (!_hasPendingCallback)
                return;

            Action callback = _onOwnershipFailed;
            CancelPendingRequest();
            callback?.Invoke();
        }
    }
}
