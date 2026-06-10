using UnityEngine;

namespace OntologyMetaverse.DataCollection.Calendar
{
    /// <summary>
    /// CalendarHelper.java 의 static 메서드 호출 wrapper.
    /// </summary>
    public static class CalendarBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string PluginClassName = "com.ontology.metaverse.calendar.CalendarHelper";
        private static AndroidJavaClass _cls;
        private static AndroidJavaClass Cls
        {
            get
            {
                if (_cls == null) _cls = new AndroidJavaClass(PluginClassName);
                return _cls;
            }
        }

        private static AndroidJavaObject GetActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
#endif

        /// <summary>
        /// READ_CALENDAR 권한 grant 여부.
        /// </summary>
        public static bool HasPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<bool>("hasPermission", GetActivity()); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CalendarBridge] hasPermission 실패: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// 지정 시간 범위의 캘린더 이벤트를 JSON으로 반환.
        /// </summary>
        /// <param name="pastOffsetMs">음수 (예: -7일 = -7L*86400000L)</param>
        /// <param name="futureOffsetMs">양수</param>
        public static string GetEventsJson(long pastOffsetMs, long futureOffsetMs)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return Cls.CallStatic<string>("getEventsJson", GetActivity(), pastOffsetMs, futureOffsetMs); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CalendarBridge] getEventsJson 실패: {e.Message}");
                return "{\"events\":[],\"error\":\"" + e.Message + "\"}";
            }
#else
            return "{\"events\":[]}";
#endif
        }
    }
}
