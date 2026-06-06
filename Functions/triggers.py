"""
triggers.py
Firestore onWrite 트리거 — temp_triples/{uid}/items/{itemId}

활성화 시점: 김준석님 1차 검증 완료 후
배포 보류 중: firebase deploy 미실행 상태

debounce: 동일 uid에 대해 DEBOUNCE_SECONDS(30초) 이내 추론이 이미 실행된 경우
          후속 트리거를 건너뜀으로써 다수 트리플 동시 업로드 시 중복 추론을 방지.
          Firestore 트랜잭션으로 lastInferenceAt 기록 — 전역 상태 없음(Stateless 준수).
          진정한 "지연 실행" 방식(Cloud Tasks)과의 비교는
          Docs/contracts/debounce_design.md 참조.
"""

from __future__ import annotations

import logging
import os
import traceback
from datetime import datetime, timezone
from typing import Any

logger = logging.getLogger(__name__)

_THIS_DIR = os.path.dirname(os.path.abspath(__file__))
RULES_PATH = os.path.join(_THIS_DIR, "ontology", "rules", "inference_rules.sparql")
DEBOUNCE_SECONDS: int = 30


def _load_rules() -> str:
    """SPARQL 추론 규칙 파일 로드."""
    with open(RULES_PATH, encoding="utf-8") as f:
        return f.read()


def _ensure_firebase_initialized() -> None:
    import firebase_admin
    if not firebase_admin._apps:
        firebase_admin.initialize_app()


def _try_acquire_run_slot(uid: str) -> bool:
    """
    Firestore 트랜잭션으로 추론 실행 권한 획득 시도.
    temp_triples/{uid}.lastInferenceAt 을 읽어 DEBOUNCE_SECONDS 이내면 False.
    획득 성공 시 lastInferenceAt 을 현재 시각으로 갱신하고 True 반환.
    """
    from firebase_admin import firestore

    _ensure_firebase_initialized()
    db = firestore.client()
    meta_ref = db.document(f"temp_triples/{uid}")

    @firestore.transactional
    def _txn(transaction: Any) -> bool:
        snapshot = meta_ref.get(transaction=transaction)
        data = snapshot.to_dict() or {}
        last_run = data.get("lastInferenceAt")
        now = datetime.now(timezone.utc)

        if last_run is not None:
            elapsed = (now - last_run).total_seconds()
            if elapsed < DEBOUNCE_SECONDS:
                return False

        transaction.set(meta_ref, {"lastInferenceAt": now}, merge=True)
        return True

    return _txn(firestore.transaction(db))


def _handle_triple_written(event: Any) -> None:
    """
    Firestore onWrite 트리거 핵심 로직.
    1. STORAGE_BUCKET 환경변수 확인
    2. uid 추출
    3. Debounce 슬롯 확인 (DEBOUNCE_SECONDS 내 이미 실행됐으면 skip)
    4. run_inference() 호출
    테스트에서 직접 호출 가능 (Stateless — 전역 상태 없음).
    """
    from ontology_engine import run_inference  # 지연 임포트 (Stateless 원칙)

    bucket_name = os.environ.get("STORAGE_BUCKET", "")
    if not bucket_name:
        logger.warning("STORAGE_BUCKET 환경변수 미설정 — 추론 건너뜀")
        return

    params = getattr(event, "params", {}) or {}
    uid = params.get("uid", "")
    if not uid:
        logger.warning("uid 추출 실패 (event.params=%s) — 추론 건너뜀", params)
        return

    if not _try_acquire_run_slot(uid):
        logger.info("Debounce skip: uid=%s — %ds 내 추론 이미 실행됨", uid, DEBOUNCE_SECONDS)
        return

    logger.info("Firestore onWrite 트리거: uid=%s", uid)
    try:
        rules_sparql = _load_rules()
        result = run_inference(uid, bucket_name, rules_sparql)
        logger.info("추론 완료: uid=%s result=%s", uid, result)
    except Exception:
        logger.error("추론 오류: uid=%s\n%s", uid, traceback.format_exc())


# Firebase Functions 데코레이터 등록 (배포 환경에서만 실제 동작)
try:
    from firebase_functions import firestore_fn  # type: ignore[import]

    @firestore_fn.on_document_written(document="temp_triples/{uid}/items/{itemId}")
    def on_new_triple_written(event: Any) -> None:  # type: ignore[misc]
        _handle_triple_written(event)

except ImportError:
    on_new_triple_written = _handle_triple_written  # type: ignore[assignment]
