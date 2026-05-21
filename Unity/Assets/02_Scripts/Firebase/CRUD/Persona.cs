using Firebase.Firestore;

[FirestoreData]
public class Persona
{
    [FirestoreProperty]
    public string EnergyType { get; set; }

    [FirestoreProperty]
    public string SocialPreference { get; set; }

    [FirestoreProperty]
    public string LifePattern { get; set; }

    [FirestoreProperty]
    public Timestamp UpdatedAt { get; set; }
}