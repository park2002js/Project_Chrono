using System;
using UnityEngine;

/// <summary>Slash에 대한 구체 탄환의 반응 구분</summary>
public enum ProjectileSlashResponse
{
    Destroy, // Slash 판정에서 제거
    Reflect, // Slash Line 기준 반사각으로 방향을 바꾸고 각성 획득
    Ignore // Slash 반응 없음
}

/// <summary>구체 탄환의 게임 시간 이동·피해·수명과 일회 Slash 반응</summary>
public class Projectile : MonoBehaviour
{
    public BossCharacter Owner; // 최초 발사자이자 반사 뒤 피해를 받을 Boss
    public float Damage = 10f; // 탄환이 Player에 전달할 피해
    public float ReflectDamage = 100f; // 반사된 뒤 소유 Boss의 피격 영역에 닿았을 때 전달할 피해
    public Vector2 Direction = Vector2.right; // 비행 중 현재 운동 방향이며 반사 순간 계산한 방향으로 교체
    public float Speed = 4f; // 일반 탄환의 초당 월드 이동 거리
    public float Lifetime = 8f; // 생성 이후 유지할 게임 시간이며 0은 시간 만료 없이 유지
    public ProjectileSlashResponse SlashResponse = ProjectileSlashResponse.Destroy; // 이 탄환을 Slash로 처리하는 방식
    public float GroggyGain = 10f; // 성공한 Slash 반응으로 획득할 그로기 수치
    public bool GrantsFever = true; // 금색 반사 성공에서 각성 획득을 요청할지 설정
    public BossCharacter ReflectionTarget { get; private set; } // 반사 뒤 실제 피격 영역에 닿았을 때 피해를 받을 Boss
    public float ReflectionSpeed = 20f; // 계산한 반사 방향으로 직진할 초당 월드 이동 거리
    public float CollisionRadius = 0.08f; // 빠른 이동 사이의 Player 판정을 조회할 반경
    public LayerMask CollisionMask = ~0; // 이동 경로의 Player 피격 영역과 맵 벽을 조회할 레이어
    public Transform Visual; // 스미어가 있는 탄환에서 진행 방향으로 돌릴 표시 자식
    public bool FaceDirection; // 표시 자식의 +X를 현재 운동 방향에 맞출지 설정
    public BoxCollider2D SlashTrail; // 고속탄이 한 프레임에 지나간 구간을 Slash에만 노출할 후방 판정
    public float TrailWidth = 1.125f; // 후방 판정의 폭이며 탄 몸체 지름과 같게 설정
    public bool IsReflected { get; private set; } // Player 피해와 중복 Slash 보상을 차단할 반사 상태
    public bool IsActive { get; private set; } = true; // 수명·피해·보상 처리의 일회 종료 기준
    public float RemainingLifetime { get; private set; } // 시간 만료까지 남은 게임 시간이며 무기한 발사는 0 유지

    private PlayerCharacter reflector; // 반사 피해의 출처로 유지할 Player
    private MapManager map; // 발사 경계에서 한 번 연결한 이번 맵의 벽 판별 담당
    protected Collider2D shape; // 구체 탄환이 준비하고 종료 즉시 닫을 자기 Collider
    private ContactFilter2D filter; // Trigger를 포함한 이동 경로 조회 조건
    private RaycastHit2D[] hits = new RaycastHit2D[8]; // 이동 중 피격 질의에 재사용할 결과 버퍼
    protected int attackId; // 이번 발사에서 여러 판정을 하나로 묶을 피해 식별자

    /// <summary>자기 Collider와 재사용 질의 조건을 생성 시 한 번 준비</summary>
    protected virtual void Awake()
    {
        // 구체 탄환의 형태를 준비한 뒤 같은 수명·종료 경로에 연결
        shape = PrepareShape();
        shape.isTrigger = true;
        RemainingLifetime = Lifetime;
        filter = new ContactFilter2D { useTriggers = true };
        attackId = DamageInstance.NextAttackId();
    }

    /// <summary>게임 시간으로 수명·이동·피해를 진행하고 정지 중에는 현재 상태 유지</summary>
    private void Update()
    {
        // 종료·정지와 유효하지 않은 이동 설정 제외
        float elapsed = Time.deltaTime;
        if (!IsActive || Time.timeScale <= 0f || elapsed <= 0f) return;
        if (!Nonnegative(Speed) || !Nonnegative(Damage) || !Nonnegative(ReflectDamage) || !Nonnegative(Lifetime) || !Nonnegative(RemainingLifetime) || !Finite(Direction))
        {
            Despawn();
            return;
        }
        // 0 수명은 시간만료 없이 다른 적중·Slash·전투 정리 경계까지 유지
        if (Lifetime > 0f)
        {
            RemainingLifetime = Mathf.Max(0f, RemainingLifetime - elapsed);
            if (RemainingLifetime <= 0f)
            {
                Despawn();
                return;
            }
        }

        // 반사 탄은 Player 피해 없이 계산한 방향으로 직진하며 Boss 피격과 벽 확인
        if (IsReflected)
        {
            UpdateReflection(elapsed);
            return;
        }

        // 구체 탄환은 비행 또는 고정 위험 영역의 진행만 담당
        TickFlight(elapsed);
    }

