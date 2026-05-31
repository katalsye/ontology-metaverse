using UnityEngine;

/// <summary>
/// 씬에 이미 배치된 Ontology 아이템에 붙이는 컴포넌트.
///
/// OntologySpawnManager.Instance.Spawn(this) 를 외부에서 호출하면
/// 홈 위치(transform.position)가 비어 있으면 그 자리에, 겹치면
/// zone 에 맞는 랜덤 위치로 이동 후 SetActive(true).
/// </summary>
public class OntologyItem : MonoBehaviour
{
    public enum OntologyZone { Floor, TableSurface, Wall, Ceiling, Ignore }

    [Header("소환 구역 (겹쳤을 때 랜덤 배치 범위)")]
    [Tooltip("Ignore = 겹침 체크 없이 항상 현재 위치에서 소환 (예: moon_lamp)")]
    public OntologyZone zone = OntologyZone.Floor;
}
