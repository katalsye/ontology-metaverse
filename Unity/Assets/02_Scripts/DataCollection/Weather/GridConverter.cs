using System;
using UnityEngine;

namespace OntologyMetaverse.DataCollection.Weather
{
    /// <summary>
    /// 위도/경도 → 기상청 단기예보용 격자 X/Y 좌표 변환.
    ///
    /// 기상청 API는 WGS84 좌표(GPS)가 아니라 Lambert Conformal Conic 투영법으로
    /// 변환한 격자 좌표 (정수 nx, ny) 를 받음.
    ///
    /// 변환식은 기상청 공식 문서 기준 (https://www.kma.go.kr 단기예보 API 가이드).
    /// 한국 격자 시스템 파라미터:
    ///   - RE: 지구 반경 (km)
    ///   - GRID: 격자 간격 5km
    ///   - SLAT1/SLAT2: 표준 위도 30°/60°
    ///   - OLON/OLAT: 기준점 경도 126°/위도 38° (한반도 중심)
    ///   - XO/YO: 기준점 격자 좌표 (한반도 좌하단)
    ///
    /// 검증값 (참고):
    ///   서울시청 (37.5665, 126.9780) → (60, 127)
    ///   대구 (35.8714, 128.6014)     → (89, 90)
    ///   부산 (35.1796, 129.0756)     → (98, 76)
    /// </summary>
    public static class GridConverter
    {
        // ─── 기상청 LCC 투영법 상수 ─────────────────────────────────
        private const double RE = 6371.00877;      // 지구 반경 (km)
        private const double GRID = 5.0;           // 격자 간격 (km)
        private const double SLAT1 = 30.0;         // 투영 위도1 (°)
        private const double SLAT2 = 60.0;         // 투영 위도2 (°)
        private const double OLON = 126.0;         // 기준점 경도 (°)
        private const double OLAT = 38.0;          // 기준점 위도 (°)
        private const int XO = 43;                 // 기준점 X좌표 (격자)
        private const int YO = 136;                // 기준점 Y좌표 (격자)

        /// <summary>
        /// GPS lat/lng → 기상청 격자 nx/ny (정수).
        /// </summary>
        public static (int nx, int ny) ToGrid(double lat, double lng)
        {
            double DEGRAD = Math.PI / 180.0;

            double re = RE / GRID;
            double slat1 = SLAT1 * DEGRAD;
            double slat2 = SLAT2 * DEGRAD;
            double olon = OLON * DEGRAD;
            double olat = OLAT * DEGRAD;

            double sn = Math.Tan(Math.PI * 0.25 + slat2 * 0.5) / Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
            sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) / Math.Log(sn);

            double sf = Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
            sf = Math.Pow(sf, sn) * Math.Cos(slat1) / sn;

            double ro = Math.Tan(Math.PI * 0.25 + olat * 0.5);
            ro = re * sf / Math.Pow(ro, sn);

            double ra = Math.Tan(Math.PI * 0.25 + lat * DEGRAD * 0.5);
            ra = re * sf / Math.Pow(ra, sn);

            double theta = lng * DEGRAD - olon;
            if (theta > Math.PI) theta -= 2.0 * Math.PI;
            if (theta < -Math.PI) theta += 2.0 * Math.PI;
            theta *= sn;

            int nx = (int)Math.Floor(ra * Math.Sin(theta) + XO + 0.5);
            int ny = (int)Math.Floor(ro - ra * Math.Cos(theta) + YO + 0.5);

            return (nx, ny);
        }
    }
}
