## 프로젝트
온톨로지 기반 개인 생산성 향상 플랫폼 (7조, 종합설계)

## 내 파트
온톨로지 설계 및 SPARQL 추론 규칙 작성 (담당: 김무성)
파일 위치:
- Functions/ontology/core.ttl         → 온톨로지 클래스/관계 정의
- Functions/ontology/rules/           → SPARQL 추론 규칙
- Functions/ontology_engine.py        → 추론 실행 Python 코드
- Functions/validate_ontology.py      → 온톨로지 검증 스크립트

## 기술 스택
- Python + RDFLib (온톨로지 로드/저장/추론)
- SPARQL 1.1 (추론 규칙 작성)
- DuckDB (트리플 집계 및 패턴 분석)
- Firebase Storage (온톨로지 그래프 .ttl 파일 직렬화 저장)
- Cloud Functions Python (배포 환경, 반드시 Stateless)
- Firestore (추론 결과 저장)

## 네임스페이스
@prefix prod: <http://7team.dev/ontology#>

## 핵심 클래스 및 속성

### User (사용자)
- 속성: uid, name
- 관계: hasSleepData, hasStepCount, hasAppUsage,
        hasGalleryPhoto, hasLocation, hasActivity,
        hasPersona, receivesQuest

### SleepData (수면)
- 속성: duration (float, 시간), quality (int, 0-100),
        deepSleepRatio (float), timestamp

### StepCount (걸음수)
- 속성: count (int), timestamp, date

### AppUsage (앱 사용)
- 속성: appName (string), usageDuration (int, 분), date

### GalleryPhoto (갤러리 사진)
- 속성: latitude, longitude, timestamp, foodType,
        placeType, analyzedBy (Gemma 3n)

### Location (위치)
- 속성: placeName, latitude, longitude, visitTime,
        visitCount

### Activity (활동)
- 속성: activityType, timestamp
- 관계: triggeredBy → Quest

### Quest (퀘스트)
- 속성: title (string), questType (보완형/개선형),
        isCompleted (bool), createdAt
- 관계: gives → Reward

### Reward (보상)
- 속성: amount (int), rewardType (재화/아이템)

### Persona (사용자 성격 요약)
- 속성: energyType, socialPreference, lifePattern,
        updatedAt
- 저장 위치: Firestore users/{uid}에 JSON 백업

### RoomObject (3D 방 오브젝트)
- 속성: objectType, positionX, positionY, positionZ,
        inferredFrom
- 저장 위치: Firestore room_objects

## 트리플 형태 (Gemma 3n이 생성)
Gemma 3n은 기기 raw 데이터를 아래 형태의 트리플로 변환함.
추론 없이 정형화만 수행.
예시:
- (user, visited, cafe_gangnam)
- (visit, date, 2026-04-07)
- (user, slept, 5.5)          ← 시간 단위
- (user, walked, 2800)        ← 걸음 수
- (user, ate, pasta)
- (user, used_app, youtube, 90min)

## 데이터 흐름 (7단계, 내 파트 기준)
1. Android 기기 → GPS, 걸음수, 수면, 갤러리EXIF, 앱사용시간 수집
   → SQLite raw_data 테이블에 저장 (서버 전송 절대 금지)

2. Gemma 3n (온디바이스) → raw 데이터를 트리플로 변환
   → SQLite triples 테이블에 저장

3. batch 시점 → 새 트리플만 Firestore temp_triples에 임시 업로드

4. Cloud Functions → Firebase Storage에서 기존 온톨로지 그래프 로드

5. RDFLib로 새 트리플 그래프에 추가
   → DuckDB로 집계/패턴 분석
   → SPARQL 추론 규칙 실행

6. 추론 결과 분기:
   [그래프 충분] → Firestore에 저장:
     - room_objects: Unity 3D 방에 배치할 오브젝트 목록/위치
     - quests: 사용자에게 줄 퀘스트
     - persona: 사용자 성격 요약 (users/{uid}에 JSON)
   [빈 노드 감지] → 데이터 보완형 퀘스트 생성:
     예) 카페 방문 기록은 있는데 동행자 정보 없음
         → "오늘 카페 누구랑 갔어?" 퀘스트 생성

7. 그래프 직렬화 → Storage에 저장 → temp_triples 삭제
   → FCM으로 사용자에게 퀘스트 알림 전송

## 추론 규칙 (SPARQL로 구현할 것들)
1. 수면 6시간 미만 + 카페인 2잔 초과
   → prod:FatigueRisk 상태 부여

2. prod:FatigueRisk + 운동 퀘스트 미완료 3일 이상
   → prod:BurnoutWarning 생성
   → 삶 개선형 퀘스트: "가벼운 스트레칭 10분"

3. 걸음수 3000보 미만 3일 연속
   → prod:SedentaryPattern
   → 삶 개선형 퀘스트: "30분 산책하기"

4. 같은 장소 주 3회 이상 방문
   → prod:PlaceHabit 추론
   → room_objects에 해당 장소 관련 오브젝트 추가

5. 카페인 섭취 오후 6시 이후 + 수면질 60 미만
   → 인과관계 추론: 카페인 → 수면 질 저하
   → 삶 개선형 퀘스트: "오후엔 디카페인 어때요?"

6. 빈 노드 감지 (방문 장소는 있는데 동행자 없음)
   → 데이터 보완형 퀘스트: "오늘 [장소] 누구랑 갔어?"

## 퀘스트 종류
- 데이터 보완형: 빈 노드 채우기 위한 자연스러운 질문
  (설문 느낌 아닌 대화 형태로 설계)
- 삶 개선형: 추론 체인 기반 행동 제안
- 퀘스트 완료 시 재화(Reward) 지급 → Unity 상점에서 사용

## 제약사항 (반드시 지킬 것)
- raw 데이터(GPS, 건강, 갤러리 EXIF)는 서버 전송 절대 금지
- Cloud Functions는 항상 Stateless
  (매 호출마다 Storage에서 그래프 로드, 클래스 변수 상태 저장 금지)
- 운영비 0원 유지 (Firebase 무료 티어, Cloud Functions 무료 한도 내)
- 하루 최대 6회 batch 실행 (Cloud Scheduler 트리거)
- 온톨로지 수정 후 반드시 validate_ontology.py 실행

## Firestore 컬렉션 구조 (읽기 전용, 내 파트에서 쓰기)
- room_objects/{uid}: 방 오브젝트 목록/위치 (Unity가 실시간 리스너)
- quests/{uid}: 현재/완료 퀘스트 목록
- users/{uid}: 프로필 + persona JSON 백업
- temp_triples/{uid}: batch용 임시 트리플 (추론 후 삭제)
