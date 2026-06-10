using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// Last.fm artist.getTopTags로 아티스트 장르(genre) 조회.
    ///
    /// 왜 필요한가:
    ///   Spotify 신규 앱(Development Mode)은 catalog 엔드포인트(/v1/artists)가 403이고,
    ///   user 우회(/v1/me/top/artists)는 200이지만 보컬로이드 등 인디 아티스트의 genres가 비어있음.
    ///   → Spotify로는 genre를 못 얻어, 무성님 음악 추론(Rule 9/26/27/28)이 안 돈다.
    ///   Last.fm은 전 세계 음악 메타데이터 DB라 아티스트 이름만 주면 장르 태그를 준다.
    ///   (k-pop·pop·j-pop·rock·classical 등 풍부, 무료, read-only 키만 필요)
    ///
    /// 역할 분담:
    ///   Spotify  = "뭘 언제 들었나" (내 청취 기록)
    ///   Last.fm  = "그 아티스트가 무슨 장르냐" (genre만 보강)
    ///
    /// API (무료):
    ///   GET https://ws.audioscrobbler.com/2.0/?method=artist.gettoptags
    ///       &artist={name}&api_key={key}&format=json&autocorrect=1
    ///   응답: { "toptags": { "tag": [ {"name":"j-pop","count":100}, ... ] } }
    ///   에러 시: { "error":6, "message":"..." } → toptags null → "" 반환
    /// </summary>
    public class LastFmGenreClient
    {
        private const string API_BASE = "https://ws.audioscrobbler.com/2.0/";
        private readonly string _apiKey;

        // 무성님 음악 규칙이 LCASE+CONTAINS 매칭하는 장르 키워드.
        // 이 키워드가 들어간 태그를 우선 선택 → "j-pop"이면 Rule 28(pop), "hard rock"이면 Rule 27(rock) 매칭.
        //   Rule 26 classic/lo-fi · Rule 27 metal/rock · Rule 28 dance/pop · Rule 9 장르별 SUM
        private static readonly string[] RuleGenreKeywords =
            { "classic", "lo-fi", "lofi", "metal", "rock", "dance", "pop",
              "k-pop", "j-pop", "hip-hop", "hip hop", "jazz", "r&b", "soul", "indie", "electronic" };

        // 장르가 아닌 노이즈 태그 (Last.fm 사용자들이 자유롭게 붙이는 비-장르 태그). 정확히 일치하면 제외.
        private static readonly HashSet<string> NoiseTags = new HashSet<string>
        {
            "seen live", "favorite", "favourite", "favorites", "favourites", "awesome",
            "love", "loved", "beautiful", "spotify", "male vocalists", "female vocalists",
            "male vocalist", "female vocalist", "under 2000 listeners", "amazing", "cool"
        };

        public LastFmGenreClient(string apiKey) { _apiKey = apiKey; }

        public bool HasKey => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>
        /// 아티스트 이름 → 대표 genre 1개. 실패/없음이면 "".
        /// </summary>
        public IEnumerator GetArtistGenre(string artistName, Action<string> onResult)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(artistName))
            {
                onResult?.Invoke("");
                yield break;
            }

            string url = $"{API_BASE}?method=artist.gettoptags" +
                         $"&artist={UnityWebRequest.EscapeURL(artistName)}" +
                         $"&api_key={_apiKey}&format=json&autocorrect=1";

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[LastFm] '{artistName}' 태그 조회 실패: {req.error}");
                    onResult?.Invoke("");
                    yield break;
                }

                string genre = "";
                try
                {
                    var parsed = JsonUtility.FromJson<TopTagsResponse>(req.downloadHandler.text);
                    genre = PickGenre(parsed?.toptags?.tag);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LastFm] '{artistName}' 파싱 실패: {e.Message}");
                }
                onResult?.Invoke(genre);
            }
        }

        // 인기순 태그에서 무성님 규칙 키워드 매칭 우선 → 없으면 노이즈 아닌 top1.
        private static string PickGenre(List<LastFmTag> tags)
        {
            if (tags == null || tags.Count == 0) return "";

            // 1순위: 무성님 규칙 키워드를 포함하는 가장 인기 있는 태그 (j-pop→pop, hard rock→rock)
            foreach (var t in tags)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.name)) continue;
                string lower = t.name.ToLowerInvariant();
                foreach (var kw in RuleGenreKeywords)
                    if (lower.Contains(kw)) return t.name;
            }

            // 2순위: 노이즈 아닌 가장 인기 태그 (규칙 매칭은 안 돼도 genre 자체는 보존 → Rule 6-D/9 카운팅용)
            foreach (var t in tags)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.name)) continue;
                if (!NoiseTags.Contains(t.name.ToLowerInvariant())) return t.name;
            }

            return "";
        }

        // ── JSON 모델 (JsonUtility 호환) ──
        [Serializable] private class TopTagsResponse { public TopTags toptags; }
        [Serializable] private class TopTags { public List<LastFmTag> tag; }
        [Serializable] private class LastFmTag { public string name; public int count; }
    }
}
