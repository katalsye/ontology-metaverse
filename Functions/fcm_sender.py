"""
fcm_sender.py
FCM 알림 송신 모듈 — 팔로워 방 업데이트 알림

유니티팀 합의 (③④):
  - 토큰 경로: users/{uid}/fcmTokens/{token}  (서브컬렉션, 멀티 디바이스 대응)
  - 팔로워 경로: follows/{uid}/followers/{followerUid}  (서브컬렉션)
  - 메시지 형식: data-only, type="room_updated"
  - Unity FcmManager가 data.type 읽어 NotificationData → users/{uid}/notifications 저장

Firestore 구조 (실제 확인 기준):
  follows/{uid}/followers/{followerUid}   → 팔로워 목록 (doc.id = followerUid)
  users/{followerUid}/fcmTokens/{token}   → 토큰 서브컬렉션 (doc.id = FCM 토큰 문자열)
"""
from __future__ import annotations

import logging
from typing import Any

logger = logging.getLogger(__name__)

_FCM_CHUNK_SIZE = 500  # firebase_admin.messaging.send_each 상한


def send_room_updated_to_followers(
    db: Any,
    messaging_client: Any,
    uid: str,
) -> int:
    """
    uid 사용자의 팔로워들에게 "방 업데이트됨" FCM data-only 알림 전송.

    Parameters:
        db              : Firestore client (firebase_admin.firestore.client())
        messaging_client: firebase_admin.messaging 모듈
        uid             : 추론 완료된 방 주인 uid

    Returns:
        성공적으로 전송된 메시지 수 (부분 성공 포함).
        예외 발생 시 0 반환 (호출자에게 전파하지 않음).
    """
    try:
        follower_uids = _get_follower_uids(db, uid)
        if not follower_uids:
            return 0

        messages = []
        for follower_uid in follower_uids:
            tokens = _get_fcm_tokens(db, follower_uid)
            for token in tokens:
                messages.append(
                    messaging_client.Message(
                        data={"type": "room_updated", "uid": uid},
                        token=token,
                    )
                )

        if not messages:
            return 0

        return _send_in_chunks(messaging_client, messages)

    except Exception as exc:
        logger.warning("send_room_updated_to_followers 실패 (무시): %s", exc)
        return 0


def _get_follower_uids(db: Any, uid: str) -> list[str]:
    """follows/{uid}/followers 서브컬렉션에서 팔로워 uid 목록 조회.

    Firestore 경로: follows/{uid}/followers/{followerUid}
    문서 ID가 곧 followerUid.
    """
    docs = (
        db.collection("follows")
        .document(uid)
        .collection("followers")
        .stream()
    )
    return [doc.id for doc in docs]


def _get_fcm_tokens(db: Any, uid: str) -> list[str]:
    """users/{uid}/fcmTokens 서브컬렉션에서 FCM 토큰 목록 조회.

    Firestore 경로: users/{uid}/fcmTokens/{token}
    문서 ID가 곧 FCM 토큰 문자열.
    Unity TokenReceived 이벤트로 자동 저장·삭제 처리.
    """
    docs = (
        db.collection("users")
        .document(uid)
        .collection("fcmTokens")
        .stream()
    )
    return [doc.id for doc in docs]


def _send_in_chunks(messaging_client: Any, messages: list[Any]) -> int:
    """메시지를 500개 단위 청크로 나눠 send_each 호출.

    FCM send_each 상한: 500개 / 호출.
    토큰 만료(registration-token-not-registered) 등 개별 실패는 무시하고
    성공 건수만 집계.
    """
    success_count = 0
    for i in range(0, len(messages), _FCM_CHUNK_SIZE):
        chunk = messages[i : i + _FCM_CHUNK_SIZE]
        try:
            batch_response = messaging_client.send_each(chunk)
            for resp in batch_response.responses:
                if resp.success:
                    success_count += 1
        except Exception as exc:
            logger.warning("FCM send_each 청크 실패 (무시): %s", exc)
    return success_count
