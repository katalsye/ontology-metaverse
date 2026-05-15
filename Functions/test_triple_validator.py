"""
test_triple_validator.py
TripleValidator 단위 테스트 — 14개 그룹, 176개 check 호출
Firebase 없이 RDFLib만으로 실행

Usage:
    python Functions/test_triple_validator.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from triple_validator import TripleValidator, validate_required_properties, _validate_subject_uri

PROD  = "http://7team.dev/ontology#"
XSD   = "http://www.w3.org/2001/XMLSchema#"
RDF_T = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type"

PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"


def check(label: str, condition: bool, detail: str = "") -> bool:
    tag = PASS if condition else FAIL
    suffix = f"  ↳ {detail}" if detail else ""
    print(f"  [{tag}] {label}{suffix}")
    return condition


def V() -> TripleValidator:
    return TripleValidator(Path("Functions/ontology/core.ttl"))


# ── Test 1: predicate 정규화 ──────────────────────────────────────────────────

def test_predicate_normalization() -> bool:
    print("\n[Test 1] predicate 정규화")
    v = make_v = V()
    r = []

    # 로컬명 → full URI
    valid, _ = v.validate([{"subject": PROD + "s", "predicate": "hasSleepData",
                             "object": PROD + "sl"}])
    r.append(check("'hasSleepData' → full URI",
                   len(valid) == 1 and valid[0]["predicate"] == PROD + "hasSleepData"))

    # prod: 접두사 포함
    valid, _ = v.validate([{"subject": PROD + "u", "predicate": "prod:uid", "object": "abc"}])
    r.append(check("'prod:uid' → full URI",
                   len(valid) == 1 and valid[0]["predicate"] == PROD + "uid"))

    # 이미 full URI
    valid, _ = v.validate([{"subject": PROD + "u", "predicate": PROD + "name",
                             "object": "홍길동"}])
    r.append(check("이미 full URI → 그대로 통과",
                   len(valid) == 1 and valid[0]["predicate"] == PROD + "name"))

    # "a" → rdf:type
    valid, _ = v.validate([{"subject": PROD + "u1", "predicate": "a",
                             "object": PROD + "User"}])
    r.append(check("'a' → rdf:type URI",
                   len(valid) == 1 and valid[0]["predicate"] == RDF_T))

    # "rdf:type" → rdf:type
    valid, _ = v.validate([{"subject": PROD + "u1", "predicate": "rdf:type",
                             "object": PROD + "User"}])
    r.append(check("'rdf:type' → rdf:type full URI",
                   len(valid) == 1 and valid[0]["predicate"] == RDF_T))

    # 알 수 없는 predicate → 제외 + 경고
    valid, warnings = v.validate([{"subject": PROD + "s", "predicate": "visited",
                                    "object": "어딘가"}])
    r.append(check("알 수 없는 'visited' → 트리플 제외", len(valid) == 0))
    r.append(check("→ 경고 메시지 포함", any("visited" in w for w in warnings)))

    # 완전히 알 수 없는 full URI → 제외
    valid, warnings = v.validate([{"subject": PROD + "s",
                                    "predicate": "http://unknown.org/prop",
                                    "object": "x"}])
    r.append(check("미정의 full URI predicate → 제외",
                   len(valid) == 0 and len(warnings) >= 1))

    return all(r)


# ── Test 2: 스키마 검증 ───────────────────────────────────────────────────────

def test_schema_validation() -> bool:
    print("\n[Test 2] 스키마 검증 (미정의 predicate 제외)")
    v = V()
    r = []

    # valid 1 + unknown 1 혼합
    triples = [
        {"subject": PROD + "u", "predicate": "uid",         "object": "u1"},
        {"subject": PROD + "u", "predicate": "unknownProp", "object": "x"},
    ]
    valid, warnings = v.validate(triples)
    r.append(check("valid 1 + unknown 1 → valid 1개만 통과", len(valid) == 1))
    r.append(check("→ 경고 1개 (unknownProp)",
                   len([w for w in warnings if "unknownProp" in w]) == 1))

    # 전부 unknown
    triples = [
        {"subject": PROD + "s", "predicate": "foo", "object": "bar"},
        {"subject": PROD + "s", "predicate": "baz", "object": "qux"},
    ]
    valid, warnings = v.validate(triples)
    r.append(check("모두 unknown → valid 0, 경고 2개",
                   len(valid) == 0 and len(warnings) >= 2))

    return all(r)


# ── Test 3: 범위 제약 검증 ────────────────────────────────────────────────────

def test_range_validation() -> bool:
    print("\n[Test 3] 범위 제약 검증")
    v = V()
    r = []

    def t(subj_local: str, pred_local: str, obj: str, xsd_type: str | None = None):
        rec = {"subject": PROD + subj_local,
               "predicate": PROD + pred_local,
               "object": obj}
        if xsd_type:
            rec["datatype"] = XSD + xsd_type
        return rec

    # duration: 0.0~24.0
    valid, w = v.validate([t("sl", "duration", "25.0", "float")])
    r.append(check("duration=25.0 → 제외", len(valid) == 0 and any("duration" in x for x in w)))

    valid, _ = v.validate([t("sl", "duration", "7.5", "float")])
    r.append(check("duration=7.5 → 통과", len(valid) == 1))

    valid, w = v.validate([t("sl", "duration", "0.0", "float")])
    r.append(check("duration=0.0 → 제외 (수면 시간 비정상)",
                   len(valid) == 0 and any("수면 시간 비정상" in x for x in w)))

    # quality: 0~100
    valid, w = v.validate([t("sl", "quality", "101", "integer")])
    r.append(check("quality=101 → 제외", len(valid) == 0 and any("quality" in x for x in w)))

    valid, _ = v.validate([t("sl", "quality", "100", "integer")])
    r.append(check("quality=100 (경계) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "quality", "0", "integer")])
    r.append(check("quality=0 (경계) → 통과", len(valid) == 1))

    # count: 0 이상
    valid, w = v.validate([t("sc", "count", "-1", "integer")])
    r.append(check("count=-1 → 제외", len(valid) == 0 and any("count" in x for x in w)))

    valid, _ = v.validate([t("sc", "count", "0", "integer")])
    r.append(check("count=0 (경계) → 통과", len(valid) == 1))

    # temperature: -50~60
    valid, _ = v.validate([t("w", "temperature", "61.0", "float")])
    r.append(check("temperature=61.0 → 제외", len(valid) == 0))

    valid, _ = v.validate([t("w", "temperature", "-51.0", "float")])
    r.append(check("temperature=-51.0 → 제외", len(valid) == 0))

    valid, _ = v.validate([t("w", "temperature", "-50.0", "float")])
    r.append(check("temperature=-50.0 (경계) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("w", "temperature", "60.0", "float")])
    r.append(check("temperature=60.0 (경계) → 통과", len(valid) == 1))

    # humidity: 0~100
    valid, _ = v.validate([t("w", "humidity", "100.1", "float")])
    r.append(check("humidity=100.1 → 제외", len(valid) == 0))

    valid, _ = v.validate([t("w", "humidity", "0.0", "float")])
    r.append(check("humidity=0.0 → 통과", len(valid) == 1))

    return all(r)


# ── Test 4: 타입 자동 변환 ────────────────────────────────────────────────────

def test_type_coercion() -> bool:
    print("\n[Test 4] 타입 자동 변환")
    v = V()
    r = []

    sl = PROD + "sl_tc"
    q  = PROD + "q_tc"

    # "5.5" (no datatype) + range=float → xsd:float
    valid, _ = v.validate([{"subject": sl, "predicate": PROD + "duration", "object": "5.5"}])
    r.append(check("'5.5' + range=float → xsd:float 추론",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("float")))

    # "72" + range=integer → xsd:integer
    valid, _ = v.validate([{"subject": sl, "predicate": PROD + "quality", "object": "72"}])
    r.append(check("'72' + range=integer → xsd:integer 추론",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("integer")))

    # "true" + range=boolean → xsd:boolean
    valid, _ = v.validate([{"subject": q, "predicate": PROD + "isCompleted", "object": "true"}])
    r.append(check("'true' + range=boolean → xsd:boolean",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("boolean")))

    # "False" → 소문자 "false"^^xsd:boolean
    valid, _ = v.validate([{"subject": q, "predicate": PROD + "isCompleted", "object": "False"}])
    r.append(check("'False' → 'false'^^xsd:boolean 정규화",
                   len(valid) == 1 and valid[0]["object"] == "false"))

    # "1" + range=boolean → "true"
    valid, _ = v.validate([{"subject": q, "predicate": PROD + "isCompleted", "object": "1"}])
    r.append(check("'1' + range=boolean → 'true'^^xsd:boolean",
                   len(valid) == 1 and valid[0]["object"] == "true"))

    # datatype 이미 있으면 그대로
    valid, _ = v.validate([{"subject": sl, "predicate": PROD + "duration", "object": "6.0",
                             "datatype": XSD + "float"}])
    r.append(check("datatype 이미 있으면 그대로 유지",
                   len(valid) == 1 and valid[0]["datatype"] == XSD + "float"))

    # count="300" + range=integer → xsd:integer
    valid, _ = v.validate([{"subject": PROD + "sc", "predicate": PROD + "count", "object": "300"}])
    r.append(check("'300' + range=integer → xsd:integer",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("integer")))

    # range 없는 숫자 "42" → xsd:integer 자동 추론
    valid, _ = v.validate([{"subject": PROD + "u", "predicate": PROD + "visitCount",
                             "object": "42"}])
    r.append(check("range 없는 '42' → xsd:integer 자동 추론",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("integer")))

    # range 없는 소수 "3.14" → xsd:float 자동 추론
    valid, _ = v.validate([{"subject": PROD + "u", "predicate": PROD + "latitude",
                             "object": "3.14"}])
    r.append(check("range 없는 '3.14' → xsd:float 자동 추론",
                   len(valid) == 1 and valid[0].get("datatype", "").endswith("float")))

    return all(r)


# ── Test 5: User 연결 감지 ────────────────────────────────────────────────────

def test_user_connection() -> bool:
    print("\n[Test 5] User 연결 감지")
    v = V()
    r = []

    user  = PROD + "user_conn"
    sleep = PROD + "sl_conn"

    # 고립 노드: hasSleepData 연결 없음 → 경고
    triples = [
        {"subject": user,  "predicate": PROD + "uid", "object": "conn"},
        {"subject": sleep, "predicate": PROD + "duration", "object": "6.5",
         "datatype": XSD + "float"},
    ]
    _, warnings = v.validate(triples)
    r.append(check("sleep 노드 미연결 → 고립 경고 발생",
                   any(sleep in w for w in warnings), f"warnings: {warnings}"))

    # hasSleepData로 연결 → 고립 경고 없음
    triples = [
        {"subject": user,  "predicate": PROD + "uid", "object": "conn"},
        {"subject": user,  "predicate": PROD + "hasSleepData", "object": sleep},
        {"subject": sleep, "predicate": PROD + "duration", "object": "6.5",
         "datatype": XSD + "float"},
    ]
    _, warnings = v.validate(triples)
    orphan_w = [w for w in warnings if "고립" in w]
    r.append(check("hasSleepData로 연결된 노드 → 고립 경고 없음",
                   len(orphan_w) == 0, f"unexpected: {orphan_w}"))

    # rdf:type prod:User로 User 식별 → 미연결 sleep은 여전히 고립
    triples = [
        {"subject": user,  "predicate": RDF_T, "object": PROD + "User"},
        {"subject": sleep, "predicate": PROD + "duration", "object": "7.0",
         "datatype": XSD + "float"},
    ]
    _, warnings = v.validate(triples)
    r.append(check("rdf:type prod:User 식별 + 미연결 sleep → 고립 경고",
                   any(sleep in w for w in warnings)))

    # User 노드 자체는 고립 경고 없음
    triples = [
        {"subject": user, "predicate": PROD + "uid", "object": "conn"},
        {"subject": user, "predicate": PROD + "name", "object": "홍길동"},
    ]
    _, warnings = v.validate(triples)
    orphan_w = [w for w in warnings if "고립" in w]
    r.append(check("User 노드만 있으면 고립 경고 없음",
                   len(orphan_w) == 0))

    return all(r)


# ── Test 6: 빈 입력 및 엣지 케이스 ───────────────────────────────────────────

def test_edge_cases() -> bool:
    print("\n[Test 6] 빈 입력 및 엣지 케이스")
    v = V()
    r = []

    # 빈 리스트
    valid, warnings = v.validate([])
    r.append(check("빈 리스트 → valid 0, warnings 0",
                   len(valid) == 0 and len(warnings) == 0))

    # predicate 누락 (빈 문자열)
    valid, warnings = v.validate([{"subject": PROD + "s", "predicate": "", "object": "x"}])
    r.append(check("predicate 빈 문자열 → 제외 + 경고",
                   len(valid) == 0 and len(warnings) >= 1))

    # 동일 트리플 중복 입력 → 둘 다 통과 (중복 제거는 graph에서 처리)
    t = {"subject": PROD + "u", "predicate": PROD + "uid", "object": "u1"}
    valid, _ = v.validate([t, t])
    r.append(check("동일 트리플 2개 입력 → 2개 모두 통과 (graph dedup은 엔진 역할)",
                   len(valid) == 2))

    return all(r)


# ── Test 7: 통합 시나리오 ─────────────────────────────────────────────────────

def test_integration() -> bool:
    print("\n[Test 7] 통합 시나리오 (복합 입력)")
    v = V()
    r = []

    user  = PROD + "user_int"
    sleep = PROD + "sl_int"
    sc    = PROD + "sc_int"

    triples = [
        # ✓ 사용자 uid
        {"subject": user, "predicate": "uid", "object": "int_user"},
        # ✓ hasSleepData 연결 (Object Property)
        {"subject": user, "predicate": "hasSleepData", "object": sleep},
        # ✓ duration — 타입 변환 필요 (no datatype)
        {"subject": sleep, "predicate": "duration", "object": "5.5"},
        # ✓ quality — 타입 변환 필요
        {"subject": sleep, "predicate": "quality", "object": "45"},
        # ✗ duration 범위 위반 (>24)
        {"subject": sleep, "predicate": "duration", "object": "30.0"},
        # ✗ 알 수 없는 predicate
        {"subject": user, "predicate": "unknownField", "object": "x"},
        # ✓ count — 타입 변환
        {"subject": user, "predicate": "hasStepCount", "object": sc},
        {"subject": sc,   "predicate": "count", "object": "8500"},
    ]
    valid, warnings = v.validate(triples)

    r.append(check("8개 입력 → 6개 통과 (범위위반 1 + unknown 1 제외)",
                   len(valid) == 6, f"실제: {len(valid)}개"))
    r.append(check("경고 2개 이상 생성",
                   len(warnings) >= 2, f"경고 수: {len(warnings)}"))
    r.append(check("duration '5.5' → xsd:float 추론",
                   any(t["predicate"].endswith("duration") and
                       t.get("datatype", "").endswith("float") for t in valid)))
    r.append(check("quality '45' → xsd:integer 추론",
                   any(t["predicate"].endswith("quality") and
                       t.get("datatype", "").endswith("integer") for t in valid)))
    r.append(check("count '8500' → xsd:integer 추론",
                   any(t["predicate"].endswith("count") and
                       t.get("datatype", "").endswith("integer") for t in valid)))
    r.append(check("모든 predicate가 full URI로 정규화됨",
                   all(t["predicate"].startswith("http") for t in valid)))
    r.append(check("고립 경고 없음 (sc는 hasStepCount로 연결됨)",
                   not any("고립" in w for w in warnings)))

    return all(r)


# ── Test 8: 시간대 검증 (timestamp, visitTime, date) ──────────────────────────

def test_timestamp_validation() -> bool:
    print("\n[Test 8] 시간대 검증 (ISO 8601 / YYYY-MM-DD)")
    v = V()
    r = []

    loc = PROD + "loc_ts"
    sleep = PROD + "sl_ts"
    sc = PROD + "sc_ts"

    # ── timestamp 검증 ──

    # 유효: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00"}])
    r.append(check("timestamp='2026-05-13T14:30:00' → 통과",
                   len(valid) == 1))

    # 유효: Z 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00Z"}])
    r.append(check("timestamp='2026-05-13T14:30:00Z' → 통과",
                   len(valid) == 1))

    # 유효: +09:00 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00+09:00"}])
    r.append(check("timestamp='2026-05-13T14:30:00+09:00' → 통과",
                   len(valid) == 1))

    # 무효: 시간만 (HH:MM 형식)
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "14:30"}])
    r.append(check("timestamp='14:30' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 무효: 텍스트 ("2pm")
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2pm"}])
    r.append(check("timestamp='2pm' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 무효: Unix timestamp (숫자만)
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "1747123456"}])
    r.append(check("timestamp='1747123456' (Unix) → 제외",
                   len(valid) == 0))

    # 무효: 빈 문자열
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": ""}])
    r.append(check("timestamp='' → 제외",
                   len(valid) == 0))

    # ── visitTime 검증 ──

    # 유효: ISO 8601
    valid, _ = v.validate([{"subject": loc, "predicate": "visitTime",
                             "object": "2026-05-13T18:45:00"}])
    r.append(check("visitTime='2026-05-13T18:45:00' → 통과",
                   len(valid) == 1))

    # 무효: afternoon (텍스트)
    valid, w = v.validate([{"subject": loc, "predicate": "visitTime",
                             "object": "afternoon"}])
    r.append(check("visitTime='afternoon' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # ── date 검증 ──

    # 유효: YYYY-MM-DD
    valid, _ = v.validate([{"subject": sc, "predicate": "date",
                             "object": "2026-05-13"}])
    r.append(check("date='2026-05-13' → 통과",
                   len(valid) == 1))

    # 무효: MM/DD/YYYY
    valid, w = v.validate([{"subject": sc, "predicate": "date",
                             "object": "05/13/2026"}])
    r.append(check("date='05/13/2026' → 제외",
                   len(valid) == 0 and any("날짜 형식" in x for x in w)))

    # 무효: DD-MM-YYYY
    valid, w = v.validate([{"subject": sc, "predicate": "date",
                             "object": "13-05-2026"}])
    r.append(check("date='13-05-2026' → 제외",
                   len(valid) == 0))

    # 무효: YYYYMMDD (하이픈 없음)
    valid, w = v.validate([{"subject": sc, "predicate": "date",
                             "object": "20260513"}])
    r.append(check("date='20260513' → 제외",
                   len(valid) == 0))

    # ── 미래 timestamp 경고 (제외는 안 함) ──

    # 미래 날짜 (2030년)
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2030-01-01T00:00:00"}])
    future_warnings = [x for x in w if "미래 시각" in x]
    r.append(check("timestamp='2030-01-01...' → 통과하지만 미래 경고",
                   len(valid) == 1 and len(future_warnings) == 1))

    # 현재 날짜 (경고 없어야 함)
    from datetime import datetime
    now_str = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": now_str}])
    future_warnings = [x for x in w if "미래 시각" in x]
    r.append(check("timestamp=현재 시각 → 미래 경고 없음",
                   len(valid) == 1 and len(future_warnings) == 0))

    return all(r)


# ── Test 9: 수면 시간 특별 검증 (0.5~18.0) ─────────────────────────────────────

def test_sleep_duration_special() -> bool:
    print("\n[Test 9] 수면 시간 특별 검증 (0.5~18.0 시간)")
    v = V()
    r = []

    sleep = PROD + "sl_dur"

    # 유효: 0.5 (경계)
    valid, _ = v.validate([{"subject": sleep, "predicate": "duration", "object": "0.5"}])
    r.append(check("duration=0.5 (경계) → 통과", len(valid) == 1))

    # 유효: 18.0 (경계)
    valid, _ = v.validate([{"subject": sleep, "predicate": "duration", "object": "18.0"}])
    r.append(check("duration=18.0 (경계) → 통과", len(valid) == 1))

    # 유효: 7.5 (정상)
    valid, _ = v.validate([{"subject": sleep, "predicate": "duration", "object": "7.5"}])
    r.append(check("duration=7.5 → 통과", len(valid) == 1))

    # 무효: 0.3 (< 0.5)
    valid, w = v.validate([{"subject": sleep, "predicate": "duration", "object": "0.3"}])
    r.append(check("duration=0.3 → 제외 (비정상)",
                   len(valid) == 0 and any("수면 시간 비정상" in x for x in w)))

    # 무효: 0.0
    valid, w = v.validate([{"subject": sleep, "predicate": "duration", "object": "0.0"}])
    r.append(check("duration=0.0 → 제외 (비정상)",
                   len(valid) == 0))

    # 무효: 20.0 (> 18.0)
    valid, w = v.validate([{"subject": sleep, "predicate": "duration", "object": "20.0"}])
    r.append(check("duration=20.0 → 제외 (비정상)",
                   len(valid) == 0 and any("수면 시간 비정상" in x for x in w)))

    # 무효: 24.5 (범위 검사에서 먼저 걸림)
    valid, w = v.validate([{"subject": sleep, "predicate": "duration", "object": "24.5"}])
    r.append(check("duration=24.5 → 제외 (범위 위반 먼저)",
                   len(valid) == 0 and any("범위 위반" in x for x in w)))

    return all(r)


# ── Test 10: 타임존 혼합 및 밀리초 엣지케이스 ────────────────────────────────

def test_timezone_millisecond_edge_cases() -> bool:
    print("\n[Test 10] 타임존 혼합 및 밀리초 엣지케이스")
    v = V()
    r = []

    loc = PROD + "loc_edge"

    # 밀리초 + Z 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.123Z"}])
    r.append(check("밀리초 + Z 타임존 → 통과", len(valid) == 1))

    # 밀리초 + +09:00 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.456+09:00"}])
    r.append(check("밀리초 + +09:00 타임존 → 통과", len(valid) == 1))

    # 밀리초 + -05:00 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.789-05:00"}])
    r.append(check("밀리초 + -05:00 타임존 → 통과", len(valid) == 1))

    # 밀리초만 (타임존 없음)
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.999"}])
    r.append(check("밀리초만 (타임존 없음) → 통과", len(valid) == 1))

    # 6자리 밀리초 (마이크로초)
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.123456"}])
    r.append(check("6자리 밀리초 (마이크로초) → 통과", len(valid) == 1))

    # 6자리 밀리초 + 타임존
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.123456+09:00"}])
    r.append(check("6자리 밀리초 + 타임존 → 통과", len(valid) == 1))

    # 미래 timestamp (밀리초 포함) — 경고만
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2030-12-31T23:59:59.999Z"}])
    future_warnings = [x for x in w if "미래 시각" in x]
    r.append(check("미래 timestamp (밀리초+Z) → 통과 + 미래 경고",
                   len(valid) == 1 and len(future_warnings) == 1))

    # 미래 timestamp (밀리초 + +09:00)
    valid, w = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2030-01-01T00:00:00.001+09:00"}])
    future_warnings = [x for x in w if "미래 시각" in x]
    r.append(check("미래 timestamp (밀리초++09:00) → 통과 + 미래 경고",
                   len(valid) == 1 and len(future_warnings) == 1))

    # 0 밀리초 (명시적)
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.000"}])
    r.append(check("0 밀리초 명시적 → 통과", len(valid) == 1))

    # 1자리 밀리초
    valid, _ = v.validate([{"subject": loc, "predicate": "timestamp",
                             "object": "2026-05-13T14:30:00.1Z"}])
    r.append(check("1자리 밀리초 + Z → 통과", len(valid) == 1))

    return all(r)


# ── Test 11: GPS 좌표 범위 검증 ───────────────────────────────────────────────

def test_gps_coordinates() -> bool:
    print("\n[Test 11] GPS 좌표 범위 검증 (latitude/longitude)")
    v = V()
    r = []

    photo = PROD + "photo_gps"

    # ── latitude 검증 ──

    # 정례: 서울 위도 (37.5665)
    valid, _ = v.validate([{"subject": photo, "predicate": "latitude", "object": "37.5665"}])
    r.append(check("latitude=37.5665 (서울) → 통과", len(valid) == 1))

    # 정례: 뉴욕 위도 (40.7128)
    valid, _ = v.validate([{"subject": photo, "predicate": "latitude", "object": "40.7128"}])
    r.append(check("latitude=40.7128 (뉴욕) → 통과", len(valid) == 1))

    # 경계: 북극 (+90.0)
    valid, _ = v.validate([{"subject": photo, "predicate": "latitude", "object": "90.0"}])
    r.append(check("latitude=90.0 (북극 경계) → 통과", len(valid) == 1))

    # 경계: 남극 (-90.0)
    valid, _ = v.validate([{"subject": photo, "predicate": "latitude", "object": "-90.0"}])
    r.append(check("latitude=-90.0 (남극 경계) → 통과", len(valid) == 1))

    # 반례: 범위 초과 (200.5)
    valid, w = v.validate([{"subject": photo, "predicate": "latitude", "object": "200.5"}])
    r.append(check("latitude=200.5 → 제외 (범위 위반)",
                   len(valid) == 0 and any("latitude" in x and "범위 위반" in x for x in w)))

    # 반례: 범위 미달 (-100.0)
    valid, w = v.validate([{"subject": photo, "predicate": "latitude", "object": "-100.0"}])
    r.append(check("latitude=-100.0 → 제외 (범위 위반)",
                   len(valid) == 0 and any("latitude" in x for x in w)))

    # 반례: 경계 초과 (90.1)
    valid, w = v.validate([{"subject": photo, "predicate": "latitude", "object": "90.1"}])
    r.append(check("latitude=90.1 → 제외", len(valid) == 0))

    # 반례: 경계 미달 (-90.1)
    valid, w = v.validate([{"subject": photo, "predicate": "latitude", "object": "-90.1"}])
    r.append(check("latitude=-90.1 → 제외", len(valid) == 0))

    # ── longitude 검증 ──

    # 정례: 서울 경도 (126.9780)
    valid, _ = v.validate([{"subject": photo, "predicate": "longitude", "object": "126.9780"}])
    r.append(check("longitude=126.9780 (서울) → 통과", len(valid) == 1))

    # 정례: 뉴욕 경도 (-74.0060)
    valid, _ = v.validate([{"subject": photo, "predicate": "longitude", "object": "-74.0060"}])
    r.append(check("longitude=-74.0060 (뉴욕) → 통과", len(valid) == 1))

    # 경계: 동쪽 경계 (+180.0)
    valid, _ = v.validate([{"subject": photo, "predicate": "longitude", "object": "180.0"}])
    r.append(check("longitude=180.0 (동쪽 경계) → 통과", len(valid) == 1))

    # 경계: 서쪽 경계 (-180.0)
    valid, _ = v.validate([{"subject": photo, "predicate": "longitude", "object": "-180.0"}])
    r.append(check("longitude=-180.0 (서쪽 경계) → 통과", len(valid) == 1))

    # 반례: 범위 초과 (-500.0)
    valid, w = v.validate([{"subject": photo, "predicate": "longitude", "object": "-500.0"}])
    r.append(check("longitude=-500.0 → 제외 (범위 위반)",
                   len(valid) == 0 and any("longitude" in x and "범위 위반" in x for x in w)))

    # 반례: 범위 초과 (300.0)
    valid, w = v.validate([{"subject": photo, "predicate": "longitude", "object": "300.0"}])
    r.append(check("longitude=300.0 → 제외 (범위 위반)",
                   len(valid) == 0 and any("longitude" in x for x in w)))

    # 반례: 경계 초과 (180.1)
    valid, w = v.validate([{"subject": photo, "predicate": "longitude", "object": "180.1"}])
    r.append(check("longitude=180.1 → 제외", len(valid) == 0))

    # 반례: 경계 미달 (-180.1)
    valid, w = v.validate([{"subject": photo, "predicate": "longitude", "object": "-180.1"}])
    r.append(check("longitude=-180.1 → 제외", len(valid) == 0))

    # ── 복합 검증 (latitude + longitude) ──

    # 정례: 서울 GPS 좌표
    triples = [
        {"subject": photo, "predicate": "latitude",  "object": "37.5665"},
        {"subject": photo, "predicate": "longitude", "object": "126.9780"},
    ]
    valid, _ = v.validate(triples)
    r.append(check("서울 GPS 좌표 (lat+lon) → 2개 모두 통과", len(valid) == 2))

    # 반례: latitude 정상 + longitude 비정상
    triples = [
        {"subject": photo, "predicate": "latitude",  "object": "37.5665"},
        {"subject": photo, "predicate": "longitude", "object": "500.0"},
    ]
    valid, w = v.validate(triples)
    r.append(check("latitude 정상 + longitude 비정상 → 1개만 통과",
                   len(valid) == 1 and any("longitude" in x for x in w)))

    # 반례: latitude 비정상 + longitude 정상
    triples = [
        {"subject": photo, "predicate": "latitude",  "object": "100.0"},
        {"subject": photo, "predicate": "longitude", "object": "126.9780"},
    ]
    valid, w = v.validate(triples)
    r.append(check("latitude 비정상 + longitude 정상 → 1개만 통과",
                   len(valid) == 1 and any("latitude" in x for x in w)))

    # 반례: 둘 다 비정상
    triples = [
        {"subject": photo, "predicate": "latitude",  "object": "200.0"},
        {"subject": photo, "predicate": "longitude", "object": "-300.0"},
    ]
    valid, w = v.validate(triples)
    r.append(check("lat+lon 둘 다 비정상 → 0개 통과, 경고 2개",
                   len(valid) == 0 and len([x for x in w if "범위 위반" in x]) == 2))

    # ── Location subject 검증 (GPS 기반 방문 장소) ──

    loc_node = PROD + "loc_gps"

    # 정례: 서울 위도 (Location subject)
    valid, _ = v.validate([{"subject": loc_node, "predicate": "latitude", "object": "37.5665"}])
    r.append(check("Location latitude=37.5665 (서울) → 통과", len(valid) == 1))

    # 정례: 서울 경도 (Location subject)
    valid, _ = v.validate([{"subject": loc_node, "predicate": "longitude", "object": "126.9780"}])
    r.append(check("Location longitude=126.9780 (서울) → 통과", len(valid) == 1))

    # 경계: 북극 위도 (Location subject)
    valid, _ = v.validate([{"subject": loc_node, "predicate": "latitude", "object": "90.0"}])
    r.append(check("Location latitude=90.0 (북극 경계) → 통과", len(valid) == 1))

    # 경계: 서쪽 경계 경도 (Location subject)
    valid, _ = v.validate([{"subject": loc_node, "predicate": "longitude", "object": "-180.0"}])
    r.append(check("Location longitude=-180.0 (서쪽 경계) → 통과", len(valid) == 1))

    # 반례: Location latitude 범위 초과
    valid, w = v.validate([{"subject": loc_node, "predicate": "latitude", "object": "91.0"}])
    r.append(check("Location latitude=91.0 → 제외 (범위 위반)",
                   len(valid) == 0 and any("latitude" in x and "범위 위반" in x for x in w)))

    # 반례: Location longitude 범위 초과
    valid, w = v.validate([{"subject": loc_node, "predicate": "longitude", "object": "200.0"}])
    r.append(check("Location longitude=200.0 → 제외 (범위 위반)",
                   len(valid) == 0 and any("longitude" in x and "범위 위반" in x for x in w)))

    # 정례: Location GPS 좌표 복합 (lat+lon)
    triples = [
        {"subject": loc_node, "predicate": "latitude",  "object": "35.6762"},
        {"subject": loc_node, "predicate": "longitude", "object": "139.6503"},
    ]
    valid, _ = v.validate(triples)
    r.append(check("Location 도쿄 GPS (lat+lon) → 2개 모두 통과", len(valid) == 2))

    # ── 문자열 반례 (숫자 변환 불가 → 제외 + 경고) ──

    # weather 노드 (온도 문자열 반례)
    weather_node = PROD + "weather_str"

    # temperature 문자열 반례
    valid, w = v.validate([{"subject": weather_node, "predicate": "temperature",
                             "object": "hot"}])
    r.append(check("temperature='hot' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("temperature" in x for x in w)))

    # latitude 문자열 반례
    valid, w = v.validate([{"subject": photo, "predicate": "latitude",
                             "object": "north"}])
    r.append(check("latitude='north' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("latitude" in x for x in w)))

    # longitude 문자열 반례
    valid, w = v.validate([{"subject": photo, "predicate": "longitude",
                             "object": "east"}])
    r.append(check("longitude='east' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("longitude" in x for x in w)))

    # humidity 문자열 반례
    valid, w = v.validate([{"subject": weather_node, "predicate": "humidity",
                             "object": "wet"}])
    r.append(check("humidity='wet' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("humidity" in x for x in w)))

    return all(r)


# ── Test 12: 확장 시간 속성 검증 (playedAt, recordedAt, startTime, endTime) ──

def test_extended_datetime_props() -> bool:
    print("\n[Test 12] 확장 시간 속성 검증 (playedAt / recordedAt / startTime / endTime)")
    v = V()
    r = []

    music   = PROD + "music_ext"
    weather = PROD + "weather_ext"
    cal     = PROD + "cal_ext"

    # ── playedAt (MusicListening 재생 시각) ──

    # 정례: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "2026-05-14T09:00:00"}])
    r.append(check("playedAt='2026-05-14T09:00:00' → 통과", len(valid) == 1))

    # 정례: Z 타임존
    valid, _ = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "2026-05-14T09:00:00Z"}])
    r.append(check("playedAt='2026-05-14T09:00:00Z' → 통과", len(valid) == 1))

    # 정례: +09:00 타임존
    valid, _ = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "2026-05-14T09:00:00+09:00"}])
    r.append(check("playedAt='2026-05-14T09:00:00+09:00' → 통과", len(valid) == 1))

    # 정례: 밀리초 포함
    valid, _ = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "2026-05-14T09:00:00.123Z"}])
    r.append(check("playedAt 밀리초+Z → 통과", len(valid) == 1))

    # 반례: 날짜만 (YYYY-MM-DD)
    valid, w = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "2026-05-14"}])
    r.append(check("playedAt='2026-05-14' (날짜만) → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 텍스트
    valid, w = v.validate([{"subject": music, "predicate": "playedAt",
                             "object": "morning"}])
    r.append(check("playedAt='morning' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # ── recordedAt (Weather 기록 시각) ──

    # 정례: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": weather, "predicate": "recordedAt",
                             "object": "2026-05-14T06:00:00"}])
    r.append(check("recordedAt='2026-05-14T06:00:00' → 통과", len(valid) == 1))

    # 정례: Z 타임존
    valid, _ = v.validate([{"subject": weather, "predicate": "recordedAt",
                             "object": "2026-05-14T06:00:00Z"}])
    r.append(check("recordedAt='2026-05-14T06:00:00Z' → 통과", len(valid) == 1))

    # 정례: +09:00 타임존
    valid, _ = v.validate([{"subject": weather, "predicate": "recordedAt",
                             "object": "2026-05-14T06:00:00+09:00"}])
    r.append(check("recordedAt='2026-05-14T06:00:00+09:00' → 통과", len(valid) == 1))

    # 반례: Unix timestamp (숫자)
    valid, w = v.validate([{"subject": weather, "predicate": "recordedAt",
                             "object": "1747123456"}])
    r.append(check("recordedAt='1747123456' (Unix) → 제외",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 빈 문자열
    valid, w = v.validate([{"subject": weather, "predicate": "recordedAt",
                             "object": ""}])
    r.append(check("recordedAt='' → 제외",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # ── startTime (CalendarEvent 시작 시각) ──

    # 정례: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": cal, "predicate": "startTime",
                             "object": "2026-05-14T10:00:00"}])
    r.append(check("startTime='2026-05-14T10:00:00' → 통과", len(valid) == 1))

    # 정례: Z 타임존
    valid, _ = v.validate([{"subject": cal, "predicate": "startTime",
                             "object": "2026-05-14T10:00:00Z"}])
    r.append(check("startTime='2026-05-14T10:00:00Z' → 통과", len(valid) == 1))

    # 정례: 밀리초 + 타임존
    valid, _ = v.validate([{"subject": cal, "predicate": "startTime",
                             "object": "2026-05-14T10:00:00.500+09:00"}])
    r.append(check("startTime 밀리초++09:00 → 통과", len(valid) == 1))

    # 반례: HH:MM 형식 (날짜 없음)
    valid, w = v.validate([{"subject": cal, "predicate": "startTime",
                             "object": "10:00"}])
    r.append(check("startTime='10:00' (시간만) → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 텍스트 날짜 표현
    valid, w = v.validate([{"subject": cal, "predicate": "startTime",
                             "object": "May 14 2026"}])
    r.append(check("startTime='May 14 2026' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # ── endTime (CalendarEvent 종료 시각) ──

    # 정례: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": cal, "predicate": "endTime",
                             "object": "2026-05-14T11:00:00"}])
    r.append(check("endTime='2026-05-14T11:00:00' → 통과", len(valid) == 1))

    # 정례: Z 타임존
    valid, _ = v.validate([{"subject": cal, "predicate": "endTime",
                             "object": "2026-05-14T11:00:00Z"}])
    r.append(check("endTime='2026-05-14T11:00:00Z' → 통과", len(valid) == 1))

    # 정례: -05:00 타임존
    valid, _ = v.validate([{"subject": cal, "predicate": "endTime",
                             "object": "2026-05-14T11:00:00-05:00"}])
    r.append(check("endTime='2026-05-14T11:00:00-05:00' → 통과", len(valid) == 1))

    # 반례: YYYY/MM/DD 슬래시 형식
    valid, w = v.validate([{"subject": cal, "predicate": "endTime",
                             "object": "2026/05/14T11:00:00"}])
    r.append(check("endTime='2026/05/14T11:00:00' (슬래시) → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 빈 문자열
    valid, w = v.validate([{"subject": cal, "predicate": "endTime",
                             "object": ""}])
    r.append(check("endTime='' → 제외",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # ── 복합: startTime + endTime 동시 검증 ──

    # 정례: 시작/종료 모두 유효
    triples = [
        {"subject": cal, "predicate": "startTime", "object": "2026-05-14T10:00:00+09:00"},
        {"subject": cal, "predicate": "endTime",   "object": "2026-05-14T11:00:00+09:00"},
    ]
    valid, w = v.validate(triples)
    r.append(check("startTime + endTime 둘 다 유효 → 2개 통과", len(valid) == 2))

    # 반례: startTime 유효 + endTime 무효
    triples = [
        {"subject": cal, "predicate": "startTime", "object": "2026-05-14T10:00:00"},
        {"subject": cal, "predicate": "endTime",   "object": "invalid-time"},
    ]
    valid, w = v.validate(triples)
    r.append(check("startTime 유효 + endTime 무효 → 1개만 통과",
                   len(valid) == 1 and any("시간 형식" in x and "endTime" in x for x in w)))

    return all(r)


# ── Test 13: updatedAt 시간 속성 검증 ────────────────────────────────────────

def test_updatedAt_prop() -> bool:
    print("\n[Test 13] updatedAt 시간 속성 검증 (Persona 갱신 시각)")
    v = V()
    r = []

    persona = PROD + "persona_upd"

    # 정례: ISO 8601 기본 형식
    valid, _ = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2026-05-14T09:00:00"}])
    r.append(check("updatedAt='2026-05-14T09:00:00' → 통과", len(valid) == 1))

    # 정례: Z 타임존
    valid, _ = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2026-05-14T09:00:00Z"}])
    r.append(check("updatedAt='2026-05-14T09:00:00Z' → 통과", len(valid) == 1))

    # 정례: +09:00 타임존
    valid, _ = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2026-05-14T09:00:00+09:00"}])
    r.append(check("updatedAt='2026-05-14T09:00:00+09:00' → 통과", len(valid) == 1))

    # 정례: 밀리초 포함
    valid, _ = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2026-05-14T09:00:00.123Z"}])
    r.append(check("updatedAt 밀리초 포함 → 통과", len(valid) == 1))

    # 반례: 날짜만 (YYYY-MM-DD) → 제외 + 경고
    valid, w = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2026-05-14"}])
    r.append(check("updatedAt='2026-05-14' (날짜만) → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 빈 문자열 → 제외
    valid, w = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": ""}])
    r.append(check("updatedAt='' → 제외",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 반례: 텍스트 → 제외 + 경고
    valid, w = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "yesterday"}])
    r.append(check("updatedAt='yesterday' → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    # 미래 시각 경고 (통과하지만 경고 발생)
    valid, w = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "2030-01-01T00:00:00Z"}])
    future_warnings = [x for x in w if "미래 시각" in x]
    r.append(check("updatedAt 미래 시각 → 통과하지만 미래 경고",
                   len(valid) == 1 and len(future_warnings) == 1))

    # Unix timestamp 반례
    valid, w = v.validate([{"subject": persona, "predicate": "updatedAt",
                             "object": "1747123456"}])
    r.append(check("updatedAt='1747123456' (Unix) → 제외 + 경고",
                   len(valid) == 0 and any("시간 형식" in x for x in w)))

    return all(r)


# ── Test 14: 숫자 범위 검증 (count / duration / quality / deepSleepRatio /
#             usageDuration / visitCount) ─────────────────────────────────────

def test_numeric_ranges() -> bool:
    print("\n[Test 14] 숫자 범위 검증 (NUMERIC_RANGES 전체)")
    v = V()
    r = []

    def t(subj_local: str, pred_local: str, obj: str):
        return {"subject": PROD + subj_local,
                "predicate": PROD + pred_local,
                "object": obj}

    # ── count (걸음수: 0 ~ 100_000) ──

    valid, _ = v.validate([t("sc", "count", "5000")])
    r.append(check("count=5000 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sc", "count", "0")])
    r.append(check("count=0 (경계 최솟값) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sc", "count", "100000")])
    r.append(check("count=100000 (경계 최댓값) → 통과", len(valid) == 1))

    valid, w = v.validate([t("sc", "count", "-1")])
    r.append(check("count=-1 (음수) → 제외 + 경고",
                   len(valid) == 0 and any("count" in x for x in w)))

    valid, w = v.validate([t("sc", "count", "100001")])
    r.append(check("count=100001 (상한 초과) → 제외 + 경고",
                   len(valid) == 0 and any("count" in x for x in w)))

    valid, w = v.validate([t("sc", "count", "abc")])
    r.append(check("count='abc' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("count" in x for x in w)))

    # ── duration (수면 시간: 0.0 ~ 24.0, 실용 검증은 0.5~18.0) ──
    # duration은 NUMERIC_RANGES 내에 있으나 수면 특별 검증도 적용됨
    # 0.0 은 RANGE_BOUNDS 통과(경계값)하지만 수면 특별 검증에서 제외
    # 24.0 은 RANGE_BOUNDS 경계값이므로 통과, 수면 특별 검증(>18.0)에서 제외

    valid, _ = v.validate([t("sl", "duration", "7.5")])
    r.append(check("duration=7.5 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "duration", "0.5")])
    r.append(check("duration=0.5 (수면 최솟값 경계) → 통과", len(valid) == 1))

    valid, w = v.validate([t("sl", "duration", "-0.5")])
    r.append(check("duration=-0.5 (RANGE_BOUNDS 위반) → 제외",
                   len(valid) == 0 and any("duration" in x for x in w)))

    valid, w = v.validate([t("sl", "duration", "24.1")])
    r.append(check("duration=24.1 (상한 초과) → 제외",
                   len(valid) == 0 and any("duration" in x for x in w)))

    # ── quality (수면 질: 0 ~ 100) ──

    valid, _ = v.validate([t("sl", "quality", "75")])
    r.append(check("quality=75 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "quality", "0")])
    r.append(check("quality=0 (경계 최솟값) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "quality", "100")])
    r.append(check("quality=100 (경계 최댓값) → 통과", len(valid) == 1))

    valid, w = v.validate([t("sl", "quality", "-1")])
    r.append(check("quality=-1 → 제외 + 경고",
                   len(valid) == 0 and any("quality" in x for x in w)))

    valid, w = v.validate([t("sl", "quality", "101")])
    r.append(check("quality=101 → 제외 + 경고",
                   len(valid) == 0 and any("quality" in x for x in w)))

    # ── deepSleepRatio (0.0 ~ 1.0) ──

    valid, _ = v.validate([t("sl", "deepSleepRatio", "0.3")])
    r.append(check("deepSleepRatio=0.3 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "deepSleepRatio", "0.0")])
    r.append(check("deepSleepRatio=0.0 (경계 최솟값) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("sl", "deepSleepRatio", "1.0")])
    r.append(check("deepSleepRatio=1.0 (경계 최댓값) → 통과", len(valid) == 1))

    valid, w = v.validate([t("sl", "deepSleepRatio", "-0.1")])
    r.append(check("deepSleepRatio=-0.1 → 제외 + 경고",
                   len(valid) == 0 and any("deepSleepRatio" in x for x in w)))

    valid, w = v.validate([t("sl", "deepSleepRatio", "1.1")])
    r.append(check("deepSleepRatio=1.1 → 제외 + 경고",
                   len(valid) == 0 and any("deepSleepRatio" in x for x in w)))

    # ── usageDuration (앱 사용 분: 0 ~ 1440) ──

    valid, _ = v.validate([t("au", "usageDuration", "90")])
    r.append(check("usageDuration=90 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("au", "usageDuration", "0")])
    r.append(check("usageDuration=0 (경계 최솟값) → 통과", len(valid) == 1))

    valid, _ = v.validate([t("au", "usageDuration", "1440")])
    r.append(check("usageDuration=1440 (경계 최댓값, 24시간) → 통과", len(valid) == 1))

    valid, w = v.validate([t("au", "usageDuration", "-1")])
    r.append(check("usageDuration=-1 → 제외 + 경고",
                   len(valid) == 0 and any("usageDuration" in x for x in w)))

    valid, w = v.validate([t("au", "usageDuration", "1441")])
    r.append(check("usageDuration=1441 → 제외 + 경고",
                   len(valid) == 0 and any("usageDuration" in x for x in w)))

    # ── visitCount (방문 횟수: 1 ~ 10_000) ──

    valid, _ = v.validate([t("loc", "visitCount", "3")])
    r.append(check("visitCount=3 → 통과", len(valid) == 1))

    valid, _ = v.validate([t("loc", "visitCount", "1")])
    r.append(check("visitCount=1 (경계 최솟값) → 통과", len(valid) == 1))

    valid, w = v.validate([t("loc", "visitCount", "0")])
    r.append(check("visitCount=0 (최솟값 미만) → 제외 + 경고",
                   len(valid) == 0 and any("visitCount" in x for x in w)))

    valid, w = v.validate([t("loc", "visitCount", "-1")])
    r.append(check("visitCount=-1 → 제외 + 경고",
                   len(valid) == 0 and any("visitCount" in x for x in w)))

    # visitCount 상한 경계값/초과
    loc = PROD + "loc_rng"
    valid, _ = v.validate([{"subject": loc, "predicate": "visitCount", "object": "10000"}])
    r.append(check("visitCount=10000 (경계 최댓값) → 통과", len(valid) == 1))

    valid, w = v.validate([{"subject": loc, "predicate": "visitCount", "object": "10001"}])
    r.append(check("visitCount=10001 (초과) → 제외 + 경고",
                   len(valid) == 0 and any("visitCount" in x for x in w)))

    # ── amount (보상 금액: 0 ~ 1_000_000) ──
    reward = PROD + "reward_rng"

    valid, _ = v.validate([{"subject": reward, "predicate": "amount", "object": "500"}])
    r.append(check("amount=500 → 통과", len(valid) == 1))

    valid, _ = v.validate([{"subject": reward, "predicate": "amount", "object": "0"}])
    r.append(check("amount=0 (경계 최솟값) → 통과", len(valid) == 1))

    valid, _ = v.validate([{"subject": reward, "predicate": "amount", "object": "1000000"}])
    r.append(check("amount=1000000 (경계 최댓값) → 통과", len(valid) == 1))

    valid, w = v.validate([{"subject": reward, "predicate": "amount", "object": "-1"}])
    r.append(check("amount=-1 → 제외 + 경고",
                   len(valid) == 0 and any("amount" in x for x in w)))

    valid, w = v.validate([{"subject": reward, "predicate": "amount", "object": "1000001"}])
    r.append(check("amount=1000001 (초과) → 제외 + 경고",
                   len(valid) == 0 and any("amount" in x for x in w)))

    valid, w = v.validate([{"subject": reward, "predicate": "amount", "object": "abc"}])
    r.append(check("amount='abc' → 제외 + 경고",
                   len(valid) == 0 and any("amount" in x for x in w)))

    # ── duration 문자열 반례 ──
    valid, w = v.validate([t("sl", "duration", "abc")])
    r.append(check("duration='abc' (문자열) → 제외 + 경고",
                   len(valid) == 0 and any("duration" in x for x in w)))

    return all(r)


# ── Test 15: validate_required_properties (필수 속성 누락 감지) ──────────────

def test_validate_required_properties() -> bool:
    print("\n[Test 15] validate_required_properties (필수 속성 누락 감지)")
    from rdflib import Graph, RDF, URIRef, Literal
    from rdflib.namespace import XSD

    PROD_NS = "http://7team.dev/ontology#"
    r = []

    def make_uri(local: str) -> URIRef:
        return URIRef(PROD_NS + local)

    # ── 정례: User uid 있음 → WARNING 없음 ──
    g = Graph()
    user = make_uri("user_ok")
    g.add((user, RDF.type, make_uri("User")))
    g.add((user, make_uri("uid"), Literal("u001")))
    warnings = validate_required_properties(g)
    uid_warnings = [w for w in warnings if "uid" in w and "user_ok" in w]
    r.append(check("User uid 있음 → uid WARNING 없음",
                   len(uid_warnings) == 0, f"warnings: {warnings}"))

    # ── 반례: User uid 없음 → WARNING 포함 ──
    g2 = Graph()
    user2 = make_uri("user_no_uid")
    g2.add((user2, RDF.type, make_uri("User")))
    g2.add((user2, make_uri("name"), Literal("홍길동")))
    warnings2 = validate_required_properties(g2)
    r.append(check("User uid 없음 → [WARNING] User ... uid 누락 포함",
                   any("uid 누락" in w and "user_no_uid" in w for w in warnings2),
                   f"warnings: {warnings2}"))
    r.append(check("WARNING 메시지 형식 '[WARNING] User ...: uid 누락'",
                   any(w.startswith("[WARNING] User") and "uid 누락" in w
                       for w in warnings2)))

    # ── 반례: Quest title 없음 → WARNING 포함 ──
    g3 = Graph()
    quest = make_uri("quest_no_title")
    g3.add((quest, RDF.type, make_uri("Quest")))
    g3.add((quest, make_uri("isCompleted"), Literal("false", datatype=XSD.boolean)))
    warnings3 = validate_required_properties(g3)
    r.append(check("Quest title 없음 → title WARNING 포함",
                   any("title 누락" in w and "quest_no_title" in w for w in warnings3),
                   f"warnings: {warnings3}"))

    # ── 정례: Quest title 있음 → WARNING 없음 ──
    g4 = Graph()
    quest2 = make_uri("quest_ok")
    g4.add((quest2, RDF.type, make_uri("Quest")))
    g4.add((quest2, make_uri("title"), Literal("산책하기")))
    warnings4 = validate_required_properties(g4)
    title_warns = [w for w in warnings4 if "title" in w and "quest_ok" in w]
    r.append(check("Quest title 있음 → title WARNING 없음",
                   len(title_warns) == 0, f"warnings: {warnings4}"))

    # ── 반례: SleepData duration 없음 → WARNING 포함 ──
    g5 = Graph()
    sleep = make_uri("sleep_no_dur")
    g5.add((sleep, RDF.type, make_uri("SleepData")))
    g5.add((sleep, make_uri("quality"), Literal(75, datatype=XSD.integer)))
    warnings5 = validate_required_properties(g5)
    r.append(check("SleepData duration 없음 → duration WARNING 포함",
                   any("duration 누락" in w and "sleep_no_dur" in w for w in warnings5),
                   f"warnings: {warnings5}"))

    # ── 정례: SleepData duration 있음 → WARNING 없음 ──
    g6 = Graph()
    sleep2 = make_uri("sleep_ok")
    g6.add((sleep2, RDF.type, make_uri("SleepData")))
    g6.add((sleep2, make_uri("duration"), Literal(7.5, datatype=XSD.float)))
    warnings6 = validate_required_properties(g6)
    dur_warns = [w for w in warnings6 if "duration" in w and "sleep_ok" in w]
    r.append(check("SleepData duration 있음 → duration WARNING 없음",
                   len(dur_warns) == 0, f"warnings: {warnings6}"))

    # ── 반례: AppUsage appName+usageDuration 모두 없음 → WARNING 2개 ──
    g7 = Graph()
    app = make_uri("app_no_props")
    g7.add((app, RDF.type, make_uri("AppUsage")))
    warnings7 = validate_required_properties(g7)
    app_warns = [w for w in warnings7 if "app_no_props" in w]
    r.append(check("AppUsage appName+usageDuration 모두 없음 → WARNING 2개",
                   len(app_warns) == 2, f"경고 수: {len(app_warns)}, warnings: {app_warns}"))
    r.append(check("appName 누락 경고 포함",
                   any("appName 누락" in w for w in app_warns)))
    r.append(check("usageDuration 누락 경고 포함",
                   any("usageDuration 누락" in w for w in app_warns)))

    # ── 정례: AppUsage 필수 속성 모두 있음 → WARNING 없음 ──
    g8 = Graph()
    app2 = make_uri("app_ok")
    g8.add((app2, RDF.type, make_uri("AppUsage")))
    g8.add((app2, make_uri("appName"), Literal("YouTube")))
    g8.add((app2, make_uri("usageDuration"), Literal(90, datatype=XSD.integer)))
    warnings8 = validate_required_properties(g8)
    app2_warns = [w for w in warnings8 if "app_ok" in w]
    r.append(check("AppUsage 필수 속성 모두 있음 → WARNING 없음",
                   len(app2_warns) == 0, f"warnings: {app2_warns}"))

    # ── 정례: 빈 그래프 → WARNING 없음 ──
    g9 = Graph()
    warnings9 = validate_required_properties(g9)
    r.append(check("빈 그래프 → WARNING 없음",
                   len(warnings9) == 0, f"warnings: {warnings9}"))

    # ── 정례: 모든 필수 속성 있는 복합 그래프 → WARNING 없음 ──
    g10 = Graph()
    u = make_uri("user_full")
    sl = make_uri("sleep_full")
    sc = make_uri("sc_full")
    au = make_uri("app_full")
    q = make_uri("quest_full")
    g10.add((u,  RDF.type, make_uri("User")))
    g10.add((u,  make_uri("uid"), Literal("full_user")))
    g10.add((sl, RDF.type, make_uri("SleepData")))
    g10.add((sl, make_uri("duration"), Literal(8.0, datatype=XSD.float)))
    g10.add((sc, RDF.type, make_uri("StepCount")))
    g10.add((sc, make_uri("count"), Literal(8000, datatype=XSD.integer)))
    g10.add((au, RDF.type, make_uri("AppUsage")))
    g10.add((au, make_uri("appName"), Literal("Spotify")))
    g10.add((au, make_uri("usageDuration"), Literal(60, datatype=XSD.integer)))
    g10.add((q,  RDF.type, make_uri("Quest")))
    g10.add((q,  make_uri("title"), Literal("스트레칭 10분")))
    warnings10 = validate_required_properties(g10)
    r.append(check("복합 그래프 필수 속성 모두 있음 → WARNING 없음",
                   len(warnings10) == 0, f"warnings: {warnings10}"))

    # ── 반례: StepCount count 없음 → WARNING 포함 ──
    g11 = Graph()
    sc2 = make_uri("sc_no_count")
    g11.add((sc2, RDF.type, make_uri("StepCount")))
    g11.add((sc2, make_uri("date"), Literal("2026-05-14")))
    warnings11 = validate_required_properties(g11)
    r.append(check("StepCount count 없음 → count WARNING 포함",
                   any("count 누락" in w and "sc_no_count" in w for w in warnings11),
                   f"warnings: {warnings11}"))

    return all(r)


# ── Test 16: listenDuration 범위 검증 (MusicListening.listenDuration: 1~1440) ──

def test_listen_duration_range() -> None:
    print("\n[Test 16] listenDuration 범위 검증 (MusicListening.listenDuration: 1~1440분)")
    v = V()

    music = PROD + "music_ld"

    def t(obj: str):
        return {"subject": music, "predicate": PROD + "listenDuration", "object": obj}

    # ── 정상값 ──

    # 정례: 30분 (일반적인 청취 시간)
    valid, _ = v.validate([t("30")])
    assert check("listenDuration=30 (정상) → 통과", len(valid) == 1)

    # 정례: 120분 (MusicMood 추론 임계값)
    valid, _ = v.validate([t("120")])
    assert check("listenDuration=120 (MusicMood 임계값) → 통과", len(valid) == 1)

    # 정례: 180분 (3시간, FocusMode 추론 임계값)
    valid, _ = v.validate([t("180")])
    assert check("listenDuration=180 (FocusMode 임계값) → 통과", len(valid) == 1)

    # ── 경계값 ──

    # 하한 경계값: 1분
    valid, _ = v.validate([t("1")])
    assert check("listenDuration=1 (하한 경계값) → 통과", len(valid) == 1)

    # 상한 경계값: 1440분 (24시간)
    valid, _ = v.validate([t("1440")])
    assert check("listenDuration=1440 (상한 경계값, 24시간) → 통과", len(valid) == 1)

    # ── 비정상값 ──

    # 하한 미만: 0분 (1분 미만은 청취 기록 불필요)
    valid, w = v.validate([t("0")])
    assert check("listenDuration=0 (하한 미만) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))

    # 상한 초과: 1441분
    valid, w = v.validate([t("1441")])
    assert check("listenDuration=1441 (상한 초과) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))

    # 음수: -1분
    valid, w = v.validate([t("-1")])
    assert check("listenDuration=-1 (음수) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))

    # 문자열: 숫자 변환 불가
    valid, w = v.validate([t("long")])
    assert check("listenDuration='long' (문자열) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))

    # ── float 입력 케이스 ──
    # listenDuration은 int 타입(RANGE_BOUNDS 기준). float 문자열 입력 시
    # int() 변환에서 ValueError 발생 → _check_numeric_range가 "숫자 변환 실패" 경고 반환
    # → 트리플 제외됨.

    # float 입력: "1.5" → int 변환 불가 → 제외 + 경고
    valid, w = v.validate([t("1.5")])
    assert check("listenDuration='1.5' (float 문자열) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))

    # float 입력: "120.0" → int 변환 불가 → 제외 + 경고
    valid, w = v.validate([t("120.0")])
    assert check("listenDuration='120.0' (float 문자열) → 제외 + 경고",
                 len(valid) == 0 and any("listenDuration" in x for x in w))


# ── Test 17: _validate_subject_uri 직접 검증 ─────────────────────────────────

def test_validate_subject_uri() -> None:
    print("\n[Test 17] _validate_subject_uri (subject URI 형식 검증)")

    # 정례: 유효한 http:// URI → 빈 리스트
    result = _validate_subject_uri("http://7team.dev/ontology#user1")
    assert check("유효한 http:// URI → 경고 없음",
                 result == [], f"실제: {result}")

    # 정례: 유효한 https:// URI → 빈 리스트
    result = _validate_subject_uri("https://example.com/resource/1")
    assert check("유효한 https:// URI → 경고 없음",
                 result == [], f"실제: {result}")

    # 반례: 빈 문자열 → 경고 반환
    result = _validate_subject_uri("")
    assert check("빈 문자열 → 경고 반환",
                 len(result) >= 1 and any("비어있거나 공백" in w for w in result),
                 f"실제: {result}")

    # 반례: 공백만 있는 문자열 → 경고 반환
    result = _validate_subject_uri("   ")
    assert check("공백만 있는 문자열 → 경고 반환",
                 len(result) >= 1 and any("비어있거나 공백" in w for w in result),
                 f"실제: {result}")

    # 반례: URI에 공백 포함 → 경고 반환
    result = _validate_subject_uri("http://7team.dev/ontology#user 1")
    assert check("URI에 공백 포함 → 공백 경고 반환",
                 len(result) >= 1 and any("공백 포함" in w for w in result),
                 f"실제: {result}")

    # 반례: 상대 URI (http:// 없음) → 경고 반환
    result = _validate_subject_uri("user1")
    assert check("상대 URI 'user1' → 절대 URI 아님 경고",
                 len(result) >= 1 and any("절대 URI가 아님" in w for w in result),
                 f"실제: {result}")

    # 반례: 상대 URI (prod: 접두사) → 경고 반환
    result = _validate_subject_uri("prod:user1")
    assert check("상대 URI 'prod:user1' → 절대 URI 아님 경고",
                 len(result) >= 1 and any("절대 URI가 아님" in w for w in result),
                 f"실제: {result}")

    # 반례: 특수문자 포함 URI (공백 없음, http:// 있음) → 경고 없음
    # (특수문자 자체는 URI 스펙상 인코딩 가능, 공백만 명시적으로 검사)
    result = _validate_subject_uri("http://7team.dev/ontology#user-1_test")
    assert check("http:// URI + 하이픈/언더스코어 → 경고 없음",
                 result == [], f"실제: {result}")

    # 반례: 공백 포함 상대 URI → 경고 2개 이상 (공백 포함 + 절대 URI 아님)
    result = _validate_subject_uri("user name")
    assert len(result) >= 2, f"공백+상대URI는 경고 2개 이상: {result}"

    # validate()와 연동: subject URI 문제 시 트리플 제외 + 경고 누적
    v = V()

    # 빈 subject → 트리플 제외 + 경고
    valid, warnings = v.validate([{"subject": "", "predicate": PROD + "uid", "object": "u1"}])
    assert check("validate(): subject='' → 트리플 제외",
                 len(valid) == 0, f"실제 valid: {len(valid)}")
    assert check("validate(): subject='' → 경고 포함",
                 any("비어있거나 공백" in w for w in warnings),
                 f"실제 warnings: {warnings}")

    # 상대 URI subject → 트리플 제외 + 경고
    valid, warnings = v.validate([{"subject": "relative_uri", "predicate": PROD + "uid",
                                   "object": "u1"}])
    assert check("validate(): subject='relative_uri' → 트리플 제외",
                 len(valid) == 0, f"실제 valid: {len(valid)}")
    assert check("validate(): subject='relative_uri' → 절대 URI 경고",
                 any("절대 URI가 아님" in w for w in warnings),
                 f"실제 warnings: {warnings}")

    # 공백 포함 subject → 트리플 제외 + 경고
    valid, warnings = v.validate([{"subject": "http://7team.dev/ontology#user 1",
                                   "predicate": PROD + "uid", "object": "u1"}])
    assert check("validate(): subject에 공백 포함 → 트리플 제외",
                 len(valid) == 0, f"실제 valid: {len(valid)}")
    assert check("validate(): subject에 공백 포함 → 공백 경고",
                 any("공백 포함" in w for w in warnings),
                 f"실제 warnings: {warnings}")

    # 유효한 subject → 정상 통과
    valid, warnings = v.validate([{"subject": PROD + "user1", "predicate": PROD + "uid",
                                   "object": "u1"}])
    subj_warns = [w for w in warnings if "subject URI" in w]
    assert check("validate(): 유효한 subject → subject URI 경고 없음",
                 len(subj_warns) == 0, f"실제 subject URI 경고: {subj_warns}")


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 60)
    print("triple_validator.py 단위 테스트")
    print("=" * 60)

    test_groups = [
        ("predicate 정규화",               test_predicate_normalization),
        ("스키마 검증",                     test_schema_validation),
        ("범위 제약 검증",                   test_range_validation),
        ("타입 자동 변환",                   test_type_coercion),
        ("User 연결 감지",                  test_user_connection),
        ("엣지 케이스",                      test_edge_cases),
        ("통합 시나리오",                     test_integration),
        ("시간대 검증",                      test_timestamp_validation),
        ("수면 시간 특별 검증",               test_sleep_duration_special),
        ("타임존 혼합 및 밀리초 엣지케이스",  test_timezone_millisecond_edge_cases),
        ("GPS 좌표 범위 검증",               test_gps_coordinates),
        ("확장 시간 속성 검증",               test_extended_datetime_props),
        ("updatedAt 시간 속성 검증",         test_updatedAt_prop),
        ("숫자 범위 검증",                   test_numeric_ranges),
        ("필수 속성 누락 감지",               test_validate_required_properties),
        ("listenDuration 범위 검증",         test_listen_duration_range),
        ("subject URI 형식 검증",            test_validate_subject_uri),
    ]

    passed = 0
    failed: list[str] = []
    for name, fn in test_groups:
        try:
            result = fn()
            # assert 기반 테스트(None 반환)와 bool 반환 테스트 모두 지원
            ok = True if result is None else bool(result)
        except (AssertionError, Exception) as exc:
            import traceback
            print(f"  [EXCEPTION] {name}: {exc}")
            traceback.print_exc()
            ok = False
        if ok:
            passed += 1
        else:
            failed.append(name)

    print("\n" + "=" * 60)
    total = len(test_groups)
    print(f"결과: {passed}/{total} 테스트 그룹 통과")
    if failed:
        print(f"실패 그룹: {failed}")
        sys.exit(1)
    else:
        print("모든 테스트 통과")


if __name__ == "__main__":
    main()
