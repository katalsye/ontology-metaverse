using SQLite;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// 수집한 raw 데이터를 저장하는 테이블
    /// 서버 미전송 (프라이버시 핵심 원칙)
    /// </summary>
    [Table("raw_data")]
    public class RawData
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        /// <summary>
        /// 데이터 타입: "gps", "exif", "step", "sleep", "app_usage" 등
        /// </summary>
        [NotNull]
        public string Type { get; set; }
        
        /// <summary>
        /// 실제 데이터 (JSON 형식으로 저장)
        /// </summary>
        [NotNull]
        public string Content { get; set; }
        
        /// <summary>
        /// 수집 시각 (ISO 8601 형식)
        /// </summary>
        [NotNull]
        public string Timestamp { get; set; }
    }
}