/*
 * 역할: 슬라임 진행 상태를 사용자별 PlayerPrefs 키에 JSON으로 저장하고 복원합니다.
 * 핵심 설계: JsonUtility가 Dictionary를 직렬화하지 못하므로 리스트 기반 Wrapper로 변환합니다.
 */
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SlimeStatusSaveData의 로컬 저장 구현체입니다.
/// </summary>
public class PlayerPrefsSlimeStatusRepository : ISlimeStatusRepository
{
    // 같은 기기의 여러 계정 저장 데이터를 분리하는 PlayerPrefs 키 접두어입니다.
    private readonly string _userId;
    private const string KEY_SUFFIX = "_SlimeStatus";

    public PlayerPrefsSlimeStatusRepository(string userId)
    {
        _userId = userId;
    }

    private string GetKey() => $"{_userId}{KEY_SUFFIX}";

    /// <summary>
    /// 저장 DTO를 Wrapper로 변환해 JSON과 PlayerPrefs에 즉시 기록합니다.
    /// </summary>
    public UniTask Save(SlimeStatusSaveData saveData)
    {
        try
        {
            string json = JsonUtility.ToJson(new SlimeStatusSaveDataWrapper(saveData));
            PlayerPrefs.SetString(GetKey(), json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 저장 실패: {e.Message}");
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 데이터가 없으면 기본값을 반환하고 JSON을 저장 DTO로 복원합니다.
    /// </summary>
    public UniTask<SlimeStatusSaveData> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult(SlimeStatusSaveData.Default);
            }

            string json = PlayerPrefs.GetString(key);
            var wrapper = JsonUtility.FromJson<SlimeStatusSaveDataWrapper>(json);
            return UniTask.FromResult(wrapper.ToSaveData());
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 로드 실패: {e.Message}");
            return UniTask.FromResult<SlimeStatusSaveData>(null);
        }
    }
}

// JsonUtility는 Dictionary를 직렬화할 수 없으므로 Wrapper 클래스 사용
/// <summary>
/// Dictionary 대신 직렬화 가능한 리스트로 저장 DTO를 감싸는 변환 객체입니다.
/// </summary>
[Serializable]
public class SlimeStatusSaveDataWrapper
{
    public int HighestGrade;
    public List<SlimeEntryWrapper> ActiveSlimes = new();
    public string LastSaveTime;

    public SlimeStatusSaveDataWrapper() { }

    public SlimeStatusSaveDataWrapper(SlimeStatusSaveData data)
    {
        HighestGrade = data.HighestGrade;
        LastSaveTime = data.LastSaveTime;
        ActiveSlimes = new List<SlimeEntryWrapper>();

        if (data.ActiveSlimes != null)
        {
            foreach (var entry in data.ActiveSlimes)
            {
                ActiveSlimes.Add(new SlimeEntryWrapper { Grade = entry.Grade, Count = entry.Count });
            }
        }
    }

    /// <summary>
    /// 직렬화용 리스트 항목을 런타임에서 사용하는 SlimeStatusSaveData로 다시 변환합니다.
    /// </summary>
    public SlimeStatusSaveData ToSaveData()
    {
        var data = new SlimeStatusSaveData
        {
            HighestGrade = HighestGrade,
            LastSaveTime = LastSaveTime,
            ActiveSlimes = new List<SlimeEntry>()
        };

        foreach (var entry in ActiveSlimes)
        {
            data.ActiveSlimes.Add(new SlimeEntry { Grade = entry.Grade, Count = entry.Count });
        }

        return data;
    }
}

/// <summary>
/// 등급과 개수를 JsonUtility가 처리할 수 있는 단순 필드로 보관합니다.
/// </summary>
[Serializable]
public class SlimeEntryWrapper
{
    public int Grade;
    public int Count;
}
