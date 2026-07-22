/*
 * 역할: 네트워크 아이템을 풀 또는 Photon 네트워크에서 안전하게 회수하는 공용 유틸리티입니다.
 * 핵심 설계: 마스터가 소유자가 아닐 때는 소유권을 먼저 획득한 뒤 제거하여 권한 오류와 잔존 오브젝트를 방지합니다.
 */
using Photon.Pun;
using UnityEngine;

namespace DontDillyDally.Data
{
    /// <summary>
    /// 아이템 종류와 네트워크 상태에 따라 적절한 회수 경로를 선택합니다.
    /// </summary>
    public static class ItemRecycleUtility
    {
        /// <summary>
        /// 호출자의 권한과 아이템 상태를 확인해 일반 회수 또는 마스터 회수 경로를 선택합니다.
        /// </summary>
        public static bool TryRecycle(ItemObject itemObject)
        {
            if (itemObject == null)
            {
                return false;
            }

            if (itemObject.IsPendingRecycle)
            {
                return true;
            }

            PhotonView photonView = itemObject.PhotonView;
            if (!PhotonNetwork.InRoom)
            {
                return false;
            }

            if (photonView == null)
            {
                Debug.LogWarning($"[ItemRecycle] Photon 룸 아이템 '{itemObject.name}'에 PhotonView가 없습니다.");
                return false;
            }

            if (!PhotonNetwork.IsMasterClient && !photonView.IsMine)
            {
                return false;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                itemObject.RequestRecycleOnMaster();
                return true;
            }

            return TryRecycleAsMaster(itemObject);
        }

        /// <summary>
        /// 마스터 전용 회수 요청을 시작하고 필요한 경우 비동기 소유권 획득을 기다립니다.
        /// </summary>
        public static bool TryRecycleAsMaster(ItemObject itemObject)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || itemObject == null)
            {
                return false;
            }

            PhotonView photonView = itemObject.PhotonView;
            if (photonView == null)
            {
                return false;
            }

            if (!itemObject.TryBeginRecycle())
            {
                return true;
            }

            if (photonView.IsMine)
            {
                RecycleNetworkedObject(itemObject);
                return true;
            }

            NetworkItemOwnership networkOwnership = itemObject.NetworkOwnership;
            if (networkOwnership == null)
            {
                itemObject.ResetRecycleState();
                return false;
            }

            networkOwnership.RequestOwnershipWithCallback(
                onAcquired: () => RecycleAfterMasterOwnershipAcquired(itemObject),
                onFailed: itemObject.ResetRecycleState);

            return true;
        }

        /// <summary>
        /// 마스터 소유권 획득 콜백 이후 실제 네트워크 제거를 수행합니다.
        /// </summary>
        private static void RecycleAfterMasterOwnershipAcquired(ItemObject itemObject)
        {
            if (itemObject == null)
            {
                return;
            }

            PhotonView photonView = itemObject.PhotonView;
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || photonView == null || !photonView.IsMine)
            {
                itemObject.ResetRecycleState();
                return;
            }

            RecycleNetworkedObject(itemObject);
        }

        /// <summary>
        /// PhotonView 유효성과 방 상태를 확인한 뒤 최종 제거 방식을 결정합니다.
        /// </summary>
        private static void RecycleNetworkedObject(ItemObject itemObject)
        {
            itemObject.PrepareForRecycle();
            PhotonNetwork.Destroy(itemObject.gameObject);
        }
    }
}

