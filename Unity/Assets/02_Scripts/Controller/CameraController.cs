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
    [Range(1f, 5f)]
    public float touchSensitivityMultiplier = 2.5f;
    public float dragDeadZonePx = 0.3f;
    public float startPitch = 30f;
    public float minPitch = 10f;
    public float maxPitch = 80f;

    [Header("카메라 충돌")]
    public LayerMask obstacleLayer;
    public float collisionRadius = 0.8f;

    [Header("조이스틱 영역 (드래그 제외)")]
    public RectTransform joystickArea;

    [Header("클릭 판정")]
    [Tooltip("이 픽셀 이상 드래그하면 클릭으로 인정 안 함")]
    public float clickMaxDragPx = 15f;

    public static CameraController Instance { get; private set; }

    private float _yaw = 0f;
    private float _pitch;
    private float _savedYaw;
    private float _savedPitch;
    private int   _dragFingerId = -1;
    private Vector2 _lastDragPos;
    private Vector2 _touchClickStartPos;
    private Vector2 _mouseClickStartPos;
    private bool  _mouseDragActive = false;

    void Awake() { Instance = this; }
    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    public void SaveState()    { _savedYaw = _yaw; _savedPitch = _pitch; }
    public void RestoreState() { _yaw = _savedYaw; _pitch = _savedPitch; }

    void Start()
    {
        _pitch = startPitch;
        if (player == null) return;
        ApplyPositionAndRotation(snap: true);
    }

    void LateUpdate()
    {
        HandleRotationInput();

        var bfc  = BoardFocusController.Instance;
        var ltfc = LaunchTableFocusController.Instance;
        var ffc  = FurnitureFocusController.Instance;
        if (bfc  != null && bfc.State        != BoardFocusController.FocusState.Free)      return;
        if (ltfc != null && ltfc.State       != LaunchTableFocusController.FocusState.Free) return;
        if (ffc  != null && ffc.CurrentState != FurnitureFocusController.State.Free)       return;

        ApplyPositionAndRotation(snap: false);
    }

    void ApplyPositionAndRotation(bool snap)
    {
        if (player == null) return;

        Vector3 pivotPos = cameraPivot != null ? cameraPivot.position : player.position;
        Quaternion camRot = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(_pitch, 0f, 0f);
        Vector3 desiredPos = pivotPos + camRot * new Vector3(0f, 0f, -distance);

        Vector3 dir = (desiredPos - pivotPos).normalized;
        if (Physics.SphereCast(pivotPos, collisionRadius, dir, out RaycastHit hit,
            Vector3.Distance(pivotPos, desiredPos), obstacleLayer))
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
                    if (IsPointerOverUI(touch.screenPosition)) continue;
                    _dragFingerId       = touch.finger.index;
                    _lastDragPos        = touch.screenPosition;
                    _touchClickStartPos = touch.screenPosition;
                }
                else if (touch.phase == TouchPhase.Moved && touch.finger.index == _dragFingerId)
                {
                    ApplyRotation((touch.screenPosition - _lastDragPos) * touchSensitivityMultiplier);
                    _lastDragPos = touch.screenPosition;
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _dragFingerId)
                {
                    if (Vector2.Distance(touch.screenPosition, _touchClickStartPos) < clickMaxDragPx)
                        CheckBoardClick(touch.screenPosition);
                    _dragFingerId = -1;
                }
            }
        }
        else if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _lastDragPos        = mousePos;
                _mouseClickStartPos = mousePos;
                _mouseDragActive    = true;
            }
            else if (Mouse.current.leftButton.isPressed && _mouseDragActive)
            {
                ApplyRotation(mousePos - _lastDragPos);
                _lastDragPos = mousePos;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (!IsTouchOnJoystick(mousePos) && !IsPointerOverUI(mousePos)
                    && Vector2.Distance(mousePos, _mouseClickStartPos) < clickMaxDragPx)
                    CheckBoardClick(mousePos);
                _mouseDragActive = false;
            }
        }
    }

    void ApplyRotation(Vector2 delta)
    {
        if (delta.magnitude < dragDeadZonePx) return;
        _yaw   -= delta.x * rotateSpeed;
        _pitch  = Mathf.Clamp(_pitch - delta.y * rotateSpeed, minPitch, maxPitch);
    }

    void CheckBoardClick(Vector2 screenPos)
    {
        var cui = CommentInputUI.Instance;
        if (cui != null && cui.IsPanelOpen) return;

        RaycastHit[] hits = Physics.RaycastAll(Camera.main.ScreenPointToRay(screenPos), 500f);

        var bfc = BoardFocusController.Instance;
        if (bfc == null) return;

        Debug.Log($"[CAM] Click — hits={hits.Length} bfcState={bfc.State}");
        foreach (var h in hits)
            Debug.Log($"[CAM]   hit: {h.collider.gameObject.name} (parent: {h.collider.transform.parent?.name})");

        RaycastHit? hCommentBtn = null, hPostIt = null, hBoard = null, hFurniture = null, hTable = null, hCalendar = null, hHost = null, hSelf = null;
        foreach (var h in hits)
        {
            bool hasMarker    = h.collider.GetComponent<CommentButtonMarker>() != null
                             || h.collider.GetComponentInParent<CommentButtonMarker>() != null;
            bool hasNote      = h.collider.GetComponent<PostItNote>() != null
                             || h.collider.GetComponentInParent<PostItNote>() != null;
            bool hasBoard     = h.collider.GetComponent<BoardInteraction>() != null;
            bool hasFurniture = h.collider.GetComponent<FurnitureInteraction>() != null
                             || h.collider.GetComponentInParent<FurnitureInteraction>() != null;
            bool hasTable     = h.collider.GetComponent<LaunchTableInteraction>() != null
                             || h.collider.GetComponentInParent<LaunchTableInteraction>() != null;
            bool hasCalendar  = h.collider.GetComponent<CalendarInteraction>() != null
                             || h.collider.GetComponentInParent<CalendarInteraction>() != null;
            bool hasHost      = h.collider.GetComponent<HostInteraction>() != null
                             || h.collider.GetComponentInParent<HostInteraction>() != null;
            bool hasSelf      = h.collider.GetComponent<PlayerSelfInteraction>() != null
                             || h.collider.GetComponentInParent<PlayerSelfInteraction>() != null;

            Debug.Log($"[CAM]   → {h.collider.gameObject.name} | marker={hasMarker} note={hasNote} board={hasBoard} furniture={hasFurniture} table={hasTable} calendar={hasCalendar} host={hasHost} self={hasSelf}");

            if      (hCommentBtn == null && hasMarker)    hCommentBtn = h;
            else if (hPostIt     == null && hasNote)      hPostIt     = h;
            else if (hBoard      == null && hasBoard)     hBoard      = h;
            else if (hHost       == null && hasHost)      hHost       = h;
            else if (hSelf       == null && hasSelf)      hSelf       = h;
            else if (hFurniture  == null && hasFurniture) hFurniture  = h;
            else if (hTable      == null && hasTable)     hTable      = h;
            else if (hCalendar   == null && hasCalendar)  hCalendar   = h;
        }

        Debug.Log($"[CAM] 감지 결과 — calendar={hCalendar.HasValue} | host={hHost.HasValue} | self={hSelf.HasValue} | cfc={CalendarFocusController.Instance?.State}");

        if (hCommentBtn.HasValue && bfc.State == BoardFocusController.FocusState.Board)
        {
            CommentInputUI.Instance?.TogglePanel();
            return;
        }

        if (hPostIt.HasValue)
        {
            var col  = hPostIt.Value.collider;
            var note = col.GetComponentInParent<PostItNote>() ?? col.GetComponent<PostItNote>();
            if (note != null && bfc.State == BoardFocusController.FocusState.Board)
            { bfc.FocusPostIt(note); return; }
            if (note != null && bfc.State == BoardFocusController.FocusState.PostIt)
            {
                if (note == bfc.FocusedNote) bfc.BackToBoard();
                else                         bfc.FocusPostIt(note);
                return;
            }
        }

        if (hBoard.HasValue && bfc.State == BoardFocusController.FocusState.Free)
        { bfc.FocusBoard(); return; }

        // host 클릭 — 줌인/줌아웃 토글
        var hfc = HostFocusController.Instance;
        if (hHost.HasValue && hfc != null)
        {
            if (hfc.State == HostFocusController.FocusState.Free
                && bfc.State == BoardFocusController.FocusState.Free)
            {
                Debug.Log("[CAM] → Host 클릭 — 줌인");
                SaveState();
                hfc.FocusHost();
            }
            else if (hfc.State == HostFocusController.FocusState.Host)
            {
                Debug.Log("[CAM] → Host 재클릭 — 줌아웃");
                hfc.BackToFree();
            }
            return;
        }

        if (hfc != null && hfc.State == HostFocusController.FocusState.Host && !hHost.HasValue)
        {
            hfc.BackToFree();
            return;
        }

        // 플레이어 자신 클릭 — UI 토글
        var psc = PlayerSelfController.Instance;
        if (hSelf.HasValue && psc != null && bfc.State == BoardFocusController.FocusState.Free)
        {
            if (!psc.IsOpen) { Debug.Log("[CAM] → 플레이어 자신 클릭 — 줌인 + UI 오픈"); SaveState(); psc.Open(); }
            else             { Debug.Log("[CAM] → 플레이어 자신 재클릭 — UI 닫기"); psc.Close(); }
            return;
        }
        if (psc != null && psc.IsOpen && !hSelf.HasValue)
        {
            psc.Close();
            return;
        }

        // 캘린더 클릭 — 줌인 + CalendarUI 열기
        var cfc = CalendarFocusController.Instance;
        if (hCalendar.HasValue && bfc.State == BoardFocusController.FocusState.Free
            && cfc != null && cfc.State == CalendarFocusController.FocusState.Free)
        {
            Debug.Log("[CAM] → 캘린더 클릭 — 줌인");
            SaveState();
            cfc.FocusCalendar();
            return;
        }

        // 캘린더 줌인 상태에서 빈 곳 클릭 → 복귀
        if (cfc != null && cfc.State == CalendarFocusController.FocusState.Calendar
            && !hCalendar.HasValue)
        {
            cfc.BackToFree();
            return;
        }

        var ffc = FurnitureFocusController.Instance;
        if (hFurniture.HasValue && ffc != null && ffc.CurrentState == FurnitureFocusController.State.Free
            && bfc.State == BoardFocusController.FocusState.Free)
        {
            var col = hFurniture.Value.collider;
            var fi  = col.GetComponent<FurnitureInteraction>() ?? col.GetComponentInParent<FurnitureInteraction>();
            if (fi != null) { SaveState(); ffc.FocusFurniture(fi.gameObject); return; }
        }

        var ltfc2 = LaunchTableFocusController.Instance;
        if (hTable.HasValue && bfc.State == BoardFocusController.FocusState.Free
            && ltfc2 != null && ltfc2.State == LaunchTableFocusController.FocusState.Free)
        { Debug.Log("[CAM] → FocusTable"); SaveState(); ltfc2.FocusTable(); return; }

        if (ltfc2 != null && ltfc2.State == LaunchTableFocusController.FocusState.Table)
        {
            var dui = DiaryWindowUI.Instance;
            if (dui != null && dui.IsAnyPanelOpen) { dui.CloseAll(); return; }

            if (hits.Length > 0 && dui != null)
            {
                foreach (var h in hits)
                    if (IsAncestorNamed(h.collider.transform, "BookOpen_01"))
                    { dui.OpenViewDiary(); return; }

                Transform tableRoot = ltfc2.table;
                foreach (var h in hits)
                    if (tableRoot != null &&
                        (h.collider.transform == tableRoot || h.collider.transform.IsChildOf(tableRoot)))
                    { dui.OpenWriteDiary(); return; }
            }

            if (!hTable.HasValue) { ltfc2.BackToFree(); }
            return;
        }

        if (bfc.State == BoardFocusController.FocusState.PostIt && !hPostIt.HasValue)
            bfc.BackToBoard();
        else if (bfc.State == BoardFocusController.FocusState.Board
                 && !hBoard.HasValue && !hPostIt.HasValue && !hCommentBtn.HasValue)
            bfc.BackToFree();
    }

    static bool IsAncestorNamed(Transform t, string name)
    {
        while (t != null) { if (t.name == name) return true; t = t.parent; }
        return false;
    }

    static bool IsPointerOverUI(Vector2 screenPos)
    {
        var ped = new UnityEngine.EventSystems.PointerEventData(EventSystem.current) { position = screenPos };
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count > 0;
    }

    bool IsTouchOnJoystick(Vector2 screenPos)
    {
        if (joystickArea == null) return false;
        if (!joystickArea.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(joystickArea, screenPos);
    }
}
