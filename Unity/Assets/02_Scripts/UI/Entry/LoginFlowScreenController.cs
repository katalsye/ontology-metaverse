using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 1-3. LoginFlowScreen 컨트롤러
/// 3단계 플로우: 로그인 → 권한 요청 → 프로필 설정
/// 완료 시 → 마이룸으로 이동
/// </summary>
public class LoginFlowScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;

    // 스텝
    private VisualElement stepLogin;
    private VisualElement stepPermissions;
    private VisualElement stepProfile;
    private VisualElement stepBarFill;
    private Label stepLabel;

    // 로그인 버튼
    private Button btnGoogle;
    private Button btnKakao;

    // 권한
    private Button btnPermNext;
    private Dictionary<string, bool> permissions = new Dictionary<string, bool>
    {
        { "gallery", false },
        { "location", false },
        { "health", false },
        { "usage", false },
    };

    // 프로필
    private TextField inputNickname;
    private TextField inputStatus;
    private Button btnStart;
    private Label nicknameHint;

    private int currentStep = 0;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 스텝 바인딩
        stepLogin = root.Q("step-login");
        stepPermissions = root.Q("step-permissions");
        stepProfile = root.Q("step-profile");
        stepBarFill = root.Q("step-bar-fill");
        stepLabel = root.Q<Label>("step-label");

        // 로그인 버튼
        btnGoogle = root.Q<Button>("btn-google");
        btnKakao = root.Q<Button>("btn-kakao");
        btnGoogle.clicked += () => OnSocialLogin("google");
        btnKakao.clicked += () => OnSocialLogin("kakao");

        // 권한 토글
        btnPermNext = root.Q<Button>("btn-perm-next");
        btnPermNext.clicked += OnPermissionsNext;
        SetupToggle("toggle-gallery", "gallery");
        SetupToggle("toggle-location", "location");
        SetupToggle("toggle-health", "health");
        SetupToggle("toggle-usage", "usage");

        // 프로필
        inputNickname = root.Q<TextField>("input-nickname");
        inputStatus = root.Q<TextField>("input-status");
        btnStart = root.Q<Button>("btn-start");
        nicknameHint = root.Q<Label>("nickname-hint");
        btnStart.clicked += OnStartClicked;

        // 닉네임 유효성 실시간 체크
        inputNickname.RegisterValueChangedCallback(OnNicknameChanged);

        // 초기 상태
        GoToStep(0);
    }

    /// <summary>
    /// 토글 스위치 설정 (클릭 시 on/off 전환)
    /// </summary>
    private void SetupToggle(string toggleName, string permKey)
    {
        var toggle = root.Q(toggleName);
        if (toggle == null) return;

        // 토글 내부 원(thumb) 추가
        var thumb = new VisualElement();
        thumb.name = $"{toggleName}-thumb";
        thumb.style.width = 22;
        thumb.style.height = 22;
        thumb.style.borderTopLeftRadius = 11;
        thumb.style.borderTopRightRadius = 11;
        thumb.style.borderBottomLeftRadius = 11;
        thumb.style.borderBottomRightRadius = 11;
        thumb.style.backgroundColor = new StyleColor(Color.white);
        thumb.style.position = Position.Absolute;
        thumb.style.top = 3;
        thumb.style.left = 3;
        thumb.style.transitionProperty = new List<StylePropertyName> { new("left") };
        thumb.style.transitionDuration = new List<TimeValue> { new(200, TimeUnit.Millisecond) };
        toggle.Add(thumb);

        toggle.RegisterCallback<ClickEvent>(evt =>
        {
            permissions[permKey] = !permissions[permKey];
            bool isOn = permissions[permKey];

            if (isOn)
            {
                toggle.AddToClassList("toggle--on");
                thumb.style.left = 23; // 48 - 22 - 3
            }
            else
            {
                toggle.RemoveFromClassList("toggle--on");
                thumb.style.left = 3;
            }
        });
    }

    /// <summary>
    /// 스텝 전환
    /// </summary>
    private void GoToStep(int step)
    {
        currentStep = step;

        // 모든 스텝 숨기기
        stepLogin.RemoveFromClassList("active");
        stepLogin.AddToClassList("hidden");
        stepPermissions.RemoveFromClassList("active");
        stepPermissions.AddToClassList("hidden");
        stepProfile.RemoveFromClassList("active");
        stepProfile.AddToClassList("hidden");

        // 현재 스텝 표시
        var current = step switch
        {
            0 => stepLogin,
            1 => stepPermissions,
            2 => stepProfile,
            _ => stepLogin,
        };
        current.RemoveFromClassList("hidden");
        current.AddToClassList("active");

        // 프로그래스 바 업데이트
        float progress = (step + 1f) / 3f * 100f;
        stepBarFill.style.width = Length.Percent(progress);
        stepLabel.text = $"{step + 1} / 3";
    }

    // ── 이벤트 핸들러 ──

    /// <summary>
    /// 소셜 로그인 버튼 클릭
    /// 실제 구현 시 Firebase Auth 호출
    /// </summary>
    private void OnSocialLogin(string provider)
    {
        Debug.Log($"[LoginFlow] 소셜 로그인: {provider}");

        // TODO: Firebase Auth 연동
        // FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)

        // 로그인 성공 가정 → 권한 요청으로
        GoToStep(1);
    }

    /// <summary>
    /// 권한 요청 → 프로필 설정으로
    /// </summary>
    private void OnPermissionsNext()
    {
        Debug.Log($"[LoginFlow] 권한 설정 완료: " +
            $"갤러리={permissions["gallery"]}, " +
            $"위치={permissions["location"]}, " +
            $"Health={permissions["health"]}, " +
            $"사용시간={permissions["usage"]}");

        // TODO: 실제 Android 권한 요청 호출
        // AndroidPermissionHelper.RequestPermissions(...)

        // 권한 설정 저장
        foreach (var kv in permissions)
        {
            PlayerPrefs.SetInt($"perm_{kv.Key}", kv.Value ? 1 : 0);
        }

        GoToStep(2);
    }

    /// <summary>
    /// 닉네임 유효성 검사
    /// </summary>
    private void OnNicknameChanged(ChangeEvent<string> evt)
    {
        string name = evt.newValue;
        bool valid = name.Length >= 2 && name.Length <= 12;

        if (name.Length > 0 && !valid)
        {
            nicknameHint.style.color = new StyleColor(AppColors.Red);
            nicknameHint.text = name.Length < 2 ? "2자 이상 입력해주세요" : "12자 이하로 입력해주세요";
        }
        else
        {
            nicknameHint.style.color = new StyleColor(AppColors.InkFaint);
            nicknameHint.text = "2~12자, 한글/영문/숫자";
        }

        btnStart.SetEnabled(valid);
    }

    /// <summary>
    /// 시작하기 버튼 → 온보딩 완료, 마이룸으로 이동
    /// </summary>
    private void OnStartClicked()
    {
        string nickname = inputNickname.value.Trim();
        string status = inputStatus.value.Trim();

        if (nickname.Length < 2)
        {
            Debug.LogWarning("[LoginFlow] 닉네임이 너무 짧음");
            return;
        }

        Debug.Log($"[LoginFlow] 프로필 저장: 닉네임={nickname}, 상태메시지={status}");

        // 로컬 저장 (오프라인 폴백용)
        PlayerPrefs.SetString("nickname", nickname);
        PlayerPrefs.SetString("status_message", status);
        PlayerPrefs.SetInt("onboarding_complete", 1);
        PlayerPrefs.Save();

        btnStart.SetEnabled(false);

        // Firestore에 프로필 저장 후 화면 전환
        UserManager.Instance.UpdateProfile(nickname, status,
            onSuccess: () => ScreenManager.Instance.GoTo("myroom"),
            onFailure: err =>
            {
                Debug.LogError($"[LoginFlow] 프로필 저장 실패: {err}");
                // Firestore 실패해도 로컬 저장 완료됐으므로 진행
                ScreenManager.Instance.GoTo("myroom");
            }
        );
    }
}
