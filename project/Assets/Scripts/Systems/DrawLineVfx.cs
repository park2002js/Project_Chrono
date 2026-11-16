using UnityEngine;

/// <summary>경로 기하와 분리한 선 폭·알파를 이미지 FX에 적용</summary>
[RequireComponent(typeof(LineRenderer))]
public class DrawLineVfx : MonoBehaviour
{
    [Min(0f)] public float WidthMultiplier = 1f; // 선 오라 표시와 Slash 전체 판정 폭을 계산할 두께 배율
    public float Opacity { get; private set; } = 1f; // 경고에서 원래 이미지 알파에 곱할 현재 투명도

    private static readonly int OpacityId = Shader.PropertyToID("_Opacity"); // 공용 재질 변경 없이 전달할 알파 프로퍼티 식별자
    private LineRenderer line; // Awake에서 한 번 확보해 재사용할 선 렌더러
    private MaterialPropertyBlock properties; // 같은 렌더러에서 계속 재사용할 개별 표현 프로퍼티

    /// <summary>데이터·입력과 분리된 자기 표현 컴포넌트 준비</summary>
    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        properties = new MaterialPropertyBlock();
        line.GetPropertyBlock(properties);
        ApplyVisualProperties();
    }

    private void OnEnable()
    {
        ApplyWidth();
        ApplyVisualProperties();
    }

    /// <summary>게임 정지 중에도 바뀐 경로 폭을 실제 렌더링에 반영</summary>
    private void LateUpdate()
    {
        ApplyWidth();
    }

    /// <summary>개체별 알파만 갱신해 원본 텍스처와 다른 선의 표현 보존</summary>
    public void SetOpacity(float opacity)
    {
        Opacity = Mathf.Clamp01(opacity);
        ApplyVisualProperties();
    }

    /// <summary>경로의 판정 폭에 표시 전용 배율을 곱해 실제 선 메시 폭 반영</summary>
    private void ApplyWidth()
    {
        if (line != null)
            line.widthMultiplier = Mathf.Abs(transform.lossyScale.y) * Mathf.Max(0f, WidthMultiplier);
    }

    /// <summary>Awake에서 준비한 블록으로 해당 렌더러의 표현 값만 전달</summary>
    private void ApplyVisualProperties()
    {
        if (line == null || properties == null) return;
        properties.SetFloat(OpacityId, Opacity);
        line.SetPropertyBlock(properties);
    }
}
