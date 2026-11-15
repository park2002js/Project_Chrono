using UnityEngine;

/// <summary>요청된 변위의 유효성·충돌을 확인하고 실제 물리 위치에 적용</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerBodyController : MonoBehaviour
{
    public LayerMask CollisionMask = -1; // 이동을 막는 일반 Collider의 레이어, Trigger는 제외
    public float SkinWidth = 0.01f; // 충돌면과 몸체 사이에 남겨 끼임을 줄이는 거리
    public Vector2 MoveDirection; // 충돌 처리 후 실제로 이동한 방향
    public Vector2 ActualMovement { get; private set; } // 마지막 Move 호출에서 충돌 보정 후 실제 적용한 변위

    private Rigidbody2D body; // 직접 위치를 이동하고 몸체 충돌을 검사할 Rigidbody2D
    private RaycastHit2D[] hits = new RaycastHit2D[16]; // 이동 경로의 충돌 결과를 담아 반복 사용하는 배열

    /// <summary>관성과 중력 없이 직접 이동시킬 2D 몸체 준비</summary>
    private void Awake()
    {
        // 실제 위치 적용과 충돌 조회에 사용할 자기 물리 컴포넌트 확보
        body = GetComponent<Rigidbody2D>();

        // 물리 엔진의 관성 대신 Character가 전달한 변위로만 위치 변경
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>Character의 Update에서 받은 변위를 같은 호출 안에서 충돌 보정·위치 적용</summary>
    public void Move(Vector2 displacement)
    {
        // 비활성 이동 대상과 물리 좌표를 손상시키는 변위 제외
        if (!isActiveAndEnabled || !IsFinite(displacement)) return;

        // 일반 이동과 대시가 계산된 프레임에 실제 몸체 위치 반영
        Vector2 origin = body.position;
        ApplyMovement(displacement);
        ActualMovement = body.position - origin;
        base.transform.position = new Vector3(body.position.x, body.position.y, base.transform.position.z);
        MoveDirection = ActualMovement.sqrMagnitude > 0.0000001f ? ActualMovement.normalized : Vector2.zero;
    }

    /// <summary>비활성화·세션 정리 때 마지막 이동 결과와 관성 초기화</summary>
    public void Stop()
    {
        // 이전 이동 결과가 정지 이후 남지 않도록 제거
        MoveDirection = Vector2.zero;
        ActualMovement = Vector2.zero;

        // 비활성화가 Awake보다 먼저 발생한 경우에도 마지막 이동 상태 정리
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    /// <summary>충돌 지점까지 이동하고 남은 변위를 충돌면에 투영</summary>
    private void ApplyMovement(Vector2 displacement)
    {
        // 이번 이동량에 적용할 충돌 검사 조건 준비
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(CollisionMask);
        filter.useTriggers = false;
        Vector2 remaining = displacement;

        // 최대 세 번 충돌을 보정하며 벽면을 따라 남은 거리 이동
        for (int pass = 0; pass < 3 && remaining.sqrMagnitude > 0.0000001f; pass++)
        {
            // 현재 경로에서 실제 이동을 막는 가장 가까운 충돌 선택
            float distance = remaining.magnitude;
            Vector2 direction = remaining / distance;
            int count = body.Cast(direction, filter, hits, distance + SkinWidth);
            float travel = distance;
            Vector2 normal = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Dot(direction, hits[i].normal) >= -0.0001f)
                    continue;
                float allowed = Mathf.Max(0f, hits[i].distance - SkinWidth);
                if (allowed < travel)
                {
                    travel = allowed;
                    normal = hits[i].normal;
                }
            }

            // 충돌 직전까지 이동하고 남은 변위에서 벽을 향한 성분 제거
            body.position += direction * travel;
            if (normal == Vector2.zero)
                break;
            remaining -= direction * travel;
            remaining -= normal * Mathf.Min(0f, Vector2.Dot(remaining, normal));
        }
    }

    /// <summary>물리 좌표를 손상시키는 NaN·Infinity 변위 거부</summary>
    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.sqrMagnitude);
    }
}
