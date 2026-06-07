using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

/// <summary>
/// 4-6. CalendarScreen 컨트롤러
/// 월간 캘린더 그리드 + 날짜 선택 → 과거 방 3D
/// </summary>
public class CalendarScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private VisualElement calendarGrid;
    private VisualElement selectedInfo;
    private Label monthTitle;
    private Label selectedDateLabel;
    private Label selectedSummary;
    private Button btnViewRoom;

    private int viewYear;
    private int viewMonth;
    private int selectedDay = -1;

    // 해당 월에 일기가 있는 날 집합 (비동기 로드 후 채워짐)
    private HashSet<int> daysWithData = new HashSet<int>();

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();
        root.Q<Button>("btn-prev").clicked += () => ChangeMonth(-1);
        root.Q<Button>("btn-next").clicked += () => ChangeMonth(1);

        calendarGrid = root.Q("calendar-grid");
        selectedInfo = root.Q("selected-info");
        monthTitle = root.Q<Label>("month-title");
        selectedDateLabel = root.Q<Label>("selected-date-label");
        selectedSummary = root.Q<Label>("selected-summary");
        btnViewRoom = root.Q<Button>("btn-view-room");

        btnViewRoom.clicked += OnViewRoomClicked;

        viewYear = DateTime.Now.Year;
        viewMonth = DateTime.Now.Month;

        LoadMonthData();
    }

    private void ChangeMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth > 12) { viewMonth = 1; viewYear++; }
        if (viewMonth < 1) { viewMonth = 12; viewYear--; }
        selectedDay = -1;
        selectedInfo.RemoveFromClassList("selected-info--visible");
        LoadMonthData();
    }

    // Firestore에서 해당 월 일기 목록 로드 → 날짜 집합 채운 뒤 캘린더 렌더링
    private void LoadMonthData()
    {
        monthTitle.text = $"{viewYear}년 {viewMonth}월";
        daysWithData.Clear();

        string yearMonth = $"{viewYear}-{viewMonth:D2}";

        DiaryManager.Instance.GetMonthlyDiaries(yearMonth,
            onSuccess: entries =>
            {
                foreach (var entry in entries)
                {
                    // Date 형식: "yyyy-MM-dd"
                    if (entry.Date != null && entry.Date.Length == 10 &&
                        int.TryParse(entry.Date.Substring(8, 2), out int day))
                    {
                        daysWithData.Add(day);
                    }
                }
                RenderCalendar();
            },
            onFailure: err =>
            {
                Debug.LogWarning($"[Calendar] 월간 데이터 로드 실패: {err}");
                RenderCalendar(); // 점 없이라도 캘린더는 표시
            }
        );
    }

    private void RenderCalendar()
    {
        calendarGrid.Clear();

        DateTime firstDay = new DateTime(viewYear, viewMonth, 1);
        int startDow = (int)firstDay.DayOfWeek; // 0=Sun
        int daysInMonth = DateTime.DaysInMonth(viewYear, viewMonth);
        int today = DateTime.Now.Day;
        bool isCurrentMonth = (viewYear == DateTime.Now.Year && viewMonth == DateTime.Now.Month);

        // 빈 셀 (이전 달)
        for (int i = 0; i < startDow; i++)
        {
            calendarGrid.Add(CreateCell(0, false, false, i));
        }

        // 날짜 셀
        for (int d = 1; d <= daysInMonth; d++)
        {
            int dow = (startDow + d - 1) % 7;
            bool isToday = isCurrentMonth && d == today;
            bool hasData = daysWithData.Contains(d);
            calendarGrid.Add(CreateCell(d, isToday, hasData, dow));
        }

        // 남은 빈 셀
        int totalCells = startDow + daysInMonth;
        int remaining = (7 - totalCells % 7) % 7;
        for (int i = 0; i < remaining; i++)
        {
            calendarGrid.Add(CreateCell(0, false, false, (totalCells + i) % 7));
        }
    }

    private VisualElement CreateCell(int day, bool isToday, bool hasData, int dow)
    {
        var cell = new VisualElement();
        cell.AddToClassList("cal-cell");

        if (day == 0)
        {
            cell.AddToClassList("cal-cell--empty");
            var inner = new VisualElement();
            inner.AddToClassList("cal-cell-inner");
            inner.Add(new Label("") { });
            cell.Add(inner);
            return cell;
        }

        if (dow == 0) cell.AddToClassList("cal-cell--sun");
        if (dow == 6) cell.AddToClassList("cal-cell--sat");
        if (isToday) cell.AddToClassList("cal-cell--today");

        var cellInner = new VisualElement();
        cellInner.AddToClassList("cal-cell-inner");

        var dayLabel = new Label(day.ToString());
        dayLabel.AddToClassList("cal-day");
        cellInner.Add(dayLabel);
        cell.Add(cellInner);

        if (hasData)
        {
            var dot = new VisualElement();
            dot.AddToClassList("cal-dot");
            cell.Add(dot);
        }

        int capturedDay = day;
        cell.RegisterCallback<ClickEvent>(evt => OnDayClicked(capturedDay));

        return cell;
    }

    private void OnDayClicked(int day)
    {
        calendarGrid.Query(className: "cal-cell--selected").ForEach(el =>
            el.RemoveFromClassList("cal-cell--selected"));

        selectedDay = day;

        int index = (int)new DateTime(viewYear, viewMonth, 1).DayOfWeek + day - 1;
        if (index < calendarGrid.childCount)
            calendarGrid[index].AddToClassList("cal-cell--selected");

        var dt = new DateTime(viewYear, viewMonth, day);
        selectedDateLabel.text = dt.ToString("M월 d일 dddd");

        bool hasData = daysWithData.Contains(day);
        selectedSummary.text = hasData ? "이 날의 방 기록이 있어요" : "이 날의 기록이 없어요";
        btnViewRoom.SetEnabled(hasData);

        selectedInfo.AddToClassList("selected-info--visible");
    }

    private void OnViewRoomClicked()
    {
        if (selectedDay < 1) return;
        string dateKey = $"{viewYear}{viewMonth:D2}{selectedDay:D2}";
        PlayerPrefs.SetString("viewing_past_date", dateKey);
        ScreenManager.Instance.GoTo("past_room");
    }
}
