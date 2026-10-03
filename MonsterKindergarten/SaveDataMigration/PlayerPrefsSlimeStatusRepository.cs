using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

public class PlayerPrefsSlimeStatusRepository : ISlimeStatusRepository
{
    private readonly string _userId;
    private const string KEY_SUFFIX = "_SlimeStatus";

    // 저장 데이터의 도감 목록은 빈 20칸을 기본값으로 갖는다. Newtonsoft는 기본 설정에서
    // 그런 리스트를 새로 만들지 않고 읽은 값을 뒤에 이어 붙여 40칸을 만들고, 이어지는
    // 정규화가 앞 20칸만 남겨 읽을 때마다 도감 등록과 통계가 기본값으로 지워졌다.
    // 리스트를 교체하게 해 저장된 값이 그대로 남게 한다.
    private static readonly JsonSerializerSettings LoadSettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    public PlayerPrefsSlimeStatusRepository(string userId)
    {
        _userId = userId;
    }

    private string GetKey() => $"{_userId}{KEY_SUFFIX}";

    public void Delete()
    {
        PlayerPrefs.DeleteKey(GetKey());
    }

    // 쓰기 실패를 삼키지 않는다. 로드 경로와 같은 규율이다.
    //
    // 여기서 잡으면 HybridRepository가 로컬 사본이 갱신되지 않은 사실을 알 수 없다.
    // 무엇을 할지는 두 저장소를 함께 아는 위층이 정한다.
    public UniTask Save(SlimeStatusSaveData saveData)
    {
        string json = JsonConvert.SerializeObject(saveData);
        PlayerPrefs.SetString(GetKey(), json);
        PlayerPrefs.Save();

        return UniTask.CompletedTask;
    }

    // 즉시 저장하므로 미뤄 둔 쓰기가 없다.
    public void FlushPendingSave()
    {
    }

    public UniTask<SaveLoadResult<SlimeStatusSaveData>> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.NotFound());
            }

            string json = PlayerPrefs.GetString(key);
            JObject root = JObject.Parse(json);
            int schemaVersion = root.Value<int?>(nameof(ISaveData.SchemaVersion)) ??
                                SaveSchema.LegacyVersion;

            if (schemaVersion > GameDataDomains.SlimeStatus.CurrentSchemaVersion)
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.Failed(
                        ESaveLoadFailure.UnsupportedVersion,
                        UnsupportedSaveVersionException.BuildMessage(
                            "SlimeStatus",
                            schemaVersion,
                            GameDataDomains.SlimeStatus.CurrentSchemaVersion)));
            }

            SlimeStatusSaveData saveData;
            if (schemaVersion < SaveSchema.SlimeInstanceVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    JsonConvert.DeserializeObject<LegacySlimeStatusSaveData>(
                        json,
                        LoadSettings);
                saveData = SlimeStatusSaveMigration.Upgrade(legacyData);
            }
            else
            {
                saveData = JsonConvert.DeserializeObject<SlimeStatusSaveData>(
                    json,
                    LoadSettings);
                if (schemaVersion < GameDataDomains.SlimeStatus.CurrentSchemaVersion ||
                    SlimeStatusSaveMigration.HasLegacyPendingTickets(saveData))
                {
                    saveData = SlimeStatusSaveMigration.UpgradeInstanceData(
                        saveData,
                        schemaVersion);
                }
            }

            if (saveData == null)
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.Failed(
                        ESaveLoadFailure.Unreadable,
                        "슬라임 저장 데이터를 변환하지 못했습니다."));
            }

            saveData.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstanceSaveData>();
            saveData.CompletedTutorials ??= new System.Collections.Generic.List<string>();
            saveData.NormalCollectionRegistered =
                SlimeStatusSaveData.NormalizeNormalCollection(
                    saveData.NormalCollectionRegistered);
            SlimeStatusSaveData.NormalizeCollectionStats(saveData);
            return UniTask.FromResult(
                SaveLoadResult<SlimeStatusSaveData>.Loaded(saveData));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 로드 실패: {e.Message}");
            // 기본값으로 바꾸면 손상된 저장이 신규 계정이 되고, 첫 저장이 원본을 덮어쓴다.
            // 초기화가 멈추지 않도록 실패는 SaveDataLoadGuard가 세션 단위로 처리한다.
            return UniTask.FromResult(
                SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    e.Message));
        }
    }
}
