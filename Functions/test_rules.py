"""
test_rules.py
추론 규칙 단위 테스트 — Firebase 없이 RDFLib만으로 실행

Usage:
    pip install rdflib
    python Functions/test_rules.py
"""

import re
import sys
from pathlib import Path
from rdflib import Graph, Namespace, RDF, Literal, URIRef
from rdflib.namespace import XSD

PROD = Namespace("http://7team.dev/ontology#")
TTL_PATH  = Path("Functions/ontology/core.ttl")
RULES_PATH = Path("Functions/ontology/rules/inference_rules.sparql")

PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"


# ── 헬퍼 ────────────────────────────────────────────────────────────────────

def load_base_graph() -> Graph:
    g = Graph()
    g.bind("prod", PROD)
    g.parse(str(TTL_PATH), format="turtle")
    return g


def parse_rules(rules_text: str) -> dict[str, str]:
    pattern = re.compile(
        r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)"
    )
    return {m.group(1): m.group(2).strip() for m in pattern.finditer(rules_text)}


def apply_rule(g: Graph, sparql: str) -> list:
    """CONSTRUCT 실행 후 새 트리플 목록 반환."""
    result = list(g.query(sparql))
    for triple in result:
        g.add(triple)
    return result


def check(label: str, condition: bool, detail: str = "") -> bool:
    tag = PASS if condition else FAIL
    suffix = f"  ↳ {detail}" if detail else ""
    print(f"  [{tag}] {label}{suffix}")
    return condition


# ── 테스트 케이스 ────────────────────────────────────────────────────────────

def test_fatigue_risk(rules: dict[str, str]) -> bool:
    """
    Rule 1: 수면 6시간 미만 + 카페인(갤러리 placeType=cafe) 2회 초과
             → prod:FatigueRisk 상태 부여
    """
    print("\n[Test 1] FatigueRisk 규칙")
    g = load_base_graph()

    user = PROD["user_test1"]
    g.add((user, RDF.type, PROD.User))
    g.add((user, PROD.uid, Literal("test1")))

    # 수면 5.5시간 (< 6.0)
    sleep = PROD["sleep_test1"]
    g.add((sleep, RDF.type, PROD.SleepData))
    g.add((sleep, PROD.duration, Literal(5.5, datatype=XSD.float)))
    g.add((sleep, PROD.quality, Literal(70, datatype=XSD.integer)))
    g.add((user, PROD.hasSleepData, sleep))

    # 카페 갤러리 사진 3장 (> 2)
    for i in range(3):
        photo = PROD[f"photo_test1_{i}"]
        g.add((photo, RDF.type, PROD.GalleryPhoto))
        g.add((photo, PROD.placeType, Literal("cafe")))
        g.add((user, PROD.hasGalleryPhoto, photo))

    triples = apply_rule(g, rules["fatigue_risk"])

    has_state = (user, PROD.hasState, PROD.FatigueRisk) in g
    ok1 = check("FatigueRisk 트리플 생성됨", has_state,
                f"수면 5.5h, 카페 사진 3장 → {len(triples)}개 트리플")

    # 반례: 수면 충분 (7시간) → FatigueRisk 미생성
    g2 = load_base_graph()
    user2 = PROD["user_test1b"]
    g2.add((user2, RDF.type, PROD.User))
    sleep2 = PROD["sleep_test1b"]
    g2.add((sleep2, RDF.type, PROD.SleepData))
    g2.add((sleep2, PROD.duration, Literal(7.0, datatype=XSD.float)))
    g2.add((user2, PROD.hasSleepData, sleep2))
    for i in range(3):
        photo = PROD[f"photo_test1b_{i}"]
        g2.add((photo, RDF.type, PROD.GalleryPhoto))
        g2.add((photo, PROD.placeType, Literal("cafe")))
        g2.add((user2, PROD.hasGalleryPhoto, photo))
    apply_rule(g2, rules["fatigue_risk"])
    no_state = (user2, PROD.hasState, PROD.FatigueRisk) not in g2
    ok2 = check("수면 7h일 때 FatigueRisk 미생성 (반례)", no_state)

    return ok1 and ok2


