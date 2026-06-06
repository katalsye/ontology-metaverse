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
        int coins = PlayerPrefs.GetInt("coins", 0);
        int gems = PlayerPrefs.GetInt("gems", 0);
        coinLabel.text = coins.ToString();
        gemLabel.text = gems.ToString();
    }

    public void AddCoins(int amount)
    {
        int coins = PlayerPrefs.GetInt("coins", 0) + amount;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        Refresh();
    }

    public void AddGems(int amount)
    {
        int gems = PlayerPrefs.GetInt("gems", 0) + amount;
        PlayerPrefs.SetInt("gems", gems);
        PlayerPrefs.Save();
        Refresh();
    }
}
