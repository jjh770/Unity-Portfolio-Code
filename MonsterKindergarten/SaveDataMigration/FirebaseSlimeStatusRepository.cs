
using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

public class FirebaseSlimeStatusRepository : ISlimeStatusRepository
{
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;

    // 쓰기 실패를 삼키지 않는다.
    //
    // 여기서 잡으면 HybridRepository가 저장이 실패한 사실을 알 수 없고, 로컬만 최신인
    // 채로 클라우드가 조용히 멈춘다. 그 사실은 재설치나 기기 변경 때에야 드러난다.
    //
    // 로드 경로와 같은 규율이다. 리포지토리는 무슨 일이 있었는지 충실히 알리고,
    // 무엇을 할지는 두 저장소를 함께 아는 위층이 정한다.
    public async UniTask Save(SlimeStatusSaveData saveData)
    {
        string userId = _auth.CurrentUser.UserId;
        await _db.Collection(GameDataDomains.SlimeStatus.CloudCollectionName)
            .Document(userId).SetAsync(saveData).AsUniTask();
    }

    // 즉시 저장하므로 미뤄 둔 쓰기가 없다.
    public void FlushPendingSave()
    {
    }

    public async UniTask<SaveLoadResult<SlimeStatusSaveData>> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db
                .Collection(GameDataDomains.SlimeStatus.CloudCollectionName)
                .Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return SaveLoadResult<SlimeStatusSaveData>.NotFound();
            }

            int schemaVersion = SaveSchema.LegacyVersion;
            if (snapshot.TryGetValue<long>(
                    nameof(ISaveData.SchemaVersion),
                    out long storedSchemaVersion))
            {
                schemaVersion = (int)storedSchemaVersion;
            }

            if (schemaVersion > GameDataDomains.SlimeStatus.CurrentSchemaVersion)
            {
                return SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.UnsupportedVersion,
                    UnsupportedSaveVersionException.BuildMessage(
                        "SlimeStatus",
                        schemaVersion,
                        GameDataDomains.SlimeStatus.CurrentSchemaVersion));
            }

            if (schemaVersion < SaveSchema.SlimeInstanceVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    snapshot.ConvertTo<LegacySlimeStatusSaveData>();
                return SaveLoadResult<SlimeStatusSaveData>.Loaded(
                    SlimeStatusSaveMigration.Upgrade(legacyData));
            }

            SlimeStatusSaveData data = snapshot.ConvertTo<SlimeStatusSaveData>();
            if (data == null)
            {
                return SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    "SlimeStatus 문서를 변환하지 못했습니다.");
            }

            if (schemaVersion < GameDataDomains.SlimeStatus.CurrentSchemaVersion ||
                SlimeStatusSaveMigration.HasLegacyPendingTickets(data))
            {
                data = SlimeStatusSaveMigration.UpgradeInstanceData(
                    data,
                    schemaVersion);
            }

            data.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstanceSaveData>();
            data.CompletedTutorials ??= new System.Collections.Generic.List<string>();
            data.NormalCollectionRegistered =
                SlimeStatusSaveData.NormalizeNormalCollection(
                    data.NormalCollectionRegistered);
            SlimeStatusSaveData.NormalizeCollectionStats(data);
            return SaveLoadResult<SlimeStatusSaveData>.Loaded(data);
        }
        catch (Exception e)
        {
            // 문서가 없는 것과 읽지 못한 것은 다르다. 실패를 기본값으로 바꾸지 않는다.
            Debug.LogError("SlimeStatus 로드 실패: " + e.Message);
            return SaveLoadResult<SlimeStatusSaveData>.Failed(
                ESaveLoadFailure.Unreachable,
                e.Message);
        }
    }
}
