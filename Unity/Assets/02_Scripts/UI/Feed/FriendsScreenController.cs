using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 2-2. FriendsScreen 컨트롤러
/// 팔로우 요청 수락/거절 + 친구 검색/추가
/// </summary>
public class FriendsScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private TextField inputSearch;
    private VisualElement requestSection;
    private VisualElement searchResultSection;
    private VisualElement friendsListSection;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 바인딩
        root.Q<Button>("btn-back").clicked += OnBackClicked;
        root.Q<Button>("btn-search").clicked += OnSearchClicked;

        inputSearch = root.Q<TextField>("input-search");
        requestSection = root.Q("request-section");
        searchResultSection = root.Q("search-result-section");
        friendsListSection = root.Q("friends-list-section");

        // 템플릿 숨기기
        var requestTemplate = root.Q("request-template");
        if (requestTemplate != null)
            requestTemplate.style.display = DisplayStyle.None;

        // 데이터 로드
        LoadFollowRequests();
        LoadFriendsList();
    }

    /// <summary>
    /// 팔로우 요청 목록 로드
    /// </summary>
    private void LoadFollowRequests()
    {
        // TODO: Firestore에서 팔로우 요청 가져오기

        // 테스트 데이터
        var requests = new List<(string userId, string name)>
        {
            ("req1", "새로운유저1"),
            ("req2", "새로운유저2"),
        };

        foreach (var req in requests)
        {
            var row = CreateRequestRow(req.userId, req.name);
            requestSection.Add(row);
        }
    }

    /// <summary>
    /// 친구 목록 로드
    /// </summary>
    private void LoadFriendsList()
    {
        // TODO: Firestore에서 팔로잉 목록 가져오기

        var friends = new List<(string userId, string name)>
        {
            ("f1", "김민수"),
            ("f2", "이지은"),
            ("f3", "박서준"),
        };

        foreach (var f in friends)
        {
            var row = CreateFriendRow(f.userId, f.name, true);
            friendsListSection.Add(row);
        }
    }

    /// <summary>
    /// 팔로우 요청 행 생성
    /// </summary>
    private VisualElement CreateRequestRow(string userId, string name)
    {
        var row = new VisualElement();
        row.AddToClassList("friend-row");
        row.AddToClassList("row");

        // 아바타
        var avatar = new VisualElement();
        avatar.AddToClassList("friend-avatar");
        avatar.AddToClassList("center-content");
        var emoji = new Label("😊");
        emoji.AddToClassList("friend-avatar-emoji");
        avatar.Add(emoji);

        // 이름
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("friend-name");

        // 수락/거절 버튼
        var actions = new VisualElement();
        actions.AddToClassList("request-actions");
        actions.AddToClassList("row");

        var btnAccept = new Button(() => OnAcceptRequest(userId, row)) { text = "수락" };
        btnAccept.AddToClassList("btn");
        btnAccept.AddToClassList("btn--sm");
        btnAccept.AddToClassList("btn--rose");

        var btnReject = new Button(() => OnRejectRequest(userId, row)) { text = "거절" };
        btnReject.AddToClassList("btn");
        btnReject.AddToClassList("btn--sm");
        btnReject.AddToClassList("btn--ghost");

        actions.Add(btnAccept);
        actions.Add(btnReject);

        row.Add(avatar);
        row.Add(nameLabel);
        row.Add(actions);

        return row;
    }

    /// <summary>
    /// 친구/검색 결과 행 생성
    /// </summary>
    private VisualElement CreateFriendRow(string userId, string name, bool isFollowing)
    {
        var row = new VisualElement();
        row.AddToClassList("friend-row");
        row.AddToClassList("row");

        // 아바타
        var avatar = new VisualElement();
        avatar.AddToClassList("friend-avatar");
        avatar.AddToClassList("center-content");
        var emoji = new Label("😊");
        emoji.AddToClassList("friend-avatar-emoji");
        avatar.Add(emoji);

        // 이름 (클릭 시 프로필로)
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("friend-name");
        nameLabel.RegisterCallback<ClickEvent>(evt =>
        {
            PlayerPrefs.SetString("viewing_user_id", userId);
            ScreenManager.Instance.GoTo("user_profile");
        });

        // 팔로우 버튼
        var followBtn = new Button();
        followBtn.AddToClassList("follow-btn");

        if (isFollowing)
        {
            followBtn.text = "팔로잉";
            followBtn.AddToClassList("follow-btn--following");
        }
        else
        {
            followBtn.text = "팔로우";
            followBtn.AddToClassList("follow-btn--follow");
        }

        bool following = isFollowing;
        followBtn.clicked += () =>
        {
            following = !following;
            if (following)
            {
                followBtn.text = "팔로잉";
                followBtn.RemoveFromClassList("follow-btn--follow");
                followBtn.AddToClassList("follow-btn--following");
            }
            else
            {
                followBtn.text = "팔로우";
                followBtn.RemoveFromClassList("follow-btn--following");
                followBtn.AddToClassList("follow-btn--follow");
            }
            // TODO: Firestore 팔로우/언팔로우 처리
            Debug.Log($"[Friends] {(following ? "팔로우" : "언팔로우")}: {name}");
        };

        row.Add(avatar);
        row.Add(nameLabel);
        row.Add(followBtn);

        return row;
    }

    // ── 이벤트 ──

    private void OnSearchClicked()
    {
        string query = inputSearch.value.Trim();
        if (string.IsNullOrEmpty(query)) return;

        Debug.Log($"[Friends] 검색: {query}");

        // TODO: Firestore에서 닉네임 검색
        // 검색 결과를 searchResultSection에 렌더링

        // 기존 검색 결과 제거
        var toRemove = new List<VisualElement>();
        foreach (var child in searchResultSection.Children())
        {
            if (child is not Label) toRemove.Add(child);
        }
        foreach (var child in toRemove)
            searchResultSection.Remove(child);

        // 테스트: 검색어를 포함한 결과
        var result = CreateFriendRow("search1", $"{query}_유저", false);
        searchResultSection.Add(result);
    }

    private void OnAcceptRequest(string userId, VisualElement row)
    {
        Debug.Log($"[Friends] 팔로우 요청 수락: {userId}");
        // TODO: Firestore 팔로우 수락 처리
        row.RemoveFromHierarchy();
    }

    private void OnRejectRequest(string userId, VisualElement row)
    {
        Debug.Log($"[Friends] 팔로우 요청 거절: {userId}");
        // TODO: Firestore 팔로우 거절 처리
        row.RemoveFromHierarchy();
    }

    private void OnBackClicked()
    {
        ScreenManager.Instance.GoBack();
    }
}
