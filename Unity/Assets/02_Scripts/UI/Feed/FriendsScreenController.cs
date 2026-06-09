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
    private Button btnSearch;

    private int _searchGen;
    private int _friendsLoadGen;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += OnBackClicked;
        btnSearch = root.Q<Button>("btn-search");
        btnSearch.clicked += OnSearchClicked;

        inputSearch = root.Q<TextField>("input-search");
        requestSection = root.Q("request-section");
        searchResultSection = root.Q("search-result-section");
        friendsListSection = root.Q("friends-list-section");

        var requestTemplate = root.Q("request-template");
        if (requestTemplate != null)
            requestTemplate.style.display = DisplayStyle.None;

        LoadFollowRequests();
        LoadFriendsList();
    }

    // ── 데이터 로드 ──

    private void LoadFollowRequests()
    {
        FollowManager.Instance.GetPendingRequests(
            onSuccess: relations =>
            {
                if (!isActiveAndEnabled) return;
                requestSection.Clear();
                foreach (var rel in relations)
                {
                    UserManager.Instance.GetUserProfileForFollow(rel.FromUid,
                        onSuccess: profile =>
                        {
                            if (!isActiveAndEnabled) return;
                            var row = CreateRequestRow(rel.FromUid, profile.Nickname);
                            requestSection.Add(row);
                        },
                        onFailure: _ => { }
                    );
                }
            },
            onFailure: err => Debug.LogWarning($"[Friends] 팔로우 요청 로드 실패: {err}")
        );
    }

    private void LoadFriendsList()
    {
        friendsListSection.Clear();
        int gen = ++_friendsLoadGen;

        FollowManager.Instance.GetFollowings(
            onSuccess: relations =>
            {
                if (!isActiveAndEnabled || _friendsLoadGen != gen) return;
                foreach (var rel in relations)
                {
                    UserManager.Instance.GetUserProfileForFollow(rel.ToUid,
                        onSuccess: profile =>
                        {
                            if (!isActiveAndEnabled || _friendsLoadGen != gen) return;
                            var row = CreateFriendRow(rel.ToUid, profile.Nickname, true);
                            friendsListSection.Add(row);
                        },
                        onFailure: _ => { }
                    );
                }
            },
            onFailure: err => Debug.LogWarning($"[Friends] 친구 목록 로드 실패: {err}")
        );
    }

    // ── UI 생성 ──

    private VisualElement CreateRequestRow(string fromUid, string name)
    {
        var row = new VisualElement();
        row.AddToClassList("friend-row");
        row.AddToClassList("row");

        var avatar = new VisualElement();
        avatar.AddToClassList("friend-avatar");
        avatar.AddToClassList("center-content");
        var emoji = new Label("😊");
        emoji.AddToClassList("friend-avatar-emoji");
        avatar.Add(emoji);

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("friend-name");

        var actions = new VisualElement();
        actions.AddToClassList("request-actions");
        actions.AddToClassList("row");

        var btnAccept = new Button(() => OnAcceptRequest(fromUid, row)) { text = "수락" };
        btnAccept.AddToClassList("btn");
        btnAccept.AddToClassList("btn--sm");
        btnAccept.AddToClassList("btn--rose");

        var btnReject = new Button(() => OnRejectRequest(fromUid, row)) { text = "거절" };
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

    private VisualElement CreateFriendRow(string userId, string name, bool isFollowing)
    {
        var row = new VisualElement();
        row.AddToClassList("friend-row");
        row.AddToClassList("row");

        var avatar = new VisualElement();
        avatar.AddToClassList("friend-avatar");
        avatar.AddToClassList("center-content");
        var emoji = new Label("😊");
        emoji.AddToClassList("friend-avatar-emoji");
        avatar.Add(emoji);

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("friend-name");
        nameLabel.RegisterCallback<ClickEvent>(evt =>
        {
            PlayerPrefs.SetString("viewing_user_id", userId);
            ScreenManager.Instance.GoTo("user_profile");
        });

        var followBtn = new Button();
        followBtn.AddToClassList("follow-btn");

        SetFollowButtonState(followBtn, isFollowing);

        bool following = isFollowing;
        followBtn.clicked += () =>
        {
            followBtn.SetEnabled(false);

            if (following)
            {
                FollowManager.Instance.Unfollow(userId,
                    onSuccess: () =>
                    {
                        following = false;
                        SetFollowButtonState(followBtn, false);
                        followBtn.SetEnabled(true);
                    },
                    onFailure: err =>
                    {
                        Debug.LogError($"[Friends] 언팔로우 실패: {err}");
                        followBtn.SetEnabled(true);
                    }
                );
            }
            else
            {
                FollowManager.Instance.SendFollowRequest(userId,
                    onSuccess: () =>
                    {
                        followBtn.text = "요청 완료";
                        followBtn.RemoveFromClassList("follow-btn--follow");
                        followBtn.AddToClassList("follow-btn--following");
                        // 요청 수락 전이므로 버튼 비활성 유지
                    },
                    onFailure: err =>
                    {
                        Debug.LogError($"[Friends] 팔로우 요청 실패: {err}");
                        followBtn.SetEnabled(true);
                    }
                );
            }
        };

        row.Add(avatar);
        row.Add(nameLabel);
        row.Add(followBtn);

        return row;
    }

    private void SetFollowButtonState(Button btn, bool following)
    {
        if (following)
        {
            btn.text = "팔로잉";
            btn.RemoveFromClassList("follow-btn--follow");
            btn.AddToClassList("follow-btn--following");
        }
        else
        {
            btn.text = "팔로우";
            btn.RemoveFromClassList("follow-btn--following");
            btn.AddToClassList("follow-btn--follow");
        }
    }

    // ── 이벤트 ──

    private void OnSearchClicked()
    {
        string query = inputSearch.value.Trim();
        if (string.IsNullOrEmpty(query)) return;

        int gen = ++_searchGen;
        btnSearch.SetEnabled(false);

        var toRemove = new List<VisualElement>();
        foreach (var child in searchResultSection.Children())
        {
            if (child is not Label) toRemove.Add(child);
        }
        foreach (var child in toRemove)
            searchResultSection.Remove(child);

        UserManager.Instance.SearchUserByNickname(query,
            onSuccess: profiles =>
            {
                if (!isActiveAndEnabled || _searchGen != gen) return;
                btnSearch.SetEnabled(true);

                if (profiles.Count == 0)
                {
                    Debug.Log($"[Friends] 검색 결과 없음: {query}");
                    return;
                }

                foreach (var profile in profiles)
                {
                    FollowManager.Instance.CheckIsFollowing(profile.Uid,
                        onResult: isFollowing =>
                        {
                            if (!isActiveAndEnabled || _searchGen != gen) return;
                            var row = CreateFriendRow(profile.Uid, profile.Nickname, isFollowing);
                            searchResultSection.Add(row);
                        }
                    );
                }
            },
            onFailure: err =>
            {
                if (isActiveAndEnabled) btnSearch.SetEnabled(true);
                Debug.LogWarning($"[Friends] 검색 실패: {err}");
            }
        );
    }

    private void OnAcceptRequest(string fromUid, VisualElement row)
    {
        FollowManager.Instance.AcceptFollowRequest(fromUid,
            onSuccess: () =>
            {
                row.RemoveFromHierarchy();
                LoadFriendsList();
            },
            onFailure: err => Debug.LogError($"[Friends] 팔로우 수락 실패: {err}")
        );
    }

    private void OnRejectRequest(string fromUid, VisualElement row)
    {
        FollowManager.Instance.RejectFollowRequest(fromUid,
            onSuccess: () => row.RemoveFromHierarchy(),
            onFailure: err => Debug.LogError($"[Friends] 팔로우 거절 실패: {err}")
        );
    }

    private void OnBackClicked()
    {
        ScreenManager.Instance.GoBack();
    }
}
