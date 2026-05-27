using System.Collections.Generic;
using UnityEngine;

public enum UIPage
{
    Feed,
    MyRoom,
    Quest,
    Shop
}

public class UIPageManager : MonoBehaviour
{
    public static UIPageManager Instance { get; private set; }

    private readonly Dictionary<UIPage, GameObject> pages = new Dictionary<UIPage, GameObject>();
    private RectTransform pageRoot;
    private BottomNavigation bottomNavigation;

    private void Awake()
    {
        Instance = this;
    }

    public void BuildDefaultUI()
    {
        GameObject background = UIFactory.CreateUIObject("App Background", transform);
        UIFactory.Stretch(background.GetComponent<RectTransform>());
        UIFactory.AddImage(background, UIFactory.PageBackground);

        GameObject pageRootObject = UIFactory.CreateUIObject("Pages", transform);
        pageRoot = pageRootObject.GetComponent<RectTransform>();
        UIFactory.SetAnchored(pageRoot, Vector2.zero, Vector2.one, new Vector2(0f, 76f), Vector2.zero);

        CreatePage<FeedPage>(UIPage.Feed, "Feed Page");
        CreatePage<MyRoomPage>(UIPage.MyRoom, "My Room Page");
        CreatePage<QuestPage>(UIPage.Quest, "Quest Page");
        CreatePage<ShopPage>(UIPage.Shop, "Shop Page");

        GameObject navObject = UIFactory.CreateUIObject("Bottom Navigation", transform);
        RectTransform navRect = navObject.GetComponent<RectTransform>();
        UIFactory.SetAnchored(navRect, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 76f));
        bottomNavigation = navObject.AddComponent<BottomNavigation>();
        bottomNavigation.Initialize(this);

        ShowPage(UIPage.MyRoom);
    }

    public void ShowPage(UIPage page)
    {
        foreach (KeyValuePair<UIPage, GameObject> pair in pages)
        {
            pair.Value.SetActive(pair.Key == page);
        }

        Debug.Log("Show Page: " + page);
        bottomNavigation?.SetSelected(page);
    }

    private void CreatePage<T>(UIPage page, string name) where T : MonoBehaviour
    {
        GameObject pageObject = UIFactory.CreateUIObject(name, pageRoot);
        UIFactory.Stretch(pageObject.GetComponent<RectTransform>());
        pageObject.AddComponent<T>();
        pages[page] = pageObject;
    }
}
