using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>양눈 레이저의 에너지 충전·가속 소사·회복 단계</summary>
public enum GuardEyeLaserPhase
{
    Idle, // 눈의 발광과 빔을 숨긴 기본 상태
    Charging, // 양눈에 충전 고리를 재생하는 구간
    Firing, // 정해진 경로를 따라 지면 접점이 가속 이동하는 구간
    Recovery // 빔을 닫고 다음 패턴 전환까지 대기
}

/// <summary>두 눈에서 나온 원통형 빔이 한 지면 타원에서 겹치고 그 타원이 시작점에서 끝점까지 가속하는 패턴 5 실행체</summary>
public class GuardEyeLaser : MonoBehaviour
{
    public Transform[] Eyes = Array.Empty<Transform>(); // 왼눈·오른눈의 빔 시작점
    public LineRenderer[] BeamCores = Array.Empty<LineRenderer>(); // 두 빔의 진한 자홍 중심 기둥
    public LineRenderer[] BeamHalos = Array.Empty<LineRenderer>(); // 두 빔을 감싸며 깜빡이는 연분홍 외곽 기둥
    public SpriteClipPlayer EyeFx; // 양눈의 충전 고리와 발사 중 발광을 재생할 전체 캔버스 덧그림
    public SpriteClip EyeTellClip; // 충전 동안 한 번 재생할 눈 충전 고리
    public SpriteClip EyeFireClip; // 발사 동안 반복할 눈 발광
    public SpriteRenderer ImpactZone; // 두 빔이 닿는 지면에서 실제 피해 범위를 빔 위에 반투명하게 표시할 타원
    public SpriteRenderer ImpactGlow; // 접점 주변 바닥에 번지는 빛
    public ParticleSystem Sparks; // 접점에서 튀는 작은 불티
    public ParticleSystem Smoke; // 접점에서 가끔 피어오르는 작은 연기
    public AudioSource Audio; // 충전·발사 소리를 낼 오디오 소스
    public AudioClip ChargeClip; // 충전 중 재생할 소리
    public AudioClip FireClip; // 발사 중 반복할 소리
    [Range(0f, 1f)] public float Volume = 0.8f; // 레이저 소리의 음량
    public float ChargeDuration = 1.2f; // 빔 발사 전에 눈 에너지를 모을 게임 시간
    public float FiringDuration = 1f; // 접점이 시작점에서 끝점까지 이동하는 게임 시간
    public float RecoveryDuration = 0.5f; // 빔을 닫은 뒤 다음 패턴까지의 게임 시간
    public float BeamRadius = 0.1875f; // 빔 중심 기둥의 반지름이며 눈 반경 3px에 해당
    public float HaloScale = 2f; // 중심 기둥 반지름 대비 외곽 기둥 반지름의 배율
    public Color CoreColor = new Color(0.651f, 0.078f, 0.365f, 0.9f); // 중심 기둥의 진한 자홍색
    public Color HaloColor = new Color(1f, 0.867f, 1f, 0.3f); // 외곽 기둥의 연분홍색
    public float HaloFlickerRate = 60f; // 외곽 기둥을 켜고 끄는 초당 전환 횟수
    public Vector2 ImpactZoneSize = new Vector2(1f, 0.5f); // 지면 피해 타원의 가로·세로 크기이며 표시와 판정이 공유
    public Color ImpactZoneColor = new Color(0.72f, 0.32f, 0.05f, 0.75f); // 자홍 빔과 구분되는 어두운 주황의 피해 타원색
    public float BeamDamage = 10f; // 한 번의 소사에서 Player에게 한 번 전달할 피해
    public float StartDistanceMin = 4.5f; // Player에서 시작점까지의 최소 거리
    public float StartDistanceMax = 6.5f; // Player에서 시작점까지의 최대 거리
    [Range(0.1f, 0.95f)] public float PlayerPathPoint = 0.75f; // 시작점에서 끝점까지의 경로 중 Player가 놓일 지점의 비율
    public float GlowRadius = 1.25f; // 접점 주변 빛의 반지름
    [Range(0f, 1f)] public float GlowOpacity = 0.2f; // 접점 주변 빛의 알파
    public float SparksPerSecond = 180f; // 접점에서 방출할 초당 불티 수
    public float SparkSpeed = 7.5f; // 불티가 튀어 나가는 초당 최대 거리
    [Range(0f, 1f)] public float SmokeChance = 0.2f; // 방출 한 번이 불티 대신 연기가 될 확률
    public bool IsRunning { get; private set; } // 패턴이 시간 진행 또는 취소를 전달할 실행 상태
    public GuardEyeLaserPhase Phase { get; private set; } // 현재 충전·발사·회복 진행 단계
    public float PhaseElapsed { get; private set; } // 현재 단계에서 지난 게임 시간
    public Vector2 PathStart { get; private set; } // 접점이 처음 닿는 지면 위치
    public Vector2 PathEnd { get; private set; } // 접점이 마지막에 닿는 지면 위치
    public Vector2 ImpactPosition { get; private set; } // 두 빔이 겹쳐 있는 현재 지면 접점

