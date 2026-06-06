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
        // TODO: DB 연동 후 서버에서 받아온 구매 목록으로 설정할 것
        public bool   isPurchased;
    }

    public HatData[] hats;
}
