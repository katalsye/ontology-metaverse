"""
ontology_engine.py
Cloud Functions Python — Stateless 온톨로지 추론 엔진

흐름:
  1. Firebase Storage에서 graphs/{uid}/latest.ttl 로드
  2. Firestore temp_triples에서 새 트리플 로드 후 그래프에 추가
  3. 새 트리플 그래프에 병합
  4. DuckDB 집계 → pre-computed 트리플 주입 (연속 저보행 일수 등)
  5. SPARQL CONSTRUCT 추론 규칙 순서대로 실행
  6. 추론 결과를 Firestore(room_objects, quests, users/{uid}/persona)에 저장
  7. 버전 파일(YYYY-MM-DD-HH.ttl) + latest.ttl 저장, 7일 이전 버전 삭제
  8. temp_triples 삭제 → FCM 퀘스트 알림 전송

버전 관리 공개 API:
  list_graph_versions(uid, bucket_name)    → 저장된 버전 목록(최신순)
  restore_graph_version(uid, version_str, bucket_name) → 특정 버전을 latest로 복원
"""

from __future__ import annotations

import os
import re
import io
import logging
from datetime import datetime, timezone, timedelta
from typing import Any

import duckdb
import firebase_admin
from firebase_admin import firestore, messaging, storage
from rdflib import Graph, Namespace, URIRef, Literal, RDF
from triple_validator import validate_required_properties, deduplicate_quests

logger = logging.getLogger(__name__)

PROD = Namespace("http://7team.dev/ontology#")
_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
RULES_PATH = os.path.join(_BASE_DIR, "ontology", "rules", "inference_rules.sparql")

# 추론 규칙 ID 실행 순서 — Rule 2는 Rule 1 결과에 의존하므로 순서 고정
RULE_ORDER = [
    # 독립 규칙
    "fatigue_risk",             # Rule 1
    "burnout_warning",          # Rule 2  (Rule 1 결과 의존)
    "sedentary_pattern",        # Rule 3
    "exercise_quest_generation", # Rule 3-B (저보행일 운동 퀘스트 — Rule 2 의존성 공급)
    "complete_sedentary_pattern",  # A2-1: 30분 산책하기 완료 (StepCount >= 6000)
    "complete_exercise_quest",     # A2-2: 운동 퀘스트 완료 (StepCount >= 8000, 날짜 매칭)
    "place_habit",              # Rule 4
    "late_caffeine_sleep_quality",  # Rule 5
    "missing_companion",        # Rule 6
    "missing_emotion",          # Rule 6-B (감정 빈 노드)
    "missing_purpose",          # Rule 6-C (의도 빈 노드)
    "missing_music_mood",       # Rule 6-D (음악 맥락 빈 노드)
    "missing_sleep_cause",      # Rule 6-E (수면 원인 빈 노드)
    "missing_event_review",      # Rule 6-F (일정 후기 빈 노드)
    # 데이터 보완형 퀘스트 자동 완료 (Phase 1, #116)
    "complete_missing_companion",    # Rule C1
    "complete_missing_emotion",      # Rule C2
    "complete_missing_purpose",      # Rule C3
    "complete_missing_music_mood",   # Rule C4
    "complete_missing_sleep_cause",  # Rule C5
    "complete_missing_event_review", # Rule C6
    "indoor_day_pattern",       # Rule 7
    "sunny_indoor_quest",       # Rule 14 (Rule 7 IndoorDayPattern 의존)
    "complete_sunny_indoor_quest", # A2-4: 날씨 맑음 + StepCount >= 5000
    "routine_detection",        # Rule 8
    "music_mood",               # Rule 9
    # Spotify 음악 청취 패턴 기반 감정 상태 추론 (Issue #16)
    "focus_music_pattern",      # Rule 26 (집중 음악 패턴)
    "stress_music_pattern",     # Rule 27 (스트레스 음악 패턴)
    "complete_stress_reason",      # C: 힘든 이유 입력 시 완료 (prod:reason)
    "social_music_pattern",     # Rule 28 (사교 음악 패턴)
    "schedule_overload",               # Rule 29 (Google Calendar 일정 과부하)
    # 다단계 인과 체인 — 순서 고정 필수
    "causal_sleep_impaired",        # Rule 10
    "causal_exercise_skipped",      # Rule 11 (Rule 10 의존)
    "causal_weekly_activity_low",   # Rule 12 (Rule 11 의존)
    "causal_burnout_from_chain",    # Rule 13 (Rule 12 의존)
    "complete_causal_burnout",      # A2-3: 인과 번아웃 퀘스트 완료 (StepCount >= 6000)
    # 페르소나 자동 생성 — 상태 규칙 이후 실행
    "persona_active",               # Rule P1
    "persona_indoor",               # Rule P2  (indoor_day_pattern 의존)
    "persona_social",               # Rule P3
    "persona_solitary",             # Rule P4
    "persona_routine",              # Rule P5  (routine_detection 의존)
    "persona_night_owl",            # Rule P6
    "recovery_deficit_persona",     # Rule P7  (HRV 회복 부족 페르소나)
    # 바이오 데이터 기반 상태 추론
    "high_resting_hr_stress",       # Rule 30  (안정시 심박 스트레스)
    "complete_high_resting_hr",     # A2-5: 심박 정상화 완료 (bpm < 90.0)
]


