using UnityEngine;

public class HatAttacher : MonoBehaviour
{
    [Tooltip("chibi SkinnedMeshRenderer가 있는 오브젝트")]
    public SkinnedMeshRenderer characterRenderer;

    [Tooltip("붙일 모자 오브젝트")]
    public GameObject hat;

    [Tooltip("찾을 본 이름 (일부만 입력해도 됨, 대소문자 무시)")]
    public string boneName = "head";

    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;
    public Vector3 scaleMultiplier = Vector3.one;

    void Start()
    {
        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (characterRenderer == null)
            characterRenderer = FindFirstObjectByType<SkinnedMeshRenderer>();

        if (hat == null || characterRenderer == null) return;

        Transform headBone = FindBone(boneName);
        if (headBone == null)
        {
            Debug.LogWarning($"[HatAttacher] '{boneName}' 본을 찾지 못했습니다.");
            return;
        }

        hat.transform.SetParent(headBone, false);
        hat.transform.localPosition    = positionOffset;
        hat.transform.localEulerAngles = rotationOffset;

        // 부모 bone의 world scale을 상쇄해서 실제 크기가 scaleMultiplier가 되도록 보정
        Vector3 ps = headBone.lossyScale;
        hat.transform.localScale = new Vector3(
            scaleMultiplier.x / ps.x,
            scaleMultiplier.y / ps.y,
            scaleMultiplier.z / ps.z);
    }

    Transform FindBone(string name)
    {
        foreach (var bone in characterRenderer.bones)
            if (bone != null && bone.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return bone;
        return null;
    }
}
