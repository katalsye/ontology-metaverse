using System.IO;
using UnityEditor;
using UnityEngine;

public class HatThumbnailGenerator
{
    const string HatFolder    = "Assets/04_Models/AddOns/Mg3D_Hats";
    const string OutputFolder = "Assets/04_Models/AddOns/Mg3D_Hats/Thumbnails";
    const string CatalogPath  = "Assets/04_Models/AddOns/Mg3D_Hats/HatCatalog.asset";

    static readonly (string fbx, string displayName)[] HatList =
    {
        ("CowboyHat",    "카우보이"),
        ("MagicianHat",  "마법사"),
        ("MinerHat",     "광부"),
        ("PajamaHat",    "잠옷"),
        ("PillboxHat",   "필박스"),
        ("ShowerCap",    "샤워캡"),
        ("Sombrero",     "솜브레로"),
        ("Crown",        "왕관"),
        ("Mustache",     "콧수염"),
        ("VikingHelmet", "바이킹"),
    };

    [MenuItem("Tools/Generate Hat Thumbnails")]
    static void Generate()
    {
        if (!Directory.Exists(OutputFolder))
            AssetDatabase.CreateFolder("Assets/04_Models/AddOns/Mg3D_Hats", "Thumbnails");

        int saved = 0;
        foreach (var (fbx, _) in HatList)
        {
            string path  = $"{HatFolder}/{fbx}.fbx";
            var    asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning($"[HatThumbnail] 없음: {path}"); continue; }

            Texture2D preview = null;
            for (int attempt = 0; attempt < 10 && preview == null; attempt++)
            {
                preview = AssetPreview.GetAssetPreview(asset);
                if (preview == null) System.Threading.Thread.Sleep(100);
            }
            if (preview == null) { Debug.LogWarning($"[HatThumbnail] 미리보기 없음: {fbx}"); continue; }

            string outPath = $"{OutputFolder}/{fbx}_thumb.png";
            var readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
            Graphics.CopyTexture(preview, readable);
            File.WriteAllBytes(outPath, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            saved++;
        }

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

        CreateCatalog();
        Debug.Log($"[HatThumbnail] {saved}개 썸네일 + HatCatalog 생성 완료");
        EditorUtility.DisplayDialog("완료", $"썸네일 {saved}개 + HatCatalog 생성 완료!", "확인");
    }

    static void CreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<HatCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<HatCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.hats = new HatCatalog.HatData[HatList.Length];
        for (int i = 0; i < HatList.Length; i++)
        {
            var (fbx, displayName) = HatList[i];
            string thumbPath = $"{OutputFolder}/{fbx}_thumb.png";
            var    sprite    = AssetDatabase.LoadAssetAtPath<Sprite>(thumbPath);

            catalog.hats[i] = new HatCatalog.HatData
            {
                id          = i,
                displayName = displayName,
                thumbnail   = sprite,
            };
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
