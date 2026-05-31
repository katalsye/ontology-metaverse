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
        // FurnitureInteraction 자동 세팅 (Ontology 아이템은 항상 이 설정)
        var fi = GetComponent<FurnitureInteraction>();
        fi.zoomOnlyNoScene  = true;
        fi.allowInVisitRoom = true;

    }
}
