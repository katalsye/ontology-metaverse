using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Firebase.Extensions;

/// <summary>
/// Closet 씬 저장 버튼 로직 (가구 + 캐릭터 통합)
///
/// [저장 클릭 시]
/// ① 변경사항 있음 + 전부 구매 → 저장(주석) + Panel1 표시 → 확인 버튼으로 닫기
/// ② 변경사항 있음 + 미구매 있음 → Panel2 표시
///      Button1: 상점 씬 이동 (현재는 SampleScene)
///      Button2: Panel2만 닫기
/// ③ 변경사항 없음 → 아무것도 하지 않음
///
/// [Panel2 "어디 눌러도 같은 버튼" 버그 진짜 원인 — 2026-05-28 재진단]
///  panel2GoShopBtn / panel2CancelBtn 자체 RectTransform은 343x100으로 분리되어 있지만,
///  각 Button의 **child 텍스트 RectTransform이 stretch anchor + LocalScale(10,10,1)**
///  로 설정되어 있어 실제 화면상 child Graphic 영역이 3430x1000까지 확장됨.
///  이 child Graphic의 raycastTarget이 켜져 있으면 Panel2 전 영역(및 그 너머)의
///  클릭이 child를 통해 부모 Button.onClick을 트리거함. 두 child 모두 거대하므로
///  Hierarchy에서 **나중에 그려진(아래쪽) child의 부모 버튼**이 모든 클릭을 독점함.
///  → 씬 수정 없이 코드로 두 버튼의 child Graphic raycastTarget을 모두 OFF 처리.
///    부모 Button의 self Graphic(343x100)만 raycast를 받도록 정상화.
/// </summary>
public class ClosetSaveUI : MonoBehaviour
{
    [Header("저장 버튼")]
    public Button saveButton;

    [Header("블로커 (팝업 뒤 전체화면 투명 패널 — Raycast Target ON)")]
    public GameObject blockerPanel;

    [Header("Panel1 — 저장 완료")]
    public GameObject panel1;
    public Button     panel1ConfirmBtn;

    [Header("Panel2 — 미구매 안내")]
    public GameObject panel2;
    public Button     panel2GoShopBtn;  // 상점으로
    public Button     panel2CancelBtn;  // 그냥 닫기

    [Header("참조")]
    public FurnitureCarouselController furnitureCarousel;
    public HatSelectionUI              hatSelectionUI;    // 모자 구매 여부 체크용

    [Header("씬")]
    [Tooltip("상점 씬 이름 — Build Settings에 등록된 씬 이름으로 Inspector에서 지정")]
    public string shopSceneName = "Shop";

    // ── 팝업 상태 (스와이프 차단용) ──────────────────────────
    public static bool IsPopupOpen { get; private set; }

    // ── 변경사항 플래그 ───────────────────────────────────────

    bool _furnitureDirty = false;
    bool _playerDirty    = false;

    bool IsDirty => _furnitureDirty || _playerDirty;

    // ── Panel2 중복 클릭 가드 ─────────────────────────────────
    // 두 버튼이 겹쳐 한 클릭으로 두 콜백이 모두 트리거되는 경우를 위한 1-shot 플래그.
    // ShowPanel(panel2)에서 false로 리셋되고, 어느 한쪽 버튼이 처리되면 true가 되어
    // 같은 프레임에 들어오는 다른 콜백을 전부 무시한다.
    bool _panel2Handled = false;

    // ── 초기화 ────────────────────────────────────────────────

    void Start()
    {
        // RemoveAllListeners 후 등록 — 중복 방지
        if (saveButton) { saveButton.onClick.RemoveAllListeners(); saveButton.onClick.AddListener(OnSaveClick); }

        if (panel1ConfirmBtn) panel1ConfirmBtn.onClick.AddListener(() => ClosePanel(panel1));

        if (panel2GoShopBtn)
        {
            panel2GoShopBtn.onClick.RemoveAllListeners();
            panel2GoShopBtn.onClick.AddListener(OnGoShopGuarded);
        }

        if (panel2CancelBtn)
        {
            panel2CancelBtn.onClick.RemoveAllListeners();
            panel2CancelBtn.onClick.AddListener(OnCancelGuarded);
        }

        // ── Panel2 버튼 child raycast 봉인 (핵심 버그 수정) ──
        // 두 버튼의 child Graphic이 LocalScale(10,10,1) + stretch anchor로
        // 화면 전체급으로 부풀어 있어 raycast를 가로채고 있음.
        // self(부모 Button의 Image, 343x100)만 raycast 받도록 child 전부 OFF.
        SealButtonChildRaycast(panel2GoShopBtn);
        SealButtonChildRaycast(panel2CancelBtn);

        blockerPanel?.SetActive(false);
        panel1?.SetActive(false);
        panel2?.SetActive(false);
    }

