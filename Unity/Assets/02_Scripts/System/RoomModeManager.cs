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

    public static void SetMode(RoomMode mode) { CurrentMode = mode; }

    [Header("디버그 — Inspector에서 모드 설정 (런타임에서도 적용됨)")]
    [SerializeField] private RoomMode _debugMode = RoomMode.MyRoom;
    private RoomMode _prevDebugMode;

    [Header("EditMode 전용")]
    [Tooltip("천장 오브젝트 — EditMode에서 숨김")]
    public GameObject ceilingObject;
    [Tooltip("플레이어 오브젝트 — EditMode에서 숨김")]
    public GameObject playerObject;

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
        // Inspector 값으로 static CurrentMode 덮어쓰기 (디버그용)
        CurrentMode   = _debugMode;
        _prevDebugMode = _debugMode;
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
                if (ceilingObject != null) ceilingObject.SetActive(true);
                if (playerObject  != null) playerObject.SetActive(true);
                // host 없음
                if (hostObject       != null) hostObject.SetActive(false);
                // 댓글 추가 버튼 없음
                if (commentAddButton != null) commentAddButton.SetActive(false);
                // 포스트잇 스폰 없음
                if (postItManager    != null) postItManager.enabled = false;
                // 내 방이니까 자신 줌인 가능
                if (playerSelfController != null) playerSelfController.gameObject.SetActive(true);

                Debug.Log("[RoomMode] MyRoom 적용");
                break;

            case RoomMode.VisitRoom:
                if (ceilingObject != null) ceilingObject.SetActive(true);
                if (playerObject  != null) playerObject.SetActive(true);
                // host 있음
                if (hostObject       != null) hostObject.SetActive(true);
                // 댓글 추가 버튼 있음
                if (commentAddButton != null) commentAddButton.SetActive(true);
                // 포스트잇 스폰 있음
                if (postItManager    != null) postItManager.enabled = true;
                // 남의 방이니까 자신 줌인 없음
                if (playerSelfController != null) playerSelfController.gameObject.SetActive(false);

                Debug.Log("[RoomMode] VisitRoom 적용");
                break;

            case RoomMode.EditMode:
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