# ── Firebase 초기화 ──────────────────────────────────────────────────────────

def _init_firebase() -> None:
    if not firebase_admin._apps:
        firebase_admin.initialize_app()


# ── 그래프 로드 ──────────────────────────────────────────────────────────────

def _load_graph_from_storage(bucket_name: str, uid: str) -> Graph:
    """Storage에서 graphs/{uid}/latest.ttl 로드. 없으면 빈 그래프 반환."""
    g = Graph()
    g.bind("prod", PROD)
    bucket = storage.bucket(bucket_name)
    blob = bucket.blob(f"graphs/{uid}/latest.ttl")
    if blob.exists():
        ttl_bytes = blob.download_as_bytes()
        g.parse(data=ttl_bytes.decode(), format="turtle")
        logger.info("Graph loaded: %d triples", len(g))
    else:
        logger.info("No existing graph for uid=%s — starting fresh", uid)
    return g


# ── 트리플 처리 ──────────────────────────────────────────────────────────────

def _load_temp_triples(db: firestore.Client, uid: str) -> list[dict]:
    docs = (
        db.collection("temp_triples")
        .document(uid)
        .collection("items")
        .stream()
    )
    return [d.to_dict() for d in docs]


# 모듈 수준 lazy validator — cold start에서 한 번만 초기화됨 (read-only이므로 Stateless 위반 아님)
_validator: Any = None


def _get_validator() -> Any:
    global _validator
    if _validator is None:
        try:
            from triple_validator import TripleValidator
            _validator = TripleValidator()
        except Exception as exc:
            logger.error("TripleValidator 로드 실패: %s — 검증 없이 진행", exc)
    return _validator


def _add_triples_to_graph(g: Graph, triples: list[dict]) -> None:
    """Gemma 3n 트리플 dict를 RDFLib 그래프에 추가.

    triple_validator.validate() 통과한 트리플만 추가한다.
    object 필드 우선순위 (검증 후):
      1. "datatype" 키가 있으면 타입 지정 Literal
      2. "http"로 시작하면 URIRef (Object Property 대상)
      3. 나머지는 plain Literal
    """
    validator = _get_validator()
    if validator is not None:
        triples, _ = validator.validate(triples)

    for t in triples:
        s = URIRef(t["subject"])
        p = URIRef(t["predicate"])
        o_val = t["object"]
        datatype = t.get("datatype")
        if datatype:
            o: Any = Literal(o_val, datatype=URIRef(datatype))
        elif isinstance(o_val, str) and o_val.startswith("http"):
            o = URIRef(o_val)
        else:
            o = Literal(o_val)
        g.add((s, p, o))


# ── DuckDB 집계 ──────────────────────────────────────────────────────────────

