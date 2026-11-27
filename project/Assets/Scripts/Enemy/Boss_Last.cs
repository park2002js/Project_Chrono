using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>순간이동 유도탄·자수정 융기·전기 기둥·수정 낙하·추적 차원문·차원문 내려찍기를 실행할 최종 보스</summary>
[DefaultExecutionOrder(1)] // Player 이동 이후의 위치를 읽어 같은 프레임의 조준에 사용
public partial class Boss_Last : BossCharacter
{
    public SpriteRenderer Body; // 몸 그림을 표시할 렌더러
    public Transform Visual; // 공중에 뜰 때 위로 올릴 몸 묶음
    public SpriteRenderer Shadow; // 실제 위치의 바닥에 남길 그림자
    public Hitbox BodyHitbox; // 지상에 있을 때만 열어 둘 몸 피격 영역
    public SpriteClipPlayer HandFxBack; // 손 뒤쪽에 겹칠 시전 효과 재생기
    public SpriteClipPlayer HandFxFront; // 손 앞쪽에 겹칠 시전 효과 재생기
    public SpriteClip[] Clips = Array.Empty<SpriteClip>(); // 이름으로 찾아 쓸 몸·효과 클립 전체
    public Sprite PillarSprite; // 떨어진 뒤 서 있는 전기 기둥
    public Sprite PillarShadowSprite; // 서 있는 기둥의 바닥 그림자
    public Sprite CrystalShadowSprite; // 솟은 수정의 바닥 그림자
    public Material SpriteMaterial; // 효과를 다른 월드 Sprite와 같게 그릴 재질
    public Material GoldMaterial; // 금빛 반투명 표면과 금색 테두리를 입힐 재질
    public AreaWarningVfx WarningPrefab; // 공용 공격 예고 프리팹
    public NormalProjectile NormalPrefab; // 패턴 5에서 수정에 착지할 때 여덟 방향으로 쏠 빨강 일반탄
    public GoldProjectile GoldFastPrefab; // 패턴 6에서 쏠 황금 특수탄
    public HomingProjectile HomingPrefab; // 패턴 1에서 쏠 유도탄
    public int ForcePattern; // 0이면 일반 추첨, 1·2·3·5·6·7이면 그 번호의 패턴만 반복
    public Vector2 ArenaSize = new Vector2(23f, 12f); // 조우 영역 중심을 기준으로 패턴이 쓸 전투 구역의 크기
    public float GridCellSize = 0.5f; // 유도탄 경로 격자 한 칸의 크기
    public float AirAltitude = 1.5f; // 공중 판정 동안 몸을 띄울 높이
    public float AirRiseDuration = 0.3f; // 뜨거나 내려오는 데 걸리는 게임 시간
    public float FallHeight = 4.5f; // 내려찍기를 시작하는 높이이며 그 자리의 차원문이 Player 중심 화면 안에 보이는 값
    public float FallDuration = 0.06f; // 내려찍기가 바닥에 닿기까지의 게임 시간
    public Color AfterimageColor = new Color(0.75f, 0.8f, 0.95f, 0.5f); // 대시 잔상의 색과 처음 알파
    public Vector2[] TeleportOffsets = { new Vector2(0f, 3.5f), new Vector2(-6.875f, -3.875f), new Vector2(6.875f, -3.875f) }; // 패턴 1에서 구역 중심 기준 상단·왼쪽 아래·오른쪽 아래 위치
    public Vector2[] CastOffsets = { new Vector2(-4.5f, 2.5f), new Vector2(4.5f, 2.5f), new Vector2(-4.5f, -1.5f), new Vector2(4.5f, -1.5f),
        new Vector2(0f, 4.5f), new Vector2(0f, -3.5f), new Vector2(-9f, 0.5f), new Vector2(9f, 0.5f) }; // 패턴 2에서 구역 중심 기준으로 지정한 시전 위치
    public Vector2Int ShadowPixels = new Vector2Int(28, 6); // 바닥 그림자 타원의 원본 픽셀 크기
    public float PatternDamage = 10f; // 탄환과 작은 범위 공격의 피해
    public float HeavyDamage = 15f; // 내려찍기와 폭발의 피해
    public AudioClip VanishSound; // 가로선으로 사라지기 시작할 때 낼 소리
    public AudioClip AppearSound; // 가로선으로 나타날 때 낼 소리
    public AudioClip RiftOpenSound; // 차원문이 열려 있는 동안 낼 소리이며 열린 차원문이 없으면 멈춤
    public AudioClip RiftCloseSound; // 차원문이 닫힐 때마다 낼 소리
    public AudioClip CounterSound; // Slash 카운터가 가능해지는 순간에 낼 알림음
    public AudioClip FallSound; // 하늘에서 내려찍기 시작할 때 낼 소리
    public AudioClip LandSound; // 내려찍기가 바닥에 닿을 때 낼 소리
    public AudioClip CrystalBurstSound; // 패턴 5에서 착지에 수정이 폭발할 때 낼 소리
    public AudioClip AmethystCastSound; // 패턴 2에서 시전 동작을 시작할 때 낼 소리
    public AudioClip AmethystRiseSound; // 패턴 2에서 자수정이 솟을 때마다 낼 소리
    public AudioClip HitSound; // 피해를 받을 때마다 낼 소리
    public AudioClip GroggySound; // 그로기에 빠지는 순간에 낼 소리
    [Range(0f, 1f)] public float SoundVolume = 0.8f; // 단발 효과음의 기본 음량
    [Range(0f, 1f)] public float RiftVolume = 0.5f; // 차원문이 열려 있는 동안의 소리 음량

