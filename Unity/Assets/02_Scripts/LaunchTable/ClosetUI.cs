using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ClosetUI : MonoBehaviour
{
    public static ClosetUI Instance { get; private set; }

    [Header("탭 버튼")]
    public Button characterButton;
    public Button furnitureButton;
    public Button moveButton;

    [Header("패널")]
    public GameObject characterPanel;
    public GameObject furniturePanel;

    [Header("3D 오브젝트")]
    public GameObject characterRoot;
    public GameObject furnitureRoot;

    [Header("씬")]
    public string roomSceneName = "SampleScene";

    void Awake() => Instance = this;

    void Start()
    {
        characterButton?.onClick.AddListener(() => SelectTab(true));
        furnitureButton?.onClick.AddListener(() => SelectTab(false));
        moveButton?.onClick.AddListener(() => SceneManager.LoadScene(roomSceneName));

        // CharacterColorController가 characterRoot에 없으면 자동 연결
        if (characterRoot != null)
        {
            var cc = CharacterColorController.Instance;
            if (cc != null && cc.characterRenderer == null)
                cc.characterRenderer = characterRoot.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        SelectTab(true);
    }

    void SelectTab(bool isCharacter)
    {
        characterButton?.gameObject.SetActive(!isCharacter);
        furnitureButton?.gameObject.SetActive(isCharacter);

        if (characterPanel != null) characterPanel.SetActive(isCharacter);
        if (furniturePanel  != null) furniturePanel.SetActive(!isCharacter);

        if (characterRoot != null) characterRoot.SetActive(isCharacter);
        if (furnitureRoot  != null) furnitureRoot.SetActive(!isCharacter);

        var cam = ClosetOrbitCamera.Instance;
        if (cam == null) return;
        cam.target = isCharacter
            ? (characterRoot != null ? characterRoot.transform : null)
            : (furnitureRoot  != null ? furnitureRoot.transform  : null);
    }
}
