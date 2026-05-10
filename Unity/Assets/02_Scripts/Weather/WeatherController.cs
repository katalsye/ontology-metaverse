using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DynamicWeatherSystem;

// 유니티 Inspector 설정:
// 1. 빈 GameObject 만들고 이름 WeatherController, 이 스크립트 붙이기
// 2. WeatherManager 슬롯에 씬의 WeatherManager 오브젝트 연결
// 3. DirectionalLight 슬롯에 씬의 Directional Light 연결
// 4. DWS 프리셋 연결: presetClear=WS_Clear, presetCloudy=WS_Fog, presetRainy=WS_Rain, presetSnowy=WS_Rain(임시)
// 5. 스카이박스 머티리얼 연결 (Cubemap .mat 파일):
//    맑음 낮   → Day_BlueSky_Nothing.mat
//    맑음 새벽 → Deep Dusk.mat
//    맑음 노을 → Epic_BlueSunset.mat
//    맑음 밤   → Night Moon Burst.mat
//    흐림/비 낮·새벽 → AllSky_Overcast4_Low.mat
//    흐림/비 노을    → Cold Sunset.mat
//    흐림/비 밤      → Cold Night.mat
//
// 코드에서 호출:
//    weatherController.SetWeather(WeatherType.Rainy, TimeOfDay.Night);  // 날씨+시간 동시
//    weatherController.SetWeather(WeatherType.Clear);                    // 날씨만 변경
//    weatherController.SetTimeOfDay(TimeOfDay.Sunset);                   // 시간만 변경

[DefaultExecutionOrder(100)] // WeatherManager(기본값 0)보다 늦게 Start() 실행
public class WeatherController : MonoBehaviour
{
    public enum WeatherType  { Clear, Cloudy, Rainy, Snowy }
    public enum TimeOfDay    { Day, Dawn, Sunset, Night }

    // ── DynamicWeatherSystem ───────────────────────────────────────────
    [Header("WeatherManager 연결")]
    public WeatherManager weatherManager;

    [Header("DWS 프리셋")]
    public WeatherStateData presetClear;   // WS_Clear
    public WeatherStateData presetCloudy;  // WS_Fog
    public WeatherStateData presetRainy;   // WS_Rain
    public WeatherStateData presetSnowy;   // WS_Rain (임시)

    // ── 스카이박스 머티리얼 ─────────────────────────────────────────────
    [Header("맑음 스카이박스")]
    public Material skyClear_Day;     // Cartoon Base BlueSky
    public Material skyClear_Dawn;    // Deep Dusk
    public Material skyClear_Sunset;  // Epic_BlueSunset
    public Material skyClear_Night;   // Night MoonBurst

    [Header("흐림 스카이박스")]
    public Material skyCloudy_Day;    // Overcast Low
    public Material skyCloudy_Dawn;   // Overcast Low (대체)
    public Material skyCloudy_Sunset; // Cold Sunset
    public Material skyCloudy_Night;  // Cold Night

    [Header("비 스카이박스")]
    public Material skyRainy_Day;     // Overcast Low
    public Material skyRainy_Dawn;    // Overcast Low (대체)
    public Material skyRainy_Sunset;  // Cold Sunset
    public Material skyRainy_Night;   // Cold Night

    [Header("눈 스카이박스 (임시: 비와 동일)")]
    public Material skySnowy_Day;
    public Material skySnowy_Dawn;
    public Material skySnowy_Sunset;
    public Material skySnowy_Night;

    // ── 라이팅 ─────────────────────────────────────────────────────────
    [Header("Directional Light 연결")]
    public Light directionalLight;

    [Header("Post Processing")]
    public Volume globalVolume; // Inspector에서 씬의 Directional Light 연결

