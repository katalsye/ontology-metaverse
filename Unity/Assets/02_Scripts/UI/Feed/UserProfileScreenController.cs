using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 2-3. UserProfileScreen 컨트롤러
/// 유저 프로필 조회 + 팔로우/언팔로우
/// </summary>
public class UserProfileScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private Label nicknameLabel;
    private Label statusLabel;
    private Label followersCount;
    private Label followingCount;
    private Button btnFollow;

    private bool isFollowing = false;
    private string viewingUserId;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 바인딩
        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();

        nicknameLabel = root.Q<Label>("profile-nickname");
        statusLabel = root.Q<Label>("profile-status");
        followersCount = root.Q<Label>("stat-followers-count");
        followingCount = root.Q<Label>("stat-following-count");
        btnFollow = root.Q<Button>("btn-follow");

        btnFollow.clicked += OnFollowToggle;

        // 유저 데이터 로드
        viewingUserId = PlayerPrefs.GetString("viewing_user_id", "");
        LoadUserProfile(viewingUserId);
    }

    private void LoadUserProfile(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        UserManager.Instance.GetUserProfile(userId,
            onSuccess: profile =>
            {
                nicknameLabel.text = profile.Nickname;
                statusLabel.text = string.IsNullOrEmpty(profile.StatusMessage)
                    ? "" : profile.StatusMessage;

                FollowManager.Instance.GetFollowCounts(userId,
                    onSuccess: (followers, following) =>
                    {
                        followersCount.text = followers.ToString();
                        followingCount.text = following.ToString();
                    }
                );

                FollowManager.Instance.CheckIsFollowing(userId,
                    onResult: following =>
                    {
                        isFollowing = following;
                        UpdateFollowButton();
                    }
                );
            },
            onFailure: err => Debug.LogError($"[UserProfile] 프로필 로드 실패: {err}")
        );
    }

    private void OnFollowToggle()
    {
        btnFollow.SetEnabled(false);

        if (isFollowing)
        {
            FollowManager.Instance.Unfollow(viewingUserId,
                onSuccess: () =>
                {
                    isFollowing = false;
                    UpdateFollowButton();
                    btnFollow.SetEnabled(true);
                    int count = int.TryParse(followersCount.text, out int c) ? c : 0;
                    followersCount.text = Mathf.Max(0, count - 1).ToString();
                },
                onFailure: err =>
                {
                    Debug.LogError($"[UserProfile] 언팔로우 실패: {err}");
                    btnFollow.SetEnabled(true);
                }
            );
        }
        else
        {
            FollowManager.Instance.SendFollowRequest(viewingUserId,
                onSuccess: () =>
                {
                    // 요청 상태 — 수락 전까지 pending
                    btnFollow.text = "요청 중";
                    btnFollow.SetEnabled(false);
                },
                onFailure: err =>
                {
                    Debug.LogError($"[UserProfile] 팔로우 요청 실패: {err}");
                    btnFollow.SetEnabled(true);
                }
            );
        }
    }

    private void UpdateFollowButton()
    {
        if (isFollowing)
        {
            btnFollow.text = "팔로잉";
            btnFollow.AddToClassList("following");
        }
        else
        {
            btnFollow.text = "팔로우";
            btnFollow.RemoveFromClassList("following");
        }
    }
}
