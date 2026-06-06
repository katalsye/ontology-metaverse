using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 4-4. DiaryHistoryScreen 컨트롤러
/// 과거 일기 월별 열람
/// </summary>
public class DiaryHistoryScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private ScrollView diaryList;
    private VisualElement emptyState;
    private Label currentMonthLabel;

    private int viewYear;
    private int viewMonth;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();
        root.Q<Button>("btn-prev-month").clicked += () => ChangeMonth(-1);
        root.Q<Button>("btn-next-month").clicked += () => ChangeMonth(1);

        diaryList = root.Q<ScrollView>("diary-list");
        emptyState = root.Q("empty-state");
        currentMonthLabel = root.Q<Label>("current-month");

        viewYear = DateTime.Now.Year;
        viewMonth = DateTime.Now.Month;

        LoadMonth();
    }

    private void ChangeMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth > 12) { viewMonth = 1; viewYear++; }
        if (viewMonth < 1) { viewMonth = 12; viewYear--; }
        LoadMonth();
    }

    private void LoadMonth()
    {
        currentMonthLabel.text = $"{viewYear}년 {viewMonth}월";
        diaryList.contentContainer.Clear();

        string yearMonth = $"{viewYear}-{viewMonth:D2}";

        DiaryManager.Instance.GetMonthlyDiaries(yearMonth,
            onSuccess: entries =>
            {
                if (entries == null || entries.Count == 0)
                {
                    emptyState.AddToClassList("empty-state--visible");
                    diaryList.style.display = DisplayStyle.None;
                    return;
                }

                // 날짜 내림차순 정렬
                entries.Sort((a, b) => string.Compare(b.Date, a.Date, StringComparison.Ordinal));

                emptyState.RemoveFromClassList("empty-state--visible");
                diaryList.style.display = DisplayStyle.Flex;

                foreach (var entry in entries)
                {
                    diaryList.contentContainer.Add(CreateEntryCard(entry));
                }
            },
            onFailure: err =>
            {
                Debug.LogWarning($"[DiaryHistory] 일기 목록 로드 실패: {err}");
                emptyState.AddToClassList("empty-state--visible");
                diaryList.style.display = DisplayStyle.None;
            }
        );
    }

    private VisualElement CreateEntryCard(DiaryEntry entry)
    {
        var card = new VisualElement();
        card.AddToClassList("diary-entry");

        string dateDisplay = entry.Date;
        if (DateTime.TryParseExact(entry.Date, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            dateDisplay = dt.ToString("M월 d일 dddd", new CultureInfo("ko-KR"));
        }

        var dateLabel = new Label(dateDisplay);
        dateLabel.AddToClassList("diary-entry-date");

        var textLabel = new Label(entry.Content);
        textLabel.AddToClassList("diary-entry-text");

        card.Add(dateLabel);
        card.Add(textLabel);

        return card;
    }
}
