using UnityEngine;
using UnityEngine.UI;

public class CharacterColorController : MonoBehaviour
{
    public static CharacterColorController Instance { get; private set; }

    [Header("캐릭터")]
    public SkinnedMeshRenderer characterRenderer;

    [Header("색상 버튼 (0~7 프리셋, 8 랜덤) — 순서대로 연결")]
    public Button[] colorButtons;
    public Sprite   randomSprite;

    static readonly Color[] Presets =
    {
        new Color(1.00f, 0.71f, 0.76f), // 0 분홍
        new Color(1.00f, 0.85f, 0.72f), // 1 살구
        new Color(1.00f, 0.96f, 0.60f), // 2 노랑
        new Color(0.72f, 0.95f, 0.60f), // 3 연두
        new Color(0.60f, 0.87f, 1.00f), // 4 하늘
        new Color(0.82f, 0.74f, 1.00f), // 5 라벤더
        new Color(0.72f, 0.52f, 0.35f), // 6 갈색
        new Color(1.00f, 1.00f, 1.00f), // 7 흰색
    };

    void Awake()
    {
        Instance = this;
        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    void Start()
    {
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] == null) continue;
            int idx = i;

            var img = colorButtons[idx].GetComponent<Image>();
            if (idx < Presets.Length)
                img.color = idx == 7 ? new Color(1.00f, 0.85f, 0.90f) : Presets[idx];
            else if (randomSprite != null)
            {
                img.sprite = randomSprite;
                img.color  = Color.white;
            }

            colorButtons[i].onClick.AddListener(() => OnColorSelected(idx));
        }
    }

    void OnColorSelected(int index)
    {
        Color color = index < Presets.Length
            ? Presets[index]
            : Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);

        Apply(color);
    }

    void Apply(Color color)
    {
        if (characterRenderer == null) return;
        var block = new MaterialPropertyBlock();
        characterRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color",     color);
        characterRenderer.SetPropertyBlock(block);
    }
}
