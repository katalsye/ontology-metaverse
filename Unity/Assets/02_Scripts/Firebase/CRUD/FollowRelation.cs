using Firebase.Firestore;

[FirestoreData]
public class FollowRelation
{
    [FirestoreProperty]
    public string FromUid { get; set; }

    [FirestoreProperty]
    public string ToUid { get; set; }

    [FirestoreProperty]
    public string Status { get; set; } // pending / accepted / rejected

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}