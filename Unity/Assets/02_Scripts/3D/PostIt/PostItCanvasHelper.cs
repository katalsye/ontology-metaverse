using UnityEngine;
using TMPro;

// 포스트잇/버튼 공용 헬퍼.
// model.dae 메시 pivot이 (-62,151,-2.8)로 크게 어긋나 있어 로컬 좌표 하드코딩이 불가능 →
// 런타임에 실제 Renderer bounds 기준으로 계산.
public static class PostItCanvasHelper
{
    // 본체 메시 렌더러 찾기 (TMP 렌더러는 제외)
    public static MeshRenderer FindBodyRenderer(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.GetComponent<TextMeshPro>() != null) continue;      // 3D TMP 제외
            if (r.GetComponentInParent<Canvas>() != null) continue;   // Canvas 하위 제외
            return r;
        }
        return null;
    }

    // 루트에 본체 메시에 맞춘 BoxCollider를 '강제로' 추가/refit.
    // 자식 collider가 있어도 무시하고 루트에 직접 — 그래야 raycast 히트 시 루트 태그가 잡힘.
    //
    // 핵심: 메시의 8 꼭짓점을 루트 로컬 공간으로 변환해서 정확한 axis-aligned bounds 계산.
    // (월드 AABB를 lossyScale로 나누는 방식은 회전 있으면 크기가 틀어짐)
    public static void EnsureFittedCollider(GameObject root)
    {
        var bc = root.GetComponent<BoxCollider>();
        if (bc == null) bc = root.AddComponent<BoxCollider>();

        var rend = FindBodyRenderer(root);
        var mf   = rend != null ? rend.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null)
        {
            // MeshFilter 없을 때 Renderer.bounds로 fallback — Sticky_note_yellow처럼 구조가 다른 오브젝트도 처리
            if (rend != null)
            {
                Bounds wb = rend.bounds;
                Transform rt = root.transform;
                bc.center = rt.InverseTransformPoint(wb.center);
                Vector3 ls = rt.InverseTransformVector(wb.size);
                bc.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            }
            else
            {
                // Debug.LogWarning($"[PostItCanvasHelper] '{root.name}' MeshFilter/Renderer 둘 다 없음 — 콜라이더 기본(1,1,1) 유지");
            }
            return;
        }

        // 메시 로컬 bounds의 8 꼭짓점 → renderer GO 로컬 → 월드 → 루트 로컬
        Bounds mb = mf.sharedMesh.bounds;
        Transform meshT = mf.transform;
        Transform rootT = root.transform;
        Vector3 c = mb.center;
        Vector3 e = mb.extents;
        Vector3[] corners = {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3(+e.x, -e.y, -e.z),
            c + new Vector3(-e.x, +e.y, -e.z),
            c + new Vector3(+e.x, +e.y, -e.z),
            c + new Vector3(-e.x, -e.y, +e.z),
            c + new Vector3(+e.x, -e.y, +e.z),
            c + new Vector3(-e.x, +e.y, +e.z),
            c + new Vector3(+e.x, +e.y, +e.z),
        };

        Bounds rootLocal = new Bounds(rootT.InverseTransformPoint(meshT.TransformPoint(corners[0])), Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            Vector3 world     = meshT.TransformPoint(corners[i]);
            Vector3 inRoot    = rootT.InverseTransformPoint(world);
            rootLocal.Encapsulate(inRoot);
        }

        bc.center = rootLocal.center;
        bc.size   = rootLocal.size;

    }
}
