/*
 * 역할: 현재 곡의 채보를 순회하며 목표 Beat보다 일정 구간 앞서 노트를 풀에서 생성합니다.
 * 핵심 설계: 한 프레임에 여러 노트의 생성 조건이 충족될 수 있으므로 while 루프로 누락 없이 처리합니다.
 */
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채보 인덱스와 활성 노트 목록을 관리하며 노트 생명주기를 PoolManager에 위임합니다.
/// </summary>
public class NoteSpawner : MonoBehaviour
{
    [Header("노트 설정")]
    [SerializeField] private Transform _leftSpawnPoint;
    [SerializeField] private Transform _rightSpawnPoint;
    [SerializeField] private Transform _judgePoint;
    [SerializeField] private float _beatsShownInAdvance = 5f;

    private BGMDataSO _currentBGMData;
    // 다음으로 생성할 채보 항목을 가리켜 매 프레임 전체 배열을 검색하지 않게 합니다.
    private int _nextNoteIndex = 0;
    private List<Note> _activeNotes = new List<Note>();
    private PoolManager _poolManager;
    // 씬 준비와 음악 재생 상태에 따라 노트 생성을 명시적으로 제어합니다.
    private bool _isSpawningEnabled = false;

    void Start()
    {
        _poolManager = PoolManager.Instance;
        LoadBGMData();
    }

    void Update()
    {
        if (!_isSpawningEnabled) return;

        if (_currentBGMData == null || _nextNoteIndex >= _currentBGMData.Notes.Length) return;

        
        float currentBeat = SongPlayManager.Instance.BgmPositionInBeats;

        while (_nextNoteIndex < _currentBGMData.Notes.Length)
        {
            NoteData nextNote = _currentBGMData.Notes[_nextNoteIndex];
            // Max 때문에 이전에 스폰되는 노트 모두 삭제됐었음.
            float spawnBeat = nextNote.beat - _beatsShownInAdvance; 

            if (currentBeat >= spawnBeat)
            {
                SpawnNote(nextNote);
                _nextNoteIndex++;
            }
            else break;
        }
    }

    /// <summary>
    /// 선택 곡 데이터를 읽고 채보 인덱스와 기존 노트를 초기화합니다.
    /// </summary>
    public void LoadBGMData()
    {
        //SongPlayManager에서 SongManager로 변경 -> 매니저 호출은 왠만하면 Core 우선
        if (SongManager.Instance == null) return;

        _currentBGMData = SongManager.Instance.SelectedSong;
        if (_currentBGMData == null) return;

        _nextNoteIndex = 0;
        ClearAllNotes();
        _currentBGMData.SortNotes();
    }

    /// <summary>
    /// 현재 음악 Beat를 기준으로 채보 생성 루프를 활성화합니다.
    /// </summary>
    public void StartSpawning()
    {
        _nextNoteIndex = 0;
        _isSpawningEnabled = true;
        Debug.Log("[NoteSpawner] 노트 스폰 시작!");
    }

    public void StopSpawning()
    {
        _isSpawningEnabled = false;
        Debug.Log("[NoteSpawner] 노트 스폰 중지!");
    }

    public void ChangeSpawnerPosition()
    {
        Transform tempSpawnPoint = _leftSpawnPoint;
        _leftSpawnPoint = _rightSpawnPoint;
        _rightSpawnPoint = tempSpawnPoint;
    }

    void SpawnNote(NoteData noteData)
    {
        Note noteObject = _poolManager.SpawnGetComponent<NotePool, ENoteType, Note>(noteData.type);

        if (noteObject == null) return;

        Transform spawnPos = (noteData.type == ENoteType.LNote) ? _leftSpawnPoint : _rightSpawnPoint;
        noteObject.transform.position = spawnPos.position;

        noteObject.Initialize(noteData.beat, noteData.type, _beatsShownInAdvance, _judgePoint, this);

        _activeNotes.Add(noteObject);
    }

    public void ReturnNoteToPool(GameObject note)
    {
        note.SetActive(false);
        _poolManager.Despawn<NotePool, ENoteType>(note.GetComponent<Note>().NoteType, note);
    }

    /// <summary>
    /// 곡 변경이나 스테이지 종료 시 활성 노트를 모두 풀로 반환합니다.
    /// </summary>
    public void ClearAllNotes()
    {
        foreach (Note note in _activeNotes)
        {
            if (note != null && note.gameObject != null)
            {
                _poolManager.Despawn<NotePool, ENoteType>(note.GetComponent<Note>().NoteType, note.gameObject);
            }
        }
        _activeNotes.Clear();
    }

    public List<Note> GetActiveNotes()
    {
        _activeNotes.RemoveAll(note => note == null || !note.gameObject.activeSelf);
        return _activeNotes;
    }

    public bool IsSpawningEnabled()
    {
        return _isSpawningEnabled;
    }
    // 즉시 제거하지 않고, 리스트에서만 제거
    public void RemoveNote(Note note)
    {
        _activeNotes.Remove(note);
    }

    // 애니메이션 후 풀 반환용 (Note에서 호출)
    public void ReturnNoteAfterAnimation(GameObject note)
    {
        _poolManager.Despawn<NotePool, ENoteType>(note.GetComponent<Note>().NoteType, note);
    }
}
