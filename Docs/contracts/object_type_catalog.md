# RoomObject objectType 카탈로그

> 유니티팀(노성민, 이서윤) 합의 미팅용 — 추론 엔진이 생성하는 3D 에셋 목록
>
> 출처: `Functions/ontology/rules/inference_rules.sparql`  
> 최종 갱신: 2026-06-08 (HeartRate/HRV 기반 Rule 30/P7 추가, 합계 22종)  
> v0.3 (2026-06-08): dish_plate(restaurant 전용), meditation_cushion(HRV 회복 부족) 추가

---

## 고정 문자열 objectType — 장소 습관 기반 (7종, Rule 4)

> Rule 4(place_habit): 같은 placeType 장소를 주 3회 이상 방문 시 생성.  
> 모든 값이 규칙 코드에 리터럴로 확정됨. Unity 에셋 이름과 1:1 매핑 가능.

---

### 1. `coffee_cup`

| 항목 | 내용 |
|------|------|
| **값** | `"coffee_cup"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | `placeType = "cafe"` 인 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:cafe"` |
| **배치 영역** | `"desk"` |
| **설명** | 카페를 자주 방문하는 습관을 상징하는 커피컵 오브젝트 |

---

### 2. `dumbbell`

| 항목 | 내용 |
|------|------|
| **값** | `"dumbbell"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | `placeType = "gym"` 인 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:gym"` |
| **배치 영역** | `"floor"` |
| **설명** | 헬스장·운동시설을 자주 방문하는 습관을 상징하는 덤벨 오브젝트 |

---

### 3. `bookshelf`

| 항목 | 내용 |
|------|------|
| **값** | `"bookshelf"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | `placeType = "library"` 인 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:library"` |
| **배치 영역** | `"wall"` |
| **설명** | 도서관·서점을 자주 방문하는 습관을 상징하는 책장 오브젝트 |

---

### 4. `tree_pot`

| 항목 | 내용 |
|------|------|
| **값** | `"tree_pot"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | `placeType = "park"` 인 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:park"` |
| **배치 영역** | `"floor"` |
| **설명** | 공원·야외 공간을 자주 방문하는 습관을 상징하는 화분 오브젝트 |

---

### 5. `generic_marker`

| 항목 | 내용 |
|------|------|
| **값** | `"generic_marker"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | cafe/gym/library/park 이외의 `placeType` 값을 가진 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:{placeType값}"` (예: `"PlaceHabit:hospital"`) |
| **배치 영역** | `"floor"` |
| **설명** | 매핑 테이블에 없는 장소 카테고리에 대한 폴백 오브젝트 |

> **Unity팀 참고:** `inferredFrom` 트리플에서 콜론 뒤 값으로 실제 placeType을 추출 가능.  
> `placeType` 없는 Location은 Rule 4에서 완전히 제외됨.

---

### 6. `desk_light_bright`

| 항목 | 내용 |
|------|------|
| **값** | `"desk_light_bright"` |
| **규칙 ID** | `focus_music_pattern` (Rule 26) |
| **생성 조건** | 클래식·Lo-fi 장르 음악을 하루 합산 180분(3시간) 이상 청취 |
| **인퍼런스 소스** | `prod:FocusMode` |
| **inferredFrom** | `"FocusMode"` |
| **배치 영역** | `"desk"` |
| **설명** | 집중 음악 청취 패턴 감지 시 책상에 배치되는 밝은 조명 오브젝트 |

---

### 7. `party_light`

| 항목 | 내용 |
|------|------|
| **값** | `"party_light"` |
| **규칙 ID** | `social_music_pattern` (Rule 28) |
| **생성 조건** | 댄스·팝 장르 청취 + 외출 위치(집 외) 방문이 동일 날짜·3시간 이내 동시 발생 |
| **인퍼런스 소스** | `prod:SocialActivity` |
| **inferredFrom** | `"SocialActivity"` |
| **배치 영역** | `"ceiling"` |
| **설명** | 사교적 외출과 흥겨운 음악이 겹칠 때 방에 배치되는 파티 조명 오브젝트 |

---

## 추론 상태 기반 objectType (7종) — 신규 추가

> 기존 추론 상태(FatigueRisk 등)의 CONSTRUCT에 RoomObject 생성 트리플 추가.  
> WHERE절 변경 없음 — 각 규칙의 발동 조건은 기존과 동일.

---

