using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 4-8. SleepScreen 컨트롤러
/// Health Connect 수면 데이터 표시
/// </summary>
public class SleepScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();

        LoadSleepData();
        RenderWeeklyBars();
    }

    private void LoadSleepData()
    {
        // TODO: Health Connect에서 수면 데이터 가져오기
        // HealthConnectManager.Instance.GetSleepData(today)

        // 테스트 데이터는 UXML에 이미 기본값으로 설정됨
        // 실제 구현 시 아래처럼 업데이트:
        // root.Q<Label>("sleep-hours").text = "7시간 32분";
        // root.Q<Label>("sleep-range").text = "23:20 취침 → 06:52 기상";

        // 수면 질 바 업데이트
        float quality = 0.82f;
        var fill = root.Q("quality-bar-fill");
        fill.style.width = Length.Percent(quality * 100f);
        root.Q<Label>("quality-percent").text = $"{(quality * 100):F0}%";

        string qualityText = quality >= 0.8f ? "양호" :
                             quality >= 0.6f ? "보통" : "부족";
        root.Q<Label>("quality-label").text = qualityText;
    }

    private void RenderWeeklyBars()
    {
        var barsContainer = root.Q("weekly-bars");
        barsContainer.Clear();

        // TODO: 최근 7일 수면 시간 데이터
        float[] hours = { 6.5f, 7.2f, 5.8f, 8.0f, 7.0f, 6.2f, 7.5f };
        float maxHours = 10f;

        for (int i = 0; i < 7; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("weekly-bar");

            float heightPercent = (hours[i] / maxHours) * 100f;
            bar.style.height = Length.Percent(heightPercent);

            // 오늘(마지막 바) 강조
            if (i == 6)
                bar.AddToClassList("weekly-bar--today");

            barsContainer.Add(bar);
        }
    }
}
