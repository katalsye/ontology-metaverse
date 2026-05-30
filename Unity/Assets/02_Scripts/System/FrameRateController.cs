using UnityEngine;

// 유니티 Inspector 설정:
// 빈 GameObject에 붙이기 (예: GameManager 또는 SystemManager)
// 씬 시작 시 initialQuality 기준으로 자동 적용
//
// 코드에서 호출:
//    frameRateController.SetQuality(FrameRateController.QualityLevel.High);
//    frameRateController.SetQuality(FrameRateController.QualityLevel.Medium);
//    frameRateController.SetQuality(FrameRateController.QualityLevel.Low);

public class FrameRateController : MonoBehaviour
{
    public enum QualityLevel { Low, Medium, High }

    [Header("초기 품질 설정")]
    public QualityLevel initialQuality = QualityLevel.Medium;

    // ── 프레임 설정 ─────────────────────────────────────────────────────
    // Low    : 30fps, 해상도 0.5x, 그림자 끔
    // Medium : 60fps, 해상도 0.75x, 그림자 Low
    // High   : 120fps (또는 무제한), 해상도 1.0x, 그림자 Medium
    static readonly (int targetFPS, float resScale, ShadowResolution shadowRes, ShadowQuality shadowQ)[] _qualitySettings =
    {
        // Low
        (30,  0.50f, ShadowResolution.Low,    ShadowQuality.Disable),
        // Medium
        (60,  0.75f, ShadowResolution.Medium, ShadowQuality.HardOnly),
        // High
        (120, 1.00f, ShadowResolution.High,   ShadowQuality.All),
    };

    private QualityLevel _current;
    public  QualityLevel Current => _current;

    void Start()
    {
        // VSync 끔 (targetFrameRate 적용을 위해 필수)
        QualitySettings.vSyncCount = 0;
        SetQuality(initialQuality);
    }

    public void SetQuality(QualityLevel level)
    {
        _current = level;
        var (fps, scale, shadowRes, shadowQ) = _qualitySettings[(int)level];

        Application.targetFrameRate           = fps;
        UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale = scale;
        QualitySettings.shadowResolution      = shadowRes;
        QualitySettings.shadows               = shadowQ;

    }

    // 순환 토글 (버튼 하나로 Low→Medium→High→Low)
    public void CycleQuality()
    {
        int next = ((int)_current + 1) % 3;
        SetQuality((QualityLevel)next);
    }
}
