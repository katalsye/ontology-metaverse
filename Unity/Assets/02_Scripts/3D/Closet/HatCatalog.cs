using UnityEngine;

[CreateAssetMenu(fileName = "HatCatalog", menuName = "Ontology/Hat Catalog")]
public class HatCatalog : ScriptableObject
{
    [System.Serializable]
    public class HatData
    {
        public int    id;
        public string displayName;
        public Sprite thumbnail;
        // Inspector 기본값 — 런타임에는 HatSelectionUI.LoadPurchasesFromFirestore()가 덮어씀
        public bool   isPurchased;
    }

    public HatData[] hats;
}
