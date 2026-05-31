using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 씬에 배치된 실제 가구에 "모델 variant 교체 + 색상 적용"을 수행한다.
///
/// 데이터 소스:
///   - FurnitureSkinCatalog (에셋) : 모델 prefab + 색상 material 규칙 (Closet과 공유)
///
/// 매핑(이름 의존 없음, Inspector 직접 연결):
///   - entries[] 각 줄 = [방 가구 GameObject] + [catalog 가구ID] + [모델번호] + [색상번호]
///   - 같은 가구 GameObject가 catalog의 몇 번 가구인지, 어떤 모델/색상을 쓸지를 Inspector에서 지정.
///
/// 모델 교체(비파괴):
///   - 가구 GameObject(target) 자체가 기본(variant 0) 모델.
///   - variant != 0 선택 시 → target 본체 메시 끄고, 선택 모델 prefab을 target 자식으로 Instantiate.
///   - target GameObject는 유지되므로 다른 스크립트의 Inspector 참조·콜라이더가 안 깨진다.
///   - 런타임 자식 추가라 에디터 프리팹 hierarchy를 건드리지 않는다.
///
/// 색상 적용:
///   - catalog의 colorChildren(rendererPath + materialIndex + materials[])을 사용.
///   - 현재 표시 중인 루트(클론 또는 본체)에서 rendererPath로 Renderer를 찾아 material 교체.
///
/// 적용 시점:
///   - 지금은 방 진입(Start) 시 Inspector 값을 그대로 적용 (저장/불러오기 자리는 주석).
///   - 추후 DB 불러오기로 modelIndex/colorIndex를 덮어쓰면 됨. 불러오기 실패 시 Inspector 값 사용.
/// </summary>
public class RoomFurnitureCustomizer : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("방에 배치된 실제 가구 GameObject (이 자체가 기본 모델)")]
        public GameObject target;

        [Tooltip("FurnitureSkinCatalog.furnitures 의 인덱스 (이 가구가 카탈로그의 몇 번인지)")]
        public int furnitureId;

        [Tooltip("적용할 모델 variant 번호 (0 = 기본/본체, 1.. = 교체 모델). 모델이 하나뿐이면 0)")]
        public int modelIndex;

        [Tooltip("적용할 색상 번호 (colorChildren.materials 의 인덱스)")]
        public int colorIndex;
    }

    [Header("데이터 (모델/색상 카탈로그 — Closet과 공유 에셋)")]
    public FurnitureSkinCatalog catalog;

    [Header("상점템 prefab 목록 (불러오기 재소환용)")]
    [Tooltip("AddFurnitureSelectionUI가 쓰는 것과 동일한 FurnitureCatalog.\n" +
             "불러오기 시 저장된 상점템을 prefab으로 재소환하기 위해 참조.")]
    public FurnitureCatalog shopCatalog;

    [Header("가구별 적용 설정 (Inspector에서 직접 연결)")]
    public Entry[] entries;

    // 런타임에 생성한 모델 클론 식별용 이름
    const string SpawnName = "__ModelOverride";

    // target별 본체 메시 렌더러 캐시 (variant 0 복귀 시 다시 켜기 위함)
    readonly Dictionary<GameObject, Renderer[]> _bodyRenderers = new();

    void Start()
    {
        // ── DB 불러오기 자리 (추후 구현) ──────────────────────────
        // TODO: 서버/로컬에서 가구별 (modelIndex, colorIndex)를 불러와 entries 값을 덮어쓴다.
        //   var saved = RoomDataManager.Instance?.LoadFurnitureSkins();
        //   if (saved != null) { foreach(e in entries) e.modelIndex/colorIndex = saved[...]; }
        // 불러오기 실패(또는 미구현) 시: 아래처럼 Inspector에 입력된 값을 그대로 적용한다.

        ApplyAll();
    }

    // ── 공개 적용 함수 ─────────────────────────────────────────

    /// <summary>entries 전체를 Inspector 값대로 적용.</summary>
    public void ApplyAll()
    {
        if (entries == null) return;
        foreach (var e in entries)
            ApplyEntry(e);
    }

    /// <summary>한 가구에 모델+색상 적용.</summary>
    public void ApplyEntry(Entry e)
    {
        if (e == null || e.target == null) return;
        ApplyModel(e.target, e.furnitureId, e.modelIndex);
        ApplyColor(e.target, e.furnitureId, e.modelIndex, e.colorIndex);
    }

    // ── 모델 교체 ──────────────────────────────────────────────

    public void ApplyModel(GameObject target, int furnitureId, int variantIndex)
    {
        if (target == null) return;

        CacheBodyRenderers(target);

        // 기존 클론 제거
        var existing = target.transform.Find(SpawnName);
        if (existing != null) DestroySafe(existing.gameObject);

        // variant 0 또는 데이터 없음 → 본체 모델로 복귀 (본체 메시 다시 켜기)
        var variant = catalog != null ? catalog.GetVariant(furnitureId, variantIndex) : null;
        GameObject prefab = variant != null ? variant.modelPrefab : null;

        if (variantIndex == 0 || prefab == null)
        {
            SetBodyRenderers(target, true);
            return;
        }

        // 본체 메시 끄고, 선택 모델 prefab을 자식으로 생성
        SetBodyRenderers(target, false);

        var clone = Instantiate(prefab, target.transform);
        clone.name = SpawnName;
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale    = Vector3.one;
        clone.SetActive(true);
    }

    // ── 색상 적용 ──────────────────────────────────────────────

    public void ApplyColor(GameObject target, int furnitureId, int variantIndex, int colorIndex)
    {
        if (target == null || catalog == null) return;

        var variant = catalog.GetVariant(furnitureId, variantIndex);
        if (variant == null || variant.colorChildren == null) return;

        // 적용 루트: 클론이 있으면 클론, 없으면 본체(target)
        Transform spawn     = target.transform.Find(SpawnName);
        Transform applyRoot = spawn != null ? spawn : target.transform;

        foreach (var cc in variant.colorChildren)
        {
            if (cc == null || cc.materials == null) continue;
            if (colorIndex < 0 || colorIndex >= cc.materials.Length) continue;
            if (cc.materials[colorIndex] == null) continue;

            Renderer r = FindRendererByPath(applyRoot, cc.rendererPath);
            if (r == null) continue;

            var mats = r.sharedMaterials;
            if (cc.materialIndex >= 0 && cc.materialIndex < mats.Length)
            {
                mats[cc.materialIndex] = cc.materials[colorIndex];
                r.sharedMaterials = mats;
            }
        }
    }

    // ── 상점템 불러오기 (재소환 + 위치/회전 + 색상) ─────────────
    //
    // 기본가구(0~6)와 달리 상점템(Door 이후 인덱스)은 씬에 미리 없다.
    // 저장된 방을 불러올 때, 저장 데이터에 상점템이 있으면:
    //   1) shopCatalog의 prefab으로 재소환(Instantiate)
    //   2) 저장된 position/rotation 세팅
    //   3) 저장된 colorId로 색상 적용 (catalog의 색상 데이터 사용)
    //
    // ※ 소환(처음 구매) 시점에는 기본 색상으로 뜨고, 색상은 Closet에서 변경 → 저장됨.
    //   그 저장값을 다시 입힐 때가 바로 이 함수.

    /// <summary>
    /// 불러오기로 받은 상점템 1개의 저장 데이터.
    /// DB/서버에서 이 형태로 받았다고 가정한다. (프론트는 이 구조만 채워서 넘기면 됨)
    /// </summary>
    [System.Serializable]
    public struct SavedShopItem
    {
        public int     furnitureId;    // 카탈로그 인덱스 (FurnitureCatalog·SkinCatalog 동일 정렬)
        public int     colorId;        // 적용할 색상 번호
        public Vector3 position;       // 월드 위치
        public Vector3 eulerRotation;  // 월드 회전(Euler)
        public Vector3 scale;          // 로컬 스케일 (0,0,0 이면 prefab 원본 스케일 유지)
    }

    /// <summary>
    /// 저장된 상점템 1개를 재소환한다. (불러오기 전용, 완성형)
    /// 1) shopCatalog.prefab 으로 Instantiate
    /// 2) 위치/회전/스케일 세팅
    /// 3) isCeiling 여부에 따라 부모 컨테이너 배치 (천장 / 일반)
    /// 4) 콜라이더 보장 + FurnitureEditController.editableItems 등록 (EditMode 편집 가능)
    /// 5) 저장된 colorId 색상 적용
    /// 반환: 생성된 GameObject (실패 시 null)
    /// </summary>
    public GameObject SpawnSavedShopItem(SavedShopItem s)
    {
        if (shopCatalog == null || shopCatalog.furnitures == null) return null;
        if (s.furnitureId < 0 || s.furnitureId >= shopCatalog.furnitures.Length) return null;

        var item = shopCatalog.furnitures[s.furnitureId];
        if (item == null || item.prefab == null) return null;

        var ctrl = FurnitureEditController.Instance;

        // ── 1) 재소환 + 2) transform ──
        var instance = Instantiate(item.prefab, s.position, Quaternion.Euler(s.eulerRotation));
        if (s.scale != Vector3.zero)
            instance.transform.localScale = s.scale;

        // ── 3) 부모 컨테이너 배치 ──
        //   isCeiling → ceilingItemParent, 그 외 → furnitureParent
        Transform parent = null;
        if (ctrl != null)
            parent = item.isCeiling ? ctrl.ceilingItemParent : ctrl.furnitureParent;
        if (parent != null)
            instance.transform.SetParent(parent, worldPositionStays: true);

        // ── 4) 편집 시스템 합류 (콜라이더 + editableItems 등록) ──
        if (ctrl != null)
        {
            ctrl.EnsureColliderPublic(instance);

            var list = new List<FurnitureEditConfig>(ctrl.editableItems ?? new FurnitureEditConfig[0]);
            list.Add(new FurnitureEditConfig
            {
                target         = instance,
                canMove        = true,
                canDesign      = true,
                ceilingMounted = item.isCeiling,   // 천장템은 천장 드래그 로직 사용
                isAdded        = true,             // 추가가구 → 삭제 버튼 대상
            });
            ctrl.editableItems = list.ToArray();
        }

        // ── 5) 색상 적용 (상점템은 모델 1개 → variant 0 기준 색상만) ──
        ApplyColor(instance, s.furnitureId, 0, s.colorId);

        return instance;
    }

    /// <summary>
    /// 불러오기 일괄 처리 (완성형). 저장된 상점템 목록을 받아 전부 재소환한다.
    /// 호출부(DB/서버 로드)는 추후 연결 — 데이터를 받았다고 가정하고 이 함수에 넘기면 된다.
    /// </summary>
    public void LoadSavedShopItems(IList<SavedShopItem> saved)
    {
        // 불러오기 실패(saved == null)면 상점템은 생성하지 않는다.
        //   (원래 방에 없던 것이므로 — 기본가구는 Start()의 ApplyAll()로 이미 처리됨)
        if (saved == null) return;

        foreach (var s in saved)
            SpawnSavedShopItem(s);

        // ── DB 연동 자리 (추후) ──────────────────────────────────
        // 실제로는 RoomDataManager 등이 서버에서 List<SavedShopItem>을 만들어
        // 이 함수를 호출하게 된다. 예:
        //   var saved = RoomDataManager.Instance?.LoadShopItems();
        //   customizer.LoadSavedShopItems(saved);
    }

    // ── 내부 유틸 ──────────────────────────────────────────────

    void CacheBodyRenderers(GameObject target)
    {
        if (_bodyRenderers.ContainsKey(target)) return;

        Transform spawn = target.transform.Find(SpawnName);
        var all  = target.GetComponentsInChildren<Renderer>(true);
        var body = new List<Renderer>();
        foreach (var r in all)
        {
            if (spawn != null && r.transform.IsChildOf(spawn)) continue; // 클론 하위 제외
            body.Add(r);
        }
        _bodyRenderers[target] = body.ToArray();
    }

    void SetBodyRenderers(GameObject target, bool enabled)
    {
        if (!_bodyRenderers.TryGetValue(target, out var rends)) return;
        foreach (var r in rends)
            if (r != null) r.enabled = enabled;
    }

    // root 기준 "A/B/C" 경로의 Renderer 반환. path 빈문자열이면 root 자신.
    static Renderer FindRendererByPath(Transform root, string path)
    {
        if (root == null) return null;
        if (string.IsNullOrEmpty(path)) return root.GetComponent<Renderer>();
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    static void DestroySafe(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }
}
