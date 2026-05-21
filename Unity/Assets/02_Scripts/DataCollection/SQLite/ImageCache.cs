using SQLite;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// 이미지 분석 결과 캐싱 테이블
    /// 동일 사진을 Gemma 비전으로 재분석하지 않도록 방지 (서버 미전송)
    /// </summary>
    [Table("image_cache")]
    public class ImageCache
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        /// <summary>
        /// 이미지 파일 경로 (Unique)
        /// </summary>
        [NotNull, Unique]
        public string ImagePath { get; set; }
        
        /// <summary>
        /// Gemma 분석 결과 (트리플 JSON 형태로 저장)
        /// </summary>
        [NotNull]
        public string AnalysisResult { get; set; }
        
        /// <summary>
        /// 분석 시각 (ISO 8601 형식)
        /// </summary>
        [NotNull]
        public string Timestamp { get; set; }
    }
}