"""test_triggers.py — Firestore onWrite 트리거 단위 테스트."""

from __future__ import annotations

import os
import sys
import unittest
from unittest.mock import MagicMock, mock_open, patch

# ── 무거운 의존성 Mock — 테스트 실행 스코프에 한정 (#179) ──────────────────────
# triggers.py는 firebase_admin / ontology_engine을 함수 내부에서 지연 임포트하므로
# `import triggers`(pytest 수집 시점)에는 Mock이 필요 없다. Mock은 테스트가 트리거
# 함수를 호출할 때만 필요하므로 setUpModule/tearDownModule로 주입·복원한다.
#
# 과거엔 모듈 로드(수집) 시 sys.modules.setdefault(...)로 전역 주입했는데,
# duckdb를 지연 임포트하는 test_ontology_engine이 가짜 duckdb를 잡아
# `pytest Functions` 전체 실행 시 DuckDB 시나리오 2건이 거짓 실패했다.
_mock_engine = MagicMock()

_MOD_MOCKS = {
    name: MagicMock()
    for name in [
        "firebase_admin",
        "firebase_admin.firestore",
        "firebase_admin.messaging",
        "firebase_admin.storage",
        "firebase_functions",
        "firebase_functions.firestore_fn",
    ]
}
_MOD_MOCKS["ontology_engine"] = _mock_engine

_sys_modules_patch = patch.dict(sys.modules, _MOD_MOCKS)


def setUpModule() -> None:
    _sys_modules_patch.start()


def tearDownModule() -> None:
    _sys_modules_patch.stop()


sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import triggers  # noqa: E402


# ── 헬퍼 ──────────────────────────────────────────────────────────────────────

def _make_event(uid: str = "user_test") -> MagicMock:
    event = MagicMock()
    event.params = {"uid": uid}
    return event


# ── 테스트 케이스 ──────────────────────────────────────────────────────────────

class TestSkipWhenNoBucket(unittest.TestCase):
    """STORAGE_BUCKET 미설정 시 run_inference 미호출 검증."""

    def test_skip_run_inference(self):
        _mock_engine.reset_mock()
        env_without_bucket = {k: v for k, v in os.environ.items() if k != "STORAGE_BUCKET"}
        with patch.dict(os.environ, env_without_bucket, clear=True):
            triggers._handle_triple_written(_make_event("u1"))
        _mock_engine.run_inference.assert_not_called()


class TestSkipWhenNoUid(unittest.TestCase):
    """uid 없는 event 시 run_inference 미호출 검증."""

    def test_skip_no_uid(self):
        _mock_engine.reset_mock()
        event = MagicMock()
        event.params = {}
        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            triggers._handle_triple_written(event)
        _mock_engine.run_inference.assert_not_called()


class TestRunInferenceCalled(unittest.TestCase):
    """정상 실행 시 run_inference 호출 및 인자 검증."""

    def test_called_with_correct_args(self):
        _mock_engine.reset_mock()
        _mock_engine.run_inference.side_effect = None
        _mock_engine.run_inference.return_value = {"status": "ok"}
        with patch.dict(os.environ, {"STORAGE_BUCKET": "prod-bucket"}):
            with patch("builtins.open", mock_open(read_data="# rules sparql")):
                triggers._handle_triple_written(_make_event("user_abc"))
        _mock_engine.run_inference.assert_called_once()
        call_args = _mock_engine.run_inference.call_args[0]
        self.assertEqual(call_args[0], "user_abc")
        self.assertEqual(call_args[1], "prod-bucket")
        self.assertEqual(call_args[2], "# rules sparql")


class TestErrorHandlingTraceback(unittest.TestCase):
    """run_inference 예외 발생 시 traceback 로깅 검증."""

    def test_traceback_logged(self):
        _mock_engine.reset_mock()
        _mock_engine.run_inference.side_effect = RuntimeError("DB connection failed")
        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            with patch("builtins.open", mock_open(read_data="# rules")):
                with self.assertLogs("triggers", level="ERROR") as log_ctx:
                    triggers._handle_triple_written(_make_event("user_err"))
        full_log = "\n".join(log_ctx.output)
        self.assertIn("Traceback", full_log)


class TestErrorHandlingSafeExit(unittest.TestCase):
    """run_inference 예외 발생 시 예외 재발생 없이 안전 종료 검증."""

    def test_does_not_reraise(self):
        _mock_engine.reset_mock()
        _mock_engine.run_inference.side_effect = RuntimeError("boom")
        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            with patch("builtins.open", mock_open(read_data="# rules")):
                with patch.object(triggers.logger, "error"):
                    try:
                        triggers._handle_triple_written(_make_event("user_safe"))
                    except RuntimeError:
                        self.fail("_handle_triple_written이 예외를 재발생시킴")


class TestDebounceSkipsWithinWindow(unittest.TestCase):
    """DEBOUNCE_SECONDS 내 중복 트리거 → run_inference 1회만 호출 검증."""

    def test_second_trigger_skipped(self):
        _mock_engine.reset_mock()
        _mock_engine.run_inference.side_effect = None
        _mock_engine.run_inference.return_value = {"status": "ok"}

        call_count = {"n": 0}

        def mock_acquire(uid: str) -> bool:
            call_count["n"] += 1
            return call_count["n"] == 1  # 첫 번째 호출만 슬롯 획득

        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            with patch("builtins.open", mock_open(read_data="# rules")):
                with patch("triggers._try_acquire_run_slot", side_effect=mock_acquire):
                    triggers._handle_triple_written(_make_event("u_batch"))
                    triggers._handle_triple_written(_make_event("u_batch"))

        _mock_engine.run_inference.assert_called_once()


class TestDebouncePassesWhenSlotFree(unittest.TestCase):
    """_try_acquire_run_slot True 반환 시 run_inference 호출 검증."""

    def test_runs_when_slot_acquired(self):
        _mock_engine.reset_mock()
        _mock_engine.run_inference.side_effect = None
        _mock_engine.run_inference.return_value = {"status": "ok"}

        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            with patch("builtins.open", mock_open(read_data="# rules")):
                with patch("triggers._try_acquire_run_slot", return_value=True):
                    triggers._handle_triple_written(_make_event("u_free"))

        _mock_engine.run_inference.assert_called_once()


class TestDebounceSkipsWhenSlotTaken(unittest.TestCase):
    """_try_acquire_run_slot False 반환 시 run_inference 미호출 검증."""

    def test_skips_when_slot_not_acquired(self):
        _mock_engine.reset_mock()

        with patch.dict(os.environ, {"STORAGE_BUCKET": "test-bucket"}):
            with patch("triggers._try_acquire_run_slot", return_value=False):
                triggers._handle_triple_written(_make_event("u_taken"))

        _mock_engine.run_inference.assert_not_called()


if __name__ == "__main__":
    unittest.main(verbosity=2)
