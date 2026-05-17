using UnityEngine;

public class LaunchTableFocusController : MonoBehaviour
{
    public static LaunchTableFocusController Instance { get; private set; }

    [Header("연결")]
    public PlayerMovementController playerMovement;
    public Transform table;
    [Tooltip("줌인 시 숨길 조이스틱 UI (비워두면 JoystickController에서 자동 검색)")]
    public GameObject joystickObject;

    [Header("테이블까지 카메라 거리")]
    public float tableViewDist = 5f;

    [Header("카메라 이동 속도")]
    public float moveSpeed = 5f;

    public enum FocusState { Free, Table, Returning }
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

        // table에 자동으로 마커 컴포넌트 + 콜라이더 추가
        if (table != null && table.GetComponent<LaunchTableInteraction>() == null)
            table.gameObject.AddComponent<LaunchTableInteraction>();

        // 필기구 / 책 — Renderer bounds 기준 콜라이더 자동 추가
        string[] clickableNames = { "BookOpen_01", "Pencil yellow", "Pencil black", "Eraser", "Highlighter pen yellow" };
        foreach (string objName in clickableNames)
        {
            var obj = GameObject.Find(objName);
            if (obj == null) continue;
            if (obj.GetComponent<Collider>() != null) continue;

            var col = obj.AddComponent<BoxCollider>();
            // 자식 Renderer 전체를 감싸도록 bounds 계산
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                col.center = obj.transform.InverseTransformPoint(b.center);
                col.size   = Vector3.Scale(b.size, new Vector3(
                    1f / obj.transform.lossyScale.x,
                    1f / obj.transform.lossyScale.y,
                    1f / obj.transform.lossyScale.z));
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

    public void FocusTable()
    {
        if (State != FocusState.Free) return;

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;

        State = FocusState.Table;
        LockPlayer(true);
        CalcTableViewPoint(out _camPosTarget, out _camRotTarget);
    }

    public void BackToFree()
    {
        State         = FocusState.Returning;
        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;

        LockPlayer(false);
        CameraController.Instance?.RestoreState();
    }

    void CalcTableViewPoint(out Vector3 pos, out Quaternion rot)
    {
        if (table == null)
        {
            pos = _cam.transform.position;
            rot = _cam.transform.rotation;
            Debug.LogWarning("[LaunchTableFocusController] table 미연결");
            return;
        }

        Vector3 center = table.position;
        var ren = table.GetComponentInChildren<Renderer>();
        if (ren != null) center = ren.bounds.center;

        pos = center + Vector3.up * tableViewDist;
        rot = Quaternion.LookRotation(Vector3.down, table.right);
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement != null)
            playerMovement.enabled = !locked;

        if (joystickObject != null)
            joystickObject.SetActive(!locked);
    }
}
