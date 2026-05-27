using UnityEngine;

public class FurniturePopupManager : MonoBehaviour
{
    private PopupManager popupManager;

    public void Initialize(PopupManager manager)
    {
        popupManager = manager;
    }

    public void OpenFurniture(FurnitureType type)
    {
        switch (type)
        {
            case FurnitureType.Bed:
                popupManager.ShowPopup("Sleep Info", "Sleep Time: 7h 30m\nSleep Quality: Good");
                break;
            case FurnitureType.Desk:
                Debug.Log("Open Quest Board from Desk");
                UIPageManager.Instance?.ShowPage(UIPage.Quest);
                break;
            case FurnitureType.Calendar:
                popupManager.ShowPopup("Past Room / History", "Past room records will be shown here.");
                break;
            case FurnitureType.Board:
                popupManager.ShowPopup("Guestbook", "Guestbook messages will be shown here.");
                break;
            case FurnitureType.Wardrobe:
                popupManager.ShowPopup("Avatar Custom", "Avatar customization will be added here.");
                break;
            case FurnitureType.Pet:
                popupManager.ShowPopup("AI Pet", "Pet Name: Mochi\nStatus: Happy");
                popupManager.AddButtonToCurrentPopup("Feed", () => Debug.Log("Pet Feed"));
                popupManager.AddButtonToCurrentPopup("Talk", () => Debug.Log("Pet Talk"));
                popupManager.AddButtonToCurrentPopup("Close", popupManager.ClosePopup);
                break;
            case FurnitureType.Door:
                Debug.Log("Go to Feed Page");
                UIPageManager.Instance?.ShowPage(UIPage.Feed);
                break;
        }
    }
}
