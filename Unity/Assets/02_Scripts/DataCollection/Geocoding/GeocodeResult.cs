using System;

namespace OntologyMetaverse.DataCollection.Geocoding
{
    /// <summary>
    /// 카카오 로컬 API 응답 → 우리 도메인 데이터 모델.
    ///
    /// 두 가지 정보 합성:
    ///   - coord2address: 도로명 주소 / 행정구역 (placeName 폴백)
    ///   - search/category: 카테고리 그룹 (placeType + 상호명)
    /// </summary>
    [Serializable]
    public class GeocodeResult
    {
        public string placeName;       // "스타벅스 강남R점" 또는 "서울 강남구 테헤란로 152"
        public string placeType;       // "cafe" | "restaurant" | "culture" | "hospital" | "general"
        public double lat;
        public double lng;
        public int sourceGpsId;        // 원본 GPS raw_data.Id (Location URI 매칭용)
        public string recordedAt;      // ISO 8601
    }
}
