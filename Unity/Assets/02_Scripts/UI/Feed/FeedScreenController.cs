using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;

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
        // Firestore에서 즐겨찾기·방문횟수 덮어쓰기 (비동기 — 완료되면 재정렬)
        LoadVisitDataFromFirestore();
    }

    /// <summary>
    /// 피드 데이터 로드 — 팔로잉 목록 → 각 유저 프로필 순차 조회
    /// </summary>
    private void LoadFeedData()
    {
        feedData.Clear();
        RenderCards();

        if (FollowManager.Instance == null)
        {
            Debug.LogError("[Feed] FollowManager.Instance가 null — MainScene의 Managers 오브젝트에 FollowManager 컴포넌트를 추가하세요.");
            return;
        }

        FollowManager.Instance.GetFollowings(
            onSuccess: followings =>
            {
                if (followings.Count == 0)
                {
                    ApplySortAndRender();
                    return;
                }

                int remaining = followings.Count;
                foreach (var relation in followings)
                {
                    UserManager.Instance.GetUserProfileForFollow(relation.ToUid,
                        onSuccess: profile =>
                        {
                            var lastActive = profile.CreatedAt.ToDateTime().ToLocalTime();
                            feedData.Add(new FeedCardData(
                                profile.Uid,
                                profile.Nickname,
                                string.IsNullOrEmpty(profile.StatusMessage) ? "..." : profile.StatusMessage,
                                ComputeTimeAgo(lastActive),
                                false,
                                lastActive
                            ));
                            remaining--;
                            if (remaining == 0) ApplySortAndRender();
                        },
                        onFailure: _ =>
                        {
                            remaining--;
                            if (remaining == 0) ApplySortAndRender();
                        }
                    );
                }
            },
            onFailure: err => Debug.LogWarning($"[Feed] 팔로잉 목록 로드 실패: {err}")
        );
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

        var time = new Label(string.IsNullOrEmpty(data.timeAgo) ? "" : data.timeAgo);
        time.AddToClassList("feed-card-time");

        topRow.Add(nickname);
        topRow.Add(time);

        var status = new Label(data.statusMessage);
        status.AddToClassList("feed-card-status");

        textArea.Add(topRow);
        textArea.Add(status);

        card.Add(avatar);
        card.Add(textArea);

        // 즐겨찾기 버튼
        var favBtn = new Button();
        favBtn.AddToClassList("fav-btn");
        favBtn.text = data.isFavorite ? "★" : "☆";
        favBtn.clicked += () =>
        {
            ToggleFavorite(data);
            favBtn.text = data.isFavorite ? "★" : "☆";
            if (currentSort == "favorite") ApplySortAndRender();
        };
        card.Add(favBtn);

        // 업데이트 점
        if (data.hasUpdate)
        {
            var dot = new VisualElement();
            dot.AddToClassList("update-dot");
            card.Add(dot);
        }

        // 카드 클릭 → 남의 방 (방문 횟수 증가)
        card.RegisterCallback<ClickEvent>(evt =>
        {
            // 즐겨찾기 버튼 클릭이면 방 이동 차단
            if (evt.target is Button) return;

            int cnt = PlayerPrefs.GetInt($"visit_{data.userId}", 0) + 1;
            PlayerPrefs.SetInt($"visit_{data.userId}", cnt);
            PlayerPrefs.Save();

            // Firestore에 방문 횟수 저장
            string myUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "";
            if (!string.IsNullOrEmpty(myUid))
                SaveVisitCountToFirestore(myUid, data.userId, cnt);

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

        btnSortRecent.RemoveFromClassList("sort-chip--active");
        btnSortFavorite.RemoveFromClassList("sort-chip--active");
        btnSortFrequent.RemoveFromClassList("sort-chip--active");

        switch (sort)
        {
            case "recent":   btnSortRecent.AddToClassList("sort-chip--active");   break;
            case "favorite": btnSortFavorite.AddToClassList("sort-chip--active"); break;
            case "frequent": btnSortFrequent.AddToClassList("sort-chip--active"); break;
        }

        ApplySortAndRender();
    }

    // ── 정렬 + 렌더 ──────────────────────────────────────────

    /// <summary>즐겨찾기·방문횟수를 PlayerPrefs에서 불러온 뒤 currentSort 기준으로 정렬하고 렌더.</summary>
    private void ApplySortAndRender()
    {
        string myUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "guest";
        string favStr = PlayerPrefs.GetString($"favs_{myUid}", "");
        var favSet = new HashSet<string>(
            favStr.Length > 0 ? favStr.Split(',') : new string[0]);

        foreach (var d in feedData)
        {
            d.isFavorite = favSet.Contains(d.userId);
            d.visitCount = PlayerPrefs.GetInt($"visit_{d.userId}", 0);
        }

        switch (currentSort)
        {
            case "recent":
                feedData.Sort((a, b) => b.lastActiveAt.CompareTo(a.lastActiveAt));
                break;
            case "favorite":
                feedData.Sort((a, b) =>
                {
                    if (a.isFavorite != b.isFavorite) return a.isFavorite ? -1 : 1;
                    return b.lastActiveAt.CompareTo(a.lastActiveAt);
                });
                break;
            case "frequent":
                feedData.Sort((a, b) => b.visitCount.CompareTo(a.visitCount));
                break;
        }

        RenderCards();
    }

    /// <summary>즐겨찾기 토글 후 PlayerPrefs + Firestore에 저장.</summary>
    private void ToggleFavorite(FeedCardData data)
    {
        data.isFavorite = !data.isFavorite;
        string myUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "guest";
        var favList = new List<string>();
        foreach (var d in feedData)
            if (d.isFavorite) favList.Add(d.userId);
        PlayerPrefs.SetString($"favs_{myUid}", string.Join(",", favList));
        PlayerPrefs.Save();

        if (myUid != "guest")
            SaveFavoritesToFirestore(myUid, favList);
    }

    private void LoadVisitDataFromFirestore()
    {
        var auth = FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;
        string myUid = auth.CurrentUser.UserId;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(myUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) return;
                var doc = task.Result;
                bool changed = false;

                if (doc.ContainsField("favorites"))
                {
                    var favList = doc.GetValue<List<string>>("favorites") ?? new List<string>();
                    PlayerPrefs.SetString($"favs_{myUid}", string.Join(",", favList));
                    changed = true;
                }
                if (doc.ContainsField("visitCounts"))
                {
                    var counts = doc.GetValue<Dictionary<string, object>>("visitCounts");
                    if (counts != null)
                        foreach (var kv in counts)
                            PlayerPrefs.SetInt($"visit_{kv.Key}", System.Convert.ToInt32(kv.Value));
                    changed = true;
                }

                if (changed) ApplySortAndRender();
            });
    }

    private void SaveFavoritesToFirestore(string myUid, List<string> favList)
    {
        var data = new Dictionary<string, object> { { "favorites", favList } };
        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(myUid)
            .UpdateAsync(data)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogWarning("[Feed] 즐겨찾기 저장 실패: " + t.Exception);
            });
    }

    private void SaveVisitCountToFirestore(string myUid, string targetUid, int count)
    {
        var data = new Dictionary<string, object>
        {
            { $"visitCounts.{targetUid}", (long)count }
        };
        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(myUid)
            .UpdateAsync(data)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogWarning("[Feed] 방문횟수 저장 실패: " + t.Exception);
            });
    }

    /// <summary>DateTime → 한국어 상대 시간 문자열.</summary>
    private static string ComputeTimeAgo(System.DateTime dt)
    {
        var diff = System.DateTime.Now - dt;
        if (diff.TotalMinutes < 1)  return "방금 전";
        if (diff.TotalHours  < 1)   return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalDays   < 1)   return $"{(int)diff.TotalHours}시간 전";
        if (diff.TotalDays   < 7)   return $"{(int)diff.TotalDays}일 전";
        return dt.ToString("M월 d일");
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

    // 정렬용
    public System.DateTime lastActiveAt;
    public bool isFavorite;
    public int visitCount;

    public FeedCardData(string userId, string nickname, string statusMessage,
                        string timeAgo, bool hasUpdate,
                        System.DateTime lastActiveAt = default)
    {
        this.userId = userId;
        this.nickname = nickname;
        this.statusMessage = statusMessage;
        this.timeAgo = timeAgo;
        this.hasUpdate = hasUpdate;
        this.lastActiveAt = lastActiveAt;
    }
}
