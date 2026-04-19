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

import re
import io
import logging
from datetime import datetime, timezone, timedelta
from typing import Any

import duckdb
import firebase_admin
from firebase_admin import firestore, messaging, storage
from rdflib import Graph, Namespace, URIRef, Literal

logger = logging.getLogger(__name__)

PROD = Namespace("http://7team.dev/ontology#")
RULES_PATH = "Functions/ontology/rules/inference_rules.sparql"

# 추론 규칙 ID 실행 순서 — Rule 2는 Rule 1 결과에 의존하므로 순서 고정
RULE_ORDER = [
    # 독립 규칙
    "fatigue_risk",             # Rule 1
    "burnout_warning",          # Rule 2  (Rule 1 결과 의존)
    "sedentary_pattern",        # Rule 3
    "place_habit",              # Rule 4
    "late_caffeine_sleep_quality",  # Rule 5
    "missing_companion",        # Rule 6
    "missing_emotion",          # Rule 6-B (감정 빈 노드)
    "missing_purpose",          # Rule 6-C (의도 빈 노드)
    "missing_music_mood",       # Rule 6-D (음악 맥락 빈 노드)
    "missing_sleep_cause",      # Rule 6-E (수면 원인 빈 노드)
    "missing_event_review",     # Rule 6-F (일정 후기 빈 노드)
    "indoor_day_pattern",       # Rule 7
    "sunny_indoor_quest",       # Rule 14 (Rule 7 IndoorDayPattern 의존)
    "routine_detection",        # Rule 8
    "music_mood",               # Rule 9
    # 다단계 인과 체인 — 순서 고정 필수
    "causal_sleep_impaired",        # Rule 10
    "causal_exercise_skipped",      # Rule 11 (Rule 10 의존)
    "causal_weekly_activity_low",   # Rule 12 (Rule 11 의존)
    "causal_burnout_from_chain",    # Rule 13 (Rule 12 의존)
    # 페르소나 자동 생성 — 상태 규칙 이후 실행
    "persona_active",               # Rule P1
    "persona_indoor",               # Rule P2  (indoor_day_pattern 의존)
    "persona_social",               # Rule P3
    "persona_solitary",             # Rule P4
    "persona_routine",              # Rule P5  (routine_detection 의존)
    "persona_night_owl",            # Rule P6
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


def _add_triples_to_graph(g: Graph, triples: list[dict]) -> None:
    """Gemma 3n 트리플 dict를 RDFLib 그래프에 추가.

    object 필드 우선순위:
      1. "datatype" 키가 있으면 타입 지정 Literal
      2. "http"로 시작하면 URIRef
      3. 나머지는 plain Literal (추론 FILTER에서 비교 불가할 수 있으므로
         Gemma 3n이 datatype을 함께 전송하도록 권장)
    """
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
    """DuckDB로 연속 저보행 일수 계산 → pre-computed 트리플 반환.

    gaps-and-islands 기법으로 사용자별 최대 연속 저보행(< 3000보) 일수를 구해
    prod:hasConsecutiveLowStepDays 트리플을 생성한다.
    sedentary_pattern 규칙(Rule 3)이 이 트리플을 읽어 판단한다.
    """
    from rdflib import URIRef, Literal
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
        logger.info("DuckDB: no step data in graph")
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

        # gaps-and-islands: 연속된 날짜는 (epoch_days - row_number)가 동일
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

        logger.info("DuckDB consecutive low-step days: %s", results)

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


# ── SPARQL 추론 ──────────────────────────────────────────────────────────────

def _parse_rules(rules_sparql: str) -> dict[str, str]:
    """RULE_ID 주석 기준으로 SPARQL CONSTRUCT 블록 분리."""
    pattern = re.compile(
        r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)"
    )
    return {m.group(1): m.group(2).strip() for m in pattern.finditer(rules_sparql)}


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
    return new_triples


# ── Firestore 저장 ───────────────────────────────────────────────────────────

def _collect_new_quests(g: Graph, new_triples: list[tuple]) -> list[URIRef]:
    """이번 추론에서 새로 생성된 Quest 노드만 반환."""
    new_subjects = {s for s, _, _ in new_triples}
    return [
        s for s in new_subjects
        if (s, PROD.questType, None) in g
    ]


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
            quests_payload.append({
                "title":       title_str,
                "questType":   str(q_type) if q_type else "",
                # Literal.toPython() → Python bool/int/float 변환 (str 변환 금지)
                "isCompleted": is_done.toPython() if is_done is not None else False,
                "createdAt":   str(created) if created else "",
            })
            new_quest_titles.append(title_str)
        db.collection("quests").document(uid).set(
            {"quests": quests_payload}, merge=True
        )
        logger.info("Saved %d new quests for uid=%s", len(quests_payload), uid)

    # room_objects — hasRoomObject로 연결된 노드만 수집
    new_obj_subjects = {s for s, p, _ in new_triples if p == PROD.hasRoomObject}
    if new_obj_subjects:
        room_objs = []
        for obj in new_obj_subjects:
            room_objs.append({
                "objectType":  str(g.value(obj, PROD.objectType) or ""),
                "inferredFrom": str(g.value(obj, PROD.inferredFrom) or ""),
                "positionX":   float(g.value(obj, PROD.positionX) or 0),
                "positionY":   float(g.value(obj, PROD.positionY) or 0),
                "positionZ":   float(g.value(obj, PROD.positionZ) or 0),
            })
        db.collection("room_objects").document(uid).set(
            {"objects": room_objs}, merge=True
        )
        logger.info("Saved %d room_objects for uid=%s", len(room_objs), uid)

    # persona — 복수 Persona 노드를 순회해 속성 병합 저장
    persona_nodes = list(g.objects(user_uri, PROD.hasPersona))
    if persona_nodes:
        persona: dict[str, str] = {
            "energyType": "", "socialPreference": "", "lifePattern": "", "updatedAt": "",
        }
        for pnode in persona_nodes:
            for key, prop in [
                ("energyType",       PROD.energyType),
                ("socialPreference", PROD.socialPreference),
                ("lifePattern",      PROD.lifePattern),
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

    # 5. SPARQL 추론 실행
    rules = _parse_rules(rules_sparql)
    new_triples = _apply_rules(g, rules)

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
