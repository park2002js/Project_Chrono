using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>경비의 두 손이 수행하는 공격 종류</summary>
public enum GuardHandMode
{
    None, // 두 손이 기본 위치에 있는 대기 상태
    IdleSlam, // 패턴 3: 한 손이 표적을 추적한 뒤 내려찍기
    CentralSlam, // 패턴 4: 두 손을 모아 충전한 뒤 중앙 내려찍기
    EdgeWave // 패턴 7: 한 손이 가장자리에서 충전한 뒤 충격파 내려찍기
}

/// <summary>손 공격 한 번의 진행 단계</summary>
public enum GuardHandPhase
{
    Idle, // 다음 실행을 기다리는 상태
    Approach, // 표적을 추적하거나 지정 위치로 이동하며 상승
    Charge, // 공중에서 충전 효과와 함께 힘을 모으는 구간
    Lock, // 착지점을 고정하고 예고B를 재생하는 구간
    Drop, // 스미어와 함께 지면으로 떨어지는 구간
    Land, // 착지 뒤 지면에 머무는 구간
    Return // 기본 위치로 돌아가는 구간
}

/// <summary>경비 두 손의 교대 내려찍기·양손 중앙 내려찍기·한 손 충격파를 같은 단계 흐름으로 진행</summary>
public class GuardHandAttack : MonoBehaviour
{
    public Transform[] Hands = Array.Empty<Transform>(); // 왼손·오른손 순서의 표시 Transform
    public SpriteRenderer[] HandRenderers = Array.Empty<SpriteRenderer>(); // 왼손·오른손의 주먹 렌더러
    public SpriteRenderer[] HandShadows = Array.Empty<SpriteRenderer>(); // 각 손의 지면 투영 위치에 놓을 그림자
    public SpriteRenderer BodyShadow; // 몸체 아래에 항상 표시할 그림자
    public Vector2Int HandShadowPixels = new Vector2Int(20, 7); // 손 그림자 타원의 원본 픽셀 크기
    public Vector2Int BodyShadowPixels = new Vector2Int(58, 9); // 몸체 그림자 타원의 원본 픽셀 크기
    public Sprite[] HandSprites = Array.Empty<Sprite>(); // 왼손·오른손의 기본 주먹 그림
    public SpriteClip[] HandSmearClips = Array.Empty<SpriteClip>(); // 왼손·오른손의 하강 스미어 두 장
    public SpriteRenderer CombinedRenderer; // 두 손을 모았을 때 표시할 합친 주먹
    public Sprite CombinedSprite; // 합친 주먹의 기본 그림
    public SpriteClip CombinedSmearClip; // 합친 주먹의 하강 스미어 두 장
    public SpriteClipPlayer ChargeBack; // 충전 시작부터 착지 순간까지 손 뒤에서 재생할 충전 전류
    public SpriteClipPlayer ChargeFront; // 충전 시작부터 착지 순간까지 손 앞에서 재생할 충전 섬광
    public SpriteClip ChargeBackClip; // 충전 전류 클립
    public SpriteClip ChargeFrontClip; // 충전 섬광 클립
    public SpriteClip StompRingClip; // 교대 내려찍기의 착지 고리
    public SpriteClip BurstClip; // 충전 내려찍기의 수직 폭발
    public SpriteClip CrackClip; // 충전 내려찍기의 바닥 균열
    public SpriteClip DustClip; // 한 손 충격파의 흙먼지
    public SpriteClip GroundHitClip; // 부채꼴로 번지는 지면 타격
    public AreaWarningVfx WarningPrefab; // 타원·부채꼴 예고로 복제해 쓸 공격 예고 프리팹
    public Material SpriteMaterial; // 착지 효과를 다른 월드 Sprite와 같게 그릴 재질
    public AudioSource Audio; // 내려찍기·충전 소리를 낼 오디오 소스
    public AudioClip SlamClip; // 교대 내려찍기의 착지음
    public AudioClip ChargedSlamClip; // 충전 내려찍기의 착지음
    public AudioClip ChargeClip; // 주먹 충전 중 반복할 소리
    public AudioClip WaveBlastClip; // 패턴 7의 지면 타격이 터질 때 낼 소리
    [Range(0f, 1f)] public float Volume = 0.8f; // 손 공격 소리의 음량
    public LayerMask CollisionMask = ~0; // 착지 순간 Player 피격 영역을 수집할 레이어
    public float LockDuration = 0.6f; // 착지점을 고정하고 예고B를 보여줄 게임 시간
    public float DropDuration = 0.06f; // 스미어 두 장과 함께 지면까지 떨어지는 게임 시간
    public float RiseDuration = 0.3f; // 지면에서 공중 높이까지 올라가는 게임 시간
    public float IdleTrackDuration = 1f; // 패턴 3에서 표적을 추적하며 예고A를 보여줄 게임 시간
    public float IdleTrackingSpeed = 10f; // 패턴 3에서 손이 표적 위로 따라가는 초당 거리
    public float IdleHoverHeight = 1f; // 패턴 3에서 손이 떠 있는 높이
    public Vector2 IdleImpactSize = new Vector2(2.5f, 1.25f); // 패턴 3의 타원 예고와 피해 범위
    public float IdleDamage = 10f; // 패턴 3의 착지 피해
    public float IdleLandDuration = 0.17f; // 패턴 3에서 착지 뒤 지면에 머무는 게임 시간
    public float IdleReturnDuration = 0.37f; // 패턴 3에서 기본 위치로 돌아가는 게임 시간
    public float CentralApproachDuration = 0.5f; // 패턴 4에서 두 손이 모이는 게임 시간
    public float CentralChargeDuration = 1.5f; // 패턴 4에서 힘을 모으는 게임 시간
    public float CentralHoverHeight = 2.6f; // 패턴 4에서 합친 주먹 바닥이 지면에서 떠 있는 높이
    public Vector2 CentralImpactSize = new Vector2(4f, 2.5f); // 패턴 4의 타원 예고와 피해 범위
    public float CentralDamage = 15f; // 패턴 4의 착지 피해
    public float CentralLandDuration = 0.35f; // 패턴 4에서 착지 뒤 지면에 머무는 게임 시간
    public float CentralReturnDuration = 0.55f; // 패턴 4에서 두 손이 갈라져 돌아가는 게임 시간
    public float CentralHandGap = 0.97f; // 모인 두 손의 중심 사이 거리
    public float EdgeApproachDuration = 0.5f; // 패턴 7에서 가장자리로 이동하는 게임 시간
    public float EdgeChargeDuration = 3.5f; // 패턴 7에서 기를 모으는 게임 시간
    public float EdgeHoverHeight = 2.6f; // 패턴 7에서 주먹 바닥이 지면에서 떠 있는 높이
    public Vector2 EdgeImpactSize = new Vector2(3f, 1.75f); // 패턴 7의 주먹 아래 타원 예고와 피해 범위
    public float EdgeDamage = 15f; // 패턴 7의 타원 착지 피해
    public float EdgeLandDuration = 0.35f; // 패턴 7에서 착지 뒤 지면에 머무는 게임 시간
    public float EdgeReturnDuration = 0.56f; // 패턴 7에서 기본 위치로 돌아가는 게임 시간
    public float WaveAngle = 35f; // 패턴 7 부채꼴의 중심각
    public float WaveRadius = 11f; // 패턴이 닿을 거리를 따로 넘기지 않았을 때 쓸 패턴 7 부채꼴의 반지름
    public float WaveDuration = 0.6f; // 착지 뒤 가장 먼 자리의 지면 타격이 터지기까지의 게임 시간
    public float WaveDamage = 10f; // 지면 타격이 터지는 자리에 있던 Player에게 충격파 한 번당 한 번 주는 피해
    public float WaveRingSpacing = 1f; // 지면 타격을 놓을 반지름 방향 간격
    public float WaveArcSpacing = 1.5f; // 같은 거리에서 지면 타격을 놓을 원호 방향 간격
    public float WaveHitRadius = 0.9f; // 지면 타격 한 곳이 폭발 순간 피해를 주는 반지름
    [Range(0f, 0.5f)] public float WaveRingTwist = 0.5f; // 원호마다 그 줄의 자리 전체를 좌우로 돌릴 최대 폭이며 자리 사이 각도에 대한 비율
    public float WaveSpotJitter = 0.3f; // 자리마다 반지름 방향과 원호 방향으로 따로 어긋나게 할 최대 거리
    public float WaveBlastInterval = 0.12f; // 지면 타격음을 다시 낼 수 있는 최소 간격이며 그보다 촘촘히 터지는 자리는 소리 없이 지나감
    [Range(0f, 1f)] public float WaveBlastVolume = 0.6f; // 지면 타격음의 음량이며 손 공격 소리의 음량에 곱함
    public SpriteClip WaveTelegraphClip; // 각 폭발 자리에 미리 띄울 예고 효과이며 비어 있으면 부채꼴 예고로 대신 표시
    public float WaveTelegraphDuration = 0.6f; // 각 자리의 폭발보다 예고 효과를 먼저 띄울 게임 시간이며 예고 클립 길이와 같게 설정
    public float ChargeHandCenter = 0.9f; // 주먹 바닥에서 충전 효과 중심까지의 높이
    public float ReturnHopHeight = 0.5f; // 기본 위치로 돌아갈 때 손을 들어 올리는 높이
    public float ShadowOpacity = 0.35f; // 손 그림자의 알파
    public int GroundFxOrder = 15; // 착지 고리·균열을 바닥과 예고·그림자 위에 그릴 정렬 순서
    public int FrontFxOrder = 30; // 폭발·흙먼지를 손 앞에 그릴 정렬 순서
    public int WaveFxOrder = 23; // 지면 타격을 그릴 정렬 순서
    public bool IsRunning => Mode != GuardHandMode.None; // 손이 기본 위치를 떠나 공격을 진행 중인지 여부
    public GuardHandMode Mode { get; private set; } // 현재 진행 중인 손 공격 종류
    public GuardHandPhase Phase { get; private set; } // 현재 손 공격의 진행 단계
    public float PhaseElapsed { get; private set; } // 현재 단계에서 지난 게임 시간
    public int ActiveHand { get; private set; } = -1; // 한 손 공격에 쓰는 손 번호이며 양손 공격은 -1
    public Vector2 ImpactPoint { get; private set; } // 예고와 피해가 공유하는 착지 지점
    public bool IsWaveActive { get; private set; } // 자리별 예고부터 마지막 지면 타격까지 충격파가 진행 중인지 여부

