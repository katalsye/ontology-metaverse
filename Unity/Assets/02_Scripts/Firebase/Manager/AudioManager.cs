using UnityEngine;

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

        // PlayerPrefs에서 저장된 볼륨 불러오기
        SetBGMVolume(PlayerPrefs.GetFloat("bgm_volume", 70f) / 100f);
        SetSFXVolume(PlayerPrefs.GetFloat("sfx_volume", 80f) / 100f);
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