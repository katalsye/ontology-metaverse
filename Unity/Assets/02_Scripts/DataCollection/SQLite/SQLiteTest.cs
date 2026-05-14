using System;
using UnityEngine;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// SQLiteManager 동작 테스트용 스크립트
    /// 빈 GameObject에 붙여서 Play 시 자동 실행
    /// </summary>
    public class SQLiteTest : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[SQLiteTest] 시작");
            
            // 1. DB 초기화 (4개 테이블 자동 생성)
            var manager = SQLiteManager.Instance;
            Debug.Log($"[SQLiteTest] DB 경로: {manager.DbPath}");
            
            // 2. raw_data 테스트 INSERT
            var testRawData = new RawData
            {
                Type = "gps",
                Content = "{\"lat\":37.4979,\"lng\":127.0276}",
                Timestamp = DateTime.UtcNow.ToString("o")
            };
            int inserted = manager.Connection.Insert(testRawData);
            Debug.Log($"[SQLiteTest] raw_data INSERT: {inserted}건, Id={testRawData.Id}");
            
            // 3. triples 테스트 INSERT
            var testTriple = new Triple
            {
                Subject = "user",
                Predicate = "visited",
                Object = "cafe_gangnam",
                Datatype = "xsd:string",
                Source = "text_diary",
                Timestamp = DateTime.UtcNow.ToString("o")
            };
            manager.Connection.Insert(testTriple);
            Debug.Log($"[SQLiteTest] triples INSERT: Id={testTriple.Id}");
            
            // 4. raw_data SELECT 전체
            var allRawData = manager.Connection.Table<RawData>();
            int rawCount = 0;
            foreach (var row in allRawData)
            {
                Debug.Log($"[SQLiteTest] raw_data[{row.Id}]: Type={row.Type}, Timestamp={row.Timestamp}");
                rawCount++;
            }
            Debug.Log($"[SQLiteTest] raw_data 총 {rawCount}건");
            
            // 5. triples SELECT 전체
            var allTriples = manager.Connection.Table<Triple>();
            int tripleCount = 0;
            foreach (var row in allTriples)
            {
                Debug.Log($"[SQLiteTest] triples[{row.Id}]: ({row.Subject}, {row.Predicate}, {row.Object})");
                tripleCount++;
            }
            Debug.Log($"[SQLiteTest] triples 총 {tripleCount}건");
            
            Debug.Log("[SQLiteTest] 테스트 완료");
        }
    }
}