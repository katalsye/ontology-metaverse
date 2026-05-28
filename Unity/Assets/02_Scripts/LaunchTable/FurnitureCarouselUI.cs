using UnityEngine;

/// <summary>
/// Closet 씬 Furniture 탭 — 가구 데이터 저장소.
/// 기능(스와이프, UI 빌드, material 적용)은 FurnitureCarouselController에서 담당.
/// </summary>
public class FurnitureCarouselUI : MonoBehaviour
{
    // ── 데이터 ────────────────────────────────────────────────

    [System.Serializable]
    public class ColorChild
    {
        public Renderer   renderer;
        [Tooltip("바꾸기 시작할 슬롯 번호 (0부터)")]
        public int        materialIndex;
        [Tooltip("materials[n] = n번째 색상의 material")]
        public Material[] materials;
    }

    [System.Serializable]
    public class FurnitureModel
    {
        public GameObject   model3D;
        public ColorChild[] colorChildren;
    }

    [System.Serializable]
    public class FurnitureVariant
    {
        public string         displayName;
        // TODO: DB 연동 후 서버에서 받아온 구매 목록으로 설정할 것 (현재는 Inspector 디버그용)
        public bool           isPurchased;
        public FurnitureModel model;
        [Tooltip("색상별 썸네일 — colorThumbnails[n] 이 materials[n] 과 1:1 대응. [0]이 대표 썸네일로 사용됨")]
        public Sprite[]       colorThumbnails;
    }

    [System.Serializable]
    public class FurnitureEntry
    {
        public string             displayName;
        public FurnitureVariant[] variants; // variants[0]이 기본
    }

    // ── Inspector ─────────────────────────────────────────────

    [Header("가구 목록 (순환 순서대로)")]
    public FurnitureEntry[] furnitures;

    // ── 상태 ──────────────────────────────────────────────────

    public int CurrentIndex   { get; set; } = 0;
    public int CurrentVariant { get; set; } = 0;
}
