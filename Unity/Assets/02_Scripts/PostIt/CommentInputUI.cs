using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 댓글 작성 시스템.
//   1) Sticky_note_yellow (board 자식) → 댓글 추가 버튼으로 자동 연결 (태그 + 콜라이더)
//      클릭 감지는 CameraController.CheckBoardClick → TogglePanel()
//   2) 댓글 추가 버튼 클릭 → postitPanel + backgroundPanel 띄움
//      backgroundPanel 클릭 → 둘 다 닫고 원래 화면
//      등록 버튼 클릭 → 댓글 추가 + 닫기
//
// 사용법: 아무 GameObject에 이 스크립트 추가 →
//   Inspector에서 postitPanel / backgroundPanel / inputField / registerButton 연결.
public class CommentInputUI : MonoBehaviour
{
    public static CommentInputUI Instance { get; private set; }
    public bool IsPanelOpen => postitPanel != null && postitPanel.activeSelf;

    [Header("연동")]
    public PostItManager postItManager;
    [Tooltip("작성자 표시명 (추후 로그인 유저로 대체)")]
    public string currentUsername = "방문자";

    [Header("제한")]
    public int maxCharacters = 50;

    [Header("댓글 추가 버튼")]
    [Tooltip("씬 Hierarchy에서 Sticky_note_yellow를 여기 직접 드래그 연결")]
    public GameObject addButtonObject;

    [Header("댓글 입력 UI (직접 만든 것 연결)")]
    [Tooltip("댓글 입력 패널 (포스트잇 모양) — 평소 비활성, 버튼 클릭 시 활성")]
    public GameObject postitPanel;
    [Tooltip("배경 패널 (어둡게) — 누르면 닫힘. 평소 비활성")]
    public GameObject backgroundPanel;
    [Tooltip("배경 패널의 Button (비우면 backgroundPanel에서 자동 검색/추가)")]
    public Button backgroundButton;
    [Tooltip("댓글 입력 InputField (50자 제한 자동 적용)")]
    public TMP_InputField inputField;
    [Tooltip("등록 버튼 — 누르면 댓글 추가 + 패널 닫기")]
    public Button registerButton;

    private GameObject _addButton;

    void Awake()
    {
        Instance = this;

        // ★ EventSystem 보장 — 없으면 UI Button 클릭이 절대 안 들어옴
        if (FindObjectOfType<EventSystem>() == null)
        {
            var go = new GameObject("EventSystem (auto)");
            go.AddComponent<EventSystem>();
            // 새 Input System 패키지 있으면 그쪽 모듈, 없으면 StandaloneInputModule
            var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) go.AddComponent(t);
            else           go.AddComponent<StandaloneInputModule>();
            Debug.Log("[CommentInputUI] EventSystem 자동 생성 — UI 클릭 활성화됨");
        }

