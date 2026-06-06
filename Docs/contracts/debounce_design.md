# onWrite 트리거 Debounce 설계 문서

## 문제 정의

Firestore `temp_triples/{uid}/items/{itemId}` 경로에 onWrite 트리거가 설정되어 있다.
김준석 팀원이 batch 업로드 시 짧은 시간 내 다수의 트리플(예: 50개)을 업로드하면,
동일 uid에 대해 최대 50개의 Cloud Function 인스턴스가 동시에 실행된다.

각 인스턴스는 DuckDB 집계 + SPARQL 29개 규칙 실행으로 약 30~60초가 소요된다.
50개 동시 실행 시 불필요한 비용과 Firebase Storage 동시 쓰기 충돌 위험이 있다.

---

## 사용 시나리오

### 시나리오 A: 배치 업로드 (중복 추론 문제)
- 상황: 김준석 batch 업로드 — 50개 트리플을 1초 이내 동시 업로드
- 기대: Cloud Function 인스턴스 1개만 추론 실행 (50개 트리플 모두 포함)
- 문제: 50개 인스턴스가 동시에 `run_inference()` 진입 → 중복 비용 발생

### 시나리오 B: 시간차 업로드 (각각 추론 필요)
- 상황: 사용자가 5분 간격으로 개별 트리플 추가
- 기대: 각 업로드마다 debounce window(30초)를 지나면 추론 실행
- 결과: DEBOUNCE_SECONDS(30초) 경과 후 첫 트리거가 슬롯 획득 → 추론 실행 ✓

### 시나리오 C: Cloud Scheduler와 onWrite 충돌 방지
- 상황: Cloud Scheduler 6회/일 batch 추론과 onWrite 트리거 동시 발생
- 결과: `run_inference()` 내부의 `_load_temp_triples()` 가 빈 결과 반환 →
        `no_new_triples` 조기 반환으로 자연 충돌 방지 (별도 lock 불필요)

---

## 옵션 비교

### Option A: Cloud Tasks 지연 호출

**방식**: 트리거 발생 시 named Cloud Task 등록(N초 후 실행).
동일 uid의 기존 Task를 삭제 후 새로 등록하면 자연스러운 debounce가 된다.

```python
# 의사코드 — 미구현
task_name = f"{queue_path}/tasks/infer-{uid}"
try:
    client.delete_task(name=task_name)
except Exception:
    pass
client.create_task(parent=queue_path, task={
    "name": task_name,
    "schedule_time": now + timedelta(seconds=DEBOUNCE_SECONDS),
    "http_request": {"url": INFER_URL, "body": json.dumps({"uid": uid}).encode()},
})
```

| 항목 | 평가 |
|------|------|
| 진정한 debounce | ✓ (마지막 쓰기 후 N초 대기 후 1회 실행) |
| 무료 티어 | ✓ (Cloud Tasks 1M 요청/월 무료) |
| 복잡도 | 높음 (Cloud Tasks API 활성화, 큐 생성, INFER_URL 환경변수, google-cloud-tasks 패키지 추가) |
| Stateless | ✓ (상태는 Cloud Tasks 서비스에 저장) |

---

### Option B: Firestore 타임스탬프 락 ← **현재 구현**

**방식**: Firestore 트랜잭션으로 `temp_triples/{uid}.lastInferenceAt` 기록.
DEBOUNCE_SECONDS 이내 재실행 요청을 차단한다.

```python
# triggers.py _try_acquire_run_slot() 핵심 로직
@firestore.transactional
def _txn(transaction):
    data = meta_ref.get(transaction=transaction).to_dict() or {}
    last_run = data.get("lastInferenceAt")
    now = datetime.now(timezone.utc)
    if last_run and (now - last_run).total_seconds() < DEBOUNCE_SECONDS:
        return False  # skip
    transaction.set(meta_ref, {"lastInferenceAt": now}, merge=True)
    return True
```

| 항목 | 평가 |
|------|------|
| 진정한 debounce | △ (rate limiter. 첫 트리거가 즉시 실행됨) |
| 무료 티어 | ✓ (Firestore 읽기/쓰기만 사용, 추가 API 없음) |
| 복잡도 | 낮음 (firebase-admin만 사용) |
| Stateless | ✓ (락은 Firestore에 저장, 전역 변수 없음) |

**한계**: 50개 트리플이 동시 업로드될 때 첫 번째 트리거가 즉시 추론을 시작한다.
추론 시작 시점에 일부 트리플이 아직 업로드 중일 수 있다.
→ 미처리 트리플은 Cloud Scheduler 다음 실행(최대 4시간 후)에 반영된다.

---

### Option C: Pub/Sub + Cloud Scheduler 결합

| 항목 | 평가 |
|------|------|
| 복잡도 | 매우 높음 (Pub/Sub 토픽·구독 설정, 별도 구독 함수 필요) |
| 무료 티어 | ✓ (Pub/Sub 10GB/월 무료) |
| 적합성 | 과도한 설계 — 현 규모(7명 팀)에 불필요 |

---

## 권장 옵션: Option B (Firestore 타임스탬프 락)

### 선정 근거

1. **무료 티어 유지**: 추가 API 활성화 없이 기존 Firestore만 활용
2. **단순성**: `google-cloud-tasks` 패키지 불필요, `firebase-admin`만 사용
3. **실용적 충분**: Cloud Scheduler 6회/일이 미처리 트리플을 보완하므로
   "마지막 트리플까지 기다리는" 진정한 debounce가 필수가 아님
4. **Stateless 준수**: 락 상태를 Firestore에 저장, 전역 변수 사용 없음

### Option A로 업그레이드 권장 조건

- 사용자 50명 이상으로 증가 → GB-seconds 비용 주의 구간 진입
- 실시간 피드백이 중요해져 Cloud Scheduler 4시간 지연이 허용 불가
- batch 업로드 패턴 확정 후 Cloud Tasks 큐 이름·URL을 환경변수로 관리 가능

---

## 비용 시뮬레이션

**가정**: 사용자 1명, 하루 트리플 100개, 5분 단위 batch 업로드

| 구분 | debounce 없음 | Option B 적용 |
|------|:------------:|:-------------:|
| 추론 호출 횟수/일 | 100회 | 6~10회 |
| Cloud Functions 실행 시간/일 | 100 × 45s = 4,500s | 10 × 45s = 450s |
| GB-seconds/일 (512MB) | 2,250 | 225 |
| GB-seconds/월 (30일) | 67,500 | 6,750 |
| 무료 한도 (400K GB-s/월) | 여유 ✓ | 여유 ✓ |
| Firestore 추가 쓰기/일 | 0 | +10회 (lock 기록) |
| 월 비용 | $0 | $0 |

**사용자 50명 시나리오**:

| 구분 | debounce 없음 | Option B 적용 |
|------|:------------:|:-------------:|
| GB-seconds/월 | 3,375,000 | 337,500 |
| 무료 한도 초과 | **초과 위험** ⚠ | 여유 ✓ |

→ 사용자 50명 이상부터 Option A(Cloud Tasks) 전환 필요.

---

## 구현 위치

- `Functions/triggers.py`: `DEBOUNCE_SECONDS`, `_try_acquire_run_slot()`, `_handle_triple_written()` 수정
- `Functions/test_triggers.py`: 디바운스 시나리오 3건 추가 (총 8건)
