using UnityEngine;
using UnityEngine.UI;

public class QuestCard : MonoBehaviour
{
    private Image iconImage;
    private Text titleText;
    private Text descriptionText;
    private Text progressText;
    private Text rewardText;
    private Image progressFill;

    public void Build()
    {
        UIFactory.AddImage(gameObject, new Color(0.98f, 0.985f, 0.995f));

        GameObject icon = UIFactory.CreateUIObject("Icon", transform);
        iconImage = UIFactory.AddImage(icon, Color.green);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(16f, -16f);
        iconRect.sizeDelta = new Vector2(38f, 38f);

        Text iconText = UIFactory.CreateText("Icon Text", icon.transform, "OK", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(iconText.rectTransform);

        titleText = UIFactory.CreateText("Title", transform, "", 16, FontStyle.Bold, UIFactory.Ink, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(68f, -40f), new Vector2(-78f, -12f));

        descriptionText = UIFactory.CreateText("Description", transform, "", 13, FontStyle.Normal, UIFactory.MutedInk, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(descriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(68f, -62f), new Vector2(-18f, -38f));

        rewardText = UIFactory.CreateText("Reward", transform, "", 15, FontStyle.Bold, UIFactory.Orange, TextAnchor.MiddleRight);
        UIFactory.SetAnchored(rewardText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-82f, -38f), new Vector2(-16f, -14f));

        progressText = UIFactory.CreateText("Progress Text", transform, "", 12, FontStyle.Normal, UIFactory.MutedInk, TextAnchor.MiddleLeft);
        UIFactory.SetAnchored(progressText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 22f), new Vector2(-16f, 44f));

        GameObject track = UIFactory.CreateUIObject("Progress Track", transform);
        UIFactory.AddImage(track, new Color(0.89f, 0.91f, 0.94f));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        UIFactory.SetAnchored(trackRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 13f), new Vector2(-16f, 20f));

        GameObject fill = UIFactory.CreateUIObject("Progress Fill", track.transform);
        progressFill = UIFactory.AddImage(fill, Color.green);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    public void Bind(QuestData data)
    {
        titleText.text = data.title;
        descriptionText.text = data.description;
        progressText.text = data.progressText;
        rewardText.text = "Coin +" + data.reward;
        iconImage.color = data.themeColor;
        progressFill.color = data.themeColor;

        RectTransform fillRect = progressFill.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(data.progressValue), 1f);

        Debug.Log("Quest Card Bind: " + data.title);
    }
}
