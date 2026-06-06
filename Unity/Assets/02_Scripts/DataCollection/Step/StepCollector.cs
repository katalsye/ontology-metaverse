using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Step
{
    /// <summary>
    /// Android TYPE_STEP_COUNTER 센서 기반 걸음수 수집기.
    ///
    /// 동작:
    ///   1. ACTIVITY_RECOGNITION 권한 요청 (Android 10+).
    ///   2. Plugins/Android/StepHelper.java 호출하여 센서 등록.
    ///   3. CollectCurrentSteps() 호출 시점에 누적 카운트 읽어서 SQLite raw_data 저장.
    ///
    /// 주의:
    ///   - 센서가 반환하는 값은 "디바이스 부팅 후 누적 걸음 수". 일별 차이는 추론 단계에서 계산.
    ///   - 일부 디바이스는 step counter 미지원 → start() false 반환 → 수집 스킵.
    ///   - 센서 첫 데이터가 올 때까지 시간 걸릴 수 있음 (초기 -1 반환 가능).
    /// </summary>
    public class StepCollector : MonoBehaviour
    {
        [Header("센서 등록 대기 최대 시간 (초)")]
        public int maxInitWaitSeconds = 15;

        public bool IsSensorAvailable { get; private set; }

        /// <summary>
        /// 권한 요청 + 센서 등록 + 첫 데이터 대기. 코루틴으로 호출.
        /// </summary>
        public IEnumerator StartCollection()
        {
            Debug.Log("[StepCollector] 시작");

#if UNITY_ANDROID
            // 1. ACTIVITY_RECOGNITION 권한 (Android 10+ 필수)
            const string activityPermission = "android.permission.ACTIVITY_RECOGNITION";
            if (!Permission.HasUserAuthorizedPermission(activityPermission))
            {
                Debug.Log("[StepCollector] ACTIVITY_RECOGNITION 권한 요청");
                Permission.RequestUserPermission(activityPermission);

                float waitTime = 0;
                while (!Permission.HasUserAuthorizedPermission(activityPermission) && waitTime < 10f)
                {
                    yield return new WaitForSeconds(0.5f);
                    waitTime += 0.5f;
                }

                if (!Permission.HasUserAuthorizedPermission(activityPermission))
                {
                    Debug.LogWarning("[StepCollector] ACTIVITY_RECOGNITION 권한 거부됨 → 수집 중단");
                    yield break;
                }
            }
#endif

            // 2. Java 헬퍼 통해 센서 등록
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var stepHelper = new AndroidJavaClass("com.ontology.metaverse.step.StepHelper"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                bool ok = stepHelper.CallStatic<bool>("start", activity);
                IsSensorAvailable = ok;
                if (!ok)
                {
                    Debug.LogWarning("[StepCollector] 센서 등록 실패 (디바이스에 step counter 없을 수 있음)");
                    yield break;
                }
                Debug.Log("[StepCollector] 센서 등록 성공, 첫 데이터 대기...");
            }
#else
            IsSensorAvailable = true;
            Debug.Log("[StepCollector] Editor mode (mock)");
#endif

            // 3. 첫 데이터 들어올 때까지 대기 (사용자가 걸어야 발생)
            //    이 단계에서 못 받아도 OK — 나중에 CollectCurrentSteps()가 polling 식으로 동작.
            int wait = 0;
            while (wait < maxInitWaitSeconds)
            {
                int c = ReadSensorCount();
                if (c >= 0)
                {
                    Debug.Log($"[StepCollector] 첫 카운트 수신: {c}");
                    break;
                }
                yield return new WaitForSeconds(1);
                wait++;
            }
        }

        /// <summary>
        /// 현재 누적 걸음수를 SQLite raw_data에 1건 저장.
        /// BatchScheduler가 주기적으로 호출.
        /// </summary>
        public void CollectCurrentSteps()
        {
            if (!IsSensorAvailable)
            {
                Debug.LogWarning("[StepCollector] 센서 미사용 가능 → 수집 스킵");
                return;
            }

            int count = ReadSensorCount();
            if (count < 0)
            {
                Debug.LogWarning("[StepCollector] 아직 센서 데이터 없음 → 스킵");
                return;
            }

            string content = $"{{\"count\":{count},\"date\":\"{DateTime.Now:yyyy-MM-dd}\"}}";
            var rawData = new RawData
            {
                Type = "step",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Processed = 0
            };
            SQLiteManager.Instance.Connection.Insert(rawData);
            Debug.Log($"[StepCollector] 저장: Id={rawData.Id}, 누적 {count}걸음");
        }

        /// <summary>
        /// Java StepHelper에서 마지막 카운트 읽기.
        /// </summary>
        private int ReadSensorCount()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var stepHelper = new AndroidJavaClass("com.ontology.metaverse.step.StepHelper"))
            {
                return stepHelper.CallStatic<int>("getCurrentCount");
            }
#else
            // Editor mock: 임의의 누적 카운트
            return UnityEngine.Random.Range(2000, 8000);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var stepHelper = new AndroidJavaClass("com.ontology.metaverse.step.StepHelper"))
                {
                    stepHelper.CallStatic("stop");
                }
            }
            catch (Exception e) { Debug.LogError($"[StepCollector] stop 실패: {e.Message}"); }
#endif
        }
    }
}