    private const float Frame = 0.05f; // 원본 한 장의 표시 시간
    private const int BackOrder = 16; // 발끝 기준으로 정렬되는 캐릭터·구조물 전체보다 뒤에 그릴 효과의 정렬 순서
    private const int FrontOrder = 24; // 발끝 기준으로 정렬되는 캐릭터·구조물 전체보다 앞에 그릴 효과의 정렬 순서
    private const int GroundFxOrder = 13; // 바닥에 깔리는 효과의 정렬 순서
    private const int ShadowOrder = 14; // 그림자의 정렬 순서
    private const int FrontFxOrder = 34; // 캐릭터 위에 그리는 효과의 정렬 순서
    private static readonly Vector2 HandRaised = new Vector2(1f, 2.3125f); // 시전 준비 자세의 손끝 위치
    private static readonly Vector2 HandLowered = new Vector2(1.25f, 0.5625f); // 내려 뻗은 자세의 손끝 위치
    private static readonly Vector2 HandSky = new Vector2(0.625f, 3f); // 하늘로 든 자세의 손끝 위치
    private readonly Dictionary<string, SpriteClip> clipLookup = new Dictionary<string, SpriteClip>(); // 이름으로 클립을 찾을 표
    private readonly List<AreaWarningVfx> warnings = new List<AreaWarningVfx>(); // 패턴이 번호로 빌려 쓰는 예고 묶음
    private readonly List<GameObject> spawned = new List<GameObject>(); // 패턴이 끝나거나 취소될 때 지울 차원문·기둥·수정
    private readonly Collider2D[] overlaps = new Collider2D[16]; // 범위 피해 조회에 다시 쓸 버퍼
    private readonly GridPathfinder grid = new GridPathfinder(); // 유도탄이 쓸 전투 구역 격자
    private BossSpawner bossSpawner; // 조우 영역에서 전투 구역을 읽을 자기 생성 담당
    private DepthSorter depthSorter; // 발끝 높이로 다른 캐릭터·구조물·탄환과의 앞뒤를 정할 자기 공통 정렬
    private SpriteClip bodyClip; // 지금 재생 중인 몸 클립
    private float bodyElapsed; // 몸 클립을 재생한 게임 시간
    private bool isHidden; // 필드에서 사라진 상태
    private float altitude; // 현재 몸을 띄운 높이
    private float altitudeTarget; // 도달하려는 높이
    private float goldRemaining; // 금빛 강조를 유지할 남은 시간이며 음수는 끌 때까지 유지
    private bool counterOpen; // 지금 Slash에 맞으면 그로기에 빠지는 상태
    private Action counterAction; // 카운터가 성공한 순간 패턴이 추가로 처리할 일
    private Material bodyMaterial; // 금빛 강조를 끄면 되돌릴 몸의 원래 재질
    private Rect gridArea; // 마지막으로 격자를 만든 구역
    private bool deathPlayed; // 사망 동작을 이미 시작했는지 여부
    private AudioSource soundSource; // 단발 효과음을 겹쳐 낼 자기 오디오 소스
    private AudioSource riftSource; // 열려 있는 차원문의 소리를 내다가 멈출 자기 오디오 소스
    private int openRifts; // 지금 열려 있는 차원문 수

