using System.Collections.Generic;
using UnityEngine;

public class RoomDataManager : MonoBehaviour
{
    public static RoomDataManager Instance { get; private set; }

    public RoomData CurrentRoom { get; private set; } = new RoomData();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        FetchFromFirestore();

        if (RoomObjectManager.Instance != null)
            RoomObjectManager.Instance.OnInferredObjectsAdded += HandleInferredObjectsAdded;
    }

    void OnDestroy()
    {
        if (RoomObjectManager.Instance != null)
            RoomObjectManager.Instance.OnInferredObjectsAdded -= HandleInferredObjectsAdded;
    }

    public void Load(RoomData data)
    {
        CurrentRoom = data ?? new RoomData();
    }

    public void FetchFromFirestore()
    {
        if (RoomObjectManager.Instance == null) return;
        RoomObjectManager.Instance.GetRoomObjects(
            objects =>
            {
                RoomObjectManager.Instance.GetCustomPositions(
                    positions =>
                    {
                        var positionMap = new Dictionary<string, RoomCustomPosition>();
                        foreach (var p in positions)
                            if (!string.IsNullOrEmpty(p.ObjectId))
                                positionMap[p.ObjectId] = p;

                        var data = new RoomData();
                        foreach (var obj in objects)
                            data.furnitures.Add(ToFurnitureItemData(obj, positionMap));
                        Load(data);
                    },
                    err =>
                    {
                        Debug.LogWarning("[RoomDataManager] 커스텀 위치 로드 실패: " + err);
                        var data = new RoomData();
                        foreach (var obj in objects)
                            data.furnitures.Add(ToFurnitureItemData(obj, null));
                        Load(data);
                    }
                );
            },
            err => Debug.LogWarning("[RoomDataManager] Firestore 로드 실패: " + err)
        );
    }

    // 온톨로지가 새 오브젝트를 추론·배치했을 때 토스트 알림 + 씬 배치
    private void HandleInferredObjectsAdded(List<RoomObject> inferred)
    {
        foreach (var obj in inferred)
        {
            string concept = string.IsNullOrEmpty(obj.InferredFromConcept)
                ? obj.InferredFrom
                : obj.InferredFromConcept;
            string msg = $"{concept} 기반으로 {obj.ObjectType}이(가) 방에 추가됐어요!";
            Debug.Log($"[RoomDataManager] 추론 오브젝트 감지: {msg}");

            if (ToastController != null)
                ToastController.Show("온톨로지 추천", msg, "myroom");

            // ObjectType 문자열과 GameObject 이름이 같은 OntologyItem을 찾아 소환
            var item = FindOntologyItemByType(obj.ObjectType);
            if (item != null && OntologySpawnManager.Instance != null)
                OntologySpawnManager.Instance.Spawn(item);
            else
                Debug.LogWarning($"[RoomDataManager] OntologyItem을 찾지 못함: objectType={obj.ObjectType}");
        }
    }

    private OntologyItem FindOntologyItemByType(string objectType)
    {
        if (string.IsNullOrEmpty(objectType)) return null;

        string target = NormalizeName(objectType);
        OntologyItem fallback = null;

        foreach (var item in FindObjectsOfType<OntologyItem>(true))
        {
            string name = item.gameObject.name;
            if (name == objectType) return item;                 // 정확 일치 우선

            // 대소문자·공백·Unity 복제 접미사(" (1)") 무시 후보.
            // Rule 4가 복수 오브젝트를 만들 때 씬 복제본이 "coffee_cup (1)"이 되는 경우 대응.
            if (fallback == null && NormalizeName(name) == target)
                fallback = item;
        }

        return fallback;
    }

    // 매칭용 이름 정규화: Unity 복제 접미사 제거 + trim + 소문자.
    private static string NormalizeName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int p = s.IndexOf(" (");
        if (p > 0 && s.EndsWith(")")) s = s.Substring(0, p);
        return s.Trim().ToLowerInvariant();
    }

    private ToastController _toastController;
    private ToastController ToastController
    {
        get
        {
            if (_toastController == null)
                _toastController = FindObjectOfType<ToastController>();
            return _toastController;
        }
    }

    public void Save()
    {
        if (RoomObjectManager.Instance == null) return;
        var objects = new List<RoomObject>();
        foreach (var f in CurrentRoom.furnitures)
        {
            objects.Add(new RoomObject
            {
                ObjectId            = f.furnitureType,
                ObjectType          = f.furnitureType,
                PositionX           = f.position.x,
                PositionY           = f.position.y,
                PositionZ           = f.position.z,
                InferredFrom        = f.inferredFrom,
                InferredFromConcept = f.inferredFromConcept,
            });
        }
        RoomObjectManager.Instance.SaveCustomLayout(objects);
    }

    private static FurnitureItemData ToFurnitureItemData(RoomObject obj, Dictionary<string, RoomCustomPosition> positionMap)
    {
        Vector3 position = Vector3.zero;
        if (positionMap != null && !string.IsNullOrEmpty(obj.ObjectId)
            && positionMap.TryGetValue(obj.ObjectId, out var p))
            position = new Vector3(p.PositionX, p.PositionY, p.PositionZ);

        return new FurnitureItemData
        {
            furnitureType       = obj.ObjectType,
            designIndex         = 0,
            position            = position,
            rotation            = Vector3.zero,
            inferredFrom        = obj.InferredFrom,
            inferredFromConcept = obj.InferredFromConcept,
        };
    }
}
