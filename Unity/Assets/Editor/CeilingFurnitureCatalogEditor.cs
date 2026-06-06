#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// FurnitureCatalog Inspector에 "썸네일 자동 생성" 버튼 추가.
/// 프리팹 → AssetPreview → PNG 저장 → Sprite 임포트 → thumbnail 자동 연결.
/// </summary>
[CustomEditor(typeof(FurnitureCatalog))]
public class FurnitureCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("썸네일 자동 생성", GUILayout.Height(32)))
            GenerateThumbnails((FurnitureCatalog)target);
    }

    static void GenerateThumbnails(FurnitureCatalog catalog)
    {
        if (catalog.furnitures == null || catalog.furnitures.Length == 0)
        {
            Debug.LogWarning("[FurnitureCatalog] furnitures 배열이 비어 있습니다.");
            return;
        }

        string saveDir = "Assets/03_Prefabs/Thumbnails";
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
            AssetDatabase.Refresh();
        }

        AssetPreview.SetPreviewTextureCacheSize(64);

        int generated = 0;
        for (int i = 0; i < catalog.furnitures.Length; i++)
        {
            var item = catalog.furnitures[i];
            if (item == null || item.prefab == null)
            {
                Debug.LogWarning($"[FurnitureCatalog] [{i}] prefab이 없어 건너뜀");
                continue;
            }

            // AssetPreview 요청 — 최대 30회 재시도
            Texture2D preview = null;
            for (int retry = 0; retry < 30 && preview == null; retry++)
            {
                preview = AssetPreview.GetAssetPreview(item.prefab);
                if (preview == null) System.Threading.Thread.Sleep(100);
            }

            if (preview == null)
            {
                Debug.LogWarning($"[FurnitureCatalog] 썸네일 생성 실패: {item.prefab.name}");
                continue;
            }

            // 읽기 가능한 복사본
            var rt = RenderTexture.GetTemporary(preview.width, preview.height, 0);
            var prevRT = RenderTexture.active;
            Graphics.Blit(preview, rt);
            RenderTexture.active = rt;
            var readableTex = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableTex.Apply();
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);

            // PNG 저장
            string pngPath = $"{saveDir}/{item.prefab.name}_thumb.png";
            File.WriteAllBytes(pngPath, readableTex.EncodeToPNG());
            DestroyImmediate(readableTex);
            AssetDatabase.ImportAsset(pngPath);

            // Sprite 타입으로 설정
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite != null)
            {
                item.thumbnail = sprite;
                generated++;
                Debug.Log($"[FurnitureCatalog] 완료: {item.prefab.name} → {pngPath}");
            }
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "썸네일 자동 생성 완료",
            $"{generated} / {catalog.furnitures.Length} 개 생성\n저장 위치: {saveDir}",
            "확인");
    }
}
#endif
