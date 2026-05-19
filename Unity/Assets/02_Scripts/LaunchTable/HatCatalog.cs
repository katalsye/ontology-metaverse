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
    }

    public HatData[] hats;
}
