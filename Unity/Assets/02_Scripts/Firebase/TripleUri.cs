/// <summary>
/// 온톨로지 트리플 URI 공통 헬퍼(단일 소스).
/// 업로더(TempTripleManager / TempTripleSyncManager)에 복붙돼 드리프트 위험이 있던
/// user URI 정규화 로직(#150)을 여기로 통합한다.
/// </summary>
public static class TripleUri
{
    public const string OntologyBaseUri = "http://7team.dev/ontology#";

    /// <summary>
    /// user_* 로 시작하는 노드 URI를 실제 로그인 uid 기준으로 통일.
    /// (추출 단계의 user_001 하드코딩 / 중복 prefix 등을 흡수)
    /// </summary>
    public static string NormalizeUserUri(string uri, string realUid)
    {
        if (!string.IsNullOrEmpty(uri) && uri.StartsWith(OntologyBaseUri + "user_"))
            return OntologyBaseUri + "user_" + realUid;
        return uri;
    }
}
