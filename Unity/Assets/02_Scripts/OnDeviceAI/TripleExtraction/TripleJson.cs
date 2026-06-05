using System;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// Gemma가 추출한 트리플 1개를 표현하는 데이터 클래스
    /// 명세서(Docs/triple-json-spec.md) 규격을 따름
    /// </summary>
    [Serializable]
    public class TripleJson
    {
        public string s;          // Subject (예: "prod:user_{uid}")
        public string p;          // Predicate (예: "prod:visited")
        public string o;          // Object (예: "강남 카페")
        public string datatype;   // 데이터 타입 (예: "xsd:string", null 가능)

        // 보기 좋게 출력 (디버깅용)
        public override string ToString()
        {
            return $"({s}, {p}, {o}, datatype={datatype})";
        }
    }

    /// <summary>
    /// Gemma 응답 JSON을 통째로 받기 위한 래퍼 클래스
    /// 예시: {"triples": [ ... ]}
    /// </summary>
    [Serializable]
    public class TripleJsonResponse
    {
        public TripleJson[] triples;
    }
}