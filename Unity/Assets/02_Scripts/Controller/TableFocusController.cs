using UnityEngine;

public class TableFocusController : MonoBehaviour
{
    public static TableFocusController Instance { get; private set; }

    [Header("연결")]
    public Transform table;

    [Header("줌인 높이 (테이블 위)")]
    public float viewHeight = 3f;

    [Header("카메라 이동 속도")]
    public float moveSpeed = 5f;

    public bool IsZoomed { get; private set; }

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;

        // 자기 자신이 테이블이면 자동 연결
        if (table == null) table = transform;

        if (table.GetComponent<TableInteraction>() == null)
            table.gameObject.AddComponent<TableInteraction>();
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
        Debug.Log($"[Table] 줌인 — target={_camPosTarget}");
    }

    public void BackToFree()
    {
        if (!IsZoomed) return;

        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;
        IsZoomed = false;
        Debug.Log("[Table] 줌아웃");
    }
}
