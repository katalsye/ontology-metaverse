using UnityEngine;

[System.Serializable]
public class QuestData
{
    public string title;
    public string description;
    public string progressText;
    [Range(0f, 1f)] public float progressValue;
    public int reward;
    public Color themeColor;

    public QuestData(string title, string description, string progressText, float progressValue, int reward, Color themeColor)
    {
        this.title = title;
        this.description = description;
        this.progressText = progressText;
        this.progressValue = Mathf.Clamp01(progressValue);
        this.reward = reward;
        this.themeColor = themeColor;
    }
}
