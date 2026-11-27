using UnityEngine;

/// <summary>일반 대상은 지연 추적하고 Draw에서는 Player와 마우스 조준에 즉시 반응하는 게임 카메라</summary>
[RequireComponent(typeof(Camera))]
public class GameCamera : MonoBehaviour
{
    public static GameCamera Instance; // 외부에서 게임 카메라와 추적 대상을 설정할 단일 창구
    public Camera Camera; // 화면 투영과 줌에 사용하는 이 객체의 Unity 카메라
    public Transform Target; // 외부에서 지정한 Player 또는 연출 대상의 Transform
    [Range(0f, 1f)] public float FollowFraction = 0.15f; // 60fps에서 Player까지 남은 거리 중 한 프레임에 이동할 비율
    [Range(0f, 1f)] public float FocusFraction = 1f / 15f; // 60fps에서 임시 초점까지 남은 거리 중 한 프레임에 이동할 비율
    public float DefaultZoom = 5.625f; // 16:9 일반 전투 시야 20×11.25유닛의 반높이
    public Vector2 VerticalLimits = new Vector2(-10000f, 10000f); // 완성 맵 위아래의 빈 바깥 공간을 노출하지 않을 카메라 범위
    public bool IsDrawMode { get; private set; } // Player 기준 마우스 조준 오프셋을 적용하는 Draw 표시 상태
    public bool IsSlashMode { get; private set; } // Player 중심에서 검격 줌을 적용하는 표시 상태
    public float SlashFastZoom = 3.2f; // 빠른 확대가 끝나고 느린 확대를 시작할 직교 줌의 초기 튜닝값
    public float SlashZoom = 2.8f; // 느린 확대가 도달할 직교 줌의 초기 튜닝값
    public float SlashStartZoom => previousZoom; // Slash 진입 전에 보관해 복귀 목표로 사용할 직교 줌
    public bool IsDeathMode { get; private set; } // 생성·사망 표시가 Player 초점을 소유한 상태
    public float DeathZoom = 3f; // 생성·사망 표현에서 사용할 직교 줌의 초기 튜닝값
    public float DrawZoom = 6.5f; // Draw 진입 후 사용할 직교 줌의 초기 튜닝값

    private bool temporaryFocus; // 보스 등 임시 대상에 적용하는 추적 속도 선택
    private Vector2 drawAim; // Draw 전용 입력에서 전달한 최신 월드 커서 위치
    private Transform previousTarget; // Draw 종료 때 되돌릴 진입 전 추적 대상
    private float previousZoom; // Draw 종료 때 되돌릴 진입 전 직교 줌
    private float deathReturn; // 생성·사망의 가까운 시야에서 진입 전 시야로 돌아온 정도(0 가까운 시야, 1 진입 전 시야)

    /// <summary>다른 객체를 조회하지 않고 단일 인스턴스와 자신의 카메라를 준비</summary>
    private void Awake()
    {
        // 기존 카메라의 대상과 Instance를 보존하고 중복 객체 제거
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // 전역 조회 창구와 자체 카메라의 기본 투영 설정
        Instance = this;
        Camera = GetComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = DefaultZoom;
    }

