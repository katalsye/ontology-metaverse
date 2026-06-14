"""test_fcm_sender.py — fcm_sender.py 단위 테스트."""
from __future__ import annotations

import sys
import os
import unittest
from unittest.mock import MagicMock, patch, call

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import fcm_sender
from fcm_sender import (
    send_room_updated_to_followers,
    send_quest_completed_notification,
    _get_follower_uids,
    _get_fcm_tokens,
    _send_in_chunks,
)

PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"


# ── Firestore mock 헬퍼 ──────────────────────────────────────────────────────

def _make_db(followers: dict[str, list[str]]) -> MagicMock:
    """
    followers: { uid: [followerUid, ...] }
    fcmTokens: followers 중 각 uid에 대해 token__{uid}_{i} 형식으로 자동 생성.

    실제 구조:
      follows/{uid}/followers/{followerUid}
      users/{followerUid}/fcmTokens/{token}
    """
    return _make_db_with_tokens(
        followers=followers,
        tokens={f_uid: [f"token__{f_uid}_{i}" for i in range(1)]
                for uid_list in followers.values()
                for f_uid in uid_list},
    )


def _make_db_with_tokens(
    followers: dict[str, list[str]],
    tokens: dict[str, list[str]],
) -> MagicMock:
    """followers와 per-uid 토큰을 독립 지정할 수 있는 mock DB."""

    def _stream_docs(ids: list[str]) -> list[MagicMock]:
        docs = []
        for doc_id in ids:
            d = MagicMock()
            d.id = doc_id
            docs.append(d)
        return docs

    db = MagicMock()

    def _collection(name: str) -> MagicMock:
        col = MagicMock()

        def _document(doc_id: str) -> MagicMock:
            doc = MagicMock()

            def _subcollection(sub_name: str) -> MagicMock:
                sub = MagicMock()
                if name == "follows" and sub_name == "followers":
                    sub.stream.return_value = _stream_docs(
                        followers.get(doc_id, [])
                    )
                elif name == "users" and sub_name == "fcmTokens":
                    sub.stream.return_value = _stream_docs(
                        tokens.get(doc_id, [])
                    )
                else:
                    sub.stream.return_value = []
                return sub

            doc.collection.side_effect = _subcollection
            return doc

        col.document.side_effect = _document
        return col

    db.collection.side_effect = _collection
    return db


def _make_messaging(success_flags: list[bool] | None = None) -> MagicMock:
    """send_each 응답 mock. success_flags 미지정 시 모두 성공."""
    m = MagicMock()

    def _send_each(messages: list) -> MagicMock:
        flags = success_flags if success_flags is not None else [True] * len(messages)
        batch = MagicMock()
        resps = []
        for ok in flags:
            r = MagicMock()
            r.success = ok
            resps.append(r)
        batch.responses = resps
        return batch

    m.send_each.side_effect = _send_each

    # Message 생성자는 호출 인자를 기록하는 mock 객체 반환
    m.Message.side_effect = lambda **kwargs: kwargs
    return m


# ── 테스트 케이스 ─────────────────────────────────────────────────────────────

