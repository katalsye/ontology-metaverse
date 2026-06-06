using Firebase.Firestore;

[FirestoreData]
public class RoomObject
{
    [FirestoreProperty("objectId")]
    public string ObjectId { get; set; }

    [FirestoreProperty("objectType")]
    public string ObjectType { get; set; }

    [FirestoreProperty("placementZone")]
    public string PlacementZone { get; set; }

    [FirestoreProperty("positionX")]
    public float PositionX { get; set; }

    [FirestoreProperty("positionY")]
    public float PositionY { get; set; }

    [FirestoreProperty("positionZ")]
    public float PositionZ { get; set; }

    [FirestoreProperty("inferredFrom")]
    public string InferredFrom { get; set; }

    [FirestoreProperty("inferredFromConcept")]
    public string InferredFromConcept { get; set; }
}
