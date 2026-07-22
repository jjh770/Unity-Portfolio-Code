# Beat & Buddy — Code Samples

오디오 클럭을 기준으로 노트 생성·이동·판정을 같은 시간축에 맞추고, `.osu` 채보를 게임 데이터로 변환한 리듬게임 코드입니다.

## 핵심 문제

- 프레임 시간에 의존할 경우 음악과 노트 위치·판정이 누적해서 어긋나는 문제
- 곡마다 노트 시각을 수동 입력하면 데이터 제작 비용과 오류가 커지는 문제
- 보스 패턴이 기존 리듬 규칙과 별개로 동작하면 플레이 경험이 분리되는 문제

## 폴더

| 폴더 | 내용 |
|---|---|
| [RhythmCore](./RhythmCore/README.md) | DSP 시간축, Beat 기반 노트 생성·이동·판정 |
| [BeatmapImport](./BeatmapImport/README.md) | `.osu` 파일 파싱과 ScriptableObject 데이터 연결 |
| [BossPattern](./BossPattern/README.md) | 활성 노트를 조건별로 조회해 보스 패턴에서 재사용 |

## 권장 읽기 순서

1. `RhythmCore/SongPlayManager.cs`
2. `BeatmapImport/OSUParser.cs`
3. `RhythmCore/NoteSpawner.cs`
4. `RhythmCore/Note.cs`
5. `RhythmCore/JudgeManager.cs`
6. `BossPattern/NoteController.cs`

## 주요 의존성

- Unity Audio / `AudioSettings.dspTime`
- DOTween
- 프로젝트 공용 PoolManager 및 SceneSingleton

## 공개 범위

UI, 사운드 매니저, 플레이어 연출과 공용 풀 구현은 제외했습니다. 각 파일은 실제 프로젝트에서 핵심 시간 계산과 판정 흐름을 확인하기 위한 샘플입니다.
