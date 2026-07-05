using System;
using System.Collections.Generic;
using Firebase.Firestore;

[FirestoreData]
public class RoomObject
{
    // Firestore 필드명 단일 소스. [FirestoreProperty]와 FromMap/ToMap이 공유한다.
    public const string FieldObjectId            = "objectId";
    public const string FieldObjectType          = "objectType";
    public const string FieldPlacementZone       = "placementZone";
    public const string FieldPositionX           = "positionX";
    public const string FieldPositionY           = "positionY";
    public const string FieldPositionZ           = "positionZ";
    public const string FieldInferredFrom        = "inferredFrom";
    public const string FieldInferredFromConcept = "inferredFromConcept";

    [FirestoreProperty(FieldObjectId)]
    public string ObjectId { get; set; }

    [FirestoreProperty(FieldObjectType)]
    public string ObjectType { get; set; }

    [FirestoreProperty(FieldPlacementZone)]
    public string PlacementZone { get; set; }

    [FirestoreProperty(FieldPositionX)]
    public float PositionX { get; set; }

    [FirestoreProperty(FieldPositionY)]
    public float PositionY { get; set; }

    [FirestoreProperty(FieldPositionZ)]
    public float PositionZ { get; set; }

    [FirestoreProperty(FieldInferredFrom)]
    public string InferredFrom { get; set; }

    [FirestoreProperty(FieldInferredFromConcept)]
    public string InferredFromConcept { get; set; }

    // ── room_objects 문서의 "objects" 배열(맵) 항목 ↔ RoomObject 변환 ──
    // 배열 내 맵이라 DocumentSnapshot.ConvertTo를 쓸 수 없어 수동 변환한다.
    // 필드명은 위 상수를 단일 소스로 사용(매니저 측 중복 제거).

    public static RoomObject FromMap(Dictionary<string, object> d)
    {
        return new RoomObject
        {
            ObjectId            = GetStr(d, FieldObjectId),
            ObjectType          = GetStr(d, FieldObjectType),
            PlacementZone       = GetStr(d, FieldPlacementZone),
            PositionX           = GetFloat(d, FieldPositionX),
            PositionY           = GetFloat(d, FieldPositionY),
            PositionZ           = GetFloat(d, FieldPositionZ),
            InferredFrom        = GetStr(d, FieldInferredFrom),
            InferredFromConcept = GetStr(d, FieldInferredFromConcept),
        };
    }

    public Dictionary<string, object> ToMap()
    {
        var d = new Dictionary<string, object>
        {
            { FieldObjectType,          ObjectType },
            { FieldPlacementZone,       PlacementZone },
            { FieldPositionX,           PositionX },
            { FieldPositionY,           PositionY },
            { FieldPositionZ,           PositionZ },
            { FieldInferredFrom,        InferredFrom },
            { FieldInferredFromConcept, InferredFromConcept },
        };
        // objectId는 비어있지 않을 때만 저장(기존 동작 유지)
        if (!string.IsNullOrEmpty(ObjectId))
            d[FieldObjectId] = ObjectId;
        return d;
    }

    private static string GetStr(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static float GetFloat(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v != null ? Convert.ToSingle(v) : 0f;
}
