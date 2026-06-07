using UnityEngine;
using UnityEngine.UIElements;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// 4-8. SleepScreen 컨트롤러
/// SQLite triples 테이블에서 수면 트리플(predicate=slept)을 읽어 표시.
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
        try
        {
            var db    = SQLiteManager.Instance.Connection;
            string today = System.DateTime.Now.ToString("yyyy-MM-dd");

            var rows = db.Query<Triple>(
                "SELECT * FROM triples WHERE predicate='slept' AND source='health' " +
                "AND timestamp LIKE ? ORDER BY timestamp DESC LIMIT 1",
                today + "%");

            if (rows.Count > 0 && float.TryParse(rows[0].Object, out float duration))
            {
                int h = (int)duration;
                int m = (int)((duration - h) * 60);
                root.Q<Label>("sleep-hours").text = $"{h}시간 {m}분";
            }

            // 수면 질 트리플 (predicate=sleepQuality, object=0~100 int)
            var qualityRows = db.Query<Triple>(
                "SELECT * FROM triples WHERE predicate='sleepQuality' AND source='health' " +
                "AND timestamp LIKE ? ORDER BY timestamp DESC LIMIT 1",
                today + "%");

            float quality = 0.82f;
            if (qualityRows.Count > 0 && float.TryParse(qualityRows[0].Object, out float q))
                quality = Mathf.Clamp01(q / 100f);

            var fill = root.Q("quality-bar-fill");
            fill.style.width = Length.Percent(quality * 100f);
            root.Q<Label>("quality-percent").text = $"{(quality * 100):F0}%";
            root.Q<Label>("quality-label").text = quality >= 0.8f ? "양호"
                                                : quality >= 0.6f ? "보통" : "부족";
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SleepScreenController] 수면 데이터 로드 실패: " + e.Message);
        }
    }

    private void RenderWeeklyBars()
    {
        var barsContainer = root.Q("weekly-bars");
        barsContainer.Clear();

        float[] hours = new float[7];
        try
        {
            var db = SQLiteManager.Instance.Connection;
            for (int i = 6; i >= 0; i--)
            {
                string dateStr = System.DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                var rows = db.Query<Triple>(
                    "SELECT * FROM triples WHERE predicate='slept' AND source='health' " +
                    "AND timestamp LIKE ? ORDER BY timestamp DESC LIMIT 1",
                    dateStr + "%");

                if (rows.Count > 0 && float.TryParse(rows[0].Object, out float h))
                    hours[6 - i] = h;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SleepScreenController] 주간 수면 로드 실패: " + e.Message);
            hours = new float[] { 6.5f, 7.2f, 5.8f, 8.0f, 7.0f, 6.2f, 7.5f };
        }

        float maxHours = 10f;
        for (int i = 0; i < 7; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("weekly-bar");
            bar.style.height = Length.Percent(hours[i] / maxHours * 100f);
            if (i == 6) bar.AddToClassList("weekly-bar--today");
            barsContainer.Add(bar);
        }
    }
}
