using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class FurnitureCarouselController : MonoBehaviour
{
    [Header("데이터")]
    public FurnitureCarouselUI  data;
    public FurnitureShopCatalog catalog;
    public ClosetSaveUI         saveUI; // 선택 변경 시 dirty 알림 (선택사항)

    [Header("UI")]
    public RectTransform swipePanel;
    public Transform     buttonParent;
    public GameObject    buttonPrefab;
    public Button        prevButton;   // color → variant 로 돌아가기 (variant 여러 개일 때만)

    [Header("색상")]
    public Color selectedColor = new Color(0.6f, 0.9f, 1.0f);
    public Color normalColor   = Color.white;
    public Color lockedColor   = new Color(0.4f, 0.4f, 0.4f, 0.8f);

    [Header("스와이프")]
    public float swipeThreshold = 60f;

    public int CurrentIndex   => data != null ? data.CurrentIndex   : 0;
    public int CurrentVariant => data != null ? data.CurrentVariant : 0;

    Button[] _variantButtons;
    Button[] _colorButtons;
    bool     _swiping, _swipeConsumed;
    Vector2  _swipeStartPos;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        prevButton?.onClick.AddListener(OnPrevClick);
        if (data != null) ShowFurniture(data.CurrentIndex);
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        prevButton?.onClick.RemoveListener(OnPrevClick);
    }

    void Update() { HandleSwipeInput(); }

    // ── 스와이프 ───────────────────────────────────────────────

    void HandleSwipeInput()
    {
        if (ClosetSaveUI.IsPopupOpen) return;
        if (Touch.activeTouches.Count > 0)
        {
            var t = Touch.activeTouches[0];
            switch (t.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    if (!IsInSwipePanel(t.screenPosition)) break;
                    _swipeStartPos = t.screenPosition; _swiping = true; _swipeConsumed = false;
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    if (_swiping && !_swipeConsumed) TrySwipe(t.screenPosition.x - _swipeStartPos.x);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _swiping = false; break;
            }
        }
        else if (UnityEngine.InputSystem.Mouse.current != null)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m.leftButton.wasPressedThisFrame)
            {
                var pos = m.position.ReadValue();
                if (!IsInSwipePanel(pos)) return;
                _swipeStartPos = pos; _swiping = true; _swipeConsumed = false;
            }
            else if (_swiping && !_swipeConsumed && m.leftButton.isPressed)
                TrySwipe(m.position.ReadValue().x - _swipeStartPos.x);
            else if (m.leftButton.wasReleasedThisFrame)
                _swiping = false;
        }
    }

    bool IsInSwipePanel(Vector2 p) =>
        swipePanel == null || RectTransformUtility.RectangleContainsScreenPoint(swipePanel, p, null);

    void TrySwipe(float dx)
    {
        if (Mathf.Abs(dx) < swipeThreshold) return;
        _swipeConsumed = true;
        if (dx < 0) ShowNext(); else ShowPrev();
    }

    // ── 가구 표시 ──────────────────────────────────────────────

    public void ShowNext()
    {
        if (data.furnitures == null || data.furnitures.Length == 0) return;
        data.CurrentIndex = (data.CurrentIndex + 1) % data.furnitures.Length;
        ShowFurniture(data.CurrentIndex);
    }

    public void ShowPrev()
    {
        if (data.furnitures == null || data.furnitures.Length == 0) return;
        data.CurrentIndex = (data.CurrentIndex - 1 + data.furnitures.Length) % data.furnitures.Length;
        ShowFurniture(data.CurrentIndex);
    }

    public void ShowFurniture(int index)
    {
        if (data?.furnitures == null || data.furnitures.Length == 0) return;
        data.CurrentIndex   = index;
        data.CurrentVariant = 0;

        var cur = data.furnitures[index];
        if (cur.variants != null && cur.variants.Length > 1)
        {
            BuildVariantButtons(index);
            ApplyVariant(index, 0);
            SetPrevButton(false); // variant 단계 — prev 비활성
        }
        else
        {
            ClearButtons();
            ApplyVariant(index, 0);
            BuildColorButtons(index, 0);
            SetPrevButton(false); // variant 하나뿐 — prev 불필요
        }
        Rebuild();
    }

    // ── variant 버튼 ──────────────────────────────────────────

    void BuildVariantButtons(int fi)
    {
        ClearButtons();
        var entry = data.furnitures[fi];
        if (entry.variants == null) return;

        _variantButtons = new Button[entry.variants.Length];
        for (int i = 0; i < entry.variants.Length; i++)
        {
            int    idx   = i;
            bool   ok    = entry.variants[i].isPurchased;
            Sprite thumb = CatThumb(fi, i, 0);

            var go  = Instantiate(buttonPrefab, buttonParent);
            SetImage(go, thumb);
            SetLock(go, !ok);

            var btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                // 미구매도 색상 조회는 가능 — 저장 시점에 구매 여부 체크
                ApplyColorBlock(btn, ok ? normalColor : lockedColor);
                btn.onClick.AddListener(() => OnVariantClick(fi, idx));
            }
            _variantButtons[i] = btn;
        }
    }

    void OnVariantClick(int fi, int vi)
    {
        data.CurrentVariant = vi;
        ApplyVariant(fi, vi);
        BuildColorButtons(fi, vi);
        SetPrevButton(true); // color 단계 — prev 활성
        Rebuild();
    }

    void OnPrevClick()
    {
        // color → variant 버튼으로 복귀
        BuildVariantButtons(data.CurrentIndex);
        ApplyVariant(data.CurrentIndex, 0);
        SetPrevButton(false);
        Rebuild();
    }

    void SetPrevButton(bool active)
    {
        if (prevButton != null) prevButton.gameObject.SetActive(active);
    }

    // ── 색상 버튼 ─────────────────────────────────────────────

    void BuildColorButtons(int fi, int vi)
    {
        ClearButtons();

        // 색상 개수: catalog 우선, 없으면 model materials에서 계산
        var catVar     = CatVariant(fi, vi);
        int colorCount = catVar?.colors?.Length ?? 0;

        if (colorCount == 0)
        {
            var fm = data.furnitures[fi].variants[vi].model;
            if (fm?.colorChildren != null)
                foreach (var cc in fm.colorChildren)
                    if (cc?.materials != null && cc.materials.Length > colorCount)
                        colorCount = cc.materials.Length;
        }
        if (colorCount == 0) return;

        _colorButtons = new Button[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            int    ci    = i;
            Sprite thumb = (catVar?.colors != null && ci < catVar.colors.Length)
                           ? catVar.colors[ci].thumbnail : null;

            var go  = Instantiate(buttonPrefab, buttonParent);
            SetImage(go, thumb);

            var btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                ApplyColorBlock(btn, normalColor);
                btn.onClick.AddListener(() => OnColorClick(fi, vi, ci));
            }
            _colorButtons[i] = btn;
        }
        OnColorClick(fi, vi, 0);
    }

    void OnColorClick(int fi, int vi, int ci)
    {
        if (_colorButtons != null)
            for (int i = 0; i < _colorButtons.Length; i++)
                if (_colorButtons[i] != null)
                    ApplyColorBlock(_colorButtons[i], i == ci ? selectedColor : normalColor);

        ApplyColor(fi, vi, ci);
    }

    // ── 모델/색상 적용 ────────────────────────────────────────

    void ApplyVariant(int fi, int vi)
    {
        // 모든 가구, 모든 variant 모델 전부 끄기
        if (data?.furnitures != null)
            foreach (var e in data.furnitures)
                if (e.variants != null)
                    foreach (var v in e.variants)
                        if (v.model?.model3D != null)
                            v.model.model3D.SetActive(false);

        var entry = data.furnitures[fi];
        if (entry.variants == null || vi >= entry.variants.Length) return;

        var fm = entry.variants[vi].model;
        if (fm?.model3D == null) return;
        fm.model3D.SetActive(true);

        var cam = ClosetOrbitCamera.Instance;
        if (cam != null) { cam.ResetView(); cam.target = fm.model3D.transform; }
    }

    void ApplyColor(int fi, int vi, int ci)
    {
        var fm = data.furnitures[fi].variants[vi].model;
        if (fm?.colorChildren == null) return;
        foreach (var cc in fm.colorChildren)
        {
            if (cc?.renderer == null || cc.materials == null || ci >= cc.materials.Length) continue;
            if (cc.materials[ci] == null) continue;
            var mats = cc.renderer.sharedMaterials;
            if (cc.materialIndex < mats.Length) mats[cc.materialIndex] = cc.materials[ci];
            cc.renderer.sharedMaterials = mats;
        }
        // TODO: DB 저장 시 data.furnitures[fi].variants[vi].isPurchased 체크 후 미구매면 저장 차단
        saveUI?.MarkFurnitureDirty();
    }

    // ── 유틸 ──────────────────────────────────────────────────

    void ClearButtons()
    {
        if (buttonParent == null) return;
        foreach (Transform child in buttonParent) Destroy(child.gameObject);
        _variantButtons = null;
        _colorButtons   = null;
    }

    void Rebuild()
    {
        if (buttonParent == null) return;
        var rt = buttonParent.GetComponent<RectTransform>();
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    Sprite CatThumb(int fi, int vi, int ci)
    {
        var v = CatVariant(fi, vi);
        return (v?.colors != null && ci < v.colors.Length) ? v.colors[ci].thumbnail : null;
    }

    FurnitureShopCatalog.VariantData CatVariant(int fi, int vi)
    {
        if (catalog?.furnitures == null || fi >= catalog.furnitures.Length) return null;
        var f = catalog.furnitures[fi];
        return (f.variants != null && vi < f.variants.Length) ? f.variants[vi] : null;
    }

    static void SetImage(GameObject go, Sprite thumb)
    {
        var img = go.transform.Find("Thumbnail")?.GetComponent<Image>() ?? go.GetComponent<Image>();
        if (img == null) return;
        img.sprite = thumb;
        img.color  = thumb != null ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    static void SetLock(GameObject go, bool locked)
    {
        var lo = go.transform.Find("LockOverlay")?.GetComponent<Image>();
        if (lo != null) lo.gameObject.SetActive(locked);
    }

    // 깜빡임 방지: normalColor만 바꾸면 Button의 Selected/Highlighted 상태와 충돌하므로
    // 모든 상태를 같은 색으로 통일
    static void ApplyColorBlock(Button btn, Color c)
    {
        var bc = btn.colors;
        bc.normalColor      = c;
        bc.highlightedColor = c;
        bc.pressedColor     = new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, c.a);
        bc.selectedColor    = c;
        bc.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        bc.colorMultiplier  = 1f;
        btn.colors = bc;
    }

    static readonly List<RaycastResult> _hits = new List<RaycastResult>();
    static bool IsOverUI(Vector2 p)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = p };
        _hits.Clear();
        EventSystem.current.RaycastAll(ped, _hits);
        return _hits.Count > 0;
    }
}
