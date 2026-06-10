"""
cleanup_persona_quest.py — 일회성 Persona/Quest 잔재 정리 스크립트

용도:
  추론 엔진 버그(Persona 누적 + 빈 title Quest 잔류) 수정 배포 이전에
  이미 Storage에 쌓인 잔재를 수동으로 정리한다.
  버그 수정 배포 후에는 엔진이 매 사이클 시작 시 자동으로 정리하므로
  이 스크립트는 1회만 실행하면 된다.

실행 방법:
  cd C:/Users/user/Desktop/ontology-metaverse/Functions
  STORAGE_BUCKET=ontology-metaverse.firebasestorage.app python scripts/cleanup_persona_quest.py
  # 또는 특정 uid 지정:
  STORAGE_BUCKET=ontology-metaverse.firebasestorage.app python scripts/cleanup_persona_quest.py <uid>

주의:
  - 실행 전 Storage에서 graphs/{uid}/latest.ttl을 수동 백업 권장
  - serviceAccountKey.json이 Functions/ 디렉터리에 있어야 함
  - 배포 파일(requirements.txt, main.py)에 포함되지 않으므로 Cloud Functions에 업로드되지 않음
"""

import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import firebase_admin
from firebase_admin import credentials, storage
from rdflib import Graph, Namespace, RDF

PROD = Namespace("http://7team.dev/ontology#")

DEFAULT_UID = "aQxeWJOmJOTwNcxzmIQSAt5Yy8m2"


def cleanup(uid: str, bucket_name: str) -> None:
    key_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                            "serviceAccountKey.json")
    if not firebase_admin._apps:
        cred = credentials.Certificate(key_path)
        firebase_admin.initialize_app(cred)

    bucket = storage.bucket(bucket_name)
    blob = bucket.blob(f"graphs/{uid}/latest.ttl")
    if not blob.exists():
        print(f"[ERROR] graphs/{uid}/latest.ttl not found — uid가 올바른지 확인")
        sys.exit(1)

    with tempfile.NamedTemporaryFile(suffix=".ttl", delete=False) as tmp_in:
        tmp_in_path = tmp_in.name
    with tempfile.NamedTemporaryFile(suffix=".ttl", delete=False) as tmp_out:
        tmp_out_path = tmp_out.name

    try:
        blob.download_to_filename(tmp_in_path)
        g = Graph()
        g.parse(tmp_in_path, format="turtle")
        print(f"정리 전 트리플 수: {len(g)}")

        # ── Persona 노드 정리 ───────────────────────────────────────────────
        persona_nodes = list(g.subjects(RDF.type, PROD.Persona))
        print(f"Persona 노드 수: {len(persona_nodes)}")
        for p in persona_nodes:
            for pred, obj in list(g.predicate_objects(p)):
                g.remove((p, pred, obj))
            for subj, pred in list(g.subject_predicates(p)):
                g.remove((subj, pred, p))

        # ── 빈 title Quest 정리 ─────────────────────────────────────────────
        quest_nodes = list(g.subjects(RDF.type, PROD.Quest))
        removed_quests = 0
        for q in quest_nodes:
            if g.value(q, PROD.title) is None:
                for pred, obj in list(g.predicate_objects(q)):
                    g.remove((q, pred, obj))
                for subj, pred in list(g.subject_predicates(q)):
                    g.remove((subj, pred, q))
                removed_quests += 1
        print(f"빈 title Quest 제거: {removed_quests}건")

        print(f"정리 후 트리플 수: {len(g)}")

        # ── 업로드 ──────────────────────────────────────────────────────────
        g.serialize(destination=tmp_out_path, format="turtle")
        blob.upload_from_filename(tmp_out_path, content_type="text/turtle")
        print(f"정리 완료 → graphs/{uid}/latest.ttl 업로드됨")

    finally:
        for path in (tmp_in_path, tmp_out_path):
            try:
                os.unlink(path)
            except OSError:
                pass


if __name__ == "__main__":
    uid = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_UID
    bucket_name = os.environ.get("STORAGE_BUCKET", "ontology-metaverse.firebasestorage.app")
    print(f"uid={uid}, bucket={bucket_name}")
    cleanup(uid, bucket_name)
