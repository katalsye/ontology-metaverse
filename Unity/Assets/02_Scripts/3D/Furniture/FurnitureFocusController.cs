using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("설명 UI (VisitRoom 줌인 후 표시)")]
    [Tooltip("줌인 후 표시할 패널 — Inspector에서 연결. 패널 안의 Button onClick → HideDescription() 연결.")]
    public GameObject descriptionPanel;
    [Tooltip("설명 텍스트 컴포넌트 — Inspector에서 연결.")]
    public TMP_Text descriptionText;

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
    private bool                _descriptionOverlay; // 데스크 줌인 중 UI만 띄운 경우
    public  FurnitureInteraction CurrentFI => _currentFI; // 현재 줌인된 오브젝트의 FI

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
        // 설명 패널 시작 시 반드시 비활성화 (활성 상태면 클릭을 가로챔)
        if (descriptionPanel != null)
            descriptionPanel.SetActive(false);

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
        if (CurrentState != State.Free) { Debug.Log($"[FFC] {target.name}: state != Free ({CurrentState}) → 무시"); return; }

        var fi = target.GetComponent<FurnitureInteraction>()
               ?? target.GetComponentInParent<FurnitureInteraction>();
        if (fi == null) { Debug.LogWarning($"[FFC] {target.name}: FurnitureInteraction 없음 → 무시"); return; }
        Debug.Log($"[FFC] {target.name}: fi 찾음 sceneName='{fi.sceneName}' zoomOnly={fi.zoomOnlyNoScene} allowVisit={fi.allowInVisitRoom}");
        // zoomOnlyNoScene이면 sceneName 없어도 줌인 허용 (설명 패널만 표시하는 Ontology 아이템)
        if (!fi.zoomOnlyNoScene && string.IsNullOrEmpty(fi.sceneName)) { Debug.LogWarning($"[FFC] {target.name}: sceneName 비어있고 zoomOnly 아님 → 무시"); return; }

        // VisitRoom: allowInVisitRoom이 켜진 가구만 허용
        if (RoomModeManager.CurrentMode == RoomMode.VisitRoom && !fi.allowInVisitRoom)
        { Debug.Log($"[FFC] {target.name}: VisitRoom인데 allowInVisitRoom=false → 무시"); return; }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { return; }

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;
        _targetScene  = fi.sceneName;
        _currentFI    = fi;

        CalcViewPoint(target, out _camPosTarget, out _camRotTarget);

        // zoomOnlyNoScene 가구
        if (fi.zoomOnlyNoScene)
        {
            var oi = target.GetComponent<OntologyItem>()
                  ?? target.GetComponentInParent<OntologyItem>();

            if (oi != null)
            {
                // Ontology 아이템: cameraViewPoint 있으면 그대로, 없으면 bounds 중심 기준 줌인
                if (oi.cameraViewPoint != null)
                {
                    _camPosTarget = oi.cameraViewPoint.position;
                    _camRotTarget = oi.cameraViewPoint.rotation;
                }
                else
                {
                    float prevDist = viewDist;
                    if (oi.viewDistance > 0f) viewDist = oi.viewDistance;
                    CalcViewPoint(target, out _camPosTarget, out _camRotTarget);
                    viewDist = prevDist;
                }
            }
            else
            {
                // 데스크 등 기존 가구: EditMode 책상 줌인과 동일한 카메라 설정 (원래 동작 유지)
                var fec = FurnitureEditController.Instance;
                if (fec != null)
                {
                    var rens = target.GetComponentsInChildren<Renderer>();
                    Vector3 deskCenter = target.transform.position;
                    if (rens.Length > 0)
                    {
                        Bounds b = rens[0].bounds;
                        for (int i = 1; i < rens.Length; i++) b.Encapsulate(rens[i].bounds);
                        deskCenter = b.center;
                    }
                    _camPosTarget = deskCenter + fec.deskCamOffset;
                    _camRotTarget = Quaternion.LookRotation((deskCenter + fec.deskCamLookOffset) - _camPosTarget);
                }
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
        // OntologyItem 있는 경우만 UI 표시, 데스크 등은 줌인만 하고 끝
        if (_currentFI != null && _currentFI.zoomOnlyNoScene)
        {
            var oi = _currentFI.GetComponent<OntologyItem>()
                  ?? _currentFI.GetComponentInParent<OntologyItem>();
            if (oi != null) ShowDescription(oi.message);
            yield break;
        }

        // TODO: 씬 준비되면 각 가구별 실제 씬 이름으로 교체
        SceneManager.LoadScene(_targetScene);
    }

    /// <summary>줌인 후 설명 패널을 열고 텍스트를 세팅한다.</summary>
    /// <param name="overlay">true = 이미 줌인된 상태에서 UI만 띄움 (닫을 때 BackToFree 안 함)</param>
    public void ShowDescription(string msg, bool overlay = false)
    {
        if (descriptionPanel == null) return;
        if (descriptionText != null)
            descriptionText.text = msg ?? "";
        descriptionPanel.SetActive(true);
        _descriptionOverlay = overlay;
    }

    /// <summary>
    /// 설명 패널을 닫고 카메라를 원래 위치로 복귀시킨다.
    /// 패널의 Button onClick → 이 메서드를 Inspector에서 연결할 것.
    /// 백엔드에서 호출해도 됨.
    /// </summary>
    public void HideDescription()
    {
        if (descriptionPanel != null)
            descriptionPanel.SetActive(false);

        if (_descriptionOverlay)
        {
            _descriptionOverlay = false; // 패널만 닫고 줌인 유지
            return;
        }
        BackToFree();
    }

    /// <summary>카메라 이동 없이 즉시 Free 상태로 전환 (다른 컨트롤러로 전환할 때 사용)</summary>
    public void ForceSetFree()
    {
        StopAllCoroutines();
        CurrentState = State.Free;
        _descriptionOverlay = false;
        LockPlayer(false);
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
