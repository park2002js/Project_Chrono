using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>경비 손의 추격 충전·원형 경고·낙하·복귀 진행 단계</summary>
public enum GuardHandPhase
{
    Idle, // 손의 기본 위치에서 다음 실행 대기
    Tracking, // 상승과 지면 추격 중 경고를 함께 이동시키며 충전
    Holding, // 가득 찬 경고를 유지하는 낙하 직전 구간
    Falling, // 고정된 지면으로 빠르게 낙하
    Recovery, // 착지 뒤 손을 잠시 유지하는 구간
    Returning // 사용한 손을 원래 위치로 복귀
}

/// <summary>한 손 추격과 양손 중앙 공격의 이동 경고·충전·착지 단일 피해를 패턴 시간으로 진행</summary>
public class GuardHandAttack : MonoBehaviour
{
    public Transform[] Hands = Array.Empty<Transform>(); // 왼손·오른손 순서로 연결할 표시 Transform 두 개
    public AreaWarningVfx Warning; // 한 번 준비해 재사용할 비활성 원형 지면 경고
    public float Height = 2.5f; // 지면 목표와 분리해 표시할 손의 공중 높이
    public float RiseDuration = 0.35f; // 지면에서 목표 높이까지 상승할 게임 시간
    public float TrackingSpeed = 10f; // 상승 중 지면 목표를 추격할 초당 월드 거리
    public float ChargeDuration = 2.2f; // 한 손 추격과 이동 경고의 중앙 채움을 함께 진행할 게임 시간
    public float CentralChargeDuration = 1.2f; // 양손을 중앙으로 모으며 큰 원형 경고를 채울 게임 시간
    public float CentralRadius = 3.5f; // 양손 중앙 공격의 경고와 실제 피해가 공유할 월드 반경
    public float CentralDamage = 15f; // 양손 중앙 공격의 착지 피해
    public float CentralHandOffset = 1.1f; // 중앙 공격에서 양손을 벌려 놓을 좌우 월드 간격
    public float FallDuration = 0.12f; // 고정된 목표 높이에서 지면까지 낙하할 게임 시간
    public float ImpactRadius = 1.35f; // 한 손 공격의 경고와 실제 착지 피해가 공유할 월드 반경
    public float ImpactDamage = 10f; // 한 손 착지 순간 범위 안의 Player에게 전달할 피해
    public float RecoveryDuration = 0.35f; // 착지 뒤 지면에서 유지할 게임 시간
    public float ReturnDuration = 0.35f; // 착지 위치에서 기본 손 위치까지 돌아올 게임 시간
    public LayerMask CollisionMask = ~0; // 착지 순간 Player 피격 영역을 수집할 레이어
    public bool IsRunning { get; private set; } // 패턴이 다음 Tick 또는 취소를 전달할 실행 상태
    public bool IsCentral { get; private set; } // 이번 실행이 양손을 함께 사용하는 중앙 공격인지 조회
    public GuardHandPhase Phase { get; private set; } // 현재 손 공격의 진행 단계
    public int SelectedHandIndex { get; private set; } = -1; // 한 손 공격의 선택 번호이며 중앙 공격과 유휴에서는 -1
    public float FillProgress { get; private set; } // 손의 충전 시간에 따라 경고 내부가 차오른 0~1 진행률
    public float ChargeElapsed { get; private set; } // 이번 공격에서 실제 진행한 충전 게임 시간
    public Vector2 GroundPosition { get; private set; } // 손의 시각적 높이와 별개인 실제 지면 목표 위치
    public float PhaseElapsed { get; private set; } // 현재 단계에서 경과한 게임 시간

