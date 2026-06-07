using System;
using System.Collections.Generic;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.UsageStats
{
    /// <summary>
    /// 안드로이드 UsageStatsManager를 활용한 앱 사용 통계 수집기
    /// 
    /// 특수 권한 (PACKAGE_USAGE_STATS) 처리:
    /// - 일반 권한 팝업 안 뜸
    /// - Settings → 사용 권한 액세스 → 본인 앱 → 사용자가 직접 허용
    /// - AppOpsManager로 권한 상태 체크
    /// 
    /// 동작:
    /// - Editor: 더미 앱 사용 통계 1건 저장
    /// - 실기기: 지난 N시간의 앱별 사용 시간 수집
    /// </summary>
    public class UsageStatsCollector : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("수집할 시간 범위 (시간 단위, 예: 24 = 지난 24시간)")]
        public int hoursToCollect = 24;

        [Tooltip("최소 사용 시간 임계값 (밀리초). 이 시간 이상 사용한 앱만 수집")]
        public long minUsageTimeMs = 60000;  // 1분

        /// <summary>
        /// 앱 사용 통계 수집 시작
        /// </summary>
        public void CollectUsageStats()
        {
            Debug.Log("[UsageStatsCollector] 앱 사용 통계 수집 시작");

#if UNITY_EDITOR
            CollectInEditor();
#else
            CollectInAndroid();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 모드: 더미 데이터 저장
        /// </summary>
        private void CollectInEditor()
        {
            Debug.LogWarning("[UsageStatsCollector] Editor에서는 더미 데이터 사용");

            // 더미 앱 사용 통계 3건
            SaveToSQLite("com.instagram.android", "Instagram", 3600000);  // 1시간
            SaveToSQLite("com.spotify.music", "Spotify", 1800000);  // 30분
            SaveToSQLite("com.youtube.android", "YouTube", 2400000);  // 40분

            Debug.Log("[UsageStatsCollector] 더미 데이터 3건 저장 완료");
        }
#else
        /// <summary>
        /// 실기기 모드: UsageStatsManager 호출
        /// </summary>
        private void CollectInAndroid()
        {
            // 1. 특수 권한 체크
            if (!HasUsageAccessPermission())
            {
                Debug.LogWarning("[UsageStatsCollector] 사용 권한 없음 - Settings 열기");
                OpenUsageAccessSettings();
                return;
            }

            // 2. UsageStatsManager 호출
            int savedCount = QueryAndSaveUsageStats();
            Debug.Log($"[UsageStatsCollector] 수집 완료: {savedCount}건 저장");
        }

        /// <summary>
        /// 특수 권한 (PACKAGE_USAGE_STATS) 보유 여부 체크
        /// AppOpsManager를 사용해 권한 상태 확인
        /// </summary>
        private bool HasUsageAccessPermission()
        {
            try
            {
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject appOpsService = context.Call<AndroidJavaObject>("getSystemService", "appops"))
                {
                    // AppOpsManager.checkOpNoThrow("android:get_usage_stats", uid, packageName)
                    int uid = context.Call<int>("getUid", context.Call<AndroidJavaObject>("getApplicationInfo").Get<int>("uid"));
                    string packageName = context.Call<string>("getPackageName");

                    int mode = appOpsService.Call<int>(
                        "checkOpNoThrow",
                        "android:get_usage_stats",
                        uid,
                        packageName
                    );

                    // AppOpsManager.MODE_ALLOWED = 0
                    return mode == 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UsageStatsCollector] 권한 체크 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용 권한 액세스 Settings 화면 열기
        /// Intent.ACTION_USAGE_ACCESS_SETTINGS 호출
        /// </summary>
        private void OpenUsageAccessSettings()
        {
            try
            {
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.USAGE_ACCESS_SETTINGS"))
                {
                    // FLAG_ACTIVITY_NEW_TASK = 268435456
                    intent.Call<AndroidJavaObject>("setFlags", 268435456);
                    context.Call("startActivity", intent);

                    Debug.Log("[UsageStatsCollector] Settings 화면 열림 - 사용자가 권한 허용해야 함");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UsageStatsCollector] Settings 열기 실패: {e.Message}");
            }
        }

        /// <summary>
        /// UsageStatsManager로 앱 사용 통계 쿼리 + SQLite 저장
        /// </summary>
        /// <returns>저장 건수</returns>
        private int QueryAndSaveUsageStats()
        {
            int savedCount = 0;

            try
            {
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject usageStatsManager = context.Call<AndroidJavaObject>("getSystemService", "usagestats"))
                {
                    // 시간 범위: 지난 N시간
                    long endTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                    long startTime = endTime - (long)(hoursToCollect * 3600000);

                    // INTERVAL_DAILY = 0
                    AndroidJavaObject usageStatsList = usageStatsManager.Call<AndroidJavaObject>(
                        "queryUsageStats",
                        0,
                        startTime,
                        endTime
                    );

                    int size = usageStatsList.Call<int>("size");
                    Debug.Log($"[UsageStatsCollector] UsageStats 쿼리 결과: {size}개");

                    for (int i = 0; i < size; i++)
                    {
                        using (AndroidJavaObject usageStat = usageStatsList.Call<AndroidJavaObject>("get", i))
                        {
                            string packageName = usageStat.Call<string>("getPackageName");
                            long totalTimeInForeground = usageStat.Call<long>("getTotalTimeInForeground");

                            // 최소 사용 시간 필터링
                            if (totalTimeInForeground >= minUsageTimeMs)
                            {
                                SaveToSQLite(packageName, packageName, totalTimeInForeground);
                                savedCount++;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UsageStatsCollector] UsageStats 쿼리 실패: {e.Message}");
            }

            return savedCount;
        }
#endif

        /// <summary>
        /// 앱 사용 통계를 SQLite raw_data에 저장
        /// </summary>
        private void SaveToSQLite(string packageName, string appName, long usageTimeMs)
        {
            string content = $"{{\"package\":\"{packageName}\",\"app_name\":\"{appName}\",\"usage_ms\":{usageTimeMs}}}";

            var rawData = new RawData
            {
                Type = "usage_stats",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            SQLiteManager.Instance.Connection.Insert(rawData);
            Debug.Log($"[UsageStatsCollector] DB 저장: Id={rawData.Id}, package={packageName}, time={usageTimeMs}ms");
        }
    }
}