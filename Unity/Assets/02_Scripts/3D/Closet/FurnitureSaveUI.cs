using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Extensions;

/// <summary>
/// 저장 버튼 로직
///
/// [저장 클릭 시]
/// ① 변경사항 있음 + 전부 구매 → 저장(주석) + Panel1 표시 → 확인 버튼으로 닫기
/// ② 변경사항 있음 + 미구매 있음 → Panel2 표시
///      Button1: 상점 씬 이동
///      Button2: Panel2만 닫기
/// ③ 변경사항 없음 → 아무것도 하지 않음
///
/// 변경사항 감지: furniture 또는 player 중 하나라도 dirty면 저장 대상
/// </summary>
public class FurnitureSaveUI : MonoBehaviour
{
    [Header("저장 버튼")]
    public Button saveButton;

    [Header("Panel1 — 저장 완료")]
    public GameObject panel1;
    public Button     panel1ConfirmBtn;

    [Header("Panel2 — 미구매 안내")]
    public GameObject panel2;
    public Button     panel2GoShopBtn;  // 상점으로
    public Button     panel2CancelBtn;  // 그냥 닫기

    [Header("참조")]
    public FurnitureCarouselController furnitureCarousel;
    public HatSelectionUI              hatSelectionUI; // 모자 구매 여부 체크용

    [Header("씬")]
    [Tooltip("상점 씬 이름 — Build Settings에 등록된 씬 이름으로 Inspector에서 지정")]
    public string shopSceneName = "Shop";

    // ── 변경사항 플래그 ───────────────────────────────────────
    bool _furnitureDirty = false;
    bool _playerDirty    = false;

    bool IsDirty => _furnitureDirty || _playerDirty;

    // ── 초기화 ────────────────────────────────────────────────

    void Start()
    {
        saveButton?.onClick.AddListener(OnSaveClick);

        panel1ConfirmBtn?.onClick.AddListener(() => panel1.SetActive(false));

        panel2GoShopBtn?.onClick.AddListener(OnGoShop);
        panel2CancelBtn?.onClick.AddListener(() => panel2.SetActive(false));

        panel1?.SetActive(false);
        panel2?.SetActive(false);
    }

    // ── 저장 버튼 ─────────────────────────────────────────────

    void OnSaveClick()
    {
        if (!IsDirty) return; // 변경사항 없으면 아무것도 안 함

        if (HasUnpurchased())
        {
            // ② 미구매 있음 → Panel2
            panel2.SetActive(true);
        }
        else
        {
            // ① 전부 구매 → 저장 후 Panel1
            DoSave();
            panel1.SetActive(true);
        }
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

        db.Collection("users").Document(uid)
          .UpdateAsync("closetConfig", data)
          .ContinueWithOnMainThread(t =>
          {
              if (t.IsFaulted) Debug.LogError("[FurnitureSaveUI] 저장 실패: " + t.Exception);
              else Debug.Log("[FurnitureSaveUI] 저장 완료");
          });
    }

    // ── 미구매 체크 ───────────────────────────────────────────

    bool HasUnpurchased()
    {
        // 가구 미구매 체크
        if (furnitureCarousel?.data?.furnitures != null)
        {
            var fi = furnitureCarousel.CurrentIndex;
            var vi = furnitureCarousel.CurrentVariant;
            var furnitures = furnitureCarousel.data.furnitures;
            if (fi < furnitures.Length)
            {
                var variants = furnitures[fi].variants;
                if (variants != null && vi < variants.Length && !variants[vi].isPurchased)
                    return true;
            }
        }

        // 모자 미구매 체크
        if (hatSelectionUI != null && !hatSelectionUI.IsCurrentHatPurchased())
            return true;

        return false;
    }

    // ── 상점 이동 ─────────────────────────────────────────────

    void OnGoShop()
    {
        panel2.SetActive(false);
        SceneManager.LoadScene(shopSceneName);
    }

    // ── 외부에서 dirty 표시 ───────────────────────────────────

    /// <summary>가구 선택이 바뀔 때 FurnitureCarouselController에서 호출</summary>
    public void MarkFurnitureDirty() => _furnitureDirty = true;

    /// <summary>캐릭터 색상/모자가 바뀔 때 CharacterColorController 등에서 호출</summary>
    public void MarkPlayerDirty() => _playerDirty = true;
}
