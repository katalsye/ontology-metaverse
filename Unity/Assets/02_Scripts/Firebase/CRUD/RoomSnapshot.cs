using Firebase.Firestore;
using System.Collections.Generic;

[FirestoreData]
public class RoomSnapshot
{
    [FirestoreProperty]
    public string SnapshotId { get; set; }

    [FirestoreProperty]
    public string Date { get; set; } // yyyy-MM-dd

    [FirestoreProperty("objects")]
    public List<RoomObject> Objects { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
