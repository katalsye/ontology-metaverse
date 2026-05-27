using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BottomNavigation : MonoBehaviour
{
    private readonly Dictionary<UIPage, Button> buttons = new Dictionary<UIPage, Button>();
    private UIPageManager pageManager;

    public void Initialize(UIPageManager manager)
    {
        pageManager = manager;
        UIFactory.AddImage(gameObject, Color.white);

        HorizontalLayoutGroup layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 8f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        AddButton(UIPage.Feed, "Feed");
        AddButton(UIPage.MyRoom, "MyRoom");
        AddButton(UIPage.Quest, "Quest");
        AddButton(UIPage.Shop, "Shop");
    }

    public void SetSelected(UIPage selectedPage)
    {
        foreach (KeyValuePair<UIPage, Button> pair in buttons)
        {
            Image image = pair.Value.GetComponent<Image>();
            image.color = pair.Key == selectedPage ? UIFactory.Orange : new Color(0.93f, 0.94f, 0.96f);
            Text label = pair.Value.GetComponentInChildren<Text>();
            label.color = pair.Key == selectedPage ? Color.white : UIFactory.MutedInk;
        }
    }

    private void AddButton(UIPage page, string label)
    {
        string icon = GetIcon(page);
        Button button = UIFactory.CreateButton(page + " Button", transform, icon + "\n" + label, new Color(0.93f, 0.94f, 0.96f), UIFactory.MutedInk);
        Text buttonLabel = button.GetComponentInChildren<Text>();
        buttonLabel.fontSize = 10;
        buttonLabel.lineSpacing = 0.82f;
        button.onClick.AddListener(() =>
        {
            Debug.Log("Bottom Navigation Click: " + page);
            pageManager.ShowPage(page);
        });
        buttons[page] = button;

        if (page == UIPage.Quest)
        {
            GameObject badge = UIFactory.CreateUIObject("Unread Badge", button.transform);
            UIFactory.AddImage(badge, new Color(0.96f, 0.18f, 0.18f));
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.62f, 0.66f);
            badgeRect.anchorMax = new Vector2(0.62f, 0.66f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(18f, 18f);

            Text count = UIFactory.CreateText("Badge Count", badge.transform, "3", 9, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(count.rectTransform);
        }
    }

    private string GetIcon(UIPage page)
    {
        switch (page)
        {
            case UIPage.Feed:
                return "Home";
            case UIPage.MyRoom:
                return "Room";
            case UIPage.Quest:
                return "Map";
            case UIPage.Shop:
                return "Bag";
            default:
                return "";
        }
    }
}