    /// <summary>Player 이동 후 unscaled time으로 카메라 위치를 갱신</summary>
    private void LateUpdate()
    {
        // 대상이 없을 때 현재 화면 위치 유지
        if (Target == null)
            return;

        // Draw 마우스는 Gungeon의 거리별 오프셋에 즉시 반응하고 일반 대상만 지연 추적
        Vector3 position = Target.position;
        if (IsDrawMode)
            position += (Vector3)GetDrawOffset(position);
        position.y = ClampY(position.y);
        position.z = transform.position.z;
        if (IsDrawMode)
            transform.position = position;
        else
        {
            float fraction = temporaryFocus ? FocusFraction : FollowFraction;
            float blend = 1f - Mathf.Pow(1f - Mathf.Clamp01(fraction), 60f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, position, blend);
        }

        // Draw·생성·사망의 화면 크기는 게임 정지와 무관한 실제 시간으로 전환
        if (IsDeathMode)
            Camera.orthographicSize = deathReturn > 0f ? Mathf.Lerp(Mathf.Max(0.1f, DeathZoom), previousZoom, deathReturn)
                : Mathf.MoveTowards(Camera.orthographicSize, Mathf.Max(0.1f, DeathZoom), Time.unscaledDeltaTime * 8f);
        else if (IsDrawMode)
            Camera.orthographicSize = Mathf.MoveTowards(Camera.orthographicSize, Mathf.Max(0.1f, DrawZoom), Time.unscaledDeltaTime * 8f);

        // 생성·사망·Draw의 줌 복구 직후에도 실제 화면이 맵 경계를 넘지 않도록 보정
        Vector3 bounded = transform.position;
        bounded.y = ClampY(bounded.y);
        transform.position = bounded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>PlayerSpawner 또는 연출 코드가 초점을 맞출 대상을 지정하거나 해제</summary>
    public void Follow(Transform target, bool snap = false, bool temporary = false)
    {
        // 일반 추적으로 돌아올 때는 Player의 추적 비율을 바로 복구
        Target = target;
        temporaryFocus = temporary;
        if (snap && target != null)
            transform.position = new Vector3(target.position.x, ClampY(target.position.y), transform.position.z);
    }

    /// <summary>월드의 한 지점이 현재 화면 가장자리에서 여백만큼 안쪽에 들어와 있는지 조회</summary>
    public bool IsVisible(Vector2 point, float margin = 0f)
    {
        float halfHeight = Camera.orthographicSize - margin, halfWidth = Camera.orthographicSize * Camera.aspect - margin;
        Vector2 center = transform.position;
        return Mathf.Abs(point.x - center.x) <= halfWidth && Mathf.Abs(point.y - center.y) <= halfHeight;
    }

    /// <summary>현재 줌의 화면 높이를 포함해 맵 위아래 경계 안으로 추적 중심 제한</summary>
    private float ClampY(float y)
    {
        float halfHeight = Camera.orthographicSize;
        return Mathf.Clamp(y, VerticalLimits.x + halfHeight, Mathf.Max(VerticalLimits.x + halfHeight, VerticalLimits.y - halfHeight));
    }

    /// <summary>Draw 시작 상태를 보관하고 편집 중 최신 월드 커서 전달</summary>
    public void SetDrawAim(bool enabled, Vector2 aim)
    {
        // 종료 신호는 커서 값과 무관하게 자신이 보관한 카메라 상태 복구
        if (!enabled)
        {
            Restore();
            return;
        }

        // 유효한 좌표만 수락하고 최초 진입에서 복귀 대상과 줌 보관
        if (float.IsNaN(aim.x) || float.IsInfinity(aim.x) || float.IsNaN(aim.y) || float.IsInfinity(aim.y)) return;
        if (!IsDrawMode)
        {
            previousTarget = Target;
            previousZoom = Camera.orthographicSize;
            IsDrawMode = true;
        }
        drawAim = aim;
    }

    /// <summary>Draw가 바꾼 추적 대상과 줌만 진입 전 값으로 복구</summary>
    public void Restore()
    {
        if (!IsDrawMode) return;
        IsDrawMode = false;
        Target = previousTarget;
        Camera.orthographicSize = previousZoom;
        previousTarget = null;
    }

    /// <summary>Slash의 Player 중심 줌을 시작하고 종료 때 원래 대상·줌 복구</summary>
    public void SetSlashMode(bool slashing, Transform player = null)
    {
        // 최초 진입과 종료에서만 추적 대상·복귀 줌을 보관하거나 복원
        if (IsSlashMode == slashing) return;
        if (slashing)
        {
            previousTarget = Target;
            previousZoom = Camera.orthographicSize;
            if (player != null) Target = player;
        }
        else
        {
            Target = previousTarget;
            Camera.orthographicSize = previousZoom;
            previousTarget = null;
        }
        IsSlashMode = slashing;
    }

    /// <summary>Skill이 계산한 빠른 확대·느린 확대·복귀의 현재 줌을 직접 반영</summary>
    public void SetSlashZoom(float zoom)
    {
        // Slash의 확대·복귀는 Skill에서 보낸 현재 샘플로만 진행
        if (!IsSlashMode || float.IsNaN(zoom) || float.IsInfinity(zoom)) return;
        Camera.orthographicSize = Mathf.Max(0.1f, zoom);

        // 같은 프레임의 화면에서도 확대 크기에 맞는 맵 경계 유지
        Vector3 position = transform.position;
        position.y = ClampY(position.y);
        transform.position = position;
    }

    /// <summary>요청자가 지정한 생성·사망 초점과 줌을 적용하고 이전 상태 복구</summary>
    public void SetDeathMode(bool enabled, Transform focus = null)
    {
        if (IsDeathMode == enabled) return;
        deathReturn = 0f;
        if (enabled)
        {
            previousTarget = Target;
            previousZoom = Camera.orthographicSize;
            if (focus != null) Target = focus;
        }
        else
        {
            Target = previousTarget;
            Camera.orthographicSize = previousZoom;
            previousTarget = null;
        }
        IsDeathMode = enabled;
    }

    /// <summary>생성 표현의 가까운 시야를 확대 과정 없이 첫 화면부터 적용</summary>
    public void SnapDeathZoom()
    {
        if (!IsDeathMode) return;
        Camera.orthographicSize = Mathf.Max(0.1f, DeathZoom);
        if (Target != null) transform.position = new Vector3(Target.position.x, ClampY(Target.position.y), transform.position.z);
    }

    /// <summary>생성 표현을 끝내기 전에 가까운 시야에서 진입 전 시야로 돌아온 정도를 반영</summary>
    public void SetDeathZoomReturn(float blend)
    {
        if (!IsDeathMode || float.IsNaN(blend) || float.IsInfinity(blend)) return;
        deathReturn = Mathf.Clamp01(blend);
    }

    /// <summary>Gungeon의 마우스 전용 화면비 보정·근거리 무반응·거리 가중치·축별 상한 계산</summary>
    private Vector2 GetDrawOffset(Vector2 playerPosition)
    {
        // 가로로 넓은 화면에서 수평 조준만 과하게 카메라를 밀지 않도록 화면비 보정
        float halfHeight = Camera.orthographicSize;
        Vector2 offset = drawAim - playerPosition;
        offset.x /= Mathf.Max(0.01f, Camera.aspect);
        float limit = halfHeight * 1.5f;
        offset.x = Mathf.Clamp(offset.x, -limit, limit);
        offset.y = Mathf.Clamp(offset.y, -limit, limit);

        // Player 가까이는 고정하고 멀수록 최대 1/3까지 반영하며 미세 잔여 이동 제거
        float distanceWeight = Mathf.Sqrt(Mathf.Clamp01((offset.magnitude - 1f) / Mathf.Max(0.01f, halfHeight - 1f)));
        offset *= 0.33333f * distanceWeight;
        return offset.magnitude < 1f / 64f ? Vector2.zero : offset;
    }
}