    private const float FilledHoldDuration = 0.2f; // 경고가 가득 찬 뒤 사용자 확정으로 유지할 게임 시간
    private readonly Vector3[] homeLocalPositions = new Vector3[2]; // 같은 손 두 개의 복귀 기준 로컬 좌표
    private readonly Vector2[] handGroundPositions = new Vector2[2]; // 각 손의 시각적 높이와 분리한 지면 투영 위치
    private readonly Vector2[] fallStartGroundPositions = new Vector2[2]; // 중앙 낙하 시작에서 마지막 가로 이동을 연결할 손 위치
    private readonly Vector3[] returnStartLocalPositions = new Vector3[2]; // 각 손의 복귀 시작 로컬 위치
    private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>(); // 같은 착지에서 여러 피격 Collider의 피해 중복 방지
    private Collider2D[] overlaps = new Collider2D[8]; // 착지 공간 질의에 재사용할 결과 버퍼
    private BossCharacter owner; // 자기 생성 범위에서 계속 재사용할 Boss 피해 출처
    private PlayerCharacter target; // 이번 패턴 시작에서 주입받은 같은 전투의 Player
    private Transform warningFill; // 경고 내부의 중앙 채움 크기만 바꿀 표시 Transform
    private Vector3 fullFillScale; // 취소와 다음 실행에서 복원할 경고 내부의 원래 크기
    private ContactFilter2D impactFilter; // 착지 순간에 재사용할 Trigger 포함 피격 수집 조건
    private float height; // 실제 지면 위치 위에 더할 현재 시각적 높이
    private float fallStartHeight; // 충전이 끝나 실제 낙하를 시작한 손의 높이
    private int attackId; // 이번 착지 피해를 하나의 공격으로 구분할 번호
    private int runVersion; // 취소 콜백에서 새 실행이 시작되어도 이전 착지와 구분할 번호
    private float currentRadius; // 실행 시작에서 한 손 또는 중앙 공격의 반경을 고정한 값
    private float currentDamage; // 실행 시작에서 한 손 또는 중앙 공격의 피해를 고정한 값
    private float currentDuration; // 실행 시작에서 한 손 또는 중앙 공격의 충전 시간을 고정한 값
    private Action<Vector2> impactCallback; // 중앙 착지 순간 한 번 통지하고 바로 해제할 후속 공격
    private bool impacted; // 착지 경계에서 피해 질의가 중복 실행되지 않도록 차단
    private bool prepared; // 자기 참조·홈·필터의 일회 준비 완료 상태

    /// <summary>자기 Boss·손·경고와 재사용 질의를 한 번 준비</summary>
    private void Awake()
    {
        Prepare();
        Cancel();
    }

    private void OnDisable()
    {
        // 씬 해제와 비활성화에서도 손과 경고를 기본 상태로 복원
        Cancel();
    }

    /// <summary>살아 있는 전투 상대를 연결하고 무작위 한 손의 추격 시작</summary>
    public bool Begin(PlayerCharacter player)
    {
        // 무작위 한 손과 이동 경고를 같은 충전 실행으로 시작
        return BeginRun(player, false, Vector2.zero, null);
    }

    /// <summary>고정된 중앙의 큰 경고와 양손 공격을 시작하고 착지 순간 후속 공격 통지</summary>
    public bool BeginCentral(Vector2 center, Action<Vector2> onImpact)
    {
        // 외부 Player를 다시 찾지 않고 보스의 같은 전투 대상 재사용
        Prepare();
        if (float.IsNaN(center.x) || float.IsInfinity(center.x) || float.IsNaN(center.y) || float.IsInfinity(center.y)) return false;
        return BeginRun(owner.Target, true, center, onImpact);
    }

