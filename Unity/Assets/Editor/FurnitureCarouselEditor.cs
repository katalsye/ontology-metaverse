#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FurnitureCarouselUI))]
public class FurnitureCarouselEditor : Editor
{
    private int _previewFurnitureIndex = 0;
    private int _previewVariantIndex   = 0;
    private int _previewColorIndex     = 0;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        var ui = (FurnitureCarouselUI)target;
        if (ui.furnitures == null || ui.furnitures.Length == 0) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 미리보기 ──", EditorStyles.boldLabel);

        // 가구 선택
        string[] furnitureNames = new string[ui.furnitures.Length];
        for (int i = 0; i < ui.furnitures.Length; i++)
            furnitureNames[i] = string.IsNullOrEmpty(ui.furnitures[i].displayName) ? $"가구{i}" : ui.furnitures[i].displayName;
        _previewFurnitureIndex = Mathf.Clamp(_previewFurnitureIndex, 0, ui.furnitures.Length - 1);
        _previewFurnitureIndex = EditorGUILayout.Popup("가구", _previewFurnitureIndex, furnitureNames);

        var entry = ui.furnitures[_previewFurnitureIndex];
        if (entry.variants == null || entry.variants.Length == 0) return;

        // variant 선택
        string[] variantNames = new string[entry.variants.Length];
        for (int i = 0; i < entry.variants.Length; i++)
            variantNames[i] = string.IsNullOrEmpty(entry.variants[i].displayName) ? $"종류{i}" : entry.variants[i].displayName;
        _previewVariantIndex = Mathf.Clamp(_previewVariantIndex, 0, entry.variants.Length - 1);
        _previewVariantIndex = EditorGUILayout.Popup("종류 (variant)", _previewVariantIndex, variantNames);

        // 색상 인덱스 — colorChildren.materials[] 길이 기준
        var selVariant  = entry.variants[_previewVariantIndex];
        int maxColor    = GetMaxColorCount(selVariant);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"색상 번호 (최대 {Mathf.Max(0, maxColor - 1)})", GUILayout.Width(180));
        _previewColorIndex = EditorGUILayout.IntField(_previewColorIndex);
        if (_previewColorIndex < 0) _previewColorIndex = 0;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("적용", GUILayout.Height(28)))
            PreviewVariant(ui, _previewFurnitureIndex, _previewVariantIndex, _previewColorIndex);
    }

    int GetMaxColorCount(FurnitureCarouselUI.FurnitureVariant variant)
    {
        int max = 0;
        var fm = variant.model;
        if (fm?.colorChildren == null) return max;
        foreach (var cc in fm.colorChildren)
            if (cc?.materials != null && cc.materials.Length > max)
                max = cc.materials.Length;
        return max;
    }

    void PreviewVariant(FurnitureCarouselUI ui, int furnitureIndex, int variantIndex, int colorIndex)
    {
        var entry = ui.furnitures[furnitureIndex];

        // 모든 모델 비활성
        foreach (var e in ui.furnitures)
            if (e.variants != null)
                foreach (var v in e.variants)
                    if (v.model?.model3D != null)
                    {
                        Undo.RecordObject(v.model.model3D, "Preview Variant");
                        v.model.model3D.SetActive(false);
                    }

        var variant = entry.variants[variantIndex];
        var fm = variant.model;
        if (fm?.model3D == null) return;

        Undo.RecordObject(fm.model3D, "Preview Variant");
        fm.model3D.SetActive(true);

        if (fm.colorChildren == null) return;
        foreach (var cc in fm.colorChildren)
        {
            if (cc?.renderer == null || cc.materials == null || cc.materials.Length == 0) continue;
            if (colorIndex >= cc.materials.Length || cc.materials[colorIndex] == null) continue;
            int ci = colorIndex;
            Undo.RecordObject(cc.renderer, "Preview Variant");
            var mats = cc.renderer.sharedMaterials;
            int slot = cc.materialIndex;
            if (slot < mats.Length)
                mats[slot] = cc.materials[ci];
            cc.renderer.sharedMaterials = mats;
        }

        Debug.Log($"[FurnitureCarousel] 미리보기: {entry.displayName} - {variant.displayName} / 색상{colorIndex}");
    }
}
#endif
