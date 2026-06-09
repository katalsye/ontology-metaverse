"""
main.py — Cloud Functions 진입점
"""
from firebase_admin import initialize_app
from firebase_functions.options import set_global_options

initialize_app()
set_global_options(max_instances=10)

# Firestore onWrite 트리거 (triggers.py는 경량 import — 직접 로드)
from triggers import on_new_triple_written  # noqa: F401, E402

# HTTP 트리거 — ontology_engine은 duckdb/rdflib 포함으로 import 시 ~7초 소요
# Firebase CLI 10초 타임아웃 우회를 위해 지연 로딩 래퍼 사용
def infer(request):  # type: ignore[no-untyped-def]
    """Cloud Functions HTTP 진입점 (지연 로딩 래퍼)."""
    from ontology_engine import infer as _infer
    return _infer(request)
