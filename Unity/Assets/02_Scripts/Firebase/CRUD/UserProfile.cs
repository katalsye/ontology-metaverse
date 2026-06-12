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

    // ontology_engine.py가 소문자 "persona" 필드에 추론 결과를 기록함
    [FirestoreProperty("persona")]
    public Persona Persona { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}