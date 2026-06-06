using Firebase.Firestore;

[FirestoreData]
public class Quest
{
    [FirestoreProperty]
    public string QuestId { get; set; }

    [FirestoreProperty]
    public string Title { get; set; }

    [FirestoreProperty]
    public string QuestType { get; set; } // 보완형 / 개선형

    [FirestoreProperty]
    public bool IsCompleted { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty]
    public Timestamp CompletedAt { get; set; }

    [FirestoreProperty]
    public int RewardAmount { get; set; }

    [FirestoreProperty]
    public string RewardType { get; set; } // 재화 / 아이템
}