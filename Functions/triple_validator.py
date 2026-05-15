"""
triple_validator.py
Gemma 3n 생성 트리플 dict 목록의 정규화·스키마 검증·타입 변환 모듈.
ontology_engine._add_triples_to_graph() 호출 전에 자동 실행됨.

처리 순서:
  1. predicate 정규화 (약식 → full URI, core.ttl 기반)
  2. 스키마 검증 (core.ttl 미정의 predicate 제외)
  3. 타입 자동 변환 (range 정보 또는 값 패턴 기반)
  4. 범위 제약 검사 (duration·quality·count·deepSleepRatio·
                      usageDuration·visitCount·amount·temperature·
                      humidity·latitude·longitude)
  5. 시간대 검증 (timestamp·visitTime·date·createdAt 형식 검증)
  6. User 연결 감지 (고립 노드 경고)
"""
from __future__ import annotations

import logging
import re
from datetime import datetime, timedelta
from pathlib import Path

from rdflib import Graph, Namespace, RDF, OWL, RDFS
from rdflib.namespace import XSD

logger = logging.getLogger(__name__)

PROD = Namespace("http://7team.dev/ontology#")
TTL_PATH = Path("Functions/ontology/core.ttl")

RDF_TYPE_URI = str(RDF.type)

# 범위 제약: 속성 로컬명 → (min, max, python_type)   max=None은 상한 없음
RANGE_BOUNDS: dict[str, tuple] = {
    "duration":       (0.0,    24.0,        float),
    "quality":        (0,      100,         int),
    "count":          (0,      100_000,     int),    # StepCount.count 상한 10만
    "deepSleepRatio": (0.0,    1.0,         float),  # SleepData.deepSleepRatio
    "usageDuration":  (0,      1440,        int),    # AppUsage.usageDuration (분)
    "listenDuration": (1,      1440,        int),    # MusicListening.listenDuration (분, 1분 이상)
    "visitCount":     (1,      10_000,      int),    # Location.visitCount 최솟값 1
    "amount":         (0,      1_000_000,   int),    # Reward.amount
    "temperature":    (-50.0,  60.0,        float),
    "humidity":       (0.0,    100.0,       float),
    "latitude":       (-90.0,  90.0,        float),  # 위도: 남극(-90) ~ 북극(+90)
    "longitude":      (-180.0, 180.0,       float),  # 경도: -180 ~ +180
}

# 숫자 범위 검증 대상 속성 (min, max) — RANGE_BOUNDS에서 자동 파생
# DATETIME_PROPS 근처에 위치시켜 범위 상수 일람 가능하도록 배치
# temperature / humidity / latitude / longitude 포함: 문자열 입력 시 경고 발생
NUMERIC_RANGES: dict[str, tuple[float, float]] = {
    k: (v[0], v[1])
    for k, v in RANGE_BOUNDS.items()
}

# 시간대 검증 대상 속성 (ISO 8601 dateTime 형식 필요)
DATETIME_PROPS: frozenset[str] = frozenset({
    "timestamp",    # 공통 시각
    "visitTime",    # Location 방문 시각
    "createdAt",    # Quest 생성 시각
    "playedAt",     # MusicListening 재생 시각
    "recordedAt",   # Weather 기록 시각
    "startTime",    # CalendarEvent 시작 시각
    "endTime",      # CalendarEvent 종료 시각
    "updatedAt",    # Persona 갱신 시각
})

# 날짜 검증 대상 속성 (YYYY-MM-DD 형식 필요)
DATE_PROPS: frozenset[str] = frozenset({"date"})

# ISO 8601 dateTime 정규식 (기본 형식 + Z/+09:00 타임존 지원)
ISO_DATETIME_PATTERN = re.compile(
    r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?$"
)

