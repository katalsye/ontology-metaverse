"""
check_inference.py
김준석-김무성 1차 통합 테스트용 Firestore 상태 확인 + HTTP infer 호출 도구.

사용법:
  python scripts/check_inference.py <uid>                       # temp_triples 상태만
  python scripts/check_inference.py <uid> <endpoint_url>        # 전체 흐름 실행
  python scripts/check_inference.py <uid> --results-only        # 결과만 조회
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from typing import Any

# Functions/ 디렉토리를 sys.path에 추가 (triple_validator 임포트용)
_FUNCTIONS_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _FUNCTIONS_DIR not in sys.path:
    sys.path.insert(0, _FUNCTIONS_DIR)

# ANSI 색상
_GREEN  = "\033[32m"
_YELLOW = "\033[33m"
_RED    = "\033[31m"
_BOLD   = "\033[1m"
_RESET  = "\033[0m"


def _ok(msg: str) -> str:
    return f"{_GREEN}✓ {msg}{_RESET}"


def _warn(msg: str) -> str:
    return f"{_YELLOW}⚠ {msg}{_RESET}"


def _err(msg: str) -> str:
    return f"{_RED}✗ {msg}{_RESET}"


def _read_triple_fields(t: dict) -> tuple[str, str, str, str]:
    """subject/predicate/object 또는 s/p/o 약식 필드명 양쪽 지원.

    Returns: (subj, pred, obj, fmt_hint)
      fmt_hint — 약식 필드명 사용 시 "(필드: s/p/o)" 형태 문자열, 정식 필드면 빈 문자열.
    """
    if t.get("subject") is not None:
        subj_key, subj = "subject", str(t["subject"])
    else:
        subj_key, subj = "s", str(t.get("s", ""))

    if t.get("predicate") is not None:
        pred_key, pred = "predicate", str(t["predicate"])
    else:
        pred_key, pred = "p", str(t.get("p", ""))

    if t.get("object") is not None:
        obj_key, obj = "object", str(t["object"])
    elif t.get("o") is not None:
        obj_key, obj = "o", str(t["o"])
    elif t.get("object_uri") is not None:
        obj_key, obj = "object_uri", str(t["object_uri"])
    else:
        obj_key, obj = "object", ""

    short = {subj_key, pred_key, obj_key} & {"s", "p", "o"}
    fmt_hint = f"  (필드: {subj_key}/{pred_key}/{obj_key})" if short else ""
    return subj, pred, obj, fmt_hint


def _header(title: str) -> None:
    print(f"\n{_BOLD}{'=' * 52}{_RESET}")
    print(f"{_BOLD}  {title}{_RESET}")
    print(f"{_BOLD}{'=' * 52}{_RESET}")


def _init_firebase() -> Any:
    """firebase_admin 초기화 (ADC 사용). 실패 시 명확한 에러 출력 후 종료."""
    try:
        import firebase_admin
        from firebase_admin import firestore
        if not firebase_admin._apps:
            firebase_admin.initialize_app()
        return firestore.client()
    except Exception as exc:
        print(_err(f"Firebase 초기화 실패: {exc}"))
        print(_warn("GOOGLE_APPLICATION_CREDENTIALS 환경변수 또는 firebase login 상태 확인"))
        sys.exit(1)


def _load_prop_map() -> dict[str, str] | None:
    """core.ttl에서 알려진 predicate 목록 로드. 실패 시 None 반환."""
    try:
        from triple_validator import TripleValidator
        v = TripleValidator()
        return v._prop_map  # {local_name: full_uri}
    except Exception as exc:
        print(_warn(f"predicate 검증 불가 (TripleValidator 로드 실패: {exc})"))
        return None


def check_temp_triples(uid: str, db: Any = None) -> list[dict]:
    """
    temp_triples/{uid}/items 컬렉션 조회.
    처음 5개 미리보기, subject URI 형식 + predicate 유효성 검사 출력.
    Returns: 조회된 트리플 목록 전체.
    """
    _header("temp_triples 상태")
    if db is None:
        db = _init_firebase()

    docs = list(
        db.collection("temp_triples")
        .document(uid)
        .collection("items")
        .stream()
    )
    triples = [d.to_dict() for d in docs]
    count = len(triples)

    if count == 0:
        print(_warn(f"uid={uid} — temp_triples 비어 있음 (업로드 전이거나 이미 처리됨)"))
        return triples

    print(_ok(f"uid={uid} — temp_triples 트리플 수: {count}"))
    print()

    # 처음 5개 미리보기
    preview_n = min(5, count)
    print(f"  [처음 {preview_n}개 미리보기]")
    for i, t in enumerate(triples[:preview_n]):
        subj, pred, obj, fmt_hint = _read_triple_fields(t)
        print(f"  {i + 1}. {subj!r}  —[{pred}]→  {obj!r}{fmt_hint}")

    print()

    # predicate + subject URI 유효성 검사
    prop_map = _load_prop_map()
    if prop_map is not None:
        invalid_subjects: set[str] = set()
        unknown_preds: set[str] = set()

        for t in triples:
            subj, pred, _, _ = _read_triple_fields(t)

            if subj and not subj.startswith("http"):
                invalid_subjects.add(subj)

            if pred:
                local = pred.split(":")[-1].split("#")[-1]
                is_known = (
                    pred in prop_map.values()
                    or pred in prop_map
                    or local in prop_map
                    or pred in ("a", "rdf:type")
                    or pred.startswith("http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
                )
                if not is_known:
                    unknown_preds.add(pred)

        if invalid_subjects:
            for s in sorted(invalid_subjects):
                print(_warn(f"  subject가 절대 URI 아님: {s!r}"))
        else:
            print(_ok("  모든 subject가 절대 URI 형식"))

        if unknown_preds:
            for p in sorted(unknown_preds):
                print(_warn(f"  미정의 predicate (추론 시 제외됨): {p!r}"))
        else:
            print(_ok("  모든 predicate가 core.ttl에 정의됨"))

    return triples


def trigger_inference(uid: str, endpoint: str) -> dict:
    """
    HTTP infer 함수 수동 호출.
    Returns: 응답 JSON dict (에러 시 {"error": ...}).
    """
    _header("infer 호출")
    payload = json.dumps({"uid": uid}).encode("utf-8")
    req = urllib.request.Request(
        endpoint,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = resp.read().decode("utf-8")
            result = json.loads(body)
        print(_ok(f"infer 완료 (HTTP {resp.status})"))
        print(f"  status     : {result.get('status')}")
        print(f"  triples    : {result.get('triples')}")
        new_quests = result.get("new_quests", [])
        print(f"  new_quests : {len(new_quests)}건")
        for q in new_quests:
            print(f"    - {q}")
        for w in result.get("missing_property_warnings", []):
            print(_warn(f"  {w}"))
        return result
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        print(_err(f"HTTP {exc.code}: {body[:300]}"))
        return {"error": str(exc)}
    except Exception as exc:
        print(_err(f"infer 호출 실패: {exc}"))
        return {"error": str(exc)}


def check_results(uid: str, db: Any = None) -> None:
    """quests, room_objects, persona 결과 조회."""
    if db is None:
        db = _init_firebase()

    # quests
    _header("quests")
    quests_doc = db.collection("quests").document(uid).get()
    if quests_doc.exists:
        quests = quests_doc.to_dict().get("quests", [])
        print(_ok(f"퀘스트 수: {len(quests)}"))
        for q in quests:
            status = "완료" if q.get("isCompleted") else "진행중"
            print(f"  [{status}] {q.get('title', '(제목 없음)')}  ({q.get('questType', '')})")
    else:
        print(_warn(f"uid={uid} — quests 문서 없음"))

    # room_objects
    _header("room_objects")
    ro_doc = db.collection("room_objects").document(uid).get()
    if ro_doc.exists:
        objects = ro_doc.to_dict().get("objects", [])
        print(_ok(f"room_objects 수: {len(objects)}"))
        for o in objects[:10]:
            print(f"  {o.get('objectType', '?')}  inferredFrom={o.get('inferredFrom', '')}")
        if len(objects) > 10:
            print(f"  ... 외 {len(objects) - 10}개")
    else:
        print(_warn(f"uid={uid} — room_objects 문서 없음"))

    # persona
    _header("persona")
    user_doc = db.collection("users").document(uid).get()
    if user_doc.exists:
        persona = user_doc.to_dict().get("persona")
        if persona:
            print(_ok("persona 저장됨"))
            for k, v in persona.items():
                print(f"  {k}: {v}")
        else:
            print(_warn(f"uid={uid} — users 문서는 있으나 persona 필드 없음"))
    else:
        print(_warn(f"uid={uid} — users 문서 없음"))


def main() -> None:
    parser = argparse.ArgumentParser(
        description="통합 테스트용 Firestore 상태 확인 + HTTP infer 호출 도구",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
예시:
  python scripts/check_inference.py user_001
  python scripts/check_inference.py user_001 https://REGION-PROJECT.cloudfunctions.net/infer
  python scripts/check_inference.py user_001 --results-only
        """,
    )
    parser.add_argument("uid", help="Firestore 사용자 uid")
    parser.add_argument(
        "endpoint",
        nargs="?",
        default=None,
        help="Cloud Functions infer HTTP 엔드포인트 URL (생략 시 temp_triples 확인만)",
    )
    parser.add_argument(
        "--results-only",
        action="store_true",
        help="추론 결과만 조회 (temp_triples 확인 및 infer 호출 생략)",
    )
    args = parser.parse_args()

    db = _init_firebase()

    if args.results_only:
        check_results(args.uid, db)
    elif args.endpoint:
        check_temp_triples(args.uid, db)
        trigger_inference(args.uid, args.endpoint)
        check_results(args.uid, db)
    else:
        check_temp_triples(args.uid, db)

    print()


if __name__ == "__main__":
    main()
