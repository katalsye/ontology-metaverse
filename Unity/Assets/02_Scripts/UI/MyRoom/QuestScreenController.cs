using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// 4-11. QuestScreen 컨트롤러
/// 진행 중/완료/일일 탭 + 카드에 유형 라벨 + 상세 오버레이
/// </summary>
public class QuestScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("References")]
    [SerializeField] private RewardPopupController rewardPopup;

    [Header("Audio")]
    [Tooltip("AudioManager.sfxClips 배열 내 퀘스트 완료 SFX 인덱스")]
    [SerializeField] private int questCompleteSfxIndex = 0;

    private VisualElement root;
    private ScrollView questList;
    private VisualElement emptyState;
    private Label emptyText;

    // 탭
    private Button tabActive, tabDone, tabDaily;
    private string currentTab = "active";

    // 상세 오버레이
    private VisualElement detailOverlay;
    private Label detailTitle, detailDesc, detailProgressText;
    private VisualElement detailTypePill, detailProgressFill;
    private Label detailRewardAmount;
    private Button btnClaim;

    // 데이터
    private List<QuestData> allQuests = new List<QuestData>();
    private QuestData selectedQuest;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoTo("myroom", false);

        questList = root.Q<ScrollView>("quest-list");
        emptyState = root.Q("empty-state");
        emptyText = root.Q<Label>("empty-text");

        // 탭
        tabActive = root.Q<Button>("tab-active");
        tabDone = root.Q<Button>("tab-done");
        tabDaily = root.Q<Button>("tab-daily");
        tabActive.clicked += () => SetTab("active");
        tabDone.clicked += () => SetTab("done");
        tabDaily.clicked += () => SetTab("daily");

        // 상세 오버레이
        detailOverlay = root.Q("quest-detail-overlay");
        detailTitle = root.Q<Label>("detail-title");
        detailDesc = root.Q<Label>("detail-desc");
        detailTypePill = root.Q("detail-type-pill");
        detailProgressText = root.Q<Label>("detail-progress-text");
        detailProgressFill = root.Q("detail-progress-fill");
        detailRewardAmount = root.Q<Label>("detail-reward-amount");
        btnClaim = root.Q<Button>("btn-claim");

        root.Q<Button>("btn-close-detail").clicked += CloseDetail;
        btnClaim.clicked += OnClaimReward;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged += OnQuestsUpdated;
            QuestManager.Instance.OnQuestAutoCompleted += OnQuestAutoCompleted;
            QuestManager.Instance.StartQuestListener();
        }

        // Firebase 응답 전에도 빈 상태를 즉시 표시
        RenderQuests();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged -= OnQuestsUpdated;
            QuestManager.Instance.OnQuestAutoCompleted -= OnQuestAutoCompleted;
        }
    }

    private void OnQuestsUpdated(System.Collections.Generic.List<Quest> quests)
    {
        allQuests = quests.ConvertAll(MapToQuestData);
        RenderQuests();
    }

    private void OnQuestAutoCompleted(Quest quest)
    {
        AudioManager.Instance?.PlaySFX(questCompleteSfxIndex);
        SetTab("done");
        ShowCompletionToast(quest.Title);
    }

    private void ShowCompletionToast(string questTitle)
    {
        // 중복 토스트 방지
        root.Q("quest-completion-toast")?.RemoveFromHierarchy();

        var toast = new VisualElement { name = "quest-completion-toast" };
        toast.style.position = Position.Absolute;
        toast.style.top = 80;
        toast.style.left = Length.Percent(10);
        toast.style.right = Length.Percent(10);
        toast.style.backgroundColor = new StyleColor(new Color(0.18f, 0.72f, 0.42f, 0.95f));
        toast.style.borderTopLeftRadius = toast.style.borderTopRightRadius =
        toast.style.borderBottomLeftRadius = toast.style.borderBottomRightRadius = 12;
        toast.style.paddingTop = toast.style.paddingBottom = 10;
        toast.style.paddingLeft = toast.style.paddingRight = 16;
        toast.style.flexDirection = FlexDirection.Row;
        toast.style.alignItems = Align.Center;

        var icon = new Label("✓");
        icon.style.fontSize = 18;
        icon.style.color = new StyleColor(Color.white);
        icon.style.marginRight = 10;

        var text = new Label($"퀘스트 완료!\n{questTitle}");
        text.style.color = new StyleColor(Color.white);
        text.style.whiteSpace = WhiteSpace.Normal;
        text.style.flexGrow = 1;

        toast.Add(icon);
        toast.Add(text);
        root.Add(toast);

        root.schedule.Execute(() =>
        {
            toast.RemoveFromHierarchy();
        }).ExecuteLater(3500);
    }

    private QuestData MapToQuestData(Quest q)
    {
        var type = q.QuestType switch
        {
            "보완형" => QuestType.DataFill,
            "개선형" => QuestType.LifeImprove,
            "일일"  => QuestType.Daily,
            _       => QuestType.DataFill,
        };
        var status = q.IsCompleted ? QuestStatus.Done : QuestStatus.Active;
        float progress = q.IsCompleted ? 1f : 0f;

        return new QuestData(q.Index, q.Title, "", type, status, progress, q.RewardAmount, q.Claimed, q.CompletedAt ?? "");
    }

    private void SetTab(string tab)
    {
        currentTab = tab;

        tabActive.RemoveFromClassList("tab-btn--active");
        tabDone.RemoveFromClassList("tab-btn--active");
        tabDaily.RemoveFromClassList("tab-btn--active");

        switch (tab)
        {
            case "active": tabActive.AddToClassList("tab-btn--active"); break;
            case "done": tabDone.AddToClassList("tab-btn--active"); break;
            case "daily": tabDaily.AddToClassList("tab-btn--active"); break;
        }

        RenderQuests();
    }

    private void RenderQuests()
    {
        questList.contentContainer.Clear();

        var filtered = currentTab switch
        {
            "active" => allQuests
                .Where(q => q.status == QuestStatus.Active && q.type != QuestType.Daily)
                .ToList(),
            "done" => allQuests
                .Where(q => q.status == QuestStatus.Done)
                .OrderByDescending(q =>
                {
                    DateTime.TryParse(q.completedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt);
                    return dt;
                })
                .ToList(),
            "daily" => allQuests.Where(q => q.type == QuestType.Daily).ToList(),
            _ => allQuests,
        };

        if (filtered.Count == 0)
        {
            emptyState.AddToClassList("empty-state--visible");
            questList.style.display = DisplayStyle.None;
            emptyText.text = currentTab switch
            {
                "active" => "진행 중인 퀘스트가 없어요",
                "done" => "완료한 퀘스트가 없어요",
                "daily" => "오늘의 일일 퀘스트가 없어요",
                _ => "",
            };
            return;
        }

        emptyState.RemoveFromClassList("empty-state--visible");
        questList.style.display = DisplayStyle.Flex;

        foreach (var quest in filtered)
        {
            questList.contentContainer.Add(CreateQuestCard(quest));
        }
    }

    private VisualElement CreateQuestCard(QuestData quest)
    {
        var card = new VisualElement();
        card.AddToClassList("quest-card");

        // 헤더: 제목 + 유형 라벨
        var header = new VisualElement();
        header.AddToClassList("quest-card-header");

        var title = new Label(quest.title);
        title.AddToClassList("quest-card-title");

        var typePill = new Label(quest.type switch
        {
            QuestType.DataFill => "데이터 보완",
            QuestType.LifeImprove => "삶 개선",
            QuestType.Daily => "일일",
            _ => "",
        });
        typePill.AddToClassList("quest-type-pill");
        typePill.AddToClassList(quest.type switch
        {
            QuestType.DataFill => "quest-type--data",
            QuestType.LifeImprove => "quest-type--life",
            QuestType.Daily => "quest-type--daily",
            _ => "",
        });

        header.Add(title);
        header.Add(typePill);

        // 설명
        var desc = new Label(quest.description);
        desc.AddToClassList("quest-card-desc");

        // 진행 바
        var progressRow = new VisualElement();
        progressRow.AddToClassList("quest-progress-row");

        var progressBg = new VisualElement();
        progressBg.AddToClassList("progress-bg");
        var progressFill = new VisualElement();
        progressFill.AddToClassList("progress-fill");
        progressFill.style.width = Length.Percent(quest.progress * 100f);
        progressBg.Add(progressFill);

        var progressText = new Label($"{(quest.progress * 100):F0}%");
        progressText.AddToClassList("quest-progress-text");

        progressRow.Add(progressBg);
        progressRow.Add(progressText);

        // 보상 미리보기
        var rewardRow = new VisualElement();
        rewardRow.AddToClassList("quest-reward-preview");
        var coinIcon = new VisualElement();
        coinIcon.AddToClassList("reward-icon");
        var coinAmount = new Label($"+{quest.rewardCoins}");
        coinAmount.AddToClassList("reward-amount");
        rewardRow.Add(coinIcon);
        rewardRow.Add(coinAmount);

        card.Add(header);
        card.Add(desc);
        card.Add(progressRow);
        card.Add(rewardRow);

        // 클릭 → 상세
        card.RegisterCallback<ClickEvent>(evt => ShowDetail(quest));

        return card;
    }

    private void ShowDetail(QuestData quest)
    {
        selectedQuest = quest;

        detailTitle.text = quest.title;
        detailDesc.text = quest.description;
        detailProgressText.text = $"{(quest.progress * 100):F0}%";
        detailProgressFill.style.width = Length.Percent(quest.progress * 100f);
        detailRewardAmount.text = quest.rewardCoins.ToString();

        // 유형 라벨
        detailTypePill.Clear();
        var pillLabel = new Label(quest.type switch
        {
            QuestType.DataFill => "데이터 보완",
            QuestType.LifeImprove => "삶 개선",
            QuestType.Daily => "일일",
            _ => "",
        });
        pillLabel.AddToClassList("quest-type-pill");
        detailTypePill.Add(pillLabel);

        // 보상 수령 버튼 (완료 + 미수령일 때만)
        btnClaim.style.display = (quest.status == QuestStatus.Done && !quest.claimed)
            ? DisplayStyle.Flex : DisplayStyle.None;

        detailOverlay.style.display = DisplayStyle.Flex;
    }

    private void CloseDetail()
    {
        detailOverlay.style.display = DisplayStyle.None;
        selectedQuest = null;
    }

    private void OnClaimReward()
    {
        if (selectedQuest == null) return;

        btnClaim.SetEnabled(false);

        QuestManager.Instance.ClaimReward(selectedQuest.id, selectedQuest.rewardCoins,
            onSuccess: () =>
            {
                int coins = selectedQuest.rewardCoins;
                AudioManager.Instance?.PlaySFX(1);
                CloseDetail();
                rewardPopup?.Show("코인", coins, null);
            },
            onFailure: err =>
            {
                Debug.LogError($"[Quest] 보상 수령 실패: {err}");
                btnClaim.SetEnabled(true);
            }
        );
    }
}

public enum QuestType { DataFill, LifeImprove, Daily }
public enum QuestStatus { Active, Done }

public class QuestData
{
    public int id;            // quests 배열 내 위치 (Index)
    public string title;
    public string description;
    public QuestType type;
    public QuestStatus status;
    public float progress;
    public int rewardCoins;
    public bool claimed;
    public string completedAt;  // ISO 8601, done탭 정렬용

    public QuestData(int id, string title, string description,
                     QuestType type, QuestStatus status, float progress,
                     int rewardCoins, bool claimed, string completedAt = "")
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.type = type;
        this.status = status;
        this.progress = progress;
        this.rewardCoins = rewardCoins;
        this.claimed = claimed;
        this.completedAt = completedAt;
    }
}
