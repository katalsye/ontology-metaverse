#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class FurnitureShopCatalogBuilder : EditorWindow
{
    private FurnitureCarouselUI  _carousel;
    private FurnitureShopCatalog _catalog;

    [MenuItem("Ontology/Furniture Shop Catalog Builder")]
    static void Open() => GetWindow<FurnitureShopCatalogBuilder>("Catalog Builder");

    void OnGUI()
    {
        EditorGUILayout.LabelField("FurnitureCarouselUI → FurnitureShopCatalog", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _carousel = (FurnitureCarouselUI)EditorGUILayout.ObjectField(
            "FurnitureCarouselUI", _carousel, typeof(FurnitureCarouselUI), true);
        _catalog  = (FurnitureShopCatalog)EditorGUILayout.ObjectField(
            "FurnitureShopCatalog", _catalog, typeof(FurnitureShopCatalog), false);

        EditorGUILayout.Space(8);

        GUI.enabled = _carousel != null && _catalog != null;
        if (GUILayout.Button("CarouselUI → Catalog 동기화", GUILayout.Height(32)))
            Sync();
        GUI.enabled = true;

        if (_catalog == null)
            EditorGUILayout.HelpBox("Assets > Create > Ontology > Furniture Shop Catalog 으로 asset 먼저 생성하세요.", MessageType.Info);
    }

    void Sync()
    {
        if (_carousel.furnitures == null || _carousel.furnitures.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "CarouselUI에 가구 데이터가 없습니다.", "확인");
            return;
        }

        var furnitures = new FurnitureShopCatalog.FurnitureData[_carousel.furnitures.Length];

        for (int fi = 0; fi < _carousel.furnitures.Length; fi++)
        {
            var entry    = _carousel.furnitures[fi];
            var variants = entry.variants ?? new FurnitureCarouselUI.FurnitureVariant[0];

            var variantList = new FurnitureShopCatalog.VariantData[variants.Length];
            for (int vi = 0; vi < variants.Length; vi++)
            {
                var v      = variants[vi];
                var thumbs = v.colorThumbnails ?? new Sprite[0];

                var colorList = new FurnitureShopCatalog.ColorData[thumbs.Length];
                for (int ci = 0; ci < thumbs.Length; ci++)
                    colorList[ci] = new FurnitureShopCatalog.ColorData
                    {
                        thumbnail   = thumbs[ci],
                        isPurchased = v.isPurchased,
                    };

                variantList[vi] = new FurnitureShopCatalog.VariantData
                {
                    displayName = v.displayName,
                    colors      = colorList,
                };
            }

            furnitures[fi] = new FurnitureShopCatalog.FurnitureData
            {
                id          = fi,
                displayName = entry.displayName,
                variants    = variantList,
            };
        }

        Undo.RecordObject(_catalog, "Sync FurnitureShopCatalog");
        _catalog.furnitures = furnitures;
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CatalogBuilder] {furnitures.Length}개 가구 동기화 완료");
    }
}
#endif
