using Firebase.Firestore;

[FirestoreData]
public class NotificationData
{
    // doc.Id에서 수동 설정 — Firestore 필드 아님
    public string NotificationId { get; set; }

    [FirestoreProperty]
    public string Type { get; set; }  // follow_request / room_updated / quest_completed / new_quest

    [FirestoreProperty]
    public string Title { get; set; }

    [FirestoreProperty]
    public string Body { get; set; }

    [FirestoreProperty]
    public string Payload { get; set; }  // JSON (fromUid, roomId, questId 등)

    [FirestoreProperty]
    public bool IsRead { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}
