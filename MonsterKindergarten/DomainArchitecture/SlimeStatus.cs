/*
 * 역할: 최고 해금 등급과 현재 활성 슬라임의 등급별 개수를 관리하는 도메인 객체입니다.
 * 핵심 설계: 최고 등급은 감소할 수 없고 활성 개수는 음수가 될 수 없다는 규칙을 강제합니다.
 */
using System;
using System.Collections.Generic;

/// <summary>
/// 슬라임 진행 상태와 불변 규칙을 Unity 오브젝트와 분리해 관리합니다.
/// </summary>
public class SlimeStatus
{
    // 슬라임의 현황을 나타내는 슬라임 스테이터스
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public ESlimeGrade HighestGrade { get; private set; }

    // 활성 슬라임: Grade별 개수
    private readonly Dictionary<ESlimeGrade, int> _activeSlimes = new();
    // 0개 항목을 제거해 현재 존재하는 등급만 보관하는 상태 Dictionary입니다.
    public Dictionary<ESlimeGrade, int> ActiveSlimes => _activeSlimes;

    /// <summary>
    /// 저장 데이터로부터 상태를 만들 때 등급과 개수의 유효성을 검증합니다.
    /// </summary>
    public SlimeStatus(ESlimeGrade highestGrade, Dictionary<ESlimeGrade, int> activeSlimes)
    {
        // 최고 등급 규칙
        if (highestGrade == ESlimeGrade.None || highestGrade == ESlimeGrade.Count)
        {
            throw new ArgumentException($"올바른 등급설정이 아닙니다. : {highestGrade}");
        }
        HighestGrade = highestGrade;

        // 활성 슬라임 규칙
        foreach (var pair in activeSlimes)
        {
            if (pair.Key == ESlimeGrade.None || pair.Key == ESlimeGrade.Count)
            {
                throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {pair.Key}");
            }
            if (pair.Value < 0)
            {
                throw new ArgumentException($"슬라임 개수는 0 이상이어야 합니다. : {pair.Key} = {pair.Value}");
            }
            if (pair.Value > 0)
            {
                _activeSlimes[pair.Key] = pair.Value;
            }
        }
    }

    // 최고 등급 갱신 (올라가기만 가능)
    public void UpdateHighestGrade(ESlimeGrade newGrade)
    {
        if (newGrade == ESlimeGrade.None || newGrade == ESlimeGrade.Count)
            throw new ArgumentException($"올바른 등급설정이 아닙니다. : {newGrade}");
        if (newGrade <= HighestGrade)
            throw new ArgumentException($"새 등급은 현재 최고 등급보다 높아야 합니다. : {newGrade} <= {HighestGrade}");

        HighestGrade = newGrade;
    }

    // 슬라임 추가
    public void AddSlime(ESlimeGrade grade)
    {
        if (grade == ESlimeGrade.None || grade == ESlimeGrade.Count)
            throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {grade}");

        if (_activeSlimes.ContainsKey(grade))
            _activeSlimes[grade]++;
        else
            _activeSlimes[grade] = 1;
    }

    // 슬라임 제거
    public void RemoveSlime(ESlimeGrade grade)
    {
        if (grade == ESlimeGrade.None || grade == ESlimeGrade.Count)
            throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {grade}");

        if (!_activeSlimes.ContainsKey(grade) || _activeSlimes[grade] <= 0)
            throw new InvalidOperationException($"제거할 슬라임이 없습니다. : {grade}");

        _activeSlimes[grade]--;
        if (_activeSlimes[grade] <= 0)
            _activeSlimes.Remove(grade);
    }
}
