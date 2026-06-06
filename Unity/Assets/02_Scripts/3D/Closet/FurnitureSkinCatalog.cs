using UnityEngine;

/// <summary>
/// 가구 스킨(모델 variant + 색상) 데이터를 담는 에셋(ScriptableObject).
///
/// 목적:
///   - Closet 씬의 FurnitureCarouselUI 데이터는 "씬 인스턴스 참조"라 다른 씬(방)에서 못 쓴다.
///   - 이 에셋은 프로젝트 전역에서 공유되므로 Closet·방 양쪽이 같은 데이터를 읽을 수 있다.
///
/// 씬 참조를 에셋에 담기 위한 변환:
///   - model3D(씬 인스턴스) → modelPrefab(프로젝트 .prefab 에셋)
///   - colorChildren.renderer(씬 Renderer 직접참조) → rendererPath(모델 루트 기준 상대경로 문자열)
///
/// 생성: Assets 우클릭 → Create → Ontology/Furniture Skin Catalog
/// 데이터 채우기: 메뉴 Tools → Ontology → Import FurnitureSkin From Closet (자동 이전)
/// </summary>
[CreateAssetMenu(fileName = "FurnitureSkinCatalog", menuName = "Ontology/Furniture Skin Catalog")]
public class FurnitureSkinCatalog : ScriptableObject
{
    /// <summary>색상 1종을 적용할 대상 — 모델 내부 특정 Renderer의 특정 material 슬롯.</summary>
    [System.Serializable]
    public class ColorChild
    {
        [Tooltip("모델 루트(modelPrefab) 기준 Renderer까지의 상대 경로. 예: \"Body/Top\". 빈 문자열이면 루트 자신.")]
        public string     rendererPath;
        [Tooltip("바꿀 material 슬롯 번호 (0부터)")]
        public int        materialIndex;
        [Tooltip("materials[n] = n번째 색상의 material")]
        public Material[] materials;
    }

    /// <summary>모델 1종(variant) — 교체용 prefab + 그 모델의 색상 적용 규칙들.</summary>
    [System.Serializable]
    public class Variant
    {
        public string       displayName;
        [Tooltip("방에서 교체 시 Instantiate할 모델 prefab 에셋")]
        public GameObject   modelPrefab;
        public ColorChild[] colorChildren;
    }

    /// <summary>가구 1종 — variant 여러 개(모델 선택지). variants[0]이 기본.</summary>
    [System.Serializable]
    public class Furniture
    {
        public string    displayName;
        public Variant[] variants;
    }

    public Furniture[] furnitures;

    /// <summary>인덱스 안전 조회.</summary>
    public Furniture GetFurniture(int i)
        => (furnitures != null && i >= 0 && i < furnitures.Length) ? furnitures[i] : null;

    public Variant GetVariant(int fi, int vi)
    {
        var f = GetFurniture(fi);
        return (f?.variants != null && vi >= 0 && vi < f.variants.Length) ? f.variants[vi] : null;
    }
}
