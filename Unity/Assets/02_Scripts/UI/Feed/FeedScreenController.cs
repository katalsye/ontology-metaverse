using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 2-1. FeedScreen 컨트롤러
/// 팔로워 방 카드 리스트 + 빈 상태 + 정렬
/// 카드 탭 → 남의 방 3D (ScreenManager)
/// 친구 관리 아이콘 → FriendsScreen
/// </summary>
public class FeedScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private ScrollView feedScroll;
    private VisualElement emptyState;
    private VisualElement cardTemplate;

    // 정렬 칩
    private Button btnSortRecent;
    private Button btnSortFavorite;
    private Button btnSortFrequent;
    private string currentSort = "recent";

    // 카드 데이터 (실제 구현 시 Firestore에서 가져옴)
    private List<FeedCardData> feedData = new List<FeedCardData>();

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        feedScroll = root.Q<ScrollView>("feed-scroll");
        emptyState = root.Q("empty-state");
        cardTemplate = root.Q("card-template");

        // 템플릿 숨기기 (동적 생성용)
        if (cardTemplate != null)
            cardTemplate.style.display = DisplayStyle.None;

        // 버튼 바인딩
        root.Q<Button>("btn-friends").clicked += OnFriendsClicked;
        root.Q<Button>("btn-sort").clicked += ToggleSortBar;
        root.Q<Button>("btn-add-friend").clicked += OnFriendsClicked;

        // 정렬 칩
        btnSortRecent = root.Q<Button>("btn-sort-recent");
        btnSortFavorite = root.Q<Button>("btn-sort-favorite");
        btnSortFrequent = root.Q<Button>("btn-sort-frequent");

        btnSortRecent.clicked += () => SetSort("recent");
        btnSortFavorite.clicked += () => SetSort("favorite");
        btnSortFrequent.clicked += () => SetSort("frequent");

        // 데이터 로드
        LoadFeedData();
    }

    /// <summary>
    /// 피드 데이터 로드 (Firestore 연동 포인트)
    /// </summary>
    private void LoadFeedData()
    {
        // TODO: Firestore에서 팔로워 목록 + 최근 업데이트 가져오기
        // var snapshot = await FirestoreManager.Instance.GetFeed(userId);

        // 테스트 데이터
        feedData = new List<FeedCardData>
        {
            new FeedCardData("user1", "김민수", "오늘 카페에서 공부 중 ☕", "3분 전", true),
            new FeedCardData("user2", "이지은", "퇴근하고 러닝 완료 🏃", "1시간 전", true),
            new FeedCardData("user3", "박서준", "주말 요리 도전!", "3시간 전", false),
            new FeedCardData("user4", "최유진", "도서관에서 독서 중 📚", "5시간 전", false),
        };

        RenderCards();
    }

    /// <summary>
    /// 카드 목록 렌더링
    /// </summary>
    private void RenderCards()
    {
        // 기존 카드 제거 (템플릿 제외)
        var children = new List<VisualElement>();
        foreach (var child in feedScroll.contentContainer.Children())
        {
            if (child != cardTemplate)
                children.Add(child);
        }
        foreach (var child in children)
            feedScroll.contentContainer.Remove(child);

        // 빈 상태 처리
        if (feedData.Count == 0)
        {
            emptyState.AddToClassList("empty-state--visible");
            feedScroll.style.display = DisplayStyle.None;
            return;
        }

        emptyState.RemoveFromClassList("empty-state--visible");
        feedScroll.style.display = DisplayStyle.Flex;

        // 카드 생성
        foreach (var data in feedData)
        {
            var card = CreateCard(data);
            feedScroll.contentContainer.Add(card);
        }
    }

    /// <summary>
    /// 개별 피드 카드 생성
    /// </summary>
    private VisualElement CreateCard(FeedCardData data)
    {
        var card = new VisualElement();
        card.AddToClassList("feed-card");
        card.AddToClassList("row");

        // 아바타
        var avatar = new VisualElement();
        avatar.AddToClassList("feed-avatar");
        avatar.AddToClassList("center-content");
        var avatarEmoji = new Label("😊");
        avatarEmoji.AddToClassList("feed-avatar-emoji");
        avatar.Add(avatarEmoji);

        // 텍스트 영역
        var textArea = new VisualElement();
        textArea.AddToClassList("feed-card-text");
        textArea.AddToClassList("col");

        var topRow = new VisualElement();
        topRow.AddToClassList("row");

        var nickname = new Label(data.nickname);
        nickname.AddToClassList("feed-card-name");

        var time = new Label(data.timeAgo);
        time.AddToClassList("feed-card-time");

        topRow.Add(nickname);
        topRow.Add(time);

        var status = new Label(data.statusMessage);
        status.AddToClassList("feed-card-status");

        textArea.Add(topRow);
        textArea.Add(status);

        card.Add(avatar);
        card.Add(textArea);

        // 업데이트 점
        if (data.hasUpdate)
        {
            var dot = new VisualElement();
            dot.AddToClassList("update-dot");
            card.Add(dot);
        }

        // 카드 클릭 → 남의 방
        card.RegisterCallback<ClickEvent>(evt =>
        {
            Debug.Log($"[Feed] 카드 탭: {data.nickname} → 남의 방");
            // TODO: 유저 ID 전달
            PlayerPrefs.SetString("visiting_user_id", data.userId);
            ScreenManager.Instance.GoTo("theirs_room");
        });

        return card;
    }

    /// <summary>
    /// 정렬 변경
    /// </summary>
    private void SetSort(string sort)
    {
        currentSort = sort;

        // 칩 스타일 업데이트
        btnSortRecent.RemoveFromClassList("sort-chip--active");
        btnSortFavorite.RemoveFromClassList("sort-chip--active");
        btnSortFrequent.RemoveFromClassList("sort-chip--active");

        switch (sort)
        {
            case "recent":
                btnSortRecent.AddToClassList("sort-chip--active");
                break;
            case "favorite":
                btnSortFavorite.AddToClassList("sort-chip--active");
                break;
            case "frequent":
                btnSortFrequent.AddToClassList("sort-chip--active");
                break;
        }

        // TODO: 정렬 로직 적용 후 RenderCards() 호출
        Debug.Log($"[Feed] 정렬 변경: {sort}");
    }

    private void ToggleSortBar()
    {
        var sortBar = root.Q("sort-bar");
        bool visible = sortBar.style.display == DisplayStyle.Flex;
        sortBar.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void OnFriendsClicked()
    {
        ScreenManager.Instance.GoTo("friends");
    }
}

/// <summary>
/// 피드 카드 데이터 모델
/// </summary>
public class FeedCardData
{
    public string userId;
    public string nickname;
    public string statusMessage;
    public string timeAgo;
    public bool hasUpdate;

    public FeedCardData(string userId, string nickname, string statusMessage,
                        string timeAgo, bool hasUpdate)
    {
        this.userId = userId;
        this.nickname = nickname;
        this.statusMessage = statusMessage;
        this.timeAgo = timeAgo;
        this.hasUpdate = hasUpdate;
    }
}
