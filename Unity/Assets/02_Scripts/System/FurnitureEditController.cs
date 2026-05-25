using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DynamicWeatherSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// EditMode에서 가구 선택 → 디자인 변경 / 위치 이동을 담당.
/// 씬에 하나만 배치, RoomModeManager.ApplyMode()에서 EnterEditMode() 호출.
/// </summary>
public class FurnitureEditController : MonoBehaviour
{
    public static FurnitureEditController Instance { get; private set; }

    [Header("자동 등록 — 씬 컨테이너")]
    [Tooltip("가구들의 부모 Transform — RoomData.furnitureType으로 자식을 검색해 자동 등록")]
    public Transform furnitureParent;
    [Tooltip("벽들의 부모 Transform — 자식 전체가 canMove=false, canDesign=true 로 자동 등록")]
    public Transform wallParent;

    [Header("편집 가능 가구 목록 (런타임 자동 생성 — 수동 추가도 가능)")]
    public FurnitureEditConfig[] editableItems;

    [Header("그리드")]
    public float gridCellSize = 10f;
    [Tooltip("바닥 높이 (보통 0 — 가구 Y 위치 기준)")]
    public float floorY = 0f;

    [Header("방 경계 (이 안에서만 이동 가능)")]
    public float roomMinX = -200f;
    public float roomMaxX =  100f;
    public float roomMinZ = -100f;
    public float roomMaxZ =  200f;

    [Header("벽 부착 가구 Y 범위 (Hanger 등)")]
    public float hangerMinY = 10f;
    public float hangerMaxY = 80f;

    [Header("천장 부착 가구 Y (Ceiling 등)")]
    [Tooltip("천장 가구가 이동할 Y 높이 — 실제 천장 내부 표면에 맞게 조정")]
    public float ceilingY = 150f;

    [Header("카메라 고정 뷰 (Inspector에서 튜닝)")]
    public Vector3 editCamPosition = new Vector3(-50f, 90f,  80f);
    public Vector3 editCamLookAt   = new Vector3(-50f,  0f,  10f);

    [Header("선택 하이라이트")]
    public Color highlightColor = new Color(1f, 0.85f, 0f);
    [Range(0f, 1f)]
    public float highlightIntensity = 0.35f;

    [Header("조이스틱")]
    public GameObject joystickObject;

    [Header("벽 부착 가구 (Hanger)")]
    [Tooltip("벽 부착 가구 컨테이너 — 이 안의 자식 전부가 wallMounted=true 로 등록됨\n비워두면 이름에 'Board' 포함된 것만 자동 감지 (폴백)")]
    public Transform hangerItemParent;
    [Tooltip("Hanger(Board)가 닿으면 빨갛게 될 특정 오브젝트 (유리 등)")]
    public GameObject glassObject;

    [Header("책상 줌인 편집")]
    [Tooltip("탭 시 줌인되는 큰 책상 오브젝트")]
    public GameObject deskObject;
    [Tooltip("책상 위 물품 컨테이너 — 이 안의 자식들만 이동 편집 대상 (비우면 deskObject 직계 자식 사용)")]
    public Transform deskItemParent;
    [Tooltip("책상 중심 기준 카메라 오프셋 — Y: 높이, Z: 앞뒤(음수=앞쪽으로 빠져서 올려다보는 각도)")]
    public Vector3 deskCamOffset = new Vector3(80f, 40f, 0f);
    [Tooltip("카메라가 바라볼 지점 오프셋 (책상 중심 기준)")]
    public Vector3 deskCamLookOffset = new Vector3(0f, 10f, 0f);

    [Header("천장 편집")]
    [Tooltip("천장 가구 컨테이너 — 이 안의 자식들을 활성화하고 이동 편집 대상으로 사용")]
    public Transform ceilingItemParent;
    [Tooltip("천장 편집 모드 카메라 위치 — 바닥 부근에서 위를 올려다보는 지점")]
    public Vector3 ceilingCamPosition = new Vector3(-50f, 10f, 50f);
    [Tooltip("천장 편집 모드에서 숨길 바닥 오브젝트 (ray 차단 방지)")]
    public GameObject floorObject;

    [Header("UI — 하단 버튼 2개 (모드에 따라 기능 전환)")]
    [Tooltip("일반 모드: 가구 추가 패널 열기 / 위치 편집 모드: 90° 회전")]
    public Button addRotateBtn;
    [Tooltip("일반 모드: 위치 편집 진입 / 위치 편집 모드: 저장 확인")]
    public Button moveConfirmBtn;

    [Header("UI — 위치 편집 모드 전용 버튼")]
    [Tooltip("위치 편집 모드에서만 표시 — 누르면 위치 복원 후 종료 (겹침 여부 무관)")]
    public Button cancelMoveBtn;

    [Header("UI — 버튼 스프라이트")]
    public Sprite spriteAdd;      // addRotateBtn 일반 모드
    public Sprite spriteRotate;   // addRotateBtn 위치편집 모드
    public Sprite spriteMove;     // moveConfirmBtn 일반 모드
    public Sprite spriteConfirm;  // moveConfirmBtn 위치편집 모드
    public Sprite spriteCeiling;  // cancelMoveBtn 일반 모드 (천장 편집 진입)
    public Sprite spriteCancel;   // cancelMoveBtn 위치편집 모드 (취소)
    [Header("UI — 가구 추가 패널")]
    public GameObject addFurniturePanel;

    // ── 환경 원복용 (Start에서 1회 저장) ─────────────────────────
    private float            _origLightIntensity;
    private Color            _origLightColor;
    private AmbientMode      _origAmbientMode;
    private Color            _origAmbientLight;
    private bool             _origFog;
    private CameraClearFlags _origClearFlags;
    private Color            _origBgColor;
    private bool             _origPostProcessing;
    private Volume[]         _sceneVolumes;

    // EditMode 중 매 프레임 환경 강제 고정에 쓰는 플래그
    private bool _editModeActive = false;

    // ── 상태 ─────────────────────────────────────────────────
    private Camera      _cam;
    private GameObject  _selected;
    private bool        _positionEditMode; // 전체 위치 편집 모드
    private System.Collections.Generic.Dictionary<GameObject,(Vector3 pos, Quaternion rot)> _posSnapshot;
    private int         _dragFingerId  = -1;
    private Vector3     _dragOffset;
    private bool        _dragOffsetInit;
    private bool        _isDragOverlap = false;
    private bool        _waitForNewTouch = false; // 선택 전환 후 새 Began이 올 때까지 드래그 차단

