using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class CameraController : MonoBehaviour
{
    [Header("플레이어 연결")]
    public Transform player;
    public Transform cameraPivot;

    [Header("카메라 설정")]
    public float distance = 5f;
    public float followSpeed = 8f;

    [Header("카메라 회전")]
    [Tooltip("마우스 드래그 회전 감도 (데스크탑)")]
    public float rotateSpeed = 0.15f;
    [Tooltip("터치 드래그 회전 감도 배율 (모바일 — 손가락 작은 움직임도 크게 반영)")]
    [Range(1f, 5f)]
    public float touchSensitivityMultiplier = 2.5f;
    [Tooltip("드래그 무시 최소 픽셀 (이 이하 움직임은 회전 안 함)")]
    public float dragDeadZonePx = 0.3f;
    public float startPitch = 30f;
    public float minPitch = 10f;
    public float maxPitch = 80f;

    [Header("카메라 충돌")]
    public LayerMask obstacleLayer;
    public float collisionRadius = 0.3f;

    [Header("조이스틱 영역 (드래그 제외)")]
    public RectTransform joystickArea;

    public static CameraController Instance { get; private set; }

    private float _yaw = 0f;
    private float _pitch;

    private float _savedYaw;
    private float _savedPitch;

    private int _dragFingerId = -1;
    private Vector2 _lastDragPos;
    private bool _mouseDragActive = false;

    void Awake() { Instance = this; }

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    // 보드 포커스 직전에 호출 → yaw/pitch 저장
    public void SaveState()
    {
        _savedYaw   = _yaw;
        _savedPitch = _pitch;
    }

    // BackToFree 때 호출 → yaw/pitch 복원 → 카메라가 이전 각도로 돌아옴
    public void RestoreState()
    {
        _yaw   = _savedYaw;
        _pitch = _savedPitch;
    }

    void Start()
    {
        _pitch = startPitch;
        if (player == null) return;
        ApplyPositionAndRotation(snap: true);
    }

    void LateUpdate()
    {
        // 클릭 감지는 항상 처리 (Board/PostIt 상태에서 나가기 위해)
        HandleRotationInput();

        // 카메라 이동은 Free 상태일 때만 (Board/PostIt/Returning은 BoardFocusController가 제어)
        var bfc = BoardFocusController.Instance;
        if (bfc != null && bfc.State != BoardFocusController.FocusState.Free) return;

        ApplyPositionAndRotation(snap: false);
    }

    void ApplyPositionAndRotation(bool snap)
    {
        if (player == null) return;

        Vector3 pivotPos = cameraPivot != null ? cameraPivot.position : player.position;

        Quaternion yawRot   = Quaternion.Euler(0f, _yaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(_pitch, 0f, 0f);
        Quaternion camRot   = yawRot * pitchRot;

        Vector3 desiredPos = pivotPos + camRot * new Vector3(0f, 0f, -distance);

        Vector3 dir = (desiredPos - pivotPos).normalized;
        float maxDist = Vector3.Distance(pivotPos, desiredPos);
        if (Physics.SphereCast(pivotPos, collisionRadius, dir, out RaycastHit hit, maxDist, obstacleLayer))
            desiredPos = hit.point - dir * collisionRadius;

        Quaternion desiredRot = Quaternion.LookRotation(pivotPos - desiredPos, Vector3.up);

        if (snap)
        {
            transform.position = desiredPos;
            transform.rotation = desiredRot;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, followSpeed * Time.deltaTime);
        }
    }

    void HandleRotationInput()
    {
        if (Touch.activeTouches.Count > 0)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (IsTouchOnJoystick(touch.screenPosition)) continue;
                    if (EventSystem.current.IsPointerOverGameObject(touch.finger.index)) continue;

                    _dragFingerId = touch.finger.index;
                    _lastDragPos  = touch.screenPosition;

                    CheckBoardClick(touch.screenPosition);
                }
                else if (touch.phase == TouchPhase.Moved && touch.finger.index == _dragFingerId)
                {
                    Vector2 delta = touch.screenPosition - _lastDragPos;
                    // 모바일 터치는 감도 배율 적용
                    ApplyRotation(delta * touchSensitivityMultiplier);
                    _lastDragPos = touch.screenPosition;
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _dragFingerId)
                {
                    _dragFingerId = -1;
                }
            }
        }
        else if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsTouchOnJoystick(mousePos))
                {
                    _lastDragPos     = mousePos;
                    _mouseDragActive = true;
                    CheckBoardClick(mousePos);
                }
            }
            else if (Mouse.current.leftButton.isPressed && _mouseDragActive)
            {
                Vector2 delta = mousePos - _lastDragPos;
                ApplyRotation(delta);
                _lastDragPos = mousePos;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _mouseDragActive = false;
            }
        }
    }

    void ApplyRotation(Vector2 delta)
    {
        if (delta.magnitude < dragDeadZonePx) return;
        _yaw   -= delta.x * rotateSpeed;
        _pitch -= delta.y * rotateSpeed;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    void CheckBoardClick(Vector2 screenPos)
    {
        // 포스트잇 입력 창이 열려있으면 3D 상호작용 차단
        var cui = CommentInputUI.Instance;
        if (cui != null && cui.IsPanelOpen) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // 모든 히트 수집 — board collider가 포스트잇 앞을 막아도 전부 잡힘
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);

        var bfc = BoardFocusController.Instance;
        if (bfc == null) return;

        Debug.Log($"[CAM] Click — hits={hits.Length} state={bfc.State}");

        // 우선순위: CommentButton > PostIt > Board
        // Board 감지: CompareTag("Board") 대신 BoardInteraction 컴포넌트로 — 태그 미설정 문제 방지
        RaycastHit? hCommentBtn = null, hPostIt = null, hBoard = null;
        foreach (var h in hits)
        {
            bool hasMarker = h.collider.GetComponent<CommentButtonMarker>() != null
                          || h.collider.GetComponentInParent<CommentButtonMarker>() != null;
            bool hasNote   = h.collider.GetComponent<PostItNote>() != null
                          || h.collider.GetComponentInParent<PostItNote>() != null;
            bool hasBoard  = h.collider.GetComponent<BoardInteraction>() != null;

            Debug.Log($"[CAM]   hit '{h.collider.gameObject.name}' marker={hasMarker} note={hasNote} board={hasBoard}");

            if      (hCommentBtn == null && hasMarker) hCommentBtn = h;
            else if (hPostIt     == null && hasNote)   hPostIt     = h;
            else if (hBoard      == null && hasBoard)  hBoard      = h;
        }

        // ── 댓글 버튼 클릭 (Board 상태에서만) ────────────────────────────────
        if (hCommentBtn.HasValue && bfc.State == BoardFocusController.FocusState.Board)
        {
            Debug.Log("[CAM] → CommentButton → TogglePanel");
            CommentInputUI.Instance?.TogglePanel();
            return;
        }

        // ── 포스트잇 클릭 ─────────────────────────────────────────────────────
        if (hPostIt.HasValue)
        {
            var col  = hPostIt.Value.collider;
            var note = col.GetComponentInParent<PostItNote>() ?? col.GetComponent<PostItNote>();

            if (note != null && bfc.State == BoardFocusController.FocusState.Board)
            {
                Debug.Log($"[CAM] → FocusPostIt({note.Username})");
                bfc.FocusPostIt(note);
                return;
            }
            if (note != null && bfc.State == BoardFocusController.FocusState.PostIt)
            {
                if (note == bfc.FocusedNote) { Debug.Log("[CAM] → 같은 포스팃 → BackToBoard"); bfc.BackToBoard(); }
                else                         { Debug.Log($"[CAM] → 다른 포스팃 → FocusPostIt({note.Username})"); bfc.FocusPostIt(note); }
                return;
            }
        }

        // ── Board 클릭 (Free 상태) ────────────────────────────────────────────
        if (hBoard.HasValue && bfc.State == BoardFocusController.FocusState.Free)
        {
            Debug.Log("[CAM] → FocusBoard");
            bfc.FocusBoard();
            return;
        }

        // ── 빈 공간 클릭 → 상태별 뒤로가기 ─────────────────────────────────
        if (bfc.State == BoardFocusController.FocusState.PostIt && !hPostIt.HasValue)
        { Debug.Log("[CAM] → BackToBoard (빈 공간)"); bfc.BackToBoard(); }
        else if (bfc.State == BoardFocusController.FocusState.Board
                 && !hBoard.HasValue && !hPostIt.HasValue && !hCommentBtn.HasValue)
        { Debug.Log("[CAM] → BackToFree (빈 공간)"); bfc.BackToFree(); }
    }

    bool IsTouchOnJoystick(Vector2 screenPos)
    {
        if (joystickArea == null) return false;
        // 조이스틱이 숨겨져 있으면(줌인 상태) 터치를 가로채지 않음 → 포스트잇 클릭 가능
        if (!joystickArea.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(joystickArea, screenPos);
    }
}
