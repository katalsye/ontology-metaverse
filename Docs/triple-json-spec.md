# Triple JSON 명세서 (v3)

## 개요

본 명세서는 본인 영역(Android/Unity)에서 추출된 RDF 트리플을 김무성님의 온톨로지 추론 엔진(Cloud Functions)에 전달하기 위한 JSON 포맷을 정의한다.

**적용 경로**: `Firestore: temp_triples/{uid}/items/{auto_id}`

---

## 1. 기본 형식

각 트리플은 다음 4개 필드를 갖는다:

```json
{
  "subject": "Subject URI",
  "predicate": "Predicate (약식 또는 full URI)",
  "object": "Object (URI 또는 리터럴)",
  "datatype": "xsd 데이터 타입 또는 null"
}
```

Gemma 응답은 배열을 포함하는 객체 형식이다:

```json
{
  "triples": [
    { ... },
    { ... }
  ]
}
```

**중요**: Firestore에 업로드되는 필드명은 `subject`, `predicate`, `object` (full form)를 사용한다. Gemma 응답 단계에서는 약식(s, p, o)을 쓸 수 있으나, SQLite와 Firestore 사이의 변환 단계에서 full form으로 통일된다.

---

## 2. Subject URI 형식 (무성님 명세 v2)

**모든 Subject와 Object의 URI는 절대 URI(full URI) 형식을 사용한다.**

### Base URI
```
http://7team.dev/ontology#
```

### Subject URI 명명 규칙

| Subject 종류 | 형식 | 예시 |
|---|---|---|
| 사용자 | `{baseUri}user_{uid}` | `http://7team.dev/ontology#user_001` |
| 위치 | `{baseUri}loc_{id}` | `http://7team.dev/ontology#loc_001` |
| 사진 | `{baseUri}photo_{id}` | `http://7team.dev/ontology#photo_001` |
| 사용자 정의 | `{baseUri}{prefix}_{id}` | `http://7team.dev/ontology#meal_001` |

### 허용/금지

```
✅ 허용:
"http://7team.dev/ontology#user_001"
"http://7team.dev/ontology#loc_gangnam_cafe_20260520"

❌ 금지 (절대 URI 아님):
"user_001"
"prod:user_001"
"loc_001"
```

→ 절대 URI가 아닌 경우 무성님 측 `triple_validator.py`에서 필터링됨.

---

## 3. Predicate (무성님 온톨로지 정의)

Predicate는 두 가지 형식 허용:

### 약식 (권장)
```
prod:hasLocation
prod:placeName
```

→ 무성님 측에서 자동으로 full URI로 정규화됨.

### Full URI (선택)
```
http://7team.dev/ontology#hasLocation
```

### 사용 가능한 Predicate 목록 (core.ttl에 정의됨)

#### 위치 관련
| Predicate | 의미 | 사용 위치 |
|---|---|---|
| `prod:hasLocation` | 사용자가 방문한 장소 | User → Location |
| `prod:placeName` | 장소 이름 | Location → string |
| `prod:placeType` | 장소 유형 (cafe, restaurant, park 등) | Location → string |
| `prod:visitTime` | 방문 시각 | Location → datetime |

#### 사진 관련
| Predicate | 의미 | 사용 위치 |
|---|---|---|
| `prod:hasGalleryPhoto` | 사용자가 찍은 사진 | User → GalleryPhoto |
| `prod:foodType` | 음식 종류 | GalleryPhoto → string |
| `prod:analyzedBy` | 분석 도구명 | GalleryPhoto → string |

#### 미정의 Predicate
무성님의 `core.ttl`에 정의되지 않은 predicate는 무성님 측 `triple_validator.py`에서 필터링되어 무시된다.

---

## 4. Object 형식

### 리터럴 (값)
```json
{ "object": "강남 카페", "datatype": "xsd:string" }
{ "object": "37.5", "datatype": "xsd:float" }
{ "object": "100", "datatype": "xsd:integer" }
```

### URI (관계)
```json
{ "object": "http://7team.dev/ontology#loc_001", "datatype": null }
```

→ 관계 트리플(URI 참조)은 `datatype`을 `null`로 두기.

---

## 5. Datatype 목록

