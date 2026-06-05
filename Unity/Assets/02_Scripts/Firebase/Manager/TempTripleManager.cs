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
/// 
/// 인증:
/// - Editor: Anonymous Auth (이서윤님 가이드)
/// - 실기기: GoogleFirebaseLogin 모듈 활용
/// </summary>
public class TempTripleManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    // 온톨로지 base URI (명세서 §2)
    private const string OntologyBaseUri = "http://7team.dev/ontology#";

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    /// <summary>
    /// SQLite에서 미전송 트리플(synced=0)을 가져와 Firestore에 업로드
    /// 업로드 성공 시 SQLite synced=1로 업데이트
    /// </summary>
    public void SyncPendingTriples(
        Action<int> onSuccess = null,
        Action<string> onFailure = null)
    {
        // 1. 로그인 상태 확인 (없으면 Editor는 Anonymous Auth로 자동 로그인)
        if (auth?.CurrentUser == null)
        {
#if UNITY_EDITOR
            // Editor: Anonymous Auth 자동 로그인 시도
            Debug.Log("[TempTripleManager] 로그인 없음 → Anonymous Auth 시도");
            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[TempTripleManager] Anonymous 로그인 실패: {task.Exception}");
                    onFailure?.Invoke("Anonymous 로그인 실패");
                    return;
                }

                string uid = task.Result.User.UserId;
                Debug.Log($"[TempTripleManager] Anonymous 로그인 성공: uid={uid}");

                // 로그인 후 실제 sync 진행
                StartSync(uid, onSuccess, onFailure);
            });
            return;
#else
            // 실기기: GoogleFirebaseLogin이 먼저 호출되어야 함
            Debug.LogWarning("[TempTripleManager] 로그인 안 됨 - GoogleFirebaseLogin 필요");
            onFailure?.Invoke("로그인 필요");
            return;
#endif
        }

        // 이미 로그인 되어 있으면 바로 sync 진행
        string currentUid = auth.CurrentUser.UserId;
        StartSync(currentUid, onSuccess, onFailure);
    }

    /// <summary>
    /// 로그인 확인 후 실제 sync 흐름 시작
    /// </summary>
    private void StartSync(string uid, Action<int> onSuccess, Action<string> onFailure)
    {
        Debug.Log($"[TempTripleManager] Sync 시작 (uid: {uid})");

        // SQLite에서 미전송 트리플 조회
        List<Triple> pendingTriples = GetPendingTriplesFromSQLite();

        if (pendingTriples.Count == 0)
        {
            Debug.Log("[TempTripleManager] 업로드할 트리플 없음");
            onSuccess?.Invoke(0);
            return;
        }

        Debug.Log($"[TempTripleManager] 미전송 트리플 {pendingTriples.Count}개 발견");

        // User 타입 선언 먼저 (추론 규칙 전제조건, 명세 필수 트리플)
        UploadUserTypeDeclaration(uid);

        // Firestore에 업로드
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
    /// 
    /// CreatedAt은 FieldValue.ServerTimestamp로 서버 시간 자동 설정
    /// (Timestamp 기본값이 1970년이라 별도 처리 필요)
    /// user_* 노드는 NormalizeUserUri로 실제 uid 기준 통일
    /// </summary>
    private void UploadTriples(
        string uid,
        List<Triple> triples,
        Action<int> onSuccess,
        Action<string> onFailure)
    {
        int successCount = 0;
        int failureCount = 0;
        int total = triples.Count;

        foreach (var triple in triples)
        {
            // Firestore temp_triples/{uid}/items/ 에 추가 (auto_id)
            DocumentReference docRef = db.Collection("temp_triples")
                .Document(uid)
                .Collection("items")
                .Document();  // auto_id

            int currentId = triple.Id;  // 클로저용 저장

            // Dictionary로 데이터 구성 (ServerTimestamp 사용을 위해)
            // 명세서: Docs/triple-json-spec.md 규격
            // subject/object의 user_* 노드는 실제 uid로 정규화
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "subject", NormalizeUserUri(triple.Subject, uid) },
                { "predicate", triple.Predicate },
                { "object", NormalizeUserUri(triple.Object, uid) },
                { "datatype", triple.Datatype },
                { "CreatedAt", FieldValue.ServerTimestamp }
            };

            docRef.SetAsync(data).ContinueWithOnMainThread(task =>
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
    }

    /// <summary>
    /// user_* 로 시작하는 노드 URI를 실제 로그인 uid 기준으로 통일.
    /// (추출 단계의 user_001 하드코딩 / 중복 prefix 등을 흡수)
    /// </summary>
    private string NormalizeUserUri(string uri, string realUid)
    {
        if (!string.IsNullOrEmpty(uri) && uri.StartsWith(OntologyBaseUri + "user_"))
        {
            return OntologyBaseUri + "user_" + realUid;
        }
        return uri;
    }

    /// <summary>
    /// user_{uid}가 prod:User(사람)임을 선언하는 트리플 업로드.
    /// 무성님 추론 규칙(?user a prod:User)의 전제조건이라 모든 배치에 포함.
    /// </summary>
    private void UploadUserTypeDeclaration(string uid)
    {
        string userUri = OntologyBaseUri + "user_" + uid;

        DocumentReference docRef = db.Collection("temp_triples")
            .Document(uid)
            .Collection("items")
            .Document();  // auto_id

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "subject", userUri },
            { "predicate", "rdf:type" },
            { "object", OntologyBaseUri + "User" },
            { "datatype", null },
            { "CreatedAt", FieldValue.ServerTimestamp }
        };

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[TempTripleManager] User 타입 선언 업로드 실패: {task.Exception}");
            }
            else
            {
                Debug.Log($"[TempTripleManager] User 타입 선언 업로드 성공: {userUri}");
            }
        });
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

    /// <summary>
    /// 앱 종료 시 Anonymous Auth 세션 정리 (이서윤님 가이드 옵션 B)
    /// Editor에서 Play 정지 시 호출됨
    /// 같은 세션 내에서는 uid 유지하다가 종료 시 정리
    /// </summary>
    void OnDestroy()
    {
#if UNITY_EDITOR
        // Editor: Anonymous Auth 세션 정리
        if (auth != null && auth.CurrentUser != null)
        {
            Debug.Log("[TempTripleManager] OnDestroy: Anonymous Auth 세션 정리 (SignOut)");
            auth.SignOut();
        }
#endif
    }
}