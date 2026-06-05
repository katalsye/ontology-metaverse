using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Location
{
    /// <summary>
    /// Unity LocationService를 활용한 GPS 위치 수집기
    /// 권한 요청 → 서비스 시작 → 좌표 획득 → SQLite raw_data 저장
    /// </summary>
    public class LocationCollector : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("위치 정확도 (미터). 작을수록 정확하지만 배터리 소모 큼.")]
        public float desiredAccuracyInMeters = 10f;

        [Tooltip("위치 업데이트 거리 임계값 (미터). 이 거리 이상 이동 시 업데이트.")]
        public float updateDistanceInMeters = 10f;

        [Tooltip("LocationService 초기화 대기 최대 시간 (초)")]
        public int maxInitWaitSeconds = 20;

        /// <summary>
        /// 권한 요청 + LocationService 시작
        /// </summary>
        public IEnumerator StartLocationService()
        {
            Debug.Log("[LocationCollector] 시작");

            // 1. Android 권한 확인 및 요청
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log("[LocationCollector] FineLocation 권한 요청");
                Permission.RequestUserPermission(Permission.FineLocation);

                // 권한 응답 대기 (최대 10초)
                float waitTime = 0;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && waitTime < 10f)
                {
                    yield return new WaitForSeconds(0.5f);
                    waitTime += 0.5f;
                }

                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    Debug.LogWarning("[LocationCollector] FineLocation 권한 거부됨. 수집 중단.");
                    yield break;
                }
            }
#endif

            // 2. 사용자가 시스템 설정에서 위치 서비스 켜놓았는지 확인
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[LocationCollector] 사용자가 위치 서비스를 비활성화함.");
                yield break;
            }

            // 3. LocationService 시작
            Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            Debug.Log("[LocationCollector] LocationService Start 호출");

            // 4. 초기화 대기 (Initializing → Running)
            int waitCount = 0;
            while (Input.location.status == LocationServiceStatus.Initializing && waitCount < maxInitWaitSeconds)
            {
                yield return new WaitForSeconds(1);
                waitCount++;
            }

            // 5. 결과 분기
            if (waitCount >= maxInitWaitSeconds)
            {
                Debug.LogError("[LocationCollector] 초기화 타임아웃");
                yield break;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                Debug.LogError("[LocationCollector] LocationService 초기화 실패");
                yield break;
            }

            // 6. 정상 시작
            Debug.Log($"[LocationCollector] LocationService Running. " +
                      $"Status: {Input.location.status}");
            
            // 7. 첫 위치 1회 수집
            CollectCurrentLocation();
        }

        /// <summary>
        /// 현재 위치 1회 수집 후 SQLite raw_data에 저장
        /// </summary>
        public void CollectCurrentLocation()
        {
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning($"[LocationCollector] LocationService가 Running 상태가 아님. " +
                                 $"현재: {Input.location.status}");
                return;
            }

            var data = Input.location.lastData;
            float lat = data.latitude;
            float lng = data.longitude;
            float alt = data.altitude;
            float accuracy = data.horizontalAccuracy;
            double rawTimestamp = data.timestamp; // Unix epoch (초)

            // JSON 형태로 직렬화 (Content 필드)
            string content = $"{{\"lat\":{lat},\"lng\":{lng},\"alt\":{alt},\"accuracy\":{accuracy}}}";

            // SQLite raw_data 저장
            var rawData = new RawData
            {
                Type = "gps",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            SQLiteManager.Instance.Connection.Insert(rawData);

            Debug.Log($"[LocationCollector] GPS 저장: Id={rawData.Id}, " +
                      $"lat={lat}, lng={lng}, accuracy={accuracy}m");
        }

        /// <summary>
        /// LocationService 정지
        /// </summary>
        public void StopLocationService()
        {
            Input.location.Stop();
            Debug.Log("[LocationCollector] LocationService 정지");
        }

        private void OnDisable()
        {
            // 컴포넌트 비활성화 시 자동 정리
            if (Input.location.status == LocationServiceStatus.Running)
            {
                StopLocationService();
            }
        }
    }
}