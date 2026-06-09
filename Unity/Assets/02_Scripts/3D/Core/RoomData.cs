using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내 방 전체 데이터. Firebase 저장/불러오기 기준 클래스.
/// </summary>
[System.Serializable]
public class RoomData
{
    public List<FurnitureItemData> furnitures = new();
    public WallData                wall       = new();
}

/// <summary>가구 1개 데이터</summary>
[System.Serializable]
public class FurnitureItemData
{
    public string   furnitureType;        // 가구 종류 (예: "SingleBed", "KitchenIsland")
    public int      designIndex;          // 디자인 번호
    public Vector3  position;
    public Vector3  rotation;             // Euler angles
    public string   inferredFrom;         // 온톨로지 추론 근거 (예: "PlaceHabit")
    public string   inferredFromConcept;  // 추론된 개념 (예: "cafe_gangnam")

    public bool IsInferred => !string.IsNullOrEmpty(inferredFrom);
}

/// <summary>벽 데이터</summary>
[System.Serializable]
public class WallData
{
    public int designIndex;          // 벽 디자인 번호
}