    /// <summary>발사자가 준비한 비행 수치를 적용하고 수명 시작</summary>
    public virtual void Initialize(BossCharacter owner, Vector2 direction, float speed, float lifetime, float damage)
    {
        // 잘못된 발사 데이터는 아직 활성 상태를 바꾸기 전에 거부
        if (owner == null || !Finite(direction) || direction.sqrMagnitude <= 0f || !Nonnegative(speed)
            || !Nonnegative(lifetime) || !Nonnegative(damage))
            throw new ArgumentException("Projectile requires an owner, direction and finite flight settings.");

        // 같은 탄환의 발사 정보와 일회 반응 상태를 함께 준비
        Owner = owner;
        map = MapManager.Instance;
        Direction = direction.normalized;
        Speed = speed;
        Lifetime = lifetime;
        Damage = damage;
        RemainingLifetime = lifetime;
        ReflectionTarget = null;
        reflector = null;
        IsReflected = false;
        IsActive = true;
        shape.enabled = true;
        attackId = DamageInstance.NextAttackId();
        ApplyFacing();
        UpdateTrail(0f);
    }

    /// <summary>Slash 적중에서 제거 또는 금색 반사와 일회 보상 처리</summary>
    public bool OnSlashed(PlayerCharacter player, BossCharacter boss, DamageInstance slash, Vector2 lineDirection)
    {
        if (!isActiveAndEnabled || !IsActive || IsReflected || player == null || player.IsDead
            || slash.Kind != DamageType.Slash || SlashResponse == ProjectileSlashResponse.Ignore)
            return false;

        // 유효한 Boss와 Line 기준 반사가 준비된 요청에만 금색 보상 허용
        BossCharacter target = Owner != null ? Owner : boss;
        if (SlashResponse == ProjectileSlashResponse.Reflect)
        {
            if (!Reflect(target, player, lineDirection)) return false;
            if (GrantsFever && player.Buffs != null)
                player.Buffs.ApplyFever();
        }
        else
            Despawn();

        // 탄환별 처리를 먼저 닫은 뒤 성공한 Slash 보상을 한 번 전달
        player.AddGroggy(GroggyGain, target);
        return true;
    }

