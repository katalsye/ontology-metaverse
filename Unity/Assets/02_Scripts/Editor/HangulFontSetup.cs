using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 한글 폰트(Pretendard)를 TMP_FontAsset(Static SDF)으로 변환하고
/// TMP_Settings의 Fallback에 등록한다.
///
/// 왜 Static인가:
///   Dynamic 모드는 IL2CPP + Stripping 환경에서 런타임 글리프 추가 시
///   TMP_FontAsset.TryAddCharacterInternal에서 NPE 발생 (FreeType native deps 누락).
///   Static 모드는 빌드 시점에 atlas를 미리 굽기 때문에 런타임 글리프 동적 추가 불필요.
///
/// 굽는 글리프:
///   - ASCII printable (U+0020 ~ U+007E)
///   - 한글 완성형 전체 (U+AC00 ~ U+D7A3, 11,172자)
///
/// Atlas 크기:
///   samplingPointSize=48, atlasPadding=4, multi-atlas 활성화.
///   11k 글리프가 4096x4096 한 장에 안 들어가면 Unity가 자동으로 atlas를 추가 생성.
///
/// 사용법: Unity 메뉴 "Tools/Setup Hangul Font (Pretendard)" 클릭.
/// </summary>
public static class HangulFontSetup
{
    private const string FontPath = "Assets/04_Fonts/Pretendard-Regular.ttf";
    private const string FontAssetPath = "Assets/04_Fonts/Pretendard-Regular SDF.asset";

