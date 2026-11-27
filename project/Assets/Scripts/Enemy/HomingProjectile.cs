using UnityEngine;

/// <summary>처음에는 맵 중앙으로 향하다가 점점 A* 최단 경로를 따라 Player를 쫓고, 몸에 닿으면 그 자리에서 폭발하며 Slash에 제거되는 유도탄</summary>
public class HomingProjectile : NormalProjectile
{
    public float PathWeightDuration = 1.5f; // 발사 뒤 중앙 방향에서 최단 경로 방향으로 완전히 넘어가는 데 걸리는 게임 시간
    public float TurnSpeed = 150f; // 쫓는 방향이 바뀌어도 곡선을 그리도록 제한할 초당 회전 각도
    public float PathInterval = 0.15f; // 최단 경로를 다시 계산할 간격
    public float ContactRadius = 0.6f; // 탄환 중심과 Player 몸 중심이 이 거리 안이면 닿은 것으로 보고 폭발
    public float TargetCenterHeight = 0.5f; // Player 기준점인 발에서 몸 중심까지의 높이
    public float DirectRange = 2.5f; // 이 거리 안에서는 경로 격자 대신 Player 몸 중심을 곧장 추적
    public float CloseTurnSpeed = 360f; // 곧장 추적하는 동안 허용할 초당 회전 각도
    public float TrailInterval = 0.1f; // 꼬리를 한 번씩 남길 간격
    public float BlinkStart = 0.75f; // 수명의 이 비율을 지나면 밝은 몸체가 깜빡이기 시작
    public float BlinkDuration = 0.05f; // 밝은 몸체를 한 번 켜 둘 시간
    public SpriteClip[] TrailClips = new SpriteClip[0]; // 번갈아 남길 꼬리 클립
    public SpriteClip ExplosionClip; // Player 접촉·Slash·벽·수명 종료에서 재생할 폭발 클립
    public AudioClip ExplosionSound; // 폭발 클립과 함께 낼 소리
    public GameObject Bright; // 수명이 끝나갈 때 켰다 끌 밝은 몸체
    public Material SpriteMaterial; // 꼬리와 폭발을 다른 월드 Sprite와 같게 그릴 재질
    public int TrailOrder = DepthSorter.SharedOrder - 1; // 꼬리를 발끝 기준으로 정렬되는 캐릭터·탄환 전체보다 아래에 그릴 정렬 순서
    public int ExplosionOrder = 32; // 폭발을 캐릭터와 몸체 위에 그릴 정렬 순서
    public float FlightTime { get; private set; } // 발사 뒤 지난 게임 시간

    private Transform target; // 쫓아갈 Player
    private Hitbox targetHitbox; // 닿은 순간 피해를 전달할 Player 피격 영역
    private GridPathfinder grid; // 발사한 보스가 준비해 넘긴 전투 구역 격자
    private Vector2 center; // 발사 직후 향할 전투 구역의 중앙
    private Vector2 pathPoint; // 최단 경로에서 지금 향하는 다음 지점
    private float pathTimer; // 다음 경로 계산까지 남은 시간
    private float trailTimer; // 다음 꼬리까지 남은 시간
    private int trailIndex; // 다음에 남길 꼬리 클립 번호
    private float nextBlinkRatio; // 다음 깜빡임이 시작될 수명 비율
    private float blinkRemaining; // 밝은 몸체를 켜 둘 남은 시간
    private bool silent; // 전투 정리로 지울 때 폭발을 남기지 않도록 표시

    /// <summary>발사한 보스가 쫓을 대상·격자·중앙을 연결</summary>
    public void BeginHoming(Transform player, GridPathfinder pathfinder, Vector2 arenaCenter)
    {
        target = player;
        targetHitbox = player != null ? player.GetComponentInChildren<Hitbox>() : null;
        if (targetHitbox != null && !(targetHitbox.Owner is PlayerCharacter)) targetHitbox = null;
        grid = pathfinder;
        center = arenaCenter;
        pathPoint = player != null ? (Vector2)player.position : arenaCenter;
        FlightTime = 0f;
        pathTimer = 0f;
        trailTimer = TrailInterval;
        trailIndex = 0;
        nextBlinkRatio = BlinkStart;
        blinkRemaining = 0f;
        silent = false;
        if (Bright != null) Bright.SetActive(false);
    }

    /// <summary>폭발 없이 조용히 제거</summary>
    public void DespawnSilently()
    {
        silent = true;
        Despawn();
    }