### 8. `tired_pillow`

| 항목 | 내용 |
|------|------|
| **값** | `"tired_pillow"` |
| **규칙 ID** | `fatigue_risk` (Rule 1) |
| **생성 조건** | 수면 6시간 미만 + 카페인 조건(주간 3회 초과 / 야간 1회 초과 / 주말 점수 4.5 이상) |
| **인퍼런스 소스** | `prod:FatigueRisk` |
| **inferredFrom** | `"FatigueRisk"` |
| **배치 영역** | `"shelf"` |
| **설명** | 피로 위험 상태 감지 시 방 구석에 배치되는 베개 오브젝트 |

---

### 9. `running_shoes`

| 항목 | 내용 |
|------|------|
| **값** | `"running_shoes"` |
| **규칙 ID** | `sedentary_pattern` (Rule 3) |
| **생성 조건** | DuckDB 전처리 기준 걸음수 3,000보 미만 3일 연속 |
| **인퍼런스 소스** | `prod:SedentaryPattern` |
| **inferredFrom** | `"SedentaryPattern"` |
| **배치 영역** | `"floor"` |
| **설명** | 운동 부족 패턴 감지 시 현관 쪽에 배치되는 운동화 오브젝트 |

---

### 10. `window_rain`

| 항목 | 내용 |
|------|------|
| **값** | `"window_rain"` |
| **규칙 ID** | `indoor_day_pattern` (Rule 7) |
| **생성 조건** | 비(rain) 또는 겨울(12·1·2월) + 외출 장소 2곳 미만 |
| **인퍼런스 소스** | `prod:IndoorDayPattern` |
| **inferredFrom** | `"IndoorDayPattern"` |
| **배치 영역** | `"window"` |
| **설명** | 실내형 하루 패턴 감지 시 창문에 배치되는 비 이펙트 오브젝트 |

---

### 11. `alarm_clock`

| 항목 | 내용 |
|------|------|
| **값** | `"alarm_clock"` |
| **규칙 ID** | `routine_detection` (Rule 8) |
| **생성 조건** | 같은 시간대(hour) CalendarEvent 주 3회 이상 |
| **인퍼런스 소스** | `prod:Routine` |
| **inferredFrom** | `"Routine"` |
| **배치 영역** | `"desk"` |
| **설명** | 루틴 패턴 감지 시 배치되는 알람 시계 오브젝트 |

> **Unity팀 참고:** Rule 8은 `prod:Routine` 타입 노드(P5 페르소나 참조용)와 별도로  
> `prod:RoomObject` 타입의 `alarm_clock` 노드를 추가 생성함. 두 노드는 독립적.

---

### 12. `music_speaker`

| 항목 | 내용 |
|------|------|
| **값** | `"music_speaker"` |
| **규칙 ID** | `music_mood` (Rule 9) |
| **생성 조건** | 특정 장르 하루 총 청취 120분(2시간) 이상 |
| **인퍼런스 소스** | `prod:MusicMood` |
| **inferredFrom** | `"MusicMood"` |
| **배치 영역** | `"shelf"` |
| **설명** | 음악 청취 집중 패턴 감지 시 배치되는 스피커 오브젝트 |

> **Unity팀 참고:** 장르별로 조건을 충족하면 장르 수만큼 노드 생성 가능.

---

### 13. `stress_ball`

| 항목 | 내용 |
|------|------|
| **값** | `"stress_ball"` |
| **규칙 ID** | `stress_music_pattern` (Rule 27) |
| **생성 조건** | 헤비메탈·록 장르를 야간(22시 이후) 청취 |
| **인퍼런스 소스** | `prod:StressIndicator` |
| **inferredFrom** | `"StressIndicator"` |
| **배치 영역** | `"desk"` |
| **설명** | 스트레스 지표 감지 시 배치되는 스트레스볼 오브젝트 |

---

### 14. `calendar_wall`

| 항목 | 내용 |
|------|------|
| **값** | `"calendar_wall"` |
| **규칙 ID** | `schedule_overload` (Rule 29) |
| **생성 조건** | 같은 날짜에 CalendarEvent 5개 이상 |
| **인퍼런스 소스** | `prod:ScheduleOverload` |
| **inferredFrom** | `"ScheduleOverload"` |
| **배치 영역** | `"wall"` |
| **설명** | 일정 과부하 감지 시 벽면에 배치되는 캘린더 오브젝트 |

