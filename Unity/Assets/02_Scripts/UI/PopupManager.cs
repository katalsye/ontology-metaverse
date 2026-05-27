using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    private GameObject overlay;
    private Transform popupParent;
    private int actionButtonCount;

    public static PopupManager GetOrCreate(Transform parent)
    {
        PopupManager manager = parent.GetComponentInChildren<PopupManager>(true);
        if (manager != null)
        {
            return manager;
        }

        GameObject managerObject = UIFactory.CreateUIObject("Popup Manager", parent);
        UIFactory.Stretch(managerObject.GetComponent<RectTransform>());
        return managerObject.AddComponent<PopupManager>();
    }

    public RectTransform ShowPopup(string title, string body)
    {
        ClosePopup();

        overlay = UIFactory.CreateUIObject(title + " Overlay", transform);
        UIFactory.Stretch(overlay.GetComponent<RectTransform>());
        UIFactory.AddImage(overlay, new Color(0f, 0f, 0f, 0.45f));

        GameObject panel = UIFactory.CreateUIObject(title + " Popup", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(330f, 260f);
        UIFactory.AddImage(panel, Color.white);

        Text titleText = UIFactory.CreateText("Title", panel.transform, title, 22, FontStyle.Bold, UIFactory.Ink, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -64f), new Vector2(-64f, -18f));

        Button closeButton = UIFactory.CreateButton("Close Button", panel.transform, "X", new Color(0.95f, 0.96f, 0.98f), UIFactory.Ink);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-18f, -18f);
        closeRect.sizeDelta = new Vector2(42f, 42f);
        closeButton.onClick.AddListener(ClosePopup);

        Text bodyText = UIFactory.CreateText("Body", panel.transform, body, 16, FontStyle.Normal, UIFactory.MutedInk, TextAnchor.UpperLeft);
        UIFactory.SetAnchored(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -82f));

        popupParent = panel.transform;
        actionButtonCount = 0;
        Debug.Log("Open Popup: " + title);
        return panelRect;
    }

    public void AddButtonToCurrentPopup(string label, System.Action onClick)
    {
        if (popupParent == null)
        {
            return;
        }

        Button button = UIFactory.CreateButton(label + " Button", popupParent, label, UIFactory.Orange, Color.white);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(24f + actionButtonCount * 94f, 22f);
        rect.sizeDelta = new Vector2(86f, 42f);
        actionButtonCount++;
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    public void ClosePopup()
    {
        if (overlay != null)
        {
            Debug.Log("Close Popup");
            Destroy(overlay);
            overlay = null;
            popupParent = null;
        }
    }
}
