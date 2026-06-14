using UnityEngine;

/// <summary>
/// AddFurnitureSelectionUI / RoomFurnitureCustomizer.SpawnSavedShopItem로 생성된
/// 상점 가구에 부착되는 식별 태그.
/// shopCatalog(FurnitureCatalog).furnitures의 인덱스(furnitureId)와
/// 현재 적용된 색상 번호(colorId)를 보관해 room_shop_items 저장/불러오기에 사용한다.
/// </summary>
public class ShopItemInstance : MonoBehaviour
{
    public int furnitureId;
    public int colorId;
}
