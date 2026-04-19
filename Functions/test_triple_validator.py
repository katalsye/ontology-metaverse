"""
test_triple_validator.py
TripleValidator 단위 테스트 — 7개 그룹, 40개 assertion
Firebase 없이 RDFLib만으로 실행

Usage:
    python Functions/test_triple_validator.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from triple_validator import TripleValidator

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

    valid, _ = v.validate([t("sl", "duration", "0.0", "float")])
    r.append(check("duration=0.0 (경계) → 통과", len(valid) == 1))

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


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 60)
    print("triple_validator.py 단위 테스트")
    print("=" * 60)

    test_groups = [
        ("predicate 정규화",    test_predicate_normalization),
        ("스키마 검증",          test_schema_validation),
        ("범위 제약 검증",        test_range_validation),
        ("타입 자동 변환",        test_type_coercion),
        ("User 연결 감지",       test_user_connection),
        ("엣지 케이스",           test_edge_cases),
        ("통합 시나리오",          test_integration),
    ]

    passed = 0
    failed: list[str] = []
    for name, fn in test_groups:
        try:
            ok = fn()
        except Exception as exc:
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
