using UnityEngine;

public class CalendarFocusController : MonoBehaviour
{
    public static CalendarFocusController Instance { get; private set; }

    [Header("연결")]
    public PlayerMovementController playerMovement;
    public Transform calendarObject;
    [Tooltip("줌인 시 숨길 조이스틱 UI (비워두면 JoystickController에서 자동 검색)")]
    public GameObject joystickObject;

    [Header("카메라 설정")]
    public float viewDistance = 1.5f;   // 캘린더 앞까지 거리
    public float moveSpeed    = 5f;

    public enum FocusState { Free, Calendar, Returning }
    public FocusState State { get; private set; } = FocusState.Free;

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;

        if (joystickObject == null)
        {
            var jc = FindObjectOfType<JoystickController>();
            if (jc != null && jc.joystickArea != null)
                joystickObject = jc.joystickArea.gameObject;
        }

        // CalendarInteraction 마커 + 콜라이더 자동 추가
        if (calendarObject != null)
        {
            if (calendarObject.GetComponent<CalendarInteraction>() == null)
                calendarObject.gameObject.AddComponent<CalendarInteraction>();

            // Renderer bounds 기준으로 콜라이더 크기 자동 설정
            var col = calendarObject.GetComponent<BoxCollider>();
            if (col == null) col = calendarObject.gameObject.AddComponent<BoxCollider>();
            var renderers = calendarObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                col.center = calendarObject.InverseTransformPoint(b.center);
                col.size   = Vector3.Scale(b.size, new Vector3(
                    1f / calendarObject.lossyScale.x,
                    1f / calendarObject.lossyScale.y,
                    1f / calendarObject.lossyScale.z));
            }
        }
    }

    void Update()
    {
        if (State == FocusState.Free) return;

        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
        _cam.transform.rotation = Quaternion.Slerp(
            _cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);

        if (State == FocusState.Returning)
        {
            if (Vector3.Distance(_cam.transform.position, _camPosTarget) < 0.05f)
            {
                _cam.transform.position = _camPosTarget;
                _cam.transform.rotation = _camRotTarget;
                State = FocusState.Free;
                LockPlayer(false);
            }
        }
    }

    public void FocusCalendar()
    {
        if (State != FocusState.Free) return;

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;

        State = FocusState.Calendar;
        LockPlayer(true);
        CalcViewPoint(out _camPosTarget, out _camRotTarget);

        // 과거 방 선택용 날짜 피커 열기 (일기 CalendarUI와 별개)
        RoomCalendarUI.Instance?.Open();
    }

    public void BackToFree()
    {
        if (State == FocusState.Free) return;

        // RoomCalendarUI는 네이티브 피커라 별도 Close 불필요

        State         = FocusState.Returning;
        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;

        LockPlayer(false);
        CameraController.Instance?.RestoreState();
    }

    void CalcViewPoint(out Vector3 pos, out Quaternion rot)
    {
        if (calendarObject == null)
        {
            pos = _cam.transform.position;
            rot = _cam.transform.rotation;
            return;
        }

        // Renderer bounds 중심 계산
        Vector3 center = calendarObject.position;
        var ren = calendarObject.GetComponentInChildren<Renderer>();
        if (ren != null) center = ren.bounds.center;

        // 캘린더 앞면 기준으로 카메라 위치 계산 (-forward = 앞면 쪽)
        Vector3 forward = -calendarObject.forward;
        pos = center + forward * viewDistance;
        rot = Quaternion.LookRotation(-forward, Vector3.up);
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement != null)
            playerMovement.enabled = !locked;

        if (joystickObject != null)
            joystickObject.SetActive(!locked);
    }
}
