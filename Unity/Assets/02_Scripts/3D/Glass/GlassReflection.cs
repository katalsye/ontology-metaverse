using UnityEngine;
using UnityEngine.Rendering;

// 유니티 Inspector 설정:
// 유리 오브젝트에 이 스크립트 붙이기
// 씬 시작 시 자동으로 Reflection Probe를 생성하고 초기 1회 렌더링
// 카메라가 가까워지면 추가로 한 번 더 업데이트 (자연스러운 반사)

[RequireComponent(typeof(Renderer))]
public class GlassReflection : MonoBehaviour
{
    [Header("반사 설정")]
    [Tooltip("반사 텍스처 해상도 (낮을수록 성능 좋음, 128 권장)")]
    public int probeResolution = 128;

    [Tooltip("반사 감지 범위 (방 크기에 맞게)")]
    public Vector3 probeSize = new Vector3(30f, 15f, 30f);

    [Header("근접 업데이트")]
    [Tooltip("이 거리 이하로 플레이어가 오면 반사 한 번 더 갱신")]
    public float refreshDistance = 6f;

    [Tooltip("Inspector에서 Player 오브젝트 연결")]
    public Transform player;

    private ReflectionProbe _probe;
    private bool _refreshed = false;

    void Start()
    {
        // player가 연결 안 됐으면 Player 태그로 자동 탐색
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Reflection Probe 생성
        GameObject probeGO = new GameObject("_GlassProbe");
        probeGO.transform.SetParent(transform);
        probeGO.transform.localPosition = Vector3.zero;

        _probe = probeGO.AddComponent<ReflectionProbe>();
        _probe.mode           = ReflectionProbeMode.Realtime;
        _probe.refreshMode    = ReflectionProbeRefreshMode.ViaScripting;
        _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        _probe.resolution     = probeResolution;
        _probe.size           = probeSize;
        _probe.intensity      = 1f;
        _probe.blendDistance  = 1f;

        // 씬 시작 시 1회 렌더
        _probe.RenderProbe();
    }

    void Update()
    {
        if (player == null || _probe == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        // 가까이 오면 반사 한 번 갱신 (근접 반사 자연스럽게)
        if (!_refreshed && dist < refreshDistance)
        {
            _probe.RenderProbe();
            _refreshed = true;
        }
        else if (_refreshed && dist >= refreshDistance)
        {
            _refreshed = false; // 멀어지면 리셋 (다음 접근 때 다시 갱신)
        }
    }
}