    [MenuItem("Tools/Setup Hangul Font (Pretendard)")]
    public static void Setup()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[HangulFontSetup] {FontPath} 를 찾을 수 없습니다. ttf를 04_Fonts에 두고 다시 시도하세요.");
            return;
        }

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            RemoveFromFallback(existing);
            AssetDatabase.DeleteAsset(FontAssetPath);
            Debug.Log("[HangulFontSetup] 기존 Pretendard SDF asset 제거");
        }

        // 순서가 중요: Dynamic으로 생성해야 sourceFontFile reference가 유지되어
        // TryAddCharacters가 폰트를 찾을 수 있다. 굽기 끝난 후 Static으로 모드 변경.
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 48,
            atlasPadding: 4,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 4096,
            atlasHeight: 4096,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[HangulFontSetup] TMP_FontAsset 생성 실패");
            return;
        }

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        // Material 생성 후 sub-asset 등록. CreateFontAsset이 material을 자동 생성하지 않는 경우 대비.
        // 런타임 TMP_MaterialManager.GetFallbackMaterial이 fontAsset.material null이면 NPE 발생.
        EnsureMaterial(fontAsset);

        // Atlas texture sub-asset 등록 (이 시점에선 보통 빈 텍스처 1장)
        EnsureAtlasTexturesAsSubAssets(fontAsset);

        // ASCII + 한글 완성형 전체 미리 굽기
        List<uint> chars = new List<uint>();
        for (uint c = 0x0020; c <= 0x007E; c++) chars.Add(c);   // ASCII printable (95자)
        for (uint c = 0xAC00; c <= 0xD7A3; c++) chars.Add(c);   // 한글 완성형 (11,172자)

        Debug.Log($"[HangulFontSetup] 글리프 굽는 중 ({chars.Count}자)... 수십 초 걸릴 수 있습니다");
        bool ok = fontAsset.TryAddCharacters(chars.ToArray(), out uint[] missing, includeFontFeatures: false);

        // 굽기 중에 multi-atlas로 새 texture가 생성될 수 있으니 다시 sub-asset 등록
        EnsureAtlasTexturesAsSubAssets(fontAsset);

        if (missing != null && missing.Length > 0)
        {
            Debug.LogWarning($"[HangulFontSetup] 폰트에 없는 글리프 {missing.Length}자 (alternative ttf 한계로 추정)");
        }

        if (!ok)
        {
            Debug.LogWarning("[HangulFontSetup] TryAddCharacters가 false 반환 — 일부 글리프 누락 가능");
        }

        // 굽기 완료 후 Static으로 전환: 런타임에 동적 글리프 추가 시도 차단 (NPE 방지).
        // glyph/character table은 이미 채워진 상태로 유지되므로 빌드 후에도 한글 정상 표시.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[HangulFontSetup] 글리프 굽기 완료: characterTable {fontAsset.characterTable.Count}자, atlas {fontAsset.atlasTextures.Length}장");

        AddToFallback(fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int atlasCount = fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0;
        Debug.Log($"[HangulFontSetup] 완료 → {FontAssetPath}, atlas {atlasCount}장 생성, Fallback 등록 완료");

        Selection.activeObject = fontAsset;
        EditorGUIUtility.PingObject(fontAsset);
    }

    private static void AddToFallback(TMP_FontAsset fontAsset)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null)
        {
            Debug.LogError("[HangulFontSetup] TMP_Settings 인스턴스 없음. Window > TextMeshPro > Settings 열어서 생성 후 재시도.");
            return;
        }

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_fallbackFontAssets");
        if (prop == null)
        {
            List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null)
            {
                Debug.LogError("[HangulFontSetup] Fallback 리스트를 찾지 못했습니다. 수동 등록 필요.");
                return;
            }
            if (!fallbacks.Contains(fontAsset))
            {
                fallbacks.Add(fontAsset);
                EditorUtility.SetDirty(settings);
            }
            return;
        }

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
            {
                Debug.Log("[HangulFontSetup] 이미 Fallback에 등록되어 있음");
                return;
            }
        }

        prop.arraySize++;
        prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = fontAsset;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
    }

    /// <summary>
    /// fontAsset.material이 null이면 SDF shader로 새 Material을 만들고
    /// SerializedObject로 m_Material 셋팅 + sub-asset 등록.
    /// 이미 존재하면 sub-asset 등록만 보장.
    /// </summary>
    private static void EnsureMaterial(TMP_FontAsset fontAsset)
    {
        if (fontAsset.material == null)
        {
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader == null) shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (shader == null)
            {
                Debug.LogError("[HangulFontSetup] TMP SDF shader를 찾을 수 없음 — TMP 패키지 설치 확인 필요");
                return;
            }

            Material mat = new Material(shader);
            mat.name = "Pretendard-Regular SDF Material";

            // atlas texture 있으면 main texture로 연결
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                mat.mainTexture = fontAsset.atlasTextures[0];
            }

            // SerializedObject로 m_Material 셋팅 (public setter 없음)
            SerializedObject so = new SerializedObject(fontAsset);
            SerializedProperty matProp = so.FindProperty("m_Material");
            if (matProp != null)
            {
                matProp.objectReferenceValue = mat;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.AddObjectToAsset(mat, fontAsset);
            Debug.Log("[HangulFontSetup] Material sub-asset 생성 + 등록");
        }
        else if (!AssetDatabase.IsSubAsset(fontAsset.material))
        {
            fontAsset.material.name = "Pretendard-Regular SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            Debug.Log("[HangulFontSetup] 기존 Material sub-asset 등록");
        }
    }

    /// <summary>
    /// fontAsset.atlasTextures 의 모든 텍스처를 sub-asset으로 등록 (중복 방지).
    /// </summary>
    private static void EnsureAtlasTexturesAsSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.atlasTextures == null) return;

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            Texture2D atlas = fontAsset.atlasTextures[i];
            if (atlas == null) continue;
            if (AssetDatabase.IsSubAsset(atlas)) continue;
            if (AssetDatabase.Contains(atlas)) continue;

            atlas.name = $"Atlas Texture {i}";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }
    }

    private static void RemoveFromFallback(TMP_FontAsset fontAsset)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null) return;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_fallbackFontAssets");
        if (prop == null) return;

        for (int i = prop.arraySize - 1; i >= 0; i--)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
            {
                prop.DeleteArrayElementAtIndex(i);
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
    }
}
