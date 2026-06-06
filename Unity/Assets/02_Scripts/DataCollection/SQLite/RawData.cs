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

        /// <summary>
        /// 트리플 변환 처리 여부.
        /// 0 = 미처리 (Gemma 변환 대기 중)
        /// 1 = 처리 완료 (이미 triples 테이블로 변환됨)
        ///
        /// RawDataToTripleConverter가 batch마다 Processed=0인 것들만 가져와 처리 후 1로 마크.
        /// 기본값 0 (SQLite-net이 신규 컬럼 추가 시 기존 row는 0으로 채움).
        /// </summary>
        public int Processed { get; set; }
    }
}