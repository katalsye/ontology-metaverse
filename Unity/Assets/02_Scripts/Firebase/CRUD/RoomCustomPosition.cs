using Firebase.Firestore;

[FirestoreData]
public class RoomCustomPosition
{
    [FirestoreProperty("objectId")]
    public string ObjectId { get; set; }

    [FirestoreProperty("positionX")]
    public float PositionX { get; set; }

    [FirestoreProperty("positionY")]
    public float PositionY { get; set; }

    [FirestoreProperty("positionZ")]
    public float PositionZ { get; set; }
}
