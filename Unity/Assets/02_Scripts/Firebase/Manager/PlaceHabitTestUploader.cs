#if UNITY_EDITOR
using System;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// [통합 테스트 임시 도구] place_habit 규칙 검증용.
/// 서로 다른 카페 loc 노드 3개(loc_cafe_001/002/003) × (placeType + hasLocation) = 6건을
/// SQLite에 삽입 후 sync. User 타입 선언 1건은 TempTripleManager가 자동 추가 → 총 7건.
/// </summary>
public class PlaceHabitTestUploader : MonoBehaviour
{
    [Tooltip("TempTripleManager 붙은 오브젝트 드래그")]
    public TempTripleManager tempTripleManager;

    private const string BaseUri = "http://7team.dev/ontology#";

    [ContextMenu("place_habit 테스트 실행 (삽입 + 업로드)")]
    public void RunTest()
    {
        InsertCafeVisitTestData();
        Upload();
    }

    public void InsertCafeVisitTestData()
    {
        var conn = SQLiteManager.Instance.Connection;

        for (int i = 1; i <= 3; i++)
        {
            string locUri = $"{BaseUri}loc_cafe_{i:000}";

            // placeType 트리플 (cafe)
            conn.Insert(new Triple
            {
                Subject = locUri,
                Predicate = "prod:placeType",
                Object = "cafe",
                Datatype = "xsd:string",
                Source = "place_habit_test",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0
            });

            // hasLocation 트리플 (user는 placeholder, 업로드 시 실제 uid로 정규화됨)
            conn.Insert(new Triple
            {
                Subject = $"{BaseUri}user_001",
                Predicate = "prod:hasLocation",
                Object = locUri,
                Datatype = null,
                Source = "place_habit_test",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0
            });
        }

        Debug.Log("[PlaceHabitTest] 카페 방문 6건 SQLite 삽입 완료 (loc_cafe_001/002/003, synced=0)");
    }

    public void Upload()
    {
        if (tempTripleManager == null)
        {
            Debug.LogError("[PlaceHabitTest] TempTripleManager 참조 없음 - Inspector에서 연결하세요");
            return;
        }

        tempTripleManager.SyncPendingTriples(
            onSuccess: n => Debug.Log($"[PlaceHabitTest] 업로드 성공: {n}건 (+ User 타입 선언 1건 = 총 {n + 1}건)"),
            onFailure: e => Debug.LogError($"[PlaceHabitTest] 업로드 실패: {e}")
        );
    }
}
#endif
