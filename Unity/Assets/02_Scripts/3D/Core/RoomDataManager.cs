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
                var data = new RoomData();
                foreach (var obj in objects)
                {
                    data.furnitures.Add(new FurnitureItemData
                    {
                        furnitureType = obj.ObjectType,
                        designIndex   = 0,
                        position      = new Vector3(obj.PositionX, obj.PositionY, obj.PositionZ),
                        rotation      = Vector3.zero,
                    });
                }
                Load(data);
            },
            err => Debug.LogWarning("[RoomDataManager] Firestore 로드 실패: " + err)
        );
    }

    public void Save()
    {
        if (RoomObjectManager.Instance == null) return;
        var objects = new List<RoomObject>();
        foreach (var f in CurrentRoom.furnitures)
        {
            objects.Add(new RoomObject
            {
                ObjectId   = f.furnitureType,
                ObjectType = f.furnitureType,
                PositionX  = f.position.x,
                PositionY  = f.position.y,
                PositionZ  = f.position.z,
            });
        }
        RoomObjectManager.Instance.SaveCustomLayout(objects);
    }
}
