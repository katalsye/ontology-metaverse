using UnityEngine;
using UnityEngine.UI;
using Firebase.Extensions;

/// <summary>
/// AddFurniture 패널 안의 ScrollView Content에 FurnitureCatalog 아이템을 그리드로 표시.
/// 버튼 클릭 시 씬의 빈 공간에 해당 프리팹을 생성:
///   - isCeiling = true  → ceilingItemParent 아래 배치 (천장 가구)
///   - isCeiling = false → furnitureParent 아래 배치 (일반 가구)
/// 구매하지 않은 아이템은 LockOverlay 표시 + 버튼 비활성화.
/// </summary>
public class AddFurnitureSelectionUI : MonoBehaviour
{
    [Header("데이터")]
    public FurnitureCatalog catalog;

    [Header("스크롤뷰 Content 오브젝트")]
    public RectTransform gridContent;

    [Header("아이템 프리팹 (Button + Thumbnail + Text + LockOverlay)")]
    public GameObject itemPrefab;

    [Header("색상")]
    public Color normalColor   = Color.white;
    public Color selectedColor = new Color(0.6f, 0.9f, 1.0f);
    public Color lockedColor   = new Color(0.4f, 0.4f, 0.4f, 0.8f);

    // 구매 여부 배열 — catalog.furnitures 인덱스와 1:1 대응
    // TODO: 실제 서비스에서는 DB/서버에서 받아온 구매 목록으로 SetPurchased() 호출
    bool[]   _purchased;
    Button[] _buttons;
    int      _lastSelected = -1;

    void Awake()
    {
        InitDefaultPurchased();
    }

    void Start()
    {
        LoadPurchasesFromFirestore();
    }

    void OnEnable()
    {
        BuildGrid();
    }

