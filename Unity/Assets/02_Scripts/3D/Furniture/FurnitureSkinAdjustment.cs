using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 가구에 붙여서 skinId에 따라 프리팹을 교체.
///
/// - 에디터: ContextMenu로 프리팹 연결 교체 (Prefab 칸 변경)
/// - 런타임: Instantiate로 교체 (백엔드 로드 시)
/// </summary>
public class FurnitureSkinAdjustment : MonoBehaviour
{
    [System.Serializable]
    public class SkinEntry
    {
        public int        skinId;
        public GameObject prefab;
    }

    public SkinEntry[] skins;
    public int         currentSkinId;

    void Start() => ApplySkin(currentSkinId);

    /// <summary>
    /// 런타임에서 호출. 현재 오브젝트를 skinId에 맞는 프리팹으로 교체.
    /// 위치·회전·스케일·부모 유지.
    /// </summary>
    public GameObject ApplySkin(int skinId)
    {
        var entry = FindEntry(skinId);
        if (entry == null || entry.prefab == null) return null;

        var inst = Instantiate(entry.prefab, transform.parent);
        inst.transform.localPosition = transform.localPosition;
        inst.transform.localRotation = transform.localRotation;
        inst.transform.localScale    = transform.localScale;
        inst.name = entry.prefab.name;

        Destroy(gameObject);
        return inst;
    }

    SkinEntry FindEntry(int id)
    {
        if (skins == null) return null;
        foreach (var s in skins)
            if (s.skinId == id) return s;
        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Skin (프리팹 교체)")]
    void ApplySkinInEditor()
    {
        var entry = FindEntry(currentSkinId);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[FurnitureSkinAdjustment] skinId {currentSkinId}에 프리팹 없음");
            return;
        }

        var root = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        if (root != null)
        {
            var settings = new PrefabReplacingSettings();
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                root, entry.prefab, settings, InteractionMode.UserAction);
        }
    }
#endif
}
