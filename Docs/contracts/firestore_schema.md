# Firestore 스키마 — 추론 엔진 출력 명세

> 유니티팀(노성민, 이서윤) 인터페이스 명세서  
> 작성: 김무성 (온톨로지 담당)  
> 최종 갱신: 2026-06-10 (targetEntityUri/targetValue/completedAt/recoveryLevel 필드 추가 v0.3)

---

## room_objects/{uid}

Unity가 실시간 리스너(Firestore Snapshot)로 수신. 추론 실행 후 `merge:true`로 덮어씀.

```json
{
  "objects": [
    {
      "objectType": "coffee_cup",
      "placementZone": "desk",
      "inferredFrom": "PlaceHabit:cafe",
      "inferredFromConcept": "http://7team.dev/ontology#PlaceHabit"
    },
    {
      "objectType": "tired_pillow",
      "placementZone": "shelf",
      "inferredFrom": "FatigueRisk",
      "inferredFromConcept": "http://7team.dev/ontology#FatigueRisk"
    },
    {
      "objectType": "sports_trophy",
      "placementZone": "shelf",
      "inferredFrom": "Persona:active",
      "inferredFromConcept": "http://7team.dev/ontology#Persona"
    }
  ]
}
```

### 필드 설명

| 필드 | 타입 | 예시 | 설명 |
|------|------|------|------|
| `objectType` | `string` | `"coffee_cup"` | 에셋 이름. [object_type_catalog.md](./object_type_catalog.md) 참조 |
| `placementZone` | `string` | `"desk"` | 배치 영역 힌트. 6종 (아래 표 참조) |
| `inferredFrom` | `string` | `"PlaceHabit:cafe"` | 추론 근거 문자열 (로그/디버깅용) |
| `inferredFromConcept` | `string` | `"...#PlaceHabit"` | 추론 근거 URI (로그/디버깅용) |

### placementZone 허용 값 ✅

| 값 | 의미 | 해당 objectType 예시 |
|----|------|---------------------|
| `"desk"` | 책상 위 | `coffee_cup`, `desk_light_bright`, `alarm_clock`, `stress_ball` |
| `"floor"` | 바닥 | `dumbbell`, `tree_pot`, `running_shoes`, `cozy_blanket`, `single_chair` |
| `"wall"` | 벽면 | `bookshelf`, `calendar_wall`, `photo_frame_friends`, `organized_shelf` |
| `"ceiling"` | 천장 | `party_light`, `moon_lamp` |
| `"window"` | 창문 | `window_rain` |
| `"shelf"` | 선반 | `tired_pillow`, `music_speaker`, `sports_trophy` |

> **합의 사항 🤝:** `placementZone` → Unity 내 실제 좌표 매핑은 Unity팀이 구현.  
> 추론 엔진은 영역 이름만 제공. 같은 영역에 오브젝트 여러 개 오면 Unity팀이 배치 조정.

---

## quests/{uid}

퀘스트 목록. 추론 실행 후 `merge:true`로 업데이트.

```json
{
  "quests": [
    {
      "title": "30분 산책하기",
      "questType": "삶 개선형",
      "rewardAmount": 50,
      "isCompleted": false,
      "createdAt": "2026-05-21T12:00:00"
    },
    {
      "title": "오늘 스타벅스 강남점 누구랑 갔어?",
      "questType": "데이터 보완형",
      "rewardAmount": 30,
      "isCompleted": false,
      "createdAt": "2026-05-21T12:00:00",
      "targetEntityUri": "http://7team.dev/ontology#visit_starbucks_gangnam",
      "targetValue": "",
      "completedAt": ""
    }
  ]
}
```

### 필드 설명

| 필드 | 타입 | 허용 값 | 설명 |
|------|------|---------|------|
| `title` | `string` | — | 퀘스트 표시 문구. 사용자에게 그대로 노출 |
| `questType` | `string` | `"삶 개선형"`, `"데이터 보완형"` | 퀘스트 분류 |
| `rewardAmount` | `integer` | `30`, `50` | 완료 시 지급 코인. 데이터 보완형=30, 삶 개선형=50 ✅ |
| `isCompleted` | `boolean` | `true`, `false` | Android 앱에서 완료 처리 후 `true`로 갱신 |
| `createdAt` | `string` | ISO 8601 | 생성 시각 (`"2026-05-21T12:00:00"` 형식) |
| `targetEntityUri` | `string` | URI 또는 `""` | 데이터 보완형 전용. 빈 노드 대상 엔티티 URI |
| `targetValue` | `string` | Literal 또는 `""` | 데이터 보완형 전용. 빈 노드 대상 Literal 값 |
| `completedAt` | `string` | ISO 8601 또는 `""` | 자동 완료 추론 처리 시각. 수동 완료 시 `""` |

---

## users/{uid} — persona 필드

persona는 `users` 문서 내 중첩 필드로 저장. `merge:true`.

```json
{
  "persona": {
    "energyType": "active",
    "socialPreference": "social",
    "lifePattern": "routine",
    "recoveryLevel": "high",
    "updatedAt": "2026-05-21T12:00:00"
  }
}
```

### 필드 설명

| 필드 | 타입 | 허용 값 | 설명 |
|------|------|---------|------|
| `energyType` | `string \| null` | `"active"`, `"indoor"`, `null` | 활동성 페르소나. P1/P2 규칙 발동 시 설정 |
| `socialPreference` | `string \| null` | `"social"`, `"solitary"`, `null` | 사교성 페르소나. P3/P4 규칙 발동 시 설정 |
| `lifePattern` | `string \| null` | `"routine"`, `"night_owl"`, `null` | 생활 패턴 페르소나. P5/P6 규칙 발동 시 설정 |
| `recoveryLevel` | `string \| null` | `"high"`, `"low"`, `null` | 회복력 페르소나. Rule P7 발동 시 설정 |
| `updatedAt` | `string` | ISO 8601 | 마지막 추론 시각 |

> **합의 사항 🤝:** 복수 페르소나 규칙이 동시 발동되면 여러 값이 병합됨.  
> 예: P1(active) + P3(social) 동시 발동 → `energyType=active`, `socialPreference=social`.  
> 충돌(active+indoor 동시)은 마지막 발동 규칙 값으로 덮어씀.

---

## 미확정 항목 ❓

| 항목 | 현황 | 필요한 합의 |ls Functions/serviceAccountKey.json
|------|------|------------|
| 같은 `placementZone`에 복수 오브젝트 | ❓ 미정 | Unity에서 자동 배치 조정 방식 결정 필요 |
| `room_objects` 누적 vs 초기화 | ✅ `merge:true` 누적 | 확정 |
| 퀘스트 완료 처리 주체 | ✅ Android 앱 | 확정 |
| `persona` 초기화 타이밍 | ❓ 미정 | 새 추론마다 덮어씀 vs 이전 값 유지 |
| `inferredFromConcept` 사용 여부 | ❓ 미정 | Unity에서 디버깅 외 활용 계획 없으면 제거 가능 |

---

## 관련 파일

- objectType 카탈로그: [`Docs/contracts/object_type_catalog.md`](./object_type_catalog.md)
- 추론 규칙: [`Functions/ontology/rules/inference_rules.sparql`](../../Functions/ontology/rules/inference_rules.sparql)
- 추론 엔진: [`Functions/ontology_engine.py`](../../Functions/ontology_engine.py)
