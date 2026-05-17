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

    [Header("가구 → 씬 매핑")]
    [Tooltip("가구 오브젝트 이름 / 목적지 씬 이름 쌍. 씬 준비되면 SampleScene → 실제 씬 이름으로 교체")]
    public FurnitureEntry[] furnitureEntries = new FurnitureEntry[]
    {
        new FurnitureEntry { objectName = "SingleBed",     sceneName = "SampleScene" },
        new FurnitureEntry { objectName = "KitchenIsland", sceneName = "SampleScene" },
        new FurnitureEntry { objectName = "Drawer1",       sceneName = "SampleScene" },
        new FurnitureEntry { objectName = "Door",          sceneName = "SampleScene" },
    };

    [System.Serializable]
    public class FurnitureEntry
    {
        public string objectName;
        public string sceneName;
    }

    public enum State { Free, ZoomingIn, Returning }
    public State CurrentState { get; private set; } = State.Free;

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;
    private string     _targetScene;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        Debug.Log("[Furniture] Awake 실행됨");

        if (joystickObject == null)
        {
            var jc = FindObjectOfType<JoystickController>();
            if (jc != null && jc.joystickArea != null)
                joystickObject = jc.joystickArea.gameObject;
        }
    }

    void Start()
    {
        foreach (var entry in furnitureEntries)
        {
            var obj = GameObject.Find(entry.objectName) ?? FindIgnoreCase(entry.objectName);
            if (obj == null)
            {
                Debug.LogWarning($"[Furniture] '{entry.objectName}' 오브젝트를 찾지 못함");
                continue;
            }

            if (obj.GetComponent<Collider>() == null)
            {
                var col = obj.AddComponent<BoxCollider>();
                FitColliderToBounds(obj, col);
            }

            var fi = obj.GetComponent<FurnitureInteraction>();
            if (fi == null) fi = obj.AddComponent<FurnitureInteraction>();
            fi.sceneName = entry.sceneName;
            Debug.Log($"[Furniture] '{entry.objectName}' 등록 완료 → {entry.sceneName}");
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
        if (CurrentState != State.Free) { Debug.Log("[Furniture] 이미 포커스 중"); return; }

        var fi = target.GetComponent<FurnitureInteraction>()
               ?? target.GetComponentInParent<FurnitureInteraction>();
        if (fi == null || string.IsNullOrEmpty(fi.sceneName))
        { Debug.Log($"[Furniture] FurnitureInteraction 없음: {target.name}"); return; }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { Debug.LogError("[Furniture] Camera.main null"); return; }

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;
        _targetScene  = fi.sceneName;

        CalcViewPoint(target, out _camPosTarget, out _camRotTarget);
        Debug.Log($"[Furniture] FocusFurniture({target.name}) → target={_camPosTarget}");

        CurrentState = State.ZoomingIn;
        LockPlayer(true);
        StartCoroutine(LoadSceneAfterZoom());
    }

    IEnumerator LoadSceneAfterZoom()
    {
        yield return new WaitForSeconds(zoomHoldTime);

        if (CurrentState != State.ZoomingIn) yield break;

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

    static GameObject FindIgnoreCase(string name)
    {
        foreach (var obj in FindObjectsOfType<GameObject>(true))
            if (string.Equals(obj.name, name, System.StringComparison.OrdinalIgnoreCase))
                return obj;
        return null;
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
