package com.ontology.metaverse.gallery;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentUris;
import android.database.Cursor;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Build;
import android.provider.MediaStore;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.IOException;
import java.io.InputStream;

/**
 * 갤러리 사진 EXIF 메타데이터 추출 헬퍼.
 *
 * 동작:
 *   MediaStore.Images에서 최근 N장의 사진을 시간 역순으로 가져온 후,
 *   각 사진의 EXIF에서 GPS 좌표, 촬영 시각을 추출하여 JSON 배열로 반환.
 *
 * 권한: READ_MEDIA_IMAGES (Android 13+) / READ_EXTERNAL_STORAGE (12-)
 *
 * Unity 호출:
 *   AndroidJavaClass cls = new AndroidJavaClass("com.ontology.metaverse.gallery.GalleryHelper");
 *   string json = cls.CallStatic&lt;string&gt;("getRecentPhotos", activity, 10);
 */
public class GalleryHelper {
    private static final String TAG = "GalleryHelper";

    /**
     * 최근 maxCount장의 사진을 EXIF 메타데이터와 함께 JSON 배열로 반환.
     * 형식: [{"image_path":"content://...","lat":35.88,"lng":128.60,"capture_time":"2026-06-06T12:34:56"}, ...]
     */
    public static String getRecentPhotos(Activity activity, int maxCount) {
        try {
            ContentResolver resolver = activity.getContentResolver();

            String[] projection = {
                    MediaStore.Images.Media._ID,
                    MediaStore.Images.Media.DATE_TAKEN
            };
            // Android 11+ MediaStore는 SQL injection 방지로 sortOrder의 LIMIT 키워드를 거부함.
            // LIMIT 빼고 cursor 루프에서 count 기준으로 직접 제한.
            String sortOrder = MediaStore.Images.Media.DATE_TAKEN + " DESC";

            Uri collection;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                collection = MediaStore.Images.Media.getContentUri(MediaStore.VOLUME_EXTERNAL);
            } else {
                collection = MediaStore.Images.Media.EXTERNAL_CONTENT_URI;
            }

            JSONArray arr = new JSONArray();

            try (Cursor cursor = resolver.query(collection, projection, null, null, sortOrder)) {
                if (cursor == null) {
                    Log.w(TAG, "MediaStore cursor null");
                    return "[]";
                }

                int idCol = cursor.getColumnIndexOrThrow(MediaStore.Images.Media._ID);
                int count = 0;

                while (cursor.moveToNext() && count < maxCount) {
                    long id = cursor.getLong(idCol);
                    Uri photoUri = ContentUris.withAppendedId(collection, id);

                    try (InputStream is = resolver.openInputStream(photoUri)) {
                        if (is == null) continue;

                        ExifInterface exif = new ExifInterface(is);

                        float[] latlng = new float[2];
                        boolean hasGps = exif.getLatLong(latlng);

                        String dateTimeOriginal = exif.getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL);
                        // ExifInterface는 "2026:06:06 12:34:56" 형식 — ISO 8601로 변환
                        String captureTime = convertExifDateToIso(dateTimeOriginal);

                        // GPS 없으면 그래도 timestamp/path는 저장 (foodType/placeType만 못 채움)
                        JSONObject row = new JSONObject();
                        row.put("image_path", photoUri.toString());
                        row.put("lat", hasGps ? (double) latlng[0] : 0.0);
                        row.put("lng", hasGps ? (double) latlng[1] : 0.0);
                        row.put("capture_time", captureTime != null ? captureTime : "");
                        arr.put(row);
                        count++;
                    } catch (Throwable t) {
                        Log.w(TAG, "사진 1장 EXIF 추출 실패 (uri=" + photoUri + "): " + t.getMessage());
                        // 한 장 실패는 무시하고 다음 진행
                    }
                }
                Log.i(TAG, "getRecentPhotos: " + count + "장 수집");
            }

            return arr.toString();
        } catch (Throwable t) {
            Log.e(TAG, "getRecentPhotos 실패: " + t.getMessage(), t);
            return "[]";
        }
    }

    /**
     * EXIF "2026:06:06 12:34:56" → ISO 8601 "2026-06-06T12:34:56"
     */
    private static String convertExifDateToIso(String exifDate) {
        if (exifDate == null || exifDate.length() < 19) return null;
        try {
            String date = exifDate.substring(0, 10).replace(':', '-');
            String time = exifDate.substring(11);
            return date + "T" + time;
        } catch (Throwable t) {
            return null;
        }
    }
}