    protected override void OnDestroy()
    {
        // 공통 전투 연결을 해제한 뒤 보스 밖에 만들어 둔 예고와 생성물 정리
        base.OnDestroy();
        for (int i = 0; i < warnings.Count; i++)
            if (warnings[i] != null) Destroy(warnings[i].gameObject);
        ClearSpawned();
    }

    /// <summary>패턴 진행과 무관하게 몸 동작·높이·정렬·금빛 강조를 게임 시간으로 갱신</summary>
    private void LateUpdate()
    {
        if (!PatternsInitialized || Body == null) return;

        // 사망은 처치 연출로 게임 시간이 멈춘 동안에도 첫 장부터 바로 표시
        if (IsDead)
        {
            if (!deathPlayed)
            {
                deathPlayed = true;
                ResetPresentation();
                PlayBody("body-die");
            }

            // 흩어지며 사라지는 동작은 사체 표시용 회색을 입히지 않고 원래 색으로 재생
            Body.color = Color.white;
        }
        if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        float delta = Time.deltaTime;

        // 그로기는 패턴 밖에서 몸 동작을 직접 고름
        if (!IsDead)
        {
            deathPlayed = false;
            if (IsGroggy && bodyClip != Clip("body-groggy")) PlayBody("body-groggy");
            else if (!IsGroggy && bodyClip == Clip("body-groggy")) PlayBody("body-idle");
        }
        TickBody(delta);

        // 목표 높이로 일정 속도로 이동하고 그림자는 바닥에 그대로 둠
        if (!Mathf.Approximately(altitude, altitudeTarget))
        {
            float speed = AirRiseDuration > 0f ? Mathf.Max(AirAltitude, 0.01f) / AirRiseDuration : float.PositiveInfinity;
            altitude = Mathf.MoveTowards(altitude, altitudeTarget, speed * delta);
        }
        Visual.localPosition = new Vector3(0f, altitude, 0f);
        Shadow.enabled = !isHidden && !(IsDead && bodyElapsed > 0.5f);

        // 앞뒤는 발끝 높이의 공통 정렬에 맡기고 공중에 뜬 동안에만 다른 대상 앞에 그림
        depthSorter.AlwaysInFront = altitude > 0.1f;
        Vector3 hand = GetHandPoint();
        HandFxBack.transform.position = hand;
        HandFxFront.transform.position = hand;

        // 시간이 정해진 금빛 강조는 끝나면 스스로 꺼짐
        if (goldRemaining > 0f)
        {
            goldRemaining -= delta;
            if (goldRemaining <= 0f) SetBodyGold(false);
        }
    }

    /// <summary>자기 프리팹의 참조를 확인하고 여섯 패턴 등록</summary>
    protected override void InitializePatterns()
    {
        // Player 생성 전에는 자기 프리팹에 직렬화된 참조만 확인
        bossSpawner = GetComponent<BossSpawner>();
        depthSorter = GetComponent<DepthSorter>();
        if (depthSorter == null || Body == null || Visual == null || Shadow == null || BodyHitbox == null || HandFxBack == null || HandFxFront == null
            || WarningPrefab == null || NormalPrefab == null || GoldFastPrefab == null || HomingPrefab == null
            || GoldMaterial == null || PillarSprite == null || bossSpawner == null)
            throw new InvalidOperationException("Boss_Last requires its depth sorter, body, shadow, hitbox, hand effects, bullet and warning prefabs, gold material and spawner.");
        foreach (SpriteClip clip in Clips)
            if (clip != null) clipLookup[clip.name] = clip;
        bodyMaterial = Body.sharedMaterial;

        // 단발 효과음과 차원문 유지음을 따로 낼 2D 오디오 소스를 한 번만 준비
        if (soundSource == null) soundSource = CreateAudioSource();
        if (riftSource == null) riftSource = CreateAudioSource();
        if (Shadow.sprite == null) Shadow.sprite = PixelShapeTexture.GetSolidEllipse(ShadowPixels.x, ShadowPixels.y);
        PlayBody("body-idle");
        PatternSelector.SetPatterns(new TeleportVolleyPattern(this), new AmethystLinePattern(this), new ShockPillarPattern(this),
            new CrystalDescentPattern(this), new TrackingPortalPattern(this), new PortalSlamPattern(this));
    }

