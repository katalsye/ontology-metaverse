using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// Spotify Web API 클라이언트 (UnityWebRequest 기반).
    ///
    /// 엔드포인트:
    ///   - GET /v1/me/player/recently-played?limit=50  → 최근 들은 트랙 50개
    ///   - GET /v1/artists?ids=id1,id2,...,id50         → 여러 artist genre 한 번에 (배치)
    ///
    /// 인증: Authorization: Bearer {access_token}
    ///
    /// 사용:
    ///   yield return client.FetchRecentlyPlayed(token, items => { ... }, err => { ... });
    ///   yield return client.FetchArtists(token, ids, artists => { ... }, err => { ... });
    /// </summary>
    public class SpotifyApiClient
    {
        private const string API_BASE = "https://api.spotify.com/v1";

        /// <summary>
        /// 최근 재생 50건 조회.
        /// </summary>
        public IEnumerator FetchRecentlyPlayed(
            string accessToken,
            Action<List<RecentlyPlayedItem>> onSuccess,
            Action<string> onError)
        {
            string url = $"{API_BASE}/me/player/recently-played?limit=50";
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"recently-played HTTP 실패: {req.error}, body={req.downloadHandler.text}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                try
                {
                    var parsed = JsonUtility.FromJson<RecentlyPlayedResponse>(json);
                    if (parsed?.items == null)
                    {
                        onError?.Invoke($"items null. JSON 일부: {json.Substring(0, Math.Min(json.Length, 300))}");
                        yield break;
                    }
                    onSuccess?.Invoke(parsed.items);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"recently-played 파싱 예외: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 여러 artist ID를 한 번에 조회 (최대 50개). genre 추출용.
        /// </summary>
        public IEnumerator FetchArtists(
            string accessToken,
            List<string> artistIds,
            Action<List<SpotifyArtist>> onSuccess,
            Action<string> onError)
        {
            if (artistIds == null || artistIds.Count == 0)
            {
                onSuccess?.Invoke(new List<SpotifyArtist>());
                yield break;
            }

            // 최대 50개씩 batch
            const int batchSize = 50;
            var result = new List<SpotifyArtist>();
            for (int i = 0; i < artistIds.Count; i += batchSize)
            {
                int end = Math.Min(i + batchSize, artistIds.Count);
                var slice = artistIds.GetRange(i, end - i);
                string idsParam = string.Join(",", slice);
                string url = $"{API_BASE}/artists?ids={UnityWebRequest.EscapeURL(idsParam)}";

                using (var req = UnityWebRequest.Get(url))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                    req.timeout = 15;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"artists HTTP 실패: {req.error}, body={req.downloadHandler.text}");
                        yield break;
                    }

                    string json = req.downloadHandler.text;
                    try
                    {
                        var parsed = JsonUtility.FromJson<ArtistsResponse>(json);
                        if (parsed?.artists != null) result.AddRange(parsed.artists);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"artists 파싱 예외: {e.Message}");
                        yield break;
                    }
                }
            }

            onSuccess?.Invoke(result);
        }
    }
}