# YYYY-MM-DD 날짜 정규식
DATE_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}$")

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
        """범위 제약 위반 시 False 반환. 숫자 변환 불가 시 True (상위 로직에서 별도 처리)."""
        bounds = RANGE_BOUNDS.get(prop_local)
        if not bounds:
            return True
        lo, hi, typ = bounds
        try:
            v = typ(value)
        except (ValueError, TypeError):
            return True  # 숫자 변환 불가 — validate()에서 NUMERIC_RANGES 로직이 처리
        if lo is not None and v < lo:
            return False
        if hi is not None and v > hi:
            return False
        return True

    @staticmethod
    def _check_numeric_range(prop_local: str, value: str) -> str | None:
        """NUMERIC_RANGES 대상 속성의 숫자 변환 실패 또는 범위 위반 감지.

        반환:
            None  — 통과 (범위 내 또는 검사 대상 아님)
            str   — 경고 메시지 (트리플 제외 필요)
        """
        if prop_local not in NUMERIC_RANGES:
            return None
        lo, hi = NUMERIC_RANGES[prop_local]
        _, _, typ = RANGE_BOUNDS[prop_local]
        try:
            v = typ(value)
        except (ValueError, TypeError):
            return (
                f"숫자 변환 실패: {prop_local}='{value}' "
                f"(숫자 형식 필요, 허용: {lo}~{hi})"
            )
        if lo is not None and v < lo:
            return (
                f"범위 위반: {prop_local}={value} "
                f"(허용: {lo}~{hi})"
            )
        if hi is not None and v > hi:
            return (
                f"범위 위반: {prop_local}={value} "
                f"(허용: {lo}~{hi})"
            )
        return None

    # ── 시간대 검증 ───────────────────────────────────────────────────────────

    @staticmethod
    def _validate_datetime_format(value: str) -> bool:
        """ISO 8601 dateTime 형식 검증. 유효하면 True."""
        if not value or not isinstance(value, str):
            return False
        # 정규식 매칭
        if not ISO_DATETIME_PATTERN.match(value):
            return False
        # 파싱 가능 여부 확인 (유효한 날짜인지)
        try:
            # Z 타임존 처리
            clean = value.replace("Z", "+00:00")

            # 밀리초 처리 (타임존 혼합 대응)
            if "." in clean:
                # 타임존이 있는 경우: 2026-05-13T14:30:00.123+09:00
                if "+" in clean.split(".")[-1] or "-" in clean.split(".")[-1]:
                    dt_part, ms_tz_part = clean.split(".", 1)
                    # 밀리초와 타임존 분리
                    if "+" in ms_tz_part:
                        tz_part = "+" + ms_tz_part.split("+")[1]
                    else:
                        # 마지막 - 찾기 (날짜의 -가 아닌 타임존의 -)
                        tz_idx = ms_tz_part.rfind("-")
                        if tz_idx > 0:
                            tz_part = ms_tz_part[tz_idx:]
                        else:
                            tz_part = ""
                    clean = dt_part + (tz_part if tz_part else "")
                else:
                    # 타임존 없는 밀리초: 2026-05-13T14:30:00.123
                    clean = clean.split(".")[0]

            # 타임존 제거 후 기본 파싱
            if "+" in clean or clean.count("-") > 2:
                dt_part = clean[:19]  # YYYY-MM-DDTHH:MM:SS
                datetime.strptime(dt_part, "%Y-%m-%dT%H:%M:%S")
            else:
                datetime.strptime(clean, "%Y-%m-%dT%H:%M:%S")
            return True
        except (ValueError, AttributeError, IndexError):
            return False

    @staticmethod
    def _validate_date_format(value: str) -> bool:
        """YYYY-MM-DD 날짜 형식 검증. 유효하면 True."""
        if not value or not isinstance(value, str):
            return False
        if not DATE_PATTERN.match(value):
            return False
        try:
            datetime.strptime(value, "%Y-%m-%d")
            return True
        except ValueError:
            return False

    @staticmethod
    def _is_future_timestamp(value: str) -> bool:
        """timestamp가 현재 + 1일 이후면 True (미래 데이터 경고용)."""
        try:
            # 타임존 및 밀리초 제거 후 파싱 (나이브 datetime으로 통일)
            clean = value.replace("Z", "")

            # + 타임존 제거
            if "+" in clean:
                clean = clean.split("+")[0]
            # - 타임존 제거 (날짜 구분자가 아닌 마지막 -만)
            elif clean.count("-") > 2:
                parts = clean.rsplit("-", 1)
                if ":" in parts[-1]:  # 타임존 형식 확인 (예: -09:00)
                    clean = parts[0]

            # 밀리초 제거
            if "." in clean:
                clean = clean.split(".")[0]

            # 나이브 datetime으로 파싱하여 비교
            dt = datetime.fromisoformat(clean)
            threshold = datetime.now() + timedelta(days=1)
            return dt > threshold
        except (ValueError, AttributeError, TypeError):
            return False

    @staticmethod
    def _validate_sleep_duration(value: str) -> bool:
        """수면 시간이 0.5~18.0 범위인지 확인."""
        try:
            duration = float(value)
            return 0.5 <= duration <= 18.0
        except (ValueError, TypeError):
            return False

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

            # ③ 범위 검사 (RANGE_BOUNDS 전체 — 경계 위반 감지)
            if not self._in_bounds(prop_local, coerced_val):
                lo, hi, _ = RANGE_BOUNDS[prop_local]
                msg = (
                    f"범위 위반: {prop_local}={coerced_val} "
                    f"(허용: {lo}~{hi}) — 트리플 제외 (subject: {subj})"
                )
                warnings.append(msg)
                logger.warning("TripleValidator: %s", msg)
                continue

            # ③-a NUMERIC_RANGES 숫자 변환 실패 감지
            #     (_in_bounds는 변환 불가 시 True 반환하므로 여기서 별도 처리)
            numeric_warn = self._check_numeric_range(prop_local, coerced_val)
            if numeric_warn is not None:
                msg = f"{numeric_warn} — 트리플 제외 (subject: {subj})"
                warnings.append(msg)
                logger.warning("TripleValidator: %s", msg)
                continue

            # ④ 시간대 검증 (timestamp, visitTime, createdAt)
            if prop_local in DATETIME_PROPS:
                if not self._validate_datetime_format(str(coerced_val)):
                    msg = (
                        f"시간 형식 오류: {prop_local}='{coerced_val}' "
                        f"(ISO 8601 필요, 예: 2026-05-13T14:30:00) — 트리플 제외 (subject: {subj})"
                    )
                    warnings.append(msg)
                    logger.warning("TripleValidator: %s", msg)
                    continue
                # 미래 timestamp 경고 (제외는 안 함)
                if self._is_future_timestamp(str(coerced_val)):
                    msg = f"미래 시각 경고: {prop_local}='{coerced_val}' (현재+1일 초과)"
                    warnings.append(msg)
                    logger.warning("TripleValidator: %s", msg)

            # ⑤ 날짜 검증 (date)
            if prop_local in DATE_PROPS:
                if not self._validate_date_format(str(coerced_val)):
                    msg = (
                        f"날짜 형식 오류: {prop_local}='{coerced_val}' "
                        f"(YYYY-MM-DD 필요) — 트리플 제외 (subject: {subj})"
                    )
                    warnings.append(msg)
                    logger.warning("TripleValidator: %s", msg)
                    continue

            # ⑥ 수면 시간 특별 검증 (0.5~18.0 시간)
            if prop_local == "duration":
                # duration은 이미 범위 검사(0.0~24.0)를 통과했지만,
                # 수면 데이터의 경우 더 엄격한 검증 (0.5~18.0)
                if not self._validate_sleep_duration(coerced_val):
                    msg = (
                        f"수면 시간 비정상: duration={coerced_val} "
                        f"(현실적 범위: 0.5~18.0시간) — 트리플 제외 (subject: {subj})"
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

        # ⑦ User 연결 감지 (경고만, 트리플 제외 안 함)
        for node in self._find_orphans(valid):
            msg = f"User 연결 없는 고립 노드: {node}"
            warnings.append(msg)
            logger.warning("TripleValidator: %s", msg)

        return valid, warnings


# ── 필수 속성 매핑 (core.ttl owl:minCardinality >= 1 기준) ────────────────────
# WARNING 생성용 — 데이터 보완형 퀘스트 생성 신호로 사용됨

REQUIRED_PROPERTY_MAP: dict[str, list[str]] = {
    "User":      ["uid"],
    "Quest":     ["title"],
    "SleepData": ["duration"],
    "StepCount": ["count"],
    "AppUsage":  ["appName", "usageDuration"],
}


def validate_required_properties(graph) -> list[str]:
    """
    그래프 내 각 클래스 인스턴스가 필수 속성을 가지고 있는지 검증.
    누락 시 WARNING 문자열 반환 (ERROR 아님 — 데이터 보완형 퀘스트 생성 유도).

    Args:
        graph: rdflib.Graph 인스턴스 (온톨로지 그래프)

    Returns:
        list[str]: 누락 속성에 대한 경고 메시지 목록.
                   모든 필수 속성이 존재하면 빈 리스트 반환.
    """
    from rdflib import Graph as _Graph, RDF as _RDF, URIRef as _URIRef

    warnings: list[str] = []

    for class_name, required_props in REQUIRED_PROPERTY_MAP.items():
        class_uri = _URIRef(str(PROD) + class_name)
        # 해당 클래스의 모든 인스턴스 순회
        for instance in graph.subjects(_RDF.type, class_uri):
            instance_uri = str(instance)
            for prop in required_props:
                prop_uri = _URIRef(str(PROD) + prop)
                # 인스턴스가 해당 속성을 하나라도 가지고 있는지 확인
                has_prop = any(graph.objects(instance, prop_uri))
                if not has_prop:
                    msg = f"[WARNING] {class_name} {instance_uri}: {prop} 누락"
                    warnings.append(msg)
                    logger.warning("validate_required_properties: %s", msg)

    return warnings


# ── 모듈 수준 헬퍼 ────────────────────────────────────────────────────────────

def _coerce_bool(val_str: str) -> tuple[str, str]:
    v = val_str.lower()
    if v in ("true", "1", "yes"):
        return "true", str(XSD.boolean)
    if v in ("false", "0", "no"):
        return "false", str(XSD.boolean)
    return val_str, str(XSD.boolean)
