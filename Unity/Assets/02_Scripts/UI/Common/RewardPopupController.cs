using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// 7-4. RewardPopup 컨트롤러
/// 재화/아이템 획득 팝업
/// </summary>
public class RewardPopupController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement overlay;
    private Label rewardName;
    private Label rewardAmount;
    private Button btnConfirm;
    private Action onClose;

    private void Awake()
    {
        var root = uiDocument.rootVisualElement;
        overlay = root.Q("reward-overlay");
        rewardName = root.Q<Label>("reward-name");
        rewardAmount = root.Q<Label>("reward-amount");
        btnConfirm = root.Q<Button>("btn-reward-confirm");

        btnConfirm.clicked += () =>
        {
            overlay.style.display = DisplayStyle.None;
            onClose?.Invoke();
        };
    }

    public void Show(string itemName, int amount, Action callback)
    {
        rewardName.text = itemName;
        rewardAmount.text = $"× {amount}";
        onClose = callback;
        overlay.style.display = DisplayStyle.Flex;
        AudioManager.Instance?.PlaySFX(1);
    }
}
