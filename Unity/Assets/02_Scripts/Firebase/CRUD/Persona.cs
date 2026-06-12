using Firebase.Firestore;

/// <summary>
/// ontology_engine.py가 users/{uid}.persona(소문자) 필드에 기록하는
/// camelCase 스키마와 1:1로 매핑된다 (energyType/socialPreference/lifePattern/
/// recoveryLevel/updatedAt, updatedAt은 RDF 리터럴을 str()화한 문자열).
/// </summary>
[FirestoreData]
public class Persona
{
    [FirestoreProperty("energyType")]
    public string EnergyType { get; set; }

    [FirestoreProperty("socialPreference")]
    public string SocialPreference { get; set; }

    [FirestoreProperty("lifePattern")]
    public string LifePattern { get; set; }

    [FirestoreProperty("recoveryLevel")]
    public string RecoveryLevel { get; set; }

    [FirestoreProperty("updatedAt")]
    public string UpdatedAt { get; set; }
}