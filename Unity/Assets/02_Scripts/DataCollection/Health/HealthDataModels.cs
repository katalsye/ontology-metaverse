using System;
using System.Collections.Generic;

namespace OntologyMetaverse.DataCollection.Health
{
    /// <summary>
    /// HealthConnectPlugin.java 가 반환하는 JSON과 1:1 매칭되는 직렬화 모델.
    /// JsonUtility로 파싱 가능하도록 [Serializable] + public field.
    /// </summary>
    public static class HealthDataModels { }

    // ─── Sleep ─────────────────────────────────────────────
    // Java 반환: {"records":[{"durationHours":7.5,"quality":80,"deepSleepRatio":0.25,"startTime":"...","endTime":"..."}]}

    [Serializable]
    public class SleepRecordsJson
    {
        public List<SleepRecordRaw> records;
    }

    [Serializable]
    public class SleepRecordRaw
    {
        public float durationHours;
        public int quality;
        public float deepSleepRatio;
        public string startTime;
        public string endTime;
    }

    // ─── Steps ─────────────────────────────────────────────
    // Java 반환: {"totalSteps":2800,"startTime":"...","endTime":"..."}

    [Serializable]
    public class StepsResultJson
    {
        public long totalSteps;
        public string startTime;
        public string endTime;
    }

    // ─── HeartRate ────────────────────────────────────────
    // Java 반환: {"avgBpm":72.5,"sampleCount":42}

    [Serializable]
    public class HeartRateResultJson
    {
        public float avgBpm;
        public int sampleCount;
    }

    // ─── SDK Status (HealthConnectClient.getSdkStatus 매칭) ──
    public enum HealthConnectSdkStatus
    {
        Unavailable = 1,
        ProviderUpdateRequired = 2,
        Available = 3
    }
}
