using UnityEngine;
using UnityEngine.UIElements;
using System;

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

        RenderCalendar();
    }

    private void ChangeMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth > 12) { viewMonth = 1; viewYear++; }
        if (viewMonth < 1) { viewMonth = 12; viewYear--; }
        selectedDay = -1;
        selectedInfo.RemoveFromClassList("selected-info--visible");
        RenderCalendar();
    }

    private void RenderCalendar()
    {
        monthTitle.text = $"{viewYear}년 {viewMonth}월";
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
            bool hasData = CheckHasData(d);
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
            var label = new Label("");
            label.AddToClassList("cal-day");
            inner.Add(label);
            cell.Add(inner);
            return cell;
        }

        // 요일 스타일
        if (dow == 0) cell.AddToClassList("cal-cell--sun");
        if (dow == 6) cell.AddToClassList("cal-cell--sat");
        if (isToday) cell.AddToClassList("cal-cell--today");

        var cellInner = new VisualElement();
        cellInner.AddToClassList("cal-cell-inner");

        var dayLabel = new Label(day.ToString());
        dayLabel.AddToClassList("cal-day");
        cellInner.Add(dayLabel);
        cell.Add(cellInner);

        // 데이터 점
        if (hasData)
        {
            var dot = new VisualElement();
            dot.AddToClassList("cal-dot");
            cell.Add(dot);
        }

        // 클릭 이벤트
        int capturedDay = day;
        cell.RegisterCallback<ClickEvent>(evt => OnDayClicked(capturedDay));

        return cell;
    }

    private void OnDayClicked(int day)
    {
        // 이전 선택 해제
        calendarGrid.Query(className: "cal-cell--selected").ForEach(el =>
            el.RemoveFromClassList("cal-cell--selected"));

        selectedDay = day;

        // 선택 표시 (해당 셀 찾기)
        int index = (int)new DateTime(viewYear, viewMonth, 1).DayOfWeek + day - 1;
        if (index < calendarGrid.childCount)
        {
            calendarGrid[index].AddToClassList("cal-cell--selected");
        }

        // 하단 정보 표시
        var dt = new DateTime(viewYear, viewMonth, day);
        selectedDateLabel.text = dt.ToString("M월 d일 dddd");

        bool hasData = CheckHasData(day);
        if (hasData)
        {
            selectedSummary.text = "이 날의 방 기록이 있어요";
            btnViewRoom.SetEnabled(true);
        }
        else
        {
            selectedSummary.text = "이 날의 기록이 없어요";
            btnViewRoom.SetEnabled(false);
        }

        selectedInfo.AddToClassList("selected-info--visible");
    }

    private void OnViewRoomClicked()
    {
        if (selectedDay < 1) return;
        string dateKey = $"{viewYear}{viewMonth:D2}{selectedDay:D2}";
        PlayerPrefs.SetString("viewing_past_date", dateKey);
        Debug.Log($"[Calendar] 과거 방 보기: {dateKey}");
        ScreenManager.Instance.GoTo("past_room");
    }

    /// <summary>
    /// 해당 날짜에 데이터가 있는지 확인
    /// </summary>
    private bool CheckHasData(int day)
    {
        // TODO: Firestore에서 확인
        string key = $"diary_{viewYear}{viewMonth:D2}{day:D2}";
        return PlayerPrefs.HasKey(key);
    }
}
