using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 유리 오브젝트에 붙이기
// 자동으로 반사 카메라 생성하고 유리에 반사 화면을 렌더링함
// GlassReflection.cs, GlassProximityFade.cs 는 삭제해도 됨

[RequireComponent(typeof(Renderer))]
public class PlanarReflection : MonoBehaviour
{
    [Header("반사 설정")]
    [Tooltip("반사 텍스처 해상도 (256 권장, 높을수록 선명하지만 무거움)")]
    public int textureSize = 256;

    [Tooltip("플레이어가 이 거리 이내일 때만 반사 업데이트 (성능 절약)")]
    public float activeDistance = 20f;

    [Tooltip("Inspector에서 Player 오브젝트 연결 (비워두면 자동 탐색)")]
    public Transform player;

    private Camera _reflectCam;
    private RenderTexture _rt;
    private Material _mat;
    private static readonly int ReflectTex = Shader.PropertyToID("_ReflectionTex");
    private static readonly int ReflectStrength = Shader.PropertyToID("_EmissionMultiply");

    void Start()
    {
        // 플레이어 자동 탐색
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        // RenderTexture 생성
        _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        _rt.name = "_GlassRT";

        // 반사 카메라 생성
        var camGO = new GameObject("_ReflectCam_" + gameObject.name);
        camGO.hideFlags = HideFlags.HideAndDontSave;
        _reflectCam = camGO.AddComponent<Camera>();
        _reflectCam.targetTexture = _rt;
        _reflectCam.enabled = false;

        // 메인 카메라 URP 설정 복사
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            _reflectCam.farClipPlane  = mainCam.farClipPlane;
            _reflectCam.nearClipPlane = mainCam.nearClipPlane;
            _reflectCam.fieldOfView   = mainCam.fieldOfView;
            _reflectCam.cullingMask   = mainCam.cullingMask;

            var mainData    = mainCam.GetUniversalAdditionalCameraData();
            var reflectData = _reflectCam.GetUniversalAdditionalCameraData();
            reflectData.renderShadows        = false; // 반사에서 그림자 끔 (성능)
            reflectData.requiresColorTexture = false;
            reflectData.requiresDepthTexture = false;
        }

        // 유리 머티리얼에 텍스처 슬롯 연결
        _mat = GetComponent<Renderer>().material;
        _mat.SetTexture(ReflectTex, _rt);
    }

    void LateUpdate()
    {
        if (_reflectCam == null || Camera.main == null) return;

        // 플레이어가 멀면 업데이트 안 함 (성능)
        if (player != null)
        {
            float dist = Vector3.Distance(player.position, transform.position);
            if (dist > activeDistance) return;
        }

        Camera mainCam = Camera.main;
        Vector3 normal = transform.forward;
        Vector3 pos    = transform.position;

        // 메인 카메라를 유리 평면 기준으로 반사시켜서 반사 카메라 위치 계산
        Vector3 camPos     = mainCam.transform.position;
        float   distToPlane = Vector3.Dot(normal, camPos - pos);
        Vector3 reflectPos  = camPos - 2f * distToPlane * normal;

        Vector3 camFwd     = mainCam.transform.forward;
        Vector3 reflectFwd = camFwd - 2f * Vector3.Dot(camFwd, normal) * normal;

        _reflectCam.transform.position = reflectPos;
        _reflectCam.transform.rotation = Quaternion.LookRotation(reflectFwd, Vector3.up);
        _reflectCam.projectionMatrix   = mainCam.projectionMatrix;

        // 렌더링
        _reflectCam.Render();

        // 반사 텍스처를 Emission으로 출력 (투명 유리에서 보이게)
        _mat.SetTexture(ReflectTex, _rt);
        _mat.SetFloat(ReflectStrength, 0.4f);
    }

    void OnDestroy()
    {
        if (_reflectCam != null) DestroyImmediate(_reflectCam.gameObject);
        if (_rt != null) { _rt.Release(); DestroyImmediate(_rt); }
    }
}
