using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 7-1. BottomNav 컨트롤러
/// 마이룸/피드/퀘스트/상점 탭 + 뱃지
/// 3D 뷰 진입 시 자동 숨김
/// </summary>
public class BottomNavController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private VisualElement navBar;
    private Button[] tabs;
    private VisualElement[] badges;
    private string[] tabKeys = { "myroom", "feed", "quest", "shop" };

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;
        navBar = root.Q("bottom-nav");

        tabs = new Button[4];
        badges = new VisualElement[4];

        for (int i = 0; i < 4; i++)
        {
            tabs[i] = root.Q<Button>($"nav-{tabKeys[i]}");
            badges[i] = root.Q($"badge-{tabKeys[i]}");

            int index = i;
            tabs[i].clicked += () => OnTabClicked(index);
        }

        // 화면 전환 이벤트 구독
        ScreenManager.Instance.OnScreenChanged += OnScreenChanged;

        SetActiveTab(0); // 기본: 마이룸
    }

    private void OnDisable()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.OnScreenChanged -= OnScreenChanged;
    }

    private void OnScreenChanged(string screen)
    {
        // 3D 뷰에서는 숨김
        bool hide = ScreenManager.Instance.ShouldHideBottomNav();
        navBar.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;

        // 활성 탭 업데이트
        int tabIndex = Array.IndexOf(tabKeys, screen);
        if (tabIndex >= 0)
            SetActiveTab(tabIndex);
    }

    private void OnTabClicked(int index)
    {
        SetActiveTab(index);
        ScreenManager.Instance.GoTo(tabKeys[index], false);
    }

    private void SetActiveTab(int index)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i == index)
                tabs[i].AddToClassList("nav-tab--active");
            else
                tabs[i].RemoveFromClassList("nav-tab--active");
        }
    }

    /// <summary>
    /// 뱃지 표시/숨김
    /// </summary>
    public void SetBadge(string tabKey, bool visible)
    {
        int index = Array.IndexOf(tabKeys, tabKey);
        if (index >= 0 && badges[index] != null)
        {
            badges[index].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

/// <summary>
/// 7-4. RewardPopup 컨트롤러
/// 재화/아이템 획득 팝업
/// </summary>
public class RewardPopupController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement overlay;
    private Label rewardName;
    private Label rewardAmount;
    private Button btnConfirm;
    private Action onClose;

    private void Awake()
    {
        var root = uiDocument.rootVisualElement;
        overlay = root.Q("reward-overlay");
        rewardName = root.Q<Label>("reward-name");
        rewardAmount = root.Q<Label>("reward-amount");
        btnConfirm = root.Q<Button>("btn-reward-confirm");

        btnConfirm.clicked += () =>
        {
            overlay.style.display = DisplayStyle.None;
            onClose?.Invoke();
        };
    }

    public void Show(string itemName, int amount, Action callback)
    {
        rewardName.text = itemName;
        rewardAmount.text = $"× {amount}";
        onClose = callback;
        overlay.style.display = DisplayStyle.Flex;
    }
}

/// <summary>
/// 7-3. Toast 컨트롤러
/// 푸시 알림 토스트, 탭 시 해당 화면 이동
/// </summary>
public class ToastController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement toastContainer;
    private Label toastTitle;
    private Label toastBody;
    private string targetScreen;

    private void Awake()
    {
        var root = uiDocument.rootVisualElement;
        toastContainer = root.Q("toast-container");
        toastTitle = root.Q<Label>("toast-title");
        toastBody = root.Q<Label>("toast-body");

        toastContainer.RegisterCallback<ClickEvent>(evt =>
        {
            Hide();
            if (!string.IsNullOrEmpty(targetScreen))
                ScreenManager.Instance.GoTo(targetScreen);
        });
    }

    public void Show(string title, string body, string screen, float duration = 3f)
    {
        toastTitle.text = title;
        toastBody.text = body;
        targetScreen = screen;
        toastContainer.style.display = DisplayStyle.Flex;

        // 자동 숨김
        StartCoroutine(AutoHide(duration));
    }

    private IEnumerator AutoHide(float duration)
    {
        yield return new WaitForSeconds(duration);
        Hide();
    }

    private void Hide()
    {
        toastContainer.style.display = DisplayStyle.None;
    }
}

/// <summary>
/// 7-5. CurrencyBar 컨트롤러
/// 코인 + 보석 잔액 표시
/// </summary>
public class CurrencyBarController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private Label coinLabel;
    private Label gemLabel;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        coinLabel = root.Q<Label>("currency-coins");
        gemLabel = root.Q<Label>("currency-gems");
        Refresh();
    }

    public void Refresh()
    {
        int coins = PlayerPrefs.GetInt("coins", 0);
        int gems = PlayerPrefs.GetInt("gems", 0);
        coinLabel.text = coins.ToString();
        gemLabel.text = gems.ToString();
    }

    public void AddCoins(int amount)
    {
        int coins = PlayerPrefs.GetInt("coins", 0) + amount;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        Refresh();
    }

    public void AddGems(int amount)
    {
        int gems = PlayerPrefs.GetInt("gems", 0) + amount;
        PlayerPrefs.SetInt("gems", gems);
        PlayerPrefs.Save();
        Refresh();
    }
}
