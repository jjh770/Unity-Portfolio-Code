/*
 * 역할: 한 곡의 오디오, BPM, 난이도와 노트 배열을 묶는 ScriptableObject 데이터입니다.
 * 핵심 설계: 에디터에서 `.osu` 파일을 임포트하고 노트를 정렬·검증하는 제작 도구 역할도 함께 수행합니다.
 */
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 리듬 런타임과 에디터 채보 임포트가 공유하는 곡 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewBGMData", menuName = "Rhythm/BGM Data SO")]
public class BGMDataSO : ScriptableObject
{
    [Header("곡 식별자")]
    [SerializeField] private ESongType _songType = ESongType.None; 

    [Header("BGM 정보")]
    [SerializeField] private Sprite _bgmIcon;
    [SerializeField] private string _bgmName;
    [SerializeField] private float _bpm;
    [SerializeField] private AudioClip _audioClip;

    [Header("노트 데이터")]
    [SerializeField] private NoteData[] _notes = new NoteData[0];

    [Header("난이도")]
    [Range(1, 5)]
    [SerializeField] private int _difficulty = 3;

    public ESongType SongType => _songType;
    public Sprite BgmIcon => _bgmIcon;
    public string BgmName => _bgmName;
    // DSP 시간과 Beat 변환의 기준이 되는 분당 박자 수입니다.
    public float Bpm => _bpm;
    public AudioClip AudioClip => _audioClip;
    // 목표 Beat 순서로 정렬되어 NoteSpawner가 순차 소비하는 채보입니다.
    public NoteData[] Notes => _notes;
    public int Difficulty => _difficulty;

    public float GetDuration() => _audioClip != null ? _audioClip.length : 0f;

    /// <summary>
    /// 곡 재생과 노트 생성에 필요한 데이터가 모두 설정됐는지 검증합니다.
    /// </summary>
    public bool IsValid()
    {
        if (_audioClip == null)
        {
            Debug.LogError($"[{_bgmName}]: AudioClip이 없습니다!");
            return false;
        }
        if (_songType == ESongType.None)
        {
            Debug.LogWarning($"[{_bgmName}]: SongType이 설정되지 않았습니다!");
        }
        return true;
    }

    /// <summary>
    /// 오디오 또는 에디터 입력을 기반으로 BPM 분석 도구를 실행합니다.
    /// </summary>
    [ContextMenu("BPM 자동 분석")]
    public void AnalyzeBPM()
    {
        if (_audioClip == null)
        {
            Debug.LogError("AudioClip이 없습니다!");
            return;
        }

        _bpm = UniBpmAnalyzer.AnalyzeBpm(_audioClip);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// NoteSpawner가 앞에서부터 순회할 수 있도록 목표 Beat 오름차순으로 정렬합니다.
    /// </summary>
    [ContextMenu("노트 정렬")]
    public void SortNotes()
    {
        System.Array.Sort(_notes, (a, b) => a.beat.CompareTo(b.beat));

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 선택한 `.osu` 파일을 파싱해 메타데이터와 노트 배열을 에셋에 반영합니다.
    /// </summary>
    [ContextMenu("OSU 파일에서 임포트")]
    public void ImportFromOsuFile()
    {
        string path = EditorUtility.OpenFilePanel("OSU 파일 선택", Application.dataPath, "osu");
        if (string.IsNullOrEmpty(path)) return;

        var osuData = OsuParser.ParseOsuFile(path);
        if (osuData == null)
        {
            Debug.LogError("OSU 파일 파싱 실패!");
            return;
        }

        _bpm = osuData.bpm;

        if (string.IsNullOrEmpty(_bgmName))
            _bgmName = string.IsNullOrEmpty(osuData.title) ? "Imported Song" : osuData.title;

        _notes = osuData.notes.ToArray();
        SortNotes();

        Debug.Log($"=== OSU 임포트 완료 ===");
        Debug.Log($"곡명: {_bgmName}");
        Debug.Log($"BPM: {_bpm:F2}");
        Debug.Log($"노트 수: {_notes.Length}개");
        Debug.Log($"오디오 파일: {osuData.audioFileName}");
        Debug.Log("AudioClip을 수동으로 할당해주세요!");

        EditorUtility.SetDirty(this);
    }
#endif

    public int GetTotalNotes() => _notes.Length;

    public int GetLNoteCount()
    {
        return _notes.Count(note => note.type == ENoteType.LNote);
    }

    public int GetRNoteCount()
    {
        return _notes.Count(note => note.type == ENoteType.RNote);
    }
}