    // 시간대별 라이트 설정 (날씨와 무관하게 시간대로 결정)
    // intensity: 낮=1.0 / 새벽=0.4 / 노을=0.7 / 밤=0.02
    // color: 낮=흰색 / 새벽=연주황 / 노을=주황 / 밤=남색
    // (intensity, color, ambient, shadowStrength)
    // shadowStrength: 낮=1.0 / 새벽=0.6 / 노을=0.7 / 밤=0.15
    static readonly (float intensity, Color color, Color ambient, float shadowStrength)[] _timeLighting =
    {
        // Day  ── 2번2번: intensity=1.0 / 대박대박: intensity=3.0 / 어머어머: intensity=4.5 / 우왕: tint=(0.35,0.48,0.75) / 3번3번: intensity=4.5, ambient=(0.42,0.55,0.95) ──
        (12.0f, new Color(0.50f, 0.72f, 1.00f), new Color(0.45f, 0.65f, 1.00f), 0.85f),
        // Dawn
        (0.4f,  new Color(1.00f, 0.78f, 0.55f), new Color(0.12f, 0.11f, 0.18f), 0.6f),
        // Sunset
        (1.8f,  new Color(1.00f, 0.55f, 0.20f), new Color(0.35f, 0.22f, 0.18f), 0.7f),
        // Night
        (0.25f, new Color(0.60f, 0.78f, 1.00f), new Color(0.18f, 0.24f, 0.38f), 0.15f),
    };

    // ── 현재 상태 ──────────────────────────────────────────────────────
    [Header("초기 날씨 설정")]
    public WeatherType  initialWeather = WeatherType.Clear;
    public TimeOfDay    initialTime    = TimeOfDay.Day;

    private WeatherType  _currentWeather;
    private TimeOfDay    _currentTime;

    // ── Public API ─────────────────────────────────────────────────────
    public WeatherType  CurrentWeather => _currentWeather;
    public TimeOfDay    CurrentTime    => _currentTime;

    // ──────────────────────────────────────────────────────────────────
    void Start()
    {
        SetWeather(initialWeather, initialTime);
    }

    /// <summary>날씨와 시간대를 동시에 변경</summary>
    public void SetWeather(WeatherType weather, TimeOfDay time)
    {
        _currentWeather = weather;
        _currentTime    = time;

        ApplySkybox(weather, time);
        ApplyDWS(weather);
        ApplyLighting(time);
        if (weather == WeatherType.Clear && time == TimeOfDay.Day)
            // ── 저장 2번2번: saturation=65, contrast=-45, intensity=1.0, lightColor=(0.75,0.88,1.0) ──
            // ── 저장 3번3번: saturation=70, contrast=-45, intensity=1.25, ambient=(0.22,0.28,0.42) ──
            SetSaturation(100f, -45f);
        else if (weather == WeatherType.Clear && time == TimeOfDay.Sunset)
            SetSaturation(0f, -35f);
        else
            SetSaturation(0f);
        ApplyFogOverride(weather, time);
    }

    /// <summary>날씨만 변경 (시간대 유지)</summary>
    public void SetWeather(WeatherType weather) => SetWeather(weather, _currentTime);

    /// <summary>시간대만 변경 (날씨 유지)</summary>
    public void SetTimeOfDay(TimeOfDay time) => SetWeather(_currentWeather, time);

    // ──────────────────────────────────────────────────────────────────

    void ApplyLighting(TimeOfDay time)
    {
        var (intensity, color, ambient, shadowStrength) = _timeLighting[(int)time];

        if (directionalLight != null)
        {
            directionalLight.intensity      = intensity;
            directionalLight.color          = color;
            directionalLight.shadowStrength = shadowStrength;
        }

        RenderSettings.ambientLight = ambient;
    }

    void SetSaturation(float saturation, float contrast = 0f)
    {
        if (globalVolume == null) return;
        if (globalVolume.profile.TryGet<ColorAdjustments>(out var ca))
        {
            ca.saturation.Override(saturation);
            ca.contrast.Override(contrast);
        }
    }

