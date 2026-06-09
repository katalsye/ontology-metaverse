#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FurnitureShopCatalog))]
public class FurnitureShopCatalogEditor : Editor
{
    const string OutputFolder = "Assets/03_Prefabs/Thumbnails/Furniture";
    const int    TexSize      = 512;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 썸네일 생성 ──", EditorStyles.boldLabel);

        var carousel = FindObjectOfType<FurnitureCarouselUI>();
        var orbitCam = FindObjectOfType<ClosetOrbitCamera>();

        if (carousel == null)
            EditorGUILayout.HelpBox("씬에 FurnitureCarouselUI가 없습니다.", MessageType.Warning);
        if (orbitCam == null)
            EditorGUILayout.HelpBox("씬에 ClosetOrbitCamera가 없습니다.", MessageType.Warning);

        GUI.enabled = carousel != null && orbitCam != null && Application.isPlaying;
        if (GUILayout.Button("썸네일 생성 (Play 중에만 가능)", GUILayout.Height(30)))
            GenerateThumbnails(carousel, orbitCam);
        GUI.enabled = true;

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Closet 씬 Play 후 가구 탭 띄운 상태에서 클릭하세요.", MessageType.Info);
    }

    void GenerateThumbnails(FurnitureCarouselUI carousel, ClosetOrbitCamera orbitCam)
    {
        var catalog = (FurnitureShopCatalog)target;
        var cam     = orbitCam.GetComponent<Camera>();
        if (cam == null) return;

        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
        }

        int w  = cam.pixelWidth  > 0 ? cam.pixelWidth  : TexSize;
        int h  = cam.pixelHeight > 0 ? cam.pixelHeight : TexSize;
        var rt = new RenderTexture(w, h, 16);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;

        // 모든 모델 비활성
        foreach (var entry in carousel.furnitures)
            if (entry.variants != null)
                foreach (var v in entry.variants)
                    if (v.model?.model3D != null)
                        v.model.model3D.SetActive(false);

        for (int fi = 0; fi < carousel.furnitures.Length; fi++)
        {
            var entry = carousel.furnitures[fi];
            if (entry.variants == null) continue;

            // catalog 범위 확인
            if (fi >= catalog.furnitures.Length) continue;

            for (int vi = 0; vi < entry.variants.Length; vi++)
            {
                var variant = entry.variants[vi];
                var fm      = variant.model;
                if (fm?.model3D == null) continue;

                fm.model3D.SetActive(true);
                orbitCam.ResetView();
                orbitCam.target = fm.model3D.transform;
                orbitCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

                int colorCount = GetColorCount(fm);
                int captureCount = Mathf.Max(1, colorCount);

                // catalog variant/color 배열 크기 맞추기 + 각 요소 초기화
                if (vi >= catalog.furnitures[fi].variants.Length) continue;
                var catVariant = catalog.furnitures[fi].variants[vi];
                catVariant.colors = new FurnitureShopCatalog.ColorData[captureCount];
                for (int k = 0; k < captureCount; k++)
                    catVariant.colors[k] = new FurnitureShopCatalog.ColorData();

                for (int ci = 0; ci < captureCount; ci++)
                {
                    ApplyColor(fm, ci);
                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    string fname   = $"{Sanitize(entry.displayName)}_{Sanitize(variant.displayName)}_color{ci}_thumb.png";
                    string outPath = $"{OutputFolder}/{fname}";
                    File.WriteAllBytes(outPath, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);

                    // sprite 임포트 후 catalog에 바로 연결
                    AssetDatabase.ImportAsset(outPath);
                    var importer = AssetImporter.GetAtPath(outPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType      = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                    }
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
                    catVariant.colors[ci].thumbnail = sprite;
                }

                fm.model3D.SetActive(false);
            }
        }

        cam.targetTexture = prevTarget;
        rt.Release();
        Object.DestroyImmediate(rt);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[FurnitureThumbnail] 썸네일 생성 + catalog 연결 완료");
        EditorUtility.DisplayDialog("완료", "썸네일 생성 + catalog 자동 연결 완료!", "확인");
    }

    static int GetColorCount(FurnitureCarouselUI.FurnitureModel fm)
    {
        int max = 0;
        if (fm.colorChildren == null) return max;
        foreach (var cc in fm.colorChildren)
            if (cc?.materials != null && cc.materials.Length > max)
                max = cc.materials.Length;
        return max;
    }

    static void ApplyColor(FurnitureCarouselUI.FurnitureModel fm, int colorIndex)
    {
        if (fm.colorChildren == null) return;
        foreach (var cc in fm.colorChildren)
        {
            if (cc?.renderer == null || cc.materials == null || cc.materials.Length == 0) continue;
            int ci = Mathf.Clamp(colorIndex, 0, cc.materials.Length - 1);
            if (cc.materials[ci] == null) continue;
            var mats = cc.renderer.sharedMaterials;
            int slot = cc.materialIndex;
            if (slot < mats.Length) mats[slot] = cc.materials[ci];
            cc.renderer.sharedMaterials = mats;
        }
    }

    static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
#endif
