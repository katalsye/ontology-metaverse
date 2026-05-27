using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class FurnitureInteraction : MonoBehaviour
{
    [SerializeField] private FurnitureType furnitureType;
    private FurniturePopupManager popupManager;

    public void Initialize(FurnitureType type, FurniturePopupManager manager)
    {
        furnitureType = type;
        popupManager = manager;
        GetComponent<Button>().onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        Debug.Log("Furniture Click: " + furnitureType);
        popupManager?.OpenFurniture(furnitureType);
    }
}
