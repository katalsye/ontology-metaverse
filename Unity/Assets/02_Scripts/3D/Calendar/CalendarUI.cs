using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DatePicker;

public class CalendarUI : MonoBehaviour
{
    public static CalendarUI Instance { get; private set; }

    [Header("점 패널 (Unity UI)")]
    public GameObject  dotPanel;
    public TMP_Text    dotMonthLabel;
    public Transform   dotGrid;      // Grid Layout Group, 7열

    [Header("색상")]
    public Color colorHasDiary = Color.black;
    public Color colorNoDiary  = new Color(0.7f, 0.7f, 0.7f);
    public Color colorToday    = new Color(0.2f, 0.6f, 1f);

    public event Action<string> OnDateSelected;

    private IDatePicker _datePicker;
    private HashSet<string> _diaryDates = new HashSet<string>();
    private int _year, _month;
    private List<Image> _dots = new List<Image>();

    void Awake()
    {
        Instance = this;
        if (dotPanel != null) dotPanel.SetActive(false);

#if UNITY_EDITOR
        _datePicker = new EditorDatePickerStub();
#elif UNITY_ANDROID
        _datePicker = new AndroidDatePicker();
#endif
    }

    void Start() => BuildDots();

    public void Open(int year, int month)
    {
        _year  = year;
        _month = month;

        if (DiaryManager.Instance != null)
            DiaryManager.Instance.GetMonthlyDiaries($"{year:D4}-{month:D2}",
                entries => SetDiaryDates(entries.ConvertAll(e => e.Date)));

        RefreshDots();
        if (dotPanel != null) dotPanel.SetActive(true);

        // 네이티브 피커 열기
        _datePicker?.Show(new DateTime(year, month, 1), OnPicked);
    }

    public void SetDiaryDates(IEnumerable<string> dates)
    {
        _diaryDates = new HashSet<string>(dates);
        RefreshDots();
    }

    void OnPicked(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");
        if (dotPanel != null) dotPanel.SetActive(false);
        OnDateSelected?.Invoke(dateStr);
        LoadPastRoom(dateStr);
    }

    void LoadPastRoom(string dateStr)
    {
        if (RoomSnapshotManager.Instance == null) return;
        RoomSnapshotManager.Instance.GetSnapshotByDate(dateStr,
            snapshot =>
            {
                if (RoomObjectManager.Instance != null)
                    RoomObjectManager.Instance.ApplySnapshot(snapshot.Objects);
            },
            err => Debug.LogWarning($"[CalendarUI] 스냅샷 없음({dateStr}): {err}")
        );
    }

    public void Close()
    {
        if (dotPanel != null) dotPanel.SetActive(false);
    }

    void RefreshDots()
    {
        if (dotMonthLabel != null)
            dotMonthLabel.text = $"{_year}년 {_month}월";

        int daysInMonth = DateTime.DaysInMonth(_year, _month);
        string today    = DateTime.Now.ToString("yyyy-MM-dd");

        for (int i = 0; i < _dots.Count; i++)
        {
            int  day   = i + 1;
            bool valid = day <= daysInMonth;
            _dots[i].gameObject.SetActive(valid);
            if (!valid) continue;

            string dateStr = $"{_year:D4}-{_month:D2}-{day:D2}";
            bool hasDiary  = _diaryDates.Contains(dateStr);
            bool isToday   = dateStr == today;

            _dots[i].color = isToday ? colorToday : hasDiary ? colorHasDiary : colorNoDiary;
        }
    }

    void BuildDots()
    {
        if (dotGrid == null) return;
        foreach (Transform child in dotGrid) Destroy(child.gameObject);
        _dots.Clear();

        for (int i = 0; i < 31; i++)
        {
            var go  = new GameObject($"Dot_{i + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(dotGrid, false);
            var img = go.GetComponent<Image>();
            img.color = colorNoDiary;
            _dots.Add(img);
        }
    }

#if UNITY_EDITOR
    class EditorDatePickerStub : IDatePicker
    {
        public void Show(DateTime initDate, Action<DateTime> callback)
        {
            // 에디터 테스트용 — 오늘 날짜로 바로 선택
            // Debug.Log($"[CalendarUI] 에디터 테스트 — 오늘({DateTime.Now:yyyy-MM-dd}) 선택");
            callback?.Invoke(DateTime.Now);
        }
    }
#endif
}
