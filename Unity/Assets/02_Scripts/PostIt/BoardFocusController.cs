using UnityEngine;

// ── 전체 포커스 상태 관리 ─────────────────────────────────────────────────
// 유니티 Inspector 설정:
// 1. 빈 GameObject 만들고 BoardFocusController 이름, 이 스크립트 붙이기
// 2. playerMovement → 씬의 PlayerMovementController 연결
// 3. boardViewPoint  → 보드 앞에 Empty GameObject 배치 (카메라가 이동할 위치)
//                      보드를 바라보는 방향으로 회전 맞춰두기
// 4. postItViewDist  → 포스트잇 줌인 시 카메라와 포스트잇 사이 거리 (기본 0.3)
// 5. moveSpeed       → 카메라 이동 부드러움

public class BoardFocusController : MonoBehaviour
{
    // ── 싱글턴 ────────────────────────────────────────────────────────────
    public static BoardFocusController Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("연결")]
    public PlayerMovementController playerMovement;

    [Header("보드 뷰포인트 (보드 앞 Empty GameObject)")]
    public Transform boardViewPoint;

    [Header("포스트잇 줌인 거리")]
    public float postItViewDist = 0.25f;

    [Header("카메라 이동 속도")]
    public float moveSpeed = 5f;

    // ── 상태 ──────────────────────────────────────────────────────────────
    public enum FocusState { Free, Board, PostIt }
    public FocusState State { get; private set; } = FocusState.Free;

    // ── 내부 ──────────────────────────────────────────────────────────────
    private Camera     _cam;
    private Vector3    _camPosOrigin;
    private Quaternion _camRotOrigin;

    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;

    private PostItNote _focusedNote;

    // ── 레이어/태그 ───────────────────────────────────────────────────────
    // board 오브젝트에 "Board" 태그, postit에 "PostIt" 태그 설정 필요
    const string TAG_BOARD  = "Board";
    const string TAG_POSTIT = "PostIt";

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
    }

    void Start()
    {
        SaveOriginCamera();
    }

    void Update()
    {
        // 카메라 부드럽게 이동
        if (State != FocusState.Free)
        {
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
            _cam.transform.rotation = Quaternion.Slerp(
                _cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);
        }

        // 마우스 클릭 → 밖 클릭 감지
        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>BoardInteraction에서 호출 — 보드 포커스</summary>
    public void FocusBoard()
    {
        if (State != FocusState.Free) return;

        State = FocusState.Board;
        LockPlayer(true);

        if (boardViewPoint != null)
        {
            _camPosTarget = boardViewPoint.position;
            _camRotTarget = boardViewPoint.rotation;
        }
    }

    /// <summary>PostItNote에서 호출 — 포스트잇 포커스</summary>
    public void FocusPostIt(PostItNote note)
    {
        if (State != FocusState.Board) return;

        State        = FocusState.PostIt;
        _focusedNote = note;

        // 포스트잇 앞으로 카메라 이동
        Vector3 dir = (note.transform.position - _cam.transform.position).normalized;
        _camPosTarget = note.transform.position - dir * postItViewDist;
        _camRotTarget = Quaternion.LookRotation(dir);

        // TODO: 댓글 UI 패널 열기
        // CommentPanel.Instance.Open(note.Username, note.Comment);
        Debug.Log($"[Board] 포스트잇 열람 — {note.Username}: {note.Comment}");
    }

    // ── 내부 ──────────────────────────────────────────────────────────────

    void HandleClick()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            string tag = hit.collider.tag;

            // Board 클릭 → FocusBoard (BoardInteraction에서 처리됨)
            if (tag == TAG_BOARD  && State == FocusState.Free)  return;

            // PostIt 클릭 → FocusPostIt (PostItNote에서 처리됨)
            if (tag == TAG_POSTIT && State == FocusState.Board) return;

            // 포스트잇 포커스 중 → 포스트잇 밖 클릭 → 보드로 복귀
            if (State == FocusState.PostIt && tag != TAG_POSTIT)
            {
                BackToBoard();
                return;
            }

            // 보드 포커스 중 → 보드/포스트잇 외 클릭 → Free로 복귀
            if (State == FocusState.Board && tag != TAG_BOARD && tag != TAG_POSTIT)
            {
                BackToFree();
                return;
            }
        }
        else
        {
            // 아무것도 안 맞았을 때 (빈 공간 클릭)
            if (State == FocusState.PostIt) { BackToBoard(); return; }
            if (State == FocusState.Board)  { BackToFree();  return; }
        }
    }

    void BackToBoard()
    {
        State        = FocusState.Board;
        _focusedNote = null;

        // TODO: 댓글 UI 패널 닫기
        // CommentPanel.Instance.Close();

        if (boardViewPoint != null)
        {
            _camPosTarget = boardViewPoint.position;
            _camRotTarget = boardViewPoint.rotation;
        }
    }

    void BackToFree()
    {
        State = FocusState.Free;
        LockPlayer(false);
        _focusedNote = null;

        _camPosTarget = _camPosOrigin;
        _camRotTarget = _camRotOrigin;
    }

    void SaveOriginCamera()
    {
        _camPosOrigin = _camPosTarget = _cam.transform.position;
        _camRotOrigin = _camRotTarget = _cam.transform.rotation;
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement == null) return;
        playerMovement.enabled = !locked;
    }
}
