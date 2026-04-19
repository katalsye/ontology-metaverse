"""
triple_validator.py
Gemma 3n 생성 트리플 dict 목록의 정규화·스키마 검증·타입 변환 모듈.
ontology_engine._add_triples_to_graph() 호출 전에 자동 실행됨.

처리 순서:
  1. predicate 정규화 (약식 → full URI, core.ttl 기반)
  2. 스키마 검증 (core.ttl 미정의 predicate 제외)
  3. 타입 자동 변환 (range 정보 또는 값 패턴 기반)
  4. 범위 제약 검사 (duration·quality·count·temperature·humidity)
  5. User 연결 감지 (고립 노드 경고)
"""
from __future__ import annotations

import logging
from pathlib import Path

from rdflib import Graph, Namespace, RDF, OWL, RDFS
from rdflib.namespace import XSD

logger = logging.getLogger(__name__)

PROD = Namespace("http://7team.dev/ontology#")
TTL_PATH = Path("Functions/ontology/core.ttl")

RDF_TYPE_URI = str(RDF.type)

# 범위 제약: 속성 로컬명 → (min, max, python_type)   max=None은 상한 없음
RANGE_BOUNDS: dict[str, tuple] = {
    "duration":    (0.0,   24.0,  float),
    "quality":     (0,     100,   int),
    "count":       (0,     None,  int),
    "temperature": (-50.0, 60.0,  float),
    "humidity":    (0.0,   100.0, float),
}

# rdf:type는 core.ttl Property 목록에 없지만 항상 통과
_PASSTHROUGH_URIS: frozenset[str] = frozenset({RDF_TYPE_URI})


