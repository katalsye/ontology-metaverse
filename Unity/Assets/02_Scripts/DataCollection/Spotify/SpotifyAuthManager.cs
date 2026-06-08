using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// Spotify OAuth (Authorization Code + PKCE) 관리.
    ///
    /// 책임:
    ///   - PKCE code_verifier/challenge + state 생성·보관
    ///   - 시스템 브라우저로 /authorize 띄움
    ///   - callback polling → /token 교환 → access_token + refresh_token 확보
    ///   - PlayerPrefs에 token 영구 저장 (재실행 시 자동 복원)
    ///   - access_token 만료 5분 전부터 refresh_token으로 자동 갱신
    ///
    /// 사용:
    ///   var mgr = new SpotifyAuthManager(clientId, redirectUri, scopes);
    ///   yield return mgr.EnsureValidAccessToken(); // 필요 시 인증/갱신
    ///   string token = mgr.AccessToken;            // 그 다음 API 호출에 사용
    /// </summary>
    public class SpotifyAuthManager
    {
        private const string TOKEN_URL = "https://accounts.spotify.com/api/token";

        // PlayerPrefs keys
        private const string PREF_ACCESS_TOKEN  = "spotify_access_token";
        private const string PREF_REFRESH_TOKEN = "spotify_refresh_token";
        private const string PREF_EXPIRES_AT    = "spotify_expires_at";  // Unix epoch seconds

        // 만료 5분 전부터 갱신
        private const int REFRESH_BUFFER_SECONDS = 300;

        // Callback polling 최대 시간 (사용자가 브라우저에서 로그인하는 시간 포함)
        private const float CALLBACK_TIMEOUT_SECONDS = 180f;

        public string ClientId { get; }
        public string RedirectUri { get; }
        public string Scopes { get; }

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public long ExpiresAtUnix { get; private set; }

        private string _pendingVerifier;
        private string _pendingState;

        public SpotifyAuthManager(string clientId, string redirectUri, string scopes)
        {
            ClientId = clientId;
            RedirectUri = redirectUri;
            Scopes = scopes;
            LoadTokens();
        }

        // ─────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────

        public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);
        public bool IsAccessTokenValid =>
            !string.IsNullOrEmpty(AccessToken) && NowUnix() < ExpiresAtUnix - REFRESH_BUFFER_SECONDS;

        /// <summary>
        /// access_token 유효성 확보. 없으면 OAuth 인증, 만료 임박이면 refresh.
        /// Coroutine으로 호출. 끝나면 AccessToken 사용 가능.
        /// </summary>
        public IEnumerator EnsureValidAccessToken(Action<string> onError = null)
        {
            // 1. 이미 유효
            if (IsAccessTokenValid)
            {
                yield break;
            }

            // 2. refresh 가능
            if (HasRefreshToken)
            {
                yield return RefreshAccessToken(onError);
                if (IsAccessTokenValid) yield break;
                Debug.LogWarning("[SpotifyAuth] refresh 실패 → 재인증 필요");
            }

            // 3. 신규 OAuth
            yield return PerformOAuth(onError);
        }

        /// <summary>
        /// 신규 OAuth Authorization Code + PKCE 흐름.
        /// 사용자가 브라우저에서 로그인 + 권한 허용 → callback 받음 → /token 교환.
        /// </summary>
        public IEnumerator PerformOAuth(Action<string> onError = null)
        {
            // PKCE + state 생성
            _pendingVerifier = SpotifyPKCE.GenerateCodeVerifier();
            _pendingState = SpotifyPKCE.GenerateState();
            string challenge = SpotifyPKCE.GenerateCodeChallenge(_pendingVerifier);

            // Java로 URL 빌드 + 브라우저 띄움
            string authUrl = SpotifyAuthBridge.BuildAuthUrl(ClientId, RedirectUri, Scopes, challenge, _pendingState);
            if (string.IsNullOrEmpty(authUrl))
            {
                onError?.Invoke("BuildAuthUrl 실패 (Java 호출 에러)");
                yield break;
            }
            Debug.Log($"[SpotifyAuth] /authorize 호출: {authUrl}");
            SpotifyAuthBridge.LaunchAuth(authUrl);

            // Callback polling
            float elapsed = 0;
            while (!SpotifyAuthBridge.IsCallbackReady() && elapsed < CALLBACK_TIMEOUT_SECONDS)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;
            }

            if (!SpotifyAuthBridge.IsCallbackReady())
            {
                onError?.Invoke($"OAuth callback 타임아웃 ({CALLBACK_TIMEOUT_SECONDS}s)");
                yield break;
            }

            // 결과 추출
            string code = SpotifyAuthBridge.GetCallbackCode();
            string returnedState = SpotifyAuthBridge.GetCallbackState();
            string callbackErr = SpotifyAuthBridge.GetCallbackError();
            SpotifyAuthBridge.ResetCallback();

            if (!string.IsNullOrEmpty(callbackErr))
            {
                onError?.Invoke($"Spotify 인증 거부: {callbackErr}");
                yield break;
            }
            if (string.IsNullOrEmpty(code))
            {
                onError?.Invoke("callback code 비어있음");
                yield break;
            }
            if (returnedState != _pendingState)
            {
                onError?.Invoke($"CSRF state 불일치 (보낸 {_pendingState} vs 받은 {returnedState})");
                yield break;
            }

            // code → access_token 교환
            yield return ExchangeCodeForToken(code, _pendingVerifier, onError);
        }

        // ─────────────────────────────────────────────────────
        // Token endpoint
        // ─────────────────────────────────────────────────────

        private IEnumerator ExchangeCodeForToken(string code, string verifier, Action<string> onError)
        {
            var form = new WWWForm();
            form.AddField("grant_type", "authorization_code");
            form.AddField("code", code);
            form.AddField("redirect_uri", RedirectUri);
            form.AddField("client_id", ClientId);
            form.AddField("code_verifier", verifier);

            using (var req = UnityWebRequest.Post(TOKEN_URL, form))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"/token 실패: {req.error}, body={req.downloadHandler.text}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                if (TryParseToken(json, out string access, out string refresh, out int expiresIn))
                {
                    SetTokens(access, refresh, expiresIn);
                    Debug.Log($"[SpotifyAuth] 토큰 발급 성공 (expires_in={expiresIn}s)");
                }
                else
                {
                    onError?.Invoke($"토큰 JSON 파싱 실패: {json}");
                }
            }
        }

        private IEnumerator RefreshAccessToken(Action<string> onError)
        {
            var form = new WWWForm();
            form.AddField("grant_type", "refresh_token");
            form.AddField("refresh_token", RefreshToken);
            form.AddField("client_id", ClientId);

            using (var req = UnityWebRequest.Post(TOKEN_URL, form))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"refresh 실패: {req.error}, body={req.downloadHandler.text}");
                    // refresh 실패 시 저장된 토큰 폐기 → 재인증 유도
                    ClearTokens();
                    yield break;
                }

                string json = req.downloadHandler.text;
                if (TryParseToken(json, out string access, out string newRefresh, out int expiresIn))
                {
                    // Spotify는 refresh 응답에 refresh_token 안 줄 수도 있음 — 그러면 기존 거 유지
                    SetTokens(access, !string.IsNullOrEmpty(newRefresh) ? newRefresh : RefreshToken, expiresIn);
                    Debug.Log($"[SpotifyAuth] 토큰 갱신 성공 (expires_in={expiresIn}s)");
                }
                else
                {
                    onError?.Invoke($"refresh JSON 파싱 실패: {json}");
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // Token JSON parsing (JsonUtility로는 nested 응답 깔끔히 안 됨 — 직접 파싱)
        // ─────────────────────────────────────────────────────

        private static bool TryParseToken(string json, out string access, out string refresh, out int expiresIn)
        {
            access = ExtractStringField(json, "access_token");
            refresh = ExtractStringField(json, "refresh_token");
            expiresIn = ExtractIntField(json, "expires_in");
            return !string.IsNullOrEmpty(access);
        }

        private static string ExtractStringField(string json, string key)
        {
            string needle = $"\"{key}\":";
            int idx = json.IndexOf(needle);
            if (idx < 0) return null;
            int start = json.IndexOf('"', idx + needle.Length);
            if (start < 0) return null;
            start++;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int ExtractIntField(string json, string key)
        {
            string needle = $"\"{key}\":";
            int idx = json.IndexOf(needle);
            if (idx < 0) return 0;
            int start = idx + needle.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == start) return 0;
            int.TryParse(json.Substring(start, end - start), out int val);
            return val;
        }

        // ─────────────────────────────────────────────────────
        // PlayerPrefs 영속화
        // ─────────────────────────────────────────────────────

        private void SetTokens(string access, string refresh, int expiresIn)
        {
            AccessToken = access;
            RefreshToken = refresh;
            ExpiresAtUnix = NowUnix() + expiresIn;

            PlayerPrefs.SetString(PREF_ACCESS_TOKEN, access);
            PlayerPrefs.SetString(PREF_REFRESH_TOKEN, refresh ?? "");
            PlayerPrefs.SetString(PREF_EXPIRES_AT, ExpiresAtUnix.ToString());
            PlayerPrefs.Save();
        }

        private void LoadTokens()
        {
            AccessToken = PlayerPrefs.GetString(PREF_ACCESS_TOKEN, null);
            RefreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, null);
            string expStr = PlayerPrefs.GetString(PREF_EXPIRES_AT, "0");
            long.TryParse(expStr, out long exp);
            ExpiresAtUnix = exp;

            if (!string.IsNullOrEmpty(AccessToken))
            {
                Debug.Log($"[SpotifyAuth] 저장된 토큰 로드 (만료 {ExpiresAtUnix - NowUnix()}초 후)");
            }
        }

        public void ClearTokens()
        {
            AccessToken = null;
            RefreshToken = null;
            ExpiresAtUnix = 0;
            PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(PREF_EXPIRES_AT);
            PlayerPrefs.Save();
            Debug.Log("[SpotifyAuth] 저장된 토큰 삭제");
        }

        private static long NowUnix()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