    private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>(); // 같은 소사의 피해 중복 방지
    private Collider2D[] overlaps = new Collider2D[8]; // 피해 타원에 걸친 Collider 질의 결과
    private ContactFilter2D damageFilter; // Trigger를 포함한 Player 피격 영역 질의 조건
    private BossCharacter owner; // 피해 출처로 쓸 자기 보스
    private PlayerCharacter target; // 이번 패턴 시작에서 받은 같은 전투의 Player
    private Rect arena; // 시작점과 끝점을 가둘 전투 구역
    private float emitRemainder; // 다음 프레임으로 넘길 방출 수의 소수 부분
    private int attackId; // 이번 소사의 피해를 하나의 공격으로 묶을 번호
    private bool prepared; // 자기 참조와 표시 설정을 준비했는지 여부

    /// <summary>자기 참조와 표시 설정을 준비하고 기본 표시로 시작</summary>
    private void Awake()
    {
        Prepare();
        Cancel();
    }

    private void OnDisable()
    {
        Cancel();
    }

    /// <summary>같은 전투의 Player와 전투 구역을 받아 양눈 충전 시작</summary>
    public bool Begin(PlayerCharacter player, Rect area)
    {
        Prepare();
        if (IsRunning || !OwnerReady() || player == null || player.IsDead || !player.isActiveAndEnabled
            || ChargeDuration <= 0f || FiringDuration <= 0f || BeamRadius <= 0f || ImpactZoneSize.x <= 0f || ImpactZoneSize.y <= 0f) return false;
        target = player;
        arena = area;
        Phase = GuardEyeLaserPhase.Charging;
        PhaseElapsed = 0f;
        hitPlayers.Clear();
        attackId = DamageInstance.NextAttackId();
        IsRunning = true;

        // 충전 고리를 충전 시간에 맞춰 재생하고 충전 소리 시작
        EyeFx.gameObject.SetActive(true);
        EyeFx.PlayFor(EyeTellClip, ChargeDuration);
        PlayLoop(ChargeClip, false);
        return true;
    }

    /// <summary>패턴이 전달한 게임 시간으로 충전·소사·회복을 진행</summary>
    public void Tick(float gameDelta)
    {
        // 사망·그로기·전투 종료는 빔과 소리를 즉시 닫음
        if (!IsRunning) return;
        if (!OwnerReady() || target == null || target.IsDead || !target.isActiveAndEnabled)
        {
            Cancel();
            return;
        }
        if (Time.timeScale <= 0f || gameDelta <= 0f) return;
        PhaseElapsed += gameDelta;
        if (Phase == GuardEyeLaserPhase.Charging)
        {
            if (PhaseElapsed < ChargeDuration) return;

            // 충전이 끝난 순간의 Player 위치로 경로를 정하고 발사 시작
            PhaseElapsed -= ChargeDuration;
            Phase = GuardEyeLaserPhase.Firing;
            DecidePath();
            ImpactPosition = PathStart;
            EyeFx.Play(EyeFireClip);
            SetBeamVisible(true);
            PlayLoop(FireClip, true);
        }
        if (Phase == GuardEyeLaserPhase.Firing)
        {
            UpdateFiring(Mathf.Min(PhaseElapsed, FiringDuration), gameDelta);
            if (PhaseElapsed < FiringDuration) return;
            PhaseElapsed -= FiringDuration;
            Phase = GuardEyeLaserPhase.Recovery;
            SetBeamVisible(false);
            EyeFx.Stop();
            if (Audio != null) Audio.Stop();
        }
        if (Phase == GuardEyeLaserPhase.Recovery && PhaseElapsed >= RecoveryDuration) Cancel();
    }

