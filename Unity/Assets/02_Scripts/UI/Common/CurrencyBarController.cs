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
        // 코인: Firestore rewards/{uid}.Amount
        RewardManager.Instance.GetCurrency(
            onSuccess: coins => coinLabel.text = coins.ToString(),
            onFailure: _ => coinLabel.text = "0"
        );

        // 보석: Firestore 미구현, PlayerPrefs 유지
        gemLabel.text = PlayerPrefs.GetInt("gems", 0).ToString();
    }

    public void AddCoins(int amount)
    {
        RewardManager.Instance.AddCurrency(amount, onSuccess: Refresh);
    }

    public void AddGems(int amount)
    {
        int gems = PlayerPrefs.GetInt("gems", 0) + amount;
        PlayerPrefs.SetInt("gems", gems);
        PlayerPrefs.Save();
        gemLabel.text = gems.ToString();
    }
}
