using UnityEngine;

/// <summary>
/// Closet 씬 가구 선택 UI용 카탈로그.
/// 3D 모델/material 데이터는 FurnitureCarouselUI에서 관리.
/// </summary>
[CreateAssetMenu(fileName = "FurnitureShopCatalog", menuName = "Ontology/Furniture Shop Catalog")]
public class FurnitureShopCatalog : ScriptableObject
{
    [System.Serializable]
    public class ColorData
    {
        public Sprite thumbnail;
        // Inspector 기본값 — 실제 구매 여부는 FurnitureCarouselUI.FurnitureVariant.isPurchased
        // (purchasedFurniture Firestore 필드 기반)를 따름
        public bool   isPurchased;
    }

    [System.Serializable]
    public class VariantData
    {
        public string      displayName;
        public ColorData[] colors;

    }

    [System.Serializable]
    public class FurnitureData
    {
        public int          id;
        public string       displayName;
        public VariantData[] variants;
    }

    public FurnitureData[] furnitures;
}
