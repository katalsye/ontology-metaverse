using UnityEngine;

/// <summary>
/// 다른 오브젝트에 가려진 OntologyItem을 대신 눌러주는 프록시.
/// 예: 침대 오브젝트에 붙이고 target = mirror light OntologyItem
///     → VisitRoom에서 침대 클릭 시 mirror light가 눌린 것으로 처리.
/// </summary>
public class OntologyProxy : MonoBehaviour
{
    [Tooltip("이 오브젝트 클릭 시 대신 반응할 OntologyItem")]
    public OntologyItem target;
}
