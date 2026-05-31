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

    /// <summary>EditMode 등에서 카메라 각도를 즉시 전환</summary>
    public void SetAngle(float yaw, float pitch)
    {
        _yaw   = yaw;
        _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        ApplyPositionAndRotation(snap: true);
    }

    void Start()
    {
        _pitch = startPitch;
        if (player == null) return;
        ApplyPositionAndRotation(snap: true);
    }

    void LateUpdate()
    {
        HandleRotationInput();

        // EditMode: FurnitureEditController가 카메라 제어
        if (RoomModeManager.CurrentMode == RoomMode.EditMode) return;

        var tfc2 = TableFocusController.Instance;
        if (tfc2 != null && tfc2.IsZoomed) return;

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
        bool isEditMode = RoomModeManager.CurrentMode == RoomMode.EditMode;

        if (Touch.activeTouches.Count > 0)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (!isEditMode && IsTouchOnJoystick(touch.screenPosition)) continue;
                    // EditMode: UI 위의 터치도 _dragFingerId / _touchClickStartPos 기록
                    // (UI 버튼 탭이 Ended에서 CheckBoardClick을 막지 않도록 overUI 여부를 Ended에서 판단)
                    // 일반 모드: UI 우선 유지 (Began에서 차단)
                    if (!isEditMode && IsPointerOverUI(touch.screenPosition)) continue;
                    _dragFingerId       = touch.finger.index;
                    _lastDragPos        = touch.screenPosition;
                    _touchClickStartPos = touch.screenPosition;
                }
                else if (!isEditMode && touch.phase == TouchPhase.Moved && touch.finger.index == _dragFingerId)
                {
                    // EditMode에서는 드래그 회전 차단 (FurnitureEditController가 드래그 처리)
                    ApplyRotation((touch.screenPosition - _lastDragPos) * touchSensitivityMultiplier);
                    _lastDragPos = touch.screenPosition;
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _dragFingerId)
                {
                    // EditMode: Ended 시점에 UI 위면 버튼 탭이므로 3D 클릭 처리 생략
                    bool overUI = isEditMode && IsPointerOverButton(touch.screenPosition);
                    // Debug.Log($"[CAM] Touch Ended — isEdit={isEditMode} overUI={overUI} dist={Vector2.Distance(touch.screenPosition, _touchClickStartPos):F1}");
                    if (!overUI && Vector2.Distance(touch.screenPosition, _touchClickStartPos) < clickMaxDragPx)
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
            else if (!isEditMode && Mouse.current.leftButton.isPressed && _mouseDragActive)
            {
                // EditMode에서는 드래그 회전 차단 (FurnitureEditController가 드래그 처리)
                ApplyRotation(mousePos - _lastDragPos);
                _lastDragPos = mousePos;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool overUI     = isEditMode ? IsPointerOverButton(mousePos) : IsPointerOverUI(mousePos);
                bool onJoystick = !isEditMode && IsTouchOnJoystick(mousePos);
                bool blockByUI  = overUI || onJoystick;
                // Debug.Log($"[CAM] Mouse Released — isEdit={isEditMode} overUI={overUI} blockByUI={blockByUI} dist={Vector2.Distance(mousePos, _mouseClickStartPos):F1}");
                if (!blockByUI && Vector2.Distance(mousePos, _mouseClickStartPos) < clickMaxDragPx)
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
        // EditMode: FurnitureEditController로 라우팅
        if (RoomModeManager.CurrentMode == RoomMode.EditMode)
        {
            var fec = FurnitureEditController.Instance;
            if (fec == null) { return; }

            RaycastHit[] editHits = Physics.RaycastAll(Camera.main.ScreenPointToRay(screenPos), 2000f);
            System.Array.Sort(editHits, (a, b) => a.distance.CompareTo(b.distance)); // 가까운 것 우선
            foreach (var h in editHits)
            {
                GameObject target = fec.GetEditTarget(h.collider.gameObject);
                if (target != null) { fec.OnFurnitureClicked(target); return; }
            }
            fec.OnEmptyClicked();
            return;
        }

        var cui = CommentInputUI.Instance;
        if (cui != null && cui.IsPanelOpen) return;

        // TableFocusController 줌인 중 클릭 → 줌아웃
        var tfc = TableFocusController.Instance;
        if (tfc != null && tfc.IsZoomed) { tfc.BackToFree(); return; }

        RaycastHit[] hits = Physics.RaycastAll(Camera.main.ScreenPointToRay(screenPos), 500f);

        var bfc = BoardFocusController.Instance;
        if (bfc == null) return;

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


            if      (hCommentBtn == null && hasMarker)    hCommentBtn = h;
            else if (hPostIt     == null && hasNote)      hPostIt     = h;
            else if (hBoard      == null && hasBoard)     hBoard      = h;
            else if (hHost       == null && hasHost)      hHost       = h;
            else if (hSelf       == null && hasSelf)      hSelf       = h;
            else if (hFurniture  == null && hasFurniture) hFurniture  = h;
            else if (hTable      == null && hasTable)     hTable      = h;
            else if (hCalendar   == null && hasCalendar)  hCalendar   = h;
        }


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
                SaveState();
                hfc.FocusHost();
            }
            else if (hfc.State == HostFocusController.FocusState.Host)
            {
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
            if (!psc.IsOpen) { SaveState(); psc.Open(); }
            else             { psc.Close(); }
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
        {
            // VisitRoom: 작은책상 상호작용 차단
            if (RoomModeManager.CurrentMode == RoomMode.VisitRoom)
            { return; }
            SaveState();
            ltfc2.FocusTable();
            return;
        }

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
                    {
                        // VisitRoom: 남의 방에서는 일기 쓰기 불가
                        if (RoomModeManager.CurrentMode == RoomMode.VisitRoom)
                        { return; }
                        dui.OpenWriteDiary(); return;
                    }
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

    /// <summary>
    /// EditMode 전용: 실제 Button 컴포넌트가 있는 UI 위에 있을 때만 true.
    /// 투명 Canvas 배경은 무시하여 3D 오브젝트 클릭을 차단하지 않음.
    /// </summary>
    static bool IsPointerOverButton(Vector2 screenPos)
    {
        var ped = new UnityEngine.EventSystems.PointerEventData(EventSystem.current) { position = screenPos };
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        foreach (var r in results)
            if (r.gameObject.GetComponent<UnityEngine.UI.Button>() != null
             || r.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null)
                return true;
        return false;
    }

    bool IsTouchOnJoystick(Vector2 screenPos)
    {
        if (joystickArea == null) return false;
        if (!joystickArea.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(joystickArea, screenPos);
    }
}
