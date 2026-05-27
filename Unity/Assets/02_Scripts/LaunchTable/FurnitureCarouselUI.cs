using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Closet 씬 Furniture 탭 — 좌우 스와이프로 가구 순환, 3D 모델 전환, 종류(variant) 선택 UI.
/// furniturePanel이 활성화될 때 OnEnable()이 자동 호출되어 초기화됨.
/// </summary>
public class FurnitureCarouselUI : MonoBehaviour
{
    // ── 데이터 ────────────────────────────────────────────────

    [System.Serializable]
    public class ColorChild
    {
        public Renderer   renderer;
        [Tooltip("바꾸기 시작할 슬롯 번호 (0부터)")]
        public int        materialIndex;
        [Tooltip("materials[n] = n번째 색상의 material")]
        public Material[] materials;
    }

    [System.Serializable]
    public class FurnitureModel
    {
        public GameObject   model3D;
        public ColorChild[] colorChildren;
    }

    [System.Serializable]
    public class FurnitureVariant
    {
        public string       displayName;     // 종류 이름 (기본, 다크우드, 화이트 …)
        // TODO: DB 연동 후 서버에서 받아온 구매 목록으로 설정할 것 (현재는 Inspector 디버그용)
        public bool         isPurchased;
        public FurnitureModel model;         // variant당 모델 하나
        [Tooltip("색상별 썸네일 — colorThumbnails[n] 이 materials[n] 과 1:1 대응. [0]이 대표 썸네일로 사용됨")]
        public Sprite[]     colorThumbnails; // 색상별 썸네일 (0번이 대표)
    }

    [System.Serializable]
    public class FurnitureEntry
    {
        public string             displayName; // 가구 이름 (침대, 옷장 …)
        public FurnitureVariant[] variants;    // variants[0]이 기본
    }

    // ── Inspector ─────────────────────────────────────────────

    [Header("가구 목록 (순환 순서대로)")]
    public FurnitureEntry[] furnitures;

    [Header("UI")]
    public Text       furnitureNameText;   // 현재 가구 이름
    public Transform  variantOptionParent; // 종류 버튼들의 부모 Transform
    public GameObject variantButtonPrefab; // Button + Image(썸네일) 구성 프리팹

    [Header("스와이프 설정")]
    [Tooltip("이 픽셀 이상 드래그해야 스와이프로 인정")]
    public float swipeThreshold = 60f;

    // ── 상태 ──────────────────────────────────────────────────

    public  int     CurrentIndex    => _currentIndex;
    private int     _currentIndex   = 0;
    private int     _currentVariant = 0;
    private bool    _swiping        = false;
    private bool    _swipeConsumed  = false;
    private Vector2 _swipeStartPos;

