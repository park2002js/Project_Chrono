using UnityEngine;

/// <summary>준비된 Player를 정지 시간과 무관하게 지연 추적하는 게임 카메라</summary>
[RequireComponent(typeof(Camera))]
public class GameCamera : MonoBehaviour
{
    public static GameCamera Instance; // 외부에서 게임 카메라와 추적 대상을 설정할 단일 창구
    public Camera Camera; // 화면 투영과 줌에 사용하는 이 객체의 Unity 카메라
    public Transform Target; // 외부에서 지정한 Player 또는 연출 대상의 Transform
    public float FollowDuration = 0.5f; // 일반 이동 중 추적 대상에 접근하는 위치 보간의 기준 시간(초)
    public float DrawFollowDuration = 0.3f; // Draw에서 커서 방향 변화에 대응할 위치 보간의 기준 시간(초)
    public float DefaultZoom = 5f; // 직교 카메라 초기 화면의 세로 절반 크기를 월드 단위로 설정
    public Vector2 VerticalLimits = new Vector2(-10000f, 10000f); // 완성 맵 위아래의 빈 바깥 공간을 노출하지 않을 카메라 범위
    public bool IsDrawMode { get; private set; } // Player와 커서의 가중 중심을 추적하는 Draw 표시 상태
    public bool IsSlashMode { get; private set; } // Player 중심에서 검격 줌을 적용하는 표시 상태
    public float SlashZoom = 4f; // Slash 동안 사용할 직교 줌의 초기 튜닝값
    public bool IsDeathMode { get; private set; } // 생성·사망 표시가 Player 초점을 소유한 상태
    public float DeathZoom = 3f; // 생성·사망 표현에서 사용할 직교 줌의 초기 튜닝값
    public float DrawAimWeight = 0.35f; // Draw에서 Player 위치에 섞을 커서 위치의 비율
    public float DrawZoom = 6.5f; // Draw 진입 후 사용할 직교 줌의 초기 튜닝값

    private Vector3 velocity; // SmoothDamp가 다음 프레임에 이어 사용할 추적 속도
    private Vector2 drawAim; // Draw 전용 입력에서 전달한 최신 월드 커서 위치
    private Transform previousTarget; // Draw 종료 때 되돌릴 진입 전 추적 대상
    private float previousZoom; // Draw 종료 때 되돌릴 진입 전 직교 줌

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

        // 카메라 깊이는 유지하고 대상의 평면 위치만 부드럽게 추적
        Vector3 position = Target.position;
        if (IsDrawMode)
            position = Vector2.Lerp(Target.position, drawAim, Mathf.Clamp01(DrawAimWeight));
        position.y = ClampY(position.y);
        position.z = transform.position.z;
        float duration = IsDrawMode ? DrawFollowDuration : FollowDuration;
        transform.position = Vector3.SmoothDamp(transform.position, position, ref velocity, Mathf.Max(0.01f, duration), Mathf.Infinity, Time.unscaledDeltaTime);
        if (IsDeathMode)
            Camera.orthographicSize = Mathf.MoveTowards(Camera.orthographicSize, Mathf.Max(0.1f, DeathZoom), Time.unscaledDeltaTime * 8f);
        else if (IsDrawMode)
            Camera.orthographicSize = Mathf.MoveTowards(Camera.orthographicSize, Mathf.Max(0.1f, DrawZoom), Time.unscaledDeltaTime * 8f);
        else if (IsSlashMode)
            Camera.orthographicSize = Mathf.MoveTowards(Camera.orthographicSize, Mathf.Max(0.1f, SlashZoom), Time.unscaledDeltaTime * 12f);

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
    public void Follow(Transform target, bool snap = false)
    {
        Target = target;
        velocity = Vector3.zero;
        if (snap && target != null)
            transform.position = new Vector3(target.position.x, ClampY(target.position.y), transform.position.z);
    }

    /// <summary>현재 줌의 화면 높이를 포함해 맵 위아래 경계 안으로 추적 중심 제한</summary>
    private float ClampY(float y)
    {
        float halfHeight = Camera.orthographicSize;
        return Mathf.Clamp(y, VerticalLimits.x + halfHeight, Mathf.Max(VerticalLimits.x + halfHeight, VerticalLimits.y - halfHeight));
    }

    /// <summary>Draw 시작 상태를 보관하고 편집 중 최신 커서의 가중 중심 적용</summary>
    public void SetDrawAim(bool enabled, Vector2 aim)
    {
        if (!enabled)
        {
            Restore();
            return;
        }
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
        velocity = Vector3.zero;
    }

    /// <summary>Slash의 Player 중심 줌을 시작하고 종료 때 원래 대상·줌 복구</summary>
    public void SetSlashMode(bool slashing, Transform player = null)
    {
        if (IsSlashMode == slashing) return;
        if (slashing)
        {
            previousTarget = Target;
            previousZoom = Camera.orthographicSize;
            if (player != null) Target = player;
            velocity = Vector3.zero;
        }
        else
        {
            Target = previousTarget;
            Camera.orthographicSize = previousZoom;
            previousTarget = null;
            velocity = Vector3.zero;
        }
        IsSlashMode = slashing;
    }

    /// <summary>요청자가 지정한 생성·사망 초점과 줌을 적용하고 이전 상태 복구</summary>
    public void SetDeathMode(bool enabled, Transform focus = null)
    {
        if (IsDeathMode == enabled) return;
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
        velocity = Vector3.zero;
        IsDeathMode = enabled;
    }
}