    private readonly Vector3[] homeLocal = new Vector3[2]; // 두 손의 기본 로컬 위치
    private readonly Vector2[] ground = new Vector2[2]; // 각 손의 지면 투영 위치
    private readonly Vector2[] moveFrom = new Vector2[2]; // 이동 구간 시작 때의 지면 위치
    private readonly float[] heights = new float[2]; // 각 손이 지면에서 떠 있는 높이
    private readonly float[] heightFrom = new float[2]; // 이동 구간 시작 때의 높이
    private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>(); // 같은 착지에서 여러 Collider의 피해 중복 방지
    private readonly HashSet<PlayerCharacter> waveHitPlayers = new HashSet<PlayerCharacter>(); // 같은 충격파의 피해 중복 방지
    private Collider2D[] overlaps = new Collider2D[8]; // 공간 질의에 재사용할 결과 버퍼
    private ContactFilter2D impactFilter; // Trigger를 포함한 피격 수집 조건
    private BossCharacter owner; // 피해 출처로 쓸 자기 보스
    private PlayerCharacter target; // 이번 공격에서 추적할 Player
    private AreaWarningVfx ellipseWarning; // 주먹 아래 타원 예고
    private AreaWarningVfx sectorWarning; // 패턴 7의 부채꼴 예고
    private DepthSorter chargeBackSorter; // 충전 전류를 이번에 쓰는 손의 바로 뒤에 그릴 공통 정렬
    private DepthSorter chargeFrontSorter; // 충전 섬광을 이번에 쓰는 손의 바로 앞에 그릴 공통 정렬
    private Action<Vector2> impactCallback; // 패턴 4 착지 순간 탄환 발사를 알릴 콜백
    private Vector2 waveOrigin; // 충격파가 시작되는 착지 지점
    private Vector2 waveDirection; // 충격파가 향하는 방향이며 착지점이 정해지는 순간 Player 쪽으로 다시 잡음
    private float waveRadius; // 이번 충격파가 닿을 거리
    private Rect waveArea; // 지면 타격 자리를 허용할 전투 구역이며 폭이 0이면 제한 없음
    private readonly List<WaveSpot> waveSpots = new List<WaveSpot>(); // 이번 충격파가 차례로 터뜨릴 지면 타격 자리
    private float waveClock; // 착지 순간을 0으로 둔 충격파 시각이며 착지 전 예고 구간에서는 음수
    private float waveBlastAt; // 마지막으로 지면 타격음을 낸 충격파 시각
    private float approachDuration; // 이번 공격의 이동 구간 길이
    private float chargeDuration; // 이번 공격의 충전 구간 길이
    private float hoverHeight; // 이번 공격의 공중 높이
    private Vector2 impactSize; // 이번 공격의 타원 범위
    private float impactDamage; // 이번 공격의 착지 피해
    private float landDuration; // 이번 공격의 착지 유지 시간
    private float returnDuration; // 이번 공격의 복귀 시간
    private int attackId; // 이번 착지 피해를 하나의 공격으로 묶을 번호
    private int waveAttackId; // 이번 충격파 피해를 하나의 공격으로 묶을 번호
    private int nextIdleHand; // 패턴 3에서 다음에 쓸 손 번호
    private bool combined; // 두 손 대신 합친 주먹을 표시하는 상태
    private bool prepared; // 자기 참조와 기본 위치를 준비했는지 여부

