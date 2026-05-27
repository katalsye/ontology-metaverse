using UnityEngine;
using UnityEngine.UI;

public class ShopPage : MonoBehaviour
{
    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        UIFactory.AddImage(gameObject, UIFactory.PageBackground);

        Text title = UIFactory.CreateText("Title", transform, "Shop", 28, FontStyle.Bold, UIFactory.Ink, TextAnchor.MiddleCenter);
        UIFactory.SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -104f), new Vector2(-24f, -36f));

        Text placeholder = UIFactory.CreateText("Placeholder", transform, "Shop Page Coming Soon", 18, FontStyle.Normal, UIFactory.MutedInk, TextAnchor.MiddleCenter);
        UIFactory.SetAnchored(placeholder.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
    }
}
