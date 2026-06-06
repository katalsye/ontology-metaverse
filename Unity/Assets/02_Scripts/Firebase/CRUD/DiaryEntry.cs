using Firebase.Firestore;

[FirestoreData]
public class DiaryEntry
{
    [FirestoreProperty]
    public string EntryId { get; set; }

    [FirestoreProperty]
    public string Date { get; set; } // yyyy-MM-dd

    [FirestoreProperty]
    public string Content { get; set; }

    [FirestoreProperty]
    public bool RewardClaimed { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}