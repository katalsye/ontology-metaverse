using Firebase.Firestore;
using System.Collections.Generic;

[FirestoreData]
public class RoomSnapshot
{
    [FirestoreProperty]
    public string SnapshotId { get; set; }

    [FirestoreProperty]
    public string Date { get; set; } // yyyy-MM-dd

    [FirestoreProperty]
    public List<RoomObject> Objects { get; set; } // 해당 날짜의 방 오브젝트 목록

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}