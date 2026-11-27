using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>착지 왜곡이 재생되는 동안에만 월드 카메라 화면을 고리 모양으로 밀어내는 전체화면 패스</summary>
public class ImpactDistortionRendererFeature : FullScreenPassRendererFeature
{
    public Material SourceMaterial; // 왜곡 셰이더를 연결한 원본 재질
    public float RingStartPixels = -50f; // 재생 시작 때 고리 반지름(원본 픽셀)
    public float RingEndPixels = 70f; // 재생 끝에서 고리 반지름(원본 픽셀)
    public float RingWidthPixels = 40f; // 고리 안팎으로 왜곡이 미치는 폭(원본 픽셀)
    public float StrengthPixels = 10f; // 재생 시작 때 화면을 밀어내는 거리(원본 픽셀)
    public float PixelsPerUnit = 16f; // 월드 1유닛에 해당하는 원본 픽셀 수

    private static readonly int CenterId = Shader.PropertyToID("_Center"); // 화면 UV의 왜곡 중심
    private static readonly int RingId = Shader.PropertyToID("_Ring"); // 화면 높이 기준 고리 반지름
    private static readonly int WidthId = Shader.PropertyToID("_Width"); // 화면 높이 기준 고리 폭
    private static readonly int StrengthId = Shader.PropertyToID("_Strength"); // 화면 높이 기준 밀어내는 거리
    private static readonly int AspectId = Shader.PropertyToID("_Aspect"); // 화면 가로세로 비율
    private Material runtimeMaterial; // 원본 재질 에셋을 바꾸지 않도록 실행 중에만 쓰는 사본

    /// <summary>투명한 월드 Sprite까지 그린 뒤 처리할 전체화면 패스 준비</summary>
    public override void Create()
    {
        injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
        fetchColorBuffer = true;
        requirements = ScriptableRenderPassInput.None;
        base.Create();
    }

    /// <summary>왜곡이 재생 중이고 게임 카메라일 때만 현재 진행률로 재질을 갱신해 패스 실행</summary>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 재생 중이 아니면 패스를 건너뛰어 평소 화면 비용을 늘리지 않음
        Camera camera = renderingData.cameraData.camera;
        if (!ImpactDistortionFx.IsPlaying || SourceMaterial == null || camera == null || !camera.orthographic
            || renderingData.cameraData.cameraType != CameraType.Game) return;
        if (runtimeMaterial == null) runtimeMaterial = new Material(SourceMaterial) { hideFlags = HideFlags.HideAndDontSave };

        // 원본 픽셀 단위의 값을 화면 높이 비율로 변환
        float progress = ImpactDistortionFx.Progress;
        float heightPixels = Mathf.Max(1f, camera.orthographicSize * 2f * PixelsPerUnit);
        Vector3 center = camera.WorldToViewportPoint(ImpactDistortionFx.WorldPoint);
        runtimeMaterial.SetVector(CenterId, new Vector4(center.x, center.y, 0f, 0f));
        runtimeMaterial.SetFloat(RingId, Mathf.Lerp(RingStartPixels, RingEndPixels, progress) / heightPixels);
        runtimeMaterial.SetFloat(WidthId, Mathf.Max(1f, RingWidthPixels) / heightPixels);
        runtimeMaterial.SetFloat(StrengthId, StrengthPixels * (1f - progress) / heightPixels);
        runtimeMaterial.SetFloat(AspectId, camera.aspect);
        passMaterial = runtimeMaterial;
        base.AddRenderPasses(renderer, ref renderingData);
    }

    /// <summary>패스와 함께 실행 중에 만든 재질 사본 정리</summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (runtimeMaterial == null) return;
        if (Application.isPlaying) Destroy(runtimeMaterial);
        else DestroyImmediate(runtimeMaterial);
        runtimeMaterial = null;
    }
}