    /// <summary>현재 운동 방향을 Line 접선의 수직 법선으로 반사하고 위치와 무관하게 방향 고정</summary>
    public bool Reflect(BossCharacter boss, PlayerCharacter player, Vector2 lineDirection)
    {
        if (!isActiveAndEnabled || !IsActive || IsReflected || boss == null || !boss.isActiveAndEnabled || !boss.IsInitialized
            || boss.IsDead || player == null || player.IsDead || !Nonnegative(ReflectionSpeed) || ReflectionSpeed <= 0f
            || !Finite(Direction) || !Nonnegative(Direction.sqrMagnitude) || Direction.sqrMagnitude <= 0f
            || !Finite(lineDirection) || !Nonnegative(lineDirection.sqrMagnitude) || lineDirection.sqrMagnitude <= 0f)
            return false;

        // 중심이 이미 Line을 넘어도 입사 운동과 같은 반사면만 사용해 동일하게 반사
        Vector2 tangent = lineDirection.normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x);
        Direction = Vector2.Reflect(Direction.normalized, normal).normalized;
        IsReflected = true;
        ReflectionTarget = boss;
        reflector = player;
        if (Lifetime > 0f) RemainingLifetime = Mathf.Max(RemainingLifetime, 1f);
        ApplyFacing();
        UpdateTrail(0f);
        return true;
    }

    /// <summary>피해·보상·충돌을 즉시 닫고 탄환 제거</summary>
    public void Despawn()
    {
        if (!IsActive) return;
        OnDespawning();
        IsActive = false;
        if (shape != null) shape.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>제거가 확정된 순간 구체 탄환이 자기 위치에 마지막 표시를 남길 창구</summary>
    protected virtual void OnDespawning() { }

    /// <summary>프리팹의 Collider를 우선 사용하고 기본 탄 생성에만 원형 판정 보완</summary>
    protected virtual Collider2D PrepareShape()
    {
        Collider2D existing = GetComponent<Collider2D>();
        return existing != null ? existing : gameObject.AddComponent<CircleCollider2D>();
    }

    /// <summary>일반 탄환의 빠른 이동 사이에 놓인 Player를 경로 질의로 타격</summary>
    protected virtual void TickFlight(float elapsed)
    {
        // 재사용 결과 버퍼로 이동 경로의 모든 피격 영역 수집
        Vector2 direction = Direction.normalized;
        float distance = Speed * elapsed;
        int count = CollectFlightHits(direction, distance);
        float wallDistance = GetNearestWallDistance(count);

        // 가장 가까운 벽보다 앞의 Player만 타격하고 같은 거리에서는 벽 우선
        for (int i = 0; i < count && IsActive; i++)
        {
            Collider2D other = hits[i].collider;
            if (hits[i].distance >= wallDistance || other == null || !other.TryGetComponent(out Hitbox hitbox)
                || !(hitbox.Owner is PlayerCharacter)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(Owner, Damage, DamageType.Normal, attackId)))
                Despawn();
        }
        if (!IsActive) return;

        // 빠른 비행도 원 중심이 벽에 접촉하는 지점에서 즉시 종료
        if (wallDistance <= distance)
        {
            transform.position += (Vector3)(direction * wallDistance);
            Despawn();
        }
        else
        {
            transform.position += (Vector3)(direction * distance);
            UpdateTrail(distance);
        }
    }

    /// <summary>고정한 반사 방향의 이동 경로에서 벽과 실제 Boss 피격 중 가장 가까운 접촉 처리</summary>
    private void UpdateReflection(float elapsed)
    {
        if (ReflectionTarget == null || ReflectionTarget.IsDead || !ReflectionTarget.isActiveAndEnabled || !Nonnegative(ReflectionSpeed)
            || Direction.sqrMagnitude <= 0f)
        {
            Despawn();
            return;
        }

        // 보스 위치로 방향을 보정하지 않고 같은 경로에서 벽 접촉 거리 수집
        Vector2 direction = Direction.normalized;
        float step = ReflectionSpeed * elapsed;
        int count = CollectFlightHits(direction, step);
        float wallDistance = GetNearestWallDistance(count);

        // 가장 가까운 벽보다 앞에 있는 Boss 피격 영역만 선택하고 동거리에서는 벽 우선
        Hitbox target = null;
        float targetDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = hits[i].collider;
            if (hits[i].distance >= wallDistance || hits[i].distance >= targetDistance || other == null
                || !other.TryGetComponent(out Hitbox hitbox) || !hitbox.isActiveAndEnabled || !hitbox.IsEnabled
                || hitbox.TargetKind != TargetKind.Boss || hitbox.Owner != ReflectionTarget) continue;
            target = hitbox;
            targetDistance = hits[i].distance;
        }
        if (target != null)
        {
            transform.position += (Vector3)(direction * targetDistance);
            target.ReceiveHit(new DamageInstance(reflector, ReflectDamage, DamageType.Slash, attackId));
            Despawn();
            return;
        }

        // Boss를 빗나간 반사 탄도 벽에서 끝내고 빈 경로에서는 같은 방향으로 이동
        if (wallDistance <= step)
        {
            transform.position += (Vector3)(direction * wallDistance);
            Despawn();
            return;
        }
        transform.position += (Vector3)(direction * step);
        UpdateTrail(step);
    }

    /// <summary>스미어가 진행 반대쪽으로 놓이도록 표시 자식을 현재 운동 방향으로 회전</summary>
    private void ApplyFacing()
    {
        if (!FaceDirection || Visual == null || Direction.sqrMagnitude <= 0f) return;
        Visual.rotation = Quaternion.FromToRotation(Vector3.right, Direction.normalized);
    }

    /// <summary>방금 이동한 거리만큼 뒤로 늘어난 Slash 전용 판정을 갱신</summary>
    private void UpdateTrail(float movedDistance)
    {
        // 표시 자식의 로컬 -X가 진행 반대 방향이므로 그쪽으로만 길이를 늘림
        if (SlashTrail == null) return;
        float length = Mathf.Max(0f, movedDistance);
        SlashTrail.size = new Vector2(length + TrailWidth, TrailWidth);
        SlashTrail.offset = new Vector2(-length * 0.5f, 0f);

        // 물리 갱신을 기다리지 않고 같은 프레임의 Slash 질의가 새 위치를 보도록 반영
        Physics2D.SyncTransforms();
    }

    /// <summary>같은 버퍼로 빠른 이동 사이의 Player와 맵 벽을 모두 수집</summary>
    private int CollectFlightHits(Vector2 direction, float distance)
    {
        // Trigger 피격과 물리 벽이 같은 이동 경로에 포함되도록 질의
        filter.SetLayerMask(CollisionMask);
        int count;
        do
        {
            count = Physics2D.CircleCast(transform.position, Mathf.Max(0.001f, CollisionRadius), direction, filter, hits, distance);
            if (count < hits.Length) break;
            Array.Resize(ref hits, checked(hits.Length * 2));
        } while (true);
        return count;
    }

    /// <summary>이번 발사에 연결한 맵의 벽 중 가장 가까운 접촉 거리 조회</summary>
    private float GetNearestWallDistance(int count)
    {
        // 맵을 새로 찾지 않고 발사 때 준비한 벽 집합만 사용
        float distance = float.PositiveInfinity;
        if (map == null) return distance;
        for (int i = 0; i < count; i++)
            if (map.IsWall(hits[i].collider)) distance = Mathf.Min(distance, hits[i].distance);
        return distance;
    }

    /// <summary>이동 좌표의 두 값이 유한한지 확인</summary>
    protected static bool Finite(Vector2 value)
    {
        return Nonnegative(Mathf.Abs(value.x)) && Nonnegative(Mathf.Abs(value.y));
    }

    /// <summary>탄환 설정이 유한한 0 이상 값인지 확인</summary>
    protected static bool Nonnegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