    /// <summary>선택된 패턴이 없는 동안 Player를 바라보며 대기 동작 반복</summary>
    protected override void TickIdle(float gameDelta)
    {
        // 한 패턴만 반복하도록 지정했으면 직전 제외와 쿨타임 이력을 매번 비움
        if (ForcePattern > 0) PatternSelector.ResetHistory();
        if (bodyClip != Clip("body-idle") && IsBodyFinished) PlayBody("body-idle");
        if (Target != null) FaceTowards(Target.transform.position);
    }

    /// <summary>전투 종료·그로기·컷씬에서 몸과 생성물을 기본 상태로 복원</summary>
    protected override void CancelIdleAction()
    {
        ResetPresentation();
    }

    /// <summary>카운터가 열린 동안 몸에 닿은 Slash 피해를 그로기로 연결</summary>
    public override bool ApplyDamage(DamageInstance damage)
    {
        bool applied = base.ApplyDamage(damage);
        if (applied) PlaySound(HitSound);
        if (applied && counterOpen && damage.Kind == DamageType.Slash && !IsDead) TriggerCounter(GetBodyCenter());
        return applied;
    }

    /// <summary>단발 효과음을 설정의 효과음 배율로 겹쳐 재생하며 클립이 비어 있으면 무시</summary>
    public void PlaySound(AudioClip clip, float scale = 1f)
    {
        if (clip == null || soundSource == null) return;
        soundSource.PlayOneShot(clip, SoundVolume * scale * GameSettings.SfxVolume);
    }

    /// <summary>이름으로 클립을 찾고 없으면 실패 처리</summary>
    private SpriteClip Clip(string clipName)
    {
        if (clipLookup.TryGetValue(clipName, out SpriteClip clip)) return clip;
        throw new InvalidOperationException("Boss_Last clip is missing: " + clipName);
    }

    /// <summary>몸 클립을 처음부터 재생하며 단발 클립은 마지막 장에서 멈춤</summary>
    private void PlayBody(string clipName)
    {
        bodyClip = Clip(clipName);
        bodyElapsed = 0f;
        ApplyBodyFrame();

        // 사라짐·나타남·내려찍기 시작·그로기는 그 동작이 시작되는 순간에 소리를 함께 냄
        if (clipName.StartsWith("vanish", StringComparison.Ordinal)) PlaySound(VanishSound);
        else if (clipName.StartsWith("appear", StringComparison.Ordinal)) PlaySound(AppearSound);
        else if (clipName == "body-wave-fall") PlaySound(FallSound);
        else if (clipName == "body-groggy") PlaySound(GroggySound);
    }

    private bool IsBodyFinished => bodyClip == null || (!bodyClip.Loop && bodyElapsed >= bodyClip.GetLength()); // 단발 몸 클립이 끝까지 재생됐는지 여부

    /// <summary>몸 클립의 시간을 진행하고 현재 장 표시</summary>
    private void TickBody(float delta)
    {
        if (bodyClip == null) return;
        bodyElapsed += delta;
        ApplyBodyFrame();
    }

    /// <summary>현재 시간의 장을 표시하고 끝난 단발 클립은 마지막 장 유지</summary>
    private void ApplyBodyFrame()
    {
        int index = bodyClip.GetFrameIndex(bodyElapsed);
        if (index < 0) index = bodyClip.FrameCount - 1;

        // 사망 동작은 마지막 장 뒤에 몸을 완전히 숨김
        bool ended = IsDead && deathPlayed && IsBodyFinished;
        Body.sprite = index >= 0 ? bodyClip.Frames[index] : null;
        Body.enabled = !isHidden && !ended && Body.sprite != null;
    }

    /// <summary>지정한 지점이 있는 쪽을 바라보도록 좌우 반전</summary>
    private void FaceTowards(Vector2 point)
    {
        float offset = point.x - transform.position.x;
        if (Mathf.Abs(offset) > 0.05f) Body.flipX = offset < 0f;
    }

    /// <summary>현재 자세와 좌우 반전·높이를 반영한 손끝의 월드 위치</summary>
    private Vector3 GetHandPoint()
    {
        Vector2 offset = bodyClip == Clip("body-wave-begin") ? HandSky
            : bodyClip == Clip("body-attack-ready") || bodyClip == Clip("body-attack-ready-loop") ? HandRaised : HandLowered;
        if (Body.flipX) offset.x = -offset.x;
        return transform.position + new Vector3(offset.x, offset.y + altitude, 0f);
    }

