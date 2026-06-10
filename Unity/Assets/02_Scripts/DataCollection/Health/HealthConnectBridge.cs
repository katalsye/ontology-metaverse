using UnityEngine;

namespace OntologyMetaverse.DataCollection.Health
{
    /// <summary>
    /// HealthConnectPlugin.java 의 static 메서드를 C#에서 호출하는 wrapper.
    ///
    /// 모든 호출은 Android 전용 — Editor에서는 dummy 반환.
    /// </summary>
    public static class HealthConnectBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string PluginClassName = "com.ontology.metaverse.health.HealthConnectPlugin";
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
        /// Health Connect SDK 상태.
        /// 3 = Available, 2 = ProviderUpdateRequired (Play Store 업데이트 필요), 1 = Unavailable.
        /// </summary>
        public static HealthConnectSdkStatus GetSdkStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                int code = Cls.CallStatic<int>("getSdkStatus", GetActivity());
                return (HealthConnectSdkStatus)code;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HealthConnectBridge] getSdkStatus 실패: {e.Message}");
                return HealthConnectSdkStatus.Unavailable;
            }
#else
            return HealthConnectSdkStatus.Unavailable;
#endif
        }

        /// <summary>
        /// 우리가 요구하는 모든 권한 (READ_SLEEP/STEPS/HEART_RATE) grant 여부.
        /// </summary>
        public static bool HasAllPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Cls.CallStatic<bool>("hasAllPermissions", GetActivity());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HealthConnectBridge] hasAllPermissions 실패: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Health Connect 앱의 권한 설정 화면을 띄움. 미설치 시 Play Store로 폴백.
        /// </summary>
        public static void OpenHealthConnectSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Cls.CallStatic("openHealthConnectSettings", GetActivity());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HealthConnectBridge] openHealthConnectSettings 실패: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// 지정 type 의 비동기 read 시작. 결과는 IsReady()/GetResult() 폴링.
        /// </summary>
        public static void RequestRead(string type, long startMillis, long endMillis)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Cls.CallStatic("requestRead", GetActivity(), type, startMillis, endMillis);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HealthConnectBridge] requestRead({type}) 실패: {e.Message}");
            }
#endif
        }

        public static bool IsReady(string type)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<bool>("isReady", type); }
            catch { return false; }
#else
            return false;
#endif
        }

        public static string GetResult(string type)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getResult", type); }
            catch { return null; }
#else
            return null;
#endif
        }

        public static string GetError(string type)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getError", type); }
            catch { return null; }
#else
            return null;
#endif
        }

        public static string GetGrantedPermissionsJson()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getGrantedPermissionsJson", GetActivity()); }
            catch { return "[]"; }
#else
            return "[]";
#endif
        }
    }
}
