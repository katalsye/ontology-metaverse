using UnityEngine;
using System.Collections.Generic;

// 유니티 Inspector 설정:
//   postItPrefab → 03_Prefabs/postit 프리팹 연결
//   board        → Hierarchy의 board 오브젝트 연결
//   materials    → 포스트잇 머티리얼 3개 연결
//
// 포스트잇은 프리팹을 그대로 Instantiate해서 사용 — 프리팹 안의 TMP 위치/폰트는 건드리지 않음.
// PostItNote.Setup이 텍스트 내용과 본체 머티리얼만 채움.

[System.Serializable]
public class PostItCommentData
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

    [Header("board 실측 스폰 범위 (board 로컬 단위 — 메시 실측값 기준)")]
    [Tooltip("board 메시 X 절반 폭. BoardInteraction 주석 기준 3.72")]
    public float boardHalfWidth = 3.72f;
    [Tooltip("board 메시 하단 Y. 피벗이 하단 중앙이므로 0")]
    public float boardMinY      = 0f;
    [Tooltip("board 메시 상단 Y. BoardInteraction 주석 기준 2.45")]
    public float boardMaxY      = 2.45f;
    [Tooltip("board 경계에서 포스트잇 시각 중심까지의 여백 (board 로컬 단위)")]
    public float spawnMargin    = 0.3f;
    [Tooltip("추가 오프셋 (board 로컬 단위). 필요 시 미세조정.")]
    public Vector3 spawnOffset  = Vector3.zero;

    [Header("우측 하단 제외 영역 (Gizmo 10×10 그리드 기준)")]
    [Tooltip("우측에서 제외할 열 수 (가로). 2이면 오른쪽 2칸")]
    [Range(0, 10)]
    public int excludeRightCols   = 3;
    [Tooltip("아래쪽에서 제외할 행 수 (세로). 2이면 아래쪽 2칸")]
    [Range(0, 10)]
    public int excludeBottomRows  = 2;

    [Header("겹침 방지")]
    [Tooltip("포스트잇 간 최소 간격 (월드 단위) — 클수록 더 떨어져 배치")]
    public float minSpacing = 1.5f;
    [Tooltip("겹치지 않는 위치 탐색 시도 횟수")]
    public int   placementAttempts = 25;

    // ── 내부 ──────────────────────────────────────────────────────────────
    private readonly List<PostItNote> _notes           = new List<PostItNote>();
    private readonly List<Vector3>    _placedPositions = new List<Vector3>();

    // DB 로드 실패 시 사용할 디폴트 댓글 (운영자가 작성한 3개)
    static readonly PostItCommentData[] _dummyComments = new PostItCommentData[]
    {
        new PostItCommentData { username = "운영자", comment = "오늘 방의 댓글을 추가해주세요" },
        new PostItCommentData { username = "운영자", comment = "오늘도 좋은 하루 되세요" },
        new PostItCommentData { username = "운영자", comment = "어떤 하루를 보내고 있나요?" },
    };

    void Start()
    {
        // CommentInputUI.Start()의 MarkOccupied가 먼저 실행된 뒤 스폰하도록 한 프레임 대기
        StartCoroutine(LoadCommentsNextFrame());
    }

    System.Collections.IEnumerator LoadCommentsNextFrame()
    {
        yield return null; // 한 프레임 대기 → 모든 Start() 완료 후 실행
        LoadComments();
    }

    // ── 댓글 로드 ─────────────────────────────────────────────────────────
    void LoadComments()
    {
        // ════════════════════════════════════════════════════════════════════
        // ★★★ TODO: 백엔드 연동 — 추후 백엔드 담당이 구현 ★★★
        // 백엔드 모듈에서 방 댓글 목록을 받아와 각 항목을 AddComment(username, comment)로 호출.
        // 응답 실패 / 빈 결과 시 아래 더미(운영자 3개)로 fallback.
        // 프론트는 함수 호출만, 실제 DB 통신은 백엔드 모듈에 위임.
        // ════════════════════════════════════════════════════════════════════

        foreach (var data in _dummyComments)
            AddComment(data.username, data.comment);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>댓글 생성 시 호출 → 랜덤 머티리얼·위치로 포스트잇 프리팹 인스턴스 생성</summary>
    public void AddComment(string username, string comment)
    {
        if (postItPrefab == null || board == null || materials == null || materials.Length == 0)
        {
            return;
        }
        if (_notes.Count >= maxNotes) RemoveOldest();

        GameObject obj = Instantiate(postItPrefab, board);
        obj.transform.localRotation = Quaternion.Euler(
            90f, 0f, Random.Range(-randomRotationRange, randomRotationRange));

        PostItNote note = obj.GetComponent<PostItNote>();
        if (note == null) note = obj.AddComponent<PostItNote>();

        Material mat = materials[Random.Range(0, materials.Length)];
        note.Setup(username, comment, mat, this);

        // 시각적 중심(GetVisualCenter)이 타겟 위치에 오도록 피벗 역산.
        // model.dae 피벗이 메시 중심에서 크게 벗어나 있어 보정 필요.
        Vector3 target        = GetRandomPosition();          // 시각 중심의 목표 (board 내부)
        Vector3 pivotToVisual = note.GetVisualCenter() - obj.transform.position; // 회전 적용 후 오프셋
        obj.transform.position = target - pivotToVisual;

        _notes.Add(note);
    }

    /// <summary>모든 포스트잇 제거</summary>
    public void ClearAll()
    {
        foreach (var n in _notes)
            if (n != null) Destroy(n.gameObject);
        _notes.Clear();
        _placedPositions.Clear();
    }

    /// <summary>특정 위치를 '점유'로 표시 — 포스트잇이 minSpacing 거리 안에 안 생김.
    /// 댓글 추가 버튼(Sticky_note_yellow) 등 피하고 싶은 위치 등록용.</summary>
    public void MarkOccupied(Vector3 worldPos)
    {
        _placedPositions.Add(worldPos);
    }

    // ── 내부 ──────────────────────────────────────────────────────────────

    // board 로컬 좌표에서 메시 실측 범위 안의 랜덤 위치를 생성한 뒤 TransformPoint로 월드 변환.
    // boardCol.size + lossyScale 방식을 쓰지 않는 이유:
    //   col.size는 로컬 단위인데 board 스케일에 따라 결과가 달라지고, 감지 평면(Z=5)이라 용도가 다름.
    // 우측 하단 제외 영역의 로컬 경계 계산.
    // board 로컬 +X가 화면상 왼쪽이므로 실제 우측 = minX 쪽.
    // 제외 조건: lx < excludeMaxX(minX쪽에서 cols칸) && ly < excludeMaxY(아래에서 rows칸)
    void GetExcludeZone(float minX, float maxX, float minY, float maxY,
                        out float excludeMaxX, out float excludeMaxY)
    {
        float cellW = (maxX - minX) / 10f;
        float cellH = (maxY - minY) / 10f;
        excludeMaxX = minX + excludeRightCols  * cellW;
        excludeMaxY = minY + excludeBottomRows * cellH;
    }

    bool IsExcluded(float lx, float ly, float excludeMaxX, float excludeMaxY)
    {
        return excludeRightCols > 0 && excludeBottomRows > 0
            && lx < excludeMaxX && ly < excludeMaxY;
    }

    Vector3 GetRandomPosition()
    {
        float minX = -boardHalfWidth + spawnMargin + spawnOffset.x;
        float maxX =  boardHalfWidth - spawnMargin + spawnOffset.x;
        float minY =  boardMinY      + spawnMargin + spawnOffset.y;
        float maxY =  boardMaxY      - spawnMargin + spawnOffset.y;

        // 오프셋 적용 후에도 범위 역전 방지
        if (minX > maxX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
        if (minY > maxY) { float mid = (minY + maxY) * 0.5f; minY = maxY = mid; }

        GetExcludeZone(minX, maxX, minY, maxY, out float excludeMinX, out float excludeMaxY);

        Vector3 best        = board.TransformPoint(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, spawnOffset.z));
        float   bestMinDist = -1f;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float lx = Random.Range(minX, maxX);
            float ly = Random.Range(minY, maxY);

            if (IsExcluded(lx, ly, excludeMinX, excludeMaxY)) continue;

            // board 로컬 → 월드 (TransformPoint가 위치·회전·스케일 전부 처리)
            Vector3 candidate = board.TransformPoint(new Vector3(lx, ly, spawnOffset.z));

            float minDist = float.MaxValue;
            foreach (var p in _placedPositions)
            {
                float d = Vector3.Distance(candidate, p);
                if (d < minDist) minDist = d;
            }

            if (minDist >= minSpacing)
            {
                _placedPositions.Add(candidate);
                return candidate;
            }
            if (minDist > bestMinDist)
            {
                bestMinDist = minDist;
                best        = candidate;
            }
        }

        _placedPositions.Add(best);
        return best;
    }

    void RemoveOldest()
    {
        if (_notes.Count == 0) return;
        var oldest = _notes[0];
        _notes.RemoveAt(0);
        if (oldest != null) Destroy(oldest.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (board == null) return;

        float minX = -boardHalfWidth + spawnMargin + spawnOffset.x;
        float maxX =  boardHalfWidth - spawnMargin + spawnOffset.x;
        float minY =  boardMinY      + spawnMargin + spawnOffset.y;
        float maxY =  boardMaxY      - spawnMargin + spawnOffset.y;

        // board 로컬 4 꼭짓점 → 월드 (TransformPoint가 스케일·회전 전부 처리)
        Vector3 tl = board.TransformPoint(new Vector3(minX, maxY, spawnOffset.z));
        Vector3 tr = board.TransformPoint(new Vector3(maxX, maxY, spawnOffset.z));
        Vector3 br = board.TransformPoint(new Vector3(maxX, minY, spawnOffset.z));
        Vector3 bl = board.TransformPoint(new Vector3(minX, minY, spawnOffset.z));

        Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, tl);

        // 격자
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        for (int i = 0; i <= 10; i++)
        {
            float tx = Mathf.Lerp(minX, maxX, i / 10f);
            float ty = Mathf.Lerp(minY, maxY, i / 10f);
            Gizmos.DrawLine(board.TransformPoint(new Vector3(tx, minY, spawnOffset.z)),
                            board.TransformPoint(new Vector3(tx, maxY, spawnOffset.z)));
            Gizmos.DrawLine(board.TransformPoint(new Vector3(minX, ty, spawnOffset.z)),
                            board.TransformPoint(new Vector3(maxX, ty, spawnOffset.z)));
        }

        // 우측 하단 제외 영역 (빨간색) — 로컬 minX쪽이 화면상 우측
        if (excludeRightCols > 0 && excludeBottomRows > 0)
        {
            GetExcludeZone(minX, maxX, minY, maxY, out float exMaxX, out float exMaxY);
            Vector3 etl = board.TransformPoint(new Vector3(minX,   exMaxY, spawnOffset.z));
            Vector3 etr = board.TransformPoint(new Vector3(exMaxX, exMaxY, spawnOffset.z));
            Vector3 ebr = board.TransformPoint(new Vector3(exMaxX, minY,   spawnOffset.z));
            Vector3 ebl = board.TransformPoint(new Vector3(minX,   minY,   spawnOffset.z));
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawLine(etl, etr);
            Gizmos.DrawLine(etr, ebr);
            Gizmos.DrawLine(ebr, ebl);
            Gizmos.DrawLine(ebl, etl);
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            for (int i = 0; i <= 6; i++)
            {
                float tx = Mathf.Lerp(minX, exMaxX, i / 6f);
                float ty = Mathf.Lerp(minY, exMaxY, i / 6f);
                Gizmos.DrawLine(board.TransformPoint(new Vector3(tx, minY,   spawnOffset.z)),
                                board.TransformPoint(new Vector3(tx, exMaxY, spawnOffset.z)));
                Gizmos.DrawLine(board.TransformPoint(new Vector3(minX,   ty, spawnOffset.z)),
                                board.TransformPoint(new Vector3(exMaxX, ty, spawnOffset.z)));
            }
        }

        // 중심점
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(board.TransformPoint(new Vector3(
            spawnOffset.x, (boardMinY + boardMaxY) * 0.5f + spawnOffset.y, spawnOffset.z)), 0.05f);
    }
}
