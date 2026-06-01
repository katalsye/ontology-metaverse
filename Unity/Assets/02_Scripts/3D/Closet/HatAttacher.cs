using UnityEngine;

public class HatAttacher : MonoBehaviour
{
    public static HatAttacher Instance { get; private set; }

    public SkinnedMeshRenderer characterRenderer;

    [System.Serializable]
    public class AccessoryEntry
    {
        public GameObject accessory;
        public string     boneName        = "head";
        public Vector3    positionOffset  = Vector3.zero;
        public Vector3    rotationOffset  = Vector3.zero;
        public Vector3    scaleMultiplier = Vector3.one;
    }

    public AccessoryEntry[] accessories;
    public int activeIndex = -1;

    Transform[] _bones;

    void Awake() => Instance = this;

    void Start()
    {
        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (characterRenderer == null)
            characterRenderer = FindFirstObjectByType<SkinnedMeshRenderer>();
        if (characterRenderer == null) return;

        _bones = new Transform[accessories.Length];
        for (int i = 0; i < accessories.Length; i++)
            _bones[i] = FindBone(accessories[i].boneName);

        Equip(activeIndex);
    }

    void LateUpdate()
    {
        if (_bones == null || activeIndex < 0 || activeIndex >= accessories.Length) return;

        var e    = accessories[activeIndex];
        var bone = _bones[activeIndex];
        if (e.accessory == null || bone == null) return;

        var t = e.accessory.transform;
        t.position = bone.position + bone.TransformDirection(e.positionOffset);
        t.rotation = bone.rotation * Quaternion.Euler(e.rotationOffset);
        Vector3 ps = bone.lossyScale;
        t.localScale = new Vector3(
            e.scaleMultiplier.x / ps.x,
            e.scaleMultiplier.y / ps.y,
            e.scaleMultiplier.z / ps.z);
    }

    public void Equip(int index)
    {
        activeIndex = index;
        for (int i = 0; i < accessories.Length; i++)
            if (accessories[i].accessory != null)
                accessories[i].accessory.SetActive(i == index);
    }

    Transform FindBone(string name)
    {
        foreach (var bone in characterRenderer.bones)
            if (bone != null && bone.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return bone;
        return null;
    }
}
