"""
ontology_engine.py
Cloud Functions Python — Stateless 온톨로지 추론 엔진

흐름:
  1. Firebase Storage에서 기존 .ttl 그래프 로드
  2. Firestore temp_triples에서 새 트리플 로드 후 그래프에 추가
  3. DuckDB로 집계/패턴 분석
  4. SPARQL CONSTRUCT 추론 규칙 순서대로 실행
  5. 추론 결과를 Firestore(room_objects, quests, users/{uid}/persona)에 저장
  6. 갱신된 그래프 Storage에 직렬화 저장 후 temp_triples 삭제
  7. 새 퀘스트가 있으면 FCM으로 알림 전송
"""

from __future__ import annotations

import re
import io
import logging
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
    "fatigue_risk",
    "burnout_warning",
    "sedentary_pattern",
    "place_habit",
    "late_caffeine_sleep_quality",
    "missing_companion",
    "indoor_day_pattern",
    "routine_detection",
    "music_mood",
]


# ── Firebase 초기화 ──────────────────────────────────────────────────────────

def _init_firebase() -> None:
    if not firebase_admin._apps:
        firebase_admin.initialize_app()


# ── 그래프 로드 ──────────────────────────────────────────────────────────────

def _load_graph_from_storage(bucket_name: str, uid: str) -> Graph:
    """Storage에서 사용자별 .ttl 로드. 없으면 빈 그래프 반환."""
    g = Graph()
    g.bind("prod", PROD)
    bucket = storage.bucket(bucket_name)
    blob = bucket.blob(f"graphs/{uid}.ttl")
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

def _aggregate_with_duckdb(triples: list[dict]) -> dict[str, Any]:
    """DuckDB로 집계 분석 — 현재는 로깅 및 향후 연속일 체크에 활용."""
    if not triples:
        return {}
    con = duckdb.connect()
    con.execute(
        "CREATE TABLE triples (subject VARCHAR, predicate VARCHAR, object VARCHAR)"
    )
    con.executemany(
        "INSERT INTO triples VALUES (?, ?, ?)",
        [(t["subject"], t["predicate"], t["object"]) for t in triples],
    )

    step_agg = con.execute(
        "SELECT subject, COUNT(*) as low_day_cnt "
        "FROM triples "
        "WHERE predicate = 'http://7team.dev/ontology#count' "
        "  AND TRY_CAST(object AS INTEGER) < 3000 "
        "GROUP BY subject"
    ).fetchall()

    app_agg = con.execute(
        "SELECT object, SUM(TRY_CAST(object AS INTEGER)) as total_min "
        "FROM triples "
        "WHERE predicate = 'http://7team.dev/ontology#usageDuration' "
        "GROUP BY object"
    ).fetchall()

    con.close()
    result = {"step_low_days": step_agg, "app_usage": app_agg}
    logger.info("DuckDB aggregation: %s", result)
    return result


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

    # persona — User 노드에 연결된 Persona 업데이트
    persona_node = g.value(user_uri, PROD.hasPersona)
    if persona_node:
        persona = {
            "energyType":       str(g.value(persona_node, PROD.energyType) or ""),
            "socialPreference": str(g.value(persona_node, PROD.socialPreference) or ""),
            "lifePattern":      str(g.value(persona_node, PROD.lifePattern) or ""),
            "updatedAt":        str(g.value(persona_node, PROD.updatedAt) or ""),
        }
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

def _save_graph_to_storage(g: Graph, bucket_name: str, uid: str) -> None:
    ttl_bytes = g.serialize(format="turtle").encode()
    bucket = storage.bucket(bucket_name)
    blob = bucket.blob(f"graphs/{uid}.ttl")
    blob.upload_from_file(io.BytesIO(ttl_bytes), content_type="text/turtle")
    logger.info("Graph saved: %d triples", len(g))


def _delete_temp_triples(db: firestore.Client, uid: str) -> None:
    items_ref = (
        db.collection("temp_triples").document(uid).collection("items")
    )
    for doc in items_ref.stream():
        doc.reference.delete()
    db.collection("temp_triples").document(uid).delete()


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

    # 3. DuckDB 집계 (로깅 및 향후 연속일 체크용)
    _aggregate_with_duckdb(temp_triples)

    # 4. 새 트리플 그래프에 추가
    _add_triples_to_graph(g, temp_triples)

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