def test_sedentary_pattern(rules: dict[str, str]) -> bool:
    """
    Rule 3: 걸음수 3000보 미만 3일 연속
             → prod:SedentaryPattern + '30분 산책하기' 퀘스트 생성
    """
    print("\n[Test 2] SedentaryPattern + 산책 퀘스트 규칙")
    g = load_base_graph()

    user = PROD["user_test2"]
    g.add((user, RDF.type, PROD.User))
    g.add((user, PROD.uid, Literal("test2")))

    # 3일치 걸음수, 모두 3000 미만
    for i, cnt in enumerate([1200, 2500, 800]):
        sc = PROD[f"steps_test2_{i}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
        g.add((sc, PROD.date,
               Literal(f"2026-04-{17+i}", datatype=XSD.date)))
        g.add((user, PROD.hasStepCount, sc))

    apply_rule(g, rules["sedentary_pattern"])

    has_pattern = (user, PROD.hasState, PROD.SedentaryPattern) in g
    ok1 = check("SedentaryPattern 상태 부여됨",
                has_pattern, "걸음수 1200/2500/800보")

    quest_titles = [
        str(o)
        for s, p, o in g.triples((None, PROD.title, None))
    ]
    has_quest = "30분 산책하기" in quest_titles
    ok2 = check("'30분 산책하기' 퀘스트 생성됨", has_quest,
                f"생성된 Quest title 목록: {quest_titles}")

    quest_linked = any(
        True
        for q in g.objects(user, PROD.receivesQuest)
        if (q, PROD.title, Literal("30분 산책하기")) in g
    )
    ok3 = check("퀘스트가 User에 연결됨 (receivesQuest)", quest_linked)

    # 반례: 2일만 3000 미만 → 규칙 미작동
    g2 = load_base_graph()
    user2 = PROD["user_test2b"]
    g2.add((user2, RDF.type, PROD.User))
    for i, cnt in enumerate([800, 5000]):          # 1일만 낮음
        sc = PROD[f"steps_test2b_{i}"]
        g2.add((sc, RDF.type, PROD.StepCount))
        g2.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
        g2.add((user2, PROD.hasStepCount, sc))
    apply_rule(g2, rules["sedentary_pattern"])
    no_pattern = (user2, PROD.hasState, PROD.SedentaryPattern) not in g2
    ok4 = check("2일치만 낮을 때 SedentaryPattern 미생성 (반례)", no_pattern)

    return ok1 and ok2 and ok3 and ok4


def test_rule_isolation(rules: dict[str, str]) -> bool:
    """
    규칙 파서: 6개 규칙 ID 모두 추출됐는지 확인
    """
    print("\n[Test 3] 규칙 파서 — 6개 RULE_ID 추출 확인")
    expected = {
        "fatigue_risk", "burnout_warning", "sedentary_pattern",
        "place_habit", "late_caffeine_sleep_quality", "missing_companion",
    }
    extracted = set(rules.keys())
    ok = check(f"6개 규칙 추출됨 ({len(extracted)}개)",
               extracted == expected,
               f"추출된 ID: {sorted(extracted)}")
    return ok


# ── 경고: 규칙 설계 한계 ─────────────────────────────────────────────────────

def warn_design_issues() -> None:
    print("\n[경고] 현재 규칙 설계상 알려진 한계")
    issues = [
        ("Rule 3 — '3일 연속' 미검증",
         "현재 SPARQL은 날짜 순서 무관하게 COUNT만 함. "
         "비연속 3일도 규칙이 발동됨. "
         "해결: date 정렬 후 연속성 체크 로직 추가 필요."),
        ("Rule 2 — BurnoutWarning 선행 조건",
         "Rule 1이 같은 배치 내에서 먼저 실행돼야 FatigueRisk가 그래프에 존재함. "
         "ontology_engine.py의 RULE_ORDER 순서 의존성 주의."),
        ("Rule 1 — 카페인 근사값",
         "카페인을 GalleryPhoto.placeType='cafe' 개수로 추정함. "
         "AppUsage(커피 앱) 데이터와 결합하면 정확도 향상 가능."),
    ]
    for title, desc in issues:
        print(f"  ⚠  {title}")
        print(f"       {desc}")


# ── 메인 ────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 60)
    print("inference_rules.sparql 단위 테스트")
    print("=" * 60)

    rules_text = RULES_PATH.read_text(encoding="utf-8")
    rules = parse_rules(rules_text)

    results = [
        test_rule_isolation(rules),
        test_fatigue_risk(rules),
        test_sedentary_pattern(rules),
    ]

    warn_design_issues()

    passed = sum(results)
    total  = len(results)
    print(f"\n결과: {passed}/{total} 테스트 통과")
    sys.exit(0 if passed == total else 1)


if __name__ == "__main__":
    main()
