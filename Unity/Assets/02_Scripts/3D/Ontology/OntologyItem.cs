using UnityEngine;

/// <summary>
/// 씬에 이미 배치된 Ontology 아이템에 붙이는 컴포넌트.
///
/// OntologySpawnManager.Instance.Spawn(this) 를 외부에서 호출하면
/// 홈 위치(transform.position)가 비어 있으면 그 자리에, 겹치면
/// zone 에 맞는 랜덤 위치로 이동 후 SetActive(true).
/// </summary>
[RequireComponent(typeof(FurnitureInteraction))]
public class OntologyItem : MonoBehaviour
{
    public enum OntologyZone { Floor, TableSurface, Wall, Ceiling, Ignore }

    [Header("소환 구역 (겹쳤을 때 랜덤 배치 범위)")]
    [Tooltip("Ignore = 겹침 체크 없이 항상 현재 위치에서 소환 (예: moon_lamp)")]
    public OntologyZone zone = OntologyZone.Floor;


    [Header("카메라 뷰포인트")]
    [Tooltip("줌인 시 카메라가 위치할 지점. 이 Transform의 position·rotation을 그대로 사용.\n" +
             "오브젝트 자식으로 빈 GameObject를 만들어 원하는 면을 향해 배치 후 연결.\n" +
             "비워두면 FurnitureFocusController 기본 줌인 사용.")]
    public Transform cameraViewPoint;

    [Tooltip("줌인 거리. 0이면 FurnitureFocusController.viewDist 사용.")]
    public float viewDistance = 0f;

    [Header("설명 UI 메시지")]
    [Tooltip("클릭 시 표시할 텍스트. 백엔드 로드 후 SetMessage()로 주입 가능.")]
    public string message;

    public void SetMessage(string msg) { message = msg; }

    void Awake()
    {
        Debug.Log($"[OIDBG] {name}: OntologyItem.Awake fired");
        // FurnitureInteraction 자동 세팅 (Ontology 아이템은 항상 이 설정)
        var fi = GetComponent<FurnitureInteraction>();
        fi.zoomOnlyNoScene  = true;
        fi.allowInVisitRoom = true;

        // 클릭 감지용 콜라이더 보장.
        // VisitRoom 줌인 클릭 경로(CameraController.CheckBoardClick)는 활성 Collider가 있어야
        // Physics.RaycastAll로 hit된다.
        // ★ 메시(MeshFilter)가 있는 자식마다 개별로 콜라이더를 보장한다.
        //   - 헤드셋처럼 컵·고리가 별도 메시면 각각 콜라이더가 생겨 전부 클릭됨.
        //   - 루트에 큰 박스 하나만 붙이면 pivot 오프셋·스케일로 메시에서 벗어나는 문제도 회피.
        //   - 누군가 한 부분에만 콜라이더를 수동 추가해도(헤드셋 고리) 나머지 메시에 보강됨.
        EnsureColliders();
    }

    void EnsureColliders()
    {
        var meshFilters = GetComponentsInChildren<MeshFilter>();
        Debug.Log($"[OIDBG] {name}: Awake EnsureColliders, meshFilters={meshFilters.Length}, activeInHierarchy={gameObject.activeInHierarchy}");
        bool addedAny = false;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            // 이 메시 GameObject에 이미 콜라이더가 있으면 건너뜀
            // (자식까지 보면 안 됨 — 형제 메시의 콜라이더를 자기 것으로 오인할 수 있음)
            bool hasCollider = false;
            foreach (var c in mf.GetComponents<Collider>())
                if (c.enabled) { hasCollider = true; break; }
            if (hasCollider) continue;

            // 메시 로컬 bounds로 BoxCollider 부착 (메시별 개별 → 컵·고리 등 각각 클릭됨).
            // MeshCollider는 임포트 메시의 Read/Write가 꺼져 있으면 런타임 실패하므로 사용 안 함.
            // mesh.bounds는 Read/Write 무관하게 항상 접근 가능.
            var meshBounds = mf.sharedMesh.bounds;
            var meshBox = mf.gameObject.AddComponent<BoxCollider>();
            meshBox.center = meshBounds.center;
            meshBox.size   = meshBounds.size;
            addedAny = true;
            Debug.Log($"[OIDBG] {name}: added BoxCollider on child '{mf.gameObject.name}' layer={mf.gameObject.layer} size={meshBounds.size}");
        }

        // 메시 자식이 전혀 없을 때(스킨드 등)만 루트 Renderer bounds 박스로 폴백
        if (addedAny) return;

        bool anyExisting = false;
        foreach (var c in GetComponentsInChildren<Collider>())
            if (c.enabled) { anyExisting = true; break; }
        if (anyExisting) return;

        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);   // 보이는 메시 중심으로 보정
        Vector3 s = transform.lossyScale;
        box.size = new Vector3(
            Mathf.Abs(s.x) > 1e-5f ? b.size.x / Mathf.Abs(s.x) : b.size.x,
            Mathf.Abs(s.y) > 1e-5f ? b.size.y / Mathf.Abs(s.y) : b.size.y,
            Mathf.Abs(s.z) > 1e-5f ? b.size.z / Mathf.Abs(s.z) : b.size.z
        );
    }
}