def _aggregate_with_duckdb(g: Graph) -> list[tuple]:
    """DuckDB로 연속 저보행 일수 + 주말 카페인 가중치 계산 → pre-computed 트리플 반환.

    1. gaps-and-islands 기법으로 사용자별 최대 연속 저보행(< 3000보) 일수를 구해
       prod:hasConsecutiveLowStepDays 트리플을 생성한다.
       sedentary_pattern 규칙(Rule 3)이 이 트리플을 읽어 판단한다.

    2. CalendarEvent 기반 주말 감지 + 카페인 가중치 계산:
       - 연속 2일 캘린더 일정 공백 → 주말 프록시
       - 주말 카페 방문: 가중치 ×1.5
       - 평일 카페 방문: 가중치 ×1.0
       → prod:hasWeekendCaffeineScore 트리플 생성
       fatigue_risk 규칙(Rule 1)이 이 트리플을 읽어 판단한다.
    """
    from rdflib import URIRef, Literal
    from rdflib.namespace import XSD

    # ── 1. 연속 저보행 일수 계산 (기존 로직) ──────────────────────────────────
    step_rows = list(g.query("""
        PREFIX prod: <http://7team.dev/ontology#>
        SELECT ?user ?date ?count WHERE {
            ?user prod:hasStepCount ?s .
            ?s prod:count ?count ;
               prod:date  ?date .
        }
    """))

    # ── 2. 캘린더 일정 수집 (주말 감지용) ─────────────────────────────────────
    calendar_rows = list(g.query("""
        PREFIX prod: <http://7team.dev/ontology#>
        SELECT ?user ?startTime WHERE {
            ?user prod:hasCalendarEvent ?evt .
            ?evt  prod:startTime ?startTime .
        }
    """))

    # ── 3. 카페 방문 수집 (카페인 섭취 프록시) ────────────────────────────────
    cafe_rows = list(g.query("""
        PREFIX prod: <http://7team.dev/ontology#>
        SELECT ?user ?visitTime WHERE {
            ?user prod:hasLocation ?loc .
            ?loc  prod:placeType "cafe" ;
                  prod:visitTime ?visitTime .
        }
    """))

    if not step_rows and not cafe_rows:
        logger.info("DuckDB: no step or cafe data in graph")
        return []

    con = duckdb.connect()
    triples: list[tuple] = []

    try:
        # ── 테이블 생성 ──────────────────────────────────────────────────────
        con.execute("""
            CREATE TABLE steps (
                user_uri  VARCHAR,
                step_date DATE,
                step_cnt  INTEGER
            )
        """)
        con.execute("""
            CREATE TABLE calendar_events (
                user_uri   VARCHAR,
                event_date DATE
            )
        """)
        con.execute("""
            CREATE TABLE cafe_visits (
                user_uri  VARCHAR,
                visit_date DATE
            )
        """)

        # ── 데이터 삽입 ──────────────────────────────────────────────────────
        if step_rows:
            con.executemany(
                "INSERT INTO steps VALUES (?, TRY_CAST(? AS DATE), TRY_CAST(? AS INTEGER))",
                [(str(r[0]), str(r[1]), str(r[2])) for r in step_rows],
            )

        if calendar_rows:
            con.executemany(
                "INSERT INTO calendar_events VALUES (?, TRY_CAST(? AS DATE))",
                [(str(r[0]), str(r[1])[:10]) for r in calendar_rows],  # dateTime → date 변환
            )

        if cafe_rows:
            con.executemany(
                "INSERT INTO cafe_visits VALUES (?, TRY_CAST(? AS DATE))",
                [(str(r[0]), str(r[1])[:10]) for r in cafe_rows],
            )

        # ── 1. 연속 저보행 일수 계산 (기존 로직) ──────────────────────────────
        if step_rows:
            low_step_results = con.execute("""
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

            logger.info("DuckDB consecutive low-step days: %s", low_step_results)

            for user_uri_str, max_consec in low_step_results:
                triples.append((
                    URIRef(user_uri_str),
                    PROD.hasConsecutiveLowStepDays,
                    Literal(int(max_consec), datatype=XSD.integer),
                ))

        # ── 2. 주말 감지 + 카페인 가중치 계산 ──────────────────────────────────
        if cafe_rows:
            # 주말 프록시: 캘린더 일정 없는 날 중 연속 2일
            # (실제 주말 또는 휴일로 추정)
            weekend_caffeine_results = con.execute("""
                WITH all_dates AS (
                    SELECT DISTINCT visit_date AS dt
                    FROM cafe_visits
                ),
                event_dates AS (
                    SELECT DISTINCT event_date AS dt
                    FROM calendar_events
                ),
                no_event_dates AS (
                    SELECT ad.dt
                    FROM all_dates ad
                    LEFT JOIN event_dates ed ON ad.dt = ed.dt
                    WHERE ed.dt IS NULL
                ),
                weekend_proxy AS (
                    SELECT
                        dt,
                        LEAD(dt) OVER (ORDER BY dt) AS next_dt
                    FROM no_event_dates
                ),
                weekend_dates AS (
                    SELECT dt AS weekend_date
                    FROM weekend_proxy
                    WHERE next_dt = dt + INTERVAL 1 DAY
                    UNION
                    SELECT next_dt AS weekend_date
                    FROM weekend_proxy
                    WHERE next_dt = dt + INTERVAL 1 DAY
                ),
                cafe_with_weight AS (
                    SELECT
                        cv.user_uri,
                        cv.visit_date,
                        CASE
                            WHEN wd.weekend_date IS NOT NULL THEN 1.5
                            ELSE 1.0
                        END AS weight
                    FROM cafe_visits cv
                    LEFT JOIN weekend_dates wd ON cv.visit_date = wd.weekend_date
                )
                SELECT user_uri, SUM(weight) AS total_weighted_score
                FROM cafe_with_weight
                GROUP BY user_uri
            """).fetchall()

            logger.info("DuckDB weekend caffeine scores: %s", weekend_caffeine_results)

            for user_uri_str, score in weekend_caffeine_results:
                triples.append((
                    URIRef(user_uri_str),
                    PROD.hasWeekendCaffeineScore,
                    Literal(float(score), datatype=XSD.float),
                ))

        return triples

    finally:
        con.close()


# ── SPARQL 추론 ──────────────────────────────────────────────────────────────

def _parse_rules(rules_sparql: str) -> dict[str, str]:
    """RULE_ID 주석 기준으로 SPARQL CONSTRUCT 블록 분리."""
    pattern = re.compile(
        r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)"
    )
    return {m.group(1): m.group(2).strip() for m in pattern.finditer(rules_sparql)}


def _clear_existing_persona_nodes(g: Graph) -> int:
    """기존 Persona 노드와 관련 트리플 전체 제거.

    CONSTRUCT 규칙이 매 사이클마다 새 blank node Persona를 생성하므로
    이전 사이클 잔재를 정리하지 않으면 무한 누적된다.
    """
    removed_count = 0
    for p_node in list(g.subjects(RDF.type, PROD.Persona)):
        for p, o in list(g.predicate_objects(p_node)):
            g.remove((p_node, p, o))
            removed_count += 1
        for s, p in list(g.subject_predicates(p_node)):
            g.remove((s, p, p_node))
            removed_count += 1
    if removed_count > 0:
        logger.info("Cleared %d Persona triples (pre-cycle cleanup)", removed_count)
    return removed_count


def _clear_empty_title_quests(g: Graph) -> int:
    """title 없는 Quest 노드와 관련 트리플 제거.

    Phase 1 이전 추론 사이클에서 생성된 빈 title Quest 잔재 및
    비정상 CONSTRUCT 결과물을 그래프에서 제거한다.
    """
    removed_count = 0
    for q in list(g.subjects(RDF.type, PROD.Quest)):
        if g.value(q, PROD.title) is None:
            for p, o in list(g.predicate_objects(q)):
                g.remove((q, p, o))
                removed_count += 1
            for s, p in list(g.subject_predicates(q)):
                g.remove((s, p, q))
                removed_count += 1
    if removed_count > 0:
        logger.info("Cleared %d empty-title Quest triples (pre-cycle cleanup)", removed_count)
    return removed_count


def _apply_rules(g: Graph, rules: dict[str, str]) -> list[tuple]:
    """순서대로 CONSTRUCT 추론 실행 — 결과 트리플을 그래프에 누적.

    result를 list()로 먼저 소비한 뒤 추가해야 이중 소비 버그를 피할 수 있음.
    """
    new_triples: list[tuple] = []
    for rule_id in RULE_ORDER:
        sparql = rules.get(rule_id)
        if not sparql:
            logger.warning("Rule not found: %s", rule_id)
            continue
        try:
            result = list(g.query(sparql))   # 한 번만 소비
            for triple in result:
                g.add(triple)
            new_triples.extend(result)
            logger.info("Rule %s → %d new triples", rule_id, len(result))
        except Exception as exc:
            logger.error("Rule %s failed: %s", rule_id, exc)
    # complete_* 규칙 실행 후 isCompleted 충돌 해소:
    # isCompleted=True가 생성된 Quest의 isCompleted=False 제거 (멱등성)
    for quest in list(g.subjects(PROD.isCompleted, Literal(True))):
        g.remove((quest, PROD.isCompleted, Literal(False)))
    return new_triples



# ── Firestore 저장 ───────────────────────────────────────────────────────────

def _collect_new_quests(g: Graph, new_triples: list[tuple]) -> list[URIRef]:
    """이번 추론에서 새로 생성된 Quest 노드만 반환."""
    new_subjects = {s for s, _, _ in new_triples}
    return [
        s for s in new_subjects
        if (s, PROD.questType, None) in g
    ]


def _clear_existing_room_objects(g: Graph, user_uri: URIRef) -> int:
    """
    User에 연결된 모든 RoomObject와 관련 트리플 제거.
    매 추론마다 RoomObject를 새로 생성하기 위함 (유니티팀 합의 ⑥).

    제거 대상:
      1. ?obj ?p ?o  (RoomObject의 모든 속성 트리플)
      2. ?user prod:hasRoomObject ?obj

    Returns: 제거된 트리플 수
    """
    removed_count = 0
    room_objs = list(g.objects(user_uri, PROD.hasRoomObject))
    for obj in room_objs:
        for p, o in list(g.predicate_objects(obj)):
            g.remove((obj, p, o))
            removed_count += 1
        g.remove((user_uri, PROD.hasRoomObject, obj))
        removed_count += 1
    if removed_count > 0:
        logger.info("Cleared %d previous RoomObject triples for %s",
                    removed_count, user_uri)
    return removed_count


def _save_results_to_firestore(
    db: firestore.Client,
    uid: str,
    g: Graph,
    new_triples: list[tuple],
) -> list[str]:
    """추론 결과를 Firestore에 저장. 새로 생성된 퀘스트 title 목록 반환."""
    user_uri = URIRef(f"http://7team.dev/ontology#user_{uid}")

    # quests — 이번 배치에서 새로 생성된 것만 저장
    new_quest_titles: list[str] = []
    new_quest_nodes = _collect_new_quests(g, new_triples)
    if new_quest_nodes:
        quests_payload = []
        for quest in new_quest_nodes:
            title    = g.value(quest, PROD.title)
            q_type   = g.value(quest, PROD.questType)
            is_done  = g.value(quest, PROD.isCompleted)
            created  = g.value(quest, PROD.createdAt)
            title_str = str(title) if title else ""
            reward_amt = g.value(quest, PROD.rewardAmount)
            is_done_bool = is_done.toPython() if is_done is not None else False
            quests_payload.append({
                "title":           title_str,
                "questType":       str(q_type) if q_type else "",
                "rewardAmount":    int(reward_amt.toPython()) if reward_amt is not None else 0,
                "isCompleted":     is_done_bool,
                "createdAt":       str(created) if created else "",
                "targetEntityUri": str(g.value(quest, PROD.targetEntity)) if g.value(quest, PROD.targetEntity) else "",
                "targetValue":     str(g.value(quest, PROD.targetValue)) if g.value(quest, PROD.targetValue) else "",
                "completedAt":     str(g.value(quest, PROD.completedAt)) if g.value(quest, PROD.completedAt) else "",
            })
            if not is_done_bool:  # 완료된 퀘스트는 FCM 알림 제외
                new_quest_titles.append(title_str)
        # 빈 title은 Firestore에 저장하지 않음 (그래프 정리 누락 시 이중 방어)
        quests_payload = [q for q in quests_payload if q.get("title", "").strip()]
        if not quests_payload:
            logger.info("No valid quests to save for uid=%s (all filtered)", uid)
            return new_quest_titles
        db.collection("quests").document(uid).set(
            {"quests": quests_payload}, merge=True
        )
        logger.info("Saved %d new quests for uid=%s", len(quests_payload), uid)

    # room_objects — 그래프에서 직접 순회 (new_triples blank node ID 불일치 버그 수정)
    all_obj_subjects = list(g.objects(user_uri, PROD.hasRoomObject))
    if all_obj_subjects:
        room_objs = []
        for obj in all_obj_subjects:
            room_objs.append({
                "objectType":           str(g.value(obj, PROD.objectType) or ""),
                "inferredFrom":         str(g.value(obj, PROD.inferredFrom) or ""),
                "placementZone":        str(g.value(obj, PROD.placementZone) or "floor"),
                "inferredFromConcept":  str(g.value(obj, PROD.inferredFromConcept) or ""),
            })
        db.collection("room_objects").document(uid).set(
            {"objects": room_objs}, merge=True
        )
        logger.info("Saved %d room_objects for uid=%s", len(room_objs), uid)

        # room_snapshots — 추론 완료 시점마다 스냅샷 저장 (유니티팀 합의 ②)
        # 같은 날 재추론 시 덮어쓰기 (set)
        snapshot_date = datetime.now(timezone.utc).strftime("%Y-%m-%d")
        db.collection("room_snapshots") \
            .document(uid) \
            .collection("snapshots") \
            .document(snapshot_date) \
            .set({
                "objects":   room_objs,
                "createdAt": datetime.now(timezone.utc),
            })
        logger.info("Saved room_snapshot for uid=%s date=%s (%d objects)",
                    uid, snapshot_date, len(room_objs))

        # 팔로워 방 업데이트 알림 (유니티팀 합의 ④)
        # FCM 실패는 추론 결과에 영향 없음 — fcm_sender 내부에서 경고만 남김
        try:
            from fcm_sender import send_room_updated_to_followers
            sent = send_room_updated_to_followers(db, messaging, uid)
            if sent > 0:
                logger.info("FCM room_updated sent to %d followers for uid=%s",
                            sent, uid)
        except Exception as exc:
            logger.warning("FCM room_updated 전송 실패 (무시): %s", exc)

    # persona — 복수 Persona 노드를 순회해 속성 병합 저장
    persona_nodes = list(g.objects(user_uri, PROD.hasPersona))
    if persona_nodes:
        persona: dict[str, str] = {
            "energyType": "", "socialPreference": "", "lifePattern": "",
            "recoveryLevel": "", "updatedAt": "",
        }
        for pnode in persona_nodes:
            for key, prop in [
                ("energyType",       PROD.energyType),
                ("socialPreference", PROD.socialPreference),
                ("lifePattern",      PROD.lifePattern),
                ("recoveryLevel",    PROD.recoveryLevel),
                ("updatedAt",        PROD.updatedAt),
            ]:
                if not persona[key]:
                    val = g.value(pnode, prop)
                    if val:
                        persona[key] = str(val)
        db.collection("users").document(uid).set({"persona": persona}, merge=True)

    return new_quest_titles


# ── FCM 알림 ─────────────────────────────────────────────────────────────────

def _send_quest_notifications(uid: str, quest_titles: list[str]) -> None:
    """새 퀘스트가 있으면 FCM Topic 메시지 전송.

    Android 앱은 /topics/{uid} 구독 필요.
    무료 티어 안에서 동작 (FCM은 무료).
    """
    if not quest_titles:
        return

    body = quest_titles[0] if len(quest_titles) == 1 else f"퀘스트 {len(quest_titles)}개가 도착했어요!"
    message = messaging.Message(
        notification=messaging.Notification(
            title="새 퀘스트 도착!",
            body=body,
        ),
        data={"questCount": str(len(quest_titles))},
        topic=f"quests_{uid}",
    )
    try:
        msg_id = messaging.send(message)
        logger.info("FCM sent: %s (uid=%s)", msg_id, uid)
    except Exception as exc:
        logger.error("FCM send failed (uid=%s): %s", uid, exc)


# ── 그래프 저장 / 정리 ───────────────────────────────────────────────────────

def _prune_old_versions(bucket: Any, uid: str, now: datetime) -> None:
    """7일 이전 버전 파일 삭제. latest.ttl은 건드리지 않음."""
    cutoff = now - timedelta(days=7)
    prefix = f"graphs/{uid}/"
    for blob in bucket.list_blobs(prefix=prefix):
        name = blob.name[len(prefix):]          # e.g. "2026-04-12-10.ttl"
        if name in ("latest.ttl", ""):
            continue
        try:
            version_dt = datetime.strptime(name[:-4], "%Y-%m-%d-%H").replace(
                tzinfo=timezone.utc
            )
        except ValueError:
            continue
        if version_dt < cutoff:
            blob.delete()
            logger.info("Pruned old version: %s", blob.name)


def _save_graph_to_storage(g: Graph, bucket_name: str, uid: str) -> None:
    """버전 파일(YYYY-MM-DD-HH.ttl)과 latest.ttl을 동시에 저장.
    저장 후 7일 이전 버전을 자동 삭제한다.
    """
    ttl_bytes = g.serialize(format="turtle").encode()
    bucket = storage.bucket(bucket_name)
    now = datetime.now(timezone.utc)
    version_str = now.strftime("%Y-%m-%d-%H")

    for path in (f"graphs/{uid}/{version_str}.ttl", f"graphs/{uid}/latest.ttl"):
        bucket.blob(path).upload_from_file(
            io.BytesIO(ttl_bytes), content_type="text/turtle"
        )

    logger.info("Graph saved: %d triples (version=%s)", len(g), version_str)
    _prune_old_versions(bucket, uid, now)


def _delete_temp_triples(db: firestore.Client, uid: str) -> None:
    items_ref = (
        db.collection("temp_triples").document(uid).collection("items")
    )
    for doc in items_ref.stream():
        doc.reference.delete()
    db.collection("temp_triples").document(uid).delete()


# ── 버전 관리 공개 API ───────────────────────────────────────────────────────

def list_graph_versions(uid: str, bucket_name: str) -> list[str]:
    """저장된 버전 날짜 목록을 최신순으로 반환. latest.ttl 제외.

    반환 형식: ["2026-04-19-14", "2026-04-18-08", ...]
    """
    _init_firebase()
    bucket = storage.bucket(bucket_name)
    prefix = f"graphs/{uid}/"
    versions = []
    for blob in bucket.list_blobs(prefix=prefix):
        name = blob.name[len(prefix):]
        if name in ("latest.ttl", "") or not name.endswith(".ttl"):
            continue
        versions.append(name[:-4])
    return sorted(versions, reverse=True)


def restore_graph_version(uid: str, version_str: str, bucket_name: str) -> bool:
    """특정 버전(YYYY-MM-DD-HH)을 latest.ttl로 복원. 성공 시 True 반환."""
    _init_firebase()
    bucket = storage.bucket(bucket_name)
    src_blob = bucket.blob(f"graphs/{uid}/{version_str}.ttl")
    if not src_blob.exists():
        logger.error("Version not found: graphs/%s/%s.ttl", uid, version_str)
        return False
    ttl_bytes = src_blob.download_as_bytes()
    bucket.blob(f"graphs/{uid}/latest.ttl").upload_from_file(
        io.BytesIO(ttl_bytes), content_type="text/turtle"
    )
    logger.info("Restored version %s → latest for uid=%s", version_str, uid)
    return True


# ── 메인 진입점 ──────────────────────────────────────────────────────────────

def run_inference(uid: str, bucket_name: str, rules_sparql: str) -> dict:
    """Cloud Functions 진입점에서 호출하는 메인 함수."""
    _init_firebase()
    db = firestore.client()

    # 1. Storage에서 그래프 로드
    g = _load_graph_from_storage(bucket_name, uid)

    # 2. Firestore temp_triples 확인
    temp_triples = _load_temp_triples(db, uid)
    if not temp_triples:
        logger.info("No new triples for uid=%s — skipping", uid)
        return {"status": "no_new_triples"}

    # 3. 새 트리플 그래프에 추가
    _add_triples_to_graph(g, temp_triples)

    # 4. DuckDB 집계 → pre-computed 트리플 그래프에 주입
    #    (Rule 3 sedentary_pattern이 hasConsecutiveLowStepDays를 읽으므로
    #     반드시 SPARQL 추론 전에 실행해야 함)
    for triple in _aggregate_with_duckdb(g):
        g.add(triple)

    # 4-c. 기존 Persona 노드 정리 (CONSTRUCT가 매 사이클마다 새 blank node 생성)
    _clear_existing_persona_nodes(g)

    # 4-d. 빈 title Quest 정리 (Phase 1 이전 잔재 + 비정상 생성 방지)
    _clear_empty_title_quests(g)

    # 4-e. 이전 추론 RoomObject 정리 (유니티팀 합의 ⑥ — 매 추론마다 전체 재생성)
    #      temp_triples 확인 이후에 실행해야 조기 종료 시 기존 RoomObject 보존
    _clear_existing_room_objects(g, URIRef(f"http://7team.dev/ontology#user_{uid}"))

    # 5. SPARQL 추론 실행
    rules = _parse_rules(rules_sparql)
    new_triples = _apply_rules(g, rules)

    # 5-a. 중복 Quest 후처리 제거
    #      RDFLib CONSTRUCT는 WHERE 매칭 결과를 모두 모은 후 한 번에 추가하므로
    #      FILTER NOT EXISTS가 동시 실행 중 중복을 막지 못하는 한계가 있음.
    #      후처리로 동일 user+questType+title 조합의 중복 Quest를 제거한다.
    removed_count = deduplicate_quests(g)
    if removed_count > 0:
        logger.info("중복 Quest %d개 제거됨", removed_count)

    # 5-b. 필수 속성 누락 경고 감지
    prop_warnings = validate_required_properties(g)
    for w in prop_warnings:
        logger.warning(w)

    # 6. Firestore에 결과 저장
    new_quest_titles = _save_results_to_firestore(db, uid, g, new_triples)

    # 7. 그래프 직렬화 → Storage 저장 → temp_triples 삭제
    _save_graph_to_storage(g, bucket_name, uid)
    _delete_temp_triples(db, uid)

    # 8. FCM 퀘스트 알림
    _send_quest_notifications(uid, new_quest_titles)

    return {
        "status": "ok",
        "triples": len(g),
        "new_quests": new_quest_titles,
        "missing_property_warnings": prop_warnings,
    }


# ── Cloud Functions HTTP handler ─────────────────────────────────────────────

def infer(request):  # type: ignore[no-untyped-def]
    """Cloud Functions Python HTTP 진입점."""
    import os

    data = request.get_json(silent=True) or {}
    uid = data.get("uid")
    if not uid:
        return ("uid required", 400)

    bucket_name = os.environ.get("STORAGE_BUCKET", "")
    if not bucket_name:
        return ("STORAGE_BUCKET env var not set", 500)

    with open(RULES_PATH, encoding="utf-8") as f:
        rules_sparql = f.read()

    result = run_inference(uid, bucket_name, rules_sparql)
    return (result, 200)
