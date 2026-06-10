using System;
using System.Security.Cryptography;
using System.Text;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// PKCE (Proof Key for Code Exchange) 헬퍼.
    /// RFC 7636 — public client (mobile app)가 client_secret 없이 안전하게 OAuth code 교환.
    ///
    /// 흐름:
    ///   1. GenerateCodeVerifier() — 43~128자 random URL-safe string
    ///   2. GenerateCodeChallenge(verifier) — SHA256(verifier) → base64url-encode
    ///   3. /authorize 요청에 code_challenge + code_challenge_method=S256 전송
    ///   4. /token 요청에 code + code_verifier 전송 (verifier가 hash 일치하면 서버 통과)
    /// </summary>
    public static class SpotifyPKCE
    {
        /// <summary>
        /// 32 bytes random → base64url 인코딩. 43자 결과 (RFC 권장 길이).
        /// </summary>
        public static string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// SHA256 해시 → base64url. /authorize 요청의 code_challenge 값.
        /// </summary>
        public static string GenerateCodeChallenge(string verifier)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(verifier));
                return Base64UrlEncode(hash);
            }
        }

        /// <summary>
        /// CSRF 방지용 state 토큰. /authorize 요청에 보내고 callback 응답에서 일치 확인.
        /// </summary>
        public static string GenerateState()
        {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base64UrlEncode(bytes);
        }

        // ── base64url (RFC 4648 §5): + → -, / → _, = padding 제거 ──
        private static string Base64UrlEncode(byte[] bytes)
        {
            string b64 = Convert.ToBase64String(bytes);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
