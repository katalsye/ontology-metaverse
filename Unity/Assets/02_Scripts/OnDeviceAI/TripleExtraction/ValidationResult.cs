namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// 트리플 검증 결과를 담는 데이터 클래스
    /// 검증 통과 여부와 실패 시 이유를 함께 반환
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid;        // 통과 여부
        public string ErrorReason;  // 실패 사유 (통과 시 빈 문자열)

        // 통과 결과 생성 (간편 메서드)
        public static ValidationResult Success()
        {
            return new ValidationResult
            {
                IsValid = true,
                ErrorReason = ""
            };
        }

        // 실패 결과 생성 (간편 메서드)
        public static ValidationResult Fail(string reason)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorReason = reason
            };
        }
    }
}