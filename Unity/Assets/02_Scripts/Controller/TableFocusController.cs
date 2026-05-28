using UnityEngine;

public class TableFocusController : MonoBehaviour
{
    private static TableFocusController _instance;
    public static TableFocusController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TableFocusController>();
                if (_instance == null)
                {
                    var go = new GameObject("TableFocusController");
                    _instance = go.AddComponent<TableFocusController>();
                }
            }
            return _instance;
        }
    }

    [Header("큰책상 Transform (비워두면 KitchenIsland 자동 검색)")]
    public Transform table;

    [Header("줌인 높이 (테이블 위)")]
    public float viewHeight = 5f;

    [Header("카메라 이동 속도")]
    public float moveSpeed = 5f;

    [Header("플레이어 이동 잠금")]
    public PlayerMovementController playerMovement;
    public GameObject joystickObject;

    public bool IsZoomed { get; private set; }

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;

    void Awake()
    {
        _instance = this;
        _cam = Camera.main;

        // table 미연결 시 KitchenIsland 자동 탐색
        if (table == null)
        {
            var go = GameObject.Find("(Prb)KitchenIsland")
                  ?? GameObject.Find("KitchenIsland");
            if (go != null) table = go.transform;
            else            table = transform;
        }

        // playerMovement / joystickObject 미연결 시 자동 탐색
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovementController>();
        if (joystickObject == null)
        {
            var jc = FindObjectOfType<JoystickController>();
            if (jc != null && jc.joystickArea != null)
                joystickObject = jc.joystickArea.gameObject;
        }

        // TableInteraction 마커 자동 추가 (클릭 감지용)
        if (table != null && table.GetComponent<TableInteraction>() == null)
            AddTableInteractionRecursive(table);
    }

    // 자식 Collider가 있는 오브젝트에 마커 추가
    void AddTableInteractionRecursive(Transform root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col.GetComponent<TableInteraction>() == null)
                col.gameObject.AddComponent<TableInteraction>();
        }
        // 콜라이더가 아예 없으면 루트에 BoxCollider + 마커 추가
        if (root.GetComponentsInChildren<Collider>(true).Length == 0)
        {
            root.gameObject.AddComponent<BoxCollider>();
            root.gameObject.AddComponent<TableInteraction>();
        }
    }

    void Update()
    {
        if (!IsZoomed) return;

        _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
        _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);
    }

    public void FocusTable()
    {
        if (IsZoomed) return;

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;

        if (table == null) { Debug.LogWarning("[TableFocusController] table 미연결"); return; }

        Vector3 center = table.position;
        var ren = table.GetComponentInChildren<Renderer>();
        if (ren != null) center = ren.bounds.center;

        _camPosTarget = center + Vector3.up * viewHeight;
        _camRotTarget = Quaternion.LookRotation(center - _camPosTarget, table.forward);

        IsZoomed = true;
        LockPlayer(true);
        Debug.Log($"[Table] 줌인 — target={_camPosTarget}");
    }

    public void BackToFree()
    {
        if (!IsZoomed) return;

        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;
        IsZoomed = false;
        LockPlayer(false);
        Debug.Log("[Table] 줌아웃");
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement != null) playerMovement.enabled = !locked;
        if (joystickObject  != null) joystickObject.SetActive(!locked);
    }
}