    /// <summary>실제 채움 완료 뒤 0.2초를 확보하며 패턴의 게임 시간으로 손 공격 진행</summary>
    public void Tick(float gameDelta)
    {
        // 사망·그로기·전투 종료는 시간 정지와 별개로 즉시 자기 동작 정리
        if (!IsRunning) return;
        if (!OwnerReady() || target == null || target.IsDead || !target.isActiveAndEnabled)
        {
            Cancel();
            return;
        }
        if (Time.timeScale <= 0f || !Positive(gameDelta)) return;
        int version = runVersion;

        // 손의 이동과 상승 중에도 같은 충전 타이머로 지면 경고를 계속 채움
        PhaseElapsed += gameDelta;
        if (Phase == GuardHandPhase.Tracking)
        {
            Vector2 currentTarget = target.transform.position;
            for (int i = 0; i < Hands.Length; i++)
                if (IsCentral || i == SelectedHandIndex)
                {
                    Vector2 goal = IsCentral ? CentralGround(i) : currentTarget;
                    handGroundPositions[i] = Vector2.MoveTowards(handGroundPositions[i], goal, TrackingSpeed * gameDelta);
                }
            if (!IsCentral) GroundPosition = handGroundPositions[SelectedHandIndex];
            height = Mathf.MoveTowards(height, Height, Height / RiseDuration * gameDelta);
            SetHandHeight();
            MoveWarning();
            ChargeElapsed = Mathf.Min(currentDuration, ChargeElapsed + gameDelta);
            FillProgress = Mathf.Clamp01(ChargeElapsed / currentDuration);
            warningFill.localScale = fullFillScale * FillProgress;

            // 도착 여부와 무관하게 실제 가득 찬 순간의 지면을 고정하고 0.2초 유지 시작
            if (ChargeElapsed >= currentDuration)
            {
                PhaseElapsed = 0f;
                Phase = GuardHandPhase.Holding;
            }
            return;
        }

        // 착지 콜백에서 취소나 새 실행이 시작되면 이전 실행의 후속 처리를 중단
        while (IsRunning && runVersion == version)
        {
            switch (Phase)
            {
                case GuardHandPhase.Holding:
                    if (PhaseElapsed < FilledHoldDuration) return;
                    PhaseElapsed -= FilledHoldDuration;
                    for (int i = 0; i < Hands.Length; i++) fallStartGroundPositions[i] = handGroundPositions[i];
                    fallStartHeight = height;
                    Phase = GuardHandPhase.Falling;
                    break;
                case GuardHandPhase.Falling:
                    float fallProgress = Mathf.Clamp01(PhaseElapsed / FallDuration);
                    if (IsCentral)
                        for (int i = 0; i < Hands.Length; i++)
                            handGroundPositions[i] = Vector2.Lerp(fallStartGroundPositions[i], CentralGround(i), fallProgress);
                    height = Mathf.Lerp(fallStartHeight, 0f, fallProgress);
                    SetHandHeight();
                    if (PhaseElapsed < FallDuration) return;
                    PhaseElapsed -= FallDuration;
                    Phase = GuardHandPhase.Recovery;
                    Warning.gameObject.SetActive(false);
                    Impact();
                    break;
                case GuardHandPhase.Recovery:
                    if (PhaseElapsed < RecoveryDuration) return;
                    PhaseElapsed -= RecoveryDuration;
                    for (int i = 0; i < Hands.Length; i++) returnStartLocalPositions[i] = Hands[i].localPosition;
                    Phase = GuardHandPhase.Returning;
                    break;
                case GuardHandPhase.Returning:
                    for (int i = 0; i < Hands.Length; i++)
                        if (IsCentral || i == SelectedHandIndex)
                            Hands[i].localPosition = Vector3.Lerp(returnStartLocalPositions[i], homeLocalPositions[i],
                                Mathf.Clamp01(PhaseElapsed / ReturnDuration));
                    if (PhaseElapsed < ReturnDuration) return;
                    Cancel();
                    break;
                default:
                    Cancel();
                    break;
            }
        }
    }

