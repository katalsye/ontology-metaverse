using UnityEngine;
using UnityEngine.UI;
using Firebase.Extensions;

public class HatSelectionUI : MonoBehaviour
{
    [Header("데이터")]
    public HatCatalog catalog;
    public Sprite     noHatSprite;

    [Header("스크롤뷰 Content 오브젝트")]
    public RectTransform gridContent;

    [Header("아이템 프리팹 (Button + Thumbnail + Text + LockOverlay)")]
    public GameObject itemPrefab;

    [Header("색상")]
    public Color selectedColor = new Color(0.6f, 0.9f, 1.0f);
    public Color lockedColor   = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color normalColor   = Color.white;

    Button[] _buttons;
    bool[]   _purchased;
    int      _selected = -1;

    void Start()
    {
        _purchased = new bool[catalog.hats.Length];
        for (int i = 0; i < catalog.hats.Length; i++)
            _purchased[i] = catalog.hats[i].isPurchased;

        BuildGrid();
        Select(0);
        LoadPurchasesFromFirestore();
    }

    void LoadPurchasesFromFirestore()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;
        string uid = auth.CurrentUser.UserId;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) return;
                if (!task.Result.ContainsField("purchasedHats")) return;

                var purchasedNames = task.Result.GetValue<System.Collections.Generic.List<string>>("purchasedHats");
                if (catalog?.hats == null) return;
                var purchased = new bool[catalog.hats.Length];
                for (int i = 0; i < catalog.hats.Length; i++)
                    purchased[i] = purchasedNames?.Contains(catalog.hats[i].displayName) ?? false;
                SetPurchased(purchased);
            });
    }

    public int GetSelectedIndex() => _selected;

    /// <summary>현재 선택된 모자가 구매된 것인지 반환 (없음=0번은 항상 true)</summary>
    public bool IsCurrentHatPurchased()
    {
        if (_selected == 0) return true;               // 모자 없음
        int hatIdx = _selected - 1;
        if (hatIdx < 0 || hatIdx >= _purchased.Length) return true;
        return _purchased[hatIdx];
    }

    // DB 연결 후 외부에서 구매 여부 주입
    public void SetPurchased(bool[] purchased)
    {
        _purchased = purchased;
        BuildGrid();
    }

    void BuildGrid()
    {
        foreach (Transform child in gridContent) Destroy(child.gameObject);

        int total  = catalog.hats.Length + 1; // +1 = 모자없음
        _buttons   = new Button[total];
        _buttons[0] = CreateItem(0, "없음", noHatSprite, true);

        for (int i = 0; i < catalog.hats.Length; i++)
        {
            var hat = catalog.hats[i];
            _buttons[i + 1] = CreateItem(i + 1, hat.displayName, hat.thumbnail, _purchased[i]);
        }
    }

    Button CreateItem(int index, string label, Sprite thumbnail, bool isPurchased)
    {
        var go  = Instantiate(itemPrefab, gridContent);
        var btn = go.GetComponent<Button>();

        // Thumbnail 자식 우선, 없으면 루트 Image에 적용
        var img = go.transform.Find("Thumbnail")?.GetComponent<Image>()
                  ?? go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = thumbnail;
            img.color  = thumbnail == null ? new Color(0.85f, 0.85f, 0.85f) : Color.white;
        }


        var txt = go.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null) txt.gameObject.SetActive(false);

        var lockOverlay = go.transform.Find("LockOverlay")?.GetComponent<Image>();
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!isPurchased);

        int idx = index;
        // 미구매도 조회는 가능 — 저장 시점에 구매 여부 체크
        btn.onClick.AddListener(() => Select(idx));

        var colors = btn.colors;
        colors.normalColor   = isPurchased ? normalColor : lockedColor;
        colors.disabledColor = lockedColor;
        btn.colors = colors;

        return btn;
    }

    void Select(int index)
    {
        _selected = index;

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            bool isLocked   = i > 0 && !_purchased[i - 1];
            var  colors     = _buttons[i].colors;
            colors.normalColor = isLocked     ? lockedColor
                               : i == index   ? selectedColor
                                              : normalColor;
            _buttons[i].colors = colors;
        }

        if (HatAttacher.Instance != null)
            HatAttacher.Instance.Equip(index == 0 ? -1 : index - 1);
    }
}
