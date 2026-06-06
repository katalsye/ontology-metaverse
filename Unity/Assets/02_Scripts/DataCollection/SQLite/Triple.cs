using SQLite;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// Gemma 3n이 추출한 온톨로지 트리플 (S, P, O)
    /// batch 시점에 신규분만 Firestore temp_triples로 sync
    /// </summary>
    [Table("triples")]
    public class Triple
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        /// <summary>
        /// 주어 (Subject) — 예: "user", "prod:loc_uid_ts"
        /// </summary>
        [NotNull]
        public string Subject { get; set; }
        
        /// <summary>
        /// 술어 (Predicate) — 예: "visited", "hasSleepData"
        /// </summary>
        [NotNull]
        public string Predicate { get; set; }
        
        /// <summary>
        /// 목적어 (Object) — 예: "cafe_gangnam", "6.5"
        /// </summary>
        [NotNull]
        public string Object { get; set; }
        
        /// <summary>
        /// 데이터 타입 — 예: "xsd:string", "xsd:float", "xsd:date" (NULL 가능)
        /// </summary>
        public string Datatype { get; set; }
        
        /// <summary>
        /// 추출 출처 — 예: "text_diary", "image_gallery", "gps", "health"
        /// </summary>
        [NotNull]
        public string Source { get; set; }
        
        /// <summary>
        /// 추출 시각 (ISO 8601 형식)
        /// </summary>
        [NotNull]
        public string Timestamp { get; set; }
        
        /// <summary>
        /// Firestore 동기화 여부 — 0: 미동기화, 1: 동기화 완료
        /// </summary>
        [NotNull]
        public int Synced { get; set; } = 0;
    }
}