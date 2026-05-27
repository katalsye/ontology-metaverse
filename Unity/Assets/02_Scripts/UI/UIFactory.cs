using UnityEngine;
using UnityEngine.UI;

public static class UIFactory
{
    public static readonly Color PageBackground = new Color(0.94f, 0.95f, 0.97f);
    public static readonly Color Ink = new Color(0.12f, 0.13f, 0.16f);
    public static readonly Color MutedInk = new Color(0.42f, 0.45f, 0.50f);
    public static readonly Color Orange = new Color(1.0f, 0.55f, 0.18f);
    public static readonly Color Border = new Color(0.87f, 0.89f, 0.92f);

    private static Font cachedFont;

    public static Font DefaultFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            return cachedFont;
        }
    }

    public static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    public static Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        GameObject textObject = CreateUIObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = DefaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, Color backgroundColor, Color textColor)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image image = AddImage(buttonObject, backgroundColor);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.10f);
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform, label, 15, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return button;
    }

    public static void Stretch(RectTransform rectTransform, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    public static void SetAnchored(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    public static void SetSize(RectTransform rectTransform, float width, float height)
    {
        rectTransform.sizeDelta = new Vector2(width, height);
    }
}
