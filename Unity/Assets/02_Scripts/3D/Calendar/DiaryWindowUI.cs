using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiaryWindowUI : MonoBehaviour
{
    public static DiaryWindowUI Instance { get; private set; }

    [Header("View Diary 패널")]
    public GameObject viewPanel;
    public TMP_Text   viewContent;
    public TMP_Text   viewDateLabel;   // 나중에 클릭 → 캘린더 연결
    public Button     viewCloseButton;
    public Button     prevButton;
    public Button     nextButton;

    [Header("Write Diary 패널")]
    public GameObject     writePanel;
    public TMP_InputField writeInputField;
    public TMP_Text       writeDateLabel;
    public Button         writeSaveButton;
    public Button         writeCancelButton;

    [Header("설정")]
    public int maxCharacters = 500;

    public bool IsAnyPanelOpen => (viewPanel  != null && viewPanel.activeSelf)
                                || (writePanel != null && writePanel.activeSelf);

    // 조회한 날짜 목록 (DB 연결 후 채워짐) — 오래된 순
    private List<string> _diaryDates = new List<string>();
    private int _currentIndex = -1;
    private string _today;

    void Awake()
    {
        Instance = this;
        if (viewPanel  != null) viewPanel.SetActive(false);
        if (writePanel != null) writePanel.SetActive(false);

        if (viewCloseButton   != null) viewCloseButton.onClick.AddListener(CloseAll);
        if (prevButton        != null) prevButton.onClick.AddListener(GoToPrev);
        if (nextButton        != null) nextButton.onClick.AddListener(GoToNext);
        if (writeSaveButton   != null) writeSaveButton.onClick.AddListener(OnSave);
        if (writeCancelButton != null) writeCancelButton.onClick.AddListener(CloseAll);
        if (writeInputField   != null) writeInputField.characterLimit = maxCharacters;

        // 날짜 레이블을 Button으로 사용 → 클릭 시 달력 오픈
        if (viewDateLabel != null)
        {
            var btn = viewDateLabel.GetComponent<Button>();
            if (btn == null) btn = viewDateLabel.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(OpenCalendar);
        }

        // 달력에서 날짜 선택 시 해당 일기 로드
        if (CalendarUI.Instance != null)
            CalendarUI.Instance.OnDateSelected += OnCalendarDateSelected;
    }

    void Start()
    {
        // Start에서도 CalendarUI 연결 (Awake 순서 문제 방지)
        if (CalendarUI.Instance != null)
        {
            CalendarUI.Instance.OnDateSelected -= OnCalendarDateSelected;
            CalendarUI.Instance.OnDateSelected += OnCalendarDateSelected;
        }
    }

    // BookOpen_01 클릭 → 저장된 다이어리 보기
    public void OpenViewDiary()
    {
        _today = System.DateTime.Now.ToString("yyyy-MM-dd");
        string yearMonth = System.DateTime.Now.ToString("yyyy-MM");

        if (DiaryManager.Instance == null)
        {
            _diaryDates = new List<string> { _today };
            _currentIndex = 0;
            LoadCurrentDiary();
            return;
        }

        DiaryManager.Instance.GetMonthlyDiaries(yearMonth,
            entries =>
            {
                _diaryDates = entries.ConvertAll(e => e.Date);
                _diaryDates.Sort();
                if (_diaryDates.Count == 0)
                {
                    ShowView("이번 달 일기가 없어요.", _today);
                    return;
                }
                _currentIndex = _diaryDates.Count - 1;
                LoadCurrentDiary();
            },
            err =>
            {
                Debug.LogWarning("[DiaryWindowUI] 월간 일기 로드 실패: " + err);
                _diaryDates = new List<string> { _today };
                _currentIndex = 0;
                LoadCurrentDiary();
            });
    }

    // LaunchTable / 펜 / 지우개 / 형광펜 클릭
    public void OpenWriteDiary()
    {
        _today = System.DateTime.Now.ToString("yyyy-MM-dd");

        if (DiaryManager.Instance == null) { ShowWrite(""); return; }

        DiaryManager.Instance.GetDiary(_today,
            entry =>
            {
                _diaryDates = new List<string> { _today };
                _currentIndex = 0;
                ShowView(entry.Content, _today);
            },
            _ => ShowWrite(""));
    }

    void OpenCalendar()
    {
        if (CalendarUI.Instance == null) return;
        bool ok = DateTime.TryParse(_today, out DateTime parsed);
        DateTime dt = ok ? parsed : DateTime.Now;
        CalendarUI.Instance.Open(dt.Year, dt.Month);
    }

    void OnCalendarDateSelected(string dateStr)
    {
        if (DiaryManager.Instance == null) { ShowView("DB 연결 안 됨", dateStr); return; }
        DiaryManager.Instance.GetDiary(dateStr,
            entry => ShowView(entry.Content, dateStr),
            _ => ShowView("해당 날짜 일기 없음", dateStr));
    }

    public void CloseAll()
    {
        if (viewPanel  != null) viewPanel.SetActive(false);
        if (writePanel != null) writePanel.SetActive(false);
    }

    // 배경 패널(반투명 오버레이) 클릭용 — Inspector에서 연결하거나 버튼 onClick에 추가
    public void OnBackgroundClick() => CloseAll();

    void GoToPrev()
    {
        if (_currentIndex <= 0) return;
        _currentIndex--;
        LoadCurrentDiary();
    }

    void GoToNext()
    {
        if (_currentIndex >= _diaryDates.Count - 1) return;
        _currentIndex++;
        LoadCurrentDiary();
    }

    void LoadCurrentDiary()
    {
        if (_currentIndex < 0 || _currentIndex >= _diaryDates.Count) return;

        string date = _diaryDates[_currentIndex];

        if (DiaryManager.Instance == null) { ShowView("-", date); return; }
        DiaryManager.Instance.GetDiary(date,
            entry => ShowView(entry.Content, date),
            _ => ShowView("-", date));
    }

    void ShowView(string content, string date = null)
    {
        if (writePanel != null) writePanel.SetActive(false);

        if (viewContent   != null) viewContent.text   = content;
        if (viewDateLabel != null) viewDateLabel.text  = date ?? _today;

        // 이전/다음 버튼 — 없으면 숨김
        if (prevButton != null) prevButton.gameObject.SetActive(_currentIndex > 0);
        if (nextButton != null) nextButton.gameObject.SetActive(_currentIndex < _diaryDates.Count - 1);

        if (viewPanel != null) viewPanel.SetActive(true);
    }

    void ShowWrite(string existingContent)
    {
        if (viewPanel       != null) viewPanel.SetActive(false);
        if (writeInputField != null) writeInputField.text = existingContent;
        if (writeDateLabel  != null) writeDateLabel.text  = _today;
        if (writePanel      != null) writePanel.SetActive(true);
    }

    void OnSave()
    {
        string content = writeInputField != null ? writeInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(content)) { CloseAll(); return; }
        if (DiaryManager.Instance == null) { CloseAll(); return; }

        DiaryManager.Instance.SaveDiary(content,
            onSuccess: () =>
            {
                DiaryManager.Instance.ClaimDiaryReward(_today,
                    onSuccess: () => Debug.Log("[DiaryWindowUI] 저장 + 보상 완료"),
                    onFailure: _ => { });
                CloseAll();
            },
            onFailure: err => Debug.LogError("[DiaryWindowUI] 저장 실패: " + err));
    }
}
