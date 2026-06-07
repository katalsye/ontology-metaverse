namespace OntologyMetaverse.DataCollection.Weather
{
    /// <summary>
    /// 기상청 PTY + SKY 코드 → 무성님 명세 condition 문자열 매핑.
    ///
    /// PTY (강수 형태):
    ///   0 = 없음
    ///   1 = 비
    ///   2 = 비/눈
    ///   3 = 눈
    ///   4 = 소나기
    ///   5 = 빗방울
    ///   6 = 빗방울눈날림
    ///   7 = 눈날림
    ///
    /// SKY (하늘 상태, PTY=0일 때만 의미 있음):
    ///   1 = 맑음
    ///   3 = 구름많음
    ///   4 = 흐림
    ///
    /// 무성님 Rule 7 (IndoorDayPattern) 은 condition 안에 "rain" 또는
    /// "sun"/"clear" 단어를 검사함. 영문 소문자로 통일.
    /// </summary>
    public static class WeatherCondition
    {
        public static string Map(int pty, int sky)
        {
            // 강수가 있으면 강수 형태 우선
            switch (pty)
            {
                case 1: return "rain";
                case 2: return "sleet";       // 비/눈 섞임
                case 3: return "snow";
                case 4: return "rain";        // 소나기도 rain 으로 (Rule 7 매칭)
                case 5: return "drizzle";
                case 6: return "sleet";
                case 7: return "snow";
            }

            // 강수 없으면 하늘 상태
            switch (sky)
            {
                case 1: return "clear";       // Rule 7에서 sun/clear 매칭
                case 3: return "cloudy";
                case 4: return "overcast";
            }

            // 초단기실황 API는 SKY 미제공. PTY=0이고 SKY 정보 없으면 기본 "clear"
            // (Rule 7은 condition CONTAINS "rain" 만 검사하므로 비 아니면 안전)
            return pty == 0 ? "clear" : "unknown";
        }
    }
}
