/*
 * 역할: 현재 로그인 계정의 슬라임 상태를 Firestore 문서에 저장하고 불러오는 서버 Repository입니다.
 * 핵심 설계: WebGL 빌드에서는 전처리 지시문으로 제외되어 상위 Manager가 로컬 구현체를 사용합니다.
 */
#if !UNITY_WEBGL || UNITY_EDITOR

using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

/// <summary>
/// SlimeStatusSaveData의 Firestore 저장 구현체입니다.
/// </summary>
public class FirebaseSlimeStatusRepository : ISlimeStatusRepository
{
    // 계정별 슬라임 상태 문서를 보관할 Firestore 컬렉션 이름입니다.
    private const string COLLECTION_NAME = "SlimeStatus";
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;

    /// <summary>
    /// 현재 Firebase 계정 이메일을 문서 키로 사용해 전체 상태를 덮어씁니다.
    /// </summary>
    public async UniTask Save(SlimeStatusSaveData saveData)
    {
        try
        {
            string email = _auth.CurrentUser.Email;
            await _db.Collection(COLLECTION_NAME).Document(email).SetAsync(saveData).AsUniTask();
            Debug.Log("SlimeStatus 저장 성공");
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 저장 실패: " + e.Message);
        }
    }

    /// <summary>
    /// 문서가 없거나 요청이 실패하면 플레이 가능한 기본 저장 데이터를 반환합니다.
    /// </summary>
    public async UniTask<SlimeStatusSaveData> Load()
    {
        Debug.Log("SlimeStatus 로드");

        try
        {
            string email = _auth.CurrentUser.Email;
            DocumentSnapshot snapshot = await _db.Collection(COLLECTION_NAME).Document(email).GetSnapshotAsync().AsUniTask();
            SlimeStatusSaveData data = snapshot.ConvertTo<SlimeStatusSaveData>();
            if (data != null)
            {
                Debug.Log("SlimeStatus 로드 성공");
                return data;
            }
            return SlimeStatusSaveData.Default;
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 로드 실패: " + e.Message);
            return SlimeStatusSaveData.Default;
        }
    }
}
#endif
