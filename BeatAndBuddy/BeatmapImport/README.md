# BeatmapImport

Osu! Taiko 형식의 `.osu` 파일에서 메타데이터, BPM과 HitObject를 읽어 리듬게임의 ScriptableObject 데이터로 변환합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`OSUParser.cs`](./OSUParser.cs) | 파일 섹션을 순회하며 메타데이터·TimingPoint·HitObject를 파싱합니다. |
| [`NoteData.cs`](./NoteData.cs) | Beat 시각과 좌우 노트 종류를 보관하는 직렬화 데이터입니다. |
| [`BGMDataSO.cs`](./BGMDataSO.cs) | 오디오 클립, BPM, 난이도와 노트 배열을 묶고 에디터 임포트 기능을 제공합니다. |

## 데이터 흐름

```text
.osu 파일
→ TimingPoints에서 BPM 계산
→ HitObjects의 밀리초 시각 읽기
→ Beat 단위로 변환
→ HitSound로 좌·우 노트 분류
→ NoteData 배열
→ BGMDataSO
```

## 범위

이 샘플은 프로젝트에서 사용한 단일 BPM Taiko 채보를 대상으로 합니다. 변속 구간이나 모든 Osu! 오브젝트 타입을 지원하는 범용 파서는 아닙니다.
