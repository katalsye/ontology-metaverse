using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestPage : MonoBehaviour
{
    private readonly List<QuestCard> cards = new List<QuestCard>();

    private void Awake()
    {
        Build();
        BindDefaultQuests();
    }

    private void Build()
    {
        UIFactory.AddImage(gameObject, new Color(1f, 1f, 1f, 0.96f));

        GameObject panel = UIFactory.CreateUIObject("Quest Board Panel", transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(350f, 560f);
        UIFactory.AddImage(panel, Color.white);

        GameObject icon = UIFactory.CreateUIObject("Header Icon", panel.transform);
        UIFactory.AddImage(icon, UIFactory.Orange);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(22f, -22f);
        iconRect.sizeDelta = new Vector2(48f, 48f);

        Text targetIcon = UIFactory.CreateText("Target Icon", icon.transform, "Goal", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(targetIcon.rectTransform);

        Text title = UIFactory.CreateText("Title", panel.transform, "Quest Board", 23, FontStyle.Bold, UIFactory.Ink, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(84f, -48f), new Vector2(-62f, -16f));

        Text subtitle = UIFactory.CreateText("Subtitle", panel.transform, "Complete daily quests to earn rewards", 13, FontStyle.Normal, UIFactory.MutedInk, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(84f, -72f), new Vector2(-22f, -46f));

        Button closeButton = UIFactory.CreateButton("Close Button", panel.transform, "X", new Color(0.95f, 0.96f, 0.98f), UIFactory.Ink);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-18f, -18f);
        closeRect.sizeDelta = new Vector2(32f, 32f);
        closeButton.GetComponentInChildren<Text>().fontSize = 14;
        closeButton.onClick.AddListener(() =>
        {
            Debug.Log("Quest Board Close");
            UIPageManager.Instance?.ShowPage(UIPage.MyRoom);
        });

        GameObject list = UIFactory.CreateUIObject("Quest List", panel.transform);
        RectTransform listRect = list.GetComponent<RectTransform>();
        UIFactory.SetAnchored(listRect, Vector2.zero, Vector2.one, new Vector2(18f, 84f), new Vector2(-18f, -100f));

        VerticalLayoutGroup listLayout = list.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 12f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        CreateCard(list.transform);
        CreateCard(list.transform);
        CreateCard(list.transform);

        Button viewAllButton = UIFactory.CreateButton("View All Quests Button", panel.transform, "View All Quests", UIFactory.Orange, Color.white);
        RectTransform viewAllRect = viewAllButton.GetComponent<RectTransform>();
        UIFactory.SetAnchored(viewAllRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 68f));
        viewAllButton.onClick.AddListener(() => Debug.Log("View All Quests"));
        viewAllButton.GetComponentInChildren<Text>().text = "Trophy  View All Quests";
    }

    private void CreateCard(Transform parent)
    {
        GameObject cardObject = UIFactory.CreateUIObject("Quest Card", parent);
        LayoutElement layoutElement = cardObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 112f;
        QuestCard card = cardObject.AddComponent<QuestCard>();
        card.Build();
        cards.Add(card);
    }

    private void BindDefaultQuests()
    {
        QuestData[] data =
        {
            new QuestData("Morning Exercise", "Walk 5000 steps today", "4000 / 5000 steps", 0.8f, 50, new Color(0.20f, 0.70f, 0.36f)),
            new QuestData("Journal Entry", "Write your daily reflection", "Not started", 0f, 30, new Color(0.22f, 0.48f, 0.92f)),
            new QuestData("Pet Care", "Feed and interact with Mochi", "1 / 2 interactions", 0.5f, 20, new Color(0.55f, 0.34f, 0.86f))
        };

        for (int i = 0; i < cards.Count && i < data.Length; i++)
        {
            cards[i].Bind(data[i]);
        }
    }
}
