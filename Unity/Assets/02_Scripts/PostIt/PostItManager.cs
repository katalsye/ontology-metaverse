using UnityEngine;
using System.Collections.Generic;

// 유니티 Inspector 설정:
// 1. 빈 GameObject 만들고 PostItManager 이름, 이 스크립트 붙이기
// 2. postItPrefab → 03_Prefabs/postit 프리팹 연결
// 3. board        → Hierarchy의 board 오브젝트 연결
// 4. materials    → 포스트잇 머티리얼 3개 연결
// 5. maxNotes     → 최대 포스트잇 수 (기본 30)
//
// 백엔드 연동 시:
//    LoadCommentsFromServer() 주석 해제 후 API 응답을 AddComment()로 넘기면 됨

// ── 더미 데이터 구조 (백엔드 연동 전 임시) ────────────────────────────────
[System.Serializable]
public class CommentData
{
    public string username;
    public string comment;
}

public class PostItManager : MonoBehaviour
{
    [Header("프리팹")]
    public GameObject postItPrefab;

    [Header("board 오브젝트 연결")]
    public Transform board;

    [Header("포스트잇 머티리얼 3개")]
    public Material[] materials = new Material[3];

    [Header("설정")]
    public int   maxNotes            = 30;
    [Range(0f, 15f)]
    public float randomRotationRange = 6f;

    // ── 내부 ──────────────────────────────────────────────────────────────
    private List<PostItNote> _notes = new List<PostItNote>();
    private Bounds           _boardBounds;
    private bool             _boundsReady;

    // 백엔드 없을 때 사용할 더미 댓글
    static readonly CommentData[] _dummyComments = new CommentData[]
    {
        new CommentData { username = "user1",  comment = "방이 너무 예뻐요!" },
        new CommentData { username = "user2",  comment = "인테리어 대박" },
        new CommentData { username = "user3",  comment = "여기 자주 올게요 ㅎㅎ" },
        new CommentData { username = "user4",  comment = "분위기 최고입니다" },
        new CommentData { username = "user5",  comment = "어떤 가구 쓰셨어요?" },
    };

    void Start()
    {
        CacheBounds();
        LoadComments();
    }

    // ── 댓글 로드 ─────────────────────────────────────────────────────────

    void LoadComments()
    {
        // ── TODO: 백엔드 연동 시 아래 주석 해제하고 더미 블록 제거 ──────────
        // LoadCommentsFromServer();
        // ────────────────────────────────────────────────────────────────────

        // 백엔드 연동 전 더미 데이터로 포스트잇 생성
        foreach (var data in _dummyComments)
            AddComment(data.username, data.comment);
    }

    // TODO: 백엔드 연동 함수 (서버 API 완성 후 구현)
    // IEnumerator LoadCommentsFromServer()
    // {
    //     string url = "https://your-api.com/rooms/{roomId}/comments";
    //     using (UnityWebRequest req = UnityWebRequest.Get(url))
    //     {
    //         yield return req.SendWebRequest();
    //         if (req.result == UnityWebRequest.Result.Success)
    //         {
    //             var response = JsonUtility.FromJson<CommentListResponse>(req.downloadHandler.text);
    //             foreach (var data in response.comments)
    //                 AddComment(data.username, data.comment);
    //         }
    //     }
    // }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>댓글 생성 시 호출 → 랜덤 머티리얼·위치 포스트잇 생성</summary>
    public void AddComment(string username, string comment)
    {
        if (!ValidateSetup()) return;
        if (_notes.Count >= maxNotes) RemoveOldest();

        SpawnPostIt(username, comment);
    }

    /// <summary>모든 포스트잇 제거</summary>
    public void ClearAll()
    {
        foreach (var n in _notes)
            if (n != null) Destroy(n.gameObject);
        _notes.Clear();
    }

    /// <summary>PostItNote가 클릭됐을 때 호출 (댓글 패널 열기)</summary>
    public void OnPostItClicked(PostItNote note)
    {
        // TODO: 댓글 패널 UI 연동 (나중에 구현)
        // 예시) commentPanel.Open(note.Username, note.Comment);
        Debug.Log($"[PostIt] 클릭됨 — {note.Username}: {note.Comment}");
    }

    // ── 내부 ──────────────────────────────────────────────────────────────

    void SpawnPostIt(string username, string comment)
    {
        Vector3    pos = GetRandomPosition();
        Quaternion rot = GetRandomRotation();

        GameObject obj  = Instantiate(postItPrefab, pos, rot, board);
        PostItNote note = obj.GetComponent<PostItNote>();
        if (note == null) note = obj.AddComponent<PostItNote>();

        // 랜덤 머티리얼 선택
        Material mat = materials[Random.Range(0, materials.Length)];
        note.Setup(username, comment, mat, this);

        _notes.Add(note);
    }

    // board 의 Collider → Renderer 순으로 바운드 캐시
    void CacheBounds()
    {
        if (board == null) return;

        var col = board.GetComponentInChildren<Collider>();
        if (col != null) { _boardBounds = col.bounds; _boundsReady = true; return; }

        var ren = board.GetComponentInChildren<Renderer>();
        if (ren != null) { _boardBounds = ren.bounds; _boundsReady = true; }
    }

    Vector3 GetRandomPosition()
    {
        if (!_boundsReady) CacheBounds();

        // board 로컬 XY 평면 내 랜덤 (약간 여백)
        float rx = Random.Range(-0.5f, 0.5f) * _boardBounds.size.x * 0.8f;
        float ry = Random.Range(-0.5f, 0.5f) * _boardBounds.size.y * 0.8f;

        // board 앞면(forward)에 딱 붙임
        return _boardBounds.center
             + board.right   * rx
             + board.up      * ry
             + board.forward * (_boardBounds.size.z * 0.5f + 0.002f);
    }

    Quaternion GetRandomRotation()
    {
        // board 방향 기준 + 랜덤 Z 기울기
        float zRot = Random.Range(-randomRotationRange, randomRotationRange);
        return board.rotation * Quaternion.Euler(0f, 0f, zRot);
    }

    void RemoveOldest()
    {
        if (_notes.Count == 0) return;
        var oldest = _notes[0];
        _notes.RemoveAt(0);
        if (oldest != null) Destroy(oldest.gameObject);
    }

    bool ValidateSetup()
    {
        if (postItPrefab == null) { Debug.LogWarning("[PostItManager] postItPrefab 미연결"); return false; }
        if (board        == null) { Debug.LogWarning("[PostItManager] board 미연결");        return false; }
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning("[PostItManager] materials 비어있음");
            return false;
        }
        return true;
    }
}
