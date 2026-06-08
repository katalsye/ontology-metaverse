using System;
using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Health
{
    /// <summary>
    /// Health Connect (androidx.health.connect) 통합 수집기.
    ///
    /// 동작:
    ///   1. SDK 상태 확인 → Unavailable이면 수집 스킵 (로그만)
    ///   2. 권한 확인 → 없으면 Health Connect 앱 설정 화면 띄움 (1회)
    ///   3. Sleep / Steps / HeartRate 각각 비동기 read 요청
    ///   4. polling으로 결과 대기 (최대 10초)
    ///   5. 무성님 명세 형식으로 raw_data 저장:
    ///      - type="sleep":      {"duration":7.5,"quality":80,"deepSleepRatio":0.25,"timestamp":"..."}
    ///      - type="step":       {"count":2800,"date":"2026-06-08"}
    ///      - type="heart_rate": {"avgBpm":72.5,"sampleCount":42,"timestamp":"..."}
    ///
    /// RawDataToTripleConverter:
    ///   - ConvertSleep      → 무성님 Rule 1·2·5·6-E·10·11·12·13·P1 활성화
    ///   - ConvertStep       → 무성님 Rule 3·12·P1 활성화 (기존 StepCollector 대비 정확도↑)
    ///   - ConvertHeartRate  → 신규 (무성님 클래스 추가 필요)
    ///
    /// 권한 흐름:
    ///   첫 실행 시 hasAllPermissions=false → OpenHealthConnectSettings()로 redirect
    ///   사용자가 Health Connect 앱에서 권한 부여 → 다음 batch 사이클부터 정상 작동
    /// </summary>
    public class HealthConnectCollector : MonoBehaviour
    {
        [Header("Read 윈도우 (지난 N시간)")]
        [Tooltip("매 호출마다 이 시간만큼의 최신 데이터를 읽음. 24h면 어제~오늘 수면 한 번에 들어옴.")]
        public int readWindowHours = 24;

        [Header("폴링 타임아웃 (초)")]
        [Tooltip("Java ListenableFuture 완료 대기 최대 시간. 보통 1초 이내 끝남.")]
        public float pollTimeoutSeconds = 10f;

        [Header("권한 부족 시 자동으로 설정 화면 띄우기")]
        public bool autoOpenSettingsOnMissingPermission = true;

        private bool _settingsOpened = false;
        private bool _availabilityWarningLogged = false;

        /// <summary>
        /// 한 번의 수집 사이클. BatchScheduler가 주기적으로 호출.
        /// </summary>
        public IEnumerator CollectHealthData()
        {
            // 1. SDK 상태 확인
            var status = HealthConnectBridge.GetSdkStatus();
            if (status != HealthConnectSdkStatus.Available)
            {
                if (!_availabilityWarningLogged)
                {
                    Debug.LogWarning($"[HealthConnect] SDK 사용 불가: {status} → 수집 스킵 (Health Connect 앱 설치/업데이트 필요)");
                    _availabilityWarningLogged = true;

                    // ProviderUpdateRequired면 Play Store로 가이드
                    if (status == HealthConnectSdkStatus.ProviderUpdateRequired)
                    {
                        HealthConnectBridge.OpenHealthConnectSettings();
                    }
                }
                yield break;
            }

            // 2. 권한 확인
            if (!HealthConnectBridge.HasAllPermissions())
            {
                Debug.LogWarning($"[HealthConnect] 권한 부족. 현재 grant: {HealthConnectBridge.GetGrantedPermissionsJson()}");
                if (autoOpenSettingsOnMissingPermission && !_settingsOpened)
                {
                    Debug.Log("[HealthConnect] Health Connect 설정 화면 띄움 — 사용자가 권한 부여 필요");
                    HealthConnectBridge.OpenHealthConnectSettings();
                    _settingsOpened = true;
                }
                yield break;
            }

            // 3. 시간 윈도우 계산
            long endMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            long startMillis = endMillis - (long)readWindowHours * 3600L * 1000L;

            // 4. Sleep / Steps / HeartRate 병렬 요청
            HealthConnectBridge.RequestRead("sleep", startMillis, endMillis);
            HealthConnectBridge.RequestRead("steps", startMillis, endMillis);
            HealthConnectBridge.RequestRead("heart_rate", startMillis, endMillis);

            // 5. 폴링으로 3개 다 끝날 때까지 대기
            float elapsed = 0;
            while (elapsed < pollTimeoutSeconds)
            {
                bool sleepDone = HealthConnectBridge.IsReady("sleep");
                bool stepsDone = HealthConnectBridge.IsReady("steps");
                bool hrDone = HealthConnectBridge.IsReady("heart_rate");
                if (sleepDone && stepsDone && hrDone) break;
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            // 6. 각각 결과 처리
            ProcessSleepResult();
            ProcessStepsResult();
            ProcessHeartRateResult();
        }

        // ─────────────────────────────────────────────────────
        // Sleep
        // ─────────────────────────────────────────────────────

        private void ProcessSleepResult()
        {
            string err = HealthConnectBridge.GetError("sleep");
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[HealthConnect] sleep 에러: {err}");
                return;
            }

            string json = HealthConnectBridge.GetResult("sleep");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[HealthConnect] sleep 결과 비어있음");
                return;
            }

            try
            {
                var parsed = JsonUtility.FromJson<SleepRecordsJson>(json);
                if (parsed?.records == null || parsed.records.Count == 0)
                {
                    Debug.Log("[HealthConnect] sleep records 없음");
                    return;
                }

                foreach (var r in parsed.records)
                {
                    // 무성님 명세 형식으로 변환 (ConvertSleep 호환)
                    string content = "{" +
                        $"\"duration\":{r.durationHours.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"\"quality\":{r.quality}," +
                        $"\"deepSleepRatio\":{r.deepSleepRatio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"\"timestamp\":\"{r.startTime}\"" +
                        "}";

                    var raw = new RawData
                    {
                        Type = "sleep",
                        Content = content,
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        Processed = 0
                    };
                    SQLiteManager.Instance.Connection.Insert(raw);
                    Debug.Log($"[HealthConnect] sleep 저장: duration={r.durationHours:F1}h, quality={r.quality}, deepRatio={r.deepSleepRatio:F2}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HealthConnect] sleep 파싱 실패: {e.Message}\nJSON: {json}");
            }
        }

        // ─────────────────────────────────────────────────────
        // Steps
        // ─────────────────────────────────────────────────────

        private void ProcessStepsResult()
        {
            string err = HealthConnectBridge.GetError("steps");
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[HealthConnect] steps 에러: {err}");
                return;
            }

            string json = HealthConnectBridge.GetResult("steps");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[HealthConnect] steps 결과 비어있음");
                return;
            }

            try
            {
                var parsed = JsonUtility.FromJson<StepsResultJson>(json);
                if (parsed == null || parsed.totalSteps == 0)
                {
                    Debug.Log("[HealthConnect] steps totalSteps=0 → 저장 스킵");
                    return;
                }

                // ISO 8601 시각 → "YYYY-MM-DD" 변환 (무성님 명세 prod:date 형식)
                string dateStr = ExtractDate(parsed.endTime);
                string content = "{" +
                    $"\"count\":{parsed.totalSteps}," +
                    $"\"date\":\"{dateStr}\"" +
                    "}";

                var raw = new RawData
                {
                    Type = "step",
                    Content = content,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Processed = 0
                };
                SQLiteManager.Instance.Connection.Insert(raw);
                Debug.Log($"[HealthConnect] step 저장: count={parsed.totalSteps}, date={dateStr}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HealthConnect] steps 파싱 실패: {e.Message}\nJSON: {json}");
            }
        }

        // ─────────────────────────────────────────────────────
        // HeartRate
        // ─────────────────────────────────────────────────────

        private void ProcessHeartRateResult()
        {
            string err = HealthConnectBridge.GetError("heart_rate");
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError($"[HealthConnect] heart_rate 에러: {err}");
                return;
            }

            string json = HealthConnectBridge.GetResult("heart_rate");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[HealthConnect] heart_rate 결과 비어있음");
                return;
            }

            try
            {
                var parsed = JsonUtility.FromJson<HeartRateResultJson>(json);
                if (parsed == null || parsed.sampleCount == 0)
                {
                    Debug.Log("[HealthConnect] heart_rate samples 없음 → 저장 스킵");
                    return;
                }

                string content = "{" +
                    $"\"avgBpm\":{parsed.avgBpm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"\"sampleCount\":{parsed.sampleCount}," +
                    $"\"timestamp\":\"{DateTime.UtcNow.ToString("o")}\"" +
                    "}";

                var raw = new RawData
                {
                    Type = "heart_rate",
                    Content = content,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Processed = 0
                };
                SQLiteManager.Instance.Connection.Insert(raw);
                Debug.Log($"[HealthConnect] heart_rate 저장: avgBpm={parsed.avgBpm:F1}, samples={parsed.sampleCount}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HealthConnect] heart_rate 파싱 실패: {e.Message}\nJSON: {json}");
            }
        }

        // ─────────────────────────────────────────────────────

        /// <summary>
        /// ISO 8601 시각 ("2026-06-08T10:30:00Z") → "2026-06-08" 추출.
        /// 실패 시 오늘 날짜.
        /// </summary>
        private static string ExtractDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (iso.Length >= 10 && iso[4] == '-' && iso[7] == '-')
                return iso.Substring(0, 10);
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }
    }
}
