using System;
using UnityEngine;

/// <summary>원형 공격의 실제 지면 범위를 옅은 채움과 진한 경계선으로 표시</summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class CircleWarningVfx : MonoBehaviour
{
    public float OutlineWidth = 0.04f; // 원형 경계를 구분할 선의 로컬 폭
    [Range(0f, 1f)] public float FillOpacityMultiplier = 0.45f; // 경계선보다 옅게 표시할 내부 채움의 알파 비율
    public MeshRenderer FillRenderer; // 단위 원판 에셋을 공유하는 내부 채움 표시

    private const int SegmentCount = 96; // 반경에 관계없이 재사용할 원형 선의 꼭짓점 수
    private static readonly int ColorId = Shader.PropertyToID("_Color"); // 공용 재질 변경 없이 개별 채움색을 전달할 식별자
    private readonly Vector3[] points = new Vector3[SegmentCount]; // 반경 설정 때 다시 채울 원 둘레 좌표
    private LineRenderer outline; // 같은 경고 객체에서 계속 재사용할 선 표시
    private Transform fillTransform; // 반경 변경 때 단위 원판의 크기를 맞출 표시 Transform
    private MaterialPropertyBlock fillProperties; // 같은 원판에서 채움색만 바꿀 재사용 프로퍼티

    /// <summary>자기 선과 공유 원판의 개별 표시 참조를 최초 한 번 준비</summary>
    private void Awake()
    {
        PrepareVisuals();
    }

    private void OnEnable()
    {
        // 프리팹에 저장되지 않는 개별 채움색을 직렬화된 선 색에서 복원
        PrepareVisuals();
        SetColor(outline.startColor);
    }

    /// <summary>실제 원형 판정과 같은 반경 및 현재 경고색 적용</summary>
    public void Configure(float radius, Color color)
    {
        // 판정 크기와 다른 표시나 잘못된 선 폭을 생성하기 전에 검사
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f
            || float.IsNaN(OutlineWidth) || float.IsInfinity(OutlineWidth) || OutlineWidth <= 0f
            || float.IsNaN(FillOpacityMultiplier) || float.IsInfinity(FillOpacityMultiplier)
            || FillOpacityMultiplier < 0f || FillOpacityMultiplier > 1f)
            throw new ArgumentException("A circle warning requires positive finite geometry and a fill opacity ratio from zero to one.");
        PrepareVisuals();

        // 같은 좌표 배열로 원을 닫고 선 중심을 실제 피해 경계에 정렬
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.PI * 2f * i / points.Length;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
        outline.positionCount = points.Length;
        outline.SetPositions(points);
        outline.startWidth = outline.endWidth = OutlineWidth;
        if (fillTransform != null)
        {
            fillTransform.localScale = new Vector3(radius, radius, 1f);
            FillRenderer.sortingLayerID = outline.sortingLayerID;
            FillRenderer.sortingOrder = outline.sortingOrder - 1;
        }
        SetColor(color);
    }

    /// <summary>원형 좌표를 재생성하지 않고 경고 및 활성 색만 전환</summary>
    public void SetColor(Color color)
    {
        PrepareVisuals();
        outline.startColor = outline.endColor = color;
        if (FillRenderer == null) return;

        // 경계선 색을 유지하면서 내부만 옅게 표시하고 공용 재질은 보존
        color.a *= FillOpacityMultiplier;
        fillProperties.SetColor(ColorId, color);
        FillRenderer.SetPropertyBlock(fillProperties);
    }

    /// <summary>런타임과 Editor 프리팹 준비에서 같은 선 참조를 한 번 확보</summary>
    private void PrepareVisuals()
    {
        // 자기 선 참조는 최초 설정 이후 계속 재사용
        if (outline == null)
        {
            outline = GetComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.alignment = LineAlignment.TransformZ;
        }

        // 원판은 프리팹의 공유 Mesh를 사용하며 수명마다 복제하지 않음
        if (fillTransform == null && FillRenderer != null)
        {
            fillTransform = FillRenderer.transform;
            fillProperties = new MaterialPropertyBlock();
            FillRenderer.GetPropertyBlock(fillProperties);
        }
    }
}