class TripleValidator:
    """온톨로지 스키마 기반 트리플 정규화·검증기."""

    def __init__(self, ttl_path: Path = TTL_PATH) -> None:
        g = Graph()
        g.parse(str(ttl_path), format="turtle")
        self._prop_map   = self._build_prop_map(g)       # local_name → full_uri
        self._known_uris = set(self._prop_map.values())  # O(1) 조회용
        self._range_map  = self._build_range_map(g)      # full_prop_uri → xsd_type_uri
        logger.debug("TripleValidator: %d properties loaded from %s",
                     len(self._prop_map), ttl_path)

    # ── 초기화 헬퍼 ──────────────────────────────────────────────────────────

    @staticmethod
    def _build_prop_map(g: Graph) -> dict[str, str]:
        props: dict[str, str] = {}
        for rdf_type in (OWL.DatatypeProperty, OWL.ObjectProperty):
            for s in g.subjects(RDF.type, rdf_type):
                local = str(s).split("#")[-1]
                props[local] = str(s)
        return props

    @staticmethod
    def _build_range_map(g: Graph) -> dict[str, str]:
        return {str(s): str(o) for s, _, o in g.triples((None, RDFS.range, None))}

    # ── predicate 정규화 ──────────────────────────────────────────────────────

    def _normalize_pred(self, pred: str) -> str | None:
        """약식 predicate → 전체 URI. 알 수 없으면 None 반환."""
        if not pred:
            return None
        if pred.startswith("http"):
            return pred if (pred in self._known_uris or pred in _PASSTHROUGH_URIS) else None
        if pred in ("a", "rdf:type"):
            return RDF_TYPE_URI
        if pred in self._prop_map:
            return self._prop_map[pred]
        # "prod:xxx" 형태의 접두사 포함 표현
        local = pred.split(":")[-1]
        return self._prop_map.get(local)

    # ── 타입 변환 ────────────────────────────────────────────────────────────

    def _coerce(self, prop_uri: str, value: object, datatype: str | None
                ) -> tuple[str, str | None]:
        """값·datatype 정규화. (coerced_str, datatype_uri) 반환."""
        val_str = str(value)

        if datatype:
            return val_str, datatype

        range_uri = self._range_map.get(prop_uri, "")

        if range_uri == str(XSD.float):
            try:
                return str(float(val_str)), str(XSD.float)
            except (ValueError, TypeError):
                pass
        elif range_uri == str(XSD.integer):
            try:
                return str(int(val_str)), str(XSD.integer)
            except (ValueError, TypeError):
                pass
        elif range_uri == str(XSD.boolean):
            return _coerce_bool(val_str)
        elif range_uri in (str(XSD.date), str(XSD.dateTime), str(XSD.string)):
            return val_str, range_uri

        # range 정보 없는 경우 — 값 패턴으로 자동 추론
        try:
            int_val = int(val_str)
            if str(int_val) == val_str:   # "5.0" 같은 부동소수 문자열 제외
                return val_str, str(XSD.integer)
        except (ValueError, TypeError):
            pass
        try:
            float(val_str)
            return val_str, str(XSD.float)
        except (ValueError, TypeError):
            pass
        if val_str.lower() in ("true", "false"):
            return val_str.lower(), str(XSD.boolean)

        return val_str, None  # plain string Literal

    # ── 범위 검사 ────────────────────────────────────────────────────────────

    @staticmethod
    def _in_bounds(prop_local: str, value: str) -> bool:
        """범위 제약 위반 시 False 반환."""
        bounds = RANGE_BOUNDS.get(prop_local)
        if not bounds:
            return True
        lo, hi, typ = bounds
        try:
            v = typ(value)
        except (ValueError, TypeError):
            return True
        if lo is not None and v < lo:
            return False
        if hi is not None and v > hi:
            return False
        return True

    # ── User 연결 감지 ────────────────────────────────────────────────────────

    @staticmethod
    def _find_orphans(triples: list[dict]) -> list[str]:
        """User와 연결되지 않은 고립 subject URI 목록 반환."""
        user_nodes: set[str] = set()
        all_obj_uris: set[str] = set()

        for t in triples:
            p   = t.get("predicate", "")
            obj = str(t.get("object", ""))

            # User 노드 식별: prod:uid 보유 또는 rdf:type prod:User
            if p.endswith("#uid"):
                user_nodes.add(t["subject"])
            if p == RDF_TYPE_URI and (
                obj.endswith("#User") or obj in ("prod:User", "User")
            ):
                user_nodes.add(t["subject"])

            # Object URI 수집 (타입 지정 Literal 제외)
            if obj.startswith("http") and not t.get("datatype"):
                all_obj_uris.add(obj)

        all_subjects = {t["subject"] for t in triples if t.get("subject")}
        return sorted(all_subjects - user_nodes - all_obj_uris)

    # ── 공개 인터페이스 ───────────────────────────────────────────────────────

    def validate(self, triples: list[dict]) -> tuple[list[dict], list[str]]:
        """트리플 목록을 정규화·검증한다.

        Returns:
            (valid_triples, warnings)
            valid_triples: 검증 통과한 정규화 트리플 목록
            warnings:      경고 메시지 목록 (로그에도 출력됨)
        """
        valid: list[dict] = []
        warnings: list[str] = []

        for t in triples:
            subj     = t.get("subject", "")
            pred_raw = str(t.get("predicate", ""))
            obj_val  = t.get("object", "")
            datatype = t.get("datatype")

            # ① predicate 정규화
            pred_uri = self._normalize_pred(pred_raw)
            if pred_uri is None:
                msg = f"알 수 없는 predicate '{pred_raw}' — 트리플 제외 (subject: {subj})"
                warnings.append(msg)
                logger.warning("TripleValidator: %s", msg)
                continue

            prop_local = pred_uri.split("#")[-1]

            # ② 타입 변환
            coerced_val, coerced_dt = self._coerce(pred_uri, obj_val, datatype)

            # ③ 범위 검사
            if not self._in_bounds(prop_local, coerced_val):
                lo, hi, _ = RANGE_BOUNDS[prop_local]
                msg = (
                    f"범위 위반: {prop_local}={coerced_val} "
                    f"(허용: {lo}~{hi}) — 트리플 제외 (subject: {subj})"
                )
                warnings.append(msg)
                logger.warning("TripleValidator: %s", msg)
                continue

            clean: dict = {
                "subject":   subj,
                "predicate": pred_uri,
                "object":    coerced_val,
            }
            if coerced_dt:
                clean["datatype"] = coerced_dt
            valid.append(clean)

        # ④ User 연결 감지 (경고만, 트리플 제외 안 함)
        for node in self._find_orphans(valid):
            msg = f"User 연결 없는 고립 노드: {node}"
            warnings.append(msg)
            logger.warning("TripleValidator: %s", msg)

        return valid, warnings


# ── 모듈 수준 헬퍼 ────────────────────────────────────────────────────────────

def _coerce_bool(val_str: str) -> tuple[str, str]:
    v = val_str.lower()
    if v in ("true", "1", "yes"):
        return "true", str(XSD.boolean)
    if v in ("false", "0", "no"):
        return "false", str(XSD.boolean)
    return val_str, str(XSD.boolean)
