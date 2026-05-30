using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FurnitureFocusController : MonoBehaviour
{
    public static FurnitureFocusController Instance { get; private set; }

    [Header("연결")]
    public PlayerMovementController playerMovement;
    [Tooltip("줌인 시 숨길 조이스틱")]
    public GameObject joystickObject;

    [Header("줌인 설정")]
    public float viewDist  = 3f;
    public float moveSpeed = 5f;

    [Header("씬 전환")]
    public float zoomHoldTime = 1f;

    [System.Serializable]
    public class FurnitureEntry
    {
        public GameObject target;
        public string     sceneName;
        public bool       allowInVisitRoom;  // VisitRoom에서 줌인 허용 여부
        public bool       zoomOnlyNoScene;   // 줌인만, 씬 전환 없음
    }

    [Header("가구 목록 (Inspector에서 연결)")]
    public FurnitureEntry[] furnitureEntries;

    public enum State { Free, ZoomingIn, Returning }
    public State CurrentState { get; private set; } = State.Free;

    private Camera              _cam;
    private Vector3             _camPosTarget;
    private Quaternion          _camRotTarget;
    private Vector3             _camPosBefore;
    private Quaternion          _camRotBefore;
    private string              _targetScene;
    private FurnitureInteraction _currentFI;

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
    }

    void Start()
    {
        if (furnitureEntries == null) return;

        foreach (var entry in furnitureEntries)
        {
            if (entry.target == null) continue;

            if (entry.target.GetComponent<Collider>() == null)
            {
                var col = entry.target.AddComponent<BoxCollider>();
                FitColliderToBounds(entry.target, col);
            }

            var fi = entry.target.GetComponent<FurnitureInteraction>();
            if (fi == null) fi = entry.target.AddComponent<FurnitureInteraction>();
            fi.sceneName        = entry.sceneName;
            fi.allowInVisitRoom = entry.allowInVisitRoom;
            fi.zoomOnlyNoScene  = entry.zoomOnlyNoScene;
        }
    }

    void Update()
    {
        if (CurrentState == State.Free) return;

        _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
        _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);

        if (CurrentState == State.Returning &&
            Vector3.Distance(_cam.transform.position, _camPosTarget) < 0.05f)
        {
            _cam.transform.position = _camPosTarget;
            _cam.transform.rotation = _camRotTarget;
            CurrentState = State.Free;
            LockPlayer(false);
        }
    }

    public void FocusFurniture(GameObject target)
    {
        if (CurrentState != State.Free) { return; }

        var fi = target.GetComponent<FurnitureInteraction>()
               ?? target.GetComponentInParent<FurnitureInteraction>();
        if (fi == null || string.IsNullOrEmpty(fi.sceneName))
        { return; }

        // VisitRoom: allowInVisitRoom이 켜진 가구만 허용
        if (RoomModeManager.CurrentMode == RoomMode.VisitRoom && !fi.allowInVisitRoom)
        { return; }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { return; }

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;
        _targetScene  = fi.sceneName;
        _currentFI    = fi;

        CalcViewPoint(target, out _camPosTarget, out _camRotTarget);

        // zoomOnlyNoScene 가구: EditMode 책상 줌인과 동일한 카메라 설정 적용
        if (fi.zoomOnlyNoScene)
        {
            var fec = FurnitureEditController.Instance;
            if (fec != null)
            {
                var ren = target.GetComponentInChildren<Renderer>();
                Vector3 deskCenter = ren != null ? ren.bounds.center : target.transform.position;
                _camPosTarget = deskCenter + fec.deskCamOffset;
                _camRotTarget = Quaternion.LookRotation((deskCenter + fec.deskCamLookOffset) - _camPosTarget);
            }
        }

        CurrentState = State.ZoomingIn;
        LockPlayer(true);
        StartCoroutine(LoadSceneAfterZoom());
    }

    IEnumerator LoadSceneAfterZoom()
    {
        yield return new WaitForSeconds(zoomHoldTime);

        if (CurrentState != State.ZoomingIn) yield break;

        // zoomOnlyNoScene: 씬 이동 없이 줌인만
        if (_currentFI != null && _currentFI.zoomOnlyNoScene)
            yield break;

        // TODO: 씬 준비되면 각 가구별 실제 씬 이름으로 교체
        SceneManager.LoadScene(_targetScene);
    }

    public void BackToFree()
    {
        StopAllCoroutines();
        CurrentState  = State.Returning;
        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;
        LockPlayer(false);
        CameraController.Instance?.RestoreState();
    }

    void CalcViewPoint(GameObject target, out Vector3 pos, out Quaternion rot)
    {
        var ren = target.GetComponentInChildren<Renderer>();
        Vector3 center  = ren != null ? ren.bounds.center : target.transform.position;
        Vector3 dir     = (_cam.transform.position - center).normalized;
        Vector3 desired = center + dir * viewDist;

        var cc = CameraController.Instance;
        float radius = cc != null ? cc.collisionRadius : 0.3f;
        LayerMask mask = cc != null ? cc.obstacleLayer : Physics.DefaultRaycastLayers;
        if (Physics.SphereCast(center, radius, dir, out RaycastHit hit,
                Vector3.Distance(center, desired), mask))
            desired = hit.point - dir * radius;

        pos = desired;
        rot = Quaternion.LookRotation(center - pos);
    }

    void LockPlayer(bool locked)
    {
        if (playerMovement != null) playerMovement.enabled = !locked;
        if (joystickObject != null) joystickObject.SetActive(!locked);
    }

    void FitColliderToBounds(GameObject obj, BoxCollider col)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        col.center = obj.transform.InverseTransformPoint(b.center);
        col.size   = new Vector3(
            b.size.x / obj.transform.lossyScale.x,
            b.size.y / obj.transform.lossyScale.y,
            b.size.z / obj.transform.lossyScale.z);
    }
}
