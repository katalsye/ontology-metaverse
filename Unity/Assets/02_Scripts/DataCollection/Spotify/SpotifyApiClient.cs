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
    ///
    /// 인증: Authorization: Bearer {access_token}
    ///
    /// 사용:
    ///   yield return client.FetchRecentlyPlayed(token, items => { ... }, err => { ... });
    ///
    /// 주의: genre는 Spotify가 주지 않는다(신규 앱 catalog /v1/artists 403, top/artists는 genres 빈 값).
    ///       LastFmGenreClient가 아티스트 이름으로 장르를 보강한다.
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
    }
}
