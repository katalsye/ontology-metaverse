"""
validate_ontology.py
온톨로지 수정 후 반드시 실행 — 구조/무결성 검증 스크립트

검사 항목:
  [0] core.ttl 파싱
  [1] 클래스 존재 확인
  [2] 속성 존재 확인
  [3] owl:disjointWith 선언 확인
  [4] owl:minCardinality >= 1 확인
  [5] 데이터 범위 제약(owl:withRestrictions) 확인
  [6] rdfs:domain owl:unionOf 멤버 확인
  [7] SPARQL 규칙 ID 존재 확인
  [8] SPARQL CONSTRUCT 블록 파싱 확인

Usage:
    python validate_ontology.py
"""

import sys
import re
from pathlib import Path

from rdflib import Graph, Namespace, RDF, OWL

PROD = Namespace("http://7team.dev/ontology#")
TTL_PATH = Path("Functions/ontology/core.ttl")
RULES_PATH = Path("Functions/ontology/rules/inference_rules.sparql")

REQUIRED_CLASSES = [
    "User", "SleepData", "StepCount", "AppUsage",
    "GalleryPhoto", "Location", "Activity", "Quest",
    "Reward", "Persona", "RoomObject",
    "FatigueRisk", "BurnoutWarning", "SedentaryPattern", "PlaceHabit",
    "MusicListening", "CalendarEvent", "Weather",
    "IndoorDayPattern", "Routine", "MusicMood",
    "SleepQualityImpaired", "ExerciseSkipped", "WeeklyActivityLow",
    "FocusMode", "StressIndicator", "SocialActivity",
    "ScheduleOverload",
]

REQUIRED_PROPERTIES = [
    "uid", "name", "duration", "quality", "count", "date",
    "appName", "usageDuration", "placeName", "visitCount",
    "activityType", "title", "questType", "isCompleted",
    "amount", "rewardType", "energyType", "objectType",
    "hasSleepData", "hasStepCount", "receivesQuest", "gives",
    # 확장
    "trackName", "artist", "genre", "playedAt", "listenDuration",
    "eventTitle", "startTime", "endTime", "isRecurring",
    "temperature", "condition", "humidity", "recordedAt",
    "listensTo", "hasCalendarEvent", "hasWeather",
    # 빈 노드 감지용 선택 속성
    "emotion", "purpose", "mood", "cause", "review",
    # DuckDB 전처리 결과 주입 속성
    "hasConsecutiveLowStepDays",
    # Location 장소 유형
    "placeType",
]

DISJOINT_PAIRS = [
    ("SleepData",     "StepCount"),
    ("Quest",         "Reward"),
    ("FatigueRisk",   "BurnoutWarning"),
    ("CalendarEvent", "MusicListening"),
    ("FocusMode",     "StressIndicator"),
    ("FocusMode",     "SocialActivity"),
    ("StressIndicator", "SocialActivity"),
]

MIN_CARDINALITY_1 = [
    ("User",           "uid"),
    ("Quest",          "title"),
    ("SleepData",      "duration"),
    ("StepCount",      "count"),
    ("AppUsage",       "appName"),
    ("AppUsage",       "usageDuration"),
    ("MusicListening", "listenDuration"),
    # CalendarEvent
    ("CalendarEvent",  "startTime"),
    # Location
    ("Location",       "visitTime"),
    # Weather
    ("Weather",        "recordedAt"),
]

RANGE_CONSTRAINTS = [
    ("SleepData", "duration"),
    ("SleepData", "quality"),
    ("SleepData", "deepSleepRatio"),
    ("StepCount", "count"),
    ("Weather",   "temperature"),
    ("Weather",   "humidity"),
    ("Location",  "visitCount"),
    ("AppUsage",  "usageDuration"),
    ("Reward",    "amount"),
    ("GalleryPhoto",    "latitude"),
    ("GalleryPhoto",    "longitude"),
    ("Location",        "latitude"),
    ("Location",        "longitude"),
    ("MusicListening",  "listenDuration"),
]

DOMAIN_UNION_CONSTRAINTS = [
    ("latitude",  ["GalleryPhoto", "Location"]),
    ("longitude", ["GalleryPhoto", "Location"]),
    ("placeType", ["GalleryPhoto", "Location"]),
]

RULE_IDS = [
    "fatigue_risk", "burnout_warning", "sedentary_pattern",
    "place_habit", "late_caffeine_sleep_quality", "missing_companion",
    "indoor_day_pattern", "routine_detection", "music_mood",
    "missing_emotion", "missing_purpose", "missing_music_mood",
    "missing_sleep_cause", "missing_event_review",
    "sunny_indoor_quest",
    "causal_sleep_impaired", "causal_exercise_skipped",
    "causal_weekly_activity_low", "causal_burnout_from_chain",
    "persona_active", "persona_indoor", "persona_social",
    "persona_solitary", "persona_routine", "persona_night_owl",
    "focus_music_pattern", "stress_music_pattern", "social_music_pattern",
    "schedule_overload",
]


def check(condition: bool, msg: str) -> bool:
    status = "OK  " if condition else "FAIL"
    print(f"  [{status}] {msg}")
    return condition


def validate_ttl(g: Graph) -> int:
    print("\n[1] core.ttl 클래스 존재 확인")
    failures = 0
    for cls in REQUIRED_CLASSES:
        ok = (PROD[cls], RDF.type, OWL.Class) in g
        if not check(ok, cls):
            failures += 1

    print("\n[2] core.ttl 속성 존재 확인")
    for prop in REQUIRED_PROPERTIES:
        exists = (PROD[prop], None, None) in g
        if not check(exists, prop):
            failures += 1
    return failures


