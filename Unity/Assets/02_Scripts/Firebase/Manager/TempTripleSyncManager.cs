using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// SQLite triples 테이블(Synced=0)을 Firestore temp_triples/{uid}/triples에 일괄 업로드.
/// 업로드 성공 시 SQLite Synced=1 처리.
/// 앱이 백그라운드로 전환될 때 자동 실행, 또는 외부에서 SyncUnsyncedTriples() 직접 호출.
/// </summary>
public class TempTripleSyncManager : MonoBehaviour
{
    public static TempTripleSyncManager Instance { get; private set; }

    // Firestore WriteBatch 최대 500 operations
    private const int BATCH_LIMIT = 500;

    private FirebaseAuth _auth;
    private FirebaseFirestore _db;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _auth = FirebaseAuth.DefaultInstance;
        _db   = FirebaseFirestore.DefaultInstance;
    }

    // 앱이 백그라운드로 전환되는 시점 = 자연스러운 batch 트리거
    void OnApplicationPause(bool isPaused)
    {
        if (isPaused) SyncUnsyncedTriples();
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 미동기화 트리플을 Firestore에 업로드한다.
    /// onComplete(업로드 개수) / onFailure(에러 메시지) 콜백은 선택.
    /// </summary>
    public void SyncUnsyncedTriples(Action<int> onComplete = null, Action<string> onFailure = null)
    {
        if (_auth?.CurrentUser == null)
        {
            Debug.LogWarning("[TempTripleSync] 로그인 상태 아님 — 동기화 건너뜀");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        List<Triple> unsynced;
        try
        {
            unsynced = SQLiteManager.Instance.Connection
                .Table<Triple>()
                .Where(t => t.Synced == 0)
                .ToList();
        }
        catch (Exception e)
        {
            Debug.LogError("[TempTripleSync] SQLite 읽기 실패: " + e.Message);
            onFailure?.Invoke(e.Message);
            return;
        }

        if (unsynced.Count == 0)
        {
            Debug.Log("[TempTripleSync] 동기화할 트리플 없음");
            onComplete?.Invoke(0);
            return;
        }

        Debug.Log($"[TempTripleSync] 동기화 시작: {unsynced.Count}개");

        string uid    = _auth.CurrentUser.UserId;
        var    colRef = _db.Collection("temp_triples").Document(uid).Collection("triples");

        UploadChunk(unsynced, colRef, offset: 0, onComplete, onFailure);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void UploadChunk(
        List<Triple>         all,
        CollectionReference  colRef,
        int                  offset,
        Action<int>          onComplete,
        Action<string>       onFailure)
    {
        int end   = Math.Min(offset + BATCH_LIMIT, all.Count);
        var chunk = all.GetRange(offset, end - offset);

        var batch = _db.StartBatch();
        foreach (var t in chunk)
        {
            batch.Set(colRef.Document(), new Dictionary<string, object>
            {
                { "Subject",   t.Subject },
                { "Predicate", t.Predicate },
                { "Object",    t.Object },
                { "Datatype",  t.Datatype ?? "" },
                { "Source",    t.Source },
                { "Timestamp", t.Timestamp },
                { "CreatedAt", FieldValue.ServerTimestamp }
            });
        }

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("[TempTripleSync] 업로드 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            // 업로드 성공한 트리플만 Synced = 1
            foreach (var t in chunk)
            {
                t.Synced = 1;
                SQLiteManager.Instance.Connection.Update(t);
            }
            Debug.Log($"[TempTripleSync] {end}/{all.Count}개 처리 완료");

            // 500개 초과분이 남아 있으면 재귀 호출
            if (end < all.Count)
                UploadChunk(all, colRef, end, onComplete, onFailure);
            else
                onComplete?.Invoke(all.Count);
        });
    }
}
