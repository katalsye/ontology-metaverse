using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// EditMode — 디자인 변경 팝업.
/// 가구 종류별로 썸네일 목록을 보여주고, 선택 시 디자인 적용.
/// </summary>
public class DesignSelectUI : MonoBehaviour
{
    public static DesignSelectUI Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TMP_Text   furnitureNameText;
    public Transform  thumbnailParent;   // ScrollView > Viewport > Content (GridLayoutGroup)
    public Button     closeBtn;

    [Header("썸네일 프리팹 (Image + Button)")]
    public GameObject thumbnailPrefab;

    [Header("가구별 디자인 목록 (Inspector에서 추가)")]
    public List<FurnitureDesignConfig> designConfigs = new();

    [Header("방 가구 커스터마이저 (디자인 적용 + Firestore 저장)")]
    public RoomFurnitureCustomizer customizer;

    // ── 내부 ──────────────────────────────────────────────────
    private string _currentFurnitureName;
    private readonly List<GameObject> _spawnedThumbnails = new();

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    void Start()
    {
        if (closeBtn) closeBtn.onClick.AddListener(Close);
    }

    // ── 열기 ─────────────────────────────────────────────────

    public void Open(string furnitureName)
    {
        _currentFurnitureName = furnitureName;
        if (furnitureNameText) furnitureNameText.text = furnitureName;

        BuildThumbnails(furnitureName);

        if (panel) panel.SetActive(true);
    }

    // ── 닫기 ─────────────────────────────────────────────────

    public void Close()
    {
        if (panel) panel.SetActive(false);
        ClearThumbnails();
    }

    // ── 썸네일 생성 ───────────────────────────────────────────

    void BuildThumbnails(string furnitureName)
    {
        ClearThumbnails();

        var config = designConfigs.Find(c => c.furnitureName == furnitureName);
        if (config == null || config.designs == null || config.designs.Count == 0)
        {
            return;
        }

        foreach (var design in config.designs)
        {
            var go  = Instantiate(thumbnailPrefab, thumbnailParent);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponentInChildren<Image>();

            // 썸네일 이미지 설정
            if (img && design.thumbnail) img.sprite = design.thumbnail;

            // 클릭 시 디자인 적용
            var capturedDesign = design;
            btn.onClick.AddListener(() => OnDesignSelected(capturedDesign));

            _spawnedThumbnails.Add(go);
        }
    }

    void ClearThumbnails()
    {
        foreach (var go in _spawnedThumbnails)
            if (go) Destroy(go);
        _spawnedThumbnails.Clear();
    }

    // ── 디자인 선택 ───────────────────────────────────────────

    void OnDesignSelected(DesignData design)
    {
        if (customizer != null)
            customizer.ApplyDesignByTargetName(_currentFurnitureName, design.modelIndex, design.colorIndex);

        Close();
    }
}

// ── 데이터 구조 ───────────────────────────────────────────────

[System.Serializable]
public class DesignData
{
    public string designName;
    public string designId;
    public Sprite thumbnail;
    [Tooltip("FurnitureSkinCatalog variant 인덱스 (0 = 기본 모델)")]
    public int modelIndex;
    [Tooltip("FurnitureSkinCatalog color 인덱스")]
    public int colorIndex;
}

[System.Serializable]
public class FurnitureDesignConfig
{
    public string furnitureName;        // editableKeywords와 동일하게 (예: "Door")
    public List<DesignData> designs = new();
}
