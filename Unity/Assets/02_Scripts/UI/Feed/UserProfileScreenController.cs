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

    /// <summary>
    /// 유저 프로필 데이터 로드
    /// </summary>
    private void LoadUserProfile(string userId)
    {
        // TODO: Firestore에서 유저 프로필 가져오기
        // var doc = await FirestoreManager.Instance.GetUserProfile(userId);

        // 테스트 데이터
        nicknameLabel.text = "김민수";
        statusLabel.text = "오늘도 열심히 살기 🌱";
        followersCount.text = "128";
        followingCount.text = "64";

        // 팔로우 상태 확인
        // TODO: 현재 유저가 이 유저를 팔로우하고 있는지 확인
        isFollowing = false;
        UpdateFollowButton();
    }

    /// <summary>
    /// 팔로우/언팔로우 토글
    /// </summary>
    private void OnFollowToggle()
    {
        isFollowing = !isFollowing;
        UpdateFollowButton();

        if (isFollowing)
        {
            Debug.Log($"[UserProfile] 팔로우: {viewingUserId}");
            // TODO: Firestore 팔로우 처리
            int count = int.Parse(followersCount.text);
            followersCount.text = (count + 1).ToString();
        }
        else
        {
            Debug.Log($"[UserProfile] 언팔로우: {viewingUserId}");
            // TODO: Firestore 언팔로우 처리
            int count = int.Parse(followersCount.text);
            followersCount.text = Mathf.Max(0, count - 1).ToString();
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
