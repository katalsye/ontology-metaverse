using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// Spotify 최근 재생 트랙 수집기.
    ///
    /// 동작:
    ///   1. SpotifyAuthManager로 access_token 확보 (필요 시 OAuth/refresh)
    ///   2. /v1/me/player/recently-played?limit=50 호출
    ///   3. 트랙들의 primary artist ID 수집 → genre 캐시에 없는 artist만 batch 조회
    ///   4. (track_id + played_at) 기준 중복 체크 → 새 재생만 raw_data(type=music) 저장
    ///      Content: {"track_id":"...","trackName":"...","artist":"...","genre":"k-pop",
    ///                "playedAt":"...","listenDuration":4}
    ///
    /// RawDataToTripleConverter.ConvertMusicListening 가 이 raw_data를:
    ///   - (user, prod:listensTo, ml_{raw.Id})
    ///   - (ml_X, prod:trackName, "..."^^xsd:string)
    ///   - (ml_X, prod:artist, "..."^^xsd:string)
    ///   - (ml_X, prod:genre, "k-pop"^^xsd:string)   ← 무성님 LCASE+CONTAINS 매칭
    ///   - (ml_X, prod:playedAt, "..."^^xsd:dateTime)
    ///   - (ml_X, prod:listenDuration, 4^^xsd:integer)
    ///
    /// 활성화 Rule:
    ///   Rule 6-D (missing_music_mood): genre 있는데 mood 없음 → 보완형
    ///   Rule 9 (MusicMood):            장르별 일 SUM 120+분 → MusicMood + music_speaker
    ///   Rule 26 (FocusMode):           classic/lo-fi 180+분 → FocusMode + desk_light_bright
    ///   Rule 27 (StressIndicator):     metal/rock 야간(22h+) → stress_ball
    ///   Rule 28 (SocialActivity):      dance/pop + 외출 시간대 근접 → party_light
    /// </summary>
    public class SpotifyCollector : MonoBehaviour
    {
        [Header("Spotify Developer Dashboard에서 발급받은 Client ID")]
        [Tooltip("https://developer.spotify.com/dashboard 에서 앱 등록 후 받은 32자리 영숫자")]
        public string clientId = "";

        [Header("Redirect URI (Dashboard에 등록한 값과 정확히 일치)")]
        public string redirectUri = "ontologyapp://spotify-callback";

        [Header("OAuth Scopes")]
        [Tooltip("user-read-recently-played 만 있으면 최근 재생 조회 가능. user-top-read 추가하면 장르 추론 보강.")]
        public string scopes = "user-read-recently-played user-top-read";

        [Header("강제 재인증 (디버깅용)")]
        [Tooltip("true면 저장된 토큰 삭제 후 OAuth 다시. 사용 후 false로.")]
        public bool forceReauth = false;

        [Header("Last.fm API Key (genre 보강용)")]
        [Tooltip("https://www.last.fm/api/account/create 에서 무료 발급. Spotify가 신규 앱에서 genre를 안 줘서 " +
                 "아티스트 장르를 Last.fm으로 조회. 비우면 genre 없이 수집(음악 추론 규칙 비활성).")]
        public string lastFmApiKey = "";

        private SpotifyAuthManager _auth;
        private SpotifyApiClient _api;
        private LastFmGenreClient _lastFm;

        // artist 이름 → genre (메모리 캐시, 앱 재시작 시 초기화 OK — 아티스트 장르는 잘 안 바뀜).
        // 빈 문자열도 캐싱해 같은 아티스트 재조회를 막는다(Last.fm 호출 절약).
        private readonly Dictionary<string, string> _artistGenreCache = new Dictionary<string, string>();

        private void Awake()
        {
            _auth = new SpotifyAuthManager(clientId, redirectUri, scopes);
            _api = new SpotifyApiClient();
            _lastFm = new LastFmGenreClient(lastFmApiKey);
        }

        /// <summary>
        /// Spotify OAuth 토큰 보유 여부.
        /// BatchScheduler가 매 cycle 체크해서 미인증이면 수집 자체를 스킵.
        /// </summary>
        public bool HasAuth() => _auth != null && _auth.IsAccessTokenValid;

        /// <summary>
        /// 초기화 단계에서 OAuth 토큰만 확보하는 메서드. BatchScheduler.Start()가 직렬로 호출.
        /// Health Connect 설정 화면 · Calendar 권한 팝업과 같은 시점에 OAuth 브라우저 띄우면
        /// 서로 가려서 사용자가 못 보는 문제 방지.
        ///
        /// 동작:
        ///   - clientId 미설정 → 즉시 yield break
        ///   - 이미 유효 토큰 → 즉시 yield break
        ///   - 없으면 EnsureValidAccessToken(브라우저 OAuth) 호출
        /// </summary>
        public IEnumerator EnsureAuthInteractive()
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                Debug.LogWarning("[SpotifyCollector] clientId 미설정 → OAuth 스킵");
                yield break;
            }

            if (forceReauth)
            {
                _auth.ClearTokens();
                forceReauth = false;
            }

            if (_auth.IsAccessTokenValid)
            {
                Debug.Log("[SpotifyCollector] 토큰 이미 유효 → OAuth 스킵");
                yield break;
            }

            string authError = null;
            yield return _auth.EnsureValidAccessToken(err => authError = err);

            if (!_auth.IsAccessTokenValid)
                Debug.LogError($"[SpotifyCollector] OAuth 실패: {authError ?? "(원인 미상)"} → 다음 cycle 재시도");
            else
                Debug.Log("[SpotifyCollector] OAuth 인증 완료");
        }

        /// <summary>
        /// 한 번의 수집 사이클. BatchScheduler가 주기적으로 호출.
        /// </summary>
        public IEnumerator CollectRecentTracks()
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                Debug.LogWarning("[SpotifyCollector] clientId 미설정 → 스킵 (Inspector에 입력)");
                yield break;
            }

            // 디버그용 강제 재인증
            if (forceReauth)
            {
                _auth.ClearTokens();
                forceReauth = false;
            }

            // 1. 토큰 확보
            string authError = null;
            yield return _auth.EnsureValidAccessToken(err => authError = err);

            if (!_auth.IsAccessTokenValid)
            {
                Debug.LogError($"[SpotifyCollector] 토큰 확보 실패: {authError ?? "(원인 미상)"}");
                yield break;
            }

            // 2. recently-played 조회
            List<RecentlyPlayedItem> items = null;
            string fetchErr = null;
            yield return _api.FetchRecentlyPlayed(_auth.AccessToken,
                onSuccess: list => items = list,
                onError: e => fetchErr = e);

            if (items == null)
            {
                Debug.LogError($"[SpotifyCollector] recently-played 실패: {fetchErr}");
                yield break;
            }

            if (items.Count == 0)
            {
                Debug.Log("[SpotifyCollector] 최근 재생 없음");
                yield break;
            }

            // 3. genre 캐시 확보 — Spotify는 신규 앱에서 genre를 못 줘(catalog /v1/artists 403,
            //    user 우회 /v1/me/top/artists는 200이지만 인디/보컬로이드 genres가 빈 값).
            //    → Last.fm artist.getTopTags로 아티스트별 장르 조회. 아티스트 이름 단위 캐싱.
            if (_lastFm != null && _lastFm.HasKey)
            {
                foreach (var item in items)
                {
                    string aName = (item.track?.artists != null && item.track.artists.Count > 0)
                        ? item.track.artists[0].name : null;
                    if (string.IsNullOrWhiteSpace(aName) || _artistGenreCache.ContainsKey(aName))
                        continue;

                    string g = "";
                    yield return _lastFm.GetArtistGenre(aName, res => g = res);
                    _artistGenreCache[aName] = g;  // 빈 값도 캐싱(재조회 방지)
                    if (!string.IsNullOrEmpty(g))
                        Debug.Log($"[SpotifyCollector] Last.fm genre: {aName} → {g}");
                }
            }
            else
            {
                Debug.LogWarning("[SpotifyCollector] Last.fm API Key 미설정 → genre 없이 수집 (Inspector에 입력)");
            }

            // 4. 트랙별 중복 체크 + 저장
            int newCount = 0;
            int skipCount = 0;
            foreach (var item in items)
            {
                if (item.track == null || string.IsNullOrEmpty(item.track.id))
                {
                    skipCount++;
                    continue;
                }

                if (IsAlreadySaved(item.track.id, item.played_at))
                {
                    skipCount++;
                    continue;
                }

                string artistName = (item.track.artists != null && item.track.artists.Count > 0)
                    ? item.track.artists[0].name : "";
                string genre = _artistGenreCache.TryGetValue(artistName, out string g) ? g : "";

                var record = new MusicListeningRecord
                {
                    track_id = item.track.id,
                    trackName = item.track.name ?? "",
                    artist = artistName,
                    genre = genre,
                    playedAt = item.played_at,
                    listenDuration = Math.Max(1, item.track.duration_ms / 60000),
                };

                SaveTrack(record);
                newCount++;
            }

            Debug.Log($"[SpotifyCollector] 완료: 신규 {newCount}건 저장, 중복 {skipCount}건 스킵");
        }

        // ─────────────────────────────────────────────────────
        // SQLite 헬퍼
        // ─────────────────────────────────────────────────────

        private bool IsAlreadySaved(string trackId, string playedAt)
        {
            try
            {
                var conn = SQLiteManager.Instance.Connection;
                // track_id + played_at 둘 다 같으면 같은 재생
                string needle = $"\"track_id\":\"{trackId}\"";
                string playedNeedle = $"\"playedAt\":\"{playedAt}\"";
                var query = conn.Table<RawData>().Where(r => r.Type == "music");
                foreach (var r in query)
                {
                    if (r.Content != null && r.Content.Contains(needle) && r.Content.Contains(playedNeedle))
                        return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpotifyCollector] dedup 확인 실패: {e.Message}");
                return false;
            }
        }

        private void SaveTrack(MusicListeningRecord r)
        {
            string content = "{" +
                $"\"track_id\":\"{EscapeJson(r.track_id)}\"," +
                $"\"trackName\":\"{EscapeJson(r.trackName)}\"," +
                $"\"artist\":\"{EscapeJson(r.artist)}\"," +
                $"\"genre\":\"{EscapeJson(r.genre)}\"," +
                $"\"playedAt\":\"{r.playedAt}\"," +
                $"\"listenDuration\":{r.listenDuration}" +
                "}";

            var raw = new RawData
            {
                Type = "music",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Processed = 0
            };
            SQLiteManager.Instance.Connection.Insert(raw);
            Debug.Log($"[SpotifyCollector] 저장: \"{r.trackName}\" by {r.artist} (genre={r.genre}, {r.listenDuration}분)");
        }

        // ─────────────────────────────────────────────────────

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
