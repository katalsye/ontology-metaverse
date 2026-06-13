using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Android;
using System.Collections;
using System.Collections.Generic;
using OntologyMetaverse.DataCollection.Health;
using OntologyMetaverse.DataCollection.Spotify;

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

    // 권한
    private Button btnPermNext;
    private Dictionary<string, bool> permissions = new Dictionary<string, bool>
    {
        { "gallery", false },
        { "location", false },
        { "health", false },
        { "usage", false },
        { "calendar", false },
        { "spotify", false },
    };

    // 토글 클릭 시 권한 다이얼로그 결과에 따라 비주얼을 되돌리기 위한 참조
    private readonly Dictionary<string, (VisualElement toggle, VisualElement thumb)> toggleElements
        = new Dictionary<string, (VisualElement toggle, VisualElement thumb)>();

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
        btnGoogle.clicked += () => OnSocialLogin();

        // 권한 토글
        btnPermNext = root.Q<Button>("btn-perm-next");
        btnPermNext.clicked += OnPermissionsNext;
        SetupToggle("toggle-gallery", "gallery");
        SetupToggle("toggle-location", "location");
        SetupToggle("toggle-health", "health");
        SetupToggle("toggle-usage", "usage");
        SetupToggle("toggle-calendar", "calendar");
        SetupToggle("toggle-spotify", "spotify");

        // 프로필
        inputNickname = root.Q<TextField>("input-nickname");
        inputStatus = root.Q<TextField>("input-status");
        btnStart = root.Q<Button>("btn-start");
        nicknameHint = root.Q<Label>("nickname-hint");
        btnStart.clicked += OnStartClicked;

        // 닉네임 유효성 실시간 체크
        inputNickname.RegisterValueChangedCallback(OnNicknameChanged);

        // 익명 인증(온보딩 중 다운로드용)은 "로그인 완료"로 취급하지 않음
        var currentUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        bool isProperlyLoggedIn = currentUser != null && !currentUser.IsAnonymous;

        if (isProperlyLoggedIn)
        {
            if (PlayerPrefs.HasKey("onboarding_complete"))
            {
                ScreenManager.Instance.ClearHistory();
                ScreenManager.Instance.GoTo("myroom");
            }
            else
                GoToStep(1); // 권한 요청 단계부터
            return;
        }

        GoToStep(0);
    }

    /// <summary>
    /// 토글 스위치 설정.
    /// 켜는 동작은 실제 권한 동의 결과가 확인된 후에만 ON으로 표시되고,
    /// 동의하지 않으면 미선택 상태로 되돌아간다. 끄는 동작은 별도 동의 절차 없이 즉시 반영.
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

        toggleElements[permKey] = (toggle, thumb);

        toggle.RegisterCallback<ClickEvent>(evt =>
        {
            if (permissions[permKey])
            {
                // 끄기: 권한 동의 자체를 회수할 수는 없으므로 앱 내 사용 동의만 철회
                permissions[permKey] = false;
                SetToggleVisual(toggle, thumb, false);
            }
            else
            {
                StartCoroutine(TryEnablePermission(permKey));
            }
        });
    }

    /// <summary>
    /// 권한 다이얼로그/설정 화면을 띄운 뒤, 실제 동의 여부를 확인해 토글 비주얼을 갱신한다.
    /// 동의하지 않았으면 토글은 미선택 상태로 유지(되돌아감)된다.
    /// </summary>
    private IEnumerator TryEnablePermission(string permKey)
    {
        yield return RequestPermissionFor(permKey);

        bool granted = CheckPermissionGranted(permKey);
        permissions[permKey] = granted;

        if (toggleElements.TryGetValue(permKey, out var elems))
            SetToggleVisual(elems.toggle, elems.thumb, granted);
    }

    private void SetToggleVisual(VisualElement toggle, VisualElement thumb, bool isOn)
    {
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
    }

    /// <summary>
    /// permKey에 해당하는 권한이 실제로 부여되었는지 확인.
    /// gallery/location/calendar: Permission.HasUserAuthorizedPermission (Editor에서는 항상 true)
    /// health: ACTIVITY_RECOGNITION + Health Connect 권한 모두 필요
    /// usage: PACKAGE_USAGE_STATS (시스템 설정에서 직접 허용)
    /// spotify: OAuth access token 보유 여부
    /// </summary>
    private bool CheckPermissionGranted(string permKey)
    {
        switch (permKey)
        {
            case "gallery":
#if UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission("android.permission.READ_MEDIA_IMAGES")
                    || Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead);
#else
                return true;
#endif

            case "location":
#if UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission(Permission.FineLocation);
#else
                return true;
#endif

            case "health":
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION")
                    && HealthConnectBridge.HasAllPermissions();
#else
                return true;
#endif

            case "usage":
                return IsUsageAccessGranted();

            case "calendar":
#if UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission("android.permission.READ_CALENDAR");
#else
                return true;
#endif

            case "spotify":
                return SpotifyCollector.Instance != null && SpotifyCollector.Instance.HasAuth();

            default:
                return false;
        }
    }

    /// <summary>
    /// 앱이 다시 포그라운드로 돌아왔을 때, 외부 설정 화면(Health Connect/사용 기록 액세스)에서
    /// 사용자가 권한을 허용했는지 재확인해 토글 상태를 동기화한다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        RecheckExternalPermission("health");
        RecheckExternalPermission("usage");
    }

    private void RecheckExternalPermission(string permKey)
    {
        if (permissions[permKey]) return; // 이미 ON
        if (!toggleElements.TryGetValue(permKey, out var elems)) return;

        if (CheckPermissionGranted(permKey))
        {
            permissions[permKey] = true;
            SetToggleVisual(elems.toggle, elems.thumb, true);
        }
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
    private void OnSocialLogin()
    {
        btnGoogle.SetEnabled(false);

        AuthService.Instance.SignInWithGoogle(
            onSuccess: () => GoToStep(1),
            onFailure: err =>
            {
                Debug.LogError($"[LoginFlow] 로그인 실패: {err}");
                btnGoogle.SetEnabled(true);
            }
        );
    }

    /// <summary>
    /// 권한 요청 → 프로필 설정으로
    /// 실제 권한 요청은 토글을 켤 때 바로 트리거되므로(RequestPermissionFor),
    /// 여기서는 최종 설정값 저장 후 다음 단계로 이동만 한다.
    /// </summary>
    private void OnPermissionsNext()
    {
        Debug.Log($"[LoginFlow] 권한 설정 완료: " +
            $"갤러리={permissions["gallery"]}, " +
            $"위치={permissions["location"]}, " +
            $"Health={permissions["health"]}, " +
            $"사용시간={permissions["usage"]}, " +
            $"캘린더={permissions["calendar"]}, " +
            $"Spotify={permissions["spotify"]}");

        // 권한 설정 저장
        foreach (var kv in permissions)
        {
            PlayerPrefs.SetInt($"perm_{kv.Key}", kv.Value ? 1 : 0);
        }
        PlayerPrefs.Save();

        GoToStep(2);
    }

    /// <summary>
    /// 토글이 켜질 때 해당 항목의 실제 권한 다이얼로그/설정 화면을 바로 띈다.
    /// - 갤러리: READ_MEDIA_IMAGES / READ_EXTERNAL_STORAGE
    /// - 위치: FineLocation
    /// - Health: ACTIVITY_RECOGNITION(걸음수 센서 fallback) + Health Connect 권한 화면
    /// - 사용시간: PACKAGE_USAGE_STATS (런타임 권한이 아니라 시스템 설정에서 직접 허용)
    /// - 캘린더: READ_CALENDAR
    /// - Spotify: OAuth 브라우저 인증 (권한 동의 화면)
    /// </summary>
    private IEnumerator RequestPermissionFor(string permKey)
    {
        switch (permKey)
        {
            case "gallery":
                yield return RequestRuntimePermission("android.permission.READ_MEDIA_IMAGES", Permission.ExternalStorageRead);
                break;

            case "location":
                yield return RequestRuntimePermission(Permission.FineLocation);
                break;

            case "health":
                yield return RequestRuntimePermission("android.permission.ACTIVITY_RECOGNITION");
                HealthConnectBridge.OpenHealthConnectSettings();
                break;

            case "usage":
                OpenAppUsageSettingsIfNeeded();
                yield break;

            case "calendar":
                yield return RequestRuntimePermission("android.permission.READ_CALENDAR");
                break;

            case "spotify":
                if (SpotifyCollector.Instance != null)
                    yield return SpotifyCollector.Instance.EnsureAuthInteractive();
                else
                    Debug.LogWarning("[LoginFlow] SpotifyCollector.Instance가 없음 (MainScene _AutoCollect 확인 필요)");
                break;
        }
    }

    /// <summary>
    /// candidates 중 하나라도 이미 허용돼 있으면 스킵, 아니면 첫 후보 권한을 요청하고
    /// 사용자 응답을 최대 8초 대기.
    /// </summary>
    private IEnumerator RequestRuntimePermission(params string[] candidates)
    {
#if UNITY_ANDROID
        foreach (var p in candidates)
        {
            if (Permission.HasUserAuthorizedPermission(p))
                yield break;
        }

        Debug.Log($"[LoginFlow] 권한 요청: {candidates[0]}");
        Permission.RequestUserPermission(candidates[0]);

        float waited = 0f;
        while (waited < 8f)
        {
            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
            foreach (var p in candidates)
            {
                if (Permission.HasUserAuthorizedPermission(p))
                    yield break;
            }
        }
#else
        yield break;
#endif
    }

    /// <summary>
    /// PACKAGE_USAGE_STATS 권한 확인 후, 없으면 사용 기록 액세스 설정 화면을 연다.
    /// </summary>
    private void OpenAppUsageSettingsIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var cls = new AndroidJavaClass("com.ontology.metaverse.appusage.AppUsageHelper");
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        if (!cls.CallStatic<bool>("hasPermission", activity))
        {
            Debug.Log("[LoginFlow] 사용 기록 액세스 설정 화면 오픈");
            cls.CallStatic("openSettings", activity);
        }
#endif
    }

    /// <summary>
    /// PACKAGE_USAGE_STATS 권한이 현재 부여되어 있는지 확인.
    /// </summary>
    private bool IsUsageAccessGranted()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var cls = new AndroidJavaClass("com.ontology.metaverse.appusage.AppUsageHelper");
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        return cls.CallStatic<bool>("hasPermission", activity);
#else
        return true;
#endif
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
            onSuccess: () =>
            {
                ScreenManager.Instance.ClearHistory();
                ScreenManager.Instance.GoTo("myroom");
            },
            onFailure: err =>
            {
                Debug.LogError($"[LoginFlow] 프로필 저장 실패: {err}");
                // Firestore 실패해도 로컬 저장 완료됐으므로 진행
                ScreenManager.Instance.ClearHistory();
                ScreenManager.Instance.GoTo("myroom");
            }
        );
    }
}
