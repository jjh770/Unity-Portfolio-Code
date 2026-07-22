/*
 * 역할: 활성 노트를 거리·종류·진행도 기준으로 조회해 보스 패턴에 제공하는 선택 계층입니다.
 * 핵심 설계: 개별 패턴이 NoteSpawner 내부 컬렉션과 필터 조건을 반복 구현하지 않도록 공통화합니다.
 */
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 현재 활성 노트 집합에 대한 재사용 가능한 질의 메서드를 제공합니다.
/// </summary>
public class NoteController : MonoBehaviour
{
    [SerializeField] private NoteSpawner _noteSpawner;

    // 활성 목록에서 중복 없이 무작위 대상을 선택합니다.
    public List<Note> GetRandomNotes(int count)
    {
        return _noteSpawner.GetActiveNotes()
            .OrderBy(x => Random.value)
            .Take(count)
            .ToList();
    }

    // 판정 지점까지 남은 거리가 짧은 노트를 우선합니다.
    public List<Note> GetClosestNotes(int count)
    {
        return _noteSpawner.GetActiveNotes()
            .OrderBy(note => note.GetDistanceToTarget())
            .Take(count)
            .ToList();
    }

    // 방향성 패턴을 위해 지정한 노트 타입만 필터링합니다.
    public List<Note> GetNotesByType(ENoteType type)
    {
        return _noteSpawner.GetActiveNotes()
            .Where(note => note.NoteType == type)
            .ToList();
    }

    // 화면 진행도 범위로 패턴 적용 대상을 제한합니다.
    public List<Note> GetNotesByProgress(float minProgress, float maxProgress)
    {
        return _noteSpawner.GetActiveNotes()
            .Where(note => {
                float progress = note.GetProgressToTarget();
                return progress >= minProgress && progress <= maxProgress;
            })
            .ToList();
    }

    // 목표 Beat가 빠른 순서로 앞으로 판정될 노트를 선택합니다.
    public List<Note> GetUpcomingNotes(int count)
    {
        return _noteSpawner.GetActiveNotes()
            .OrderBy(note => note.TargetBeat)
            .Take(count)
            .ToList();
    }

    // 진행도 조건을 먼저 적용한 뒤 요청 개수만큼 무작위 선택합니다.
    public List<Note> GetRandomNotesByProgress(float minProgress, float maxProgress, int count)
    {
        var notesInRange = _noteSpawner.GetActiveNotes()
            .Where(note => {
                float progress = note.GetProgressToTarget();
                return progress >= minProgress && progress <= maxProgress;
            })
            .ToList();

        // 범위 내 노트가 없으면 빈 리스트 반환
        if (notesInRange.Count == 0)
        {
            Debug.LogWarning($"[NoteController] 진행도 {minProgress:F2}~{maxProgress:F2} 범위에 노트가 없습니다!");
            return new List<Note>();
        }

        // 요청한 개수보다 적으면 있는 만큼만 반환
        int actualCount = Mathf.Min(count, notesInRange.Count);

        if (actualCount < count)
        {
            Debug.LogWarning($"[NoteController] 요청: {count}개, 실제: {actualCount}개 (범위 내 노트 부족)");
        }

        return notesInRange
            .OrderBy(x => Random.value)
            .Take(actualCount)
            .ToList();
    }
}
