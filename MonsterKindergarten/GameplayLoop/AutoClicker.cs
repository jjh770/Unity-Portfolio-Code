/*
 * 역할: 활성 슬라임별 자동 생산 주기를 관리하고 자동 클릭을 발생시킵니다.
 * 핵심 설계: Gameplay가 비활성 상태일 때 자동 생산을 멈춰 오프라인 보상 수령 흐름과 재화 변화가 섞이지 않게 합니다.
 */
using System.Collections.Generic;
using UnityEngine;

public class AutoClicker : MonoBehaviour
{
    private readonly Dictionary<SlimeController, float> _timers = new();
    private readonly HashSet<SlimeController> _activeTargetsCache = new();

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;
        if (SpawnManager.Instance == null) return;

        List<SlimeController> activeTargets = SpawnManager.Instance.GetActiveTargets();
        CleanupTimers(activeTargets);

        foreach (SlimeController target in activeTargets)
        {
            if (target == null || target.IsDragging) continue;

            float interval = target.AutoClickInterval;
            if (interval <= 0f) continue;

            if (!_timers.ContainsKey(target))
            {
                _timers[target] = 0f;
            }

            _timers[target] += Time.deltaTime;

            if (_timers[target] >= interval)
            {
                _timers[target] = 0f;
                AutoClick(target);
            }
        }
    }

    private static void AutoClick(SlimeController target)
    {
        ClickInfo clickInfo = new ClickInfo
        {
            ClickType = EClickType.Auto,
            Position = target.transform.position,
            Point = PointCalculator.Calculate(target.Point, target.Grade, EClickType.Auto),
            Grade = target.Grade
        };

        target.OnClick(clickInfo);
    }

    private void CleanupTimers(List<SlimeController> activeTargets)
    {
        _activeTargetsCache.Clear();

        foreach (SlimeController target in activeTargets)
        {
            _activeTargetsCache.Add(target);
        }

        List<SlimeController> keysToRemove = new();

        foreach (SlimeController key in _timers.Keys)
        {
            if (key == null || !_activeTargetsCache.Contains(key))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (SlimeController key in keysToRemove)
        {
            _timers.Remove(key);
        }
    }

    public void ResetTimer(SlimeController target)
    {
        if (_timers.ContainsKey(target))
        {
            _timers[target] = 0f;
        }
    }
}
