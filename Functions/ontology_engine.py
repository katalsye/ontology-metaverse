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
"""

from __future__ import annotations

import re
import io
import logging
from typing import Any

import duckdb
import firebase_admin
from firebase_admin import credentials, firestore, storage
from rdflib import Graph, Namespace, URIRef, Literal
from rdflib.plugins.sparql import prepareQuery

logger = logging.getLogger(__name__)

PROD = Namespace("http://7team.dev/ontology#")
ONTOLOGY_TTL_PATH = "ontology/core.ttl"
RULES_PATH = "Functions/ontology/rules/inference_rules.sparql"

# 추론 규칙 ID 순서 (inference_rules.sparql 내 RULE_ID 주석 기준)
RULE_ORDER = [
    "fatigue_risk",
    "burnout_warning",
    "sedentary_pattern",
    "place_habit",
    "late_caffeine_sleep_quality",
    "missing_companion",
]


def _init_firebase() -> None:
    if not firebase_admin._apps:
        firebase_admin.initialize_app()


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


def _load_temp_triples(db: firestore.Client, uid: str) -> list[dict]:
    docs = db.collection("temp_triples").document(uid).collection("items").stream()
    return [d.to_dict() for d in docs]


def _add_triples_to_graph(g: Graph, triples: list[dict]) -> None:
    """Gemma 3n이 생성한 트리플 dict를 RDFLib 그래프에 추가."""
    for t in triples:
        s = URIRef(t["subject"])
        p = URIRef(t["predicate"])
        o_val = t["object"]
        o = URIRef(o_val) if o_val.startswith("http") else Literal(o_val)
        g.add((s, p, o))


def _aggregate_with_duckdb(triples: list[dict]) -> dict[str, Any]:
    """DuckDB로 집계 분석 — 추론 규칙 보조 데이터."""
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
    step_counts = con.execute(
        "SELECT subject, COUNT(*) as cnt FROM triples "
        "WHERE predicate = 'http://7team.dev/ontology#count' GROUP BY subject"
    ).fetchall()
    con.close()
    return {"step_counts": step_counts}


def _parse_rules(rules_sparql: str) -> dict[str, str]:
    """RULE_ID 주석 기준으로 SPARQL 블록을 분리."""
    blocks: dict[str, str] = {}
    pattern = re.compile(r"#\s*RULE_ID:\s*(\w+)\s*\n(CONSTRUCT[\s\S]+?)(?=\n#\s*RULE_ID:|\Z)")
    for m in pattern.finditer(rules_sparql):
        blocks[m.group(1)] = m.group(2).strip()
    return blocks


def _apply_rules(g: Graph, rules: dict[str, str]) -> list[tuple]:
    """순서대로 CONSTRUCT 추론 실행 — 결과 트리플을 그래프에 누적."""
    new_triples: list[tuple] = []
    for rule_id in RULE_ORDER:
        sparql = rules.get(rule_id)
        if not sparql:
            logger.warning("Rule not found: %s", rule_id)
            continue
        try:
            result = g.query(sparql)
            for triple in result:
                g.add(triple)
                new_triples.append(triple)
            logger.info("Rule %s → %d new triples", rule_id, len(list(result)))
        except Exception as exc:
            logger.error("Rule %s failed: %s", rule_id, exc)
    return new_triples


def _save_results_to_firestore(
    db: firestore.Client, uid: str, g: Graph
) -> None:
    """추론 결과를 Firestore에 저장."""
    user_uri = URIRef(f"http://7team.dev/ontology#user_{uid}")

    # quests
    quests = []
    for quest in g.subjects(predicate=PROD.questType):
        title = g.value(quest, PROD.title)
        q_type = g.value(quest, PROD.questType)
        is_done = g.value(quest, PROD.isCompleted)
        created = g.value(quest, PROD.createdAt)
        quests.append({
            "title": str(title) if title else "",
            "questType": str(q_type) if q_type else "",
            "isCompleted": bool(is_done) if is_done is not None else False,
            "createdAt": str(created) if created else "",
        })
    if quests:
        db.collection("quests").document(uid).set({"quests": quests}, merge=True)

    # room_objects
    room_objs = []
    for obj in g.subjects(predicate=PROD.objectType):
        if (obj, PROD.inferredFrom, None) in g:
            room_objs.append({
                "objectType": str(g.value(obj, PROD.objectType)),
                "inferredFrom": str(g.value(obj, PROD.inferredFrom)),
                "positionX": float(g.value(obj, PROD.positionX) or 0),
                "positionY": float(g.value(obj, PROD.positionY) or 0),
                "positionZ": float(g.value(obj, PROD.positionZ) or 0),
            })
    if room_objs:
        db.collection("room_objects").document(uid).set(
            {"objects": room_objs}, merge=True
        )

    # persona
    persona_node = g.value(user_uri, PROD.hasPersona)
    if persona_node:
        persona = {
            "energyType":       str(g.value(persona_node, PROD.energyType) or ""),
            "socialPreference": str(g.value(persona_node, PROD.socialPreference) or ""),
            "lifePattern":      str(g.value(persona_node, PROD.lifePattern) or ""),
            "updatedAt":        str(g.value(persona_node, PROD.updatedAt) or ""),
        }
        db.collection("users").document(uid).set({"persona": persona}, merge=True)


def _save_graph_to_storage(g: Graph, bucket_name: str, uid: str) -> None:
    ttl_bytes = g.serialize(format="turtle").encode()
    bucket = storage.bucket(bucket_name)
    blob = bucket.blob(f"graphs/{uid}.ttl")
    blob.upload_from_file(io.BytesIO(ttl_bytes), content_type="text/turtle")
    logger.info("Graph saved: %d triples", len(g))


def _delete_temp_triples(db: firestore.Client, uid: str) -> None:
    items_ref = db.collection("temp_triples").document(uid).collection("items")
    for doc in items_ref.stream():
        doc.reference.delete()
    db.collection("temp_triples").document(uid).delete()


def run_inference(uid: str, bucket_name: str, rules_sparql: str) -> dict:
    """Cloud Functions 진입점에서 호출하는 메인 함수."""
    _init_firebase()
    db = firestore.client()

    g = _load_graph_from_storage(bucket_name, uid)
    temp_triples = _load_temp_triples(db, uid)

    if not temp_triples:
        logger.info("No new triples for uid=%s — skipping", uid)
        return {"status": "no_new_triples"}

    _aggregate_with_duckdb(temp_triples)
    _add_triples_to_graph(g, temp_triples)

    rules = _parse_rules(rules_sparql)
    _apply_rules(g, rules)

    _save_results_to_firestore(db, uid, g)
    _save_graph_to_storage(g, bucket_name, uid)
    _delete_temp_triples(db, uid)

    return {"status": "ok", "triples": len(g)}


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
