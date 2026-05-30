"""
test_ontology_engine.py
로컬 추론 파이프라인 통합 테스트 (E2E)

Firebase Storage/Firestore 없이 in-memory Graph로 전체 파이프라인 시뮬레이션:
  1. core.ttl 로드
  2. 테스트 트리플 추가 (Gemma 3n 출력 시뮬레이션)
  3. DuckDB 집계 (_aggregate_with_duckdb)
  4. SPARQL 추론 (_apply_rules)
  5. 결과 검증 (Quest, Persona, RoomObject, State)

시나리오:
  - FatigueRisk → BurnoutWarning 체인
  - SedentaryPattern 단독
  - PlaceHabit → RoomObject 생성
  - 카페인-수면 인과관계
  - 빈 노드 감지 → 보완형 퀘스트
  - 페르소나 복수 규칙 병합
  - 다단계 인과 체인 (Rule 10→11→12→13) 순서 의존성
  - 25개 규칙 중 최소 10개 이상 발동

Usage:
    python Functions/test_ontology_engine.py
"""

import sys
import unittest
from pathlib import Path
from datetime import datetime
from rdflib import Graph, Namespace, RDF, Literal, URIRef
from rdflib.namespace import XSD

# ontology_engine 모듈에서 Firebase 의존성 없는 함수만 import
# Firebase 초기화 함수는 호출하지 않음
sys.path.insert(0, str(Path(__file__).parent))

# PROD 네임스페이스와 경로 정의
PROD = Namespace("http://7team.dev/ontology#")
BASE_DIR = Path(__file__).parent
TTL_PATH = BASE_DIR / "ontology" / "core.ttl"
RULES_PATH = BASE_DIR / "ontology" / "rules" / "inference_rules.sparql"

# 색상 출력 (터미널 지원)
PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"


