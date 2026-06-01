using UnityEngine;

/// <summary>
/// 추가 가능한 가구 목록을 담는 ScriptableObject.
/// Assets 우클릭 → Create → Ontology/Furniture Catalog 으로 생성.
/// </summary>
[CreateAssetMenu(fileName = "FurnitureCatalog", menuName = "Ontology/Furniture Catalog")]
public class FurnitureCatalog : ScriptableObject
{
    [System.Serializable]
    public class FurnitureData
    {
        public int        id;
        public string     displayName;
        public Sprite     thumbnail;
        public GameObject prefab;      // 씬에 배치할 프리팹
        public bool       isCeiling;   // true → ceilingItemParent 아래 배치
    }

    public FurnitureData[] furnitures;
}
