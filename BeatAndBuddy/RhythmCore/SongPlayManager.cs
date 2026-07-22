/*
 * 역할: 오디오 예약 재생 시각과 현재 곡 위치를 Unity DSP 클럭으로 관리합니다.
 * 핵심 설계: 노트 생성·이동·판정이 프레임 시간이 아니라 동일한 오디오 시간축을 참조하도록 기준을 제공합니다.
 */
using UnityEngine;

//스테이지 내의 BGM을 관리 -> 재생은 SoundManager가 담당
public class SongPlayManager : SceneSingleton<SongPlayManager>
{
    [Header("BGM 설정")]
    [SerializeField] private float _readyTime = 3f;

    private BGMDataSO _currentBGMData;
    // AudioSource가 실제 재생을 시작하도록 예약한 절대 DSP 시각입니다.
    private double _dspBGMTime;
    private double _songEndTime; // 음악 종료 시간 추적
    private bool _isPlaying = false;
    // 음악 시작 전 선행 노트 생성을 허용하는 시점을 표시합니다.
    private bool _isSpawnNow = false;

    public float SecPerBeat { get; private set; }
    public float BgmPosition { get; private set; }
    public float BgmPositionInBeats { get; private set; }
    public BGMDataSO CurrentBGMData => _currentBGMData;
    public float CurrentBpm => _currentBGMData?.Bpm ?? 0f;
    public bool IsSpawnNow => _isSpawnNow;


    private void Update()
    {
        if (!_isPlaying) 
            return;

        double currentDspTime = AudioSettings.dspTime;

        // 음악 시작 전 (준비 시간)
        if (currentDspTime < _dspBGMTime)
        {
            float timeUntilStart = (float)(_dspBGMTime - currentDspTime);
            BgmPosition = -timeUntilStart;
            BgmPositionInBeats = BgmPosition / SecPerBeat;
            _isSpawnNow = true;
        }
        // 음악 재생 중
        else if (currentDspTime < _songEndTime)
        {
            BgmPosition = (float)(currentDspTime - _dspBGMTime);
            BgmPositionInBeats = BgmPosition / SecPerBeat;
            _isSpawnNow = false;
        }
        // 음악 종료
        else
        {
            BgmPosition = (float)(_songEndTime - _dspBGMTime);
            BgmPositionInBeats = BgmPosition / SecPerBeat;
            _isPlaying = false;
            _isSpawnNow = false;

            Debug.Log($"[SongPlayManager] 음악 재생 완료 - 총 재생 시간: {BgmPosition:F2}초");
        }
    }

    //
    public void LoadSelectedSong()
    {
        if (!SongManager.IsManagerExist())
        {
            Debug.LogError("[SongPlayManager] SongManager가 없습니다!");
            return;
        }

        BGMDataSO selectedSong = SongManager.Instance.SelectedSong;
        if (selectedSong == null)
        {
            Debug.LogError("[SongPlayManager] 선택된 곡이 없습니다!");
            return;
        }

        LoadBGMData(selectedSong);
    }

    public void LoadBGM(ESongType songType)
    {
        if (SongManager.Instance == null)
        {
            Debug.LogError("[SongPlayManager] SongManager가 없습니다!");
            return;
        }

        BGMDataSO song = SongManager.Instance.GetSong(songType);
        if (song == null)
        {
            Debug.LogError($"[SongPlayManager] 곡을 찾을 수 없습니다: {songType}");
            return;
        }

        LoadBGMData(song);
    }

    /// <summary>
    /// 곡 데이터를 교체하고 BPM·오디오 클립·시간 상태를 초기화합니다.
    /// </summary>
    private void LoadBGMData(BGMDataSO bgmData)
    {
        if (bgmData == null)
        {
            Debug.LogError("[SongPlayManager] BGM 데이터가 null입니다!");
            return;
        }

        if (bgmData.AudioClip == null)
        {
            Debug.LogError($"[SongPlayManager] {bgmData.BgmName}의 AudioClip이 없습니다!");
            return;
        }

        _currentBGMData = bgmData;
        SecPerBeat = 60f / _currentBGMData.Bpm;

        Debug.Log($"[SongPlayManager] BGM 로드: {_currentBGMData.BgmName} ({bgmData.SongType}), BPM: {_currentBGMData.Bpm}");
    }

    /// <summary>
    /// 현재 DSP 시각보다 미래의 시작 시각을 예약해 오디오와 게임 로직의 기준점을 고정합니다.
    /// </summary>
    public void PlayBGM()
    {
        if (_currentBGMData == null)
        {
            Debug.LogError("[SongPlayManager] BGM 데이터가 없습니다!");
            return;
        }

        if (_currentBGMData.AudioClip == null)
        {
            Debug.LogError("[SongPlayManager] AudioClip이 없습니다!");
            return;
        }

        AudioClip clip = _currentBGMData.AudioClip;
        SecPerBeat = 60f / _currentBGMData.Bpm;

        // 재생 시작 시간과 종료 시간 계산
        _dspBGMTime = AudioSettings.dspTime + _readyTime;
        _songEndTime = _dspBGMTime + clip.length;

        SoundManager.Instance.PlayBGMScheduled(clip, _dspBGMTime);
        _isSpawnNow = true;
        _isPlaying = true;

        Debug.Log($"[SongPlayManager] BGM 재생 예약 - 곡 길이: {clip.length:F2}초");
    }

    public void StopBGM()
    {
        SoundManager.Instance.StopBGM();
        _isPlaying = false;
        _isSpawnNow = false;
        Debug.Log("[SongPlayManager] BGM 정지");
    }

    // 음악이 재생 중인지 확인
    public bool IsPlaying()
    {
        return _isPlaying;
    }

    // 남은 재생 시간 반환 (초)
    public float GetRemainingTime()
    {
        if (!_isPlaying) return 0f;

        double currentDspTime = AudioSettings.dspTime;

        // 아직 시작 전
        if (currentDspTime < _dspBGMTime)
        {
            return (float)(_songEndTime - _dspBGMTime);
        }

        // 재생 중
        float remaining = (float)(_songEndTime - currentDspTime);
        return Mathf.Max(0f, remaining);
    }

    // 전체 곡 길이 반환 (초)
    public float GetTotalDuration()
    {
        return _currentBGMData?.AudioClip?.length ?? 0f;
    }

    // 재생 진행도 반환 (0~1)
    public float GetProgress()
    {
        if (!_isPlaying || _currentBGMData == null) return 0f;

        float totalLength = _currentBGMData.AudioClip.length;
        if (totalLength <= 0f) return 0f;

        return Mathf.Clamp01(BgmPosition / totalLength);
    }
}