    /// <summary>방향을 중앙과 최단 경로의 가중 합 쪽으로 조금씩 돌리고 가까워지면 곧장 쫓은 뒤 공통 비행·충돌과 몸 접촉 처리</summary>
    protected override void TickFlight(float elapsed)
    {
        // 대상이 없으면 마지막 방향으로 직진
        FlightTime += elapsed;
        if (target != null)
        {
            // 이미 몸에 닿아 있으면 이동하기 전에 폭발
            Vector2 position = transform.position;
            Vector2 body = GetTargetCenter();
            if (TryContact(position, body)) return;

            // 가까우면 Player 몸 중심을 곧장 향하고 멀면 일정 간격으로 벽을 피하는 최단 경로의 다음 지점을 다시 구함
            bool close = (body - position).sqrMagnitude <= DirectRange * DirectRange;
            pathTimer -= elapsed;
            if (close) pathPoint = body;
            else if (pathTimer <= 0f)
            {
                pathTimer = Mathf.Max(0.02f, PathInterval);
                if (grid == null || !grid.TryGetNextPoint(position, target.position, out pathPoint)) pathPoint = body;
            }

            // 시간이 지날수록 중앙 방향의 비중을 줄이고 최단 경로 방향의 비중을 키우며 가까우면 경로 방향만 사용
            float weight = close || PathWeightDuration <= 0f ? 1f : Mathf.Clamp01(FlightTime / PathWeightDuration);
            Vector2 toCenter = center - position, toPath = pathPoint - position;
            Vector2 desired = (toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : Vector2.zero) * (1f - weight)
                + (toPath.sqrMagnitude > 0.0001f ? toPath.normalized : Vector2.zero) * weight;
            if (desired.sqrMagnitude > 0.0001f)
            {
                // 한 프레임에 돌 수 있는 각도를 제한해 곡선으로 쫓되 가까울 때는 주위를 돌지 않도록 한도를 높임
                float turn = close ? Mathf.Max(TurnSpeed, CloseTurnSpeed) : TurnSpeed;
                float current = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
                float wanted = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                float angle = Mathf.MoveTowardsAngle(current, wanted, Mathf.Max(0f, turn) * elapsed) * Mathf.Deg2Rad;
                Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }
        TickTrail(elapsed);
        TickBlink(elapsed);
        base.TickFlight(elapsed);

        // 이번 이동으로 몸에 닿았으면 발밑 판정과 무관하게 폭발
        if (IsActive && target != null) TryContact(transform.position, GetTargetCenter());
    }

    /// <summary>쫓는 Player의 발 위치에서 몸 중심 높이만큼 올린 지점</summary>
    private Vector2 GetTargetCenter()
    {
        return (Vector2)target.position + new Vector2(0f, TargetCenterHeight);
    }

    /// <summary>몸 중심과의 거리가 접촉 반경 안이면 피해를 전달하고 그 자리에서 폭발하며 회피·보호 중에는 피해 없이 폭발</summary>
    private bool TryContact(Vector2 position, Vector2 body)
    {
        if (ContactRadius <= 0f || (body - position).sqrMagnitude > ContactRadius * ContactRadius) return false;
        if (targetHitbox != null) targetHitbox.ReceiveHit(new DamageInstance(Owner, Damage, DamageType.Normal, attackId));
        Despawn();
        return true;
    }

    /// <summary>제거되는 자리에 폭발을 한 번 남기며 전투가 끝나 한꺼번에 정리될 때는 남기지 않음</summary>
    protected override void OnDespawning()
    {
        if (silent || ExplosionClip == null || Owner == null || !Owner.IsInCombat) return;
        SpriteClipPlayer.Spawn(ExplosionClip, transform.position, ExplosionOrder, SpriteMaterial, transform.parent);
        if (Owner is Boss_Last boss) boss.PlaySound(ExplosionSound); // 탄환은 곧 사라지므로 발사한 보스의 오디오 소스로 재생
    }

    /// <summary>지나온 자리에 두 종류의 꼬리를 번갈아 남김</summary>
    private void TickTrail(float elapsed)
    {
        if (TrailClips == null || TrailClips.Length == 0 || TrailInterval <= 0f) return;
        trailTimer -= elapsed;
        if (trailTimer > 0f) return;
        trailTimer += TrailInterval;
        SpriteClipPlayer.Spawn(TrailClips[trailIndex], transform.position, TrailOrder, SpriteMaterial, transform.parent);
        trailIndex = (trailIndex + 1) % TrailClips.Length;
    }

    /// <summary>수명이 끝나갈수록 간격이 줄어드는 밝은 몸체 깜빡임</summary>
    private void TickBlink(float elapsed)
    {
        if (Bright == null || Lifetime <= 0f) return;
        if (blinkRemaining > 0f)
        {
            blinkRemaining -= elapsed;
            if (blinkRemaining <= 0f) Bright.SetActive(false);
        }
        float ratio = 1f - RemainingLifetime / Lifetime;
        if (ratio < nextBlinkRatio) return;
        Bright.SetActive(true);
        blinkRemaining = BlinkDuration;
        nextBlinkRatio += Mathf.Max(0.01f, (1f - ratio) / 3f);
    }
}
