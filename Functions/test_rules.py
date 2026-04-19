"""
test_rules.py
추론 규칙 단위 테스트 — 25개 규칙 전체 + 엣지케이스
Firebase 없이 RDFLib만으로 실행

Usage:
    python Functions/test_rules.py
"""

import re
import sys
from pathlib import Path
from rdflib import Graph, Namespace, RDF, Literal, URIRef
from rdflib.namespace import XSD

PROD = Namespace("http://7team.dev/ontology#")
TTL_PATH   = Path("Functions/ontology/core.ttl")
RULES_PATH = Path("Functions/ontology/rules/inference_rules.sparql")

PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"
SKIP = "\033[93mSKIP\033[0m"

ALL_RULE_IDS = [
    "fatigue_risk", "burnout_warning", "sedentary_pattern",
    "place_habit", "late_caffeine_sleep_quality", "missing_companion",
    "missing_emotion", "missing_purpose", "missing_music_mood",
    "missing_sleep_cause", "missing_event_review",
    "indoor_day_pattern", "sunny_indoor_quest",
    "routine_detection", "music_mood",
    "causal_sleep_impaired", "causal_exercise_skipped",
    "causal_weekly_activity_low", "causal_burnout_from_chain",
    "persona_active", "persona_indoor", "persona_social",
    "persona_solitary", "persona_routine", "persona_night_owl",
]


# ── 헬퍼 ─────────────────────────────────────────────────────────────────────

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
    result = list(g.query(sparql))
    for triple in result:
        g.add(triple)
    return result


def check(label: str, condition: bool, detail: str = "") -> bool:
    tag = PASS if condition else FAIL
    suffix = f"  ↳ {detail}" if detail else ""
    print(f"  [{tag}] {label}{suffix}")
    return condition


def quest_titles(g: Graph) -> list[str]:
    return [str(o) for _, _, o in g.triples((None, PROD.title, None))]


def persona_prop(g: Graph, prop: URIRef) -> list[str]:
    return [
        str(v)
        for _, p in g.subject_objects(PROD.hasPersona)
        for v in g.objects(p, prop)
    ]


def _add_user(g: Graph, uid: str) -> URIRef:
    user = PROD[f"u_{uid}"]
    g.add((user, RDF.type, PROD.User))
    g.add((user, PROD.uid, Literal(uid)))
    return user


def _add_sleep(g: Graph, user: URIRef, uid: str,
               duration: float, quality: int = 80) -> URIRef:
    s = PROD[f"sl_{uid}"]
    g.add((s, RDF.type, PROD.SleepData))
    g.add((s, PROD.duration, Literal(duration, datatype=XSD.float)))
    g.add((s, PROD.quality, Literal(quality, datatype=XSD.integer)))
    g.add((user, PROD.hasSleepData, s))
    return s


