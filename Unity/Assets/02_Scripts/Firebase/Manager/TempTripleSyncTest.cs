#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// TempTripleManager 동작 테스트
/// 1. 테스트용 트리플을 SQLite에 INSERT (synced=0)
/// 2. SyncPendingTriples 호출 → Firestore 실제 업로드
/// 3. SQLite에서 synced=1로 바뀌었는지 확인
/// 
/// 무성님 명세 반영: full URI 형식 + prod:hasLocation 사용
/// </summary>
public class TempTripleSyncTest : MonoBehaviour
{
    private TempTripleManager _syncManager;

    // 온톨로지 base URI (무성님 명세)
    private const string OntologyBaseUri = "http://7team.dev/ontology#";

    IEnumerator Start()
    {
        Debug.Log("[TempTripleSyncTest] === Sync 테스트 시작 ===");

        // 1. TempTripleManager 컴포넌트 추가
        _syncManager = gameObject.AddComponent<TempTripleManager>();

        // FirebaseAuth, FirebaseFirestore 초기화 대기 (Start가 실행될 시간)
        yield return new WaitForSeconds(0.5f);

        // 2. 테스트용 트리플을 SQLite에 미리 INSERT (synced=0 상태)
        InsertTestTriples();

        // 3. 동기화 전 SQLite 상태 출력
        PrintSyncStatus("동기화 전");

        // 4. SyncPendingTriples 호출 (실제 Firestore 업로드)
        Debug.Log("[TempTripleSyncTest] SyncPendingTriples 호출");
        _syncManager.SyncPendingTriples(
            onSuccess: count =>
            {
                Debug.Log($"[TempTripleSyncTest] Sync 성공: {count}건 업로드");
            },
            onFailure: error =>
            {
                Debug.LogError($"[TempTripleSyncTest] Sync 실패: {error}");
            }
        );

        // 5. 비동기 처리 대기 (Anonymous Auth + Firestore 업로드 시간 고려)
        yield return new WaitForSeconds(3.0f);

        // 6. 동기화 후 SQLite 상태 출력
        PrintSyncStatus("동기화 후");

        Debug.Log("[TempTripleSyncTest] === 테스트 완료 ===");
    }

    /// <summary>
    /// 테스트용 트리플 3건을 SQLite에 INSERT (무성님 명세 형식)
    /// </summary>
    private void InsertTestTriples()
    {
        var manager = SQLiteManager.Instance;
        string now = System.DateTime.UtcNow.ToString("o");

        // 무성님 명세 형식: full URI + prod:hasLocation
        string userUri = OntologyBaseUri + "user_001";
        string locUri = OntologyBaseUri + "loc_001_sync_test";

        var testTriples = new[]
        {
            new Triple
            {
                Subject = userUri,
                Predicate = "prod:hasLocation",   // 무성님 정의 predicate
                Object = locUri,
                Datatype = null,
                Source = "sync_test:case1",
                Timestamp = now,
                Synced = 0
            },
            new Triple
            {
                Subject = locUri,
                Predicate = "prod:placeName",
                Object = "Sync Test Cafe",
                Datatype = "xsd:string",
                Source = "sync_test:case1",
                Timestamp = now,
                Synced = 0
            },
            new Triple
            {
                Subject = locUri,
                Predicate = "prod:placeType",
                Object = "cafe",
                Datatype = "xsd:string",
                Source = "sync_test:case1",
                Timestamp = now,
                Synced = 0
            }
        };

        foreach (var t in testTriples)
        {
            manager.Connection.Insert(t);
            Debug.Log($"[TempTripleSyncTest] 테스트 트리플 INSERT: Id={t.Id}, ({t.Subject}, {t.Predicate}, {t.Object})");
        }

        Debug.Log($"[TempTripleSyncTest] 테스트 트리플 {testTriples.Length}건 INSERT 완료");
    }

    /// <summary>
    /// SQLite triples 테이블의 sync 상태 출력
    /// sync_test 출처의 트리플만 필터링
    /// </summary>
    private void PrintSyncStatus(string label)
    {
        Debug.Log($"[TempTripleSyncTest] --- {label} (sync_test 트리플만) ---");

        var manager = SQLiteManager.Instance;
        var query = manager.Connection.Table<Triple>()
            .Where(t => t.Source.StartsWith("sync_test:"));

        int pending = 0;
        int synced = 0;
        foreach (var t in query)
        {
            string status = (t.Synced == 1) ? "✓ Synced" : "○ Pending";
            Debug.Log($"[TempTripleSyncTest] {status} Id={t.Id}: ({t.Subject}, {t.Predicate}, {t.Object})");
            if (t.Synced == 1) synced++;
            else pending++;
        }
        Debug.Log($"[TempTripleSyncTest] {label}: Pending={pending}, Synced={synced}");
    }
}
#endif