    /// <summary>충격파가 한 번 터뜨릴 지면 타격 자리와 진행 상태</summary>
    private struct WaveSpot
    {
        public Vector2 Position; // 폭발이 일어날 지면 위치
        public float ExplodeAt; // 착지 뒤 폭발까지의 게임 시간
        public bool Announced; // 예고 효과를 이미 띄웠는지 여부
        public bool Exploded; // 지면 타격과 피해 판정을 이미 처리했는지 여부
        public bool FlipX; // 예고 그림을 좌우로 뒤집을지 여부
        public bool FlipY; // 예고 그림을 상하로 뒤집을지 여부
        public SpriteClipPlayer Telegraph; // 취소할 때 함께 지울 예고 효과
    }

    /// <summary>자기 참조와 손의 기본 위치를 준비하고 기본 표시로 시작</summary>
    private void Awake()
    {
        Prepare();
        Cancel();
    }

    /// <summary>손 이동이 끝난 뒤 그림자를 지면 투영 위치에 배치</summary>
    private void LateUpdate()
    {
        if (!prepared) return;
        for (int i = 0; i < HandShadows.Length && i < 2; i++)
        {
            if (HandShadows[i] == null) continue;

            // 합친 주먹은 왼쪽 그림자 하나를 착지점 아래에 두고 오른쪽 그림자는 숨김
            bool visible = !combined || i == 0;
            HandShadows[i].enabled = visible;
            if (!visible) continue;
            Vector2 point = combined ? ImpactPoint : IsRunning ? ground[i] : (Vector2)Hands[i].position;
            HandShadows[i].transform.position = new Vector3(point.x, point.y, transform.position.z);
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        // 보스 밖에 만들어 둔 예고 객체 정리
        if (ellipseWarning != null) Destroy(ellipseWarning.gameObject);
        if (sectorWarning != null) Destroy(sectorWarning.gameObject);
    }

    /// <summary>패턴 3: 번갈아 쓰는 한 손으로 표적 추적을 시작</summary>
    public bool BeginIdleSlam(PlayerCharacter player)
    {
        if (!CanBegin(player)) return false;
        Configure(GuardHandMode.IdleSlam, IdleTrackDuration, 0f, IdleHoverHeight, IdleImpactSize, IdleDamage, IdleLandDuration, IdleReturnDuration);
        target = player;
        ActiveHand = nextIdleHand;
        nextIdleHand = 1 - nextIdleHand;
        ImpactPoint = player.transform.position;

        // 추적 동안 표적 위치에 타원 예고A 표시
        ellipseWarning.ShowEllipse(ImpactPoint, impactSize);
        ellipseWarning.BeginNotice();
        return true;
    }

    /// <summary>패턴 4: 두 손을 착지점 위로 모으기 시작하고 착지 순간 후속 탄환 발사를 통지</summary>
    public bool BeginCentral(PlayerCharacter player, Vector2 landingPoint, Action<Vector2> onImpact)
    {
        if (!CanBegin(player)) return false;
        Configure(GuardHandMode.CentralSlam, CentralApproachDuration, CentralChargeDuration, CentralHoverHeight, CentralImpactSize, CentralDamage,
            CentralLandDuration, CentralReturnDuration);
        target = player;
        ActiveHand = -1;
        ImpactPoint = landingPoint;
        impactCallback = onImpact;
        return true;
    }

    /// <summary>패턴 7: 지정한 손을 가장자리 착지점 위로 보내고 Player를 겨냥하지 못할 때 쓸 방향·지면 타격이 허용되는 구역·닿을 거리를 고정</summary>
    public bool BeginEdgeWave(PlayerCharacter player, int hand, Vector2 landingPoint, Vector2 direction, Rect area, float radius)
    {
        if (!CanBegin(player) || hand < 0 || hand > 1 || direction.sqrMagnitude <= 0f) return false;
        Configure(GuardHandMode.EdgeWave, EdgeApproachDuration, EdgeChargeDuration, EdgeHoverHeight, EdgeImpactSize, EdgeDamage,
            EdgeLandDuration, EdgeReturnDuration);
        target = player;
        ActiveHand = hand;
        ImpactPoint = landingPoint;
        waveDirection = direction.normalized;
        waveArea = area;
        waveRadius = radius > 0f ? radius : WaveRadius;
        return true;
    }

    /// <summary>보스가 매 프레임 한 번 전달하는 게임 시간으로 손 공격과 충격파를 진행</summary>
    public void Tick(float gameDelta)
    {
        // 사망·그로기·전투 종료에서는 손과 예고를 즉시 정리
        if (!prepared) return;
        if (!OwnerReady())
        {
            if (IsRunning || IsWaveActive) Cancel();
            return;
        }
        if (Time.timeScale <= 0f || gameDelta <= 0f) return;
        if (IsWaveActive) TickWave(gameDelta);
        if (!IsRunning) return;
        if (target == null || target.IsDead || !target.isActiveAndEnabled)
        {
            Cancel();
            return;
        }
        PhaseElapsed += gameDelta;

        // 한 프레임에 여러 단계를 지나도 남은 시간을 다음 단계로 넘김
        for (int guard = 0; guard < 8 && IsRunning; guard++)
        {
            float duration = GetPhaseDuration();
            UpdatePhase(Mathf.Min(PhaseElapsed, duration), duration);
            if (PhaseElapsed < duration) break;
            PhaseElapsed -= duration;
            AdvancePhase();
        }
        ApplyHandTransforms();
    }

    /// <summary>진행 중인 공격·충전 소리·예고를 닫고 두 손을 기본 위치로 복원</summary>
    public void Cancel()
    {
        Mode = GuardHandMode.None;
        Phase = GuardHandPhase.Idle;
        PhaseElapsed = 0f;
        ActiveHand = -1;
        IsWaveActive = false;
        target = null;
        impactCallback = null;
        combined = false;
        if (!prepared) return;

        // 이미 발사된 탄환은 두고 자기 표시만 복원
        StopCharge();
        ClearWaveSpots();
        if (ellipseWarning != null) ellipseWarning.Hide();
        if (sectorWarning != null) sectorWarning.Hide();
        for (int i = 0; i < 2; i++)
        {
            Hands[i].localPosition = homeLocal[i];
            ground[i] = Hands[i].position;
            heights[i] = 0f;
            HandRenderers[i].sprite = HandSprites[i];
            HandRenderers[i].enabled = true;
        }
        CombinedRenderer.enabled = false;
    }

    /// <summary>자기 보스·손·효과 참조를 확인하고 예고 두 개를 미리 생성</summary>
    private void Prepare()
    {
        if (prepared) return;
        owner = GetComponent<BossCharacter>();
        if (owner == null || Hands == null || Hands.Length != 2 || HandRenderers == null || HandRenderers.Length != 2
            || HandSprites == null || HandSprites.Length != 2 || HandSmearClips == null || HandSmearClips.Length != 2
            || CombinedRenderer == null || WarningPrefab == null || ChargeBack == null || ChargeFront == null)
            throw new InvalidOperationException("Guard hand attack requires its Boss, two hands, the combined fist, charge players and a warning prefab.");
        for (int i = 0; i < 2; i++)
        {
            if (Hands[i] == null || HandRenderers[i] == null) throw new InvalidOperationException("Guard hand attack has an unassigned hand.");
            homeLocal[i] = Hands[i].localPosition;
        }

        chargeBackSorter = ChargeBack.GetComponent<DepthSorter>();
        chargeFrontSorter = ChargeFront.GetComponent<DepthSorter>();

        // 예고는 보스의 색 변화와 이동에 묶이지 않도록 보스와 같은 부모 아래에 생성
        ellipseWarning = Instantiate(WarningPrefab, transform.parent);
        sectorWarning = Instantiate(WarningPrefab, transform.parent);
        impactFilter = new ContactFilter2D { useTriggers = true };
        impactFilter.SetLayerMask(CollisionMask);
        // 그림자는 최종 픽셀 격자에서 만든 타원을 어둡게 입혀 표시
        Sprite handShadow = PixelShapeTexture.GetSolidEllipse(HandShadowPixels.x, HandShadowPixels.y);
        for (int i = 0; i < HandShadows.Length; i++)
            if (HandShadows[i] != null)
            {
                HandShadows[i].sprite = handShadow;
                HandShadows[i].color = new Color(0f, 0f, 0f, ShadowOpacity);
            }
        if (BodyShadow != null)
        {
            BodyShadow.sprite = PixelShapeTexture.GetSolidEllipse(BodyShadowPixels.x, BodyShadowPixels.y);
            BodyShadow.color = new Color(0f, 0f, 0f, ShadowOpacity);
        }
        prepared = true;
    }

    /// <summary>새 공격을 시작할 수 있는 전투 상태와 대상인지 확인</summary>
    private bool CanBegin(PlayerCharacter player)
    {
        Prepare();
        return !IsRunning && OwnerReady() && player != null && !player.IsDead && player.isActiveAndEnabled;
    }

    /// <summary>공격 종류별 시간·높이·범위·피해를 이번 실행에 고정하고 이동 구간 시작</summary>
    private void Configure(GuardHandMode mode, float approach, float charge, float hover, Vector2 size, float damage, float land, float back)
    {
        Mode = mode;
        approachDuration = Mathf.Max(0.01f, approach);
        chargeDuration = Mathf.Max(0f, charge);
        hoverHeight = Mathf.Max(0f, hover);
        impactSize = size;
        impactDamage = damage;
        landDuration = Mathf.Max(0f, land);
        returnDuration = Mathf.Max(0.01f, back);
        impactCallback = null;
        combined = false;
        attackId = DamageInstance.NextAttackId();
        hitPlayers.Clear();

        // 보스가 배치된 뒤의 현재 손 위치에서 출발
        for (int i = 0; i < 2; i++)
        {
            ground[i] = Hands[i].position;
            heights[i] = 0f;
        }
        EnterPhase(GuardHandPhase.Approach);
        PhaseElapsed = 0f;
    }

    /// <summary>현재 단계의 길이 조회</summary>
    private float GetPhaseDuration()
    {
        switch (Phase)
        {
            case GuardHandPhase.Approach: return approachDuration;
            case GuardHandPhase.Charge: return chargeDuration;
            case GuardHandPhase.Lock: return LockDuration;
            case GuardHandPhase.Drop: return DropDuration;
            case GuardHandPhase.Land: return landDuration;
            case GuardHandPhase.Return: return returnDuration;
            default: return 0f;
        }
    }

    /// <summary>단계 안에서 지난 시간에 맞춰 손의 지면 위치·높이·그림을 갱신</summary>
    private void UpdatePhase(float elapsed, float duration)
    {
        float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        switch (Phase)
        {
            case GuardHandPhase.Approach:
                if (Mode == GuardHandMode.IdleSlam)
                {
                    // 예고A는 표적을 그대로 따라가고 손은 정해진 속도로 그 위를 쫓음
                    ImpactPoint = target.transform.position;
                    ellipseWarning.ShowEllipse(ImpactPoint, impactSize);
                    ChaseImpactPoint();
                }
                else
                {
                    // 지정 위치까지 부드럽게 이동하며 공중 높이까지 상승
                    float eased = Mathf.SmoothStep(0f, 1f, progress);
                    for (int i = 0; i < 2; i++)
                        if (UsesHand(i))
                        {
                            ground[i] = Vector2.Lerp(moveFrom[i], GetHoverGround(i), eased);
                            heights[i] = Mathf.Lerp(heightFrom[i], hoverHeight, eased);
                        }
                }
                break;
            case GuardHandPhase.Charge:
                UpdateChargePosition();
                break;
            case GuardHandPhase.Lock:
                // 충전 공격은 충전 효과를 손에 붙인 채 예고B 구간을 지남
                if (Mode == GuardHandMode.IdleSlam) ChaseImpactPoint();
                else UpdateChargePosition();
                break;
            case GuardHandPhase.Drop:
                // 스미어 두 장을 30ms씩 보여주며 고정된 착지점으로 하강
                for (int i = 0; i < 2; i++)
                    if (UsesHand(i))
                    {
                        ground[i] = Vector2.Lerp(moveFrom[i], GetHoverGround(i), progress);
                        heights[i] = Mathf.Lerp(heightFrom[i], 0f, progress);
                    }
                ShowSmear(elapsed);
                if (Mode != GuardHandMode.IdleSlam) UpdateChargePosition();
                break;
            case GuardHandPhase.Return:
                // 바닥을 끌지 않도록 낮게 들었다가 기본 위치에 내려놓음
                float back = Mathf.SmoothStep(0f, 1f, progress);
                for (int i = 0; i < 2; i++)
                    if (UsesHand(i))
                    {
                        ground[i] = Vector2.Lerp(moveFrom[i], GetHomeGround(i), back);
                        heights[i] = Mathf.Sin(progress * Mathf.PI) * ReturnHopHeight;
                    }
                break;
        }
    }

    /// <summary>현재 단계를 마치고 다음 단계로 넘어가거나 공격을 종료</summary>
    private void AdvancePhase()
    {
        switch (Phase)
        {
            case GuardHandPhase.Approach:
                EnterPhase(chargeDuration > 0f ? GuardHandPhase.Charge : GuardHandPhase.Lock);
                break;
            case GuardHandPhase.Charge:
                EnterPhase(GuardHandPhase.Lock);
                break;
            case GuardHandPhase.Lock:
                EnterPhase(GuardHandPhase.Drop);
                break;
            case GuardHandPhase.Drop:
                EnterPhase(GuardHandPhase.Land);
                break;
            case GuardHandPhase.Land:
                EnterPhase(GuardHandPhase.Return);
                break;
            default:
                Finish();
                break;
        }
    }

    /// <summary>새 단계에 들어서는 순간의 표시 전환·예고·소리·피해 처리</summary>
    private void EnterPhase(GuardHandPhase phase)
    {
        Phase = phase;
        for (int i = 0; i < 2; i++)
        {
            moveFrom[i] = ground[i];
            heightFrom[i] = heights[i];
        }
        switch (phase)
        {
            case GuardHandPhase.Charge:
                // 패턴 4는 두 손이 맞닿은 순간부터 합친 주먹으로 표시
                if (Mode == GuardHandMode.CentralSlam) SetCombined(true);
                ChargeBack.gameObject.SetActive(true);
                ChargeFront.gameObject.SetActive(true);
                ChargeBack.Play(ChargeBackClip);
                ChargeFront.Play(ChargeFrontClip);
                BindChargeDepth();
                UpdateChargePosition();
                if (Audio != null && ChargeClip != null)
                {
                    Audio.clip = ChargeClip;
                    Audio.loop = true;
                    Audio.volume = Volume * GameSettings.SfxVolume;
                    Audio.Play();
                }
                break;
            case GuardHandPhase.Lock:
                // 충전 효과와 소리는 착지까지 그대로 둔 채 착지점을 고정하고 타원의 예고B 시작
                if (Mode == GuardHandMode.IdleSlam) ImpactPoint = target.transform.position;
                ellipseWarning.ShowEllipse(ImpactPoint, impactSize);
                ellipseWarning.BeginImminent(LockDuration);
                if (Mode == GuardHandMode.EdgeWave)
                {
                    // 폭발 자리를 모두 정해 자리별 예고가 착지 전부터 뜨게 하고 예고 효과가 없을 때만 부채꼴로 대신 표시
                    BeginWave();
                    if (WaveTelegraphClip == null)
                    {
                        sectorWarning.ShowSector(ImpactPoint, waveDirection, WaveAngle, waveRadius);
                        sectorWarning.BeginImminent(LockDuration);
                    }
                }
                break;
            case GuardHandPhase.Land:
                Impact();
                break;
            case GuardHandPhase.Return:
                // 합친 주먹은 다시 두 손으로 나눈 뒤 각자 기본 위치로 복귀
                if (combined) SetCombined(false);
                for (int i = 0; i < 2; i++) moveFrom[i] = ground[i];
                break;
        }
    }

    /// <summary>손이 지면에 닿은 순간의 그림 복원·효과·소리·피해와 후속 공격 시작</summary>
    private void Impact()
    {
        // 예고B와 낙하까지 이어진 충전 효과와 소리를 착지 순간에 끝내고 주먹을 착지점 바닥 그림으로 전환
        StopCharge();
        for (int i = 0; i < 2; i++)
            if (UsesHand(i))
            {
                ground[i] = GetHoverGround(i);
                heights[i] = 0f;
                HandRenderers[i].sprite = HandSprites[i];
            }
        if (combined) CombinedRenderer.sprite = CombinedSprite;
        ApplyHandTransforms();
        Transform parent = transform.parent;
        Vector3 point = ImpactPoint;

        // 공격 종류별 착지 효과와 소리 재생
        if (Mode == GuardHandMode.IdleSlam)
        {
            SpriteClipPlayer.Spawn(StompRingClip, point, GroundFxOrder, SpriteMaterial, parent);
            Play(SlamClip);
        }
        else
        {
            SpriteClipPlayer.Spawn(CrackClip, point, GroundFxOrder, SpriteMaterial, parent);
            SpriteClipPlayer.Spawn(BurstClip, point, FrontFxOrder, SpriteMaterial, parent);
            if (Mode == GuardHandMode.EdgeWave) SpriteClipPlayer.Spawn(DustClip, point, FrontFxOrder + 1, SpriteMaterial, parent);
            ImpactDistortionFx.Play(point);
            Play(ChargedSlamClip);
        }

        // 타원 예고는 타격 순간 진동으로 바꾼 뒤 곧바로 사라지게 처리
        ellipseWarning.BeginDanger();
        ellipseWarning.EndDanger();
        if (Mode == GuardHandMode.EdgeWave)
        {
            // 예고 구간에서 쌓인 시각 오차를 없애고 착지 순간을 충격파의 0초로 맞춤
            waveClock = 0f;
            if (WaveTelegraphClip == null) sectorWarning.BeginDanger();
        }

        // 후속 탄환을 먼저 만든 뒤 피해를 적용해 Player 사망 정리가 새 탄환도 함께 닫도록 처리
        GuardHandMode mode = Mode;
        Action<Vector2> callback = impactCallback;
        impactCallback = null;
        callback?.Invoke(ImpactPoint);
        if (Mode != mode || !OwnerReady()) return;
        DamageEllipse(ImpactPoint, impactSize, impactDamage, attackId, hitPlayers);
    }

    /// <summary>복귀까지 끝난 공격을 닫고 손을 기본 위치에 고정</summary>
    private void Finish()
    {
        for (int i = 0; i < 2; i++)
        {
            Hands[i].localPosition = homeLocal[i];
            ground[i] = Hands[i].position;
            heights[i] = 0f;
        }
        Mode = GuardHandMode.None;
        Phase = GuardHandPhase.Idle;
        PhaseElapsed = 0f;
        ActiveHand = -1;
        target = null;
    }

    /// <summary>패턴 3에서 손의 지면 위치를 착지점 쪽으로 옮기며 공중 높이까지 상승</summary>
    private void ChaseImpactPoint()
    {
        int hand = ActiveHand;
        float delta = Time.deltaTime;
        ground[hand] = Vector2.MoveTowards(ground[hand], ImpactPoint, IdleTrackingSpeed * delta);
        heights[hand] = Mathf.MoveTowards(heights[hand], hoverHeight, hoverHeight / Mathf.Max(0.01f, RiseDuration) * delta);
    }

    /// <summary>이번 공격에 참여하는 손인지 확인</summary>
    private bool UsesHand(int index)
    {
        return ActiveHand < 0 || ActiveHand == index;
    }

    /// <summary>공중에서 머물 때 각 손의 지면 투영 위치</summary>
    private Vector2 GetHoverGround(int index)
    {
        if (ActiveHand >= 0) return ImpactPoint;
        return ImpactPoint + Vector2.right * ((index == 0 ? -0.5f : 0.5f) * CentralHandGap);
    }

    /// <summary>기본 위치에 놓인 손의 월드 지면 위치</summary>
    private Vector2 GetHomeGround(int index)
    {
        return Hands[index].parent != null ? (Vector2)Hands[index].parent.TransformPoint(homeLocal[index]) : (Vector2)homeLocal[index];
    }

    /// <summary>지면 위치에 화면상의 높이만 더해 손과 합친 주먹을 배치</summary>
    private void ApplyHandTransforms()
    {
        float z = transform.position.z;
        for (int i = 0; i < 2; i++)
        {
            if (!UsesHand(i)) continue;
            Hands[i].position = new Vector3(ground[i].x, ground[i].y + heights[i], z);
        }
        if (combined) CombinedRenderer.transform.position = new Vector3(ImpactPoint.x, ImpactPoint.y + heights[0], z);
    }

    /// <summary>두 손의 그림과 합친 주먹의 표시를 서로 교체</summary>
    private void SetCombined(bool value)
    {
        combined = value;
        CombinedRenderer.sprite = CombinedSprite;
        CombinedRenderer.enabled = value;
        HandRenderers[0].enabled = HandRenderers[1].enabled = !value;
        if (value) return;

        // 갈라지는 순간 두 손을 착지점 양옆 바닥에 놓음
        for (int i = 0; i < 2; i++)
        {
            ground[i] = ImpactPoint + Vector2.right * ((i == 0 ? -0.5f : 0.5f) * CentralHandGap);
            HandRenderers[i].sprite = HandSprites[i];
        }
    }

    /// <summary>하강 구간에서 지난 시간에 맞는 스미어 프레임 표시</summary>
    private void ShowSmear(float elapsed)
    {
        if (combined)
        {
            Sprite frame = GetClipFrame(CombinedSmearClip, elapsed);
            if (frame != null) CombinedRenderer.sprite = frame;
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            if (!UsesHand(i)) continue;
            Sprite frame = GetClipFrame(HandSmearClips[i], elapsed);
            if (frame != null) HandRenderers[i].sprite = frame;
        }
    }

    /// <summary>클립에서 지난 시간의 프레임을 읽고 끝났으면 마지막 프레임 유지</summary>
    private static Sprite GetClipFrame(SpriteClip clip, float elapsed)
    {
        if (clip == null || clip.FrameCount == 0) return null;
        int index = clip.GetFrameIndex(elapsed);
        return clip.Frames[index < 0 ? clip.FrameCount - 1 : index];
    }

    /// <summary>충전 효과를 손(또는 두 손의 접점) 중심에 배치</summary>
    private void UpdateChargePosition()
    {
        int hand = ActiveHand < 0 ? 0 : ActiveHand;
        Vector2 basePoint = ActiveHand < 0 ? ImpactPoint : ground[hand];
        var center = new Vector3(basePoint.x, basePoint.y + heights[hand] + ChargeHandCenter, transform.position.z);
        ChargeBack.transform.position = center;
        ChargeFront.transform.position = center;
    }

    /// <summary>충전 효과가 이번에 쓰는 손(양손이면 합친 주먹)의 그림자 지점으로 앞뒤를 가리도록 기준 교체</summary>
    private void BindChargeDepth()
    {
        SpriteRenderer shadow = HandShadows != null && HandShadows.Length == 2 ? HandShadows[ActiveHand < 0 ? 0 : ActiveHand] : null;
        if (shadow == null) return;
        if (chargeBackSorter != null) chargeBackSorter.GroundPoint = shadow.transform;
        if (chargeFrontSorter != null) chargeFrontSorter.GroundPoint = shadow.transform;
    }

    /// <summary>충전 효과와 반복 중인 충전 소리 정지</summary>
    private void StopCharge()
    {
        if (ChargeBack != null) ChargeBack.gameObject.SetActive(false);
        if (ChargeFront != null) ChargeFront.gameObject.SetActive(false);
        if (Audio != null && Audio.isPlaying && Audio.clip == ChargeClip) Audio.Stop();
    }

    /// <summary>손 공격 소리를 한 번 재생</summary>
    private void Play(AudioClip clip)
    {
        if (Audio != null && clip != null) Audio.PlayOneShot(clip, Volume * GameSettings.SfxVolume);
    }

    /// <summary>착지점이 정해진 순간 부채꼴의 중심을 Player 쪽으로 잡고 그 안의 폭발 자리와 자리별 폭발 시각을 모두 결정</summary>
    private void BeginWave()
    {
        IsWaveActive = true;
        waveOrigin = ImpactPoint;
        waveClock = -(LockDuration + DropDuration);
        waveBlastAt = float.NegativeInfinity;
        waveAttackId = DamageInstance.NextAttackId();
        waveHitPlayers.Clear();
        ClearWaveSpots();

        // 부채꼴의 중심은 이 순간 Player가 선 자리를 향하게 해 그 자리를 벗어나야 피할 수 있게 하고, Player가 허용 구역 밖이면 구역 안의 가장 가까운 점을 겨냥
        if (target != null)
        {
            Vector2 aim = target.transform.position;
            if (waveArea.width > 0f) aim = new Vector2(Mathf.Clamp(aim.x, waveArea.xMin, waveArea.xMax), Mathf.Clamp(aim.y, waveArea.yMin, waveArea.yMax));
            Vector2 toward = aim - waveOrigin;
            if (toward.sqrMagnitude > 0.01f) waveDirection = toward.normalized;
        }

        // 가까운 곳부터 원호를 따라 자리를 잡되 줄 맞춘 격자로 보이지 않게 원호마다 통째로 조금 돌리고 자리마다 따로 어긋나게 놓음
        float spacing = Mathf.Max(0.1f, WaveRingSpacing);
        float jitter = Mathf.Max(0f, WaveSpotJitter);
        for (float radius = Mathf.Max(impactSize.x * 0.5f, spacing); radius <= waveRadius; radius += spacing)
        {
            float arc = radius * WaveAngle * Mathf.Deg2Rad;
            int count = Mathf.Max(1, Mathf.RoundToInt(arc / Mathf.Max(0.1f, WaveArcSpacing)));
            float step = WaveAngle / count;
            float twist = UnityEngine.Random.Range(-1f, 1f) * WaveRingTwist * step;
            for (int i = 0; i < count; i++)
            {
                // 거리와 각도를 따로 흔들고 터지는 시각은 흔든 뒤의 거리를 따라가 물결의 앞선도 고르지 않게 함
                float distance = Mathf.Max(spacing * 0.5f, radius + UnityEngine.Random.Range(-jitter, jitter));
                float angle = -WaveAngle * 0.5f + step * (i + 0.5f) + twist + UnityEngine.Random.Range(-jitter, jitter) / distance * Mathf.Rad2Deg;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * waveDirection;
                Vector2 position = waveOrigin + direction * distance;

                // 전투 구역 밖에 놓이는 자리는 예고와 폭발을 만들지 않음
                if (waveArea.width > 0f && !waveArea.Contains(position)) continue;
                waveSpots.Add(new WaveSpot
                {
                    Position = position,
                    ExplodeAt = distance / waveRadius * WaveDuration,
                    FlipX = UnityEngine.Random.value < 0.5f,
                    FlipY = UnityEngine.Random.value < 0.5f
                });
            }
        }
    }

    /// <summary>자리마다 폭발보다 먼저 예고 효과를 띄우고 폭발 시각에 지면 타격과 피해를 한 번 처리</summary>
    private void TickWave(float gameDelta)
    {
        waveClock += gameDelta;
        bool finished = true;
        for (int i = 0; i < waveSpots.Count; i++)
        {
            WaveSpot spot = waveSpots[i];
            if (spot.Exploded) continue;

            // 폭발보다 정해진 시간만큼 먼저 그 자리에 예고 효과 표시
            if (!spot.Announced && WaveTelegraphClip != null && waveClock >= spot.ExplodeAt - WaveTelegraphDuration)
            {
                spot.Announced = true;
                spot.Telegraph = SpriteClipPlayer.Spawn(WaveTelegraphClip, spot.Position, GroundFxOrder, SpriteMaterial, transform.parent);
                if (spot.Telegraph != null)
                {
                    spot.Telegraph.PlayFor(WaveTelegraphClip, WaveTelegraphDuration);

                    // 같은 그림이 줄지어 보이지 않도록 자리마다 정해 둔 방향으로 뒤집어 표시
                    var telegraphRenderer = spot.Telegraph.GetComponent<SpriteRenderer>();
                    telegraphRenderer.flipX = spot.FlipX;
                    telegraphRenderer.flipY = spot.FlipY;
                }
            }
            if (waveClock < spot.ExplodeAt)
            {
                finished = false;
                waveSpots[i] = spot;
                continue;
            }

            // 예고를 걷고 같은 자리에 지면 타격을 재생하며 그 순간에만 피해 판정
            spot.Exploded = true;
            if (spot.Telegraph != null) Destroy(spot.Telegraph.gameObject);
            spot.Telegraph = null;
            waveSpots[i] = spot;
            SpriteClipPlayer.Spawn(GroundHitClip, spot.Position, WaveFxOrder, SpriteMaterial, transform.parent);

            // 촘촘히 이어지는 폭발은 정해진 간격으로만 소리를 내 한꺼번에 겹쳐 울리지 않게 함
            if (WaveBlastClip != null && Audio != null && waveClock - waveBlastAt >= WaveBlastInterval)
            {
                waveBlastAt = waveClock;
                Audio.PlayOneShot(WaveBlastClip, Volume * WaveBlastVolume * GameSettings.SfxVolume);
            }
            DamageSpot(spot.Position);
            if (!IsWaveActive) return;
        }
        if (!finished) return;

        // 마지막 자리까지 터지면 충격파 종료
        IsWaveActive = false;
        waveSpots.Clear();
        if (WaveTelegraphClip == null) sectorWarning.EndDanger();
    }

    /// <summary>폭발한 자리의 원 안에 있는 Player 피격 영역에 충격파 한 번당 한 번 피해 전달</summary>
    private void DamageSpot(Vector2 center)
    {
        int count;
        do
        {
            count = Physics2D.OverlapCircle(center, WaveHitRadius, impactFilter, overlaps);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, checked(overlaps.Length * 2));
        } while (true);
        for (int i = 0; i < count && IsWaveActive; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                || waveHitPlayers.Contains(player)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(owner, WaveDamage, DamageType.Normal, waveAttackId))) waveHitPlayers.Add(player);
        }
    }

    /// <summary>아직 터지지 않은 자리의 예고 효과를 지우고 자리 목록을 비움</summary>
    private void ClearWaveSpots()
    {
        for (int i = 0; i < waveSpots.Count; i++)
            if (waveSpots[i].Telegraph != null) Destroy(waveSpots[i].Telegraph.gameObject);
        waveSpots.Clear();
    }

    /// <summary>타원 안에 걸친 Player 피격 영역에 한 번 피해 전달</summary>
    private void DamageEllipse(Vector2 center, Vector2 size, float damage, int id, HashSet<PlayerCharacter> hits)
    {
        int count = Collect(center, size);
        float radiusX = Mathf.Max(0.01f, size.x * 0.5f), radiusY = Mathf.Max(0.01f, size.y * 0.5f);
        GuardHandMode mode = Mode;
        for (int i = 0; i < count && Mode == mode; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                || hits.Contains(player)) continue;

            // 피격 영역에서 타원 중심에 가장 가까운 점이 타원 안에 있을 때만 적중
            Vector2 closest = other.ClosestPoint(center) - center;
            float x = closest.x / radiusX, y = closest.y / radiusY;
            if (x * x + y * y > 1f) continue;
            if (hitbox.ReceiveHit(new DamageInstance(owner, damage, DamageType.Normal, id))) hits.Add(player);
        }
    }

    /// <summary>사각 범위에 걸친 Collider를 재사용 버퍼에 수집</summary>
    private int Collect(Vector2 center, Vector2 size)
    {
        int count;
        do
        {
            count = Physics2D.OverlapBox(center, size, 0f, impactFilter, overlaps);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, checked(overlaps.Length * 2));
        } while (true);
        return count;
    }

    /// <summary>현재 같은 보스의 전투 수명에서 손 공격을 진행할 수 있는지 확인</summary>
    private bool OwnerReady()
    {
        return isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && owner.IsInitialized
            && !owner.IsDead && owner.IsInCombat && !owner.IsGroggy && !owner.IsCutScene;
    }
}