    // ── 생명주기 ──────────────────────────────────────────────

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        ShowFurniture(_currentIndex);
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        HandleSwipeInput();
    }

    // ── 스와이프 감지 ──────────────────────────────────────────

    void HandleSwipeInput()
    {
        if (Touch.activeTouches.Count > 0)
        {
            var t = Touch.activeTouches[0];
            switch (t.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _swipeStartPos = t.screenPosition;
                    _swiping       = true;
                    _swipeConsumed = false;
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    if (_swiping && !_swipeConsumed)
                        TrySwipe(t.screenPosition.x - _swipeStartPos.x);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _swiping = false;
                    break;
            }
        }
        else if (UnityEngine.InputSystem.Mouse.current != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _swipeStartPos = mouse.position.ReadValue();
                _swiping       = true;
                _swipeConsumed = false;
            }
            else if (_swiping && !_swipeConsumed && mouse.leftButton.isPressed)
                TrySwipe(mouse.position.ReadValue().x - _swipeStartPos.x);
            else if (mouse.leftButton.wasReleasedThisFrame)
                _swiping = false;
        }
    }

    void TrySwipe(float deltaX)
    {
        if (Mathf.Abs(deltaX) < swipeThreshold) return;
        _swipeConsumed = true;
        if (deltaX < 0) ShowNext();
        else            ShowPrev();
    }

    // ── 가구 순환 ──────────────────────────────────────────────

    public void ShowNext()
    {
        if (furnitures == null || furnitures.Length == 0) return;
        _currentIndex = (_currentIndex + 1) % furnitures.Length;
        ShowFurniture(_currentIndex);
    }

    public void ShowPrev()
    {
        if (furnitures == null || furnitures.Length == 0) return;
        _currentIndex = (_currentIndex - 1 + furnitures.Length) % furnitures.Length;
        ShowFurniture(_currentIndex);
    }

    public void ShowFurniture(int index)
    {
        if (furnitures == null || furnitures.Length == 0) return;
        _currentIndex   = index;
        _currentVariant = 0;

        // 모든 모델 비활성
        foreach (var entry in furnitures)
            if (entry.variants != null)
                foreach (var v in entry.variants)
                    if (v.model?.model3D != null)
                        v.model.model3D.SetActive(false);

        var cur = furnitures[index];

        // 이름 텍스트
        if (furnitureNameText != null)
            furnitureNameText.text = cur.displayName ?? "";

        // 종류 버튼 재구성 후 variants[0] 기본 적용
        BuildVariantOptions(index);
        ApplyVariant(index, 0);
    }

    // ── 종류 선택 UI ───────────────────────────────────────────

    void BuildVariantOptions(int furnitureIndex)
    {
        if (variantOptionParent == null || variantButtonPrefab == null) return;

        foreach (Transform child in variantOptionParent)
            Destroy(child.gameObject);

        var entry = furnitures[furnitureIndex];
        if (entry.variants == null || entry.variants.Length == 0) return;

        for (int i = 0; i < entry.variants.Length; i++)
        {
            int idx     = i;
            var variant = entry.variants[i];

            var go  = Instantiate(variantButtonPrefab, variantOptionParent);
            var img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                var thumb = (variant.colorThumbnails != null && variant.colorThumbnails.Length > 0)
                            ? variant.colorThumbnails[0] : null;
                if (thumb != null) img.sprite = thumb;
                img.color = variant.isPurchased ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
            }

            // 미구매 잠금 오버레이
            var lockOverlay = go.transform.Find("LockOverlay")?.GetComponent<Image>();
            if (lockOverlay != null) lockOverlay.gameObject.SetActive(!variant.isPurchased);

            var btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.interactable = variant.isPurchased;
                btn.onClick.AddListener(() => ApplyVariant(furnitureIndex, idx));
            }
        }
    }

    // ── 종류 적용 ──────────────────────────────────────────────

    void ApplyVariant(int furnitureIndex, int variantIndex)
    {
        if (furnitures == null || furnitureIndex >= furnitures.Length) return;
        var entry = furnitures[furnitureIndex];
        if (entry.variants == null || variantIndex >= entry.variants.Length) return;

        var variant = entry.variants[variantIndex];
        _currentVariant = variantIndex;

        // 모든 모델 비활성
        foreach (var v in entry.variants)
            if (v.model?.model3D != null)
                v.model.model3D.SetActive(false);

        // 선택된 variant 모델 활성 + material 적용
        var fm = variant.model;
        if (fm?.model3D == null) return;

        fm.model3D.SetActive(true);

        if (fm.colorChildren != null)
            foreach (var cc in fm.colorChildren)
            {
                if (cc?.renderer == null || cc.materials == null || cc.materials.Length == 0) continue;
                var mats = cc.renderer.sharedMaterials;
                int slot = cc.materialIndex;
                if (slot < mats.Length && cc.materials[0] != null)
                    mats[slot] = cc.materials[0]; // 기본은 0번 색상
                cc.renderer.sharedMaterials = mats;
            }

        // 카메라 타겟
        var cam = ClosetOrbitCamera.Instance;
        if (cam != null)
        {
            cam.ResetView();
            cam.target = fm.model3D.transform;
        }

        // TODO: DB에 선택한 종류 저장 (서버에 가구 디자인 변경 전송)
    }
}
