using UnityEngine;
//
// prefab에 저장할 각 object의 데이터를 저장합니다.
// objectType : 어떤 객체인지(책상, 침대 등) 정의합니다.
//              이에 따라 객체와 상호작용 시 넘어가는 함수가 달라집니다.
//  gridWidth : 객체 크기의 x를 결정합니다. 
//  gridDepth : 객체 크기의 z를 결정합니다.
// gridHeight : 객체 크기의 y를 결정합니다.
//
//   designId : objectType에 따라 저장된 디자인 정보를 저장합니다.
//              이는 상점에서 구매할 수 있습니다.
//
public class RoomObjectData : MonoBehaviour
{
    public string objectType;      
    public int gridWidth = 1;       
    public int gridDepth = 1;      
    public int gridHeight = 1;
 
    public string designId;         
}
 