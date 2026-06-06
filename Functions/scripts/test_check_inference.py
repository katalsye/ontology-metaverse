"""test_check_inference.py — check_inference.py 단위 + CLI 통합 테스트."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import unittest
from io import BytesIO
from unittest.mock import MagicMock, patch

# ── 무거운 의존성 사전 Mock ────────────────────────────────────────────────────
for _mod in [
    "firebase_admin",
    "firebase_admin.firestore",
    "rdflib",
    "rdflib.graph",
    "rdflib.namespace",
    "rdflib.term",
]:
    sys.modules.setdefault(_mod, MagicMock())

_SCRIPTS_DIR = os.path.dirname(os.path.abspath(__file__))
_FUNCTIONS_DIR = os.path.dirname(_SCRIPTS_DIR)
sys.path.insert(0, _SCRIPTS_DIR)
sys.path.insert(0, _FUNCTIONS_DIR)

import check_inference  # noqa: E402


# ── Firestore mock 헬퍼 ──────────────────────────────────────────────────────

def _stream_doc(data: dict) -> MagicMock:
    d = MagicMock()
    d.to_dict.return_value = data
    return d


def _firestore_doc(exists: bool, data: dict | None = None) -> MagicMock:
    doc = MagicMock()
    doc.exists = exists
    doc.to_dict.return_value = data or {}
    return doc


def _db_for_temp(items: list[dict]) -> MagicMock:
    """temp_triples 스트림만 지원하는 Firestore mock."""
    db = MagicMock()
    items_col = MagicMock()
    items_col.stream.return_value = [_stream_doc(it) for it in items]
    uid_doc = MagicMock()
    uid_doc.collection.return_value = items_col
    temp_col = MagicMock()
    temp_col.document.return_value = uid_doc
    db.collection.return_value = temp_col
    return db


def _db_for_results(
    quests: list | None = None,
    room_objects: list | None = None,
    persona: dict | None = None,
) -> MagicMock:
    """quests / room_objects / users 컬렉션을 분기 지원하는 Firestore mock."""
    db = MagicMock()

    def _col(name: str) -> MagicMock:
        col = MagicMock()
        doc = MagicMock()
        if name == "quests":
            doc.get.return_value = _firestore_doc(
                quests is not None, {"quests": quests or []}
            )
        elif name == "room_objects":
            doc.get.return_value = _firestore_doc(
                room_objects is not None, {"objects": room_objects or []}
            )
        elif name == "users":
            doc.get.return_value = _firestore_doc(
                persona is not None, {"persona": persona or {}}
            )
        else:
            doc.get.return_value = _firestore_doc(False)
        col.document.return_value = doc
        return col

    db.collection.side_effect = _col
    return db


# ── check_temp_triples ────────────────────────────────────────────────────────

class TestCheckTempTriples(unittest.TestCase):
    """check_temp_triples() 동작 검증."""

    def test_empty_returns_empty_list(self):
        """temp_triples 비어있을 때 빈 리스트 반환."""
        db = _db_for_temp([])
        with patch("check_inference._load_prop_map", return_value=None):
            result = check_inference.check_temp_triples("uid1", db=db)
        self.assertEqual(result, [])

    def test_returns_all_triples(self):
        """트리플 2개 있을 때 2개 반환."""
        items = [
            {"subject": "http://7team.dev/ontology#u1", "predicate": "slept", "object": "6.5"},
            {"subject": "http://7team.dev/ontology#u1", "predicate": "walked", "object": "3000"},
        ]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            result = check_inference.check_temp_triples("uid1", db=db)
        self.assertEqual(len(result), 2)

    def test_unknown_predicate_warned(self):
        """미정의 predicate 있을 때 경고 메시지 출력."""
        items = [{"subject": "http://example.com/u1", "predicate": "ghost_pred", "object": "x"}]
        prop_map = {"slept": "http://7team.dev/ontology#slept"}
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=prop_map):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("ghost_pred", all_output)

    def test_non_uri_subject_warned(self):
        """절대 URI 아닌 subject가 있으면 경고 출력."""
        items = [{"subject": "user_plain", "predicate": "slept", "object": "5.0"}]
        prop_map = {"slept": "http://7team.dev/ontology#slept"}
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=prop_map):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("user_plain", all_output)

    # ── s/p/o 약식 필드명 호환성 ──────────────────────────────────────────────

    def test_spo_format_returns_triples(self):
        """s/p/o 약식 필드명으로 저장된 트리플도 정상 반환."""
        items = [
            {"s": "http://7team.dev/ontology#u1", "p": "slept", "o": "6.5"},
            {"s": "http://7team.dev/ontology#u1", "p": "walked", "o": "3000"},
        ]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            result = check_inference.check_temp_triples("uid1", db=db)
        self.assertEqual(len(result), 2)

    def test_spo_format_preview_shows_hint(self):
        """s/p/o 약식 필드 사용 시 미리보기에 필드명 힌트 포함."""
        items = [{"s": "http://7team.dev/ontology#u1", "p": "slept", "o": "5.0"}]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("s/p/o", all_output)

    def test_spo_format_subject_uri_validated(self):
        """s/p/o 약식 필드에서도 절대 URI 아닌 subject 경고."""
        items = [{"s": "plain_user", "p": "slept", "o": "5.0"}]
        prop_map = {"slept": "http://7team.dev/ontology#slept"}
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=prop_map):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("plain_user", all_output)

    def test_spo_object_uri_fallback(self):
        """object_uri 필드도 object 값으로 인식."""
        items = [{"subject": "http://7team.dev/ontology#u1", "predicate": "visited",
                  "object_uri": "http://7team.dev/ontology#cafe_gangnam"}]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("cafe_gangnam", all_output)

    def test_mixed_format_returns_all(self):
        """subject/predicate/object 정식 + s/p/o 약식 혼합 배치도 모두 반환."""
        items = [
            {"subject": "http://7team.dev/ontology#u1", "predicate": "slept", "object": "7.0"},
            {"s": "http://7team.dev/ontology#u1", "p": "walked", "o": "5000"},
        ]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            result = check_inference.check_temp_triples("uid1", db=db)
        self.assertEqual(len(result), 2)

    def test_full_format_preview_no_hint(self):
        """subject/predicate/object 정식 필드 사용 시 힌트 미출력."""
        items = [{"subject": "http://7team.dev/ontology#u1", "predicate": "slept", "object": "7.0"}]
        db = _db_for_temp(items)
        with patch("check_inference._load_prop_map", return_value=None):
            with patch("builtins.print") as mock_print:
                check_inference.check_temp_triples("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertNotIn("필드:", all_output)


# ── trigger_inference ─────────────────────────────────────────────────────────

class TestTriggerInference(unittest.TestCase):
    """trigger_inference() HTTP 호출 동작 검증."""

    def test_successful_call_returns_result(self):
        """HTTP 200 응답 시 파싱된 dict 반환."""
        body = json.dumps({
            "status": "ok",
            "triples": 42,
            "new_quests": ["산책 30분"],
            "missing_property_warnings": [],
        }).encode("utf-8")

        mock_resp = MagicMock()
        mock_resp.read.return_value = body
        mock_resp.status = 200

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_urlopen.return_value.__enter__.return_value = mock_resp
            mock_urlopen.return_value.__exit__.return_value = False
            result = check_inference.trigger_inference("uid1", "http://fake/infer")

        self.assertEqual(result["status"], "ok")
        self.assertEqual(result["triples"], 42)
        self.assertEqual(result["new_quests"], ["산책 30분"])

    def test_http_error_returns_error_key(self):
        """HTTP 에러 시 error 키를 포함한 dict 반환."""
        import urllib.error
        exc = urllib.error.HTTPError(
            url="http://fake", code=500, msg="Internal Server Error",
            hdrs=None, fp=BytesIO(b"boom"),
        )
        with patch("urllib.request.urlopen", side_effect=exc):
            result = check_inference.trigger_inference("uid1", "http://fake/infer")
        self.assertIn("error", result)

    def test_network_error_returns_error_key(self):
        """네트워크 오류 시 error 키 반환."""
        with patch("urllib.request.urlopen", side_effect=OSError("connection refused")):
            result = check_inference.trigger_inference("uid1", "http://fake/infer")
        self.assertIn("error", result)


# ── check_results ─────────────────────────────────────────────────────────────

class TestCheckResults(unittest.TestCase):
    """check_results() Firestore 조회 동작 검증."""

    def test_shows_quest_titles(self):
        """퀘스트 있으면 제목 출력."""
        db = _db_for_results(
            quests=[{"title": "산책 30분", "questType": "삶 개선형", "isCompleted": False}]
        )
        with patch("builtins.print") as mock_print:
            check_inference.check_results("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("산책 30분", all_output)

    def test_warns_when_quests_missing(self):
        """quests 문서 없으면 경고 포함."""
        db = _db_for_results()
        with patch("builtins.print") as mock_print:
            check_inference.check_results("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("quests 문서 없음", all_output)

    def test_shows_persona(self):
        """persona 필드 있으면 키-값 출력."""
        persona = {"energyType": "active", "lifePattern": "morning"}
        db = _db_for_results(persona=persona)
        with patch("builtins.print") as mock_print:
            check_inference.check_results("uid1", db=db)
        all_output = " ".join(str(c) for c in mock_print.call_args_list)
        self.assertIn("energyType", all_output)
        self.assertIn("active", all_output)


# ── 인자 파싱 ─────────────────────────────────────────────────────────────────

class TestArgParsing(unittest.TestCase):
    """CLI 인자 파싱 동작 검증."""

    def test_results_only_skips_temp_triples(self):
        """--results-only 플래그 → check_temp_triples 미호출."""
        with patch("sys.argv", ["check_inference.py", "uid_test", "--results-only"]):
            with patch("check_inference._init_firebase", return_value=MagicMock()):
                with patch("check_inference.check_temp_triples") as ct:
                    with patch("check_inference.check_results"):
                        check_inference.main()
        ct.assert_not_called()

    def test_endpoint_calls_all_three_steps(self):
        """endpoint 인자 제공 시 3단계 모두 호출."""
        with patch("sys.argv", ["check_inference.py", "uid1", "http://fake/infer"]):
            with patch("check_inference._init_firebase", return_value=MagicMock()):
                with patch("check_inference.check_temp_triples", return_value=[]) as ct:
                    with patch("check_inference.trigger_inference", return_value={}) as ti:
                        with patch("check_inference.check_results") as cr:
                            check_inference.main()
        ct.assert_called_once()
        ti.assert_called_once()
        cr.assert_called_once()

    def test_no_endpoint_calls_only_temp_triples(self):
        """endpoint 없으면 check_temp_triples만 호출."""
        with patch("sys.argv", ["check_inference.py", "uid2"]):
            with patch("check_inference._init_firebase", return_value=MagicMock()):
                with patch("check_inference.check_temp_triples", return_value=[]) as ct:
                    with patch("check_inference.trigger_inference") as ti:
                        with patch("check_inference.check_results") as cr:
                            check_inference.main()
        ct.assert_called_once()
        ti.assert_not_called()
        cr.assert_not_called()


# ── CLI 통합 테스트 (subprocess) ──────────────────────────────────────────────

class TestCLIIntegration(unittest.TestCase):
    """subprocess로 실제 CLI 실행 검증."""

    _SCRIPT = os.path.join(_SCRIPTS_DIR, "check_inference.py")

    def test_help_exits_zero(self):
        """--help 플래그 → 종료코드 0, 'uid' 텍스트 포함."""
        proc = subprocess.run(
            [sys.executable, self._SCRIPT, "--help"],
            capture_output=True,
            text=True,
        )
        self.assertEqual(proc.returncode, 0)
        self.assertIn("uid", proc.stdout)

    def test_no_args_exits_nonzero(self):
        """uid 없이 실행 → 종료코드 비 0 (argparse 오류)."""
        proc = subprocess.run(
            [sys.executable, self._SCRIPT],
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(proc.returncode, 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
