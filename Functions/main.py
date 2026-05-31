"""
main.py — Cloud Functions 진입점
"""
from firebase_admin import initialize_app
from firebase_functions.options import set_global_options

initialize_app()
set_global_options(max_instances=10)

# HTTP 트리거 (수동 추론 호출)
from ontology_engine import infer  # noqa: F401, E402

# Firestore onWrite 트리거 (자동 추론)
from triggers import on_new_triple_written  # noqa: F401, E402