    /// <summary>
    /// 버튼의 모든 자손 Graphic의 raycastTarget을 OFF한다.
    /// 버튼 자기 자신(루트)의 Graphic은 raycast를 유지해 클릭 영역으로 동작.
    /// </summary>
    void SealButtonChildRaycast(Button btn)
    {
        if (btn == null) return;
        var rootGo = btn.gameObject;
        var graphics = btn.GetComponentsInChildren<Graphic>(true);
        int sealedCount = 0;
        foreach (var g in graphics)
        {
            if (g == null) continue;
            if (g.gameObject == rootGo) continue; // 부모 Button 자체는 클릭 영역
            if (g.raycastTarget)
            {
                g.raycastTarget = false;
                sealedCount++;
            }
        }
    }

    // ── Panel2 버튼 가드 래퍼 ─────────────────────────────────

    void OnGoShopGuarded()
    {
        if (_panel2Handled)
        {
            // Debug.LogWarning("[ClosetSaveUI] OnGoShop 중복 호출 차단 (_panel2Handled=true)");
            return;
        }
        _panel2Handled = true;
        LockPanel2Buttons();
        OnGoShop();
    }

    void OnCancelGuarded()
    {
        if (_panel2Handled)
        {
            // Debug.LogWarning("[ClosetSaveUI] OnCancel 중복 호출 차단 (_panel2Handled=true)");
            return;
        }
        _panel2Handled = true;
        LockPanel2Buttons();
        // Debug.Log("[ClosetSaveUI] 아니오(Cancel) 클릭");
        ClosePanel(panel2);
    }

