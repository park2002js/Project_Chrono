using System;
using UnityEngine;

/// <summary>피해 판정과 분리해 표시할 지면 경고의 형태</summary>
public enum WarningShape
{
    Circle, // 가로·세로 Scale로 타원까지 표현할 원형
    Rectangle, // 가로·세로 Scale로 크기를 맞출 사각형
    Sector, // 중심각과 Scale로 범위를 맞출 부채꼴
    OpenStrip // 긴 양쪽 경계선만 표시할 직선 띠
}

/// <summary>피해 없이 원·사각형·부채꼴·직선 띠의 반투명 지면 예고를 표시</summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class AreaWarningVfx : MonoBehaviour
{
    public WarningShape Shape; // 같은 표시 객체에서 사용할 지면 경고 형태
    [Range(0.1f, 360f)] public float SectorAngle = 90f; // 위쪽을 중심으로 펼칠 부채꼴의 전체 각도
    public Color WarningColor = new Color(1f, 0.12f, 0.16f, 0.45f); // 경계선에 적용할 반투명 빨간 경고색
    public float OutlineWidth = 0.04f; // 기본 단위에서 경계를 구분할 선의 폭
    [Range(0f, 1f)] public float FillOpacityMultiplier = 0.45f; // 경계선보다 옅게 표시할 내부 채움 알파 비율
    public MeshRenderer FillRenderer; // 경고 형태의 내부 채움을 표시할 자식 렌더러
    public LineRenderer SecondaryOutline; // 직선 띠에서 반대쪽 긴 경계를 표시할 두 번째 선
    public Mesh CircleMesh; // 모든 원형 예고가 공유할 지름1의 흰 원판 에셋
    public Mesh RectangleMesh; // 사각형과 직선 띠가 공유할 가로·세로1의 흰 사각 에셋

    private const int SegmentCount = 96; // 원과 부채꼴의 곡선에 재사용할 구간 수
    private static readonly int ColorId = Shader.PropertyToID("_Color"); // 공용 재질을 바꾸지 않고 채움색을 전달할 식별자
    private readonly Vector3[] circlePoints = new Vector3[SegmentCount]; // 단위 원의 경계 좌표 버퍼
    private readonly Vector3[] sectorPoints = new Vector3[SegmentCount + 2]; // 중심과 양 끝점을 포함한 부채꼴 좌표 버퍼
    private readonly Vector3[] rectanglePoints = { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) }; // 단위 사각형의 네 경계점
    private readonly Vector3[] stripPoints = { new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) }; // 직선 띠의 위쪽 긴 경계점
    private readonly Vector3[] oppositeStripPoints = { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f) }; // 직선 띠의 아래쪽 긴 경계점
    private LineRenderer outline; // 같은 경고 객체에서 계속 재사용할 주 경계선
    private MeshFilter fillFilter; // 같은 채움 객체에서 형태별 Mesh를 연결할 참조
    private MaterialPropertyBlock fillProperties; // 같은 렌더러의 채움색에 재사용할 개별 프로퍼티
    private Mesh sectorMesh; // 각도 변경 때 재사용하고 자기 수명 종료에서 제거할 부채꼴 Mesh
    private bool secondaryPrepared; // 두 번째 선의 고정 설정을 준비했는지 보관
    private bool refreshRequested; // Inspector 변경을 다음 유효 표시 갱신으로 넘길 상태

    /// <summary>자기 선과 직렬화된 채움 참조를 최초 한 번 확보</summary>
    private void Awake()
    {
        PrepareVisuals();
    }

    private void OnEnable()
    {
        if (ReferencesReady()) Configure(Shape, WarningColor, SectorAngle);
    }

    /// <summary>Inspector 변경이 요청된 경우에만 기하와 색 갱신</summary>
    private void Update()
    {
        if (refreshRequested && ReferencesReady()) Configure(Shape, WarningColor, SectorAngle);
    }

    private void OnValidate()
    {
        // OnValidate 안에서 Unity Mesh 생성 없이 다음 Editor 표시 갱신 예약
        SectorAngle = Mathf.Clamp(SectorAngle, 0.1f, 360f);
        OutlineWidth = Mathf.Max(0.001f, OutlineWidth);
        FillOpacityMultiplier = Mathf.Clamp01(FillOpacityMultiplier);
        refreshRequested = true;
    }

    private void OnDestroy()
    {
        // 공유 에셋은 유지하고 이 경고가 소유한 가변 부채꼴 Mesh만 정리
        if (sectorMesh == null) return;
        if (Application.isPlaying) Destroy(sectorMesh);
        else DestroyImmediate(sectorMesh);
    }

    /// <summary>기본 단위 크기의 경고 형태·색·부채꼴 각도를 적용</summary>
    public void Configure(WarningShape shape, Color color, float sectorAngle = 90f)
    {
        // 실제 사용 전에 필수 표시 참조와 유효한 설정 확인
        if (!ReferencesReady(shape) || !Positive(OutlineWidth) || !Positive(sectorAngle) || sectorAngle > 360f
            || float.IsNaN(FillOpacityMultiplier) || float.IsInfinity(FillOpacityMultiplier)
            || FillOpacityMultiplier < 0f || FillOpacityMultiplier > 1f)
            throw new ArgumentException("A warning requires its shared meshes, renderers and finite geometry settings.");
        Shape = shape;
        SectorAngle = sectorAngle;
        PrepareVisuals();
        refreshRequested = false;

        // 직선 띠 이외에는 하나의 닫힌 경계선으로 표시
        outline.startWidth = outline.endWidth = OutlineWidth;
        if (SecondaryOutline != null)
        {
            SecondaryOutline.enabled = shape == WarningShape.OpenStrip;
            SecondaryOutline.startWidth = SecondaryOutline.endWidth = OutlineWidth;
        }
        switch (shape)
        {
            case WarningShape.Circle:
                ConfigureCircle();
                break;
            case WarningShape.Rectangle:
                fillFilter.sharedMesh = RectangleMesh;
                SetOutline(rectanglePoints, true);
                break;
            case WarningShape.Sector:
                if (sectorAngle == 360f) ConfigureCircle();
                else ConfigureSector(sectorAngle);
                break;
            case WarningShape.OpenStrip:
                fillFilter.sharedMesh = RectangleMesh;
                SetOutline(stripPoints, false);
                SecondaryOutline.positionCount = oppositeStripPoints.Length;
                SecondaryOutline.SetPositions(oppositeStripPoints);
                SecondaryOutline.loop = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }

        // 기존 Transform 크기·회전은 유지하고 내부를 경계선보다 아래에 표시
        FillRenderer.sortingLayerID = outline.sortingLayerID;
        FillRenderer.sortingOrder = outline.sortingOrder - 1;
        if (SecondaryOutline != null)
        {
            SecondaryOutline.sortingLayerID = outline.sortingLayerID;
            SecondaryOutline.sortingOrder = outline.sortingOrder;
        }
        SetColor(color);
    }

    /// <summary>기하와 공용 재질을 바꾸지 않고 경계와 내부 색만 갱신</summary>
    public void SetColor(Color color)
    {
        PrepareVisuals();
        WarningColor = color;
        outline.startColor = outline.endColor = color;
        if (SecondaryOutline != null) SecondaryOutline.startColor = SecondaryOutline.endColor = color;
        if (FillRenderer == null) return;
        color.a *= FillOpacityMultiplier;
        fillProperties.SetColor(ColorId, color);
        FillRenderer.SetPropertyBlock(fillProperties);
    }

    /// <summary>형태에 필요한 직렬화 참조가 모두 연결됐는지 확인</summary>
    private bool ReferencesReady(WarningShape? shape = null)
    {
        return FillRenderer != null && CircleMesh != null && RectangleMesh != null
            && ((shape ?? Shape) != WarningShape.OpenStrip || SecondaryOutline != null);
    }

    /// <summary>고정 참조와 개별 색 프로퍼티를 한 번 확보</summary>
    private void PrepareVisuals()
    {
        // 주 경계선은 자기 컴포넌트에서 최초 한 번 준비
        if (outline == null)
        {
            outline = GetComponent<LineRenderer>();
            PrepareLine(outline);
        }

        // 직선 띠의 두 번째 선과 채움 참조는 연결된 뒤 계속 재사용
        if (SecondaryOutline != null && !secondaryPrepared)
        {
            PrepareLine(SecondaryOutline);
            secondaryPrepared = true;
        }
        if (fillFilter == null && FillRenderer != null)
        {
            fillFilter = FillRenderer.GetComponent<MeshFilter>();
            if (fillFilter == null) throw new InvalidOperationException("A warning fill requires MeshFilter.");
            fillProperties = new MaterialPropertyBlock();
            FillRenderer.GetPropertyBlock(fillProperties);
        }
    }

    /// <summary>두 경계선이 같은 로컬 지면과 끝점 표시를 사용하도록 준비</summary>
    private static void PrepareLine(LineRenderer line)
    {
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
    }

    /// <summary>지름1의 공유 원판과 동일한 원 둘레 연결</summary>
    private void ConfigureCircle()
    {
        for (int i = 0; i < circlePoints.Length; i++)
        {
            float angle = Mathf.PI * 2f * i / circlePoints.Length;
            circlePoints[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.5f;
        }
        fillFilter.sharedMesh = CircleMesh;
        SetOutline(circlePoints, true);
    }

    /// <summary>위쪽을 중심으로 반경0.5의 부채꼴 기하 갱신</summary>
    private void ConfigureSector(float angle)
    {
        // 고정 버퍼에 중심과 양 끝점을 포함한 원호 좌표 기록
        sectorPoints[0] = Vector3.zero;
        for (int i = 0; i <= SegmentCount; i++)
        {
            float radians = (90f - angle * 0.5f + angle * i / SegmentCount) * Mathf.Deg2Rad;
            sectorPoints[i + 1] = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 0.5f;
        }

        // 가변 원판은 자기 수명에 한 번만 만들고 이후 각도 변경에 재사용
        if (sectorMesh == null)
        {
            sectorMesh = new Mesh { name = "WarningSector", hideFlags = HideFlags.DontSave };
            sectorMesh.MarkDynamic();
            sectorMesh.vertices = sectorPoints;
            var colors = new Color[sectorPoints.Length];
            var triangles = new int[SegmentCount * 3];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            for (int i = 0; i < SegmentCount; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            sectorMesh.colors = colors;
            sectorMesh.triangles = triangles;
        }
        else sectorMesh.vertices = sectorPoints;
        sectorMesh.RecalculateBounds();
        fillFilter.sharedMesh = sectorMesh;
        SetOutline(sectorPoints, true);
    }

    /// <summary>준비한 좌표 버퍼로 주 경계선의 열린 형태 또는 닫힌 형태 적용</summary>
    private void SetOutline(Vector3[] points, bool closed)
    {
        outline.positionCount = points.Length;
        outline.SetPositions(points);
        outline.loop = closed;
    }

    /// <summary>기하 설정의 양수 유한값 확인</summary>
    private static bool Positive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
