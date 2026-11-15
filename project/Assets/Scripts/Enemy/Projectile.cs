using System;
using UnityEngine;

/// <summary>Slash에 대한 구체 탄환의 반응 구분</summary>
public enum ProjectileSlashResponse
{
    Destroy, // Slash 판정에서 제거
    Reflect, // Boss를 향해 반사하고 각성 획득
    Ignore // Slash 반응 없음
}

/// <summary>구체 탄환의 게임 시간 이동·피해·수명과 일회 Slash 반응</summary>
public class Projectile : MonoBehaviour
{
    public BossCharacter Owner; // 최초 발사자이자 기본 반사 목표
    public float Damage = 10f; // 탄환이 Player 또는 반사 목표에 전달할 피해
    public Vector2 Direction = Vector2.right; // 일반 탄환의 월드 이동 방향
    public float Speed = 4f; // 일반 탄환의 초당 월드 이동 거리
    public float Lifetime = 8f; // 생성 이후 유지할 게임 시간
    public ProjectileSlashResponse SlashResponse = ProjectileSlashResponse.Destroy; // 이 탄환을 Slash로 처리하는 방식
    public float GroggyGain = 10f; // 성공한 Slash 반응으로 획득할 그로기 수치
    public bool GrantsFever = true; // 금색 반사 성공에서 각성 획득을 요청할지 설정
    public BossCharacter ReflectionTarget { get; private set; } // 반사 이후 빠르게 추적할 Boss
    public float ReflectionSpeed = 20f; // Boss를 향해 이동할 반사 연출 속도
    public float CollisionRadius = 0.08f; // 빠른 이동 사이의 Player 판정을 조회할 반경
    public LayerMask CollisionMask = ~0; // 이동 경로의 Player 피격 영역을 조회할 레이어
    public bool IsReflected { get; private set; } // Player 피해와 중복 Slash 보상을 차단할 반사 상태
    public bool IsActive { get; private set; } = true; // 수명·피해·보상 처리의 일회 종료 기준
    public float RemainingLifetime { get; private set; } // 수명 만료까지 남은 게임 시간

    private PlayerCharacter reflector; // 반사 피해의 출처로 유지할 Player
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
        if (!Nonnegative(Speed) || !Nonnegative(Damage) || !Nonnegative(RemainingLifetime) || !Finite(Direction))
        {
            Despawn();
            return;
        }
        RemainingLifetime = Mathf.Max(0f, RemainingLifetime - elapsed);
        if (RemainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        // 반사 탄은 Player 질의를 하지 않고 지정한 Boss만 추적
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
            || !Nonnegative(lifetime) || lifetime <= 0f || !Nonnegative(damage))
            throw new ArgumentException("Projectile requires an owner, direction and finite flight settings.");

        // 같은 탄환의 발사 정보와 일회 반응 상태를 함께 준비
        Owner = owner;
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
    }

    /// <summary>Slash 적중에서 제거 또는 금색 반사와 일회 보상 처리</summary>
    public bool OnSlashed(PlayerCharacter player, BossCharacter boss, DamageInstance slash)
    {
        if (!isActiveAndEnabled || !IsActive || IsReflected || player == null || player.IsDead
            || slash.Kind != DamageType.Slash || SlashResponse == ProjectileSlashResponse.Ignore)
            return false;

        // 반사는 유효한 Boss 목표를 먼저 확정해 실패한 요청에는 보상을 주지 않도록 처리
        BossCharacter target = Owner != null ? Owner : boss;
        if (SlashResponse == ProjectileSlashResponse.Reflect)
        {
            if (!Reflect(target, player)) return false;
            if (GrantsFever && player.Buffs != null)
                player.Buffs.ApplyFever();
        }
        else
            Despawn();

        // 탄환별 처리를 먼저 닫은 뒤 성공한 Slash 보상을 한 번 전달
        player.AddGroggy(GroggyGain, target);
        return true;
    }

    /// <summary>물리 반사각 대신 Boss를 향한 빠른 추적 연출로 전환</summary>
    public bool Reflect(BossCharacter boss, PlayerCharacter player)
    {
        if (!isActiveAndEnabled || !IsActive || IsReflected || boss == null || !boss.isActiveAndEnabled || !boss.IsInitialized
            || boss.IsDead || player == null || player.IsDead || !Nonnegative(ReflectionSpeed) || ReflectionSpeed <= 0f)
            return false;
        IsReflected = true;
        ReflectionTarget = boss;
        reflector = player;
        RemainingLifetime = Mathf.Max(RemainingLifetime, 1f);
        return true;
    }

    /// <summary>피해·보상·충돌을 즉시 닫고 탄환 제거</summary>
    public void Despawn()
    {
        if (!IsActive) return;
        IsActive = false;
        if (shape != null) shape.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

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
        filter.SetLayerMask(CollisionMask);
        int count;
        do
        {
            count = Physics2D.CircleCast(transform.position, Mathf.Max(0.001f, CollisionRadius), direction, filter, hits, distance);
            if (count < hits.Length) break;
            Array.Resize(ref hits, checked(hits.Length * 2));
        } while (true);

        // 피해가 성공한 첫 Player에서 탄을 종료하고 나머지 판정 차단
        for (int i = 0; i < count && IsActive; i++)
        {
            Collider2D other = hits[i].collider;
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(Owner, Damage, DamageType.Normal, attackId)))
                Despawn();
        }
        if (IsActive)
            transform.position += (Vector3)(direction * distance);
    }

    /// <summary>반사 목표를 추적하고 도달 순간 실제 Boss 피해 전달</summary>
    private void UpdateReflection(float elapsed)
    {
        if (ReflectionTarget == null || ReflectionTarget.IsDead || !ReflectionTarget.isActiveAndEnabled || !Nonnegative(ReflectionSpeed))
        {
            Despawn();
            return;
        }

        // 한 프레임 이동보다 목표가 가까우면 한 번 타격하고 종료
        Vector2 offset = (Vector2)ReflectionTarget.transform.position - (Vector2)transform.position;
        float step = ReflectionSpeed * elapsed;
        if (offset.sqrMagnitude <= step * step)
        {
            ReflectionTarget.ApplyDamage(new DamageInstance(reflector, Damage, DamageType.Slash, attackId));
            Despawn();
            return;
        }
        Direction = offset.normalized;
        transform.position += (Vector3)(Direction * step);
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