---

## 페르소나 기반 objectType (방 분위기, 6종) — 신규 추가

> 페르소나 규칙(P1~P6) CONSTRUCT에 RoomObject 생성 트리플 추가.  
> `inferredFromConcept prod:Persona` 트리플로 구분 가능. WHERE절 변경 없음.

---

### 15. `sports_trophy`

| 항목 | 내용 |
|------|------|
| **값** | `"sports_trophy"` |
| **규칙 ID** | `persona_active` (Rule P1) |
| **생성 조건** | 7,000보 이상인 날이 4일 이상 (활동적 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (energyType=active) |
| **inferredFrom** | `"Persona:active"` |
| **배치 영역** | `"shelf"` |
| **설명** | 활동적인 사용자 페르소나 감지 시 배치되는 트로피 오브젝트 |

---

### 16. `cozy_blanket`

| 항목 | 내용 |
|------|------|
| **값** | `"cozy_blanket"` |
| **규칙 ID** | `persona_indoor` (Rule P2) |
| **생성 조건** | `prod:IndoorDayPattern` 상태 주 3회 이상 (집순이 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (lifePattern=indoor) |
| **inferredFrom** | `"Persona:indoor"` |
| **배치 영역** | `"floor"` |
| **설명** | 실내형 생활 패턴 페르소나 감지 시 소파에 배치되는 담요 오브젝트 |

---

### 17. `photo_frame_friends`

| 항목 | 내용 |
|------|------|
| **값** | `"photo_frame_friends"` |
| **규칙 ID** | `persona_social` (Rule P3) |
| **생성 조건** | companion 있는 방문 장소 3회 이상 (사교적 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (socialPreference=social) |
| **inferredFrom** | `"Persona:social"` |
| **배치 영역** | `"wall"` |
| **설명** | 사교적 페르소나 감지 시 벽면에 배치되는 친구 사진 액자 오브젝트 |

---

### 18. `single_chair`

| 항목 | 내용 |
|------|------|
| **값** | `"single_chair"` |
| **규칙 ID** | `persona_solitary` (Rule P4) |
| **생성 조건** | companion 없는 방문이 전체의 70% 이상 (혼자 활동 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (socialPreference=solitary) |
| **inferredFrom** | `"Persona:solitary"` |
| **배치 영역** | `"floor"` |
| **설명** | 독립적 생활 패턴 페르소나 감지 시 배치되는 1인용 의자 오브젝트 |

---

### 19. `organized_shelf`

| 항목 | 내용 |
|------|------|
| **값** | `"organized_shelf"` |
| **규칙 ID** | `persona_routine` (Rule P5) |
| **생성 조건** | `prod:Routine` 노드가 3개 이상 (루틴형 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (lifePattern=routine) |
| **inferredFrom** | `"Persona:routine"` |
| **배치 영역** | `"wall"` |
| **설명** | 규칙적인 생활 패턴 페르소나 감지 시 배치되는 정리된 선반 오브젝트 |

> **Unity팀 참고:** Rule P5의 WHERE절은 `prod:Routine` 타입 노드를 참조함.  
> Rule 8이 생성하는 `alarm_clock`(`prod:RoomObject` 타입)과는 별개임.

---

### 20. `moon_lamp`

| 항목 | 내용 |
|------|------|
| **값** | `"moon_lamp"` |
| **규칙 ID** | `persona_night_owl` (Rule P6) |
| **생성 조건** | 자정(00:00) 이후 Activity가 4회 이상 (야행성 페르소나) |
| **인퍼런스 소스** | `prod:Persona` (lifePattern=night_owl) |
| **inferredFrom** | `"Persona:night_owl"` |
| **배치 영역** | `"ceiling"` |
| **설명** | 야행성 생활 패턴 페르소나 감지 시 천장 근처에 배치되는 달 모양 조명 오브젝트 |

---

## 장소 습관 기반 objectType 추가 (1종) — Rule 4 restaurant 매핑

---

### 21. `dish_plate`

| 항목 | 내용 |
|------|------|
| **값** | `"dish_plate"` |
| **규칙 ID** | `place_habit` (Rule 4) |
| **생성 조건** | `placeType = "restaurant"` 인 Location을 주 3회 이상 방문 |
| **인퍼런스 소스** | `prod:PlaceHabit` |
| **inferredFrom** | `"PlaceHabit:restaurant"` |
| **배치 영역** | `"desk"` |
| **설명** | 식당을 자주 방문하는 습관을 상징하는 접시 오브젝트 |

---

## 바이오 데이터 기반 페르소나 objectType (1종) — Rule P7

---

### 22. `meditation_cushion`

| 항목 | 내용 |
|------|------|
| **값** | `"meditation_cushion"` |
| **규칙 ID** | `recovery_deficit_persona` (Rule P7) |
| **생성 조건** | HRV RMSSD < 20ms (회복 부족 임계값) |
| **인퍼런스 소스** | `prod:Persona` (recoveryLevel=deficit) |
| **inferredFrom** | `"Persona:recovery_deficit"` |
| **배치 영역** | `"floor"` |
| **설명** | HRV 기반 회복 부족 페르소나 감지 시 바닥에 배치되는 명상 쿠션 오브젝트 |

> **Unity팀 참고:** 갤럭시워치 등 웨어러블 기기가 있는 사용자에게만 HRV 데이터가 존재하므로, 이 오브젝트는 조건부 생성됨.

---

## placeType → objectType 매핑 요약 (Rule 4)

| placeType | objectType | inferredFrom | placementZone |
|-----------|-----------|--------------|--------------|
| `cafe` | `coffee_cup` | `PlaceHabit:cafe` | `"desk"` |
| `gym` | `dumbbell` | `PlaceHabit:gym` | `"floor"` |
| `library` | `bookshelf` | `PlaceHabit:library` | `"wall"` |
| `park` | `tree_pot` | `PlaceHabit:park` | `"floor"` |
| `restaurant` | `dish_plate` | `PlaceHabit:restaurant` | `"desk"` |
| 그 외 모두 | `generic_marker` | `PlaceHabit:{값}` | `"floor"` |

---

## 추론 상태 → objectType 매핑 요약

| 추론 상태 | objectType | 규칙 ID | placementZone |
|-----------|-----------|---------|--------------|
| `FatigueRisk` | `tired_pillow` | Rule 1 | `"shelf"` |
| `SedentaryPattern` | `running_shoes` | Rule 3 | `"floor"` |
| `IndoorDayPattern` | `window_rain` | Rule 7 | `"window"` |
| `Routine` | `alarm_clock` | Rule 8 | `"desk"` |
| `MusicMood` | `music_speaker` | Rule 9 | `"shelf"` |
| `FocusMode` | `desk_light_bright` | Rule 26 | `"desk"` |
| `StressIndicator` | `stress_ball` | Rule 27 | `"desk"` |
| `SocialActivity` | `party_light` | Rule 28 | `"ceiling"` |
| `ScheduleOverload` | `calendar_wall` | Rule 29 | `"wall"` |
| `Persona` (energyType=active) | `sports_trophy` | Rule P1 | `"shelf"` |
| `Persona` (lifePattern=indoor) | `cozy_blanket` | Rule P2 | `"floor"` |
| `Persona` (socialPreference=social) | `photo_frame_friends` | Rule P3 | `"wall"` |
| `Persona` (socialPreference=solitary) | `single_chair` | Rule P4 | `"floor"` |
| `Persona` (lifePattern=routine) | `organized_shelf` | Rule P5 | `"wall"` |
| `Persona` (lifePattern=night_owl) | `moon_lamp` | Rule P6 | `"ceiling"` |
| `HighRestingHR` | `stress_ball` | Rule 30 | `"desk"` |
| `Persona` (recoveryLevel=deficit) | `meditation_cushion` | Rule P7 | `"floor"` |

---

## 중복 생성 검사

> 동일 objectType이 여러 규칙에서 생성되는지 확인.

| objectType | 생성 규칙 수 | 판정 |
|------------|-------------|------|
| `coffee_cup` | 1 (Rule 4만) | 중복 없음 |
| `dumbbell` | 1 (Rule 4만) | 중복 없음 |
| `bookshelf` | 1 (Rule 4만) | 중복 없음 |
| `tree_pot` | 1 (Rule 4만) | 중복 없음 |
| `generic_marker` | 1 (Rule 4만) | 중복 없음 |
| `desk_light_bright` | 1 (Rule 26만) | 중복 없음 |
| `party_light` | 1 (Rule 28만) | 중복 없음 |
| `tired_pillow` | 1 (Rule 1만) | 중복 없음 |
| `running_shoes` | 1 (Rule 3만) | 중복 없음 |
| `window_rain` | 1 (Rule 7만) | 중복 없음 |
| `alarm_clock` | 1 (Rule 8만) | 중복 없음 |
| `music_speaker` | 1 (Rule 9만) | 중복 없음 |
| `stress_ball` | 2 (Rule 27, Rule 30) | ⚠️ 중복 발생 — `inferredFrom`으로 구분 (`"StressIndicator"` vs `"HighRestingHR"`) |
| `calendar_wall` | 1 (Rule 29만) | 중복 없음 |
| `sports_trophy` | 1 (Rule P1만) | 중복 없음 |
| `cozy_blanket` | 1 (Rule P2만) | 중복 없음 |
| `photo_frame_friends` | 1 (Rule P3만) | 중복 없음 |
| `single_chair` | 1 (Rule P4만) | 중복 없음 |
| `organized_shelf` | 1 (Rule P5만) | 중복 없음 |
| `moon_lamp` | 1 (Rule P6만) | 중복 없음 |
| `dish_plate` | 1 (Rule 4만) | 중복 없음 |
| `meditation_cushion` | 1 (Rule P7만) | 중복 없음 |

**주의: 간접 중복 가능 시나리오**

Rule 4는 조건(`visitCnt >= 3`)을 만족하는 placeType이 복수개일 경우 **카테고리마다 별도 RoomObject 노드를 생성**한다.  
예: 사용자가 cafe와 gym 모두 주 3회 이상 방문 → `coffee_cup` + `dumbbell` 두 개 동시 생성.  
Unity팀은 복수 오브젝트 동시 배치 상황을 처리해야 한다.

---

## 요약

| 구분 | 개수 |
|------|------|
| Rule 4 (place_habit) objectType | **6종** (`coffee_cup`, `dumbbell`, `bookshelf`, `tree_pot`, `dish_plate`, `generic_marker`) |
| Rule 26 (focus_music_pattern) objectType | **1종** (`desk_light_bright`) |
| Rule 28 (social_music_pattern) objectType | **1종** (`party_light`) |
| Rule 1 (fatigue_risk) objectType | **1종** (`tired_pillow`) |
| Rule 3 (sedentary_pattern) objectType | **1종** (`running_shoes`) |
| Rule 7 (indoor_day_pattern) objectType | **1종** (`window_rain`) |
| Rule 8 (routine_detection) objectType | **1종** (`alarm_clock`) |
| Rule 9 (music_mood) objectType | **1종** (`music_speaker`) |
| Rule 27 (stress_music_pattern) objectType | **1종** (`stress_ball`) |
| Rule 29 (schedule_overload) objectType | **1종** (`calendar_wall`) |
| Rule 30 (high_resting_hr_stress) objectType | **1종** (`stress_ball` — Rule 27과 공유, `inferredFrom`으로 구분) |
| Rule P1 (persona_active) objectType | **1종** (`sports_trophy`) |
| Rule P2 (persona_indoor) objectType | **1종** (`cozy_blanket`) |
| Rule P3 (persona_social) objectType | **1종** (`photo_frame_friends`) |
| Rule P4 (persona_solitary) objectType | **1종** (`single_chair`) |
| Rule P5 (persona_routine) objectType | **1종** (`organized_shelf`) |
| Rule P6 (persona_night_owl) objectType | **1종** (`moon_lamp`) |
| Rule P7 (recovery_deficit_persona) objectType | **1종** (`meditation_cushion`) |
| **총 objectType 유형** | **22종 (전부 고정 문자열)** |
| RoomObject를 생성하는 규칙 수 | **18개** (Rule 1, 3, 4, 7, 8, 9, 26, 27, 28, 29, 30, P1~P7) |
| 동적 objectType | **없음** |
| placementZone 종류 | **6종** (`desk`, `floor`, `wall`, `ceiling`, `window`, `shelf`) |

---

## 관련 파일

- 추론 규칙 원본: [`Functions/ontology/rules/inference_rules.sparql`](../../Functions/ontology/rules/inference_rules.sparql)
- 추론 엔진: [`Functions/ontology_engine.py`](../../Functions/ontology_engine.py)
- 온톨로지 정의: [`Functions/ontology/core.ttl`](../../Functions/ontology/core.ttl)
- Firestore 스키마: [`Docs/contracts/firestore_schema.md`](./firestore_schema.md)