class TestSendRoomUpdatedToFollowers(unittest.TestCase):

    def test_01_no_followers_returns_zero(self):
        """팔로워 없음 → 전송 0건, 예외 없음."""
        db = _make_db_with_tokens(followers={"uid_a": []}, tokens={})
        messaging = _make_messaging()

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, 0)
        messaging.send_each.assert_not_called()
        print(f"  [{PASS}] 팔로워 없음 → 0건")

    def test_02_one_follower_one_token(self):
        """팔로워 1명 + 토큰 1개 → send_each 1회 호출, 1 성공."""
        db = _make_db_with_tokens(
            followers={"uid_a": ["f1"]},
            tokens={"f1": ["tok_f1"]},
        )
        messaging = _make_messaging(success_flags=[True])

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, 1)
        messaging.send_each.assert_called_once()
        sent_msgs = messaging.send_each.call_args[0][0]
        self.assertEqual(len(sent_msgs), 1)
        self.assertEqual(sent_msgs[0]["data"]["type"], "room_updated")
        self.assertEqual(sent_msgs[0]["data"]["uid"], "uid_a")
        self.assertEqual(sent_msgs[0]["token"], "tok_f1")
        print(f"  [{PASS}] 팔로워 1명 + 토큰 1개 → 1건 성공")

    def test_03_two_followers_two_tokens_each(self):
        """팔로워 2명 + 각각 토큰 2개 → 총 4개 메시지 전송."""
        db = _make_db_with_tokens(
            followers={"uid_a": ["f1", "f2"]},
            tokens={"f1": ["tok_f1_a", "tok_f1_b"], "f2": ["tok_f2_a", "tok_f2_b"]},
        )
        messaging = _make_messaging(success_flags=[True, True, True, True])

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, 4)
        sent_msgs = messaging.send_each.call_args[0][0]
        self.assertEqual(len(sent_msgs), 4)
        tokens_sent = {m["token"] for m in sent_msgs}
        self.assertEqual(tokens_sent, {"tok_f1_a", "tok_f1_b", "tok_f2_a", "tok_f2_b"})
        print(f"  [{PASS}] 팔로워 2명 × 토큰 2개 → 4건 성공")

    def test_04_follower_without_token_skipped(self):
        """토큰 없는 팔로워 포함 → 토큰 있는 팔로워에만 전송."""
        db = _make_db_with_tokens(
            followers={"uid_a": ["f1", "f2_no_token"]},
            tokens={"f1": ["tok_f1"], "f2_no_token": []},
        )
        messaging = _make_messaging(success_flags=[True])

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, 1)
        sent_msgs = messaging.send_each.call_args[0][0]
        self.assertEqual(len(sent_msgs), 1)
        self.assertEqual(sent_msgs[0]["token"], "tok_f1")
        print(f"  [{PASS}] 토큰 없는 팔로워 스킵 → 1건만 전송")

    def test_05_send_each_exception_returns_zero(self):
        """messaging.send_each 예외 → 0 반환, 예외 전파 안 함."""
        db = _make_db_with_tokens(
            followers={"uid_a": ["f1"]},
            tokens={"f1": ["tok_f1"]},
        )
        messaging = MagicMock()
        messaging.Message.side_effect = lambda **kwargs: kwargs
        messaging.send_each.side_effect = RuntimeError("FCM quota exceeded")

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, 0)
        print(f"  [{PASS}] send_each 예외 → 0 반환 (예외 전파 없음)")

    def test_06_partial_success_counted_correctly(self):
        """500건 중 3건 실패 → 성공 497건만 반환."""
        total = 500
        failures = 3
        flags = [True] * (total - failures) + [False] * failures

        db = _make_db_with_tokens(
            followers={"uid_a": [f"f{i}" for i in range(total)]},
            tokens={f"f{i}": [f"tok_{i}"] for i in range(total)},
        )
        messaging = _make_messaging(success_flags=flags)

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(result, total - failures)
        print(f"  [{PASS}] 부분 성공: {total}건 중 {failures}건 실패 → {total - failures}건 반환")

    def test_07_chunk_split_over_500(self):
        """501개 메시지 → send_each 2회 호출 (500 + 1 청크 분할)."""
        total = 501
        db = _make_db_with_tokens(
            followers={"uid_a": [f"f{i}" for i in range(total)]},
            tokens={f"f{i}": [f"tok_{i}"] for i in range(total)},
        )
        # 첫 번째 청크 500개 모두 성공, 두 번째 청크 1개 성공
        call_count = [0]
        def _send_each(msgs: list) -> MagicMock:
            call_count[0] += 1
            batch = MagicMock()
            resps = []
            for _ in msgs:
                r = MagicMock()
                r.success = True
                resps.append(r)
            batch.responses = resps
            return batch

        messaging = MagicMock()
        messaging.Message.side_effect = lambda **kwargs: kwargs
        messaging.send_each.side_effect = _send_each

        result = send_room_updated_to_followers(db, messaging, "uid_a")

        self.assertEqual(messaging.send_each.call_count, 2)
        self.assertEqual(result, total)
        print(f"  [{PASS}] 501개 → send_each 2회 (500+1 청크 분할), {total}건 성공")


