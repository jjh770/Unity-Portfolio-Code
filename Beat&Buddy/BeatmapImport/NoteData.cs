/*
 * 역할: 채보에서 사용하는 노트 방향과 목표 Beat를 Unity 직렬화 가능한 형태로 정의합니다.
 * 핵심 설계: 런타임 노트와 외부 채보 파서가 공유하는 최소 데이터 모델입니다.
 */
using System;
using UnityEngine;
/// <summary>
/// 플레이어 입력과 매칭할 좌·우 노트 종류입니다.
/// </summary>
public enum ENoteType
{
    LNote,  // 왼쪽 노트
    RNote,   // 오른쪽 노트
    
}

/// <summary>
/// 노트가 판정 지점에 도달해야 하는 Beat와 방향을 보관합니다.
/// </summary>
[Serializable]
public class NoteData
{
    [Tooltip("노트가 판정선에 도달할 비트")]
    // 음악 시작을 0으로 했을 때 노트의 목표 Beat입니다.
    public float beat;

    [Tooltip("노트 타입 (좌/우)")]
    // 입력 방향과 판정에 사용하는 노트 종류입니다.
    public ENoteType type;

    /// <summary>
    /// 파싱 결과 또는 에디터 데이터에서 노트 정보를 생성합니다.
    /// </summary>
    public NoteData(float beat, ENoteType type)
    {
        this.beat = beat;
        this.type = type;
    }
}
