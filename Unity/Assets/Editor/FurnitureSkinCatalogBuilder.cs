using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Closet 씬의 FurnitureCarouselUI 데이터를 읽어 FurnitureSkinCatalog(에셋)를 자동 생성/갱신한다.
///
/// 자동 변환:
///   - model3D(씬 인스턴스)        → modelPrefab(원본 .prefab 에셋)  : PrefabUtility 역추적
///   - colorChildren.renderer(씬)  → rendererPath(model3D 루트 기준 상대경로 문자열)
///   - materials / materialIndex   → 그대로 복사 (material은 프로젝트 에셋이라 안전)
///
/// 실행: 메뉴 Tools → Ontology → Build Furniture Skin Catalog (Closet 씬을 연 상태에서)
/// </summary>
public static class FurnitureSkinCatalogBuilder
{
    const string AssetPath = "Assets/FurnitureSkinCatalog.asset";

    [MenuItem("Tools/Ontology/Build Furniture Skin Catalog")]
    public static void Build()
    {
        var ui = Object.FindObjectOfType<FurnitureCarouselUI>();
        if (ui == null)
        {
            EditorUtility.DisplayDialog("실패",
                "씬에서 FurnitureCarouselUI를 찾을 수 없습니다.\nCloset 씬을 열고 다시 시도하세요.", "확인");
            return;
        }
        if (ui.furnitures == null || ui.furnitures.Length == 0)
        {
            EditorUtility.DisplayDialog("실패", "FurnitureCarouselUI에 가구 데이터가 없습니다.", "확인");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<FurnitureSkinCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FurnitureSkinCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        var warnings = new StringBuilder();
        int fCount = ui.furnitures.Length;
        catalog.furnitures = new FurnitureSkinCatalog.Furniture[fCount];

        for (int i = 0; i < fCount; i++)
        {
            var src = ui.furnitures[i];
            var dstF = new FurnitureSkinCatalog.Furniture { displayName = src.displayName };

            if (src.variants != null)
            {
                dstF.variants = new FurnitureSkinCatalog.Variant[src.variants.Length];
                for (int v = 0; v < src.variants.Length; v++)
                {
                    var sv    = src.variants[v];
                    var model = sv.model;

                    // ── model3D(씬 인스턴스) → 원본 prefab 에셋 역추적 ──
                    GameObject modelPrefab = null;
                    Transform  modelRoot   = null;
                    if (model != null && model.model3D != null)
                    {
                        modelRoot   = model.model3D.transform;
                        modelPrefab = ResolveSourcePrefab(model.model3D);
                        if (modelPrefab == null)
                            warnings.AppendLine(
                                $"  - [{src.displayName}] variant {v}: model3D '{model.model3D.name}' 의 원본 prefab을 못 찾음 → 수동 연결 필요");
                    }
                    else
                    {
                        warnings.AppendLine($"  - [{src.displayName}] variant {v}: model3D 비어있음");
                    }

                    var dstV = new FurnitureSkinCatalog.Variant
                    {
                        displayName  = sv.displayName,
                        modelPrefab  = modelPrefab,
                        colorChildren = BuildColorChildren(model, modelRoot, src.displayName, v, warnings),
                    };
                    dstF.variants[v] = dstV;
                }
            }
            catalog.furnitures[i] = dstF;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = catalog;

        string msg = $"FurnitureSkinCatalog 생성/갱신 완료 ({fCount}개 가구)\n경로: {AssetPath}";
        if (warnings.Length > 0)
            msg += "\n\n⚠ 수동 확인 필요:\n" + warnings;
        EditorUtility.DisplayDialog("완료", msg, "확인");
        if (warnings.Length > 0) Debug.LogWarning("[FurnitureSkinCatalogBuilder]\n" + warnings);
    }

    // 씬 인스턴스 GameObject → 그 인스턴스가 유래한 원본 prefab 에셋
    static GameObject ResolveSourcePrefab(GameObject sceneInstance)
    {
        // prefab 인스턴스면 원본 에셋을 직접 반환
        var src = PrefabUtility.GetCorrespondingObjectFromSource(sceneInstance);
        if (src != null)
        {
            // 인스턴스의 "루트 prefab 에셋" 경로를 얻어 그 루트 에셋을 로드 (자식이 아닌 prefab 통째)
            string path = AssetDatabase.GetAssetPath(src);
            if (!string.IsNullOrEmpty(path))
            {
                var rootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (rootAsset != null) return rootAsset;
            }
            return src as GameObject;
        }
        return null; // prefab 인스턴스가 아님 (순수 씬 오브젝트) → 자동 변환 불가
    }

    static FurnitureSkinCatalog.ColorChild[] BuildColorChildren(
        FurnitureCarouselUI.FurnitureModel model, Transform modelRoot,
        string furnitureName, int variantIndex, StringBuilder warnings)
    {
        if (model == null || model.colorChildren == null)
            return new FurnitureSkinCatalog.ColorChild[0];

        var list = new List<FurnitureSkinCatalog.ColorChild>();
        for (int c = 0; c < model.colorChildren.Length; c++)
        {
            var cc = model.colorChildren[c];
            if (cc == null) { list.Add(new FurnitureSkinCatalog.ColorChild()); continue; }

            string path = "";
            if (cc.renderer != null)
            {
                if (modelRoot != null && (cc.renderer.transform == modelRoot || cc.renderer.transform.IsChildOf(modelRoot)))
                    path = GetRelativePath(cc.renderer.transform, modelRoot);
                else
                    warnings.AppendLine(
                        $"  - [{furnitureName}] variant {variantIndex} color {c}: renderer가 model3D 하위가 아님 → rendererPath 빈값");
            }

            list.Add(new FurnitureSkinCatalog.ColorChild
            {
                rendererPath  = path,
                materialIndex = cc.materialIndex,
                materials     = cc.materials != null ? (Material[])cc.materials.Clone() : new Material[0],
            });
        }
        return list.ToArray();
    }

    // root 기준 t까지의 "/" 구분 상대 경로 (root 자신이면 "")
    static string GetRelativePath(Transform t, Transform root)
    {
        if (t == root) return "";
        var stack = new List<string>();
        while (t != null && t != root)
        {
            stack.Add(t.name);
            t = t.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }
}
