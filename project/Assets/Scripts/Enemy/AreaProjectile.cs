using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>게임 시간 전조 뒤 원형 범위를 타격하고 자체 수명으로 종료하는 장판</summary>
public class AreaProjectile : Projectile
{
    public float Radius = 1f; // 장판 중심에서 피해가 닿는 원형 판정 반경
    public float TelegraphDuration = 0.5f; // 피해 없이 위험 범위만 보여 줄 게임 시간
    public Color TelegraphColor = new Color(1f, 0.4f, 0.1f, 0.25f); // 활성 전 위험 범위를 구별할 임시 색
    public Color ActiveColor = new Color(1f, 0.15f, 0.1f, 0.75f); // 실제 피해가 가능한 범위의 임시 색
    public bool IsArmed { get; private set; } // 전조를 마치고 피해 판정을 시작했는지 조회

    protected SpriteRenderer visual; // 최초 생성 때 확보할 범위 표시용 Sprite
    private ContactFilter2D areaFilter; // 실제 Player Trigger를 수집할 재사용 조건
    private Collider2D[] overlaps = new Collider2D[8]; // 위험 영역 질의에 재사용할 결과 버퍼
    private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>(); // 같은 발사에서 성공한 Player 피해의 중복 방지

    /// <summary>공통 수명·자기 표시·고정 영역 질의 준비</summary>
    protected override void Awake()
    {
        // 구체 Collider와 공통 피해·종료 상태 준비
        base.Awake();
        visual = GetComponentInChildren<SpriteRenderer>();
        areaFilter = new ContactFilter2D { useTriggers = true };
        SlashResponse = ProjectileSlashResponse.Ignore;
    }

    /// <summary>전조를 포함한 전체 수명과 한 번의 범위 공격 준비</summary>
    public override void Initialize(BossCharacter owner, Vector2 direction, float speed, float lifetime, float damage)
    {
        // 잘못된 전조·기하를 발사 상태 변경 전에 거부
        if (!Nonnegative(TelegraphDuration) || TelegraphDuration >= lifetime)
            throw new ArgumentException("A hazard requires a finite telegraph shorter than its lifetime.");
        ValidateShape();

        // 같은 공통 발사 API를 사용하되 위치는 고정하고 Slash 반응 제외
        base.Initialize(owner, direction, 0f, lifetime, damage);
        SlashResponse = ProjectileSlashResponse.Ignore;
        IsArmed = false;
        hitPlayers.Clear();
        ConfigureShape();
        if (visual != null) visual.color = TelegraphColor;
    }

    /// <summary>장판에 맞는 원형 Collider를 최초 한 번 확보</summary>
    protected override Collider2D PrepareShape()
    {
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        return circle != null ? circle : gameObject.AddComponent<CircleCollider2D>();
    }

    /// <summary>전조가 끝난 뒤 범위에 들어온 Player에게 한 번 피해 전달</summary>
    protected override void TickFlight(float elapsed)
    {
        // 전조 동안 피해를 보류하고 활성 경계에서 표시 전환
        if (!IsArmed)
        {
            if (Lifetime - RemainingLifetime < TelegraphDuration) return;
            IsArmed = true;
            if (visual != null) visual.color = ActiveColor;
        }

        // 여러 Collider나 재진입도 한 발사의 동일 Player 피해로 합산
        areaFilter.SetLayerMask(CollisionMask);
        int count;
        do
        {
            count = shape.Overlap(areaFilter, overlaps);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, checked(overlaps.Length * 2));
        } while (true);
        for (int i = 0; i < count && IsActive; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                || hitPlayers.Contains(player)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(Owner, Damage, DamageType.Normal, attackId)))
                hitPlayers.Add(player);
        }
    }

    /// <summary>장판 반경의 유효성을 발사 전에 확인</summary>
    protected virtual void ValidateShape()
    {
        if (!Nonnegative(Radius) || Radius <= 0f)
            throw new ArgumentException("An area requires a positive finite radius.");
    }

    /// <summary>실제 원형 판정과 같은 크기로 임시 범위 표시 설정</summary>
    protected virtual void ConfigureShape()
    {
        // Collider와 표시가 같은 중심·크기를 사용하도록 설정
        CircleCollider2D circle = (CircleCollider2D)shape;
        circle.radius = Radius;
        circle.offset = Vector2.zero;
        if (visual != null)
        {
            visual.drawMode = SpriteDrawMode.Sliced;
            visual.size = Vector2.one * (Radius * 2f);
        }
    }
}
