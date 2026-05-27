using UnityEngine;

// ★ 씬 이동 시 반드시 먼저 모드 설정 후 로드할 것:
//   RoomModeManager.SetMode(RoomMode.VisitRoom);  // 남의 방
//   RoomModeManager.SetMode(RoomMode.MyRoom);     // 내 방 (디폴트)
//   SceneManager.LoadScene("3DRoomScene");
public class RoomModeManager : MonoBehaviour
{
    public static RoomModeManager Instance { get; private set; }

    // 씬 로드 전에 SetMode()로 설정 — static이라 씬 이동해도 유지
    public static RoomMode CurrentMode { get; private set; } = RoomMode.MyRoom;

    // SetMode()가 외부에서 호출됐는지 추적 — true면 Awake에서 _debugMode로 덮어쓰지 않음
    private static bool _modeSetExternally = false;

    public static void SetMode(RoomMode mode)
    {
        CurrentMode        = mode;
        _modeSetExternally = true;
    }

    [Header("디버그 — Inspector에서 모드 설정 (런타임에서도 적용됨)")]
    [SerializeField] private RoomMode _debugMode = RoomMode.MyRoom;
    private RoomMode _prevDebugMode;

    [Header("EditMode 전용")]
    [Tooltip("천장 오브젝트 — EditMode에서 숨김")]
    public GameObject ceilingObject;
    [Tooltip("플레이어 오브젝트 — EditMode에서 숨김")]
    public GameObject playerObject;
    [Tooltip("조이스틱 area — EditMode에서 끔")]
    public GameObject joystickArea;
    [Tooltip("가구 편집 UI 패널 — EditMode에서만 켜짐")]
    public GameObject furnitureEditUI;

    [Header("MyRoom / VisitRoom 공통")]
    [Tooltip("host 캐릭터 오브젝트 — MyRoom에서 비활성")]
    public GameObject hostObject;

    [Tooltip("Sticky_note_yellow (댓글 추가 버튼) — MyRoom에서 비활성")]
    public GameObject commentAddButton;

    [Tooltip("PostItManager — MyRoom에서 비활성 (포스트잇 스폰 안 함)")]
    public PostItManager postItManager;

    [Tooltip("플레이어 자신 줌인 UI — VisitRoom에서 비활성")]
    public PlayerSelfController playerSelfController;

    void Awake()
    {
        Instance = this;

        if (_modeSetExternally)
        {
            // 씬 이동 전에 SetMode()로 명시적으로 설정된 경우 → _debugMode로 덮어쓰지 않음
            // _debugMode는 현재 CurrentMode에 맞게 동기화만 해둠 (Inspector 값 변경 감지용)
            _debugMode     = CurrentMode;
            _prevDebugMode = CurrentMode;
            _modeSetExternally = false; // 다음 씬 이동을 위해 리셋
        }
        else
        {
            // 직접 씬을 Play한 경우(에디터 테스트) → Inspector _debugMode 사용
            CurrentMode    = _debugMode;
            _prevDebugMode = _debugMode;
        }

        ApplyMode();
    }

    void Update()
    {
        // Inspector에서 값 바꾸면 런타임에도 즉시 재적용
        if (_debugMode != _prevDebugMode)
        {
            CurrentMode    = _debugMode;
            _prevDebugMode = _debugMode;
            ApplyMode();
        }
    }

    void ApplyMode()
    {
        switch (CurrentMode)
        {
            case RoomMode.MyRoom:
                if (ceilingObject    != null) ceilingObject.SetActive(true);
                if (playerObject     != null) playerObject.SetActive(true);
                if (joystickArea     != null) joystickArea.SetActive(true);
                if (furnitureEditUI  != null) furnitureEditUI.SetActive(false);
                if (hostObject       != null) hostObject.SetActive(false);
                if (commentAddButton != null) commentAddButton.SetActive(false);
                if (postItManager    != null) postItManager.enabled = false;
                if (playerSelfController != null) playerSelfController.gameObject.SetActive(true);
                Debug.Log("[RoomMode] MyRoom 적용");
                break;

            case RoomMode.VisitRoom:
                if (ceilingObject    != null) ceilingObject.SetActive(true);
                if (playerObject     != null) playerObject.SetActive(true);
                if (joystickArea     != null) joystickArea.SetActive(true);
                if (furnitureEditUI  != null) furnitureEditUI.SetActive(false);
                if (hostObject       != null) hostObject.SetActive(true);
                if (commentAddButton != null) commentAddButton.SetActive(true);
                if (postItManager    != null) postItManager.enabled = true;
                if (playerSelfController != null) playerSelfController.gameObject.SetActive(false);
                Debug.Log("[RoomMode] VisitRoom 적용");
                break;

            case RoomMode.EditMode:
                if (joystickArea     != null) joystickArea.SetActive(false);
                if (furnitureEditUI  != null) furnitureEditUI.SetActive(true);
                if (hostObject       != null) hostObject.SetActive(false);
                if (commentAddButton != null) commentAddButton.SetActive(false);
                if (postItManager    != null) postItManager.enabled = false;
                if (playerSelfController != null) playerSelfController.gameObject.SetActive(false);
                if (ceilingObject    != null) ceilingObject.SetActive(false);
                if (playerObject     != null) playerObject.SetActive(false);
                FurnitureEditController.Instance?.EnterEditMode();
                Debug.Log("[RoomMode] EditMode 적용");
                break;
        }
    }
}
