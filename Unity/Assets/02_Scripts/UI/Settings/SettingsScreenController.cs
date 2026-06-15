using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// 6-1/6-2. SettingsScreen 컨트롤러
/// 토글, 슬라이더, 세그먼트, 프로필 편집, 로그아웃, 계정 삭제
/// </summary>
public class SettingsScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private VisualElement profileEditOverlay;
    private VisualElement deleteConfirmOverlay;

    private TextField editNickname, editStatus;

    // 페르소나 표시 레이블
    private VisualElement _personaSection;
    private Label _labelEnergyType;
    private Label _labelSocialPref;
    private Label _labelLifePattern;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();

        // 페르소나 섹션 바인딩
        _personaSection  = root.Q("persona-section");
        _labelEnergyType = root.Q<Label>("label-energy-type");
        _labelSocialPref = root.Q<Label>("label-social-pref");
        _labelLifePattern = root.Q<Label>("label-life-pattern");

        if (_personaSection != null)
            _personaSection.style.display = DisplayStyle.None;

        // 페르소나 이벤트 구독 + 초기 로드
        if (UserManager.Instance != null)
        {
            UserManager.Instance.OnPersonaRestored += RefreshPersonaUI;
            UserManager.Instance.GetPersona(RefreshPersonaUI);
        }

        // 프로필 편집
        profileEditOverlay = root.Q("profile-edit-overlay");
        deleteConfirmOverlay = root.Q("delete-confirm-overlay");

        root.Q("btn-edit-profile").RegisterCallback<ClickEvent>(evt => OpenProfileEdit());
        root.Q<Button>("btn-close-edit").clicked += CloseProfileEdit;
        root.Q<Button>("btn-save-profile").clicked += OnSaveProfile;

        editNickname = root.Q<TextField>("edit-nickname");
        editStatus = root.Q<TextField>("edit-status");

        // 닉네임 표시
        root.Q<Label>("settings-nickname").text = PlayerPrefs.GetString("nickname", "닉네임");

        // 토글 설정
        SetupToggle("toggle-push", "perm_push");
        SetupToggle("toggle-gallery", "perm_gallery");
        SetupToggle("toggle-location", "perm_location");
        SetupToggle("toggle-health", "perm_health");
        SetupToggle("toggle-usage", "perm_usage");
        SetupToggle("toggle-calendar", "perm_calendar");
        SetupToggle("toggle-spotify", "perm_spotify");

        // 슬라이더
        var bgmSlider = root.Q<Slider>("slider-bgm");
        bgmSlider.value = PlayerPrefs.GetFloat("bgm_volume", 70f);
        bgmSlider.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetFloat("bgm_volume", evt.newValue);
            AudioManager.Instance.SetBGMVolume(evt.newValue / 100f);
            AudioManager.Instance.SaveVolumesToFirestore();
        });

        var sfxSlider = root.Q<Slider>("slider-sfx");
        sfxSlider.value = PlayerPrefs.GetFloat("sfx_volume", 80f);
        sfxSlider.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetFloat("sfx_volume", evt.newValue);
            AudioManager.Instance.SetSFXVolume(evt.newValue / 100f);
            AudioManager.Instance.SaveVolumesToFirestore();
        });

        // 세그먼트: 해상도
        SetupSegmented("res-low", "res-mid", "res-high", "resolution",
            new[] { "low", "mid", "high" }, "mid");

        // 세그먼트: 프레임
        SetupSegmented("fps-30", "fps-60", null, "fps",
            new[] { "30", "60" }, "30");

        // 계정
        root.Q<Button>("btn-logout").clicked += OnLogout;
        root.Q<Button>("btn-delete").clicked += () =>
            deleteConfirmOverlay.style.display = DisplayStyle.Flex;
        root.Q<Button>("btn-confirm-delete").clicked += OnDeleteAccount;
        root.Q<Button>("btn-cancel-delete").clicked += () =>
            deleteConfirmOverlay.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        if (UserManager.Instance != null)
            UserManager.Instance.OnPersonaRestored -= RefreshPersonaUI;
    }

    private void RefreshPersonaUI(Persona p)
    {
        if (p == null) return;

        if (_labelEnergyType  != null) _labelEnergyType.text  = p.EnergyType      ?? "-";
        if (_labelSocialPref  != null) _labelSocialPref.text  = p.SocialPreference ?? "-";
        if (_labelLifePattern != null) _labelLifePattern.text = p.LifePattern      ?? "-";

        if (_personaSection != null)
            _personaSection.style.display = DisplayStyle.Flex;
    }

    private void SetupToggle(string toggleName, string prefKey)
    {
        var toggle = root.Q(toggleName);
        if (toggle == null) return;

        bool isOn = PlayerPrefs.GetInt(prefKey, 0) == 1;

        var thumb = new VisualElement();
        thumb.style.width = 22;
        thumb.style.height = 22;
        thumb.style.borderTopLeftRadius = 11;
        thumb.style.borderTopRightRadius = 11;
        thumb.style.borderBottomLeftRadius = 11;
        thumb.style.borderBottomRightRadius = 11;
        thumb.style.backgroundColor = new StyleColor(Color.white);
        thumb.style.position = Position.Absolute;
        thumb.style.top = 3;
        thumb.style.left = isOn ? 23 : 3;
        thumb.style.transitionProperty = new List<StylePropertyName> { new("left") };
        thumb.style.transitionDuration = new List<TimeValue> { new(200, TimeUnit.Millisecond) };
        toggle.Add(thumb);

        if (isOn) toggle.AddToClassList("toggle--on");

        toggle.RegisterCallback<ClickEvent>(evt =>
        {
            isOn = !isOn;
            PlayerPrefs.SetInt(prefKey, isOn ? 1 : 0);
            thumb.style.left = isOn ? 23 : 3;
            if (isOn) toggle.AddToClassList("toggle--on");
            else toggle.RemoveFromClassList("toggle--on");
        });
    }

    private void SetupSegmented(string btn1Name, string btn2Name, string btn3Name,
                                 string prefKey, string[] values, string defaultVal)
    {
        string current = PlayerPrefs.GetString(prefKey, defaultVal);
        var buttons = new List<Button>();

        buttons.Add(root.Q<Button>(btn1Name));
        buttons.Add(root.Q<Button>(btn2Name));
        if (btn3Name != null) buttons.Add(root.Q<Button>(btn3Name));

        for (int i = 0; i < buttons.Count; i++)
        {
            if (values[i] == current)
                buttons[i].AddToClassList("seg-btn--active");
            else
                buttons[i].RemoveFromClassList("seg-btn--active");
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i;
            buttons[i].clicked += () =>
            {
                foreach (var b in buttons) b.RemoveFromClassList("seg-btn--active");
                buttons[index].AddToClassList("seg-btn--active");
                PlayerPrefs.SetString(prefKey, values[index]);
                Debug.Log($"[Settings] {prefKey} = {values[index]}");
            };
        }
    }

    private void OpenProfileEdit()
    {
        editNickname.value = PlayerPrefs.GetString("nickname", "");
        editStatus.value = PlayerPrefs.GetString("status_message", "");
        profileEditOverlay.style.display = DisplayStyle.Flex;
    }

    private void CloseProfileEdit()
    {
        profileEditOverlay.style.display = DisplayStyle.None;
    }

    private void OnSaveProfile()
    {
        string nickname = editNickname.value.Trim();
        string status = editStatus.value.Trim();

        if (nickname.Length < 2) return;

        PlayerPrefs.SetString("nickname", nickname);
        PlayerPrefs.SetString("status_message", status);
        PlayerPrefs.Save();

        root.Q<Label>("settings-nickname").text = nickname;
        CloseProfileEdit();

        UserManager.Instance.UpdateProfile(nickname, status,
            onFailure: err => Debug.LogError($"[Settings] 프로필 저장 실패: {err}")
        );
    }

    private void OnLogout()
    {
        AuthService.Instance.SignOut(() =>
        {
            ScreenManager.Instance.ClearHistory();
            ScreenManager.Instance.GoTo("login");
        });
    }

    private void OnDeleteAccount()
    {
        AuthService.Instance.DeleteAccount(
            onSuccess: () =>
            {
                PlayerPrefs.DeleteAll();
                ScreenManager.Instance.ClearHistory();
                ScreenManager.Instance.GoTo("onboarding");
            },
            onFailure: err =>
            {
                Debug.LogError($"[Settings] 계정 삭제 실패: {err}");
                deleteConfirmOverlay.style.display = DisplayStyle.None;
            }
        );
    }
}