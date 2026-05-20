using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// SQLite triples 테이블의 미전송 트리플(synced=0)을
/// Firestore temp_triples 컬렉션으로 업로드하는 sync 매니저
/// 
/// 업로드 경로: temp_triples/{uid}/items/{auto_id}
/// 명세서: Docs/triple-json-spec.md 규격 준수
/// </summary>
public class TempTripleManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    /// <summary>
    /// SQLite에서 미전송 트리플(synced=0)을 가져와 Firestore에 업로드
    /// 업로드 성공 시 SQLite synced=1로 업데이트
    /// </summary>
    /// <param name="onSuccess">성공 콜백 (업로드된 건수 전달)</param>
    /// <param name="onFailure">실패 콜백 (에러 메시지 전달)</param>
    public void SyncPendingTriples(
        Action<int> onSuccess = null,
        Action<string> onFailure = null)
    {
        // 1. 로그인 상태 확인
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("[TempTripleManager] 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        Debug.Log($"[TempTripleManager] Sync 시작 (uid: {uid})");

        // 2. SQLite에서 미전송 트리플 조회
        List<Triple> pendingTriples = GetPendingTriplesFromSQLite();

        if (pendingTriples.Count == 0)
        {
            Debug.Log("[TempTripleManager] 업로드할 트리플 없음");
            onSuccess?.Invoke(0);
            return;
        }

        Debug.Log($"[TempTripleManager] 미전송 트리플 {pendingTriples.Count}개 발견");

        // 3. 각 트리플을 Firestore에 업로드
        UploadTriples(uid, pendingTriples, onSuccess, onFailure);
    }

    /// <summary>
    /// SQLite triples 테이블에서 synced=0인 트리플들 조회
    /// </summary>
    private List<Triple> GetPendingTriplesFromSQLite()
    {
        var manager = SQLiteManager.Instance;
        var query = manager.Connection.Table<Triple>().Where(t => t.Synced == 0);

        List<Triple> result = new List<Triple>();
        foreach (var t in query)
        {
            result.Add(t);
        }
        return result;
    }

    /// <summary>
    /// 트리플 리스트를 Firestore에 업로드
    /// 각 업로드 성공 시 해당 트리플의 SQLite synced 플래그를 1로 업데이트
    /// </summary>
    private void UploadTriples(
        string uid,
        List<Triple> triples,
        Action<int> onSuccess,
        Action<string> onFailure)
    {
#if UNITY_EDITOR
        // Editor에서는 Mock 모드: 실제 Firestore 호출 없이 SQLite synced=1만 업데이트
        Debug.Log("[TempTripleManager] Editor mock 모드: 실제 Firestore 업로드 생략");

        int mockSuccessCount = 0;
        var manager = SQLiteManager.Instance;
        foreach (var triple in triples)
        {
            // Mock: 업로드 성공으로 가정하고 synced 플래그 업데이트
            triple.Synced = 1;
            manager.Connection.Update(triple);
            mockSuccessCount++;
            Debug.Log($"[TempTripleManager] Mock 업로드: Id={triple.Id}, " +
                      $"({triple.Subject}, {triple.Predicate}, {triple.Object})");
        }

        Debug.Log($"[TempTripleManager] Mock 업로드 완료: {mockSuccessCount}건");
        onSuccess?.Invoke(mockSuccessCount);
#else
        // 실기기에서는 진짜 Firestore 업로드
        int successCount = 0;
        int failureCount = 0;
        int total = triples.Count;

        foreach (var triple in triples)
        {
            // SQLite Triple → Firestore TempTriple 변환
            TempTriple tempTriple = new TempTriple
            {
                s = triple.Subject,
                p = triple.Predicate,
                o = triple.Object,
                datatype = triple.Datatype
                // CreatedAt은 SetAsync에서 ServerTimestamp로 자동 처리됨
            };

            // Firestore temp_triples/{uid}/items/ 에 추가 (auto_id)
            DocumentReference docRef = db.Collection("temp_triples")
                .Document(uid)
                .Collection("items")
                .Document();  // auto_id

            int currentId = triple.Id;  // 클로저용 저장

            docRef.SetAsync(tempTriple).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[TempTripleManager] 업로드 실패 (Id={currentId}): {task.Exception}");
                    failureCount++;
                }
                else
                {
                    // 업로드 성공 → SQLite synced=1 업데이트
                    UpdateSyncedFlag(currentId);
                    successCount++;
                    Debug.Log($"[TempTripleManager] 업로드 성공 (Id={currentId})");
                }

                // 모든 업로드 완료 체크
                if (successCount + failureCount == total)
                {
                    Debug.Log($"[TempTripleManager] Sync 완료: 성공 {successCount}건 / 실패 {failureCount}건");
                    if (failureCount == 0)
                    {
                        onSuccess?.Invoke(successCount);
                    }
                    else
                    {
                        onFailure?.Invoke($"일부 실패: 성공 {successCount} / 실패 {failureCount}");
                    }
                }
            });
        }
#endif
    }

    /// <summary>
    /// SQLite triples 테이블의 특정 트리플 synced 플래그를 1로 업데이트
    /// </summary>
    private void UpdateSyncedFlag(int tripleId)
    {
        var manager = SQLiteManager.Instance;
        var triple = manager.Connection.Find<Triple>(tripleId);
        if (triple != null)
        {
            triple.Synced = 1;
            manager.Connection.Update(triple);
        }
    }
}