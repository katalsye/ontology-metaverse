using Firebase.Firestore;

/// <summary>
/// Firestore temp_triples 컬렉션에 저장될 트리플 1개
/// 명세서(Docs/triple-json-spec.md) 규격 준수
/// 경로: temp_triples/{uid}/items/{auto_id}
/// </summary>
[FirestoreData]
public class TempTriple
{
    [FirestoreProperty]
    public string s { get; set; }   // Subject (예: "prod:user_001")

    [FirestoreProperty]
    public string p { get; set; }   // Predicate (예: "prod:visited")

    [FirestoreProperty]
    public string o { get; set; }   // Object (예: "강남 카페")

    [FirestoreProperty]
    public string datatype { get; set; }   // 데이터 타입 (예: "xsd:string", null 가능)

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }   // 업로드 시각
}