class TestOntologyPipeline(unittest.TestCase):
    """전체 추론 파이프라인 통합 테스트"""

    @classmethod
    def setUpClass(cls):
        """전체 테스트 시작 전 온톨로지 및 규칙 로드"""
        cls.rules_text = RULES_PATH.read_text(encoding="utf-8")
        cls.rules = cls._parse_rules(cls.rules_text)
        print("\n" + "=" * 70)
        print("ontology_engine.py 통합 테스트 — 전체 추론 파이프라인 E2E 검증")
        print("=" * 70)

    @staticmethod
    def _parse_rules(rules_text: str) -> dict[str, str]:
        """SPARQL 규칙 파싱 (ontology_engine._parse_rules 복제)"""
        import re
        pattern = re.compile(
            r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)"
        )
        return {m.group(1): m.group(2).strip() for m in pattern.finditer(rules_text)}

    def _load_base_graph(self) -> Graph:
        """core.ttl 로드"""
        g = Graph()
        g.bind("prod", PROD)
        g.parse(str(TTL_PATH), format="turtle")
        return g

    def _apply_rules_in_order(self, g: Graph, rule_ids: list[str]) -> list[tuple]:
        """규칙을 순서대로 실행 (ontology_engine._apply_rules 단순화 버전)"""
        new_triples = []
        for rule_id in rule_ids:
            sparql = self.rules.get(rule_id)
            if not sparql:
                continue
            result = list(g.query(sparql))
            for triple in result:
                g.add(triple)
            new_triples.extend(result)
        return new_triples

    def _aggregate_with_duckdb(self, g: Graph) -> list[tuple]:
        """DuckDB 집계 (ontology_engine._aggregate_with_duckdb 복제)"""
        import duckdb
        from rdflib.namespace import XSD

        rows = list(g.query("""
            PREFIX prod: <http://7team.dev/ontology#>
            SELECT ?user ?date ?count WHERE {
                ?user prod:hasStepCount ?s .
                ?s prod:count ?count ;
                   prod:date  ?date .
            }
        """))

        if not rows:
            return []

        con = duckdb.connect()
        try:
            con.execute("""
                CREATE TABLE steps (
                    user_uri  VARCHAR,
                    step_date DATE,
                    step_cnt  INTEGER
                )
            """)
            con.executemany(
                "INSERT INTO steps VALUES (?, TRY_CAST(? AS DATE), TRY_CAST(? AS INTEGER))",
                [(str(r[0]), str(r[1]), str(r[2])) for r in rows],
            )

            results = con.execute("""
                WITH low_days AS (
                    SELECT user_uri, step_date
                    FROM steps
                    WHERE step_cnt < 3000
                ),
                ranked AS (
                    SELECT
                        user_uri,
                        step_date,
                        (step_date - DATE '1970-01-01') -
                        CAST(ROW_NUMBER() OVER (PARTITION BY user_uri ORDER BY step_date) AS INTEGER)
                        AS island_id
                    FROM low_days
                ),
                islands AS (
                    SELECT user_uri, island_id, COUNT(*) AS consecutive_days
                    FROM ranked
                    GROUP BY user_uri, island_id
                )
                SELECT user_uri, MAX(consecutive_days) AS max_consecutive
                FROM islands
                GROUP BY user_uri
            """).fetchall()

            return [
                (
                    URIRef(user_uri_str),
                    PROD.hasConsecutiveLowStepDays,
                    Literal(int(max_consec), datatype=XSD.integer),
                )
                for user_uri_str, max_consec in results
            ]
        finally:
            con.close()

    # ── 헬퍼 함수 ────────────────────────────────────────────────────────────────

    def _add_user(self, g: Graph, uid: str) -> URIRef:
        user = PROD[f"user_{uid}"]
        g.add((user, RDF.type, PROD.User))
        g.add((user, PROD.uid, Literal(uid)))
        return user

    def _add_sleep(self, g: Graph, user: URIRef, duration: float, quality: int = 80):
        s = PROD[f"sleep_{user.split('#')[-1]}"]
        g.add((s, RDF.type, PROD.SleepData))
        g.add((s, PROD.duration, Literal(duration, datatype=XSD.float)))
        g.add((s, PROD.quality, Literal(quality, datatype=XSD.integer)))
        g.add((user, PROD.hasSleepData, s))
        return s

    def _add_cafe_locations(self, g: Graph, user: URIRef, count: int, hour: int):
        for i in range(count):
            loc = PROD[f"loc_cafe_{user.split('#')[-1]}_{i}"]
            g.add((loc, RDF.type, PROD.Location))
            g.add((loc, PROD.placeType, Literal("cafe")))
            g.add((loc, PROD.visitTime,
                   Literal(f"2026-04-{17+i}T{hour:02d}:00:00", datatype=XSD.dateTime)))
            g.add((user, PROD.hasLocation, loc))

    def _add_step_counts(self, g: Graph, user: URIRef, count_per_day: list[int]):
        """여러 날짜에 걸쳐 걸음수 추가"""
        for i, cnt in enumerate(count_per_day):
            sc = PROD[f"step_{user.split('#')[-1]}_{i}"]
            g.add((sc, RDF.type, PROD.StepCount))
            g.add((sc, PROD["count"], Literal(cnt, datatype=XSD.integer)))
            g.add((sc, PROD.date, Literal(f"2026-04-{17+i}", datatype=XSD.date)))
            g.add((user, PROD.hasStepCount, sc))

    def _add_location(self, g: Graph, user: URIRef, place_name: str, n: int = 1, place_type: str = ""):
        """같은 장소 n회 방문 추가"""
        for i in range(n):
            loc = PROD[f"loc_{user.split('#')[-1]}_{place_name}_{i}"]
            g.add((loc, RDF.type, PROD.Location))
            g.add((loc, PROD.placeName, Literal(place_name)))
            if place_type:
                g.add((loc, PROD.placeType, Literal(place_type)))
            g.add((user, PROD.hasLocation, loc))

    def _quest_titles(self, g: Graph) -> list[str]:
        return [str(o) for _, _, o in g.triples((None, PROD.title, None))]

    def _has_state(self, g: Graph, user: URIRef, state: URIRef) -> bool:
        return (user, PROD.hasState, state) in g

    def _persona_values(self, g: Graph, user: URIRef, prop: URIRef) -> list[str]:
        """사용자의 모든 Persona 노드에서 특정 속성 값 추출"""
        return [
            str(v)
            for p in g.objects(user, PROD.hasPersona)
            for v in g.objects(p, prop)
        ]

    # ══════════════════════════════════════════════════════════════════════════════
    # 시나리오 테스트
    # ══════════════════════════════════════════════════════════════════════════════

    def test_scenario_1_fatigue_to_burnout_chain(self):
        """시나리오 1: FatigueRisk → BurnoutWarning 체인 (Rule 1 → Rule 2)"""
        print("\n[Scenario 1] FatigueRisk → BurnoutWarning 체인")

        g = self._load_base_graph()
        user = self._add_user(g, "s1")

        # 조건: 수면 5h + 주간 카페 3회 → FatigueRisk
        self._add_sleep(g, user, 5.0)
        self._add_cafe_locations(g, user, 3, hour=10)

        # 운동 퀘스트 미완료 3개 추가
        for i in range(3):
            q = PROD[f"quest_exercise_{i}"]
            g.add((q, RDF.type, PROD.Quest))
            g.add((q, PROD.questType, Literal("운동")))
            g.add((q, PROD.isCompleted, Literal(False, datatype=XSD.boolean)))
            g.add((user, PROD.receivesQuest, q))

        # 추론 실행 (Rule 1 → Rule 2)
        self._apply_rules_in_order(g, ["fatigue_risk", "burnout_warning"])

        # 검증
        self.assertTrue(self._has_state(g, user, PROD.FatigueRisk),
                        "Rule 1: FatigueRisk 상태 생성 실패")
        self.assertTrue(self._has_state(g, user, PROD.BurnoutWarning),
                        "Rule 2: BurnoutWarning 상태 생성 실패")
        self.assertIn("가벼운 스트레칭 10분", self._quest_titles(g),
                      "Rule 2: BurnoutWarning 퀘스트 생성 실패")
        self.assertTrue(
            any(int(g.value(q, PROD.rewardAmount) or 0) == 50
                for q in g.objects(user, PROD.receivesQuest)
                if str(g.value(q, PROD.title)) == "가벼운 스트레칭 10분"),
            "Rule 2: BurnoutWarning 퀘스트 rewardAmount≠50"
        )
        print(f"  [{PASS}] FatigueRisk → BurnoutWarning 체인 성공")

    def test_scenario_2_sedentary_pattern_with_duckdb(self):
        """시나리오 2: DuckDB 집계 + SedentaryPattern (Rule 3)"""
        print("\n[Scenario 2] DuckDB 집계 → SedentaryPattern")

        g = self._load_base_graph()
        user = self._add_user(g, "s2")

        # 연속 4일 저보행 (2000보)
        self._add_step_counts(g, user, [2000, 2500, 2200, 2100])

        # DuckDB 집계 실행
        duckdb_triples = self._aggregate_with_duckdb(g)
        for triple in duckdb_triples:
            g.add(triple)

        # 검증: hasConsecutiveLowStepDays 트리플 생성 확인
        consec_days = list(g.objects(user, PROD.hasConsecutiveLowStepDays))
        self.assertEqual(len(consec_days), 1, "DuckDB 집계 결과 트리플 생성 실패")
        self.assertEqual(int(consec_days[0]), 4, "DuckDB 연속일 계산 오류")

        # Rule 3 추론
        self._apply_rules_in_order(g, ["sedentary_pattern"])

        # 검증
        self.assertTrue(self._has_state(g, user, PROD.SedentaryPattern),
                        "Rule 3: SedentaryPattern 상태 생성 실패")
        self.assertIn("30분 산책하기", self._quest_titles(g),
                      "Rule 3: SedentaryPattern 퀘스트 생성 실패")
        self.assertTrue(
            any(int(g.value(q, PROD.rewardAmount) or 0) == 50
                for q in g.objects(user, PROD.receivesQuest)
                if str(g.value(q, PROD.title)) == "30분 산책하기"),
            "Rule 3: SedentaryPattern 퀘스트 rewardAmount≠50"
        )
        print(f"  [{PASS}] DuckDB 집계 → SedentaryPattern 성공 (연속 4일)")

    def test_scenario_3_place_habit_and_room_object(self):
        """시나리오 3: PlaceHabit → RoomObject 생성 (Rule 4)"""
        print("\n[Scenario 3] PlaceHabit → RoomObject 생성")

        g = self._load_base_graph()
        user = self._add_user(g, "s3")

        # placeType=gym 으로 헬스장 4회 방문
        self._add_location(g, user, "헬스장", n=4, place_type="gym")

        # Rule 4 추론
        self._apply_rules_in_order(g, ["place_habit"])

        # 검증
        self.assertTrue(self._has_state(g, user, PROD.PlaceHabit),
                        "Rule 4: PlaceHabit 상태 생성 실패")
        room_objects = list(g.objects(user, PROD.hasRoomObject))
        self.assertGreater(len(room_objects), 0, "Rule 4: RoomObject 생성 실패")

        # RoomObject 속성 검증 (placeType=gym → objectType=dumbbell)
        obj = room_objects[0]
        obj_type = str(g.value(obj, PROD.objectType))
        self.assertEqual(obj_type, "dumbbell", "RoomObject.objectType 오류")
        inferred_from = str(g.value(obj, PROD.inferredFrom) or "")
        self.assertNotEqual(inferred_from, "",
                            "RoomObject.inferredFrom 빈값 — blank node ID 불일치 버그 재발")
        print(f"  [{PASS}] PlaceHabit → RoomObject 생성 성공 (objectType=dumbbell)")

    def test_scenario_4_late_caffeine_sleep_quality(self):
        """시나리오 4: 카페인-수면 인과관계 (Rule 5)"""
        print("\n[Scenario 4] 카페인-수면 인과관계 퀘스트")

        g = self._load_base_graph()
        user = self._add_user(g, "s4")

        # 수면질 55 + 오후 7시 카페 방문
        self._add_sleep(g, user, 6.0, quality=55)
        self._add_cafe_locations(g, user, 1, hour=19)

        # Rule 5 추론
        self._apply_rules_in_order(g, ["late_caffeine_sleep_quality"])

        # 검증
        self.assertIn("오후엔 디카페인 어때요?", self._quest_titles(g),
                      "Rule 5: 카페인-수면 퀘스트 생성 실패")
        self.assertTrue(
            any(int(g.value(q, PROD.rewardAmount) or 0) == 50
                for q in g.objects(user, PROD.receivesQuest)
                if str(g.value(q, PROD.title)) == "오후엔 디카페인 어때요?"),
            "Rule 5: 카페인-수면 퀘스트 rewardAmount≠50"
        )
        print(f"  [{PASS}] 카페인-수면 인과관계 퀘스트 생성 성공")

    def test_scenario_5_blank_node_companion(self):
        """시나리오 5: 빈 노드 감지 → 보완형 퀘스트 (Rule 6)"""
        print("\n[Scenario 5] 빈 노드 감지 → 동행자 보완형 퀘스트")

        g = self._load_base_graph()
        user = self._add_user(g, "s5")

        # companion 없는 위치 추가
        loc = PROD["loc_s5_cafe"]
        g.add((loc, RDF.type, PROD.Location))
        g.add((loc, PROD.placeName, Literal("스타벅스 강남점")))
        g.add((user, PROD.hasLocation, loc))

        # Rule 6 추론
        self._apply_rules_in_order(g, ["missing_companion"])

        # 검증
        quest_titles = self._quest_titles(g)
        self.assertTrue(
            any("스타벅스 강남점" in t and "누구랑 갔어?" in t for t in quest_titles),
            "Rule 6: missing_companion 퀘스트 생성 실패"
        )
        self.assertTrue(
            any(int(g.value(q, PROD.rewardAmount) or 0) == 30
                for q in g.objects(user, PROD.receivesQuest)
                if "스타벅스 강남점" in str(g.value(q, PROD.title) or "")
                and "누구랑 갔어?" in str(g.value(q, PROD.title) or "")),
            "Rule 6: missing_companion 퀘스트 rewardAmount≠30"
        )
        print(f"  [{PASS}] 빈 노드 감지 → 보완형 퀘스트 생성 성공")

    def test_scenario_6_persona_merge(self):
        """시나리오 6: 복수 페르소나 규칙 발동 → 병합 (Rule P1, P3)"""
        print("\n[Scenario 6] 복수 페르소나 규칙 병합")

        g = self._load_base_graph()
        user = self._add_user(g, "s6")

        # P1 조건: 7000보 이상 4일
        self._add_step_counts(g, user, [8000, 7500, 7200, 7800])

        # P3 조건: companion 있는 방문 3회
        for i in range(3):
            loc = PROD[f"loc_s6_social_{i}"]
            g.add((loc, RDF.type, PROD.Location))
            g.add((loc, PROD.placeName, Literal(f"카페{i}")))
            g.add((loc, PROD.companion, Literal("친구")))
            g.add((user, PROD.hasLocation, loc))

        # Rule P1, P3 추론
        self._apply_rules_in_order(g, ["persona_active", "persona_social"])

        # 검증: 복수 Persona 노드 생성 확인
        personas = list(g.objects(user, PROD.hasPersona))
        self.assertGreaterEqual(len(personas), 2, "복수 Persona 노드 생성 실패")

        # 속성 병합 검증
        energy_values = self._persona_values(g, user, PROD.energyType)
        social_values = self._persona_values(g, user, PROD.socialPreference)

        self.assertIn("active", energy_values, "Rule P1: energyType=active 생성 실패")
        self.assertIn("social", social_values, "Rule P3: socialPreference=social 생성 실패")

        # RoomObject 검증: P1 → sports_trophy, P3 → photo_frame_friends
        room_objects_s6 = list(g.objects(user, PROD.hasRoomObject))
        obj_types_s6 = {str(g.value(obj, PROD.objectType)) for obj in room_objects_s6
                        if g.value(obj, PROD.objectType)}
        self.assertIn("sports_trophy", obj_types_s6,
                      f"Rule P1: sports_trophy RoomObject 생성 실패 (실제: {obj_types_s6})")
        self.assertIn("photo_frame_friends", obj_types_s6,
                      f"Rule P3: photo_frame_friends RoomObject 생성 실패 (실제: {obj_types_s6})")

        print(f"  [{PASS}] 복수 페르소나 규칙 병합 성공 (active + social)")

    def test_scenario_7_causal_chain_full(self):
        """시나리오 7: 다단계 인과 체인 전체 (Rule 10→11→12→13)"""
        print("\n[Scenario 7] 다단계 인과 체인 (카페인→수면→운동→활동량→번아웃)")

        g = self._load_base_graph()
        user = self._add_user(g, "s7")

        # 오후 카페 방문
        self._add_cafe_locations(g, user, 1, hour=19)
        # 수면질 45
        self._add_sleep(g, user, 6.5, quality=45)
        # 저보행 3일
        self._add_step_counts(g, user, [1500, 1800, 1600])

        # 인과 체인 순서대로 실행
        chain_rules = [
            "causal_sleep_impaired",      # Rule 10
            "causal_exercise_skipped",    # Rule 11
            "causal_weekly_activity_low", # Rule 12
            "causal_burnout_from_chain",  # Rule 13
        ]
        self._apply_rules_in_order(g, chain_rules)

        # 검증: 4단계 모두 발동
        self.assertTrue(self._has_state(g, user, PROD.SleepQualityImpaired),
                        "Rule 10: SleepQualityImpaired 생성 실패")
        self.assertTrue(self._has_state(g, user, PROD.ExerciseSkipped),
                        "Rule 11: ExerciseSkipped 생성 실패")
        self.assertTrue(self._has_state(g, user, PROD.WeeklyActivityLow),
                        "Rule 12: WeeklyActivityLow 생성 실패")
        self.assertTrue(self._has_state(g, user, PROD.BurnoutWarning),
                        "Rule 13: BurnoutWarning 생성 실패")

        # 퀘스트 검증
        quest_titles = self._quest_titles(g)
        self.assertTrue(
            any("이번 주 활동량" in t for t in quest_titles),
            "Rule 13: 인과 체인 종점 퀘스트 생성 실패"
        )
        self.assertTrue(
            any(int(g.value(q, PROD.rewardAmount) or 0) == 50
                for q in g.objects(user, PROD.receivesQuest)
                if "이번 주 활동량" in str(g.value(q, PROD.title) or "")),
            "Rule 13: 인과 체인 퀘스트 rewardAmount≠50"
        )
        print(f"  [{PASS}] 다단계 인과 체인 4단계 모두 발동 성공")

    def test_scenario_8_chain_order_dependency(self):
        """시나리오 8: 인과 체인 순서 의존성 검증"""
        print("\n[Scenario 8] 인과 체인 순서 의존성 (Rule 11 단독 실행 → 미발동)")

        g = self._load_base_graph()
        user = self._add_user(g, "s8")

        # 오후 카페 + 수면질 45 (Rule 10 조건 충족)
        self._add_cafe_locations(g, user, 1, hour=19)
        self._add_sleep(g, user, 6.5, quality=45)

        # Rule 11만 단독 실행 (Rule 10 없이)
        self._apply_rules_in_order(g, ["causal_exercise_skipped"])

        # 검증: Rule 11 미발동 (선행 조건 없음)
        self.assertFalse(self._has_state(g, user, PROD.ExerciseSkipped),
                         "Rule 11이 선행 조건 없이 발동됨 (순서 의존성 위반)")

        # 이제 Rule 10 실행 후 Rule 11 재실행
        self._apply_rules_in_order(g, ["causal_sleep_impaired", "causal_exercise_skipped"])

        # 검증: 이제 Rule 11 발동
        self.assertTrue(self._has_state(g, user, PROD.ExerciseSkipped),
                        "Rule 11이 선행 조건 충족 후에도 미발동")
        print(f"  [{PASS}] 인과 체인 순서 의존성 검증 성공")

    def test_scenario_9_indoor_sunny_quest(self):
        """시나리오 9: IndoorDayPattern + 맑은 날씨 → 강화 퀘스트 (Rule 7, 14)"""
        print("\n[Scenario 9] IndoorDayPattern + 맑은 날씨 → 산책 강화 퀘스트")

        g = self._load_base_graph()
        user = self._add_user(g, "s9")

        # 비 날씨 + 외출 없음
        w = PROD["weather_s9"]
        g.add((w, RDF.type, PROD.Weather))
        g.add((w, PROD.condition, Literal("rainy")))
        g.add((w, PROD.recordedAt, Literal("2026-04-17T12:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasWeather, w))

        # Rule 7 실행 → IndoorDayPattern
        self._apply_rules_in_order(g, ["indoor_day_pattern"])
        self.assertTrue(self._has_state(g, user, PROD.IndoorDayPattern),
                        "Rule 7: IndoorDayPattern 생성 실패")

        # 날씨를 맑음으로 변경 (새 Weather 노드 추가)
        w2 = PROD["weather_s9_sunny"]
        g.add((w2, RDF.type, PROD.Weather))
        g.add((w2, PROD.condition, Literal("sunny and clear")))
        g.add((user, PROD.hasWeather, w2))

        # Rule 14 실행
        self._apply_rules_in_order(g, ["sunny_indoor_quest"])

        # 검증
        self.assertIn("날씨가 맑아요! 지금 딱 산책하기 좋아요", self._quest_titles(g),
                      "Rule 14: 맑은 날 실내 패턴 강화 퀘스트 생성 실패")
        print(f"  [{PASS}] IndoorDayPattern + 맑은 날씨 → 강화 퀘스트 성공")

    def test_scenario_10_music_mood_routine(self):
        """시나리오 10: 음악 청취 + 루틴 (Rule 8, 9)"""
        print("\n[Scenario 10] 음악 청취 → MusicMood + 루틴 감지")

        g = self._load_base_graph()
        user = self._add_user(g, "s10")

        # 재즈 장르 130분 청취
        for i, dur in enumerate([60, 40, 30]):
            ml = PROD[f"music_s10_{i}"]
            g.add((ml, RDF.type, PROD.MusicListening))
            g.add((ml, PROD.genre, Literal("jazz")))
            g.add((ml, PROD.listenDuration, Literal(dur, datatype=XSD.integer)))
            g.add((user, PROD.listensTo, ml))

        # 같은 시간대(9시) 일정 3회
        for i in range(3):
            evt = PROD[f"event_s10_{i}"]
            g.add((evt, RDF.type, PROD.CalendarEvent))
            g.add((evt, PROD.startTime,
                   Literal(f"2026-04-{17+i}T09:00:00", datatype=XSD.dateTime)))
            g.add((user, PROD.hasCalendarEvent, evt))

        # Rule 9, 8 추론
        self._apply_rules_in_order(g, ["music_mood", "routine_detection"])

        # 검증
        self.assertTrue(self._has_state(g, user, PROD.MusicMood),
                        "Rule 9: MusicMood 생성 실패")
        self.assertTrue(self._has_state(g, user, PROD.Routine),
                        "Rule 8: Routine 생성 실패")
        print(f"  [{PASS}] 음악 청취 → MusicMood + 루틴 감지 성공")

    def test_scenario_11_full_pipeline_10_rules(self):
        """시나리오 11: 전체 파이프라인 — 최소 10개 규칙 발동 검증"""
        print("\n[Scenario 11] 전체 파이프라인 — 10개 이상 규칙 발동")

        g = self._load_base_graph()
        user = self._add_user(g, "s11")

        # 다양한 데이터 추가
        # 1. FatigueRisk (Rule 1)
        self._add_sleep(g, user, 5.0)
        self._add_cafe_locations(g, user, 3, hour=10)

        # 2. BurnoutWarning (Rule 2)
        for i in range(3):
            q = PROD[f"quest_ex_{i}"]
            g.add((q, RDF.type, PROD.Quest))
            g.add((q, PROD.questType, Literal("운동")))
            g.add((q, PROD.isCompleted, Literal(False, datatype=XSD.boolean)))
            g.add((user, PROD.receivesQuest, q))

        # 3. SedentaryPattern (Rule 3) — DuckDB
        self._add_step_counts(g, user, [2000, 2100, 2200])

        # 4. PlaceHabit (Rule 4) — placeType=library 추가
        self._add_location(g, user, "도서관", n=3, place_type="library")

        # 5. late_caffeine (Rule 5) — 수면질 저하 필요
        sleep2 = PROD["sleep2_s11"]
        g.add((sleep2, RDF.type, PROD.SleepData))
        g.add((sleep2, PROD.duration, Literal(6.0, datatype=XSD.float)))
        g.add((sleep2, PROD.quality, Literal(50, datatype=XSD.integer)))
        g.add((user, PROD.hasSleepData, sleep2))
        loc_late = PROD["loc_s11_late_cafe"]
        g.add((loc_late, RDF.type, PROD.Location))
        g.add((loc_late, PROD.placeType, Literal("cafe")))
        g.add((loc_late, PROD.visitTime, Literal("2026-04-20T19:00:00", datatype=XSD.dateTime)))
        g.add((user, PROD.hasLocation, loc_late))

        # 6. missing_companion (Rule 6) — companion 없는 위치
        loc_blank = PROD["loc_s11_blank"]
        g.add((loc_blank, RDF.type, PROD.Location))
        g.add((loc_blank, PROD.placeName, Literal("공원")))
        g.add((user, PROD.hasLocation, loc_blank))

        # 7. missing_emotion (Rule 6-B)
        act = PROD["act_s11"]
        g.add((act, RDF.type, PROD.Activity))
        g.add((act, PROD.activityType, Literal("독서")))
        g.add((user, PROD.hasActivity, act))

        # 8. Routine (Rule 8) — 같은 시간대(9시) 일정 3회
        for i in range(3):
            evt = PROD[f"event_s11_{i}"]
            g.add((evt, RDF.type, PROD.CalendarEvent))
            g.add((evt, PROD.startTime,
                   Literal(f"2026-04-{17+i}T09:00:00", datatype=XSD.dateTime)))
            g.add((user, PROD.hasCalendarEvent, evt))

        # 9. MusicMood (Rule 9)
        ml = PROD["music_s11"]
        g.add((ml, RDF.type, PROD.MusicListening))
        g.add((ml, PROD.genre, Literal("jazz")))
        g.add((ml, PROD.listenDuration, Literal(130, datatype=XSD.integer)))
        g.add((user, PROD.listensTo, ml))

        # 10. Persona Active (Rule P1)
        self._add_step_counts(g, user, [8000, 7500, 7200, 7800])

        # DuckDB 집계
        for triple in self._aggregate_with_duckdb(g):
            g.add(triple)

        # 전체 규칙 실행 (RULE_ORDER 순서대로)
        rule_order = [
            "fatigue_risk", "burnout_warning", "sedentary_pattern",
            "place_habit", "late_caffeine_sleep_quality", "missing_companion",
            "missing_emotion", "routine_detection",
            "music_mood", "persona_active",
        ]
        self._apply_rules_in_order(g, rule_order)

        # 검증: 최소 10개 규칙 발동 확인
        fired_rules = []
        if self._has_state(g, user, PROD.FatigueRisk):
            fired_rules.append("Rule 1: FatigueRisk")
        if self._has_state(g, user, PROD.BurnoutWarning):
            fired_rules.append("Rule 2: BurnoutWarning")
        if self._has_state(g, user, PROD.SedentaryPattern):
            fired_rules.append("Rule 3: SedentaryPattern")
        if self._has_state(g, user, PROD.PlaceHabit):
            fired_rules.append("Rule 4: PlaceHabit")
        if any("디카페인" in t for t in self._quest_titles(g)):
            fired_rules.append("Rule 5: late_caffeine")
        if any("공원" in t and "누구랑" in t for t in self._quest_titles(g)):
            fired_rules.append("Rule 6: missing_companion")
        if any("독서" in t and "기분" in t for t in self._quest_titles(g)):
            fired_rules.append("Rule 6-B: missing_emotion")
        if self._has_state(g, user, PROD.Routine):
            fired_rules.append("Rule 8: Routine")
        if self._has_state(g, user, PROD.MusicMood):
            fired_rules.append("Rule 9: MusicMood")
        if "active" in self._persona_values(g, user, PROD.energyType):
            fired_rules.append("Rule P1: persona_active")

        self.assertGreaterEqual(len(fired_rules), 10,
                                f"발동된 규칙 수 부족: {len(fired_rules)}개 (최소 10개 필요)\n"
                                f"발동된 규칙: {fired_rules}")

        # RoomObject 생성 검증 (추론 상태 기반 신규 5종 포함)
        room_objects = list(g.objects(user, PROD.hasRoomObject))
        obj_types = {str(g.value(obj, PROD.objectType)) for obj in room_objects
                     if g.value(obj, PROD.objectType)}
        expected_obj_types = {"tired_pillow", "running_shoes", "bookshelf",
                               "alarm_clock", "music_speaker", "sports_trophy"}
        for ot in expected_obj_types:
            self.assertIn(ot, obj_types,
                          f"RoomObject objectType '{ot}' 생성 실패 (발동된 규칙 기반)\n"
                          f"실제 objectTypes: {obj_types}")

        # objectType 있는 노드에서 inferredFrom 비어있지 않음 확인 (blank node 버그 회귀 방지)
        typed_objs = [obj for obj in room_objects if g.value(obj, PROD.objectType)]
        self.assertGreater(len(typed_objs), 0, "RoomObject objectType 있는 노드 없음")
        for obj in typed_objs:
            self.assertNotEqual(
                str(g.value(obj, PROD.inferredFrom) or ""), "",
                f"RoomObject.inferredFrom 빈값: objectType={g.value(obj, PROD.objectType)} "
                f"(blank node ID 불일치 버그 재발)"
            )

        # placementZone 존재 확인 + positionX/Y/Z 없음 확인
        for obj in room_objects:
            if g.value(obj, PROD.objectType):
                self.assertIsNotNone(
                    g.value(obj, PROD.placementZone),
                    f"RoomObject missing placementZone: {g.value(obj, PROD.objectType)}"
                )
                self.assertIsNone(
                    g.value(obj, PROD.positionX),
                    f"positionX should not exist: {g.value(obj, PROD.objectType)}"
                )

        # Quest rewardAmount 검증 — 모든 Quest는 30 또는 50이어야 함
        for q in g.objects(user, PROD.receivesQuest):
            q_title = g.value(q, PROD.title)
            if q_title:
                reward = g.value(q, PROD.rewardAmount)
                self.assertIsNotNone(reward,
                                     f"Quest missing rewardAmount: {q_title}")
                self.assertIn(int(reward or 0), (30, 50),
                              f"Quest rewardAmount should be 30 or 50, got {reward}: {q_title}")

        print(f"  [{PASS}] 전체 파이프라인 {len(fired_rules)}개 규칙 발동 성공")
        print(f"    발동된 규칙: {', '.join(fired_rules)}")
        print(f"    생성된 RoomObject objectTypes: {sorted(obj_types)}")


# ══════════════════════════════════════════════════════════════════════════════
# 메인 실행
# ══════════════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    # unittest 기본 러너 사용
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromTestCase(TestOntologyPipeline)
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)

    # 최종 결과 출력
    print("\n" + "=" * 70)
    total = result.testsRun
    passed = total - len(result.failures) - len(result.errors)
    print(f"통합 테스트 결과: {passed}/{total} 시나리오 통과")

    if result.failures or result.errors:
        print(f"\n실패한 시나리오:")
        for test, traceback in result.failures + result.errors:
            print(f"  - {test}")
        sys.exit(1)
    else:
        print("모든 통합 테스트 통과 (E2E 파이프라인 검증 완료)")
        sys.exit(0)
