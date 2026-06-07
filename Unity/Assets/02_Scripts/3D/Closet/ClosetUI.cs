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
    [Tooltip("저장 없이 MyRoom 모드로 3D 방 씬으로 이동")]
    public Button backButton;

    [Header("커스텀 버튼 & 윈도우")]
    public Button     colorButton;
    public Button     hatButton;
    public GameObject colorWindow;
    public GameObject hatWindow;

    [Header("패널")]
    public GameObject characterPanel;
    public GameObject furniturePanel;

    [Header("3D 오브젝트")]
    public GameObject characterRoot;
    public GameObject furnitureRoot;

    [Header("가구 캐러셀")]
    [Tooltip("furniturePanel 안의 FurnitureCarouselController — 카메라 타겟 및 초기화를 대신 처리")]
    public FurnitureCarouselController furnitureCarousel;

    [Header("씬")]
    [Tooltip("3D 방 씬 이름 — Build Settings에 등록된 이름과 정확히 일치해야 함")]
    public string roomSceneName = "3DRoomScene";

    void Awake() => Instance = this;

    void Start()
    {
        characterButton?.onClick.AddListener(() => SelectTab(true));
        furnitureButton?.onClick.AddListener(() => SelectTab(false));
        // Move 버튼: EditMode로 설정 후 3D 방 씬으로 이동
        moveButton?.onClick.AddListener(() => LoadRoomScene(RoomMode.EditMode));
        // Back 버튼: 저장 없이 MyRoom 모드로 3D 방 씬으로 이동
        backButton?.onClick.AddListener(() => LoadRoomScene(RoomMode.MyRoom));

        colorButton?.onClick.AddListener(() => SwitchCustomWindow(true));
        hatButton?.onClick.AddListener(() => SwitchCustomWindow(false));
        SwitchCustomWindow(true);

        // CharacterColorController가 characterRoot에 없으면 자동 연결
        if (characterRoot != null)
        {
            var cc = CharacterColorController.Instance;
            if (cc != null && cc.characterRenderer == null)
                cc.characterRenderer = characterRoot.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        SelectTab(true);
    }

    // isColor=true: 색상창 표시 + hat버튼 보이기 / isColor=false: 모자창 표시 + color버튼 보이기
    void SwitchCustomWindow(bool isColor)
    {
        colorWindow?.SetActive(isColor);
        hatWindow?.SetActive(!isColor);
        colorButton?.gameObject.SetActive(!isColor);
        hatButton?.gameObject.SetActive(isColor);
    }

    void LoadRoomScene(RoomMode mode)
    {
        RoomModeManager.SetMode(mode);

        // 모든 모드 통일: MainScene 유지하면서 Closet만 언로드 → 3DRoomScene 추가 로드
        // (EditMode도 Main과 겹치게 처리 — 특수 케이스 없앰)
        SceneManager.UnloadSceneAsync("Closet");
        SceneManager.LoadScene(roomSceneName, LoadSceneMode.Additive);
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
        cam.ResetView();

        if (isCharacter)
        {
            // 캐릭터 탭: 캐릭터 루트를 카메라 타겟으로
            cam.target = characterRoot != null ? characterRoot.transform : null;
        }
        else
        {
            // 가구 탭: FurnitureCarouselUI가 있으면 현재 선택 가구를 타겟으로,
            // 없으면 furnitureRoot 전체를 타겟으로
            if (furnitureCarousel != null)
                furnitureCarousel.ShowFurniture(furnitureCarousel.CurrentIndex);
            else
                cam.target = furnitureRoot != null ? furnitureRoot.transform : null;
        }
    }
}
