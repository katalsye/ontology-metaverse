using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Location
{
    /// <summary>
    /// LocationCollector 동작 테스트용 스크립트
    /// 빈 GameObject에 붙여서 Play 시 자동 실행
    /// </summary>
    public class LocationTest : MonoBehaviour
    {
        private LocationCollector _collector;

        IEnumerator Start()
        {
            Debug.Log("[LocationTest] 시작");

            // 1. LocationCollector 컴포넌트 추가
            _collector = gameObject.AddComponent<LocationCollector>();

            // 2. LocationService 시작 (코루틴)
            yield return StartCoroutine(_collector.StartLocationService());

            // 3. 5초마다 한 번씩 위치 추가 수집 (총 3회)
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(5);
                Debug.Log($"[LocationTest] 추가 수집 시도 {i + 1}/3");
                _collector.CollectCurrentLocation();
            }

            // 4. SQLite raw_data에 저장된 GPS 데이터 전체 출력
            Debug.Log("[LocationTest] === SQLite raw_data 조회 ===");
            var manager = SQLiteManager.Instance;
            var allGps = manager.Connection.Table<RawData>().Where(r => r.Type == "gps");
            int count = 0;
            foreach (var row in allGps)
            {
                Debug.Log($"[LocationTest] raw_data[{row.Id}]: " +
                          $"Type={row.Type}, Content={row.Content}, Timestamp={row.Timestamp}");
                count++;
            }
            Debug.Log($"[LocationTest] GPS 총 {count}건 저장됨");

            // 5. 정리
            _collector.StopLocationService();
            Debug.Log("[LocationTest] 테스트 완료");
        }
    }
}