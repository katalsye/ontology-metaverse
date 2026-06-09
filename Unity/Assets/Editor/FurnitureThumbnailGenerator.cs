#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FurnitureCarouselUI의 각 variant + 색상별 썸네일을 캡처해 PNG로 저장.
/// Closet 씬 열린 상태 + Play 모드에서 가구 탭 띄운 뒤 실행할 것.
/// Tools > Generate Furniture Thumbnails
/// </summary>
public class FurnitureThumbnailGenerator : EditorWindow
{
    const string OutputFolder = "Assets/03_Prefabs/Thumbnails/Furniture";
    const int    TexSize      = 256;

    private FurnitureCarouselUI _carousel;

    [MenuItem("Tools/Generate Furniture Thumbnails")]
    static void Open() => GetWindow<FurnitureThumbnailGenerator>("Furniture Thumbnails");

    void OnGUI()
    {
        EditorGUILayout.LabelField("가구 썸네일 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Closet 씬 Play\n2. 가구 탭 열어서 첫 가구가 잘 보이는 상태로\n3. 이 창에서 생성 버튼 클릭",
            MessageType.Info);
        EditorGUILayout.Space(6);

        _carousel = (FurnitureCarouselUI)EditorGUILayout.ObjectField(
            "FurnitureCarouselUI", _carousel, typeof(FurnitureCarouselUI), true);

        EditorGUILayout.Space(8);
        GUI.enabled = _carousel != null && Application.isPlaying;
        if (GUILayout.Button("썸네일 전체 생성", GUILayout.Height(32)))
            Generate();
        GUI.enabled = true;

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Play 모드에서만 실행 가능합니다.", MessageType.Warning);
    }

    void Generate()
    {
        if (_carousel.furnitures == null || _carousel.furnitures.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "가구 데이터가 없습니다.", "확인");
            return;
        }

        // ClosetOrbitCamera 사용 — 게임에서 보이는 구도 그대로
        var orbitCam = ClosetOrbitCamera.Instance;
        if (orbitCam == null)
        {
            EditorUtility.DisplayDialog("오류", "ClosetOrbitCamera를 찾을 수 없습니다.", "확인");
            return;
        }

        var cam = orbitCam.GetComponent<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("오류", "ClosetOrbitCamera에 Camera 컴포넌트가 없습니다.", "확인");
            return;
        }

        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
        }

        var rt = new RenderTexture(TexSize, TexSize, 16);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;

        int saved = 0;

        // 모든 모델 비활성
        foreach (var entry in _carousel.furnitures)
            if (entry.variants != null)
                foreach (var v in entry.variants)
                    if (v.model?.model3D != null)
                        v.model.model3D.SetActive(false);

        for (int fi = 0; fi < _carousel.furnitures.Length; fi++)
        {
            var entry = _carousel.furnitures[fi];
            if (entry.variants == null) continue;

            for (int vi = 0; vi < entry.variants.Length; vi++)
            {
                var variant = entry.variants[vi];
                var fm      = variant.model;
                if (fm?.model3D == null) continue;

                fm.model3D.SetActive(true);

                // ClosetOrbitCamera가 이 모델을 바라보도록
                orbitCam.ResetView();
                orbitCam.target = fm.model3D.transform;

                // 한 프레임 강제 업데이트
                orbitCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

                int colorCount   = GetColorCount(fm);
                int captureCount = Mathf.Max(1, colorCount);

                for (int ci = 0; ci < captureCount; ci++)
                {
                    ApplyColor(fm, ci);
                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, TexSize, TexSize), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    string fname   = $"{Sanitize(entry.displayName)}_{Sanitize(variant.displayName)}_color{ci}_thumb.png";
                    string outPath = $"{OutputFolder}/{fname}";
                    File.WriteAllBytes(outPath, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    saved++;
                }

                fm.model3D.SetActive(false);
            }
        }

        // 정리
        cam.targetTexture = prevTarget;
        rt.Release();
        Object.DestroyImmediate(rt);

        // Sprite 임포트
        AssetDatabase.Refresh();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { OutputFolder }))
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            var    importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType      = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("완료", $"썸네일 {saved}개 생성!\n{OutputFolder}", "확인");
        Debug.Log($"[FurnitureThumbnail] {saved}개 → {OutputFolder}");
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
