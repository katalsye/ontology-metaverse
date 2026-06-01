using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EditMode — 가구 추가 패널.
/// UI는 Inspector에서 직접 연결. Open()/Close()를 버튼에 연결할 것.
/// </summary>
public class FurnitureAddUI : MonoBehaviour
{
    public static FurnitureAddUI Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;

    void Awake() { Instance = this; if (panel) panel.SetActive(false); }

    // ── 열기 / 닫기 ──────────────────────────────────────────

    public void Open()  { if (panel) panel.SetActive(true); }
    public void Close() { if (panel) panel.SetActive(false); }

    // ── 가구 추가 확정 ────────────────────────────────────────

    /// <summary>
    /// 선택한 가구 프리팹을 씬에 배치하고 editableItems에 자동 등록.
    /// 썸네일 버튼 onClick에서 호출.
    /// </summary>
    public void AddFurniture(GameObject prefab)
    {
        if (prefab == null) return;

        // 씬에 배치 (일단 원점. 위치는 나중에 드래그로 이동)
        var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        // FurnitureEditController editableItems에 자동 등록
        var fec = FurnitureEditController.Instance;
        if (fec != null)
        {
            var list = new List<FurnitureEditConfig>(fec.editableItems ?? new FurnitureEditConfig[0]);
            list.Add(new FurnitureEditConfig { target = instance, canMove = true, canDesign = true });
            fec.editableItems = list.ToArray();
        }

        Close();
    }
}
