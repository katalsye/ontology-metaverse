using UnityEngine;

/// <summary>
/// 현재 방 데이터를 들고 있는 싱글턴.
/// </summary>
public class RoomDataManager : MonoBehaviour
{
    public static RoomDataManager Instance { get; private set; }

    public RoomData CurrentRoom { get; private set; } = new RoomData();

    void Awake()
    {
        Instance = this;
    }

    /// <summary>불러온 데이터 적용</summary>
    public void Load(RoomData data)
    {
        CurrentRoom = data ?? new RoomData();

        // TODO: DB 연동 시 주석 해제
        // FirebaseManager.Instance?.FetchRoomData(onSuccess: Load);
    }

    /// <summary>현재 씬 상태를 RoomData에 저장</summary>
    public void Save()
    {
        // TODO: DB 연동 시 주석 해제
        // FirebaseManager.Instance?.UploadRoomData(CurrentRoom);
        // Debug.Log("[RoomDataManager] Save() 호출됨 (DB 연동 전)");
    }
}
