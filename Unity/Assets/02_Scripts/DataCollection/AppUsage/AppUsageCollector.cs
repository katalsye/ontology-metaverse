using System;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.AppUsage
{
    /// <summary>
    /// Android UsageStatsManager 기반 앱 사용 시간 수집기.
    ///
    /// 권한:
    ///   PACKAGE_USAGE_STATS는 일반 권한이 아니라 시스템 권한.
    ///   사용자가 직접 설정 → 사용 기록 액세스에서 우리 앱 허용해야 함.
    ///   미허용 시 EnsurePermission()이 설정 화면 자동 오픈.
    ///
    /// 동작:
    ///   CollectTopApps(topN) 호출 시:
    ///   - 권한 체크 → 없으면 설정 오픈 + 종료
    ///   - 권한 있으면 최근 24시간 사용 상위 N개 앱 → SQLite raw_data 저장
    /// </summary>
    public class AppUsageCollector : MonoBehaviour
    {
        [Header("수집할 상위 앱 개수")]
        [Tooltip("너무 크면 raw_data 부풀어. 5~10 권장.")]
        public int topAppsCount = 5;

        [Header("미허용 시 자동으로 설정 화면 열기")]
        public bool autoOpenSettings = true;

        /// <summary>
        /// 사용 기록 권한 체크. 없으면 설정 화면 오픈.
        /// 반환값: true면 다음 사이클에 권한 있을 거 (사용자가 허용 후), 이번 사이클은 수집 안 됨.
        /// </summary>
        public bool EnsurePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var cls = new AndroidJavaClass("com.ontology.metaverse.appusage.AppUsageHelper"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                bool ok = cls.CallStatic<bool>("hasPermission", activity);
                if (!ok)
                {
                    Debug.LogWarning("[AppUsageCollector] PACKAGE_USAGE_STATS 권한 없음");
                    if (autoOpenSettings)
                    {
                        Debug.Log("[AppUsageCollector] 시스템 설정 화면 자동 오픈");
                        cls.CallStatic("openSettings", activity);
                    }
                }
                return ok;
            }
#else
            return true;
#endif
        }

        /// <summary>
        /// 상위 N개 앱 사용 데이터를 SQLite raw_data 에 저장.
        /// 각 앱마다 1건의 raw_data (type=app_usage).
        /// BatchScheduler가 주기적으로 호출.
        /// </summary>
        public void CollectTopApps()
        {
            if (!EnsurePermission())
            {
                Debug.LogWarning("[AppUsageCollector] 권한 미허용 → 이번 수집 스킵 (사용자 허용 후 다음 사이클부터 동작)");
                return;
            }

            string json = ReadTopAppsJson(topAppsCount);
            if (string.IsNullOrEmpty(json) || json == "[]")
            {
                Debug.Log("[AppUsageCollector] 수집된 앱 없음");
                return;
            }

            // JSON 배열 파싱 — JsonUtility는 array root 직접 지원 안 해서 wrapper 사용
            string wrapped = "{\"items\":" + json + "}";
            AppUsageList list;
            try { list = JsonUtility.FromJson<AppUsageList>(wrapped); }
            catch (Exception e)
            {
                Debug.LogError($"[AppUsageCollector] JSON 파싱 실패: {e.Message}\n원본: {json}");
                return;
            }

            if (list?.items == null || list.items.Length == 0)
            {
                Debug.Log("[AppUsageCollector] items 비어있음");
                return;
            }

            int saved = 0;
            string nowIso = DateTime.UtcNow.ToString("o");
            foreach (var item in list.items)
            {
                // raw_data Content는 원래 형식 그대로 (RawDataToTripleConverter가 그대로 파싱함)
                string content = $"{{\"appName\":\"{EscapeJson(item.appName)}\",\"usageDuration\":{item.usageDuration},\"date\":\"{item.date}\"}}";
                var raw = new RawData
                {
                    Type = "app_usage",
                    Content = content,
                    Timestamp = nowIso,
                    Processed = 0
                };
                SQLiteManager.Instance.Connection.Insert(raw);
                saved++;
            }
            Debug.Log($"[AppUsageCollector] {saved}건 저장 (총 {list.items.Length}개 앱)");
        }

        private string ReadTopAppsJson(int topN)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var cls = new AndroidJavaClass("com.ontology.metaverse.appusage.AppUsageHelper"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                return cls.CallStatic<string>("getTopApps", activity, topN);
            }
#else
            // Editor mock
            return "[{\"appName\":\"com.google.android.youtube\",\"usageDuration\":90,\"date\":\"" + DateTime.Now.ToString("yyyy-MM-dd") + "\"},{\"appName\":\"com.kakao.talk\",\"usageDuration\":45,\"date\":\"" + DateTime.Now.ToString("yyyy-MM-dd") + "\"}]";
#endif
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [Serializable] private class AppUsageItem { public string appName; public int usageDuration; public string date; }
        [Serializable] private class AppUsageList { public AppUsageItem[] items; }
    }
}
