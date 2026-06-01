using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Closet 씬 Furniture 탭 — 가구 전체 목록을 그리드로 표시.
/// HatSelectionUI와 동일한 구조.
/// 데이터 소스: FurnitureCarouselUI (단일 소스, FurnitureShopCatalog 불필요)
/// </summary>
public class FurnitureSelectionUI : MonoBehaviour
{
    [Header("데이터")]
    public FurnitureCarouselUI         data;     // 가구 데이터 소스 (3D)
    public FurnitureShopCatalog        catalog;  // 썸네일 전용
    public FurnitureCarouselController carousel; // 선택 시 variant/color 표시 위임

    [Header("스크롤뷰 Content 오브젝트")]
    public RectTransform gridContent;

    [Header("아이템 프리팹 (Button + Thumbnail + LockOverlay)")]
    public GameObject itemPrefab;

    [Header("색상")]
    public Color selectedColor = new Color(0.6f, 0.9f, 1.0f);
    public Color lockedColor   = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color normalColor   = Color.white;

    Button[] _buttons;
    int      _selected = -1;

    // ── 생명주기 ──────────────────────────────────────────────

    void Start()
    {
        // TODO: DB에서 구매 목록 받아오면 SetPurchased() 호출
        BuildGrid();
        Select(0);
    }

    // ── 외부 API ──────────────────────────────────────────────

    /// <summary>DB 연결 후 외부에서 구매 여부 주입</summary>
    public void SetPurchased(int furnitureIndex, bool purchased)
    {
        if (data == null || data.furnitures == null) return;
        if (furnitureIndex >= data.furnitures.Length) return;

        var entry = data.furnitures[furnitureIndex];
        if (entry.variants == null) return;
        foreach (var v in entry.variants)
            v.isPurchased = purchased;

        BuildGrid();
    }

    // ── 그리드 빌드 ───────────────────────────────────────────

    void BuildGrid()
    {
        foreach (Transform child in gridContent) Destroy(child.gameObject);
        if (data == null || data.furnitures == null) return;

        _buttons = new Button[data.furnitures.Length];

        for (int i = 0; i < data.furnitures.Length; i++)
        {
            var  entry     = data.furnitures[i];
            var  firstVar  = (entry.variants != null && entry.variants.Length > 0) ? entry.variants[0] : null;

            // 대표 썸네일: colorThumbnails[0] 우선, catalog 있으면 catalog로 덮어씀
            bool   purchased = firstVar?.isPurchased ?? false;
            Sprite thumb     = (firstVar?.colorThumbnails != null && firstVar.colorThumbnails.Length > 0)
                               ? firstVar.colorThumbnails[0] : null;
            if (catalog != null && catalog.furnitures != null && i < catalog.furnitures.Length)
            {
                var catF = catalog.furnitures[i];
                var catV = (catF.variants?.Length > 0) ? catF.variants[0] : null;
                var catC = (catV?.colors?.Length > 0) ? catV.colors[0] : null;
                if (catC != null) { thumb = catC.thumbnail; purchased = catC.isPurchased; }
            }

            _buttons[i] = CreateItem(i, entry.displayName, thumb, purchased);
        }

        // Content 크기 갱신 (Mask 클리핑 방지)
        if (gridContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
    }

    Button CreateItem(int index, string label, Sprite thumbnail, bool isPurchased)
    {
        var go  = Instantiate(itemPrefab, gridContent);
        var btn = go.GetComponent<Button>();

        // Thumbnail 자식 우선, 없으면 루트 Image
        var img = go.transform.Find("Thumbnail")?.GetComponent<Image>()
                  ?? go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = thumbnail;
            img.color  = thumbnail == null ? new Color(0.85f, 0.85f, 0.85f) : Color.white;
        }

        // Text는 숨김 (이름 표시 안 함)
        var txt = go.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null) txt.gameObject.SetActive(false);

        var lockOverlay = go.transform.Find("LockOverlay")?.GetComponent<Image>();
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!isPurchased);

        int idx = index;
        btn.interactable = isPurchased;
        btn.onClick.AddListener(() => Select(idx));

        var colors = btn.colors;
        colors.normalColor   = isPurchased ? normalColor : lockedColor;
        colors.disabledColor = lockedColor;
        btn.colors = colors;

        return btn;
    }

    // ── 선택 ──────────────────────────────────────────────────

    void Select(int index)
    {
        _selected = index;

        // 버튼 하이라이트 갱신
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var  entry    = data.furnitures[i];
            var  firstVar = (entry.variants?.Length > 0) ? entry.variants[0] : null;
            bool isLocked = !(firstVar?.isPurchased ?? false);
            var  c        = _buttons[i].colors;
            c.normalColor = isLocked   ? lockedColor
                          : i == index ? selectedColor
                                       : normalColor;
            _buttons[i].colors = c;
        }

        // 캐러셀에 선택 전달 → 해당 가구 3D + variant/color 버튼 표시
        if (carousel != null)
            carousel.ShowFurniture(index);
    }
}
