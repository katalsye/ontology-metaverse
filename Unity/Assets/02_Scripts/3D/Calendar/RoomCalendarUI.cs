using System;
using UnityEngine;
using DatePicker;

// 탁상 캘린더 클릭 시 안드로이드 네이티브 달력 오픈 — 과거 방 스냅샷 날짜 선택용
public class RoomCalendarUI : MonoBehaviour
{
    public static RoomCalendarUI Instance { get; private set; }

    private IDatePicker _datePicker;

    void Awake()
    {
        Instance = this;

#if UNITY_EDITOR
        _datePicker = new EditorDatePickerStub();
#elif UNITY_ANDROID
        _datePicker = new AndroidDatePicker();
#endif
    }

    public void Open()
    {
        _datePicker?.Show(DateTime.Now, OnDatePicked);
    }

    void OnDatePicked(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");

        // TODO: 백엔드에서 해당 날짜 방 스냅샷 불러오기
        // RoomSnapshotManager.Instance.LoadSnapshot(dateStr, snapshot => { ... });
    }

#if UNITY_EDITOR
    class EditorDatePickerStub : IDatePicker
    {
        public void Show(DateTime initDate, Action<DateTime> callback)
        {
            // Debug.Log($"[RoomCalendar] 에디터 테스트 — 오늘({DateTime.Now:yyyy-MM-dd}) 선택");
            callback?.Invoke(DateTime.Now);
        }
    }
#endif
}
