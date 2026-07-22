/*
 * 역할: 같은 등급 슬라임의 머지 가능 여부를 확인하고 런타임 오브젝트와 저장 상태 변경을 조정합니다.
 * 핵심 설계: 유지 오브젝트를 다음 등급으로 교체하고 제거 오브젝트는 풀에 반환합니다.
 */
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 드래그 결과를 슬라임 도메인, 진행 상태와 오브젝트 풀에 연결합니다.
/// </summary>
public class MergeManager : MonoBehaviour
{
    public static MergeManager Instance { get; private set; }

    [SerializeField] private int _maxLevel = 10;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 머지 검증, 등급 교체, 최고 등급 갱신, 상태 저장과 제거 대상 반환을 순서대로 수행합니다.
    /// </summary>
    public void Merge(SlimeController keeper, SlimeController removed)
    {
        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        ESlimeGrade fromGrade = keeper.Slime.SpecData.Grade;
        ESlimeGrade toGrade = fromGrade + 1;

        Slime nextSlime = SlimeManager.Instance.Get(toGrade);
        keeper.SetSlime(nextSlime);
        keeper.transform.DOPunchScale(Vector3.one, 1f, 10, 1);

        SlimeManager.Instance.TryUpdateHighestLevel(toGrade);
        SlimeManager.Instance.MergeSlime(fromGrade, toGrade);

        SpawnManager.Instance.Despawn(removed);
    }
}