    // ── 책상 줌인 편집 상태 ───────────────────────────────────
    private bool                  _deskEditMode = false;
    private bool                  _camReturning = false; // 카메라 복귀 코루틴 실행 중 플래그
    private FurnitureEditConfig[] _roomEditableItems;   // 방 레벨 editableItems 백업
    private float _savedRoomMinX, _savedRoomMaxX, _savedRoomMinZ, _savedRoomMaxZ;
    private float _savedFloorY;
    private Vector3 _savedEditCamPosition;
    private Vector3 _savedEditCamLookAt;

    // ── 천장 편집 상태 ────────────────────────────────────────
    private bool _ceilingEditMode = false;
    private System.Collections.Generic.List<(GameObject go, bool wasActive)> _ceilingActivated;
    private FurnitureEditConfig[] _ceilingRoomItems; // 천장 편집 진입 시 방 가구 백업 (겹침 체크용)

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

        // 씬 로드 시점에 미리 한 번 등록 (EnterEditMode 전에도 동작하도록)
        RefreshEditableItems();
    }

    void Start()
    {
        // 일반 모드 초기 리스너
        if (addRotateBtn)   addRotateBtn.onClick.AddListener(OpenAddFurniturePanel);
        if (moveConfirmBtn) moveConfirmBtn.onClick.AddListener(EnterPositionEditMode);

        // cancelMoveBtn: 기본은 천장 편집 진입 버튼
        if (cancelMoveBtn)
        {
            cancelMoveBtn.onClick.AddListener(EnterCeilingEditMode);
            SetBtnSprite(cancelMoveBtn, spriteCeiling);
        }

        // 원본 환경 1회 저장 — WeatherController.Start()가 DefaultExecutionOrder(100)이므로
        // 이 시점(기본 0)에는 아직 WeatherController가 Start()를 실행하기 전일 수 있음.
        // 따라서 저장은 1프레임 뒤로 미루고, EditMode가 이미 켜져 있다면 재진입.
        StartCoroutine(LateStartInit());
    }

    IEnumerator ReturnCameraToRoom(Vector3 targetPos, Vector3 targetLook)
    {
        if (_cam == null) yield break;

        _camReturning = true;

        Vector3    startPos = _cam.transform.position;
        Quaternion startRot = _cam.transform.rotation;

        Quaternion endRot = Quaternion.LookRotation((targetLook - targetPos).normalized, Vector3.up)
                            * Quaternion.Euler(0f, 0f, 180f);

        float duration = 0.5f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            _cam.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // 도착 후 일반 편집모드 카메라로 고정
        _cam.transform.position = targetPos;
        _cam.transform.LookAt(targetLook, Vector3.up);
        _cam.transform.Rotate(Vector3.forward, 180f, Space.Self);

        _camReturning = false;
    }

    // WeatherController(ExecutionOrder=100)가 Start()를 마친 뒤 저장하기 위해 1프레임 대기
    IEnumerator LateStartInit()
    {
        yield return null; // 1프레임 대기

        _sceneVolumes = FindObjectsOfType<Volume>(true);

        _origAmbientMode  = RenderSettings.ambientMode;
        _origAmbientLight = RenderSettings.ambientLight;
        _origFog          = RenderSettings.fog;

        var sun = RenderSettings.sun ?? FindObjectOfType<Light>();
        if (sun != null)
        {
            _origLightIntensity = sun.intensity;
            _origLightColor     = sun.color;
        }

        if (_cam != null)
        {
            _origClearFlags = _cam.clearFlags;
            _origBgColor    = _cam.backgroundColor;
            var cd = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (cd != null) _origPostProcessing = cd.renderPostProcessing;
        }

        // EditMode가 이미 요청되어 있으면 지금 다시 적용
        if (RoomModeManager.CurrentMode == RoomMode.EditMode)
            ApplyEditEnvironment();
    }

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    // ── EditMode 진입/퇴장 ────────────────────────────────────

    public void EnterEditMode()
    {
        if (_selected != null) { SetHighlight(_selected, false); _selected = null; }
        RefreshEditableItems();
        if (joystickObject != null) joystickObject.SetActive(false);
        if (_cam == null) _cam = Camera.main;

        // WeatherManager/WeatherController의 Start()가 끝난 뒤에 환경을 덮어써야 함.
        // LateStartInit 코루틴이 완료되어 있으면 바로 적용, 아니면 코루틴이 마저 처리함.
        if (_sceneVolumes != null)
            ApplyEditEnvironment();
        // else: LateStartInit 코루틴 끝에서 자동으로 적용됨

        _editModeActive = true;
    }

    // 실제 환경 설정 — WeatherManager/Controller가 덮어쓰므로 별도 메서드로 분리
    void ApplyEditEnvironment()
    {
        // WeatherManager(DWS) 비활성 — 이것이 핵심. LightModule이 매 프레임 조명을 재설정함.
        var weatherManager = FindObjectOfType<WeatherManager>();
        if (weatherManager != null) weatherManager.enabled = false;

        // WeatherController도 비활성
        var weatherController = FindObjectOfType<WeatherController>();
        if (weatherController != null) weatherController.enabled = false;

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            var cd = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (cd != null) cd.renderPostProcessing = false;
            _cam.clearFlags      = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.75f, 0.75f, 0.75f);
        }

        if (_sceneVolumes != null)
            foreach (var v in _sceneVolumes) if (v != null) v.enabled = false;

        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        RenderSettings.fog          = false;

        var sunLight = RenderSettings.sun ?? FindObjectOfType<Light>();
        if (sunLight != null)
        {
            sunLight.intensity = 0.4f;
            sunLight.color     = Color.white;
        }

        DynamicGI.UpdateEnvironment();
        _editModeActive = true;
    }

    public void ExitEditMode()
    {
        _editModeActive = false;
        if (_positionEditMode) CancelPositionEdit();
        if (_selected != null) { SetHighlight(_selected, false); _selected = null; }
        if (joystickObject != null) joystickObject.SetActive(true);

        // WeatherManager/Controller 복원
        var weatherManager = FindObjectOfType<WeatherManager>();
        if (weatherManager != null) weatherManager.enabled = true;

        var weatherController = FindObjectOfType<WeatherController>();
        if (weatherController != null) weatherController.enabled = true;

        // 전부 원복
        if (_cam != null)
        {
            var cd = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (cd != null) cd.renderPostProcessing = _origPostProcessing;
            _cam.clearFlags      = _origClearFlags;
            _cam.backgroundColor = _origBgColor;
        }
        if (_sceneVolumes != null)
            foreach (var v in _sceneVolumes) if (v != null) v.enabled = true;

        RenderSettings.ambientMode  = _origAmbientMode;
        RenderSettings.ambientLight = _origAmbientLight;
        RenderSettings.fog          = _origFog;

        var sunLight = RenderSettings.sun ?? FindObjectOfType<Light>();
        if (sunLight != null)
        {
            sunLight.intensity = _origLightIntensity;
            sunLight.color     = _origLightColor;
        }
        DynamicGI.UpdateEnvironment();

        // WeatherController에게 현재 날씨를 재적용시켜 원래 상태로 되돌림
        if (weatherController != null)
            weatherController.SetWeather(weatherController.CurrentWeather, weatherController.CurrentTime);

    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        if (RoomModeManager.CurrentMode != RoomMode.EditMode) return;

        if (_cam != null)
        {
            if (_ceilingEditMode)
            {
                Quaternion lookUp = Quaternion.LookRotation(Vector3.up, Vector3.back);
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, ceilingCamPosition, Time.deltaTime * 5f);
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, lookUp, Time.deltaTime * 5f);
            }
            else if (_deskEditMode)
            {
                // 책상 뷰: position + rotation 동시에 부드럽게 전환 (5f)
                // 목표 rotation: editCamLookAt을 내려다보며 forward 축으로 90도 회전한 top-down 뷰
                Quaternion lookRot   = Quaternion.LookRotation((editCamLookAt - editCamPosition).normalized, Vector3.up);
                Quaternion targetRot = lookRot * Quaternion.Euler(0f, 0f, 0f);
                _cam.transform.position = Vector3.Lerp(    _cam.transform.position, editCamPosition, Time.deltaTime * 5f);
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, targetRot,       Time.deltaTime * 5f);
            }
            else if (!_positionEditMode && !_camReturning)
            {
                // 일반 편집모드: 즉시 고정 + 180° 보정
                _cam.transform.position = editCamPosition;
                _cam.transform.LookAt(editCamLookAt, Vector3.up);
                _cam.transform.Rotate(Vector3.forward, 180f, Space.Self);
            }
        }

        if (_positionEditMode && _selected != null) HandleDrag();
    }

    // ── 가구 클릭 (CameraController에서 호출) ─────────────────

    /// <summary>EditMode에서 가구 클릭 시 CameraController가 호출</summary>
    public void OnFurnitureClicked(GameObject furniture)
    {
        if (!IsEditable(furniture)) return;

        if (_positionEditMode)
        {
            // 위치 편집 모드: canMove=false(벽 등)는 선택 불가
            var cfg = GetConfig(furniture);
            if (cfg == null || !cfg.canMove) return;

            // 탭으로 드래그 대상 전환 (같은 가구 탭 → 선택 해제)
            if (_selected != null) SetHighlight(_selected, false);
            _selected = (_selected == furniture) ? null : furniture;
            if (_selected != null) SetHighlight(_selected, true);
            _dragFingerId    = -1;
            _dragOffsetInit  = false;
            _isDragOverlap   = false;
            _waitForNewTouch = true; // 새 Began 오기 전까지 드래그 차단
        }
        else
        {
            // 일반 모드: 가구(canMove=true)만 디자인 변경, 벽은 무시
            var cfg = GetConfig(furniture);
if (cfg != null && cfg.canDesign && cfg.canMove)
            {
                // 큰 책상 탭 → 줌인 편집 모드 진입
                if (deskObject != null && cfg.target == deskObject)
                {
                    EnterDeskEditMode();
                    return;
                }
                DesignSelectUI.Instance?.Open(furniture.name);
            }
        }
    }

    /// <summary>EditMode에서 빈 곳 클릭 시 CameraController가 호출</summary>
    public void OnEmptyClicked()
    {
        // 이동 모드 중 빈 곳 탭 → 선택 해제만
        if (_positionEditMode && _selected != null)
        {
            SetHighlight(_selected, false);
            _selected = null;
        }
        // 일반 모드 빈 곳 탭 → 아무것도 안 함 (+ 버튼으로 추가)
    }

    // ── 하이라이트 ────────────────────────────────────────────

    void SetHighlight(GameObject go, bool show)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (show)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                Color baseColor = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor")
                    ? r.sharedMaterial.GetColor("_BaseColor")
                    : Color.white;
                block.SetColor("_BaseColor", Color.Lerp(baseColor, highlightColor, highlightIntensity));
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    // ── 버튼 콜백 ────────────────────────────────────────────

    // ── 자동 등록 ─────────────────────────────────────────────

    void RefreshEditableItems()
    {
        var list = new System.Collections.Generic.List<FurnitureEditConfig>();

        // furnitureParent 직계 자식 전부 자동 등록
        // - Door    : canMove=false (이동 불가, 겹침 감지 대상)
        // - Board   : wallMounted=true  → 벽면 슬라이딩 드래그
        //   ※ 벽 부착 종류 추가 시 isWallMounted 조건에 || 로 이름 추가
        // - Ceiling : ceilingMounted=true → 천장 XZ 드래그
        //   ※ 천장 부착 종류 추가 시 isCeiling 조건에 || 로 이름 추가
        if (furnitureParent != null)
            foreach (Transform child in furnitureParent)
            {
                bool isDoor    = child.name.IndexOf("Door",    System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool isCeiling = child.name.IndexOf("Ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0;
                // hangerItemParent가 없을 때만 이름 기반 폴백으로 Board 감지
                bool isWallMounted = hangerItemParent == null &&
                                     child.name.IndexOf("Board", System.StringComparison.OrdinalIgnoreCase) >= 0;
                list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = !isDoor, canDesign = true, wallMounted = isWallMounted, ceilingMounted = isCeiling });
            }

        // hangerItemParent 직계 자식 전부 → wallMounted=true (이름 무관)
        if (hangerItemParent != null)
            foreach (Transform child in hangerItemParent)
                list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = true, canDesign = true, wallMounted = true });

        // wallParent 직계 자식 전부 → canMove=false, canDesign=true
        if (wallParent != null)
            foreach (Transform child in wallParent)
                list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = false, canDesign = true });

        // ceilingItemParent 직계 자식 중 활성인 것만 → ceilingMounted=true
        if (ceilingItemParent != null)
            foreach (Transform child in ceilingItemParent)
                if (child.gameObject.activeSelf)
                    list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = true, canDesign = true, ceilingMounted = true });

        editableItems = list.ToArray();
    }

    // ── 가구 추가 ─────────────────────────────────────────────

    public void OpenAddFurniturePanel()
    {
        if (addFurniturePanel) addFurniturePanel.SetActive(true);
    }

    public void CloseAddFurniturePanel()
    {
        if (addFurniturePanel) addFurniturePanel.SetActive(false);
    }

    /// <summary>가구 추가 패널에서 선택 시 호출. 씬에 배치 후 editableItems에 자동 등록.</summary>
    public void AddFurniture(GameObject prefab)
    {
        if (prefab == null) return;
        var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        var list = new System.Collections.Generic.List<FurnitureEditConfig>(editableItems ?? new FurnitureEditConfig[0]);
        list.Add(new FurnitureEditConfig { target = instance, canMove = true, canDesign = true });
        editableItems = list.ToArray();

        CloseAddFurniturePanel();
    }

    /// <summary>우하단 '위치 변경' 버튼 → 전체 위치 편집 모드 진입</summary>
    public void EnterPositionEditMode()
    {
        _positionEditMode = true;
        _selected         = null;
        _dragFingerId     = -1;
        _dragOffsetInit   = false;
        _isDragOverlap    = false;
        _waitForNewTouch  = false; // 이전 세션에서 true로 남아있을 경우 초기화

        // 모든 canMove 가구 위치·회전 스냅샷 (취소용)
        _posSnapshot = new System.Collections.Generic.Dictionary<GameObject,(Vector3,Quaternion)>();
        if (editableItems != null)
            foreach (var cfg in editableItems)
                if (cfg.target != null && cfg.canMove)
                {
                    _posSnapshot[cfg.target] = (cfg.target.transform.position, cfg.target.transform.rotation);
                    // Rigidbody kinematic / Animator 비활성
                    var rb = cfg.target.GetComponentInChildren<Rigidbody>() ?? cfg.target.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
                    foreach (var anim in cfg.target.GetComponentsInChildren<Animator>()) anim.enabled = false;
                }

        // 버튼 기능 + 스프라이트 전환: 추가→회전, 위치이동→저장
        if (addRotateBtn)   { addRotateBtn.onClick.RemoveAllListeners();   addRotateBtn.onClick.AddListener(() => RotateSelected(90f)); SetBtnSprite(addRotateBtn,   spriteRotate); }
        if (moveConfirmBtn) { moveConfirmBtn.onClick.RemoveAllListeners(); moveConfirmBtn.onClick.AddListener(ConfirmPositionEdit);      SetBtnSprite(moveConfirmBtn, spriteConfirm); }

        // cancelMoveBtn: 취소 버튼으로 전환
        if (cancelMoveBtn)
        {
            cancelMoveBtn.onClick.RemoveAllListeners();
            cancelMoveBtn.onClick.AddListener(CancelPositionEdit);
            SetBtnSprite(cancelMoveBtn, spriteCancel);
        }
    }

    void ConfirmPositionEdit()
    {
        // 현재 선택 아이템이 겹치는 상태면 저장 불가 (천장 모드 포함)
        if (_isDragOverlap) return;
        // 방 레벨 가구 전체 겹침 체크 (책상/천장 모드는 별도 공간이므로 스킵)
        if (!_deskEditMode && !_ceilingEditMode && HasAnyOverlap()) return;

        // 이동된 모든 canMove 가구에 그리드 스냅 적용
        if (editableItems != null)
            foreach (var cfg in editableItems)
                if (cfg.target != null && cfg.canMove)
                {
                    var s = SnapToGrid(cfg.target.transform.position);
                    s.y = cfg.target.transform.position.y;
                    cfg.target.transform.position = s;
                }

        // TODO: DB 저장
        // 각 editableItems 중 canMove=true인 가구의 position·rotation을 저장
        // 예) RoomDataManager.Instance?.SaveFurniturePositions(editableItems);
        // Firebase 사용 시 각 가구 name 또는 id를 key로 position/rotation 직렬화 후 업로드

        if (_deskEditMode)    RestoreDeskState(lerpCamera: true);
        if (_ceilingEditMode) RestoreCeilingState(deactivateItems: false);
        ExitPositionEditMode();
    }

    void CancelPositionEdit()
    {
        // 책상/천장 모드는 겹침 무관하게 항상 복원 후 나갈 수 있음
        // 방 편집 모드는 겹침 있으면 취소 불가 — 먼저 겹침 해소해야 함
        if (!_deskEditMode && !_ceilingEditMode && HasAnyOverlap()) return;

        // 스냅샷으로 전부 복원
        if (_posSnapshot != null)
            foreach (var kvp in _posSnapshot)
                if (kvp.Key != null)
                {
                    kvp.Key.transform.position = kvp.Value.pos;
                    kvp.Key.transform.rotation = kvp.Value.rot;
                }
        if (_deskEditMode)    RestoreDeskState();
        if (_ceilingEditMode) RestoreCeilingState(deactivateItems: true);
        ExitPositionEditMode();
    }

    void ExitPositionEditMode()
    {
        // 빨간 하이라이트 먼저 지우고 나서 selected 해제
        if (_selected != null)
        {
            if (_isDragOverlap) SetDragHighlightColor(highlightColor);
            SetHighlight(_selected, false);
            _selected = null;
        }
        _isDragOverlap    = false;
        _positionEditMode = false;
        _posSnapshot      = null;

        // 버튼 기능 + 스프라이트 복원: 회전→추가, 저장→위치이동
        if (addRotateBtn)   { addRotateBtn.onClick.RemoveAllListeners();   addRotateBtn.onClick.AddListener(OpenAddFurniturePanel);  SetBtnSprite(addRotateBtn,   spriteAdd); }
        if (moveConfirmBtn) { moveConfirmBtn.onClick.RemoveAllListeners(); moveConfirmBtn.onClick.AddListener(EnterPositionEditMode); SetBtnSprite(moveConfirmBtn, spriteMove); }

        // cancelMoveBtn: 천장 편집 진입 버튼으로 복원
        if (cancelMoveBtn)
        {
            cancelMoveBtn.onClick.RemoveAllListeners();
            cancelMoveBtn.onClick.AddListener(EnterCeilingEditMode);
            SetBtnSprite(cancelMoveBtn, spriteCeiling);
        }
    }

    // ── 책상 줌인 편집 ────────────────────────────────────────

    void EnterDeskEditMode()
    {
        if (deskObject == null) return;

        // 방 레벨 상태 백업
        _roomEditableItems  = editableItems;
        _savedRoomMinX      = roomMinX; _savedRoomMaxX = roomMaxX;
        _savedRoomMinZ      = roomMinZ; _savedRoomMaxZ = roomMaxZ;
        _savedFloorY        = floorY;
        _savedEditCamPosition = editCamPosition;
        _savedEditCamLookAt   = editCamLookAt;

        // 책상 콜라이더 bounds → 이동 가능 범위
        Bounds db = new Bounds(deskObject.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var col in deskObject.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled) continue;
            if (!hasBounds) { db = col.bounds; hasBounds = true; } else db.Encapsulate(col.bounds);
        }
        // bounds가 없으면 책상 pivot 기준 기본값
        if (!hasBounds) db = new Bounds(deskObject.transform.position, new Vector3(50f, 5f, 50f));

        roomMinX = db.min.x; roomMaxX = db.max.x;
        roomMinZ = db.min.z; roomMaxZ = db.max.z;
        floorY   = db.max.y; // 책상 윗면 Y를 바닥으로 사용

        // 카메라: offset 기반 로우앵글 (Inspector에서 deskCamOffset / deskCamLookOffset 조정)
        Vector3 deskCenter = db.center;
        editCamPosition = deskCenter + deskCamOffset;
        editCamLookAt   = deskCenter + deskCamLookOffset;

        // editableItems = deskItemParent(OnTable 컨테이너) 자식만, 없으면 deskObject 직계 자식
        var itemRoot = deskItemParent != null ? deskItemParent : deskObject.transform;
        var list = new System.Collections.Generic.List<FurnitureEditConfig>();
        foreach (Transform child in itemRoot)
            if (child.gameObject.activeSelf)
                list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = true, canDesign = true });
        editableItems = list.ToArray();

        _deskEditMode = true;

        // 기존 위치 편집 모드 UI·버튼 재사용
        EnterPositionEditMode();
    }

    void RestoreDeskState(bool lerpCamera = false)
    {
        editableItems   = _roomEditableItems;
        roomMinX        = _savedRoomMinX; roomMaxX = _savedRoomMaxX;
        roomMinZ        = _savedRoomMinZ; roomMaxZ = _savedRoomMaxZ;
        floorY          = _savedFloorY;
        editCamPosition = _savedEditCamPosition;
        editCamLookAt   = _savedEditCamLookAt;
        _deskEditMode   = false;

        if (lerpCamera)
            StartCoroutine(ReturnCameraToRoom(_savedEditCamPosition, _savedEditCamLookAt));
        // lerpCamera=false: Update()의 !_positionEditMode 브랜치가 즉시 카메라 고정
    }

    // ── 천장 편집 ─────────────────────────────────────────────

    public void EnterCeilingEditMode()
    {
        if (ceilingItemParent == null) return;

        _ceilingRoomItems = editableItems; // 방 가구 백업 (겹침 체크용)
        _ceilingActivated = new System.Collections.Generic.List<(GameObject, bool)>();
        var list = new System.Collections.Generic.List<FurnitureEditConfig>();
        foreach (Transform child in ceilingItemParent)
        {
            bool wasActive = child.gameObject.activeSelf;
            _ceilingActivated.Add((child.gameObject, wasActive));
            child.gameObject.SetActive(true);
            list.Add(new FurnitureEditConfig { target = child.gameObject, canMove = true, canDesign = true, ceilingMounted = true });
        }
        editableItems    = list.ToArray();
        _ceilingEditMode = true;

        // 조이스틱 끄기
        if (joystickObject != null) joystickObject.SetActive(false);

        // 바닥 숨기기 (ray 차단 방지)
        if (floorObject != null) floorObject.SetActive(false);

        // 천장 메시 켜기 (RoomModeManager가 EditMode 진입 시 꺼둔 것)
        var rmm = Object.FindObjectOfType<RoomModeManager>();
        if (rmm != null && rmm.ceilingObject != null) rmm.ceilingObject.SetActive(true);

        // 카메라 즉시 스냅
        if (_cam != null)
        {
            _cam.transform.position = ceilingCamPosition;
            _cam.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        }

        // 활성 콜라이더 없는 아이템에 MeshCollider 자동 추가
        foreach (var cfg in editableItems)
        {
            if (cfg.target == null) continue;
            bool hasActive = false;
            foreach (var col in cfg.target.GetComponentsInChildren<Collider>(true))
                if (col.enabled && col.gameObject.activeInHierarchy) { hasActive = true; break; }
            if (hasActive) continue;

            bool added = false;
            foreach (var mf in cfg.target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                added = true;
            }
            if (!added)
            {
                var box = cfg.target.AddComponent<BoxCollider>();
                box.size = new Vector3(20f, 10f, 20f);
            }
        }

        EnterPositionEditMode();
    }

    void RestoreCeilingState(bool deactivateItems)
    {
        if (deactivateItems && _ceilingActivated != null)
            foreach (var (go, wasActive) in _ceilingActivated)
                if (go != null) go.SetActive(wasActive);

        _ceilingEditMode  = false;
        _ceilingActivated = null;
        _ceilingRoomItems = null;

        // 조이스틱 복원 (EditMode 일반 상태로 — 조이스틱은 EditMode에서도 꺼져있어야 하므로 false 유지)
        // if (joystickObject != null) joystickObject.SetActive(false); // EditMode에선 계속 꺼둠

        // 바닥 복원
        if (floorObject != null) floorObject.SetActive(true);

        // 천장 메시 다시 끄기 (EditMode 원상복구)
        var rmm = Object.FindObjectOfType<RoomModeManager>();
        if (rmm != null && rmm.ceilingObject != null) rmm.ceilingObject.SetActive(false);

        RefreshEditableItems();
    }

    void RotateSelected(float angleDeg)
    {
        if (_selected == null) return;
        var cfg = GetConfig(_selected);
if (cfg != null && cfg.wallMounted) return; // Board 류는 회전 불가 (벽이 자동 회전)

        // 콜라이더 bounds 중심 기준으로 회전 (pivot이 치우쳐도 제자리 회전)
        Bounds b = new Bounds(_selected.transform.position, Vector3.zero);
        bool has = false;
        foreach (var col in _selected.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled) continue;
            if (!has) { b = col.bounds; has = true; } else b.Encapsulate(col.bounds);
        }
        Vector3 pivot = has ? new Vector3(b.center.x, _selected.transform.position.y, b.center.z)
                            : _selected.transform.position;

        _selected.transform.RotateAround(pivot, Vector3.up, angleDeg);

        // 회전 후 bounds 재계산 → 방 경계 밖으로 나갔으면 클램프
        b = new Bounds(_selected.transform.position, Vector3.zero);
        foreach (var col in _selected.GetComponentsInChildren<Collider>())
            if (col.enabled) b.Encapsulate(col.bounds);
        Vector3 p2c = b.center - _selected.transform.position;
        Vector3 c   = _selected.transform.position + p2c;
        c.x = Mathf.Clamp(c.x, roomMinX + b.extents.x, roomMaxX - b.extents.x);
        c.z = Mathf.Clamp(c.z, roomMinZ + b.extents.z, roomMaxZ - b.extents.z);
        _selected.transform.position = c - p2c;
    }

    void SetBtnSprite(Button btn, Sprite sprite)
    {
        if (btn == null || sprite == null) return;
        btn.image.sprite = sprite;
    }

    // ── 드래그 처리 ───────────────────────────────────────────

    void HandleDrag()
    {
        if (_selected == null) return;

        // 선택 가구 config를 한 번만 조회 (이후 _isCeiling / _isWall 변수로 재사용)
        var  selectedCfg = GetConfig(_selected);
        bool _isCeiling  = selectedCfg != null && selectedCfg.ceilingMounted;
        bool _isWall     = selectedCfg != null && selectedCfg.wallMounted;

        Vector2 inputPos = Vector2.zero;
        bool    began    = false;
        bool    hasInput = false;

        if (Touch.activeTouches.Count > 0)
        {
            foreach (var t in Touch.activeTouches)
            {
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _dragFingerId    = t.finger.index;
                    inputPos         = t.screenPosition;
                    began            = true;
                    hasInput         = true;
                    _waitForNewTouch = false; // 새 터치 시작 → 드래그 허용
                    break;
                }
                // Ended/Canceled 터치는 드래그 입력으로 처리하지 않음
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    t.phase == UnityEngine.InputSystem.TouchPhase.Canceled) continue;
                if (t.finger.index == _dragFingerId && !_waitForNewTouch)
                { inputPos = t.screenPosition; hasInput = true; break; }
                // Began 프레임을 버튼 탭이 소비해서 _dragFingerId가 아직 -1이면 Moved로 캡처
                // 단, 선택 전환 후 새 Began을 기다리는 중이면 차단
                // 천장 모드: 이미 누르고 있는 채로 선택될 수 있으므로 _waitForNewTouch 예외 허용
                if (_dragFingerId == -1 && (!_waitForNewTouch || _isCeiling) &&
                    (t.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                     t.phase == UnityEngine.InputSystem.TouchPhase.Stationary))
                {
                    _dragFingerId    = t.finger.index;
                    inputPos         = t.screenPosition;
                    hasInput         = true;
                    _waitForNewTouch = false; // Moved로 캡처됐으므로 드래그 허용 상태로 전환
                    break;
                }
            }
        }
        else if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            { inputPos = Mouse.current.position.ReadValue(); began = true; hasInput = true; _waitForNewTouch = false; }
            else if (Mouse.current.leftButton.isPressed)
            {
                // 천장 모드: _waitForNewTouch 상태여도 드래그 허용 (이미 누르고 있는 채로 선택됨)
                // 일반 모드: _waitForNewTouch=true면 차단 (새 Began 대기)
                if (!_waitForNewTouch || _isCeiling)
                {
                    inputPos         = Mouse.current.position.ReadValue();
                    hasInput         = true;
                    _waitForNewTouch = false; // 드래그 진입 시 플래그 해제
                }
            }
        }

        if (!hasInput) return;

        Ray ray = _cam.ScreenPointToRay(inputPos);

        // 드래그 시작 시점에 터치 위치가 다른 가구 위면 선택 전환 탭이므로 이동 차단
        // (Hanger는 XZ 위치 기반으로 즉시 벽에 스냅하기 때문에 이 체크가 필수)
        // ※ RaycastAll 사용: 단수 Raycast는 씬 지오메트리를 먼저 히트해 오판할 수 있음
        // ※ ceilingMounted: 카메라가 위를 향하므로 ray가 바닥 가구들을 먼저 통과 — guard 생략
        if (!_isCeiling && (began || !_dragOffsetInit))
        {
            RaycastHit[] initHits = Physics.RaycastAll(ray, Mathf.Infinity);
            System.Array.Sort(initHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var initHit in initHits)
            {
                var hitCfg = GetConfig(initHit.collider.gameObject);
                if (hitCfg == null) continue; // 등록되지 않은 씬 지오메트리 → 스킵
                if (hitCfg.target != _selected)
                {
                    // 다른 가구가 맞았음 → OnFurnitureClicked가 선택 전환 처리하므로 드래그 생략
                    _dragFingerId   = -1;
                    _dragOffsetInit = false;
                    return;
                }
                break; // 현재 선택 가구가 먼저 히트 → 드래그 허용
            }
        }

        // wallMounted 아이템: HandleHangerDrag 사용
        if (_isWall) { HandleHangerDrag(ray, began); return; }

        // ceilingMounted 아이템: HandleCeilingDrag 사용
        if (_isCeiling) { HandleCeilingDrag(ray, began); return; }

        // ── 일반 바닥 가구 드래그 ──
        // 아이템 현재 Y를 고정
        float furY = _selected.transform.position.y;
        if (!RaycastAtHeight(ray, furY, out Vector3 worldPos)) return;

        if (began || !_dragOffsetInit)
        {
            _dragOffset     = _selected.transform.position - new Vector3(worldPos.x, furY, worldPos.z);
            _dragOffsetInit = true;
            if (began) return; // 탭 첫 프레임: 오프셋만 초기화하고 이동 생략 (선택 전환 탭이 드래그로 이어지는 오류 방지)
        }

        Vector3 targetPos = new Vector3(worldPos.x + _dragOffset.x, furY, worldPos.z + _dragOffset.z);

        // 가구 콜라이더 bounds 기준으로 클램프 (pivot이 중앙인 큰 가구도 모서리가 벽에 정확히 막힘)
        Bounds furBounds = new Bounds(_selected.transform.position, Vector3.zero);
        foreach (var col in _selected.GetComponentsInChildren<Collider>())
            if (col.enabled) furBounds.Encapsulate(col.bounds);
        Vector3 pivotToCenter = furBounds.center - _selected.transform.position;
        Vector3 predictedCenter = targetPos + pivotToCenter;
        predictedCenter.x = Mathf.Clamp(predictedCenter.x, roomMinX + furBounds.extents.x, roomMaxX - furBounds.extents.x);
        predictedCenter.z = Mathf.Clamp(predictedCenter.z, roomMinZ + furBounds.extents.z, roomMaxZ - furBounds.extents.z);
        targetPos = predictedCenter - pivotToCenter;

        // 가구끼리 겹칠 때 빨간색
        bool overlap = OverlapsFurniture(targetPos);
        if (overlap != _isDragOverlap)
        {
            _isDragOverlap = overlap;
            SetDragHighlightColor(overlap ? Color.red : highlightColor);
        }

        _selected.transform.position = targetPos;
    }

    // ── Ceiling 전용: 천장 XZ 드래그 ─────────────────────────────

    /// <summary>
    /// 천장 높이(Y = ceilingY) 평면과 ray 교점을 구한다.
    /// 카메라가 완전히 수직이 아닐 때 화면 가장자리 ray가 아래를 향해 실패할 수 있으므로,
    /// 실패 시 카메라 정면 ray(forward)로 보정해 재시도한다.
    /// </summary>
    bool RaycastCeilingPlane(Ray ray, out Vector3 result)
    {
        if (RaycastAtHeight(ray, ceilingY, out result)) return true;

        // 보정: 카메라 위치에서 forward(= 거의 Vector3.up) 방향으로 쏜 ray 사용
        if (_cam != null)
        {
            Ray fallbackRay = new Ray(_cam.transform.position, _cam.transform.forward);
            if (RaycastAtHeight(fallbackRay, ceilingY, out result)) return true;

            // 최후 수단: 카메라 정위에서 수직으로 쏘기
            Ray verticalRay = new Ray(_cam.transform.position, Vector3.up);
            if (RaycastAtHeight(verticalRay, ceilingY, out result)) return true;
        }
        return false;
    }

    void HandleCeilingDrag(Ray ray, bool began = false)
    {
        // 아이템 실제 Y를 유지 (ceilingY로 강제하면 천장 위로 순간이동)
        float furY = _selected.transform.position.y;

        // 탭 첫 프레임은 무조건 건너뜀 — 선택 전환 탭이 이동으로 이어지는 오류 방지
        if (began)
        {
            if (RaycastAtHeight(ray, furY, out Vector3 beganPos))
            {
                _dragOffset     = _selected.transform.position - new Vector3(beganPos.x, furY, beganPos.z);
                _dragOffsetInit = true;
            }
            return;
        }

        if (!RaycastAtHeight(ray, furY, out Vector3 worldPos)) return;

        if (!_dragOffsetInit)
        {
            _dragOffset     = _selected.transform.position - new Vector3(worldPos.x, furY, worldPos.z);
            _dragOffsetInit = true;
        }

        Vector3 targetPos = new Vector3(worldPos.x + _dragOffset.x, furY, worldPos.z + _dragOffset.z);

        // 콜라이더 bounds 기준 방 경계 클램프
        Bounds furBounds = new Bounds(_selected.transform.position, Vector3.zero);
        foreach (var col in _selected.GetComponentsInChildren<Collider>())
            if (col.enabled) furBounds.Encapsulate(col.bounds);
        Vector3 pivotToCenter = furBounds.center - _selected.transform.position;
        Vector3 predictedCenter = targetPos + pivotToCenter;
        predictedCenter.x = Mathf.Clamp(predictedCenter.x, roomMinX + furBounds.extents.x, roomMaxX - furBounds.extents.x);
        predictedCenter.z = Mathf.Clamp(predictedCenter.z, roomMinZ + furBounds.extents.z, roomMaxZ - furBounds.extents.z);
        targetPos = predictedCenter - pivotToCenter;
        targetPos.y = furY; // 아이템 원래 Y 유지

        _selected.transform.position = targetPos;

        bool overlap = OverlapsFurniture(targetPos);
        if (overlap != _isDragOverlap)
        {
            _isDragOverlap = overlap;
            SetDragHighlightColor(overlap ? Color.red : highlightColor);
        }
    }

    // ── Hanger 전용: 벽면 슬라이딩 드래그 ───────────────────────

    void HandleHangerDrag(Ray ray, bool began = false)
    {
        // 탭 첫 프레임은 건너뜀 — 가구 전환 탭이 Board를 다른 벽으로 이동시키는 오류 방지
        if (began) return;

        // XZ 평면 교점 → 터치의 월드 XZ 위치 확보
        if (!RaycastAtHeight(ray, floorY, out Vector3 xzPos)) return;

        // 4개 벽까지의 거리 계산
        float distLeft  = xzPos.x - roomMinX;
        float distRight = roomMaxX - xzPos.x;
        float distBack  = xzPos.z - roomMinZ;
        float distFront = roomMaxZ - xzPos.z;

        float   minDist    = Mathf.Min(distLeft, distRight, distBack, distFront);
        Vector3 wallNormal = Vector3.forward;
        bool    isXWall    = false;
        float   wallX      = 0f;
        float   wallZ      = 0f;

        if (Mathf.Approximately(minDist, distLeft))
        {
            isXWall = true;  wallX = roomMinX;  wallNormal = Vector3.right;
        }
        else if (Mathf.Approximately(minDist, distRight))
        {
            isXWall = true;  wallX = roomMaxX;  wallNormal = Vector3.left;
        }
        else if (Mathf.Approximately(minDist, distBack))
        {
            isXWall = false; wallZ = roomMinZ;  wallNormal = Vector3.forward;
        }
        else
        {
            isXWall = false; wallZ = roomMaxZ;  wallNormal = Vector3.back;
        }

        // 회전 먼저 적용 → 이후 bounds가 실제 방향 기준으로 계산됨
        _selected.transform.rotation = Quaternion.LookRotation(wallNormal, Vector3.up);

        // Renderer bounds로 피벗 → 벽 접촉면 거리 계산 (콜라이더 없어도 항상 존재)
        Bounds rb = new Bounds(_selected.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in _selected.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { rb = r.bounds; hasBounds = true; }
            else rb.Encapsulate(r.bounds);
        }

        // wallNormal 부호에 따라 벽 접촉면 edge 선택 후, 그 edge가 wall 표면에 오도록 피벗 보정
        Vector3 targetPos;
        if (isXWall)
        {
            float edgeOffset = wallNormal.x > 0f
                ? _selected.transform.position.x - rb.min.x   // 왼쪽 벽: 피벗 → min.x
                : _selected.transform.position.x - rb.max.x;  // 오른쪽 벽: 피벗 → max.x
            targetPos = new Vector3(wallX + edgeOffset, _selected.transform.position.y, Mathf.Clamp(xzPos.z, roomMinZ, roomMaxZ));
        }
        else
        {
            float edgeOffset = wallNormal.z > 0f
                ? _selected.transform.position.z - rb.min.z   // 뒤쪽 벽: 피벗 → min.z
                : _selected.transform.position.z - rb.max.z;  // 앞쪽 벽: 피벗 → max.z
            targetPos = new Vector3(Mathf.Clamp(xzPos.x, roomMinX, roomMaxX), _selected.transform.position.y, wallZ + edgeOffset);
        }

        // Y 이동: 가구가 붙은 수직 벽 평면에 ray 교차해 Y 추출
        Plane wallPlane = new Plane(wallNormal, targetPos);
        if (wallPlane.Raycast(ray, out float wallEnter) && wallEnter > 0f)
            targetPos.y = Mathf.Clamp(ray.GetPoint(wallEnter).y, hangerMinY, hangerMaxY);
        else
            targetPos.y = Mathf.Clamp(targetPos.y, hangerMinY, hangerMaxY);

        _selected.transform.position = targetPos;

        // 겹침 체크
        bool overlap = OverlapsFurniture(targetPos);
        if (overlap != _isDragOverlap)
        {
            _isDragOverlap = overlap;
            SetDragHighlightColor(overlap ? Color.red : highlightColor);
        }
    }

    // ── 바닥 평면 교차 ────────────────────────────────────────

    /// <summary>editableItems 중 canMove=true인 다른 가구와 겹치면 true (빨간색용)</summary>
    // 겹침 감지 대상 판별
    // - 모든 가구: canMove=true
    // - 문: 이름에 Door 포함
    // - 유리(glassObject): Hanger(wallMounted) 한정으로만 감지
    bool IsOverlapTarget(FurnitureEditConfig cfg, bool selectedIsWallMounted = false)
    {
        if (cfg.target == null) return false;
        if (cfg.canMove) return true;
        if (cfg.target.name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        // 유리는 벽 부착 가구(wallMounted)에 한해서만 빨간색 감지
        if (selectedIsWallMounted && glassObject != null && cfg.target == glassObject) return true;
        return false;
    }

    bool OverlapsFurniture(Vector3 worldPos)
    {
        var selCfg = GetConfig(_selected);
        bool isWallMounted = selCfg != null && selCfg.wallMounted;

        Vector3 offset = worldPos - _selected.transform.position;
        foreach (var col in _selected.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled) continue;
            Vector3 center = col.bounds.center + offset;
            Vector3 half   = col.bounds.extents * 1.0f;
            half.y = Mathf.Min(half.y, 5f);
            foreach (var ov in Physics.OverlapBox(center, half, col.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ov == null || ov.transform.IsChildOf(_selected.transform)) continue;
                if (editableItems == null) continue;
                foreach (var cfg in editableItems)
                {
                    if (cfg.target == _selected) continue;
                    if (!IsOverlapTarget(cfg, isWallMounted)) continue;
                    if (ov.transform.IsChildOf(cfg.target.transform)) return true;
                }
                // 천장 편집 모드: 방 가구와의 겹침도 체크
                if (_ceilingEditMode && _ceilingRoomItems != null)
                    foreach (var cfg in _ceilingRoomItems)
                    {
                        if (cfg.target == null || cfg.target == _selected) continue;
                        if (!IsOverlapTarget(cfg, isWallMounted)) continue;
                        if (ov.transform.IsChildOf(cfg.target.transform)) return true;
                    }
            }
        }
        return false;
    }

    /// <summary>전체 canMove 가구(+문) 중 겹치는 쌍이 하나라도 있으면 true — 저장 가능 여부 판단용</summary>
    bool HasAnyOverlap()
    {
        if (editableItems == null) return false;
        foreach (var cfg in editableItems)
        {
            if (cfg.target == null || !cfg.canMove) continue; // 이동 가능 가구만 검사 주체
            foreach (var col in cfg.target.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled) continue;
                Vector3 half = col.bounds.extents * 1.0f; // 더 작게 — 오탐 방지
                half.y = Mathf.Min(half.y, 5f);
                foreach (var ov in Physics.OverlapBox(col.bounds.center, half, col.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ov == null || ov.transform.IsChildOf(cfg.target.transform)) continue;
                    foreach (var other in editableItems)
                    {
                        if (other.target == null || other.target == cfg.target) continue;
                        if (!IsOverlapTarget(other, cfg.wallMounted)) continue; // 가구 + 문 + 유리(wallMounted 한정)
                        if (ov.transform.IsChildOf(other.target.transform)) return true;
                    }
                }
            }
        }
        return false;
    }

    void SetDragHighlightColor(Color color)
    {
        if (_selected == null) return;
        foreach (var r in _selected.GetComponentsInChildren<Renderer>())
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color baseColor = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor")
                ? r.sharedMaterial.GetColor("_BaseColor") : Color.white;
            block.SetColor("_BaseColor", Color.Lerp(baseColor, color, highlightIntensity));
            r.SetPropertyBlock(block);
        }
    }

    bool RaycastAtHeight(Ray ray, float height, out Vector3 result)
    {
        float denom = ray.direction.y;
        if (Mathf.Abs(denom) < 0.0001f) { result = Vector3.zero; return false; }
        float t = (height - ray.origin.y) / denom;
        if (t < 0f) { result = Vector3.zero; return false; }
        result = ray.origin + ray.direction * t;
        return true;
    }

    // ── 그리드 스냅 ───────────────────────────────────────────

    Vector3 SnapToGrid(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / gridCellSize) * gridCellSize,
            pos.y,
            Mathf.Round(pos.z / gridCellSize) * gridCellSize);
    }

    // ── 유틸 ─────────────────────────────────────────────────

    public bool IsEditable(GameObject go) => GetConfig(go) != null;

    /// <summary>클릭한 오브젝트가 속한 등록 target을 반환 (CameraController에서 호출)</summary>
    public GameObject GetEditTarget(GameObject go)
    {
        var cfg = GetConfig(go);
        return cfg?.target;
    }

    FurnitureEditConfig GetConfig(GameObject go)
    {
        if (editableItems == null) return null;
        foreach (var cfg in editableItems)
        {
            if (cfg.target == null) continue;
            if (go == cfg.target || go.transform.IsChildOf(cfg.target.transform)) return cfg;
        }
        return null;
    }
}

[System.Serializable]
public class FurnitureEditConfig
{
    public GameObject target;
    public bool       canMove     = true;   // false = 이동 불가 (벽, 문 등)
    public bool       canDesign   = true;   // false = 디자인 변경 불가
    public bool       wallMounted    = false; // true = 벽면 슬라이딩 드래그 사용 (Board 류)
    public bool       ceilingMounted = false; // true = 천장 XZ 드래그 사용 (Ceiling 류)
                                              // RefreshEditableItems()에서 이름 기반 자동 세팅
}
