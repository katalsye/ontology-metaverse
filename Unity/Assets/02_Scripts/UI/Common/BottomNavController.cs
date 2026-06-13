using UnityEngine;
using UnityEngine.UIElements;
using System;

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

        // FCM 미읽음 수 → feed 뱃지
        if (FcmManager.Instance != null)
            FcmManager.Instance.OnUnreadCountChanged += OnUnreadNotificationsChanged;

        // 미완료 퀘스트 수 → quest 뱃지
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnUnreadQuestCountChanged += OnUnreadQuestCountChanged;

        SetActiveTab(0); // 기본: 마이룸
    }

    private void Start()
    {
        // ScreenManager.Awake()가 모든 오브젝트의 Awake 단계에서 Instance를 설정하므로
        // Start 시점에는 항상 준비되어 있음 (OnEnable 시점은 실행 순서에 따라 null일 수 있음)
        ScreenManager.Instance.OnScreenChanged += OnScreenChanged;
    }

    private void OnDisable()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.OnScreenChanged -= OnScreenChanged;

        if (FcmManager.Instance != null)
            FcmManager.Instance.OnUnreadCountChanged -= OnUnreadNotificationsChanged;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnUnreadQuestCountChanged -= OnUnreadQuestCountChanged;
    }

    private void OnUnreadNotificationsChanged(int count)
    {
        SetBadge("feed", count > 0);
    }

    private void OnUnreadQuestCountChanged(int count)
    {
        SetBadge("quest", count > 0);
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