    /// <summary>미래 착지를 차단하고 경고와 두 손의 기본 위치 복원</summary>
    public void Cancel()
    {
        // 외부 종료가 중첩되어도 현재 실행을 먼저 닫아 후속 피해 차단
        unchecked { runVersion++; }
        IsRunning = false;
        IsCentral = false;
        Phase = GuardHandPhase.Idle;
        PhaseElapsed = 0f;
        SelectedHandIndex = -1;
        FillProgress = 0f;
        ChargeElapsed = 0f;
        height = 0f;
        impacted = true;
        target = null;
        impactCallback = null;
        if (!prepared) return;

        // 이미 발사된 다른 위험물은 건드리지 않고 자기 표시만 복원
        if (Warning != null) Warning.gameObject.SetActive(false);
        if (warningFill != null) warningFill.localScale = fullFillScale;
        for (int i = 0; i < Hands.Length; i++)
            if (Hands[i] != null) Hands[i].localPosition = homeLocalPositions[i];
    }

    /// <summary>한 손과 양손의 실행별 수치를 고정하고 충전과 경고를 동시에 시작</summary>
    private bool BeginRun(PlayerCharacter player, bool central, Vector2 center, Action<Vector2> onImpact)
    {
        // 같은 실행을 중복 시작하지 않고 준비되지 않은 전투 대상과 설정 거부
        Prepare();
        if (IsRunning || !OwnerReady() || player == null || player.IsDead || !player.isActiveAndEnabled
            || !ValidSettings(central)) return false;

        // 같은 손과 경고를 재사용하되 실행 종류에 맞는 수치는 이번 실행에 고정
        unchecked { runVersion++; }
        target = player;
        IsCentral = central;
        SelectedHandIndex = central ? -1 : UnityEngine.Random.Range(0, Hands.Length);
        currentRadius = central ? CentralRadius : ImpactRadius;
        currentDamage = central ? CentralDamage : ImpactDamage;
        currentDuration = central ? CentralChargeDuration : ChargeDuration;
        impactCallback = onImpact;
        for (int i = 0; i < Hands.Length; i++) handGroundPositions[i] = Hands[i].position;
        GroundPosition = central ? center : handGroundPositions[SelectedHandIndex];
        Phase = GuardHandPhase.Tracking;
        PhaseElapsed = 0f;
        ChargeElapsed = 0f;
        FillProgress = 0f;
        height = 0f;
        impacted = false;
        attackId = DamageInstance.NextAttackId();
        hitPlayers.Clear();
        IsRunning = true;

        // 빈 내부와 현재 손의 지면 투영 위치부터 경고를 바로 표시
        warningFill.localScale = Vector3.zero;
        ShowWarning();
        return true;
    }

    /// <summary>자기 생성 범위의 참조·손 위치·착지 필터를 한 번 확보</summary>
    private void Prepare()
    {
        // 다른 씬 객체의 Awake 완료를 요구하지 않고 자기 프리팹 참조만 확인
        if (prepared) return;
        owner = GetComponent<BossCharacter>();
        if (owner == null || Hands == null || Hands.Length != 2 || Hands[0] == null || Hands[1] == null
            || Warning == null || Warning.FillRenderer == null)
            throw new InvalidOperationException("Guard hand attack requires its Boss, two hands and a prepared circular warning.");

        // 같은 두 손과 경고 내부의 크기는 다음 공격과 취소에도 계속 재사용
        for (int i = 0; i < Hands.Length; i++) homeLocalPositions[i] = Hands[i].localPosition;
        warningFill = Warning.FillRenderer.transform;
        fullFillScale = warningFill.localScale;
        impactFilter = new ContactFilter2D { useTriggers = true };
        impactFilter.SetLayerMask(CollisionMask);
        prepared = true;
    }

    /// <summary>현재 실행 반경의 외곽선과 빈 내부를 같은 지면 중심에 표시</summary>
    private void ShowWarning()
    {
        // 실제 피해 반경의 바깥 경계는 유지하고 내부 원판 크기만 충전으로 확대
        MoveWarning();
        Warning.transform.localRotation = Quaternion.identity;
        Warning.transform.localScale = Vector3.one * (currentRadius * 2f);
        Warning.Configure(WarningShape.Circle, Warning.WarningColor);
        Warning.gameObject.SetActive(true);
    }

