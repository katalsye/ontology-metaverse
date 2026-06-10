using UnityEngine;
using Firebase.Extensions;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip[] bgmClips;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip[] sfxClips;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // PlayerPrefs를 즉시 반영 (오프라인 폴백)
        SetBGMVolume(PlayerPrefs.GetFloat("bgm_volume", 70f) / 100f);
        SetSFXVolume(PlayerPrefs.GetFloat("sfx_volume", 80f) / 100f);

        // Firestore 값으로 덮어쓰기 — CheckDependencies 완료 전 DefaultInstance 접근 시
        // "Don't call Firebase functions before CheckDependencies has finished" 예외 발생하므로
        // FirebaseBootstrap을 통해 초기화 완료 후 호출
        FirebaseBootstrap.RunWhenReady(LoadVolumesFromFirestore);
    }

    public void SetBGMVolume(float volume)
    {
        // SettingsScreen에서 0~100으로 넘어오면 /100f 해서 사용
        bgmSource.volume = Mathf.Clamp01(volume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>Firestore에서 볼륨 값을 불러와 적용. Awake 및 로그인 시 호출.</summary>
    public void LoadVolumesFromFirestore()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(auth.CurrentUser.UserId)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) return;
                var doc = task.Result;
                if (doc.ContainsField("bgmVolume"))
                {
                    float bgm = (float)System.Convert.ToDouble(doc.GetValue<object>("bgmVolume"));
                    PlayerPrefs.SetFloat("bgm_volume", bgm * 100f);
                    SetBGMVolume(bgm);
                }
                if (doc.ContainsField("sfxVolume"))
                {
                    float sfx = (float)System.Convert.ToDouble(doc.GetValue<object>("sfxVolume"));
                    PlayerPrefs.SetFloat("sfx_volume", sfx * 100f);
                    SetSFXVolume(sfx);
                }
            });
    }

    /// <summary>현재 볼륨 값을 Firestore에 저장. SettingsScreenController 슬라이더 변경 시 호출.</summary>
    public void SaveVolumesToFirestore()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;

        var data = new System.Collections.Generic.Dictionary<string, object>
        {
            { "bgmVolume", (double)bgmSource.volume },
            { "sfxVolume", (double)sfxSource.volume },
        };
        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(auth.CurrentUser.UserId)
            .UpdateAsync(data)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogWarning("[AudioManager] 볼륨 Firestore 저장 실패: " + t.Exception);
            });
    }

    public void PlayBGM(int index)
    {
        if (index < 0 || index >= bgmClips.Length) return;
        if (bgmSource.clip == bgmClips[index] && bgmSource.isPlaying) return;

        bgmSource.clip = bgmClips[index];
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PlaySFX(int index)
    {
        if (index < 0 || index >= sfxClips.Length) return;
        sfxSource.PlayOneShot(sfxClips[index]);
    }
}