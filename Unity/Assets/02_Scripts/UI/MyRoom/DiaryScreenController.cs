using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// 4-3 / 4-4. DiaryScreen 컨트롤러
/// 한줄일기 작성 + 과거 일기 열람
/// 저장 시 → 보상 팝업 → 마이룸 복귀
/// </summary>
public class DiaryScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("References")]
    [SerializeField] private RewardPopupController rewardPopup;

    private VisualElement root;
    private TextField inputDiary;
    private Label charCount;
    private Label diaryDate;
    private Label streakCount;
    private Button btnSave;

    private const int MAX_CHARS = 100;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 바인딩
        root.Q<Button>("btn-back").clicked += () => ScreenManager.Instance.GoBack();
        root.Q<Button>("btn-history").clicked += OnHistoryClicked;

        inputDiary = root.Q<TextField>("input-diary");
        charCount = root.Q<Label>("char-count");
        diaryDate = root.Q<Label>("diary-date");
        streakCount = root.Q<Label>("streak-count");
        btnSave = root.Q<Button>("btn-save");

        btnSave.clicked += OnSaveClicked;
        inputDiary.RegisterValueChangedCallback(OnTextChanged);

        // 오늘 날짜 설정
        diaryDate.text = DateTime.Now.ToString("yyyy년 M월 d일 dddd");

        // 연속 기록일 로드
        int streak = PlayerPrefs.GetInt("diary_streak", 0);
        streakCount.text = streak > 0
            ? $"{streak}일 연속 기록 중!"
            : "오늘 첫 기록을 남겨보세요!";

        // Firestore에서 오늘 일기 조회 (작성 여부 확인)
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        DiaryManager.Instance.GetDiary(today,
            onSuccess: entry =>
            {
                inputDiary.value = entry.Content;
                inputDiary.SetEnabled(false);
                btnSave.SetEnabled(false);
                btnSave.text = "오늘 이미 작성했어요";
            },
            onFailure: _ => { /* 일기 없음 = 정상, 아무것도 하지 않음 */ }
        );
    }

    private void OnTextChanged(ChangeEvent<string> evt)
    {
        string text = evt.newValue;

        // 글자수 제한
        if (text.Length > MAX_CHARS)
        {
            inputDiary.SetValueWithoutNotify(text.Substring(0, MAX_CHARS));
            text = inputDiary.value;
        }

        charCount.text = $"{text.Length} / {MAX_CHARS}";

        // 빈 텍스트면 저장 비활성화
        btnSave.SetEnabled(text.Trim().Length > 0);
    }

    private void OnSaveClicked()
    {
        string text = inputDiary.value.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string today = DateTime.Now.ToString("yyyy-MM-dd");

        btnSave.SetEnabled(false);
        btnSave.text = "저장 중...";

        DiaryManager.Instance.SaveDiary(text,
            onSuccess: () =>
            {
                UpdateStreakLocally(today);
                AudioManager.Instance?.PlaySFX(4);

                DiaryManager.Instance.ClaimDiaryReward(today,
                    onSuccess: () =>
                    {
                        AudioManager.Instance?.PlaySFX(1);
                        if (rewardPopup != null)
                            rewardPopup.Show("코인", 10, () => ScreenManager.Instance.GoBack());
                        else
                            ScreenManager.Instance.GoBack();
                    },
                    onFailure: _ =>
                    {
                        // 이미 보상 수령한 경우 포함 — 그냥 뒤로
                        ScreenManager.Instance.GoBack();
                    }
                );
            },
            onFailure: err =>
            {
                Debug.LogError($"[Diary] 저장 실패: {err}");
                btnSave.SetEnabled(true);
                btnSave.text = "저장하기";
            }
        );
    }

    private void UpdateStreakLocally(string today)
    {
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        string prevDate = PlayerPrefs.GetString("diary_prev_date", "");
        int streak = PlayerPrefs.GetInt("diary_streak", 0);

        streak = prevDate == yesterday ? streak + 1 : 1;

        PlayerPrefs.SetInt("diary_streak", streak);
        PlayerPrefs.SetString("diary_prev_date", today);
        PlayerPrefs.Save();

        streakCount.text = $"{streak}일 연속 기록 중!";
    }

    private void OnHistoryClicked()
    {
        Debug.Log("[Diary] → 과거 일기 열람");
        ScreenManager.Instance.GoTo("diary_history");
    }
}
