using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

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

        // 기존 카드 제거
        diaryList.contentContainer.Clear();

        // TODO: Firestore에서 해당 월 일기 목록 가져오기

        // 로컬 데이터에서 로드 (테스트)
        var entries = new List<(string date, string text)>();
        int daysInMonth = DateTime.DaysInMonth(viewYear, viewMonth);

        for (int d = daysInMonth; d >= 1; d--)
        {
            string key = $"diary_{viewYear}{viewMonth:D2}{d:D2}";
            string text = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(text))
            {
                var dt = new DateTime(viewYear, viewMonth, d);
                entries.Add((dt.ToString("M월 d일 dddd"), text));
            }
        }

        if (entries.Count == 0)
        {
            emptyState.AddToClassList("empty-state--visible");
            diaryList.style.display = DisplayStyle.None;
            return;
        }

        emptyState.RemoveFromClassList("empty-state--visible");
        diaryList.style.display = DisplayStyle.Flex;

        foreach (var entry in entries)
        {
            var card = new VisualElement();
            card.AddToClassList("diary-entry");

            var dateLabel = new Label(entry.date);
            dateLabel.AddToClassList("diary-entry-date");

            var textLabel = new Label(entry.text);
            textLabel.AddToClassList("diary-entry-text");

            card.Add(dateLabel);
            card.Add(textLabel);
            diaryList.contentContainer.Add(card);
        }
    }
}