    void LockPanel2Buttons()
    {
        if (panel2GoShopBtn) panel2GoShopBtn.interactable = false;
        if (panel2CancelBtn) panel2CancelBtn.interactable = false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void UnlockPanel2Buttons()
    {
        if (panel2GoShopBtn) panel2GoShopBtn.interactable = true;
        if (panel2CancelBtn) panel2CancelBtn.interactable = true;
    }

    // ── 저장 버튼 ─────────────────────────────────────────────

    void OnSaveClick()
    {
        // TODO: dirty 체크 복원 → if (!IsDirty) return;
        if (HasUnpurchased())
            StartCoroutine(ShowPanel(panel2));   // ② 미구매
        else
        {
            DoSave();
            StartCoroutine(ShowPanel(panel1));   // ① 저장 완료
        }
    }

    // 1프레임 대기 후 블로커 + 패널 열기
    IEnumerator ShowPanel(GameObject panel)
    {
        EventSystem.current?.SetSelectedGameObject(null);
        yield return null;
        IsPopupOpen = true;
        blockerPanel?.SetActive(true);
        panel.SetActive(true);
        // 블로커보다 panel이 반드시 위에 렌더링되도록 Hierarchy 순서 강제
        panel.transform.SetAsLastSibling();

        // Panel2를 여는 시점에만 가드 리셋 + interactable 복구
        if (panel == panel2)
        {
            _panel2Handled = false;
            UnlockPanel2Buttons();
        }
    }

    public void ClosePanel1() => ClosePanel(panel1);
    public void ClosePanel2() => ClosePanel(panel2);

    void ClosePanel(GameObject panel)
    {
        panel.SetActive(false);
        blockerPanel?.SetActive(false);
        IsPopupOpen = false;
    }

    void DoSave()
    {
        _furnitureDirty = false;
        _playerDirty    = false;

        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        var db   = Firebase.Firestore.FirebaseFirestore.DefaultInstance;
        if (auth?.CurrentUser == null) return;

        string uid = auth.CurrentUser.UserId;
        var data = new System.Collections.Generic.Dictionary<string, object>();

        if (furnitureCarousel?.data?.furnitures != null)
        {
            int fi = furnitureCarousel.CurrentIndex;
            int vi = furnitureCarousel.CurrentVariant;
            if (fi < furnitureCarousel.data.furnitures.Length)
            {
                data["furnitureIndex"] = fi;
                data["variantIndex"]   = vi;
                data["furnitureName"]  = furnitureCarousel.data.furnitures[fi].displayName;
            }
        }

        if (hatSelectionUI != null)
            data["hatIndex"] = hatSelectionUI.GetSelectedIndex();

        db.Collection("users").Document(uid)
          .UpdateAsync("closetConfig", data)
          .ContinueWithOnMainThread(t =>
          {
              if (t.IsFaulted) Debug.LogError("[ClosetSaveUI] 저장 실패: " + t.Exception);
              else Debug.Log("[ClosetSaveUI] 저장 완료");
          });
    }

    // ── 미구매 체크 ───────────────────────────────────────────

    bool HasUnpurchased()
    {
        // 가구 미구매 체크
        if (furnitureCarousel?.data?.furnitures != null)
        {
            var fi       = furnitureCarousel.CurrentIndex;
            var vi       = furnitureCarousel.CurrentVariant;
            var variants = furnitureCarousel.data.furnitures[fi].variants;
            if (variants != null && vi < variants.Length && !variants[vi].isPurchased)
            {
                return true;
            }
        }

        // 모자 미구매 체크
        if (hatSelectionUI == null)
        {
        }
        else if (!hatSelectionUI.IsCurrentHatPurchased())
        {
            return true;
        }

        return false;
    }

    // ── 상점 이동 ─────────────────────────────────────────────

    public void OnGoShop()
    {
        // Debug.Log($"<color=lime>[ClosetSaveUI] OnGoShop() 호출됨!</color> 현재 씬='{SceneManager.GetActiveScene().name}', 이동 대상='{shopSceneName}'");
        ClosePanel(panel2);

        // ── 씬 이동 가능 여부 체크 ──────────────────────────────
        // Build Settings에 등록된 씬인지 먼저 확인 (등록 안 됐으면 LoadScene이 조용히 실패함)
        bool canLoad = !string.IsNullOrEmpty(shopSceneName)
                       && Application.CanStreamedLevelBeLoaded(shopSceneName);

        if (canLoad)
        {
            SceneManager.LoadScene(shopSceneName);
            return;
        }


        // ── 폴백 1: Build Settings의 첫 번째 씬으로 이동 (현재 씬이 아니면) ──
        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            int activeIdx = SceneManager.GetActiveScene().buildIndex;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if (i == activeIdx) continue;
                SceneManager.LoadScene(i);
                return;
            }
        }

        // ── 폴백 2: 씬 이동 불가 → 사용자에게 시각적으로 OnGoShop이 실행됐음을 알림 ──
        StartCoroutine(FlashGoShopFeedback());
    }

    // OnGoShop이 실제로 실행됐음을 시각적으로 확인시키는 폴백 피드백
    IEnumerator FlashGoShopFeedback()
    {
        if (panel2 == null) yield break;

        var img = panel2.GetComponent<Image>();
        Color original = img ? img.color : Color.white;

        // panel2를 다시 열어서 초록색으로 깜빡임 → 아니오 동작과 명확히 구분
        IsPopupOpen = true;
        blockerPanel?.SetActive(true);
        panel2.SetActive(true);
        panel2.transform.SetAsLastSibling();

        for (int i = 0; i < 3; i++)
        {
            if (img) img.color = Color.green;
            yield return new WaitForSeconds(0.15f);
            if (img) img.color = original;
            yield return new WaitForSeconds(0.15f);
        }

        ClosePanel(panel2);
    }

    // ── 외부에서 dirty 표시 ───────────────────────────────────

    /// <summary>가구 선택이 바뀔 때 FurnitureCarouselController에서 호출</summary>
    public void MarkFurnitureDirty() => _furnitureDirty = true;

    /// <summary>캐릭터 색상/모자가 바뀔 때 CharacterColorController 등에서 호출</summary>
    public void MarkPlayerDirty() => _playerDirty = true;
}
