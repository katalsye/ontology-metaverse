using UnityEngine;

/// <summary>
/// Ontology 아이템 소환 매니저.
///
/// 소환 규칙:
///   1. 홈 위치(transform.position) 주변에 다른 오브젝트가 없으면 그 자리에서 SetActive(true).
///   2. 겹치면 OntologyItem.zone 에 맞는 영역에서 랜덤 위치를 골라 이동 후 SetActive(true).
///   3. zone = Ignore 이면 겹침 체크 없이 항상 제자리 SetActive(true).
///
/// 범위는 FurnitureEditController 재활용:
///   Floor   → roomMinX/MaxX, roomMinZ/MaxZ, floorY
///   Ceiling → roomMinX/MaxX, roomMinZ/MaxZ, ceilingY
///   Table   → deskObject renderer bounds 위 표면
///   Wall    → wallSpawnPoints X/Z + hangerMinY~hangerMaxY
///
/// ── 백엔드에서 불러온 후 호출 예시 ──────────────────────────
/// var item = ontologyGameObject.GetComponent&lt;OntologyItem&gt;();
/// OntologySpawnManager.Instance.Spawn(item);
/// ────────────────────────────────────────────────────────────
/// </summary>
public class OntologySpawnManager : MonoBehaviour
{
    public static OntologySpawnManager Instance { get; private set; }

    // ── 겹침 ──────────────────────────────────────────────────

    [Header("겹침 체크")]
    [Tooltip("홈 위치로부터 이 반경 안에 다른 오브젝트가 있으면 겹침으로 판단")]
    public float overlapRadius = 0.3f;

    [Tooltip("FurnitureParent — 이 안의 모든 가구가 겹침 체크 대상")]
    public Transform furnitureParent;

    [Tooltip("랜덤 위치 탐색 최대 시도 횟수")]
    public int maxTries = 20;

    // ── 라이프사이클 ──────────────────────────────────────────

    void Awake() { Instance = this; }

    // ── 공개 API ──────────────────────────────────────────────

    public void Spawn(OntologyItem item)
    {
        if (item == null) return;

        if (item.zone == OntologyItem.OntologyZone.Ignore || !IsOccupied(item.transform.position, item))
        {
            item.gameObject.SetActive(true);
            return;
        }

        Vector3 spawnPos = FindFreePosition(item);
        item.transform.position = spawnPos;
        item.gameObject.SetActive(true);
    }

    // ── 내부 로직 ─────────────────────────────────────────────

    bool IsOccupied(Vector3 pos, OntologyItem self)
    {
        // 활성화된 다른 OntologyItem 거리 체크
        foreach (var item in FindObjectsOfType<OntologyItem>())
        {
            if (item == self || !item.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(pos, item.transform.position) < overlapRadius) return true;
        }

        // FurnitureParent 하위 가구 콜라이더 체크
        if (furnitureParent != null)
        {
            foreach (var col in Physics.OverlapSphere(pos, overlapRadius))
                if (col.transform.IsChildOf(furnitureParent)) return true;
        }

        return false;
    }

    Vector3 FindFreePosition(OntologyItem item)
    {
        Vector3 candidate = Vector3.zero;
        for (int i = 0; i < maxTries; i++)
        {
            candidate = GetRandomCandidate(item.zone);
            if (!IsOccupied(candidate, item)) return candidate;
        }
        return candidate;
    }

    Vector3 GetRandomCandidate(OntologyItem.OntologyZone zone)
    {
        var fec = FurnitureEditController.Instance;
        if (fec == null) return Vector3.zero;

        switch (zone)
        {
            case OntologyItem.OntologyZone.Floor:
                return new Vector3(
                    Random.Range(fec.roomMinX, fec.roomMaxX),
                    fec.floorY,
                    Random.Range(fec.roomMinZ, fec.roomMaxZ));

            case OntologyItem.OntologyZone.Ceiling:
                return new Vector3(
                    Random.Range(fec.roomMinX, fec.roomMaxX),
                    fec.ceilingY,
                    Random.Range(fec.roomMinZ, fec.roomMaxZ));

            case OntologyItem.OntologyZone.TableSurface:
                if (fec.deskObject != null)
                {
                    var rens = fec.deskObject.GetComponentsInChildren<Renderer>();
                    if (rens.Length > 0)
                    {
                        Bounds b = rens[0].bounds;
                        for (int i = 1; i < rens.Length; i++) b.Encapsulate(rens[i].bounds);
                        return new Vector3(
                            Random.Range(b.min.x, b.max.x),
                            b.max.y,
                            Random.Range(b.min.z, b.max.z));
                    }
                }
                // deskObject 없으면 Floor 폴백
                goto case OntologyItem.OntologyZone.Floor;

            case OntologyItem.OntologyZone.Wall:
            {
                // 4면 벽 중 랜덤 1면 선택 → 그 면 위 랜덤 X/Z + hangerMinY~MaxY
                float wy = Random.Range(fec.hangerMinY, fec.hangerMaxY);
                int wall = Random.Range(0, 4);
                switch (wall)
                {
                    case 0: return new Vector3(fec.roomMinX, wy, Random.Range(fec.roomMinZ, fec.roomMaxZ)); // 좌벽
                    case 1: return new Vector3(fec.roomMaxX, wy, Random.Range(fec.roomMinZ, fec.roomMaxZ)); // 우벽
                    case 2: return new Vector3(Random.Range(fec.roomMinX, fec.roomMaxX), wy, fec.roomMinZ); // 앞벽
                    default:return new Vector3(Random.Range(fec.roomMinX, fec.roomMaxX), wy, fec.roomMaxZ); // 뒷벽
                }
            }

            default:
                return new Vector3(
                    Random.Range(fec.roomMinX, fec.roomMaxX),
                    fec.floorY,
                    Random.Range(fec.roomMinZ, fec.roomMaxZ));
        }
    }
}
