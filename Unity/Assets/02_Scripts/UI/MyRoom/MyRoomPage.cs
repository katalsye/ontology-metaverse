using UnityEngine;
using UnityEngine.UI;

public class MyRoomPage : MonoBehaviour
{
    private FurniturePopupManager furniturePopupManager;

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        UIFactory.AddImage(gameObject, Color.white);

        PopupManager popupManager = PopupManager.GetOrCreate(transform);
        furniturePopupManager = gameObject.AddComponent<FurniturePopupManager>();
        furniturePopupManager.Initialize(popupManager);

        BuildTopBar();
        BuildRoomObjects();
    }

    private void BuildTopBar()
    {
        Button settingsButton = UIFactory.CreateButton("Settings Button", transform, "Settings", Color.white, UIFactory.Ink);
        RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
        UIFactory.SetAnchored(settingsRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -58f), new Vector2(66f, -18f));
        settingsButton.GetComponentInChildren<Text>().fontSize = 9;

        Button profileButton = UIFactory.CreateButton("Profile Button", transform, "Me", Color.white, UIFactory.Ink);
        RectTransform profileRect = profileButton.GetComponent<RectTransform>();
        UIFactory.SetAnchored(profileRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -58f), new Vector2(104f, -18f));
        profileButton.GetComponentInChildren<Text>().fontSize = 10;

        GameObject coin = UIFactory.CreateUIObject("Coin Balance", transform);
        UIFactory.AddImage(coin, new Color(1f, 0.78f, 0.22f));
        RectTransform coinRect = coin.GetComponent<RectTransform>();
        UIFactory.SetAnchored(coinRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-122f, -56f), new Vector2(-24f, -20f));

        Text coinLabel = UIFactory.CreateText("Coin Label", coin.transform, "Coin  1,250", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(coinLabel.rectTransform);
    }

    private void BuildRoomObjects()
    {
        CreateCarpet();
        CreateFurnitureButton(FurnitureType.Calendar, "Calendar", new Vector2(92f, 234f), new Vector2(72f, 72f), new Color(0.93f, 0.29f, 0.24f));
        CreateDesk();
        CreateWindow();
        CreateBed();
        CreateFurnitureButton(FurnitureType.Door, "Door", new Vector2(242f, 118f), new Vector2(78f, 118f), new Color(0.58f, 0.36f, 0.20f));
    }

    private void CreateCarpet()
    {
        Button carpetButton = CreateFurnitureButton(FurnitureType.Pet, "Mochi", new Vector2(132f, 356f), new Vector2(218f, 86f), new Color(0.90f, 0.96f, 0.98f));
        Text carpetText = carpetButton.GetComponentInChildren<Text>();
        carpetText.text = "Smile        Mochi\ncarpet";
        carpetText.fontSize = 18;
        carpetText.color = new Color(0.25f, 0.26f, 0.30f);
    }

    private void CreateDesk()
    {
        Button desk = CreateFurnitureButton(FurnitureType.Desk, "Laptop\nDesk", new Vector2(116f, 130f), new Vector2(116f, 106f), new Color(0.72f, 0.50f, 0.30f));
        desk.GetComponentInChildren<Text>().fontSize = 14;

        GameObject weight = UIFactory.CreateUIObject("Dumbbell", transform);
        UIFactory.AddImage(weight, new Color(0.18f, 0.19f, 0.22f));
        RectTransform rect = weight.GetComponent<RectTransform>();
        UIFactory.SetAnchored(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 82f), new Vector2(82f, 98f));
    }

    private void CreateWindow()
    {
        Button window = CreateFurnitureButton(FurnitureType.Board, "Window", new Vector2(254f, 320f), new Vector2(86f, 98f), new Color(0.56f, 0.76f, 0.95f));
        window.GetComponentInChildren<Text>().fontSize = 13;
    }

    private void CreateBed()
    {
        Button bed = CreateFurnitureButton(FurnitureType.Bed, "Bed", new Vector2(254f, 224f), new Vector2(118f, 76f), new Color(0.80f, 0.84f, 0.91f));
        bed.GetComponentInChildren<Text>().fontSize = 15;
    }

    private Button CreateFurnitureButton(FurnitureType type, string label, Vector2 center, Vector2 size, Color color)
    {
        Button button = UIFactory.CreateButton(type.ToString(), transform, label, color, UIFactory.Ink);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
        FurnitureInteraction interaction = button.gameObject.AddComponent<FurnitureInteraction>();
        interaction.Initialize(type, furniturePopupManager);
        return button;
    }
}
