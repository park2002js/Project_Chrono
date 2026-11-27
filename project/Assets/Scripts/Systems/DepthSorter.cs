using UnityEngine;
using UnityEngine.Rendering;

/// <summary>발끝의 세로 위치를 기준으로 캐릭터·구조물·탄환의 앞뒤를 가리는 공통 컴포넌트</summary>
[RequireComponent(typeof(SortingGroup))]
[DefaultExecutionOrder(2000)] // 이동과 연출이 모두 끝난 뒤 그 프레임의 위치로 앞뒤 결정
public class DepthSorter : MonoBehaviour
{
    public const int SharedOrder = 23; // 발끝 높이로 앞뒤를 가리는 모든 대상이 함께 쓰는 정렬 순서이며 그림자(14)보다 위, 캐릭터 위 효과(24 이상)보다 아래
    public const float DepthPerUnit = 0.001f; // 발끝이 세로로 1유닛 높아질 때마다 카메라에서 멀어지는 깊이

    public float FootOffset; // 기준점에서 발끝까지의 세로 거리이며 발끝이 기준점보다 아래면 음수
    public bool AlwaysInFront; // 공중에 뜬 동안처럼 발끝과 무관하게 다른 대상 앞에 그릴지 여부
    public Transform GroundPoint; // 몸이 떠서 움직이는 대상이 자기 위치 대신 발끝으로 삼을 바닥 지점이며 비우면 자기 위치 사용
    public float FootY => (GroundPoint != null ? GroundPoint.position.y : transform.position.y) + FootOffset; // 앞뒤를 정하는 발끝의 월드 세로 위치
    public bool HasOrderOverride { get; private set; } // 암전 위 강조처럼 공용 순서 대신 지정한 순서를 쓰는 상태

    private SortingGroup group; // 자식 그림 전체를 한 덩어리로 정렬할 자기 묶음
    private int overrideOrder; // 공용 순서 대신 쓸 정렬 순서

    /// <summary>자기 정렬 묶음을 한 번 확보</summary>
    private void Awake()
    {
        group = GetComponent<SortingGroup>();
    }

    private void OnEnable()
    {
        Apply();
    }

    /// <summary>이번 프레임의 이동이 끝난 위치로 앞뒤를 다시 정함</summary>
    private void LateUpdate()
    {
        Apply();
    }

    /// <summary>공용 순서 대신 지정한 순서로 묶음 전체를 그리며 암전 위 강조에 사용</summary>
    public void SetOrderOverride(int order)
    {
        HasOrderOverride = true;
        overrideOrder = order;
        Apply();
    }

    /// <summary>지정한 순서를 풀고 발끝 기준의 공용 순서로 복귀</summary>
    public void ClearOrderOverride()
    {
        HasOrderOverride = false;
        Apply();
    }

    /// <summary>묶음의 정렬 순서를 맞추고 발끝이 높을수록 카메라에서 멀어지도록 깊이 지정</summary>
    private void Apply()
    {
        if (group == null) group = GetComponent<SortingGroup>();

        // 같은 순서를 쓰는 대상끼리는 카메라에 가까운 쪽이 위에 그려짐
        int order = HasOrderOverride ? overrideOrder : SharedOrder + (AlwaysInFront ? 1 : 0);
        if (group.sortingOrder != order) group.sortingOrder = order;

        // 가로·세로 위치는 그대로 두고 깊이만 발끝 높이에 맞춤
        Vector3 position = transform.position;
        float depth = FootY * DepthPerUnit;
        if (Mathf.Abs(position.z - depth) > 0.000001f) transform.position = new Vector3(position.x, position.y, depth);
    }

    /// <summary>바닥에 깔리는 그림자처럼 묶음 안에 두되 앞뒤 정렬에서는 빼고 고정 순서로 그릴 자식 설정</summary>
    public static void DrawOnGround(GameObject child, int order)
    {
        // 부모 묶음과 무관하게 전체 기준으로 정렬되는 별도 묶음으로 분리
        if (!child.TryGetComponent(out SortingGroup ground)) ground = child.AddComponent<SortingGroup>();
        ground.sortAtRoot = true;
        ground.sortingOrder = order;
    }
}