class TestSendQuestCompleted(unittest.TestCase):

    def test_01_empty_titles_no_call(self):
        """completed_titles 빈 리스트 → send_each 미호출, 0 반환."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_a": ["tok_a"]})
        messaging = _make_messaging()

        result = send_quest_completed_notification(db, messaging, "uid_a", [])

        self.assertEqual(result, 0)
        messaging.send_each.assert_not_called()
        print(f"  [{PASS}] 빈 titles → 0건, send_each 미호출")

    def test_02_sends_to_owner_tokens(self):
        """완료 퀘스트 있음 → 방 주인(uid) 토큰에 quest_completed 발송."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_a": ["tok_a1", "tok_a2"]})
        messaging = _make_messaging(success_flags=[True, True])

        result = send_quest_completed_notification(db, messaging, "uid_a", ["스트레칭 10분"])

        self.assertEqual(result, 2)
        messaging.send_each.assert_called_once()
        sent_msgs = messaging.send_each.call_args[0][0]
        self.assertEqual(len(sent_msgs), 2)
        self.assertEqual(sent_msgs[0]["data"]["type"], "quest_completed")
        self.assertEqual(sent_msgs[0]["data"]["uid"], "uid_a")
        self.assertEqual(sent_msgs[0]["data"]["count"], "1")
        print(f"  [{PASS}] 퀘스트 1건 완료 → 토큰 2개에 quest_completed 발송")

    def test_03_count_reflects_multiple_completions(self):
        """완료 퀘스트 3건 → data.count='3'."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_b": ["tok_b"]})
        messaging = _make_messaging(success_flags=[True])

        result = send_quest_completed_notification(
            db, messaging, "uid_b", ["퀘스트A", "퀘스트B", "퀘스트C"]
        )

        self.assertEqual(result, 1)
        sent_msgs = messaging.send_each.call_args[0][0]
        self.assertEqual(sent_msgs[0]["data"]["count"], "3")
        print(f"  [{PASS}] 퀘스트 3건 → data.count='3'")

    def test_04_no_tokens_returns_zero(self):
        """방 주인 FCM 토큰 없음 → 0 반환, send_each 미호출."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_a": []})
        messaging = _make_messaging()

        result = send_quest_completed_notification(db, messaging, "uid_a", ["퀘스트X"])

        self.assertEqual(result, 0)
        messaging.send_each.assert_not_called()
        print(f"  [{PASS}] 토큰 없음 → 0건, send_each 미호출")

    def test_05_exception_returns_zero(self):
        """send_each 예외 → 0 반환, 예외 전파 안 함."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_a": ["tok_a"]})
        messaging = MagicMock()
        messaging.Message.side_effect = lambda **kwargs: kwargs
        messaging.send_each.side_effect = RuntimeError("network error")

        result = send_quest_completed_notification(db, messaging, "uid_a", ["퀘스트X"])

        self.assertEqual(result, 0)
        print(f"  [{PASS}] send_each 예외 → 0 반환 (예외 전파 없음)")


class TestHelpers(unittest.TestCase):

    def test_get_follower_uids_empty(self):
        """followers 서브컬렉션 비어있으면 빈 리스트 반환."""
        db = _make_db_with_tokens(followers={"uid_x": []}, tokens={})
        result = _get_follower_uids(db, "uid_x")
        self.assertEqual(result, [])

    def test_get_follower_uids_returns_doc_ids(self):
        """followers 서브컬렉션 doc.id가 followerUid로 반환."""
        db = _make_db_with_tokens(
            followers={"uid_x": ["f_a", "f_b", "f_c"]}, tokens={}
        )
        result = _get_follower_uids(db, "uid_x")
        self.assertEqual(set(result), {"f_a", "f_b", "f_c"})

    def test_get_fcm_tokens_empty(self):
        """fcmTokens 서브컬렉션 비어있으면 빈 리스트 반환."""
        db = _make_db_with_tokens(followers={}, tokens={"uid_y": []})
        result = _get_fcm_tokens(db, "uid_y")
        self.assertEqual(result, [])

    def test_get_fcm_tokens_returns_doc_ids(self):
        """fcmTokens 서브컬렉션 doc.id가 토큰 문자열로 반환."""
        db = _make_db_with_tokens(
            followers={},
            tokens={"uid_y": ["token_abc", "token_xyz"]},
        )
        result = _get_fcm_tokens(db, "uid_y")
        self.assertEqual(set(result), {"token_abc", "token_xyz"})


if __name__ == "__main__":
    print("\n" + "=" * 60)
    print("  fcm_sender.py 단위 테스트")
    print("=" * 60)
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()
    suite.addTests(loader.loadTestsFromTestCase(TestSendRoomUpdatedToFollowers))
    suite.addTests(loader.loadTestsFromTestCase(TestSendQuestCompleted))
    suite.addTests(loader.loadTestsFromTestCase(TestHelpers))
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    print("\n" + "=" * 60)
    total = result.testsRun
    passed = total - len(result.failures) - len(result.errors)
    if passed == total:
        print(f"  결과: {passed}/{total} PASS — 모든 테스트 통과")
    else:
        print(f"  결과: {passed}/{total} PASS — {len(result.failures)} 실패")
    print("=" * 60)
