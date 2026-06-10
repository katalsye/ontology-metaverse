using System;
using System.Collections.Generic;

namespace OntologyMetaverse.DataCollection.Spotify
{
    // ────────────────────────────────────────────────────
    // /v1/me/player/recently-played 응답 모델
    // ────────────────────────────────────────────────────

    [Serializable]
    public class RecentlyPlayedResponse
    {
        public List<RecentlyPlayedItem> items;
    }

    [Serializable]
    public class RecentlyPlayedItem
    {
        public SpotifyTrack track;
        public string played_at;  // ISO 8601, 예: "2026-06-08T10:30:00.123Z"
    }

    [Serializable]
    public class SpotifyTrack
    {
        public string id;
        public string name;
        public int duration_ms;
        public List<SpotifyArtistRef> artists;  // 1개 이상, 첫 번째가 primary
    }

    [Serializable]
    public class SpotifyArtistRef
    {
        public string id;
        public string name;
    }

    // ────────────────────────────────────────────────────
    // 우리 도메인 — 트리플 발행 위한 정규화 모델
    // ────────────────────────────────────────────────────

    /// <summary>
    /// SQLite raw_data(type=music)에 저장할 정규화 데이터.
    /// </summary>
    [Serializable]
    public class MusicListeningRecord
    {
        public string track_id;       // Spotify track ID (URI 충돌 방지)
        public string trackName;      // → prod:trackName
        public string artist;         // 첫 번째 artist name → prod:artist
        public string genre;          // 첫 번째 genre (없으면 "") → prod:genre
        public string playedAt;       // ISO 8601 → prod:playedAt
        public int listenDuration;    // 분 (duration_ms/60000 반올림) → prod:listenDuration
    }
}