    /// <summary>카운터 효과와 그로기 구슬이 닿을 몸 중심</summary>
    private Vector2 GetBodyCenter()
    {
        return (Vector2)transform.position + new Vector2(0f, 1.3f + altitude);
    }

    /// <summary>손끝에 붙는 시전 효과를 뒤·앞 두 겹으로 재생하고 이름이 비면 그 겹을 끔</summary>
    private void PlayHandFx(string backClip, string frontClip)
    {
        SetPlayer(HandFxBack, backClip);
        SetPlayer(HandFxFront, frontClip);
    }

    /// <summary>재생기 하나를 켜서 클립을 재생하거나 꺼 둠</summary>
    private void SetPlayer(SpriteClipPlayer player, string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            player.Stop();
            player.gameObject.SetActive(false);
            return;
        }
        player.gameObject.SetActive(true);
        player.Play(Clip(clipName));
    }

    /// <summary>필드에서 사라지거나 다시 나타나며 사라진 동안에는 피격 영역도 닫음</summary>
    private void SetHidden(bool hidden)
    {
        isHidden = hidden;
        Body.enabled = !hidden && Body.sprite != null;
        Shadow.enabled = !hidden;
        UpdateHitbox();
    }

    /// <summary>공중 판정을 켜고 끄며 목표 높이로 서서히 이동</summary>
    private void SetAirborne(bool airborne)
    {
        altitudeTarget = airborne ? AirAltitude : 0f;
        UpdateHitbox();
    }

    /// <summary>높이를 즉시 지정</summary>
    private void SetAltitude(float value)
    {
        altitude = altitudeTarget = Mathf.Max(0f, value);
        Visual.localPosition = new Vector3(0f, altitude, 0f);
        UpdateHitbox();
    }

    /// <summary>지상에 보이는 동안에만 일반 공격과 Slash가 몸에 닿도록 설정</summary>
    private void UpdateHitbox()
    {
        BodyHitbox.SetEnabled(!isHidden && altitudeTarget <= 0.01f);
    }

    /// <summary>몸에 금빛 표면과 테두리를 입히며 시간이 0 이하이면 끌 때까지 유지</summary>
    private void SetBodyGold(bool active, float duration = 0f)
    {
        Body.sharedMaterial = active ? GoldMaterial : bodyMaterial;
        goldRemaining = active && duration > 0f ? duration : 0f;
    }

    /// <summary>지금부터 몸에 닿는 Slash를 카운터로 받도록 열거나 닫음</summary>
    private void SetCounter(bool open, Action onSuccess = null)
    {
        if (open && !counterOpen) PlaySound(CounterSound); // 닫혀 있다가 열리는 순간에만 알림
        counterOpen = open;
        counterAction = open ? onSuccess : null;
    }

    /// <summary>카운터 성공: 금빛 접점 효과를 띄우고 패턴 후처리 뒤 그로기 진입</summary>
    private void TriggerCounter(Vector2 point)
    {
        Action action = counterAction;
        SetCounter(false);
        SpawnFx("counter-gold-front", point, FrontFxOrder);
        action?.Invoke();
        EnterGroggy();
    }

    /// <summary>몸·높이·손 효과·예고·생성물을 패턴 시작 전 상태로 복원</summary>
    private void ResetPresentation()
    {
        if (Body == null || bodyMaterial == null) return;
        SetCounter(false);
        SetBodyGold(false);
        SetHidden(false);
        SetAltitude(0f);
        PlayHandFx(null, null);
        for (int i = 0; i < warnings.Count; i++)
            if (warnings[i] != null) warnings[i].Hide();
        ClearSpawned();
        StopRiftSound();
        if (!IsDead && !IsGroggy && clipLookup.Count > 0) PlayBody("body-idle");
    }

    /// <summary>거리와 무관하게 들리는 반복 없는 오디오 소스 하나 추가</summary>
    private AudioSource CreateAudioSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    /// <summary>차원문이 열릴 때마다 열림 소리를 처음부터 다시 재생</summary>
    private void PlayRiftOpen()
    {
        openRifts++;
        if (RiftOpenSound == null || riftSource == null) return;
        riftSource.clip = RiftOpenSound;
        riftSource.volume = RiftVolume * GameSettings.SfxVolume;
        riftSource.Play();
    }

    /// <summary>차원문이 닫힐 때 닫힘 소리를 내고 열린 차원문이 더 없으면 열림 소리 정지</summary>
    private void PlayRiftClose()
    {
        openRifts = Mathf.Max(0, openRifts - 1);
        if (openRifts == 0 && riftSource != null) riftSource.Stop();
        PlaySound(RiftCloseSound, 0.6f);
    }

    /// <summary>패턴이 취소되어 차원문이 한꺼번에 지워질 때 열림 소리 정지</summary>
    private void StopRiftSound()
    {
        openRifts = 0;
        if (riftSource != null) riftSource.Stop();
    }

    /// <summary>번호로 예고 하나를 빌리고 모자라면 보스와 같은 부모 아래에 새로 만듦</summary>
    private AreaWarningVfx GetWarning(int index)
    {
        while (warnings.Count <= index) warnings.Add(Instantiate(WarningPrefab, transform.parent));
        return warnings[index];
    }

    /// <summary>월드의 한 지점에 단발 효과를 재생하고 끝나면 스스로 사라지게 함</summary>
    private SpriteClipPlayer SpawnFx(string clipName, Vector2 position, int order, float angle = 0f, bool flip = false)
    {
        SpriteClipPlayer player = SpriteClipPlayer.Spawn(Clip(clipName), new Vector3(position.x, position.y, transform.position.z), order, SpriteMaterial, transform.parent);
        if (player == null) return null;
        player.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        player.GetComponent<SpriteRenderer>().flipX = flip;
        return player;
    }

    /// <summary>패턴이 직접 재생·제거를 맡는 표시 객체 하나 생성</summary>
    private SpriteClipPlayer CreatePlayer(string objectName, Vector2 position, int order, Transform parent = null)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(parent != null ? parent : transform.parent, false);
        child.transform.position = new Vector3(position.x, position.y, transform.position.z);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = SpriteMaterial;
        renderer.sortingOrder = order;
        var player = child.AddComponent<SpriteClipPlayer>();
        if (parent == null) spawned.Add(child);
        return player;
    }

    /// <summary>패턴이 만든 차원문·기둥·수정을 모두 제거</summary>
    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();
    }

    /// <summary>조우 영역의 중심에 놓인 전투 구역 사각형</summary>
    private Rect GetArena()
    {
        Vector2 center = BasePosition;
        if (bossSpawner != null && bossSpawner.EncounterTrigger is BoxCollider2D box) center = box.transform.TransformPoint(box.offset);
        return new Rect(center - ArenaSize * 0.5f, ArenaSize);
    }

    /// <summary>지점이 전투 구역 안이고 맵의 벽에 걸치지 않는지 확인</summary>
    private bool IsOpen(Vector2 point, float radius = 0.4f)
    {
        if (!GetArena().Contains(point)) return false;
        MapManager map = MapManager.Instance;
        if (map == null) return true;
        int count = Physics2D.OverlapCircle(point, radius, new ContactFilter2D { useTriggers = false }, overlaps);
        for (int i = 0; i < count; i++)
            if (map.IsWall(overlaps[i])) return false;
        return true;
    }

    /// <summary>전투 구역 안에서 벽·피할 지점과 겹치지 않는 무작위 위치를 고르고 실패하면 구역 중심 반환</summary>
    private Vector2 GetRandomPoint(float margin, Vector2 avoid, float avoidDistance, List<Vector2> others = null, float spacing = 0f)
    {
        Rect arena = GetArena();
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var point = new Vector2(UnityEngine.Random.Range(arena.xMin + margin, arena.xMax - margin), UnityEngine.Random.Range(arena.yMin + margin, arena.yMax - margin));
            if ((point - avoid).sqrMagnitude < avoidDistance * avoidDistance || !IsOpen(point)) continue;
            bool crowded = false;
            if (others != null)
                for (int i = 0; i < others.Count && !crowded; i++) crowded = (others[i] - point).sqrMagnitude < spacing * spacing;
            if (!crowded) return point;
        }
        return arena.center;
    }

    /// <summary>기준 지점 주변의 고리 안에서 후보를 여러 개 뽑아 이미 놓은 지점들과 가장 멀리 떨어진 후보를 골라 서로 뭉치지 않게 함</summary>
    private Vector2 GetSpreadPoint(Vector2 focus, float minRadius, float maxRadius, List<Vector2> others, int candidates)
    {
        Vector2 best = focus;
        float bestScore = float.NegativeInfinity;
        float inner = Mathf.Max(0f, Mathf.Min(minRadius, maxRadius)), outer = Mathf.Max(inner + 0.1f, maxRadius);
        for (int i = 0; i < Mathf.Max(1, candidates); i++)
        {
            // 고리 안의 어느 자리든 같은 확률로 뽑히도록 반지름을 넓이 기준으로 고름
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, UnityEngine.Random.value));
            Vector2 point = focus + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (!IsOpen(point)) continue;

            // 가장 가까운 기존 지점까지의 거리를 점수로 삼고 첫 지점은 무작위로 고름
            float score = UnityEngine.Random.value;
            if (others != null && others.Count > 0)
            {
                score = float.PositiveInfinity;
                for (int j = 0; j < others.Count; j++) score = Mathf.Min(score, (others[j] - point).magnitude);
            }
            if (score <= bestScore) continue;
            best = point;
            bestScore = score;
        }

        // 고리 전체가 벽이나 구역 밖이면 구역 안의 무작위 지점으로 대신함
        return float.IsNegativeInfinity(bestScore) ? GetRandomPoint(1.5f, focus, inner, others, 2f) : best;
    }

    /// <summary>유도탄이 쓸 전투 구역 격자를 준비하며 구역이 그대로면 다시 만들지 않음</summary>
    private GridPathfinder GetGrid()
    {
        Rect arena = GetArena();
        if (grid.Width > 0 && gridArea == arena) return grid;
        gridArea = arena;
        float radius = Mathf.Max(0.1f, GridCellSize * 0.45f);
        grid.Build(arena, GridCellSize, point => !IsOpen(point, radius));
        return grid;
    }

    /// <summary>타원 안에 걸친 Player 피격 영역에 한 번 피해 전달</summary>
    private bool DamageEllipse(Vector2 center, Vector2 size, float damage, int attackId)
    {
        // 타원을 감싸는 사각 범위의 Collider를 모은 뒤 가장 가까운 점이 타원 안인지 확인
        float radiusX = Mathf.Max(0.01f, size.x * 0.5f), radiusY = Mathf.Max(0.01f, size.y * 0.5f);
        int count = Physics2D.OverlapBox(center, size, 0f, new ContactFilter2D { useTriggers = true }, overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter)) continue;
            Vector2 closest = other.ClosestPoint(center) - center;
            float x = closest.x / radiusX, y = closest.y / radiusY;
            if (x * x + y * y > 1f) continue;
            if (hitbox.ReceiveHit(new DamageInstance(this, damage, DamageType.Normal, attackId))) return true;
        }
        return false;
    }

    /// <summary>전투가 이어지는 동안에만 탄환을 만들어 보스와 분리된 수명으로 발사</summary>
    private Projectile Fire(Projectile prefab, Vector2 position, Vector2 direction, float speed, float lifetime, float damage)
    {
        if (!IsInCombat || IsDead || IsGroggy || Target == null || Target.IsDead || direction.sqrMagnitude <= 0f) return null;
        Projectile projectile = Instantiate(prefab, new Vector3(position.x, position.y, transform.position.z), Quaternion.identity, transform.parent);
        projectile.Initialize(this, direction, speed, lifetime, damage);
        return projectile;
    }

    /// <summary>Slash로만 지울 수 있는 기둥·수정의 판정 객체를 만들고 처음에는 닫아 두며 밑동 높이로 앞뒤 정렬</summary>
    private BossStructure CreateStructure(string objectName, Vector2 position, Vector2 size, Vector2 offset, int partId)
    {
        var body = new GameObject(objectName);
        body.transform.SetParent(transform.parent, false);
        body.transform.position = new Vector3(position.x, position.y, transform.position.z);
        var shape = body.AddComponent<BoxCollider2D>();
        shape.isTrigger = true;
        shape.size = size;
        shape.offset = offset;
        var structure = body.AddComponent<BossStructure>();
        structure.Owner = this;
        structure.PartId = partId;
        structure.AcceptsNormalAttack = false;
        structure.HP = 1f;
        body.AddComponent<Hitbox>();
        body.AddComponent<DepthSorter>();
        structure.Remove();
        spawned.Add(body);
        return structure;
    }

    /// <summary>패턴 공통의 실행 조건: 살아 있는 전투 대상과 반복 지정 번호 확인</summary>
    private bool CanStart(BossCharacter owner, BossPattern pattern, int number)
    {
        return owner == this && Target != null && !Target.IsDead && (ForcePattern == 0 || ForcePattern == number);
    }
}
