using UnityEngine;

public class BoardFocusController : MonoBehaviour
{
    public static BoardFocusController Instance { get; private set; }

    [Header("연결")]
    public PlayerMovementController playerMovement;
    public Transform board;
    [Tooltip("줌인 시 숨길 조이스틱 UI (비워두면 JoystickController에서 자동 검색)")]
    public GameObject joystickObject;

    [Header("보드에서 카메라까지 거리")]
    public float boardViewDist = 12f;

    [Header("포스트잇 줌인 거리")]
    public float postItViewDist = 0.25f;

    [Header("카메라 이동 속도")]
    public float moveSpeed = 5f;

    public enum FocusState { Free, Board, PostIt, Returning }
    public FocusState State       { get; private set; } = FocusState.Free;
    public PostItNote FocusedNote => _focusedNote;

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;

    // 줌인 직전 카메라 위치/회전 저장
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;

    private PostItNote _focusedNote;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;

        // 조이스틱 자동 검색 (Inspector에서 지정 안 했을 때)
        if (joystickObject == null)
        {
            var jc = FindObjectOfType<JoystickController>();
            if (jc != null && jc.joystickArea != null)
                joystickObject = jc.joystickArea.gameObject;
        }
    }

    void Update()
    {
        if (State == FocusState.Free) return;

        // 줌인/줌아웃 공통 lerp
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
        _cam.transform.rotation = Quaternion.Slerp(
            _cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);

        // Returning 상태: 목표 위치에 충분히 가까워지면 Free로 전환
        if (State == FocusState.Returning)
        {
            float dist = Vector3.Distance(_cam.transform.position, _camPosTarget);
            if (dist < 0.05f)
            {
                _cam.transform.position = _camPosTarget;
                _cam.transform.rotation = _camRotTarget;
                State = FocusState.Free;
                LockPlayer(false);
            }
        }
    }

    public void FocusBoard()
    {
        if (State != FocusState.Free) return;

        // 줌인 직전 카메라 상태 저장
        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;

        State = FocusState.Board;
        LockPlayer(true);
        CalcBoardViewPoint(out _camPosTarget, out _camRotTarget);
    }

    public void FocusPostIt(PostItNote note)
    {
        if (State != FocusState.Board && State != FocusState.PostIt) return;
        State        = FocusState.PostIt;
        _focusedNote = note;

        // 포스트잇의 실제 시각적 중심 사용 — model.dae pivot이 (-62,151,-2.8)로
        // 어긋나 있어 transform.position을 쓰면 엉뚱한 곳으로 줌인됨.
        Vector3 noteCenter = note.GetVisualCenter();
        Vector3 dir = (noteCenter - _cam.transform.position).normalized;
        _camPosTarget = noteCenter - dir * postItViewDist;
        _camRotTarget = Quaternion.LookRotation(dir);

        Debug.Log($"[Board] 포스트잇 열람 — {note.Username}: {note.Comment} / center={noteCenter}");
    }

    public void BackToBoard()
    {
        State        = FocusState.Board;
        _focusedNote = null;
        CalcBoardViewPoint(out _camPosTarget, out _camRotTarget);
    }

    public void BackToFree()
    {
        _focusedNote  = null;
        State         = FocusState.Returning;

        // 저장해둔 줌인 직전 위치/회전으로 lerp
        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;

        // 플레이어는 즉시 해제
        LockPlayer(false);
    }

    void CalcBoardViewPoint(out Vector3 pos, out Quaternion rot)
    {
        if (board == null)
        {
            pos = _cam.transform.position;
            rot = _cam.transform.rotation;
            Debug.LogWarning("[BoardFocusController] board 미연결");
            return;
        }

        Vector3 boardFaceNormal = board.forward;
        Vector3 center = board.position;
        var ren = board.GetComponentInChildren<Renderer>();
        if (ren != null) center = ren.bounds.center;

        pos = center + boardFaceNormal * boardViewDist;
        rot = Quaternion.LookRotation(center - pos);
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement != null)
            playerMovement.enabled = !locked;

        // 줌인(locked) 시 조이스틱 숨김 → 시야 정리 + 포스트잇 클릭 가로채기 방지
        if (joystickObject != null)
            joystickObject.SetActive(!locked);
    }
}
