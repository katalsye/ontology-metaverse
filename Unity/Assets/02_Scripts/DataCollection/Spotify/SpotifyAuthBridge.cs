using UnityEngine;

namespace OntologyMetaverse.DataCollection.Spotify
{
    /// <summary>
    /// SpotifyAuthPlugin.java (Java) 호출 wrapper.
    /// </summary>
    public static class SpotifyAuthBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string PluginClassName = "com.ontology.metaverse.spotify.SpotifyAuthPlugin";
        private static AndroidJavaClass _cls;
        private static AndroidJavaClass Cls
        {
            get
            {
                if (_cls == null) _cls = new AndroidJavaClass(PluginClassName);
                return _cls;
            }
        }

        private static AndroidJavaObject GetActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
#endif

        /// <summary>
        /// /authorize URL 빌드 (Java의 Uri.Builder 활용).
        /// </summary>
        public static string BuildAuthUrl(string clientId, string redirectUri, string scopes,
                                          string codeChallenge, string state)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Cls.CallStatic<string>("buildAuthUrl",
                    clientId, redirectUri, scopes, codeChallenge, state);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpotifyAuthBridge] BuildAuthUrl 실패: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        /// 시스템 브라우저로 인증 페이지 띄움. callback 결과는 polling.
        /// </summary>
        public static void LaunchAuth(string authUrl)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Cls.CallStatic("launchAuth", GetActivity(), authUrl);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpotifyAuthBridge] LaunchAuth 실패: {e.Message}");
            }
#endif
        }

        public static bool IsCallbackReady()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<bool>("isCallbackReady"); } catch { return false; }
#else
            return false;
#endif
        }

        public static string GetCallbackCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getCallbackCode"); } catch { return null; }
#else
            return null;
#endif
        }

        public static string GetCallbackState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getCallbackState"); } catch { return null; }
#else
            return null;
#endif
        }

        public static string GetCallbackError()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getCallbackError"); } catch { return null; }
#else
            return null;
#endif
        }

        public static void ResetCallback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Cls.CallStatic("resetCallback"); } catch { }
#endif
        }
    }
}