def validate_rules(rules_text: str) -> int:
    print("\n[7] SPARQL 규칙 ID 존재 확인 (inference_rules.sparql 로드 완료)")
    failures = 0
    for rule_id in RULE_IDS:
        found = bool(re.search(rf"RULE_ID:\s*{rule_id}", rules_text))
        if not check(found, rule_id):
            failures += 1
    return failures


def _ask(g: Graph, sparql: str) -> bool:
    return bool(g.query(sparql))


def validate_owl_constraints(g: Graph) -> int:
    failures = 0

    print("\n[3] owl:disjointWith 선언 확인")
    for a, b in DISJOINT_PAIRS:
        ok = (
            (PROD[a], OWL.disjointWith, PROD[b]) in g or
            (PROD[b], OWL.disjointWith, PROD[a]) in g
        )
        if not check(ok, f"{a} ↔ {b}"):
            failures += 1

    print("\n[4] owl:minCardinality ≥ 1 확인")
    for cls, prop in MIN_CARDINALITY_1:
        sparql = f"""
PREFIX prod: <http://7team.dev/ontology#>
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX xsd:  <http://www.w3.org/2001/XMLSchema#>
ASK {{
    prod:{cls} rdfs:subClassOf ?r .
    ?r owl:onProperty prod:{prop} ;
       owl:minCardinality ?n .
    FILTER (?n >= 1)
}}"""
        if not check(_ask(g, sparql), f"{cls}.{prop}"):
            failures += 1

    print("\n[5] 데이터 범위 제약(owl:withRestrictions) 확인")
    for cls, prop in RANGE_CONSTRAINTS:
        sparql = f"""
PREFIX prod: <http://7team.dev/ontology#>
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
ASK {{
    prod:{cls} rdfs:subClassOf ?r .
    ?r owl:onProperty prod:{prop} ;
       owl:allValuesFrom ?dt .
    ?dt owl:withRestrictions ?list .
}}"""
        if not check(_ask(g, sparql), f"{cls}.{prop}"):
            failures += 1

    return failures


def validate_domain_union(g: Graph) -> int:
    """[6] rdfs:domain owl:unionOf 멤버 검증."""
    print("\n[6] rdfs:domain owl:unionOf 멤버 확인")
    failures = 0

    for prop, expected_classes in DOMAIN_UNION_CONSTRAINTS:
        # 1. rdfs:domain 선언 존재 여부
        has_domain = _ask(g, f"""
PREFIX prod: <http://7team.dev/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
ASK {{ prod:{prop} rdfs:domain ?domain . }}""")
        if not check(has_domain, f"{prop} — rdfs:domain 선언 존재"):
            failures += 1
            continue

        # 2. domain이 owl:unionOf를 사용하는지 확인
        has_union = _ask(g, f"""
PREFIX prod: <http://7team.dev/ontology#>
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
ASK {{
  prod:{prop} rdfs:domain ?domain .
  ?domain owl:unionOf ?list .
}}""")
        if not check(has_union, f"{prop} — domain이 owl:unionOf 사용"):
            failures += 1
            continue

        # 3. 각 예상 클래스가 unionOf 멤버에 포함되는지 확인
        for cls in expected_classes:
            member_ok = _ask(g, f"""
PREFIX prod: <http://7team.dev/ontology#>
PREFIX owl:  <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX rdf:  <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK {{
  prod:{prop} rdfs:domain ?domain .
  ?domain owl:unionOf/rdf:rest*/rdf:first prod:{cls} .
}}""")
            if not check(member_ok, f"{prop} — unionOf 멤버에 prod:{cls} 포함"):
                failures += 1

    return failures


def validate_sparql_syntax(g: Graph, rules_text: str) -> int:
    """CONSTRUCT 블록별 파싱 시도."""
    print("\n[8] SPARQL CONSTRUCT 블록 파싱 확인")
    failures = 0
    pattern = re.compile(
        r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)"
    )
    for m in pattern.finditer(rules_text):
        rule_id = m.group(1)
        sparql = m.group(2).strip()
        try:
            g.query(sparql)
            check(True, f"{rule_id} — CONSTRUCT 실행 OK")
        except Exception as exc:
            check(False, f"{rule_id} — {exc}")
            failures += 1
    return failures


def main() -> None:
    total_fail = 0

    # core.ttl 로드
    print("=" * 60)
    print("validate_ontology.py — 온톨로지 무결성 검증")
    print("=" * 60)

    print("\n[0] core.ttl 파싱")
    if not TTL_PATH.exists():
        print(f"  [FAIL] 파일 없음: {TTL_PATH}")
        sys.exit(1)

    g = Graph()
    g.bind("prod", PROD)
    try:
        g.parse(str(TTL_PATH), format="turtle")
        check(True, f"파싱 성공 — {len(g)} 트리플")
    except Exception as exc:
        check(False, f"파싱 실패: {exc}")
        sys.exit(1)

    total_fail += validate_ttl(g)
    total_fail += validate_owl_constraints(g)
    total_fail += validate_domain_union(g)

    # 추론 규칙 파일 로드
    if not RULES_PATH.exists():
        print(f"  [FAIL] 파일 없음: {RULES_PATH}")
        sys.exit(1)

    rules_text = RULES_PATH.read_text(encoding="utf-8")

    total_fail += validate_rules(rules_text)
    total_fail += validate_sparql_syntax(g, rules_text)

    # 결과
    print("\n" + "=" * 60)
    if total_fail == 0:
        print("결과: 모든 검사 통과")
    else:
        print(f"결과: {total_fail}개 항목 실패 — 온톨로지를 수정 후 재실행하세요.")
        sys.exit(1)


if __name__ == "__main__":
    main()