    void ApplyFogOverride(WeatherType weather, TimeOfDay time)
    {
        // Rainy Day: 뿌옇고 침침하게 + 채도 낮춤
        if (weather == WeatherType.Rainy && time == TimeOfDay.Day)
        {
            RenderSettings.fogDensity = 0.013f;
            if (directionalLight != null)
            {
                directionalLight.intensity = 0.55f;
                directionalLight.color     = new Color(0.72f, 0.78f, 0.88f);
            }
            RenderSettings.ambientLight = new Color(0.13f, 0.14f, 0.16f);
            SetSaturation(-40f);
        }
        // Cloudy Day만 안개 밀도와 밝기를 별도 조정
        else if (weather == WeatherType.Cloudy && time == TimeOfDay.Day)
        {
            RenderSettings.fogDensity = 0.008f;
            if (directionalLight != null)
                directionalLight.intensity = 1.2f;
        }
        else if (weather == WeatherType.Cloudy && time == TimeOfDay.Dawn)
        {
            RenderSettings.fogDensity = 0.008f;
        }
        else if (weather == WeatherType.Rainy && time == TimeOfDay.Night)
        {
            RenderSettings.fogDensity = 0.008f;
            if (directionalLight != null)
            {
                directionalLight.intensity = 0.015f;
                directionalLight.color     = new Color(0.25f, 0.30f, 0.45f);
            }
            RenderSettings.ambientLight = new Color(0.03f, 0.04f, 0.08f);
            SetSaturation(-22f);
        }
        else if (weather == WeatherType.Rainy && time == TimeOfDay.Dawn)
        {
            if (directionalLight != null)
                directionalLight.color = new Color(0.88f, 0.76f, 0.68f); // 흐린 날 새벽, 살짝 붉은기
        }
        else if (weather == WeatherType.Cloudy && time == TimeOfDay.Sunset)
        {
            RenderSettings.fogDensity = 0.008f;
        }
        else if (weather == WeatherType.Cloudy && time == TimeOfDay.Night)
        {
            RenderSettings.fogDensity = 0.012f;
            if (directionalLight != null)
            {
                directionalLight.intensity = 0.008f;
                directionalLight.color     = new Color(0.30f, 0.38f, 0.50f);
            }
            RenderSettings.ambientLight = new Color(0.04f, 0.07f, 0.16f);
            // 스카이박스 없애고 카메라 배경 검정으로
            RenderSettings.skybox = null;
            if (Camera.main != null)
            {
                Camera.main.clearFlags       = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor  = Color.black;
            }
            DynamicGI.UpdateEnvironment();
        }
    }

    void ApplySkybox(WeatherType weather, TimeOfDay time)
    {
        Material sky = GetSkybox(weather, time);
        if (sky == null)
        {
            Debug.LogWarning($"[WeatherController] 스카이박스 없음: {weather} / {time}");
            return;
        }
        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment(); // 조명 반영
    }

    void ApplyDWS(WeatherType weather)
    {
        if (weatherManager == null) return;

        WeatherStateData preset = weather switch
        {
            WeatherType.Clear  => presetClear,
            WeatherType.Cloudy => presetCloudy,
            WeatherType.Rainy  => presetRainy,
            WeatherType.Snowy  => presetSnowy,
            _                  => presetClear
        };

        if (preset == null)
        {
            Debug.LogWarning($"[WeatherController] DWS 프리셋 없음: {weather}");
            return;
        }

        weatherManager.SetWeather(preset, 0f);
    }

    Material GetSkybox(WeatherType weather, TimeOfDay time)
    {
        return weather switch
        {
            WeatherType.Clear  => time switch
            {
                TimeOfDay.Day    => skyClear_Day,
                TimeOfDay.Dawn   => skyClear_Dawn,
                TimeOfDay.Sunset => skyClear_Sunset,
                TimeOfDay.Night  => skyClear_Night,
                _                => skyClear_Day
            },
            WeatherType.Cloudy => time switch
            {
                TimeOfDay.Day    => skyCloudy_Day,
                TimeOfDay.Dawn   => skyCloudy_Dawn,
                TimeOfDay.Sunset => skyCloudy_Sunset,
                TimeOfDay.Night  => skyCloudy_Night,
                _                => skyCloudy_Day
            },
            WeatherType.Rainy  => time switch
            {
                TimeOfDay.Day    => skyRainy_Day,
                TimeOfDay.Dawn   => skyRainy_Dawn,
                TimeOfDay.Sunset => skyRainy_Sunset,
                TimeOfDay.Night  => skyRainy_Night,
                _                => skyRainy_Day
            },
            WeatherType.Snowy  => time switch
            {
                TimeOfDay.Day    => skySnowy_Day    != null ? skySnowy_Day    : skyRainy_Day,
                TimeOfDay.Dawn   => skySnowy_Dawn   != null ? skySnowy_Dawn   : skyRainy_Dawn,
                TimeOfDay.Sunset => skySnowy_Sunset != null ? skySnowy_Sunset : skyRainy_Sunset,
                TimeOfDay.Night  => skySnowy_Night  != null ? skySnowy_Night  : skyRainy_Night,
                _                => skyRainy_Day
            },
            _ => skyClear_Day
        };
    }
}