        // 시작 즉시 패널 숨김 — 사용자가 에디터에서 켜둬도 런타임에선 닫힌 상태로 시작
        if (postitPanel     != null) postitPanel.SetActive(false);
        if (backgroundPanel != null) backgroundPanel.SetActive(false);
    }

    void Start()
    {
        if (postItManager == null) postItManager = FindObjectOfType<PostItManager>();

        WireAddButton();
        WireWindow();
    }

    // ── Sticky_note_yellow 댓글 버튼 연결 ────────────────────────────────────
    void WireAddButton()
    {
        _addButton = addButtonObject;

        // Inspector 미연결 시 board 자식에서 자동 검색
        if (_addButton == null)
        {
            Transform boardT = postItManager?.board;
            if (boardT != null)
            {
                foreach (Transform t in boardT.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Sticky_note_yellow") { _addButton = t.gameObject; break; }
                }
            }
        }

        if (_addButton == null)
        {
            Debug.LogWarning("[CommentInputUI] Sticky_note_yellow 못 찾음 — Inspector의 Add Button Object에 직접 연결하세요");
            return;
        }
        Debug.Log($"[CommentInputUI] 댓글 버튼 연결됨 — '{_addButton.name}' parent={_addButton.transform.parent?.name}");

        // 1) UI Button 클릭 연결 — 사용자가 Sticky_note_yellow 안에 넣은 모든 Button을 TogglePanel에 연결
        var uiButtons = _addButton.GetComponentsInChildren<Button>(true);
        foreach (var b in uiButtons)
            b.onClick.AddListener(TogglePanel);
        Debug.Log($"[CommentInputUI] 댓글 추가 버튼 UI Button {uiButtons.Length}개 onClick 연결");

        // 2) 3D 물리 raycast 감지 — CommentButtonMarker + BoxCollider
        if (_addButton.GetComponent<CommentButtonMarker>() == null)
            _addButton.AddComponent<CommentButtonMarker>();

        // Inspector에서 직접 추가한 콜라이더가 있으면 그것을 사용.
        // 없으면 Renderer.bounds(실제 보이는 영역) 기준으로 월드→로컬 변환해서 BoxCollider 생성.
        if (_addButton.GetComponent<Collider>() == null)
        {
            var rend = PostItCanvasHelper.FindBodyRenderer(_addButton);
            var bc   = _addButton.AddComponent<BoxCollider>();
            if (rend != null)
            {
                // 실제 렌더러 월드 bounds → 루트 로컬 공간으로 변환 (피벗 오프셋 무관하게 정확)
                Bounds wb  = rend.bounds;
                Transform rootT = _addButton.transform;
                // 8 코너를 루트 로컬로 변환해 axis-aligned bounds 계산
                Vector3 wc = wb.center;
                Vector3 we = wb.extents;
                Vector3[] corners = {
                    wc + new Vector3(-we.x,-we.y,-we.z), wc + new Vector3(+we.x,-we.y,-we.z),
                    wc + new Vector3(-we.x,+we.y,-we.z), wc + new Vector3(+we.x,+we.y,-we.z),
                    wc + new Vector3(-we.x,-we.y,+we.z), wc + new Vector3(+we.x,-we.y,+we.z),
                    wc + new Vector3(-we.x,+we.y,+we.z), wc + new Vector3(+we.x,+we.y,+we.z),
                };
                Bounds local = new Bounds(rootT.InverseTransformPoint(corners[0]), Vector3.zero);
                for (int i = 1; i < 8; i++) local.Encapsulate(rootT.InverseTransformPoint(corners[i]));
                bc.center = local.center;
                bc.size   = local.size;
                Debug.Log($"[CommentInputUI] '{_addButton.name}' BoxCollider 자동 생성 — center={bc.center} size={bc.size}");
            }
            else
            {
                Debug.LogWarning($"[CommentInputUI] '{_addButton.name}' Renderer 없음 — Inspector에서 BoxCollider를 직접 추가·조정하세요");
            }
        }
        else
        {
            Debug.Log($"[CommentInputUI] '{_addButton.name}' 기존 콜라이더 사용");
        }

        // 3) 댓글 버튼 위에 포스트잇 안 겹치게 — 버튼 위치를 점유 목록에 등록
        if (postItManager != null)
        {
            var rend = PostItCanvasHelper.FindBodyRenderer(_addButton);
            Vector3 occupiedPos = rend != null ? rend.bounds.center : _addButton.transform.position;
            postItManager.MarkOccupied(occupiedPos);
            Debug.Log($"[CommentInputUI] 댓글 버튼 위치 점유 등록 — {occupiedPos}");
        }

        Debug.Log($"[CommentInputUI] 댓글 추가 버튼 연결 완료 — {_addButton.name}");
    }

    // ── 입력 UI 연결 ───────────────────────────────────────────────────────
    void WireWindow()
    {
        if (inputField != null)
        {
            inputField.characterLimit = maxCharacters;
            inputField.lineType       = TMP_InputField.LineType.MultiLineNewline;
        }

        // 노란 패널 클릭 흡수 — 뒤의 배경 버튼까지 이벤트 전달 방지
        if (postitPanel != null && postitPanel.GetComponent<Button>() == null)
            postitPanel.AddComponent<Button>().transition = Selectable.Transition.None;

        // 등록 버튼 → 댓글 추가
        if (registerButton != null) registerButton.onClick.AddListener(Confirm);

        // 배경 패널 클릭 → 닫기. backgroundButton 미연결이면 backgroundPanel에서 찾음
        if (backgroundButton == null && backgroundPanel != null)
            backgroundButton = backgroundPanel.GetComponent<Button>();
        if (backgroundButton != null)
            backgroundButton.onClick.AddListener(ClosePanel);
        else
            Debug.LogWarning("[CommentInputUI] backgroundButton 미연결 — 배경 패널에 Button 컴포넌트를 추가하고 연결하세요");

        // 평소엔 둘 다 꺼둠
        if (postitPanel     != null) postitPanel.SetActive(false);
        if (backgroundPanel != null) backgroundPanel.SetActive(false);

        if (postitPanel == null || inputField == null || registerButton == null)
            Debug.LogWarning("[CommentInputUI] 입력 UI 미연결 — Inspector에서 " +
                             "postitPanel / inputField / registerButton 연결 필요");
    }

    // UI Button.onClick 또는 CameraController(3D raycast)가 호출 — 토글
    public void TogglePanel()
    {
        Debug.Log($"[CommentInputUI] TogglePanel 호출 — postitPanel연결={postitPanel != null} 현재상태={(postitPanel != null ? postitPanel.activeSelf.ToString() : "null")}");
        if (postitPanel == null)
        {
            Debug.LogWarning("[CommentInputUI] postitPanel 미연결 — Inspector에서 연결하세요");
            return;
        }

        if (postitPanel.activeSelf) ClosePanel();
        else                        OpenPanel();
    }

    // 댓글 추가 버튼 클릭 → postitPanel + backgroundPanel 띄움
    void OpenPanel()
    {
        if (inputField != null) inputField.text = "";
        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(true);
            backgroundPanel.transform.SetAsFirstSibling(); // 배경은 맨 뒤
        }
        if (postitPanel != null)
        {
            postitPanel.SetActive(true);
            postitPanel.transform.SetAsLastSibling(); // 노란 패널은 맨 앞
        }
        if (inputField != null) inputField.ActivateInputField();
    }

    // backgroundPanel 클릭 → 창 닫고 Board 상태로 복귀
    void ClosePanel()
    {
        if (postitPanel     != null) postitPanel.SetActive(false);
        if (backgroundPanel != null) backgroundPanel.SetActive(false);
        BoardFocusController.Instance?.BackToBoard();
    }

    // 등록 버튼 클릭 → 댓글 추가
    void Confirm()
    {
        if (inputField == null) return;

        string text = inputField.text?.Trim();
        if (string.IsNullOrEmpty(text)) return;   // 내용 없으면 등록 안 함 (창 유지)
        if (text.Length > maxCharacters) text = text.Substring(0, maxCharacters);

        // 랜덤 위치/머티리얼 포스트잇 추가 (PostItManager가 처리)
        if (postItManager != null)
        {
            postItManager.AddComment(currentUsername, text);
            Debug.Log($"[CommentInputUI] 댓글 등록 완료 — 작성자={currentUsername} 내용={text}");
        }

        // ════════════════════════════════════════════════════════════════════
        // ★★★ TODO: 백엔드 연동 — 추후 백엔드 담당이 구현 ★★★
        // 댓글 작성 시 DB에 저장하는 로직을 여기에 호출.
        // 프론트는 함수 호출만, 실제 DB 통신은 백엔드 모듈/매니저에 위임.
        // 예: BackendBridge.SaveComment(roomId, currentUsername, text);
        // ════════════════════════════════════════════════════════════════════

        ClosePanel();
    }
}
