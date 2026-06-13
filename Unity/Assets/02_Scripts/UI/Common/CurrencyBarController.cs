using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 7-5. CurrencyBar 컨트롤러
/// 코인 + 보석 잔액 표시
/// </summary>
public class CurrencyBarController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private Label coinLabel;
    private Label gemLabel;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        coinLabel = root.Q<Label>("currency-coins");
        gemLabel = root.Q<Label>("currency-gems");
        Refresh();
    }

    public void Refresh()
    {
        if (RewardManager.Instance == null)
        {
            coinLabel.text = "0";
            gemLabel.text = "0";
            return;
        }

        // 코인: Firestore rewards/{uid}.Amount
        RewardManager.Instance.GetCurrency(
            onSuccess: coins => coinLabel.text = coins.ToString(),
            onFailure: _ => coinLabel.text = "0"
        );

        RewardManager.Instance.GetGems(
            onSuccess: gems => gemLabel.text = gems.ToString(),
            onFailure: _ => gemLabel.text = "0"
        );
    }

    public void AddCoins(int amount)
    {
        RewardManager.Instance.AddCurrency(amount, onSuccess: Refresh);
    }

    public void AddGems(int amount)
    {
        RewardManager.Instance.AddGems(amount, onSuccess: Refresh);
    }
}
