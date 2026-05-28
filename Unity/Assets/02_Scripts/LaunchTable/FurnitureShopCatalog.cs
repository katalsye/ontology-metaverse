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
        // TODO: DB 연동 후 구매 여부 설정
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
