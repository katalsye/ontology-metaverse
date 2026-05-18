# 트리플 JSON 포맷 명세

본 문서는 온디바이스 AI가 추출한 트리플을 Firestore로 업로드할 때 사용하는 JSON 포맷을 정의한다. 김무성(온톨로지/추론), 김준석(데이터 수집/온디바이스 AI) 협의에 따라 확정됨.

## 1. 기본 구조

```json
{
  "uid": "firebase_user_uid",
  "batch_id": "2026-04-19T09:00:00",
  "triples": [
    {
      "s": "prod:user_{uid}",
      "p": "prod:hasSleepData",
      "o": "prod:sleep_{uid}_20260419",
      "datatype": null
    },
    {
      "s": "prod:sleep_{uid}_20260419",
      "p": "prod:duration",
      "o": "5.5",
      "datatype": "xsd:float"
    }
  ]
}
```

## 2. 핵심 규칙

| 항목 | 규칙 |
| --- | --- |
| 네임스페이스 | `prod:` = `http://7team.dev/ontology#` 로 치환 |
| 노드 ID 형식 | `prod:{타입}_{uid}_{날짜 or 타임스탬프}` |
| datatype | 숫자/날짜는 반드시 명시, 문자열은 `xsd:string`, 없으면 `null` |
| 날짜 형식 | `xsd:date` → `YYYY-MM-DD`, `xsd:dateTime` → `YYYY-MM-DDTHH:MM:SS` |
| User 연결 | 모든 노드는 반드시 `User` 노드와 연결되는 트리플 포함 |
| bool 값 | `"true"` / `"false"` 문자열로 전송 (validator가 변환) |

## 3. 도메인별 트리플 예시

### 3.1 건강 (Health Connect)

```json
{"s": "prod:user_{uid}", "p": "prod:hasSleepData",  "o": "prod:sleep_{uid}_{date}", "datatype": null},
{"s": "prod:sleep_{uid}_{date}", "p": "prod:duration",  "o": "6.5",   "datatype": "xsd:float"},
{"s": "prod:sleep_{uid}_{date}", "p": "prod:quality",   "o": "72",    "datatype": "xsd:integer"},
{"s": "prod:step_{uid}_{date}",  "p": "prod:count",     "o": "4200",  "datatype": "xsd:integer"},
{"s": "prod:step_{uid}_{date}",  "p": "prod:date",      "o": "2026-04-19", "datatype": "xsd:date"}
```

### 3.2 위치 (GPS)

```json
{"s": "prod:loc_{uid}_{ts}", "p": "prod:placeName",  "o": "스타벅스 강남점", "datatype": "xsd:string"},
{"s": "prod:loc_{uid}_{ts}", "p": "prod:visitTime",  "o": "2026-04-19T14:30:00", "datatype": "xsd:dateTime"},
{"s": "prod:loc_{uid}_{ts}", "p": "prod:placeType",  "o": "cafe",    "datatype": "xsd:string"},
{"s": "prod:loc_{uid}_{ts}", "p": "prod:visitCount", "o": "3",       "datatype": "xsd:integer"}
```

### 3.3 갤러리 사진 (Gemma 3n 분석)

```json
{"s": "prod:photo_{uid}_{ts}", "p": "prod:foodType",   "o": "pasta",      "datatype": "xsd:string"},
{"s": "prod:photo_{uid}_{ts}", "p": "prod:placeType",  "o": "restaurant", "datatype": "xsd:string"},
{"s": "prod:photo_{uid}_{ts}", "p": "prod:latitude",   "o": "37.4979",    "datatype": "xsd:float"},
{"s": "prod:photo_{uid}_{ts}", "p": "prod:longitude",  "o": "127.0276",   "datatype": "xsd:float"},
{"s": "prod:photo_{uid}_{ts}", "p": "prod:analyzedBy", "o": "Gemma-3n",   "datatype": "xsd:string"}
```

### 3.4 앱 사용 (UsageStats)

```json
{"s": "prod:app_{uid}_{ts}", "p": "prod:appName",       "o": "YouTube",  "datatype": "xsd:string"},
{"s": "prod:app_{uid}_{ts}", "p": "prod:usageDuration", "o": "90",       "datatype": "xsd:integer"},
{"s": "prod:app_{uid}_{ts}", "p": "prod:date",          "o": "2026-04-19", "datatype": "xsd:date"}
```

### 3.5 음악 (Spotify)

```json
{"s": "prod:music_{uid}_{ts}", "p": "prod:trackName",      "o": "Blinding Lights", "datatype": "xsd:string"},
{"s": "prod:music_{uid}_{ts}", "p": "prod:artist",         "o": "The Weeknd",      "datatype": "xsd:string"},
{"s": "prod:music_{uid}_{ts}", "p": "prod:genre",          "o": "pop",             "datatype": "xsd:string"},
{"s": "prod:music_{uid}_{ts}", "p": "prod:listenDuration", "o": "35",              "datatype": "xsd:integer"},
{"s": "prod:music_{uid}_{ts}", "p": "prod:playedAt",       "o": "2026-04-19T22:10:00", "datatype": "xsd:dateTime"}
```

### 3.6 일정 (Google Calendar)

```json
{"s": "prod:event_{uid}_{ts}", "p": "prod:eventTitle",  "o": "팀 미팅",            "datatype": "xsd:string"},
{"s": "prod:event_{uid}_{ts}", "p": "prod:startTime",   "o": "2026-04-19T10:00:00", "datatype": "xsd:dateTime"},
{"s": "prod:event_{uid}_{ts}", "p": "prod:endTime",     "o": "2026-04-19T11:00:00", "datatype": "xsd:dateTime"},
{"s": "prod:event_{uid}_{ts}", "p": "prod:isRecurring", "o": "true",               "datatype": "xsd:boolean"}
```

### 3.7 날씨 (기상청)

```json
{"s": "prod:weather_{uid}_{date}", "p": "prod:temperature", "o": "18.5",   "datatype": "xsd:float"},
{"s": "prod:weather_{uid}_{date}", "p": "prod:condition",   "o": "sunny",  "datatype": "xsd:string"},
{"s": "prod:weather_{uid}_{date}", "p": "prod:humidity",    "o": "55.0",   "datatype": "xsd:float"},
{"s": "prod:weather_{uid}_{date}", "p": "prod:recordedAt",  "o": "2026-04-19T09:00:00", "datatype": "xsd:dateTime"}
```

## 4. Firestore temp_triples 저장 구조
temp_triples/
{uid}/
items/
{auto_id}: { s, p, o, datatype }
{auto_id}: { s, p, o, datatype }

## 5. 책임 분담

| 역할 | 담당자 | 영역 |
| --- | --- | --- |
| 트리플 생성 (Gemma + 룰 기반) | 김준석 | 온디바이스 (Unity) |
| 포맷 검증 (validator) | 김준석 | 온디바이스 (Unity) |
| Firestore 업로드 | 김준석 | 온디바이스 (Unity) |
| 트리플 수신 + 추론 | 김무성 | Cloud Functions |
| 온톨로지 스키마 정의 | 김무성 | core.ttl |