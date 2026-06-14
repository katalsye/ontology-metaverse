using Firebase.Firestore;

[FirestoreData]
public class RoomShopItem
{
    [FirestoreProperty("furnitureId")]
    public int FurnitureId { get; set; }

    [FirestoreProperty("colorId")]
    public int ColorId { get; set; }

    [FirestoreProperty("positionX")]
    public float PositionX { get; set; }

    [FirestoreProperty("positionY")]
    public float PositionY { get; set; }

    [FirestoreProperty("positionZ")]
    public float PositionZ { get; set; }

    [FirestoreProperty("rotationX")]
    public float RotationX { get; set; }

    [FirestoreProperty("rotationY")]
    public float RotationY { get; set; }

    [FirestoreProperty("rotationZ")]
    public float RotationZ { get; set; }

    [FirestoreProperty("scaleX")]
    public float ScaleX { get; set; }

    [FirestoreProperty("scaleY")]
    public float ScaleY { get; set; }

    [FirestoreProperty("scaleZ")]
    public float ScaleZ { get; set; }
}
