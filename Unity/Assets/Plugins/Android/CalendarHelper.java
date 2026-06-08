package com.ontology.metaverse.calendar;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.pm.PackageManager;
import android.database.Cursor;
import android.net.Uri;
import android.provider.CalendarContract;
import android.util.Log;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

/**
 * Android 로컬 캘린더(CalendarContract) 헬퍼 (Unity ↔ Java 브릿지).
 *
 * 동작:
 *   - ContentResolver로 CalendarContract.Events 쿼리
 *   - Google 계정 / Samsung 계정 / 기기 캘린더 모두 동일 API로 접근
 *   - 권한: android.permission.READ_CALENDAR (Manifest + runtime)
 *
 * Unity 호출:
 *   var cls = new AndroidJavaClass("com.ontology.metaverse.calendar.CalendarHelper");
 *   bool has = cls.CallStatic&lt;bool&gt;("hasPermission", activity);
 *   string json = cls.CallStatic&lt;string&gt;("getEventsJson", activity, -7L*86400000L, 7L*86400000L);
 *
 * 반환 JSON 형식:
 *   {"events":[
 *     {"event_id":1234,"title":"팀 회의","startTime":"2026-06-08T10:00:00Z",
 *      "endTime":"2026-06-08T11:00:00Z","isRecurring":true,"location":"강남역"},
 *     ...
 *   ]}
 *
 * 무성님 명세 매칭:
 *   prod:CalendarEvent + eventTitle, startTime, endTime, isRecurring
 *   → Rule 6-F (missing_event_review), Rule 8 (Routine), Rule 29 (ScheduleOverload), Rule P5 (routine 페르소나)
 */
public class CalendarHelper {
    private static final String TAG = "CalendarHelper";

    private static final String[] PROJECTION = new String[] {
        CalendarContract.Events._ID,
        CalendarContract.Events.TITLE,
        CalendarContract.Events.DTSTART,
        CalendarContract.Events.DTEND,
        CalendarContract.Events.RRULE,
        CalendarContract.Events.EVENT_LOCATION,
        CalendarContract.Events.CALENDAR_DISPLAY_NAME,
    };

    // ISO 8601 naive KST formatter.
    // 무성님 추론 규칙(Rule 8 routine 등)이 HOURS()로 시각을 추출하는데,
    // test_rules.py가 타임존 없는 naive 시각을 KST로 가정하므로 거기 맞춤.
    // (타임존 표기 'Z' 없이 Asia/Seoul 로컬 시각 출력)
    private static final SimpleDateFormat ISO_FORMAT;
    static {
        ISO_FORMAT = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US);
        ISO_FORMAT.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
    }

    /**
     * READ_CALENDAR 권한 grant 여부.
     */
    public static boolean hasPermission(Activity activity) {
        if (activity == null) return false;
        return activity.checkSelfPermission(android.Manifest.permission.READ_CALENDAR)
            == PackageManager.PERMISSION_GRANTED;
    }

    /**
     * 지정 시간 범위의 캘린더 이벤트를 JSON으로 반환.
     *
     * @param pastOffsetMs  현재로부터 과거로 이만큼 (음수, 예: -7일 → -7*86400000L)
     * @param futureOffsetMs 현재로부터 미래로 이만큼 (양수)
     * @return {"events":[...]} JSON 문자열. 에러 시 {"events":[],"error":"..."}
     */
    public static String getEventsJson(Activity activity, long pastOffsetMs, long futureOffsetMs) {
        if (!hasPermission(activity)) {
            return "{\"events\":[],\"error\":\"READ_CALENDAR 권한 없음\"}";
        }

        long now = System.currentTimeMillis();
        long startWindow = now + pastOffsetMs;   // pastOffsetMs는 음수
        long endWindow = now + futureOffsetMs;

        // dtstart가 윈도우 안에 들어오는 이벤트 (DTEND 기준 필터링 X — 종료가 미래여도 시작이 윈도우 밖이면 제외)
        // CalendarContract.Events의 DTSTART/DTEND는 epoch millis (Long)
        String selection = CalendarContract.Events.DTSTART + " >= ? AND "
                         + CalendarContract.Events.DTSTART + " <= ? AND "
                         + CalendarContract.Events.DELETED + " = 0";
        String[] selectionArgs = new String[] {
            String.valueOf(startWindow),
            String.valueOf(endWindow)
        };
        String sortOrder = CalendarContract.Events.DTSTART + " ASC";

        StringBuilder sb = new StringBuilder("{\"events\":[");
        boolean first = true;
        int totalCount = 0;
        int skippedNoTitle = 0;

        try (Cursor cursor = activity.getContentResolver().query(
                CalendarContract.Events.CONTENT_URI,
                PROJECTION,
                selection,
                selectionArgs,
                sortOrder)) {

            if (cursor == null) {
                return "{\"events\":[],\"error\":\"cursor null\"}";
            }

            while (cursor.moveToNext()) {
                long eventId = cursor.getLong(0);
                String title = cursor.getString(1);
                long dtStart = cursor.getLong(2);
                long dtEnd = cursor.isNull(3) ? dtStart : cursor.getLong(3); // 종료 없으면 시작과 동일
                String rrule = cursor.getString(4);
                String location = cursor.getString(5);
                String calendarName = cursor.getString(6);

                // title 비어있는 이벤트는 시그널이 약함 — 스킵
                if (title == null || title.trim().isEmpty()) {
                    skippedNoTitle++;
                    continue;
                }

                if (!first) sb.append(",");
                sb.append("{")
                    .append("\"event_id\":").append(eventId)
                    .append(",\"title\":\"").append(escapeJson(title)).append("\"")
                    .append(",\"startTime\":\"").append(ISO_FORMAT.format(new Date(dtStart))).append("\"")
                    .append(",\"endTime\":\"").append(ISO_FORMAT.format(new Date(dtEnd))).append("\"")
                    .append(",\"isRecurring\":").append(rrule != null && !rrule.isEmpty())
                    .append(",\"location\":\"").append(escapeJson(location != null ? location : "")).append("\"")
                    .append(",\"calendarName\":\"").append(escapeJson(calendarName != null ? calendarName : "")).append("\"")
                    .append("}");
                first = false;
                totalCount++;
            }
        } catch (SecurityException e) {
            Log.e(TAG, "READ_CALENDAR 권한 거부: " + e.getMessage(), e);
            return "{\"events\":[],\"error\":\"SecurityException: " + escapeJson(e.getMessage()) + "\"}";
        } catch (Exception e) {
            Log.e(TAG, "getEventsJson 예외: " + e.getMessage(), e);
            return "{\"events\":[],\"error\":\"" + escapeJson(e.getMessage()) + "\"}";
        }

        sb.append("]}");
        Log.i(TAG, "이벤트 " + totalCount + "건 조회 (제목 없는 " + skippedNoTitle + "건 스킵)");
        return sb.toString();
    }

    /**
     * JSON 내부 string 값에 들어갈 수 있는 특수문자 escape (최소한).
     */
    private static String escapeJson(String s) {
        if (s == null) return "";
        return s.replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\n", "\\n")
                .replace("\r", "\\r")
                .replace("\t", "\\t");
    }
}
