using SQLite;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// Gemma 추론 처리 대기열의 항목
    /// 미처리된 raw_data를 큐에 넣고 순차적으로 트리플 추출 (서버 미전송)
    /// </summary>
    [Table("inference_queue")]
    public class InferenceQueueItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        /// <summary>
        /// 처리 대상 raw_data의 ID (외래 키)
        /// </summary>
        [NotNull]
        public int RawDataId { get; set; }
        
        /// <summary>
        /// 처리 상태 — "pending", "processing", "done", "failed"
        /// </summary>
        [NotNull]
        public string Status { get; set; } = "pending";
        
        /// <summary>
        /// 큐 등록 시각 (ISO 8601 형식)
        /// </summary>
        [NotNull]
        public string Timestamp { get; set; }
        
        /// <summary>
        /// 재시도 횟수 (실패 시 카운트, 3회 초과 시 포기)
        /// </summary>
        [NotNull]
        public int RetryCount { get; set; } = 0;
    }
}