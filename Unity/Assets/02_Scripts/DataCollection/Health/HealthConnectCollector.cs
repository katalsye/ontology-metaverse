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
        /// Health Connect가 걸음수를 한 번이라도 제공했는지.
        /// BatchScheduler가 이 플래그로 센서 기반 StepCollector 이중 수집을 차단.
        /// (센서는 "부팅 후 누적값"이라 Rule 3/P1의 일별 임계값과 안 맞음)
        /// </summary>
        public bool HasProvidedSteps { get; private set; }

        /// <summary>
        /// 현재 4개 권한(SLEEP/STEPS/HEART_RATE/HRV)이 모두 부여됐는지.
        /// BatchScheduler가 매 cycle 권한 체크해서 미허용이면 수집 자체를 스킵.
        /// </summary>
        public bool HasPermission() => HealthConnectBridge.HasAllPermissions();

        /// <summary>
        /// 초기화 단계에서 권한만 받는 메서드. BatchScheduler.Start()가 직렬로 호출.
        /// Spotify OAuth · Calendar 권한 팝업과 같은 시점에 띄우지 않게 분리.
        ///
        /// 동작:
        ///   - SDK Unavailable → 즉시 yield break (수집기 통째로 비활성)
        ///   - 이미 권한 있음 → 즉시 yield break
        ///   - 권한 없음 → Health Connect 앱 띄우고 사용자가 부여하고 돌아올 때까지 polling (최대 maxWaitSeconds)
        ///   - 타임아웃 → 경고만 남기고 진행 (다음 cycle에서 CollectAll이 재시도)
        /// </summary>
        public IEnumerator EnsurePermissionInteractive()
        {
            var status = HealthConnectBridge.GetSdkStatus();
            if (status != HealthConnectSdkStatus.Available)
            {
                Debug.LogWarning($"[HealthConnect] SDK 사용 불가: {status} → 권한 요청 스킵");
                if (status == HealthConnectSdkStatus.ProviderUpdateRequired)
                    HealthConnectBridge.OpenHealthConnectSettings();
                yield break;
            }

            if (HealthConnectBridge.HasAllPermissions())
            {
                Debug.Log("[HealthConnect] 권한 이미 있음 → 요청 스킵");
                yield break;
            }

            Debug.Log("[HealthConnect] 설정 화면 띄움 — 사용자 권한 부여 대기");
            HealthConnectBridge.OpenHealthConnectSettings();
            _settingsOpened = true;

            const float maxWaitSeconds = 60f;
            const float pollIntervalSeconds = 2f;
            float elapsed = 0f;
            while (elapsed < maxWaitSeconds)
            {
                yield return new WaitForSeconds(pollIntervalSeconds);
                elapsed += pollIntervalSeconds;
                if (HealthConnectBridge.HasAllPermissions())
                {
                    Debug.Log($"[HealthConnect] 권한 부여 확인됨 ({elapsed:F0}s)");
                    yield break;
                }
            }
            Debug.LogWarning($"[HealthConnect] {maxWaitSeconds:F0}s 내 권한 부여 안 됨 → 다음 cycle 재시도");
        }

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

            // 4. Sleep / Steps / HeartRate / HRV 병렬 요청
            HealthConnectBridge.RequestRead("sleep", startMillis, endMillis);
            HealthConnectBridge.RequestRead("steps", startMillis, endMillis);
            HealthConnectBridge.RequestRead("heart_rate", startMillis, endMillis);
            HealthConnectBridge.RequestRead("hrv", startMillis, endMillis);

            // 5. 폴링으로 4개 다 끝날 때까지 대기
            float elapsed = 0;
            while (elapsed < pollTimeoutSeconds)
            {
                if (HealthConnectBridge.IsReady("sleep")
                    && HealthConnectBridge.IsReady("steps")
                    && HealthConnectBridge.IsReady("heart_rate")
                    && HealthConnectBridge.IsReady("hrv"))
                    break;
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            // 6. 각각 결과 처리
            ProcessSleepResult();
            ProcessStepsResult();
            ProcessHeartRateResult();
            ProcessHrvResult();
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
                HasProvidedSteps = true; // 센서 StepCollector 이중 수집 차단용
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
        // HRV (RMSSD) — 옵션 (갤럭시워치 등 일부 기기만 기록)
        // ─────────────────────────────────────────────────────

        private void ProcessHrvResult()
        {
            string err = HealthConnectBridge.GetError("hrv");
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogWarning($"[HealthConnect] hrv: {err}"); // HRV는 옵션 — 에러여도 경고만
                return;
            }

            string json = HealthConnectBridge.GetResult("hrv");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var parsed = JsonUtility.FromJson<HrvResultJson>(json);
                if (parsed == null || parsed.sampleCount == 0)
                {
                    Debug.Log("[HealthConnect] hrv 샘플 없음 → 저장 스킵 (기기 미지원 가능)");
                    return;
                }

                string content = "{" +
                    $"\"avgRmssd\":{parsed.avgRmssd.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"\"sampleCount\":{parsed.sampleCount}," +
                    $"\"timestamp\":\"{DateTime.UtcNow.ToString("o")}\"" +
                    "}";

                var raw = new RawData
                {
                    Type = "hrv",
                    Content = content,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Processed = 0
                };
                SQLiteManager.Instance.Connection.Insert(raw);
                Debug.Log($"[HealthConnect] hrv 저장: avgRmssd={parsed.avgRmssd:F1}ms, samples={parsed.sampleCount}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HealthConnect] hrv 파싱 실패: {e.Message}\nJSON: {json}");
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