    void LoadPurchasesFromFirestore()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;
        string uid = auth.CurrentUser.UserId;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) return;
                if (!task.Result.ContainsField("purchasedFurniture")) return;

                var purchasedNames = task.Result.GetValue<System.Collections.Generic.List<string>>("purchasedFurniture");
                if (catalog?.furnitures == null) return;
                var purchased = new bool[catalog.furnitures.Length];
                for (int i = 0; i < catalog.furnitures.Length; i++)
                    purchased[i] = purchasedNames?.Contains(catalog.furnitures[i].displayName) ?? false;
                SetPurchased(purchased);
            });
    }

    /// <summary>
    /// 기본 구매 상태 초기화.
    /// 현재: 일반 가구 중 첫 번째 1개 + 천장 가구 중 첫 번째 1개만 구매 완료로 설정.
    /// TODO: DB 연동 후 서버에서 받아온 구매 목록으로 SetPurchased() 호출할 것.
    /// </summary>
    void InitDefaultPurchased()
    {
        if (catalog == null || catalog.furnitures == null)
        {
            _purchased = new bool[0];
            return;
        }

        _purchased = new bool[catalog.furnitures.Length];

        // TODO: DB 연동 후 서버에서 받아온 구매 목록으로 대체
        // 디폴트: BedsideLight, CeilingLight 구매 완료 처리
        for (int i = 0; i < catalog.furnitures.Length; i++)
        {
            string n = catalog.furnitures[i].displayName ?? "";
            string p = catalog.furnitures[i].prefab != null ? catalog.furnitures[i].prefab.name : "";
            _purchased[i] = n.IndexOf("bedside",  System.StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("ceiling",  System.StringComparison.OrdinalIgnoreCase) >= 0
                         || p.IndexOf("bedside",  System.StringComparison.OrdinalIgnoreCase) >= 0
                         || p.IndexOf("ceiling",  System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// DB 연동 후 외부에서 구매 목록 주입 시 호출.
    /// purchased 배열은 catalog.furnitures 인덱스와 1:1 대응.
    /// </summary>
    public void SetPurchased(bool[] purchased)
    {
        _purchased = purchased;
        if (gameObject.activeInHierarchy) BuildGrid();
    }

    void BuildGrid()
    {
        // Content 앵커를 Top-Stretch로 강제 설정 (full-stretch는 ContentSizeFitter와 충돌)
        gridContent.anchorMin = new Vector2(0f, 1f);
        gridContent.anchorMax = new Vector2(1f, 1f);
        gridContent.pivot     = new Vector2(0.5f, 1f);
        gridContent.offsetMin = new Vector2(gridContent.offsetMin.x, 0f);
        gridContent.offsetMax = new Vector2(gridContent.offsetMax.x, 0f);

        // GridLayoutGroup 세팅 (Inspector 값 덮어쓰지 않고 없을 때만 추가)
        var glg = gridContent.GetComponent<GridLayoutGroup>();
        if (glg == null)
        {
            glg = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(220f, 220f);
            glg.spacing         = new Vector2(20f, 20f);
            glg.padding         = new RectOffset(20, 20, 60, 20);
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.childAlignment  = TextAnchor.UpperLeft;
        }

        // ContentSizeFitter: 세로만 PreferredSize
        var csf = gridContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // 기존 자식 즉시 제거
        var toDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in gridContent)
            toDestroy.Add(child.gameObject);
        foreach (var go in toDestroy)
            DestroyImmediate(go);

        if (catalog == null || catalog.furnitures == null || catalog.furnitures.Length == 0)
        {
            return;
        }

        // _purchased 길이가 catalog와 다를 경우 재초기화
        if (_purchased == null || _purchased.Length != catalog.furnitures.Length)
            InitDefaultPurchased();

        _buttons      = new Button[catalog.furnitures.Length];
        _lastSelected = -1;

        for (int i = 0; i < catalog.furnitures.Length; i++)
            _buttons[i] = CreateItem(i, catalog.furnitures[i], _purchased[i]);

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
    }

    Button CreateItem(int index, FurnitureCatalog.FurnitureData item, bool isPurchased)
    {
        var go  = Instantiate(itemPrefab, gridContent);
        var btn = go.GetComponent<Button>();

        // 썸네일
        var img = go.transform.Find("Thumbnail")?.GetComponent<Image>()
                  ?? go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = item.thumbnail;
            img.color  = isPurchased ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        // 이름 텍스트
        var txt = go.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null)
            txt.text = item.displayName;

        // 잠금 오버레이
        var lockOverlay = go.transform.Find("LockOverlay")?.GetComponent<Image>();
        if (lockOverlay != null)
            lockOverlay.gameObject.SetActive(!isPurchased);

        // 버튼 상태
        btn.interactable = isPurchased;
        var colors = btn.colors;
        colors.normalColor   = isPurchased ? normalColor : lockedColor;
        colors.disabledColor = lockedColor;
        btn.colors = colors;

        int idx = index;
        btn.onClick.AddListener(() => OnItemClick(idx));

        return btn;
    }

    void OnItemClick(int index)
    {
        if (catalog == null || index < 0 || index >= catalog.furnitures.Length) return;
        if (!_purchased[index]) return;

        _lastSelected = index;
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var colors = _buttons[i].colors;
            colors.normalColor = (!_purchased[i])      ? lockedColor
                               : (i == _lastSelected)  ? selectedColor
                                                       : normalColor;
            _buttons[i].colors = colors;
        }

        SpawnFurniture(catalog.furnitures[index]);
    }

    void SpawnFurniture(FurnitureCatalog.FurnitureData item)
    {
        if (item.prefab == null)
        {
            return;
        }

        var ctrl = FurnitureEditController.Instance;
        if (ctrl == null)
        {
            return;
        }

        if (item.isCeiling)
        {
            Transform parent = ctrl.ceilingItemParent;
            if (parent == null)
            {
                return;
            }

            float centerX = (ctrl.roomMinX + ctrl.roomMaxX) * 0.5f;
            float centerZ = (ctrl.roomMinZ + ctrl.roomMaxZ) * 0.5f;
            Vector3 spawnPos = new Vector3(centerX, ctrl.ceilingY, centerZ);
            var instance = Instantiate(item.prefab, spawnPos, Quaternion.identity, parent);

            // editableItems 사전 추가는 EnterCeilingEditMode()가 재진입 가드로 막힐 때를 대비해 유지.
            // EnterCeilingEditMode()가 정상 진입할 경우 내부에서 editableItems를 ceilingItemParent 자식 기반으로 재구성하므로 중복되지 않음.
            var list = new System.Collections.Generic.List<FurnitureEditConfig>(
                ctrl.editableItems ?? new FurnitureEditConfig[0]);
            list.Add(new FurnitureEditConfig
            {
                target         = instance,
                canMove        = true,
                canDesign      = true,
                ceilingMounted = true,
                isAdded        = true
            });
            ctrl.editableItems = list.ToArray();
        }
        else
        {
            float centerX = (ctrl.roomMinX + ctrl.roomMaxX) * 0.5f;
            float centerZ = (ctrl.roomMinZ + ctrl.roomMaxZ) * 0.5f;
            Vector3 spawnPos = new Vector3(centerX, ctrl.floorY, centerZ);

            // 먼저 부모 없이 instantiate → 인스턴스에서 태그 검사가 가장 신뢰성 있음
            var instance = Instantiate(item.prefab, spawnPos, Quaternion.identity);

            // PostIt(generic_marker)은 벽 부착 가구 → hangerItemParent 아래에 두고 wallMounted=true 등록
            bool isPostIt = instance.CompareTag("PostIt");
            Transform parent = isPostIt && ctrl.hangerItemParent != null
                ? ctrl.hangerItemParent
                : ctrl.furnitureParent;

            if (parent != null) instance.transform.SetParent(parent, true);

            ctrl.EnsureColliderPublic(instance);

            var list = new System.Collections.Generic.List<FurnitureEditConfig>(
                ctrl.editableItems ?? new FurnitureEditConfig[0]);
            list.Add(new FurnitureEditConfig
            {
                target      = instance,
                canMove     = true,
                canDesign   = true,
                wallMounted = isPostIt,
                isAdded     = true
            });
            ctrl.editableItems = list.ToArray();
        }

        ctrl.CloseAddFurniturePanel();

        // 설치 직후 이동 모드 진입
        // ceiling 가구는 천장 편집 모드로, 일반 가구는 위치 편집 모드로
        if (item.isCeiling)
            ctrl.EnterCeilingEditMode();
        else
            ctrl.EnterPositionEditMode();

        // Debug.Log($"[AddFurnitureSelectionUI] '{item.displayName}' 생성 완료 (isCeiling={item.isCeiling})");
    }
}