| Datatype | 용도 | 예시 값 |
|---|---|---|
| `xsd:string` | 문자열 | `"강남 카페"` |
| `xsd:integer` | 정수 | `"100"` |
| `xsd:float` | 실수 | `"37.5"` |
| `xsd:date` | 날짜 (YYYY-MM-DD) | `"2026-05-20"` |
| `xsd:dateTime` | 일시 (ISO 8601) | `"2026-05-20T14:30:00+09:00"` |
| `xsd:boolean` | 불리언 | `"true"`, `"false"` |
| `null` | 관계 표현 (URI 객체) | - |

---

## 6. 전체 예시

### 예시 1: 텍스트 입력 → 위치 트리플

**사용자 입력**: "오늘 강남 카페 갔다"

**출력 (Firestore 저장 형식)**:
```json
{
  "triples": [
    {
      "subject": "http://7team.dev/ontology#user_001",
      "predicate": "prod:hasLocation",
      "object": "http://7team.dev/ontology#loc_001",
      "datatype": null
    },
    {
      "subject": "http://7team.dev/ontology#loc_001",
      "predicate": "prod:placeName",
      "object": "강남 카페",
      "datatype": "xsd:string"
    },
    {
      "subject": "http://7team.dev/ontology#loc_001",
      "predicate": "prod:placeType",
      "object": "cafe",
      "datatype": "xsd:string"
    }
  ]
}
```

### 예시 2: 이미지 입력 → 음식 트리플

**입력**: 파스타 사진 (레스토랑)

**출력 (Firestore 저장 형식)**:
```json
{
  "triples": [
    {
      "subject": "http://7team.dev/ontology#user_001",
      "predicate": "prod:hasGalleryPhoto",
      "object": "http://7team.dev/ontology#photo_001",
      "datatype": null
    },
    {
      "subject": "http://7team.dev/ontology#photo_001",
      "predicate": "prod:foodType",
      "object": "pasta",
      "datatype": "xsd:string"
    },
    {
      "subject": "http://7team.dev/ontology#photo_001",
      "predicate": "prod:placeType",
      "object": "restaurant",
      "datatype": "xsd:string"
    },
    {
      "subject": "http://7team.dev/ontology#photo_001",
      "predicate": "prod:analyzedBy",
      "object": "Gemma-3n",
      "datatype": "xsd:string"
    }
  ]
}
```

---

## 7. 검증 흐름

### 클라이언트 측 (본인 영역, 1차 검증)

`Unity/Assets/02_Scripts/OnDeviceAI/TripleExtraction/TripleValidator.cs`

- 필수 필드 (s, p, o) 존재 여부
- prefix 형식 확인
- datatype 유효성 (xsd:* 목록 일치)
- 날짜 형식 (ISO 8601)

→ 통과한 트리플만 SQLite 저장 → Firestore 업로드.

### 서버 측 (무성님 영역, 2차 검증)

`triple_validator.py` (Cloud Functions)

- Subject URI 형식 검증 (절대 URI, 공백/제어문자 차단)
- Predicate 정규화 (약식 → full URI, 미정의는 제외)
- 타입 자동 변환
- 숫자 범위 검사 (위도 ±90, 수면 0~24h 등)
- ISO 8601 시간 형식 검증
- 미래 시각 차단
- 고립 노드 경고

→ 통과한 트리플만 추론 그래프에 추가.

---

## 8. 변경 이력

### v3 (2026-05-21)
- Firestore 필드명 통일: `s/p/o` → `subject/predicate/object` (full form)
- 무성님 측 `ontology_engine.py`와 필드명 일치
- 통합 테스트 1단계 결과 반영

### v2 (2026-05-20)
- Base URI 명시 (`http://7team.dev/ontology#`)
- Subject URI 형식 full URI로 변경
- Predicate 통일:
  - `prod:visited` → `prod:hasLocation`
  - `prod:photographed` → `prod:hasGalleryPhoto`
- 무성님 측 `core.ttl` 정의된 predicate 목록 정리

### v1 (이전)
- 약식 prefix (`prod:`) 형식
- 필드명 `s/p/o` 약식

---

## 9. 관련 파일

- **Unity 측 추출기**: `Unity/Assets/02_Scripts/OnDeviceAI/TripleExtraction/`
  - `TextTripleExtractor.cs`
  - `ImageTripleExtractor.cs`
  - `TripleValidator.cs`
- **Unity 측 sync**: `Unity/Assets/02_Scripts/Firebase/Manager/TempTripleManager.cs`
- **무성님 측 검증**: `triple_validator.py`
- **무성님 측 추론 엔진**: `ontology_engine.py`
- **무성님 측 온톨로지**: `core.ttl`