    /// <summary>손의 지면 투영 또는 고정된 중앙 위치로 경고 중심 이동</summary>
    private void MoveWarning()
    {
        // 시각적 공중 높이는 경고 중심에 더하지 않음
        Warning.transform.position = new Vector3(GroundPosition.x, GroundPosition.y, owner.transform.position.z);
    }

    /// <summary>사용하는 손들의 지면 좌표에 시각적 높이만 더해 표시</summary>
    private void SetHandHeight()
    {
        // 실제 피해 중심을 화면상의 손 높이만큼 이동시키지 않도록 분리
        for (int i = 0; i < Hands.Length; i++)
            if (IsCentral || i == SelectedHandIndex)
                Hands[i].position = new Vector3(handGroundPositions[i].x, handGroundPositions[i].y + height, owner.transform.position.z);
    }

    /// <summary>고정된 중앙을 기준으로 양손이 모일 좌우 지면 위치 계산</summary>
    private Vector2 CentralGround(int index)
    {
        // 양손의 시각적 간격과 단일 원형 피해 중심을 분리
        return GroundPosition + Vector2.right * (index == 0 ? -CentralHandOffset : CentralHandOffset);
    }

    /// <summary>지면 도달 순간의 원형 범위에만 한 번 실제 피해 전달</summary>
    private void Impact()
    {
        // 사망과 취소 콜백이 재진입해도 같은 착지 질의를 다시 실행하지 않음
        if (impacted || !IsRunning) return;
        impacted = true;
        int version = runVersion;
        Action<Vector2> callback = impactCallback;
        impactCallback = null;
        callback?.Invoke(GroundPosition);
        if (!IsRunning || runVersion != version || !OwnerReady()) return;

        // 후속 탄 생성 후 피해를 적용해 Player 사망의 전투 정리가 새 탄도 함께 닫도록 처리
        int count;
        do
        {
            count = Physics2D.OverlapCircle(GroundPosition, currentRadius, impactFilter, overlaps);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, checked(overlaps.Length * 2));
        } while (true);

        // 같은 Player의 여러 Collider를 합치고 성공한 피해만 한 번 처리
        for (int i = 0; i < count && IsRunning && runVersion == version; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                || hitPlayers.Contains(player)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(owner, currentDamage, DamageType.Normal, attackId))) hitPlayers.Add(player);
        }
    }

    /// <summary>현재 같은 보스의 전투 수명에서 손 공격을 진행할 수 있는지 확인</summary>
    private bool OwnerReady()
    {
        // 다른 객체를 다시 찾지 않고 전투 시작과 취소 경계의 현재 상태 조회
        return isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && owner.IsInitialized
            && !owner.IsDead && owner.IsInCombat && !owner.IsGroggy && !owner.IsCutScene;
    }

    /// <summary>게임 시간과 지면 기하의 유효한 설정 범위 확인</summary>
    private bool ValidSettings(bool central)
    {
        // 단계 구간의 나눗셈과 착지 판정 전에 잘못된 초기 수치 거부
        return Positive(Height) && Positive(RiseDuration) && Positive(TrackingSpeed) && Positive(FallDuration)
            && Nonnegative(RecoveryDuration) && Positive(ReturnDuration)
            && (central ? Positive(CentralChargeDuration) && Positive(CentralRadius) && Nonnegative(CentralDamage) && Nonnegative(CentralHandOffset)
                : Positive(ChargeDuration) && Positive(ImpactRadius) && Nonnegative(ImpactDamage));
    }

    /// <summary>양수 유한값이 필요한 시간·높이·반경 확인</summary>
    private static bool Positive(float value)
    {
        // 0으로 나누는 구간과 무효한 수치를 제외
        return Nonnegative(value) && value > 0f;
    }

    /// <summary>0 이상 유한값이 필요한 지연·피해 설정 확인</summary>
    private static bool Nonnegative(float value)
    {
        // 실제 게임 상태에 적용하기 전에 무효한 숫자와 음수 제외
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