def _add_cafe_location(g: Graph, user: URIRef, uid: str,
                       n: int, hour: int = 10) -> None:
    for i in range(n):
        loc = PROD[f"loc_cafe_{uid}_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeType, Literal("cafe")))
        g.add((loc, PROD.visitTime,
               Literal(f"2026-04-{17+i}T{hour:02d}:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasLocation, loc))


# ── Test 1: rule_parser ───────────────────────────────────────────────────────

def test_rule_parser(rules: dict[str, str]) -> bool:
    print("\n[Test P] 규칙 파서 — 25개 RULE_ID 추출 확인")
    expected = set(ALL_RULE_IDS)
    extracted = set(rules.keys())
    missing = sorted(expected - extracted)
    extra   = sorted(extracted - expected)
    ok = check(f"25개 규칙 추출됨 (실제 {len(extracted)}개)",
               extracted == expected,
               f"누락: {missing}  추가: {extra}" if missing or extra else "")
    return ok


# ── Test 2: fatigue_risk ──────────────────────────────────────────────────────

def test_fatigue_risk(rules: dict[str, str]) -> bool:
    print("\n[Test 1] fatigue_risk")
    results = []

    # 정례 A: 수면 5.5h + 주간 카페 3회 (>2) → FatigueRisk
    g = load_base_graph()
    user = _add_user(g, "r1a")
    _add_sleep(g, user, "r1a", 5.5)
    _add_cafe_location(g, user, "r1a", 3, hour=10)
    apply_rule(g, rules["fatigue_risk"])
    results.append(check("주간 카페 3회 → FatigueRisk",
                         (user, PROD.hasState, PROD.FatigueRisk) in g))

    # 정례 B: 수면 5.5h + 야간 카페 2회 (>1) → FatigueRisk
    g = load_base_graph()
    user = _add_user(g, "r1b")
    _add_sleep(g, user, "r1b", 5.5)
    _add_cafe_location(g, user, "r1b", 2, hour=23)
    apply_rule(g, rules["fatigue_risk"])
    results.append(check("야간 카페 2회 → FatigueRisk",
                         (user, PROD.hasState, PROD.FatigueRisk) in g))

    # 반례 A: 수면 7h → 미생성
    g = load_base_graph()
    user = _add_user(g, "r1c")
    _add_sleep(g, user, "r1c", 7.0)
    _add_cafe_location(g, user, "r1c", 3, hour=10)
    apply_rule(g, rules["fatigue_risk"])
    results.append(check("수면 7h → FatigueRisk 미생성 (반례)",
                         (user, PROD.hasState, PROD.FatigueRisk) not in g))

    # 반례 B: 주간 카페 2회만 (≤2) → 미생성
    g = load_base_graph()
    user = _add_user(g, "r1d")
    _add_sleep(g, user, "r1d", 5.0)
    _add_cafe_location(g, user, "r1d", 2, hour=10)
    apply_rule(g, rules["fatigue_risk"])
    results.append(check("주간 카페 2회 → FatigueRisk 미생성 (반례)",
                         (user, PROD.hasState, PROD.FatigueRisk) not in g))

    return all(results)


# ── Test 3: burnout_warning ───────────────────────────────────────────────────

def test_burnout_warning(rules: dict[str, str]) -> bool:
    print("\n[Test 2] burnout_warning")
    results = []

    # 정례: FatigueRisk + 운동 퀘스트 미완료 3개 → BurnoutWarning
    g = load_base_graph()
    user = _add_user(g, "r2a")
    g.add((user, PROD.hasState, PROD.FatigueRisk))
    for i in range(3):
        q = PROD[f"q_r2a_{i}"]
        g.add((q, RDF.type, PROD.Quest))
        g.add((q, PROD.questType, Literal("운동")))
        g.add((q, PROD.isCompleted, Literal(False, datatype=XSD.boolean)))
        g.add((user, PROD.receivesQuest, q))
    apply_rule(g, rules["burnout_warning"])
    ok1 = check("FatigueRisk + 운동퀘스트 3개 → BurnoutWarning",
                (user, PROD.hasState, PROD.BurnoutWarning) in g)
    ok2 = check("→ '가벼운 스트레칭 10분' 퀘스트 생성",
                "가벼운 스트레칭 10분" in quest_titles(g))
    results += [ok1, ok2]

    # 반례 A: FatigueRisk 없음 → 미생성
    g = load_base_graph()
    user = _add_user(g, "r2b")
    for i in range(3):
        q = PROD[f"q_r2b_{i}"]
        g.add((q, RDF.type, PROD.Quest))
        g.add((q, PROD.questType, Literal("운동")))
        g.add((q, PROD.isCompleted, Literal(False, datatype=XSD.boolean)))
        g.add((user, PROD.receivesQuest, q))
    apply_rule(g, rules["burnout_warning"])
    results.append(check("FatigueRisk 없음 → BurnoutWarning 미생성 (반례)",
                         (user, PROD.hasState, PROD.BurnoutWarning) not in g))

    # 반례 B: 운동 퀘스트 2개만 → 미생성
    g = load_base_graph()
    user = _add_user(g, "r2c")
    g.add((user, PROD.hasState, PROD.FatigueRisk))
    for i in range(2):
        q = PROD[f"q_r2c_{i}"]
        g.add((q, RDF.type, PROD.Quest))
        g.add((q, PROD.questType, Literal("운동")))
        g.add((q, PROD.isCompleted, Literal(False, datatype=XSD.boolean)))
        g.add((user, PROD.receivesQuest, q))
    apply_rule(g, rules["burnout_warning"])
    results.append(check("운동 퀘스트 2개 → BurnoutWarning 미생성 (반례)",
                         (user, PROD.hasState, PROD.BurnoutWarning) not in g))

    return all(results)


# ── Test 4: sedentary_pattern ─────────────────────────────────────────────────

def test_sedentary_pattern(rules: dict[str, str]) -> bool:
    print("\n[Test 3] sedentary_pattern")
    results = []

    # 정례: 연속 3일 3000보 미만 → SedentaryPattern + 퀘스트
    g = load_base_graph()
    user = _add_user(g, "r3a")
    for i, cnt in enumerate([1200, 2500, 800]):
        sc = PROD[f"sc_r3a_{i}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
        g.add((sc, PROD.date, Literal(f"2026-04-{17+i}", datatype=XSD.date)))
        g.add((user, PROD.hasStepCount, sc))
    apply_rule(g, rules["sedentary_pattern"])
    results.append(check("연속 3일 3000보 미만 → SedentaryPattern",
                         (user, PROD.hasState, PROD.SedentaryPattern) in g))
    results.append(check("→ '30분 산책하기' 퀘스트 생성",
                         "30분 산책하기" in quest_titles(g)))
    results.append(check("→ 퀘스트가 User에 연결됨",
                         any((q, PROD.title, Literal("30분 산책하기")) in g
                             for q in g.objects(user, PROD.receivesQuest))))

    # 반례 A: 비연속(하루 건너뜀) → 미생성
    g = load_base_graph()
    user = _add_user(g, "r3b")
    for date_, cnt in [("2026-04-17", 800), ("2026-04-19", 900), ("2026-04-21", 700)]:
        sc = PROD[f"sc_r3b_{date_}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
        g.add((sc, PROD.date, Literal(date_, datatype=XSD.date)))
        g.add((user, PROD.hasStepCount, sc))
    apply_rule(g, rules["sedentary_pattern"])
    results.append(check("하루 건너뜀 → SedentaryPattern 미생성 (반례)",
                         (user, PROD.hasState, PROD.SedentaryPattern) not in g))

    # 반례 B: 3일 중 1일은 3000보 초과
    g = load_base_graph()
    user = _add_user(g, "r3c")
    for i, cnt in enumerate([800, 5000, 900]):
        sc = PROD[f"sc_r3c_{i}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
        g.add((sc, PROD.date, Literal(f"2026-04-{17+i}", datatype=XSD.date)))
        g.add((user, PROD.hasStepCount, sc))
    apply_rule(g, rules["sedentary_pattern"])
    results.append(check("3일 중 1일 5000보 → SedentaryPattern 미생성 (반례)",
                         (user, PROD.hasState, PROD.SedentaryPattern) not in g))

    return all(results)


# ── Test 5: place_habit ───────────────────────────────────────────────────────

def test_place_habit(rules: dict[str, str]) -> bool:
    print("\n[Test 4] place_habit")
    results = []

    # 정례: 같은 장소 3회 방문 → PlaceHabit + RoomObject
    g = load_base_graph()
    user = _add_user(g, "r4a")
    for i in range(3):
        loc = PROD[f"loc_r4a_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal("헬스장")))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["place_habit"])
    results.append(check("같은 장소 3회 → PlaceHabit",
                         (user, PROD.hasState, PROD.PlaceHabit) in g))
    results.append(check("→ RoomObject 생성",
                         any(True for _ in g.objects(user, PROD.hasRoomObject))))

    # 반례: 다른 장소 각 1회 (총 3회이지만 같은 장소 아님)
    g = load_base_graph()
    user = _add_user(g, "r4b")
    for place in ["헬스장", "카페", "도서관"]:
        loc = PROD[f"loc_r4b_{place}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal(place)))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["place_habit"])
    results.append(check("각기 다른 장소 1회씩 → PlaceHabit 미생성 (반례)",
                         (user, PROD.hasState, PROD.PlaceHabit) not in g))

    return all(results)


# ── Test 6: late_caffeine_sleep_quality ───────────────────────────────────────

def test_late_caffeine_sleep_quality(rules: dict[str, str]) -> bool:
    print("\n[Test 5] late_caffeine_sleep_quality")
    results = []

    # 정례: 수면질 55 + 오후 7시 카페 → 퀘스트 생성
    g = load_base_graph()
    user = _add_user(g, "r5a")
    _add_sleep(g, user, "r5a", 6.0, quality=55)
    _add_cafe_location(g, user, "r5a", 1, hour=19)
    apply_rule(g, rules["late_caffeine_sleep_quality"])
    results.append(check("수면질 55 + 오후 카페 → '오후엔 디카페인 어때요?' 퀘스트",
                         "오후엔 디카페인 어때요?" in quest_titles(g)))

    # 반례 A: 수면질 70 (≥60) → 미생성
    g = load_base_graph()
    user = _add_user(g, "r5b")
    _add_sleep(g, user, "r5b", 6.0, quality=70)
    _add_cafe_location(g, user, "r5b", 1, hour=19)
    apply_rule(g, rules["late_caffeine_sleep_quality"])
    results.append(check("수면질 70 → 퀘스트 미생성 (반례)",
                         "오후엔 디카페인 어때요?" not in quest_titles(g)))

    # 반례 B: 오전 카페 방문(11시) → 미생성
    g = load_base_graph()
    user = _add_user(g, "r5c")
    _add_sleep(g, user, "r5c", 6.0, quality=55)
    _add_cafe_location(g, user, "r5c", 1, hour=11)
    apply_rule(g, rules["late_caffeine_sleep_quality"])
    results.append(check("오전 카페 → 퀘스트 미생성 (반례)",
                         "오후엔 디카페인 어때요?" not in quest_titles(g)))

    return all(results)


# ── Test 7: 빈 노드 감지 (Rules 6, 6B–6F) ────────────────────────────────────

def test_blank_node_detection(rules: dict[str, str]) -> bool:
    print("\n[Test 6] 빈 노드 감지 (missing_companion / emotion / purpose / music_mood / sleep_cause / event_review)")
    results = []

    # R6: missing_companion — companion 없는 위치 → 퀘스트
    g = load_base_graph()
    user = _add_user(g, "r6a")
    loc = PROD["loc_r6a"]
    g.add((loc, RDF.type, PROD.Location))
    g.add((loc, PROD.placeName, Literal("스타벅스")))
    g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["missing_companion"])
    results.append(check("companion 없는 위치 → '오늘 스타벅스 누구랑 갔어?' 퀘스트",
                         "오늘 스타벅스 누구랑 갔어?" in quest_titles(g)))
    # 반례: companion 있음
    g = load_base_graph()
    user = _add_user(g, "r6b")
    loc = PROD["loc_r6b"]
    g.add((loc, RDF.type, PROD.Location))
    g.add((loc, PROD.placeName, Literal("스타벅스")))
    g.add((loc, PROD.companion, Literal("친구")))
    g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["missing_companion"])
    results.append(check("companion 있음 → 미생성 (반례)",
                         "오늘 스타벅스 누구랑 갔어?" not in quest_titles(g)))

    # R6-B: missing_emotion — emotion 없는 Activity
    g = load_base_graph()
    user = _add_user(g, "r6c")
    act = PROD["act_r6c"]
    g.add((act, RDF.type, PROD.Activity))
    g.add((act, PROD.activityType, Literal("독서")))
    g.add((user, PROD.hasActivity, act))
    apply_rule(g, rules["missing_emotion"])
    results.append(check("emotion 없는 Activity → '오늘 독서 어떤 기분이었어?' 퀘스트",
                         "오늘 독서 어떤 기분이었어?" in quest_titles(g)))
    # 반례: emotion 있음
    g = load_base_graph()
    user = _add_user(g, "r6d")
    act = PROD["act_r6d"]
    g.add((act, RDF.type, PROD.Activity))
    g.add((act, PROD.activityType, Literal("독서")))
    g.add((act, PROD.emotion, Literal("즐거움")))
    g.add((user, PROD.hasActivity, act))
    apply_rule(g, rules["missing_emotion"])
    results.append(check("emotion 있음 → 미생성 (반례)",
                         "오늘 독서 어떤 기분이었어?" not in quest_titles(g)))

    # R6-C: missing_purpose — 3회 이상 방문 + purpose 없음
    g = load_base_graph()
    user = _add_user(g, "r6e")
    for i in range(3):
        loc = PROD[f"loc_r6e_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal("도서관")))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["missing_purpose"])
    results.append(check("3회 방문 + purpose 없음 → '도서관에 자주 가는 이유가 있어?' 퀘스트",
                         "도서관에 자주 가는 이유가 있어?" in quest_titles(g)))

    # R6-D: missing_music_mood — mood 없는 MusicListening
    g = load_base_graph()
    user = _add_user(g, "r6f")
    ml = PROD["ml_r6f"]
    g.add((ml, RDF.type, PROD.MusicListening))
    g.add((ml, PROD.genre, Literal("재즈")))
    g.add((user, PROD.listensTo, ml))
    apply_rule(g, rules["missing_music_mood"])
    results.append(check("mood 없는 MusicListening → '요즘 재즈 음악 자주 듣네...' 퀘스트",
                         any("재즈" in t for t in quest_titles(g))))

    # R6-E: missing_sleep_cause — quality < 60 + cause 없음 → 퀘스트
    g = load_base_graph()
    user = _add_user(g, "r6g")
    _add_sleep(g, user, "r6g", 5.0, quality=50)
    apply_rule(g, rules["missing_sleep_cause"])
    title = "어젯밤 잠이 잘 안 왔어? 이유가 있었어?"
    results.append(check("수면질 50 + cause 없음 → 수면원인 퀘스트 생성",
                         title in quest_titles(g)))
    # 중복 방지: 같은 규칙 재실행 → 퀘스트 1개 유지
    apply_rule(g, rules["missing_sleep_cause"])
    dup_count = sum(1 for t in quest_titles(g) if t == title)
    results.append(check("동일 퀘스트 중복 생성 방지 (1개 유지)",
                         dup_count == 1, f"실제 {dup_count}개"))

    # R6-F: missing_event_review — 종료된 이벤트 + review 없음
    g = load_base_graph()
    user = _add_user(g, "r6h")
    evt = PROD["evt_r6h"]
    g.add((evt, RDF.type, PROD.CalendarEvent))
    g.add((evt, PROD.eventTitle, Literal("팀 미팅")))
    g.add((evt, PROD.endTime, Literal("2020-01-01T12:00:00", datatype=XSD.dateTime)))
    g.add((user, PROD.hasCalendarEvent, evt))
    apply_rule(g, rules["missing_event_review"])
    results.append(check("종료 이벤트 + review 없음 → '팀 미팅 어땠어?' 퀘스트",
                         "팀 미팅 어땠어?" in quest_titles(g)))
    # 반례: 종료된 이벤트지만 review 이미 존재 → 퀘스트 미생성
    g = load_base_graph()
    user = _add_user(g, "r6i")
    evt = PROD["evt_r6i"]
    g.add((evt, RDF.type, PROD.CalendarEvent))
    g.add((evt, PROD.eventTitle, Literal("완료된 회의")))
    g.add((evt, PROD.endTime, Literal("2020-01-01T12:00:00", datatype=XSD.dateTime)))
    g.add((evt, PROD.review, Literal("좋았어요")))
    g.add((user, PROD.hasCalendarEvent, evt))
    apply_rule(g, rules["missing_event_review"])
    results.append(check("종료 이벤트 + review 있음 → 퀘스트 미생성 (반례)",
                         "완료된 회의 어땠어?" not in quest_titles(g)))

    return all(results)


# ── Test 8: indoor_day_pattern ────────────────────────────────────────────────

def test_indoor_day_pattern(rules: dict[str, str]) -> bool:
    print("\n[Test 7] indoor_day_pattern + sunny_indoor_quest")
    results = []

    # 정례 A: 비 + 외출 1곳 → IndoorDayPattern
    g = load_base_graph()
    user = _add_user(g, "r7a")
    w = PROD["w_r7a"]
    g.add((w, RDF.type, PROD.Weather))
    g.add((w, PROD.condition, Literal("rainy")))
    g.add((w, PROD.recordedAt, Literal("2026-04-17T12:00:00", datatype=XSD.dateTime)))
    g.add((user, PROD.hasWeather, w))
    loc = PROD["loc_r7a"]
    g.add((loc, RDF.type, PROD.Location))
    g.add((loc, PROD.placeName, Literal("집")))
    g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["indoor_day_pattern"])
    results.append(check("비 날씨 + 외출 1곳 → IndoorDayPattern",
                         (user, PROD.hasState, PROD.IndoorDayPattern) in g))

    # 정례 B: 겨울(1월) + 외출 0곳 → IndoorDayPattern
    g = load_base_graph()
    user = _add_user(g, "r7b")
    w = PROD["w_r7b"]
    g.add((w, RDF.type, PROD.Weather))
    g.add((w, PROD.condition, Literal("cloudy")))
    g.add((w, PROD.recordedAt, Literal("2026-01-15T12:00:00", datatype=XSD.dateTime)))
    g.add((user, PROD.hasWeather, w))
    apply_rule(g, rules["indoor_day_pattern"])
    results.append(check("겨울(1월) + 외출 없음 → IndoorDayPattern",
                         (user, PROD.hasState, PROD.IndoorDayPattern) in g))

    # 반례: 맑은 날 + 외출 3곳 → 미생성
    g = load_base_graph()
    user = _add_user(g, "r7c")
    w = PROD["w_r7c"]
    g.add((w, RDF.type, PROD.Weather))
    g.add((w, PROD.condition, Literal("sunny")))
    g.add((w, PROD.recordedAt, Literal("2026-06-15T12:00:00", datatype=XSD.dateTime)))
    g.add((user, PROD.hasWeather, w))
    for place in ["헬스장", "카페", "공원"]:
        loc = PROD[f"loc_r7c_{place}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal(place)))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["indoor_day_pattern"])
    results.append(check("맑은 날 + 외출 3곳 → IndoorDayPattern 미생성 (반례)",
                         (user, PROD.hasState, PROD.IndoorDayPattern) not in g))

    # Rule 14: sunny_indoor_quest — IndoorDayPattern + 맑은 날씨
    g = load_base_graph()
    user = _add_user(g, "r14a")
    g.add((user, PROD.hasState, PROD.IndoorDayPattern))
    w = PROD["w_r14a"]
    g.add((w, RDF.type, PROD.Weather))
    g.add((w, PROD.condition, Literal("sunny and clear")))
    g.add((user, PROD.hasWeather, w))
    apply_rule(g, rules["sunny_indoor_quest"])
    results.append(check("IndoorDayPattern + 맑은 날씨 → 산책 강화 퀘스트",
                         "날씨가 맑아요! 지금 딱 산책하기 좋아요" in quest_titles(g)))
    # 반례: IndoorDayPattern 없음
    g = load_base_graph()
    user = _add_user(g, "r14b")
    w = PROD["w_r14b"]
    g.add((w, RDF.type, PROD.Weather))
    g.add((w, PROD.condition, Literal("sunny")))
    g.add((user, PROD.hasWeather, w))
    apply_rule(g, rules["sunny_indoor_quest"])
    results.append(check("IndoorDayPattern 없음 → 퀘스트 미생성 (반례)",
                         "날씨가 맑아요! 지금 딱 산책하기 좋아요" not in quest_titles(g)))

    return all(results)


# ── Test 9: routine_detection + music_mood ────────────────────────────────────

def test_routine_and_music_mood(rules: dict[str, str]) -> bool:
    print("\n[Test 8/9] routine_detection + music_mood")
    results = []

    # R8: 같은 시간대(9시) 일정 3회 → Routine
    g = load_base_graph()
    user = _add_user(g, "r8a")
    for i in range(3):
        evt = PROD[f"evt_r8a_{i}"]
        g.add((evt, RDF.type, PROD.CalendarEvent))
        g.add((evt, PROD.startTime,
               Literal(f"2026-04-{17+i}T09:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasCalendarEvent, evt))
    apply_rule(g, rules["routine_detection"])
    results.append(check("같은 시간대(9시) 일정 3회 → Routine",
                         (user, PROD.hasState, PROD.Routine) in g))
    # 반례: 각기 다른 시간대
    g = load_base_graph()
    user = _add_user(g, "r8b")
    for hour in [9, 14, 18]:
        evt = PROD[f"evt_r8b_{hour}"]
        g.add((evt, RDF.type, PROD.CalendarEvent))
        g.add((evt, PROD.startTime,
               Literal(f"2026-04-17T{hour:02d}:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasCalendarEvent, evt))
    apply_rule(g, rules["routine_detection"])
    results.append(check("각기 다른 시간대 1회씩 → Routine 미생성 (반례)",
                         (user, PROD.hasState, PROD.Routine) not in g))

    # R9: 재즈 장르 130분 청취 → MusicMood
    g = load_base_graph()
    user = _add_user(g, "r9a")
    for i, dur in enumerate([60, 40, 30]):
        ml = PROD[f"ml_r9a_{i}"]
        g.add((ml, RDF.type, PROD.MusicListening))
        g.add((ml, PROD.genre, Literal("jazz")))
        g.add((ml, PROD.listenDuration, Literal(dur, datatype=XSD.integer)))
        g.add((user, PROD.listensTo, ml))
    apply_rule(g, rules["music_mood"])
    results.append(check("재즈 130분 청취 → MusicMood",
                         (user, PROD.hasState, PROD.MusicMood) in g))
    # 반례: 60분만
    g = load_base_graph()
    user = _add_user(g, "r9b")
    ml = PROD["ml_r9b"]
    g.add((ml, RDF.type, PROD.MusicListening))
    g.add((ml, PROD.genre, Literal("jazz")))
    g.add((ml, PROD.listenDuration, Literal(60, datatype=XSD.integer)))
    g.add((user, PROD.listensTo, ml))
    apply_rule(g, rules["music_mood"])
    results.append(check("60분 청취 → MusicMood 미생성 (반례)",
                         (user, PROD.hasState, PROD.MusicMood) not in g))

    return all(results)


# ── Test 10: 인과 체인 단계별 (Rules 10–13) ───────────────────────────────────

def test_causal_chain(rules: dict[str, str]) -> bool:
    print("\n[Test 10-13] 다단계 인과 추론 체인")
    results = []

    def setup_chain_base(uid: str) -> tuple[Graph, URIRef]:
        """카페인 + 수면저하 + 운동없음 + 저보행 환경 세팅."""
        g = load_base_graph()
        user = _add_user(g, uid)
        # 오후 카페 방문
        loc = PROD[f"loc_chain_{uid}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeType, Literal("cafe")))
        g.add((loc, PROD.visitTime, Literal("2026-04-17T19:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasLocation, loc))
        # 수면질 45
        _add_sleep(g, user, f"chain_{uid}", 6.5, quality=45)
        # 저보행 3일
        for i in range(3):
            sc = PROD[f"sc_chain_{uid}_{i}"]
            g.add((sc, RDF.type, PROD.StepCount))
            g.add((sc, PROD["count"], Literal(1500, datatype=XSD.integer)))
            g.add((user, PROD.hasStepCount, sc))
        return g, user

    # Rule 10: SleepQualityImpaired
    g, user = setup_chain_base("r10")
    apply_rule(g, rules["causal_sleep_impaired"])
    results.append(check("Rule 10: 오후 카페 + 수면질 45 → SleepQualityImpaired",
                         (user, PROD.hasState, PROD.SleepQualityImpaired) in g))

    # Rule 11: ExerciseSkipped (SleepQualityImpaired 수동 주입)
    g, user = setup_chain_base("r11")
    g.add((user, PROD.hasState, PROD.SleepQualityImpaired))
    apply_rule(g, rules["causal_exercise_skipped"])
    results.append(check("Rule 11: SleepQualityImpaired + 운동없음 → ExerciseSkipped",
                         (user, PROD.hasState, PROD.ExerciseSkipped) in g))

    # Rule 11 순서 의존성: SleepQualityImpaired 없이 Rule11 먼저 실행 → 미발동
    g, user = setup_chain_base("r11_order")
    apply_rule(g, rules["causal_exercise_skipped"])  # Rule10 먼저 안 함
    results.append(check("Rule 11 단독 실행(선행 상태 없음) → ExerciseSkipped 미생성 (순서 의존)",
                         (user, PROD.hasState, PROD.ExerciseSkipped) not in g))

    # Rule 12: WeeklyActivityLow
    g, user = setup_chain_base("r12")
    g.add((user, PROD.hasState, PROD.ExerciseSkipped))
    apply_rule(g, rules["causal_weekly_activity_low"])
    results.append(check("Rule 12: ExerciseSkipped + 저보행 3일 → WeeklyActivityLow",
                         (user, PROD.hasState, PROD.WeeklyActivityLow) in g))

    # Rule 13: BurnoutWarning + 퀘스트
    g, user = setup_chain_base("r13")
    g.add((user, PROD.hasState, PROD.WeeklyActivityLow))
    apply_rule(g, rules["causal_burnout_from_chain"])
    results.append(check("Rule 13: WeeklyActivityLow → BurnoutWarning",
                         (user, PROD.hasState, PROD.BurnoutWarning) in g))
    results.append(check("→ 활동량 감소 퀘스트 생성",
                         any("이번 주 활동량" in t for t in quest_titles(g))))

    # 전체 체인 순서대로 실행 (10→11→12→13)
    print("  --- 전체 체인 순서 실행 ---")
    g, user = setup_chain_base("chain_full")
    for rule_id in ["causal_sleep_impaired", "causal_exercise_skipped",
                    "causal_weekly_activity_low", "causal_burnout_from_chain"]:
        apply_rule(g, rules[rule_id])
    chain_ok = all([
        (user, PROD.hasState, PROD.SleepQualityImpaired) in g,
        (user, PROD.hasState, PROD.ExerciseSkipped) in g,
        (user, PROD.hasState, PROD.WeeklyActivityLow) in g,
        (user, PROD.hasState, PROD.BurnoutWarning) in g,
    ])
    results.append(check("전체 체인 10→11→12→13 순서 실행 → 4단계 모두 발동",
                         chain_ok))

    return all(results)


# ── Test 11: persona rules (P1–P6) ───────────────────────────────────────────

def test_persona_rules(rules: dict[str, str]) -> bool:
    print("\n[Test P1-P6] 페르소나 자동 생성 규칙")
    results = []

    # P1: persona_active — 7000보 이상 4일+
    g = load_base_graph()
    user = _add_user(g, "pp1a")
    for i in range(4):
        sc = PROD[f"sc_pp1a_{i}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(8000, datatype=XSD.integer)))
        g.add((user, PROD.hasStepCount, sc))
    apply_rule(g, rules["persona_active"])
    results.append(check("P1: 7000보 이상 4일 → Persona(energyType=active)",
                         any(str(v) == "active"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.energyType))))
    # 반례: 3일만
    g = load_base_graph()
    user = _add_user(g, "pp1b")
    for i in range(3):
        sc = PROD[f"sc_pp1b_{i}"]
        g.add((sc, RDF.type, PROD.StepCount))
        g.add((sc, PROD["count"], Literal(8000, datatype=XSD.integer)))
        g.add((user, PROD.hasStepCount, sc))
    apply_rule(g, rules["persona_active"])
    results.append(check("P1: 3일만 → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    # P2: persona_indoor — IndoorDayPattern 상태
    g = load_base_graph()
    user = _add_user(g, "pp2a")
    g.add((user, PROD.hasState, PROD.IndoorDayPattern))
    apply_rule(g, rules["persona_indoor"])
    results.append(check("P2: IndoorDayPattern → Persona(energyType=indoor)",
                         any(str(v) == "indoor"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.energyType))))
    # 반례: 상태 없음
    g = load_base_graph()
    user = _add_user(g, "pp2b")
    apply_rule(g, rules["persona_indoor"])
    results.append(check("P2: IndoorDayPattern 없음 → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    # P3: persona_social — companion 방문 3회+
    g = load_base_graph()
    user = _add_user(g, "pp3a")
    for i in range(3):
        loc = PROD[f"loc_pp3a_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.companion, Literal("친구")))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["persona_social"])
    results.append(check("P3: companion 방문 3회 → Persona(socialPreference=social)",
                         any(str(v) == "social"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.socialPreference))))
    # 반례: 2회
    g = load_base_graph()
    user = _add_user(g, "pp3b")
    for i in range(2):
        loc = PROD[f"loc_pp3b_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.companion, Literal("친구")))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["persona_social"])
    results.append(check("P3: companion 방문 2회 → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    # P4: persona_solitary — 혼자 방문 70% 이상 (5/5=100%)
    g = load_base_graph()
    user = _add_user(g, "pp4a")
    for i in range(4):
        loc = PROD[f"loc_pp4a_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal(f"장소{i}")))
        g.add((user, PROD.hasLocation, loc))
    # 1개만 companion 있음 → 3/4 = 75% > 70%
    loc_with = PROD["loc_pp4a_s"]
    g.add((loc_with, RDF.type, PROD.Location))
    g.add((loc_with, PROD.companion, Literal("동료")))
    g.add((user, PROD.hasLocation, loc_with))
    apply_rule(g, rules["persona_solitary"])
    results.append(check("P4: 혼자 방문 80% (4/5) → Persona(socialPreference=solitary)",
                         any(str(v) == "solitary"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.socialPreference))))
    # 반례: 동반 방문이 더 많음 (2/5 = 40% < 70%)
    g = load_base_graph()
    user = _add_user(g, "pp4b")
    for i in range(2):  # 2개 혼자
        loc = PROD[f"loc_pp4b_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal(f"장소{i}")))
        g.add((user, PROD.hasLocation, loc))
    for i in range(3):  # 3개 companion 있음
        loc = PROD[f"loc_pp4b_s{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.companion, Literal("친구")))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["persona_solitary"])
    results.append(check("P4: 혼자 방문 40% (2/5) → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    # P5: persona_routine — Routine 노드 2개+
    g = load_base_graph()
    user = _add_user(g, "pp5a")
    for i in range(2):
        r = PROD[f"rt_pp5a_{i}"]
        g.add((r, RDF.type, PROD.Routine))
        g.add((r, PROD.routineHour, Literal(9 + i, datatype=XSD.integer)))
        g.add((user, PROD.hasRoomObject, r))
    apply_rule(g, rules["persona_routine"])
    results.append(check("P5: Routine 2개 → Persona(lifePattern=routine)",
                         any(str(v) == "routine"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.lifePattern))))
    # 반례: Routine 1개
    g = load_base_graph()
    user = _add_user(g, "pp5b")
    r = PROD["rt_pp5b"]
    g.add((r, RDF.type, PROD.Routine))
    g.add((user, PROD.hasRoomObject, r))
    apply_rule(g, rules["persona_routine"])
    results.append(check("P5: Routine 1개 → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    # P6: persona_night_owl — 23시+ 방문 3회+
    g = load_base_graph()
    user = _add_user(g, "pp6a")
    for i in range(3):
        loc = PROD[f"loc_pp6a_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.visitTime,
               Literal(f"2026-04-{17+i}T23:30:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["persona_night_owl"])
    results.append(check("P6: 23시+ 방문 3회 → Persona(lifePattern=night_owl)",
                         any(str(v) == "night_owl"
                             for p in g.objects(user, PROD.hasPersona)
                             for v in g.objects(p, PROD.lifePattern))))
    # 반례: 오전 방문만
    g = load_base_graph()
    user = _add_user(g, "pp6b")
    for i in range(3):
        loc = PROD[f"loc_pp6b_{i}"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.visitTime,
               Literal(f"2026-04-{17+i}T10:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasLocation, loc))
    apply_rule(g, rules["persona_night_owl"])
    results.append(check("P6: 오전 방문만 → Persona 미생성 (반례)",
                         not any(True for _ in g.objects(user, PROD.hasPersona))))

    return all(results)


# ── Test 12: 엣지 케이스 ──────────────────────────────────────────────────────

def test_edge_empty_graph(rules: dict[str, str]) -> bool:
    print("\n[Test Edge] 빈 그래프 — 모든 규칙 추론 결과 없어야 함")
    g = load_base_graph()  # 인스턴스 데이터 없음
    base_triples = len(g)
    for rule_id in ALL_RULE_IDS:
        sparql = rules.get(rule_id, "")
        if sparql:
            apply_rule(g, sparql)
    added = len(g) - base_triples
    return check("빈 그래프 → 모든 규칙 트리플 추가 없음",
                 added == 0, f"추가된 트리플: {added}개")


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 60)
    print("inference_rules.sparql 단위 테스트 (25개 규칙 전체)")
    print("=" * 60)

    rules_text = RULES_PATH.read_text(encoding="utf-8")
    rules = parse_rules(rules_text)

    test_groups = [
        ("파서",           lambda: test_rule_parser(rules)),
        ("fatigue_risk",  lambda: test_fatigue_risk(rules)),
        ("burnout_warning", lambda: test_burnout_warning(rules)),
        ("sedentary",     lambda: test_sedentary_pattern(rules)),
        ("place_habit",   lambda: test_place_habit(rules)),
        ("late_caffeine", lambda: test_late_caffeine_sleep_quality(rules)),
        ("blank_node",    lambda: test_blank_node_detection(rules)),
        ("indoor+sunny",  lambda: test_indoor_day_pattern(rules)),
        ("routine+music", lambda: test_routine_and_music_mood(rules)),
        ("causal_chain",  lambda: test_causal_chain(rules)),
        ("persona_P1-P6", lambda: test_persona_rules(rules)),
        ("empty_graph",   lambda: test_edge_empty_graph(rules)),
    ]

    passed = 0
    failed_groups = []
    for name, fn in test_groups:
        try:
            ok = fn()
        except Exception as exc:
            print(f"  [EXCEPTION] {name}: {exc}")
            ok = False
        if ok:
            passed += 1
        else:
            failed_groups.append(name)

    print("\n" + "=" * 60)
    total = len(test_groups)
    print(f"결과: {passed}/{total} 테스트 그룹 통과")
    if failed_groups:
        print(f"실패 그룹: {failed_groups}")
        sys.exit(1)
    else:
        print("모든 테스트 통과")


if __name__ == "__main__":
    main()
