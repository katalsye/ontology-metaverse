using Firebase.Firestore;

[FirestoreData]
public class RoomObject
{
    [FirestoreProperty]
    public string ObjectId { get; set; }

    [FirestoreProperty]
    public string ObjectType { get; set; }

    [FirestoreProperty]
    public float PositionX { get; set; }

    [FirestoreProperty]
    public float PositionY { get; set; }

    [FirestoreProperty]
    public float PositionZ { get; set; }

    [FirestoreProperty]
    public string InferredFrom { get; set; }
}