/// <summary>
/// Firestore 컬렉션/서브컬렉션 이름 단일 소스.
/// 문자열 리터럴 산재로 인한 오타·리네임 취약성을 줄이기 위해 중앙화한다.
/// (일부 매니저는 다른 브랜치와의 충돌 회피를 위해 점진 적용 중 — 새 코드는 이 상수를 사용할 것.)
/// </summary>
public static class FirestoreCollections
{
    // 최상위 컬렉션
    public const string Users               = "users";
    public const string RoomObjects         = "room_objects";
    public const string RoomShopItems       = "room_shop_items";
    public const string RoomCustomPositions = "room_custom_positions";
    public const string RoomSnapshots       = "room_snapshots";
    public const string RoomSkins           = "room_skins";
    public const string RoomComments        = "room_comments";
    public const string Guestbooks          = "guestbooks";
    public const string Rewards             = "rewards";
    public const string Quests              = "quests";
    public const string TempTriples         = "temp_triples";
    public const string Follows             = "follows";
    public const string DiaryEntries        = "diary_entries";

    // 서브컬렉션
    public const string Notifications = "notifications";  // users/{uid}/notifications
    public const string FcmTokens     = "fcmTokens";      // users/{uid}/fcmTokens
    public const string Items         = "items";          // temp_triples/{uid}/items
    public const string Entries       = "entries";        // diary_entries/{uid}/entries
    public const string Snapshots     = "snapshots";      // room_snapshots/{uid}/snapshots
}
