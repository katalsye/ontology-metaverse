using Firebase.Firestore;

[FirestoreData]
public class UserProfile
{
    [FirestoreProperty]
    public string Uid { get; set; }

    [FirestoreProperty]
    public string Nickname { get; set; }

    [FirestoreProperty]
    public string Email { get; set; }

    [FirestoreProperty]
    public string ProfileImageUrl { get; set; }

    [FirestoreProperty]
    public string StatusMessage { get; set; }

    [FirestoreProperty]
    public bool IsPublic { get; set; }

    [FirestoreProperty]
    public Persona Persona { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}