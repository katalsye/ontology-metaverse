using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 5-1/5-2. ShopScreen 컨트롤러
/// 카테고리 탭(가구/스킨/펫) + 아이템 그리드 + 상세 시트 + 구매
/// </summary>
public class ShopScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("References")]
    [SerializeField] private RewardPopupController rewardPopup;

    private VisualElement root;
    private VisualElement itemGrid;
    private VisualElement detailOverlay;

    private Button tabFurniture, tabSkin, tabPet;
    private string currentCategory = "furniture";

    private Label detailName, detailDesc, detailPrice, previewEmoji;
    private ShopItemData selectedItem;

    private List<ShopItemData> allItems = new List<ShopItemData>();
    private System.Collections.Generic.HashSet<string> ownedItems = new System.Collections.Generic.HashSet<string>();

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        itemGrid = root.Q("item-grid");
        detailOverlay = root.Q("item-detail-overlay");

        // 탭
        tabFurniture = root.Q<Button>("tab-furniture");
        tabSkin = root.Q<Button>("tab-skin");
        tabPet = root.Q<Button>("tab-pet");
        tabFurniture.clicked += () => SetCategory("furniture");
        tabSkin.clicked += () => SetCategory("skin");
        tabPet.clicked += () => SetCategory("pet");

        // 상세
        detailName = root.Q<Label>("item-detail-name");
        detailDesc = root.Q<Label>("item-detail-desc");
        detailPrice = root.Q<Label>("item-price");
        previewEmoji = root.Q<Label>("item-preview-emoji");

        root.Q<Button>("btn-close-detail").clicked += CloseDetail;
        root.Q<Button>("btn-buy").clicked += OnBuyClicked;

        LoadItems();
    }

    private void LoadItems()
    {
        allItems = new List<ShopItemData>
        {
            new ShopItemData("f1", "furniture", "원목 책상", "따뜻한 느낌의 원목 책상", "🪑", 100),
            new ShopItemData("f2", "furniture", "미니 화분", "방에 생기를 더해요", "🪴", 50),
            new ShopItemData("f3", "furniture", "무드 조명", "은은한 분위기 연출", "💡", 80),
            new ShopItemData("f4", "furniture", "러그", "포근한 깔개", "🟫", 60),
            new ShopItemData("s1", "skin", "파자마 세트", "편안한 잠옷", "👕", 120),
            new ShopItemData("s2", "skin", "캐주얼 후드", "데일리 패션", "🧥", 150),
            new ShopItemData("p1", "pet", "고양이", "귀여운 동반자", "🐱", 300),
            new ShopItemData("p2", "pet", "강아지", "충실한 친구", "🐶", 300),
        };

        // 보유 아이템 조회 후 렌더링
        RewardManager.Instance.GetItems(
            onSuccess: items =>
            {
                ownedItems = new System.Collections.Generic.HashSet<string>(items);
                RenderItems();
            },
            onFailure: _ => RenderItems()
        );
    }

    private void SetCategory(string category)
    {
        currentCategory = category;

        tabFurniture.RemoveFromClassList("tab-btn--active");
        tabSkin.RemoveFromClassList("tab-btn--active");
        tabPet.RemoveFromClassList("tab-btn--active");

        switch (category)
        {
            case "furniture": tabFurniture.AddToClassList("tab-btn--active"); break;
            case "skin": tabSkin.AddToClassList("tab-btn--active"); break;
            case "pet": tabPet.AddToClassList("tab-btn--active"); break;
        }

        RenderItems();
    }

    private void RenderItems()
    {
        itemGrid.Clear();

        var filtered = allItems.Where(i => i.category == currentCategory).ToList();

        foreach (var item in filtered)
        {
            itemGrid.Add(CreateItemCard(item));
        }
    }

    private VisualElement CreateItemCard(ShopItemData item)
    {
        var card = new VisualElement();
        card.AddToClassList("shop-item");

        var thumb = new VisualElement();
        thumb.AddToClassList("shop-item-thumb");
        var emoji = new Label(item.emoji);
        emoji.AddToClassList("shop-item-emoji");
        thumb.Add(emoji);

        var name = new Label(item.name);
        name.AddToClassList("shop-item-name");

        var priceRow = new VisualElement();
        priceRow.AddToClassList("shop-item-price");
        var coinIcon = new Label("🪙");
        coinIcon.AddToClassList("price-icon");
        var priceLabel = new Label(item.price.ToString());
        priceLabel.AddToClassList("price-value");
        priceRow.Add(coinIcon);
        priceRow.Add(priceLabel);

        card.Add(thumb);
        card.Add(name);
        card.Add(priceRow);

        if (ownedItems.Contains(item.id))
        {
            var ownedBadge = new Label("보유 중");
            ownedBadge.AddToClassList("shop-owned-badge");
            thumb.Add(ownedBadge);
        }

        card.RegisterCallback<ClickEvent>(evt => ShowDetail(item));

        return card;
    }

    private void ShowDetail(ShopItemData item)
    {
        selectedItem = item;
        previewEmoji.text = item.emoji;
        detailName.text = item.name;
        detailDesc.text = item.description;
        detailPrice.text = item.price.ToString();
        detailOverlay.style.display = DisplayStyle.Flex;
    }

    private void CloseDetail()
    {
        detailOverlay.style.display = DisplayStyle.None;
        selectedItem = null;
    }

    private void OnBuyClicked()
    {
        if (selectedItem == null) return;
        if (ownedItems.Contains(selectedItem.id)) return;

        var buyBtn = root.Q<Button>("btn-buy");
        buyBtn.SetEnabled(false);

        RewardManager.Instance.PurchaseItem(selectedItem.id, selectedItem.price,
            onSuccess: () =>
            {
                ownedItems.Add(selectedItem.id);
                string name = selectedItem.name;
                CloseDetail();
                RenderItems();
                rewardPopup?.Show(name, 1, null);
            },
            onFailure: err =>
            {
                buyBtn.SetEnabled(true);
                Debug.LogError($"[Shop] 구매 실패: {err}");
                // err == "재화 부족" 일 때 토스트 등 안내 가능
            }
        );
    }
}

public class ShopItemData
{
    public string id, category, name, description, emoji;
    public int price;

    public ShopItemData(string id, string category, string name,
                        string description, string emoji, int price)
    {
        this.id = id; this.category = category; this.name = name;
        this.description = description; this.emoji = emoji; this.price = price;
    }
}
