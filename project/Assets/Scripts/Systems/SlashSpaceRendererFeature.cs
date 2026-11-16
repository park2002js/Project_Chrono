using UnityEngine.Rendering.Universal;

/// <summary>UI 합성 전에 현재 Slash 소유자의 월드 카메라 화면만 절단 재질로 처리</summary>
public class SlashSpaceRendererFeature : FullScreenPassRendererFeature
{
    /// <summary>투명한 월드 Sprite까지 그린 뒤 처리할 URP 기본 전체화면 패스 준비</summary>
    public override void Create()
    {
        injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
        fetchColorBuffer = true;
        requirements = ScriptableRenderPassInput.None;
        base.Create();
    }

    /// <summary>준비된 Player의 실제 공간참 동안에만 해당 월드 카메라 패스 실행</summary>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        SlashPresentationFx fx = SlashPresentationFx.ActiveSpaceSlash;
        if (fx == null || !fx.IsSpaceSlashPlaying || fx.BoundCamera != renderingData.cameraData.camera) return;
        fx.UpdateSpaceMaterial();
        passMaterial = fx.SpaceMaterial;
        base.AddRenderPasses(renderer, ref renderingData);
    }
}