    /// <summary>충전·빔·소리를 닫고 다음 실행을 위한 기본 표시로 복원</summary>
    public void Cancel()
    {
        IsRunning = false;
        Phase = GuardEyeLaserPhase.Idle;
        PhaseElapsed = 0f;
        target = null;
        if (!prepared) return;
        SetBeamVisible(false);
        if (EyeFx != null)
        {
            EyeFx.Stop();
            EyeFx.gameObject.SetActive(false);
        }
        if (Audio != null && (Audio.clip == ChargeClip || Audio.clip == FireClip)) Audio.Stop();
    }

    /// <summary>자기 보스·눈·빔 참조를 확인하고 빔과 입자의 고정 설정을 한 번 적용</summary>
    private void Prepare()
    {
        if (prepared) return;
        owner = GetComponent<BossCharacter>();
        if (owner == null || Eyes == null || Eyes.Length != 2 || BeamCores == null || BeamCores.Length != 2
            || BeamHalos == null || BeamHalos.Length != 2 || EyeFx == null || ImpactZone == null || ImpactGlow == null)
            throw new InvalidOperationException("Guard eye laser requires its Boss, two eyes, two beam layers, the eye overlay and impact visuals.");
        for (int i = 0; i < 2; i++)
        {
            if (Eyes[i] == null || BeamCores[i] == null || BeamHalos[i] == null)
                throw new InvalidOperationException("Guard eye laser has an unassigned eye or beam layer.");
            PrepareLine(BeamCores[i], BeamRadius * 2f, CoreColor);
            PrepareLine(BeamHalos[i], BeamRadius * 2f * HaloScale, HaloColor);
        }

        // 피해 타원은 1px 테두리와 반투명 내부를 가진 마스크에 구분색을 입히고 주변 빛은 원으로 표시
        int zoneWidth = Mathf.Max(3, Mathf.RoundToInt(ImpactZoneSize.x * PixelShapeTexture.PixelsPerUnit));
        int zoneHeight = Mathf.Max(3, Mathf.RoundToInt(ImpactZoneSize.y * PixelShapeTexture.PixelsPerUnit));
        int glow = Mathf.Max(2, Mathf.RoundToInt(GlowRadius * 2f * PixelShapeTexture.PixelsPerUnit));
        ImpactZone.sprite = PixelShapeTexture.GetEllipseMask(zoneWidth, zoneHeight);
        ImpactZone.color = ImpactZoneColor;
        ImpactGlow.sprite = PixelShapeTexture.GetSolidEllipse(glow, glow);
        ImpactGlow.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, GlowOpacity);
        PrepareParticles(Sparks, 1f / PixelShapeTexture.PixelsPerUnit, 2f / PixelShapeTexture.PixelsPerUnit);
        PrepareParticles(Smoke, 4f / PixelShapeTexture.PixelsPerUnit, 7f / PixelShapeTexture.PixelsPerUnit);
        damageFilter = new ContactFilter2D { useTriggers = true };
        prepared = true;
    }

    /// <summary>Player 주변의 무작위 시작점에서 Player를 지나 3/4 지점에 두는 끝점까지의 경로 결정</summary>
    private void DecidePath()
    {
        // 전투 구역 안에 들어오는 시작점을 몇 번 시도하고 실패하면 구역 안으로 당김
        Vector2 player = target.transform.position;
        Vector2 start = player;
        float min = Mathf.Max(0.1f, Mathf.Min(StartDistanceMin, StartDistanceMax)), max = Mathf.Max(min, StartDistanceMax);
        for (int i = 0; i < 12; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            start = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(min, max);
            if (arena.width <= 0f || arena.Contains(start)) break;
        }
        if (arena.width > 0f) start = ClampToArena(start);
        if ((player - start).sqrMagnitude < 0.01f) start = player + Vector2.left * min;

        // Player가 경로의 지정 비율에 놓이도록 길이를 늘리고 구역 밖으로 나간 끝은 경계에서 자름
        Vector2 end = start + (player - start) / Mathf.Clamp(PlayerPathPoint, 0.1f, 0.95f);
        if (arena.width > 0f) end = ClipToArena(start, end);
        PathStart = start;
        PathEnd = end;
    }

    /// <summary>접점을 등가속으로 옮기고 두 빔·접점 표시·불티·피해를 같은 위치에 적용</summary>
    private void UpdateFiring(float elapsed, float gameDelta)
    {
        // 정지 상태에서 출발해 끝으로 갈수록 빨라지는 이동
        float time = Mathf.Clamp01(elapsed / FiringDuration);
        Vector2 previous = ImpactPosition;
        ImpactPosition = Vector2.Lerp(PathStart, PathEnd, time * time);
        Vector3 end = new Vector3(ImpactPosition.x, ImpactPosition.y, owner.transform.position.z);

        // 외곽 기둥은 일정 간격으로 켜고 꺼서 빔이 떨리는 것처럼 표시
        bool halo = Mathf.FloorToInt(elapsed * Mathf.Max(1f, HaloFlickerRate)) % 2 == 0;
        for (int i = 0; i < 2; i++)
        {
            BeamCores[i].SetPosition(0, Eyes[i].position);
            BeamCores[i].SetPosition(1, end);
            BeamHalos[i].SetPosition(0, Eyes[i].position);
            BeamHalos[i].SetPosition(1, end);
            BeamHalos[i].enabled = halo;
        }
        ImpactZone.transform.position = end;
        ImpactGlow.transform.position = end;
        EmitParticles(end, gameDelta);
        DamageAlong(previous, ImpactPosition);
    }

    /// <summary>피해 타원이 이번 프레임에 지나간 구간을 촘촘히 짚어 그 안의 Player 피격 영역에 한 번 피해 전달</summary>
    private void DamageAlong(Vector2 from, Vector2 to)
    {
        // 빠르게 움직이는 끝부분에서도 빈틈이 없도록 타원 반폭보다 짧은 간격으로 중심을 옮김
        float radiusX = ImpactZoneSize.x * 0.5f, radiusY = ImpactZoneSize.y * 0.5f;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(0.01f, radiusX)), 1, 64);
        for (int step = 1; step <= steps && IsRunning; step++)
        {
            Vector2 center = Vector2.Lerp(from, to, (float)step / steps);
            int count;
            do
            {
                count = Physics2D.OverlapBox(center, ImpactZoneSize, 0f, damageFilter, overlaps);
                if (count < overlaps.Length) break;
                Array.Resize(ref overlaps, checked(overlaps.Length * 2));
            } while (true);
            for (int i = 0; i < count && IsRunning; i++)
            {
                Collider2D other = overlaps[i];
                if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                    || hitPlayers.Contains(player)) continue;

                // 피격 영역에서 타원 중심에 가장 가까운 점이 타원 안에 있을 때만 적중
                Vector2 closest = other.ClosestPoint(center) - center;
                float x = closest.x / radiusX, y = closest.y / radiusY;
                if (x * x + y * y > 1f) continue;
                if (hitbox.ReceiveHit(new DamageInstance(owner, BeamDamage, DamageType.Normal, attackId))) hitPlayers.Add(player);
            }
        }
    }

    /// <summary>접점에서 위쪽으로 치우친 무작위 방향의 불티와 가끔 느린 연기를 방출</summary>
    private void EmitParticles(Vector3 position, float gameDelta)
    {
        if (Sparks == null) return;
        emitRemainder += Mathf.Max(0f, SparksPerSecond) * gameDelta;
        int count = Mathf.FloorToInt(emitRemainder);
        emitRemainder -= count;
        float pixel = 1f / PixelShapeTexture.PixelsPerUnit;
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.6f, 0f).normalized;
            bool smoke = Smoke != null && UnityEngine.Random.value < SmokeChance;
            var emit = new ParticleSystem.EmitParams
            {
                position = position + (Vector3)Vector2.Scale(UnityEngine.Random.insideUnitCircle, ImpactZoneSize * 0.5f),
                velocity = direction * (smoke ? SparkSpeed * 0.15f : UnityEngine.Random.Range(SparkSpeed * 0.35f, SparkSpeed)),
                startLifetime = smoke ? UnityEngine.Random.Range(0.35f, 0.6f) : UnityEngine.Random.Range(0.15f, 0.45f),
                startSize = smoke ? UnityEngine.Random.Range(4f, 7f) * pixel : UnityEngine.Random.Range(1f, 2.01f) * pixel,
                startColor = smoke ? new Color(0.8f, 0.72f, 0.78f, 0.5f) : Color.Lerp(HaloColor, CoreColor, UnityEngine.Random.value) + new Color(0f, 0f, 0f, 1f)
            };
            (smoke ? Smoke : Sparks).Emit(emit, 1);
        }
    }

    /// <summary>두 빔과 접점 표시를 함께 켜거나 끔</summary>
    private void SetBeamVisible(bool visible)
    {
        for (int i = 0; i < 2; i++)
        {
            BeamCores[i].enabled = visible;
            BeamHalos[i].enabled = visible;
        }
        ImpactZone.enabled = visible;
        ImpactGlow.enabled = visible;
        if (!visible)
        {
            emitRemainder = 0f;
            return;
        }

        // 방출은 코드가 맡고 입자 진행만 켜 둠
        if (Sparks != null && !Sparks.isPlaying) Sparks.Play();
        if (Smoke != null && !Smoke.isPlaying) Smoke.Play();
    }

    /// <summary>충전음은 한 번, 발사음은 반복으로 같은 오디오 소스에서 재생</summary>
    private void PlayLoop(AudioClip clip, bool loop)
    {
        if (Audio == null || clip == null) return;
        Audio.clip = clip;
        Audio.loop = loop;
        Audio.volume = Volume * GameSettings.SfxVolume;
        Audio.Play();
    }

    /// <summary>두 점을 잇는 둥근 끝의 원통형 빔 한 층 준비</summary>
    private static void PrepareLine(LineRenderer line, float width, Color color)
    {
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.numCapVertices = 6;
        line.numCornerVertices = 0;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.enabled = false;
    }

    /// <summary>코드로만 방출하는 월드 공간 입자의 수명·크기 변화 설정</summary>
    private static void PrepareParticles(ParticleSystem particles, float minSize, float maxSize)
    {
        if (particles == null) return;
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.maxParticles = 512;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
    }

    /// <summary>점을 전투 구역 안쪽으로 당김</summary>
    private Vector2 ClampToArena(Vector2 point)
    {
        return new Vector2(Mathf.Clamp(point.x, arena.xMin, arena.xMax), Mathf.Clamp(point.y, arena.yMin, arena.yMax));
    }

    /// <summary>구역 안의 시작점에서 출발한 선분이 구역을 벗어나면 경계에서 자름</summary>
    private Vector2 ClipToArena(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float limit = 1f;
        if (delta.x > 0f) limit = Mathf.Min(limit, (arena.xMax - start.x) / delta.x);
        else if (delta.x < 0f) limit = Mathf.Min(limit, (arena.xMin - start.x) / delta.x);
        if (delta.y > 0f) limit = Mathf.Min(limit, (arena.yMax - start.y) / delta.y);
        else if (delta.y < 0f) limit = Mathf.Min(limit, (arena.yMin - start.y) / delta.y);
        return start + delta * Mathf.Clamp01(limit);
    }

    /// <summary>같은 보스의 현재 전투 수명에서 레이저를 유지할 수 있는지 확인</summary>
    private bool OwnerReady()
    {
        return isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && owner.IsInitialized
            && !owner.IsDead && owner.IsInCombat && !owner.IsGroggy && !owner.IsCutScene;
    }
}
