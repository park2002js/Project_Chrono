using System;
using UnityEngine;

/// <summary>입력 의도를 보관하고 이동 가능 여부·방향·몸체와 무기 표현 조정</summary>
[DefaultExecutionOrder(0)]
[RequireComponent(typeof(PlayerSpawner), typeof(PlayerBodyController), typeof(CharacterRendering))]
[RequireComponent(typeof(AttackController))]
[RequireComponent(typeof(SkillController))]
[RequireComponent(typeof(CharacterBuff))]
public class PlayerCharacter : Character
{
    public bool IsInCombat; // Player의 전투 여부
    public bool IsAttacking => attack != null && attack.IsAttacking; // 무기가 실행 중인 일반 공격 상태
    public SkillController Skills { get; private set; } // Draw 편집·Line 수명과 Slash 실행·전환을 담당할 자기 능력 컴포넌트
    public Line? CurrentLine; // SkillController가 확정·해제하고 외부가 조회할 현재 경로의 유일한 원본
    public CharacterBuff Buffs { get; private set; } // 각성의 적용·기간·수치 복구를 담당할 자기 효과 컴포넌트
    public PlayerSpawner Spawner { get; private set; } // 같은 인스턴스의 배치·부활과 활성 체크포인트를 관리할 자기 컴포넌트
    public bool IsSlashing => Skills != null && Skills.IsSlashing; // 일반 이동·공격·대시를 막을 Slash 실행 상태
    public override bool IsInvulnerable { get => base.IsInvulnerable || IsSlashing || IsSpawning || IsInteracting; set => base.IsInvulnerable = value; } // 외부 무적과 능력·생성·상호작용의 전면 보호 합산
    public bool IsSpawning => Spawner != null && Spawner.IsSpawning; // 생성·부활 기상 중인지 자기 배치 컴포넌트에서 조회
    public bool IsInteracting => activeInteraction != null; // 수정의 완료·취소 사건을 기다리는 상호작용 상태
    public event Action<bool> OnInteractionStateChanged; // 상호작용 진입·종료를 자기 표현 구독자에게 알릴 사건
    public float Groggy; // Slash로 제거·반사한 탄환에서 얻은 현재 그로기 게이지
    public const float MaxGroggy = 100f; // 보스 그로기를 즉시 발동하고 소모할 게이지 상한
    public float HitProtectionDuration = 0.75f; // 유효 피해 뒤 Hitbox 보호와 깜빡임을 유지할 게임 시간의 초기 튜닝값
    public float HitProtectionRemaining => hitProtectionActive ? (float)Math.Max(0d, hitProtectionEndsAt - Time.timeAsDouble) : 0f; // Update 이전에도 현재 시각으로 조회할 피격 보호 잔여
    public float HitProtectionElapsed => hitProtectionActive ? Mathf.Clamp(hitProtectionTotal - HitProtectionRemaining, 0f, hitProtectionTotal) : 0f; // 표현이 별도 타이머 없이 사용할 현재 보호의 경과시간
    public override bool IsHitProtected => hitProtectionActive && HitProtectionRemaining > 0f; // 피격 뒤 Hitbox 피해만 차단하는 현재 보호 여부
    public event Action<bool> OnHitProtectionChanged; // 보호 진입·종료를 표현 등 자기 책임의 구독자에게 알릴 사건
    public bool IsDrawing => Skills != null && Skills.IsDrawing; // Draw 전용 입력과 표시가 활성화된 상태
    public Vector2 MoveDirection; // 반대 입력 상쇄·대각 정규화를 마친 이동 의도
    public Vector2 AimDirection = Vector2.down; // 마지막 유효 커서 방향, 커서가 중심이면 기존 값 유지
    public Vector2 AimWorldPosition; // 입력 측에서 전달한 커서의 월드 좌표
    public float DashSpeed = 14f; // 대시 중 초당 이동 거리의 초기 튜닝값
    public float DashDuration = 0.18f; // 대시 이동량을 계산할 시간의 초기 튜닝값(초)
    public float DodgeDuration = 0.16f; // Hitbox 피해 회피 상태를 유지할 독립 시간(초)
    public float InputBlockDuration = 0.24f; // 대시 시작 뒤 일반 이동·새 행동을 잠글 독립 시간(초)
    public float DashCooldown = 0.65f; // 대시 시작부터 다음 사용까지의 독립 재사용 시간(초)
    public Vector2 DashDirection; // 대시 시작 순간 정규화해 고정한 이동 방향
    public float DashRemaining; // 대시 이동량을 계산할 남은 게임 시간
    public float DodgeRemaining; // Hitbox 피해 회피가 유지될 남은 게임 시간
    public float InputBlockRemaining; // 대시 자체 입력 잠금이 유지될 남은 게임 시간
    public float DashCooldownRemaining; // 다음 대시까지 남은 게임 시간
    public bool CanMove => IsInitialized && !IsDead && !IsCutScene && !IsSpawning && !IsInteracting && Time.timeScale > 0f; // 현재 일반 이동·바라보기의 처리 가능 여부
    public bool IsDashing => DashRemaining > 0f; // 현재 고정 방향의 대시 이동 구간인지 조회
    public override bool IsDodge => DodgeRemaining > 0f; // 대시의 Hitbox 피해 회피 상태이며 모든 피해 무적과 구분
    public bool IsInputBlocked => InputBlockRemaining > 0f; // 대시 자체 일반 이동·새 행동 잠금 여부

    private PlayerBodyController body; // 계산한 변위의 유효성·충돌 검사와 실제 위치 적용을 요청할 컴포넌트
    private CharacterRendering rendering; // 조준 방향과 계산한 이동 의도를 모션으로 표시할 컴포넌트
    private AttackController attack; // 허용된 공격의 애니메이션·판정·피해 적용 담당
    private bool attackHeld; // 입력 계층에서 전달한 공격 버튼 유지 의도
    private bool hitProtectionActive; // 보호 진입을 알린 뒤 종료 알림이 필요한 상태
    private double hitProtectionEndsAt; // 정지 중 흐르지 않는 게임 시각으로 정한 보호 만료 기한
    private float hitProtectionTotal; // 이번 피격에서 고정한 보호 길이이며 중간 설정 변경과 구분
    private CrystalCheckPoint activeInteraction; // 이번 상호작용의 완료·중단 알림을 받을 수정
    private bool interactionNotified; // 수락 전 실패에서 종료 사건을 발행하지 않을 진입 알림 상태

    /// <summary>자신의 이동·표시·무기 컴포넌트와 초기 조준 위치 준비</summary>
    protected override void Awake()
    {
        // 입력 의도를 실행하고 표시할 자기 컴포넌트 확보
        base.Awake();
        Buffs = GetComponent<CharacterBuff>();
        Spawner = GetComponent<PlayerSpawner>();
        body = GetComponent<PlayerBodyController>();
        rendering = GetComponent<CharacterRendering>();
        attack = GetComponent<AttackController>();
        Skills = GetComponent<SkillController>();

        // 첫 커서 입력 전 사용할 기본 조준 위치 설정
        AimWorldPosition = (Vector2)transform.position + AimDirection;

        // 피해·상태 사건에서 자기 보호·진행 중 행동을 정리할 구독 연결
        OnDamaged += BeginHitProtection; // 외부 Start·Rendering Awake200 구독보다 먼저 피해 보호 상태 확정
        OnDie += ResetHitProtection; // 실제 사망 순간 보호·표현의 종료 알림
        OnRevive += ResetForRevive; // 부활 사건에서 자기 행동·경로·게이지 초기화
        OnDie += CancelInteraction; // 사망 순간 아직 완료되지 않은 수정 상호작용 중단
        OnCutScene += CancelInteraction; // 외부 컷씬이 시작되면 수정의 자기 잠금만 정리
        OnDie += CancelAttack; // 치명 피해가 확정된 순간 공격 의도·무기 판정 종료
        OnCutScene += CancelAttack; // 컷씬 진입 순간 공격 의도·무기 판정 종료
        OnDie += ResetDraw; // 사망 순간 편집과 확정 Line·능력 대기 상태 정리
        OnCutScene += CancelDraw; // 컷씬 진입 순간 Draw의 입력·시간·표시 복구
        OnCutScene += CancelSlash; // 컷씬 진입 순간 Slash의 판정·보호·정지 소유 복구
    }

    /// <summary>순서0의 Update에서 이동을 계산·즉시 적용하고 표시 의도와 독립 타이머 전달</summary>
    private void Update()
    {
        // 이동 가능 여부와 별개로 만료된 피격 보호의 종료만 알림
        UpdateHitProtection();

        // 물리 적용 대상이 비활성화되면 행동 시간을 포함한 이동 상태 초기화
        if (!body.isActiveAndEnabled)
        {
            ResetMotion();
            return;
        }

        // 사망·컷씬은 대시를 취소하고 공격 중단은 진입 이벤트에서 처리, 시간 정지는 상태 보존
        if (!CanMove)
        {
            SetMoveInput(Vector2.zero);
            rendering.SetMovement(Vector2.zero, AimDirection);
            if (!IsInitialized || IsDead || IsCutScene)
                CancelDash();
            if (!IsInitialized)
                CancelAttack();
            return;
        }

        // 커서가 중심이면 이전 방향을 유지하고 몸체·무기에 같은 방향 적용
        Vector2 direction = AimWorldPosition - (Vector2)transform.position;
        if (direction.sqrMagnitude > 0.000001f)
            AimDirection = direction.normalized;
        rendering.SetFacing(AimDirection);
        attack.UpdateAim(AimDirection);

        // 입력 유지 중에도 캐릭터가 새 공격의 허용 상태와 조준 좌표 결정
        if (attackHeld)
            ApplyAttack();

        // 한 Update 안에서 대시·입력 잠금 종료 전후의 시간만큼 각각 이동량 계산
        float deltaTime = Time.deltaTime;
        float dashTime = Mathf.Min(deltaTime, DashRemaining);
        float moveTime = Mathf.Max(0f, deltaTime - Mathf.Max(DashRemaining, InputBlockRemaining));
        float moveMultiplier = IsAttacking ? Mathf.Clamp01(attack.AttackMoveMultiplier) : 1f;
        Vector2 moveDisplacement = MoveDirection * Mathf.Max(0f, SP) * moveMultiplier * moveTime;
        Vector2 displacement = DashDirection * DashSpeed * dashTime + moveDisplacement;
        body.Move(displacement);
        rendering.SetMovement(moveDisplacement, AimDirection);

        // 같은 Update에서 캐릭터가 소유한 행동 시간 진행
        DashRemaining = Mathf.Max(0f, DashRemaining - deltaTime);
        DodgeRemaining = Mathf.Max(0f, DodgeRemaining - deltaTime);
        InputBlockRemaining = Mathf.Max(0f, InputBlockRemaining - deltaTime);
        DashCooldownRemaining = Mathf.Max(0f, DashCooldownRemaining - deltaTime);
    }

    private void OnDisable()
    {
        CancelInteraction();
        ResetHitProtection();
        CancelDash();
        CancelAttack();
        CancelDraw();
        CancelSlash();

        // 비활성화 뒤 이전 이동 의도가 모션에 남지 않도록 정리
        if (rendering != null) rendering.SetMovement(Vector2.zero, AimDirection);
    }

    private void OnDestroy()
    {
        OnDamaged -= BeginHitProtection; // 제거된 Player의 피해 보호 연결 해제
        OnDie -= ResetHitProtection; // 제거된 Player의 보호 정리 연결 해제
        OnRevive -= ResetForRevive; // 제거된 Player의 부활 초기화 연결 해제
        OnDie -= CancelInteraction; // 제거된 Player의 상호작용 사망 연결 해제
        OnCutScene -= CancelInteraction; // 제거된 Player의 상호작용 컷씬 연결 해제
        OnDie -= CancelAttack; // 제거된 Player의 사망 중단 연결 해제
        OnCutScene -= CancelAttack; // 제거된 Player의 컷씬 중단 연결 해제
        OnDie -= ResetDraw; // 제거된 Player의 능력 초기화 연결 해제
        OnCutScene -= CancelDraw; // 제거된 Player의 Draw 중단 연결 해제
        OnCutScene -= CancelSlash; // 제거된 Player의 Slash 중단 연결 해제
    }

    /// <summary>입력 계층의 최신 이동 의도를 보관하고 다음 Update에서 변위에 반영</summary>
    public void SetMoveInput(Vector2 direction)
    {
        MoveDirection = Vector2.ClampMagnitude(direction, 1f);
    }

    /// <summary>화면 입력 계층이 변환한 커서 월드 좌표 보관</summary>
    public void SetAimPosition(Vector2 position)
    {
        AimWorldPosition = position;
    }

    /// <summary>버튼 유지 의도만 갱신하며 해제 시 이미 시작한 한 타는 유지</summary>
    public void SetAttackInput(bool held)
    {
        attackHeld = held;
    }

    /// <summary>공통 행동 상태를 확인하고 최신 조준 좌표로 무기에 공격 요청</summary>
    public bool ApplyAttack()
    {
        // 대시·입력 잠금과 비활성 상태에서는 공격 전환 금지
        if (!isActiveAndEnabled || !CanMove || IsDrawing || IsSlashing || IsDashing || IsInputBlocked ||
            body == null || !body.isActiveAndEnabled || attack == null || !attack.isActiveAndEnabled)
            return false;

        // 방향의 고정·콤보·시간·피해는 단일 무기가 관리
        return attack.ApplyAttack(AimWorldPosition);
    }

    /// <summary>강제 중단 시 공격 유지 의도와 무기의 진행·콤보 정리</summary>
    public void CancelAttack()
    {
        attackHeld = false;
        if (attack != null)
            attack.CancelAttack();
    }

    /// <summary>이동 의도 우선·정지 시 커서 방향으로 허용된 대시 시작</summary>
    public bool ApplyDash()
    {
        // 행동 상태·입력 잠금·재사용 제한과 대시 설정을 캐릭터에서 확인
        if (!isActiveAndEnabled || !CanMove || IsDrawing || IsSlashing || IsAttacking || body == null || !body.isActiveAndEnabled ||
            IsDashing || IsInputBlocked || DashCooldownRemaining > 0f ||
            !ValidActionValue(DashSpeed, false) || !ValidActionValue(DashDuration, false) ||
            !ValidActionValue(DodgeDuration, true) || !ValidActionValue(InputBlockDuration, true) || !ValidActionValue(DashCooldown, true))
            return false;

        // 같은 프레임의 최신 이동·커서를 사용하고 중심 커서는 마지막 방향 유지
        Vector2 direction = MoveDirection.sqrMagnitude > 0.000001f
            ? MoveDirection : AimWorldPosition - (Vector2)base.transform.position;
        if (direction.sqrMagnitude <= 0.000001f)
            direction = AimDirection.sqrMagnitude > 0.000001f ? AimDirection : Vector2.down;

        // 일반 이동과 대시 변위는 다음 Update 한 곳에서 함께 계산
        DashDirection = direction.normalized;
        DashRemaining = DashDuration;
        DodgeRemaining = DodgeDuration;
        InputBlockRemaining = InputBlockDuration;
        DashCooldownRemaining = DashCooldown;
        rendering.BeginDash(DashDirection);
        return true;
    }

    /// <summary>외부 입력 차단·사망·컷씬의 대시와 마지막 몸체 이동 상태 정리</summary>
    public void CancelDash()
    {
        // 행동 제한의 취소는 이미 시작한 쿨타임을 유지
        DashRemaining = 0f;
        DodgeRemaining = 0f;
        InputBlockRemaining = 0f;
        DashDirection = Vector2.zero;
        if (body != null)
            body.Stop();
    }

    /// <summary>재배치 전에 이동 의도와 대시의 모든 시간을 초기화</summary>
    public void ResetMotion()
    {
        CancelInteraction();
        ResetHitProtection();
        MoveDirection = Vector2.zero;
        CancelDash();
        CancelAttack();
        CancelDraw();
        CancelSlash();
        DashCooldownRemaining = 0f;

        // 재배치 이후에는 새 이동 의도를 받을 때까지 정지 모션 유지
        if (rendering != null) rendering.SetMovement(Vector2.zero, AimDirection);
    }

    /// <summary>공통 행동 상태를 확인하고 Draw 편집 시작 요청</summary>
    public bool ApplyDraw()
    {
        // 캐릭터의 시작 허용과 능력 자체의 사용 가능 여부 구분
        if (!isActiveAndEnabled || !CanMove || IsDrawing || IsSlashing || IsAttacking || IsDashing || IsInputBlocked ||
            body == null || !body.isActiveAndEnabled || Skills == null || !Skills.isActiveAndEnabled)
            return false;
        if (!Skills.ApplyDraw())
            return false;

        // 전환 직전 이미 계산된 이동과 공격 유지 의도 정리
        MoveDirection = Vector2.zero;
        attackHeld = false;
        body.Stop();
        return true;
    }

    /// <summary>Draw 편집과 전용 입력·시간·표시를 기존 상태로 복구</summary>
    public void CancelDraw()
    {
        if (Skills != null)
            Skills.CancelDraw();
    }

    /// <summary>공통 행동 상태를 확인하고 보유 Line의 Slash 실행 요청</summary>
    public bool ApplySlash()
    {
        // 생존·컷씬·다른 행동의 시작 허용은 Character 한 곳에서 판단
        if (!isActiveAndEnabled || !CanMove || IsDrawing || IsSlashing || IsAttacking || IsDashing || IsInputBlocked ||
            body == null || !body.isActiveAndEnabled || Skills == null || !Skills.isActiveAndEnabled) return false;
        if (!Skills.ApplySlash()) return false;

        // 정지 직전의 이동 상태와 공격 유지 의도 정리
        MoveDirection = Vector2.zero;
        attackHeld = false;
        body.Stop();
        return true;
    }

    /// <summary>이미 실행 중인 Slash의 판정·정지·표현 복구 요청</summary>
    public void CancelSlash()
    {
        if (Skills != null) Skills.CancelSlash();
    }

    /// <summary>탄환의 Slash 보상을 누적하고 상한 도달 순간 현재 Boss 그로기 요청</summary>
    public void AddGroggy(float amount, BossCharacter boss)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
        Groggy = Mathf.Min(MaxGroggy, Groggy + amount);
        if (Groggy < MaxGroggy || boss == null || boss.IsDead) return;
        Groggy = 0f;
        boss.EnterGroggy();
    }

    /// <summary>공통 행동 허용을 확인하고 후보 수정에 상호작용 전달</summary>
    public bool ApplyInteraction(CrystalCheckPoint target)
    {
        if (!isActiveAndEnabled || !CanMove || IsDrawing || IsSlashing || IsAttacking || IsDashing || IsInputBlocked ||
            body == null || !body.isActiveAndEnabled || target == null) return false;
        activeInteraction = target;
        target.OnActivationFinished += HandleInteractionFinished; // 수정이 완료·취소되면 자기 입력·보호 상태 해제
        if (!target.activateCheckPoint(this))
        {
            HandleInteractionFinished(target);
            return false;
        }
        MoveDirection = Vector2.zero;
        attackHeld = false;
        body.Stop();
        interactionNotified = true;
        OnInteractionStateChanged?.Invoke(true);
        return true;
    }

    /// <summary>진행 중 수정의 자기 실행만 중단하고 완료·취소 사건으로 복구</summary>
    public void CancelInteraction()
    {
        CrystalCheckPoint target = activeInteraction;
        if (target != null) target.CancelActivation();
        if (activeInteraction != null) HandleInteractionFinished(activeInteraction);
    }

    /// <summary>완료·취소를 알린 수정과 자기 상호작용 상태만 정리</summary>
    private void HandleInteractionFinished(CrystalCheckPoint target)
    {
        if (target != activeInteraction) return;
        if (target != null) target.OnActivationFinished -= HandleInteractionFinished; // 완료된 수정의 자기 구독 해제
        activeInteraction = null;
        if (!interactionNotified) return;
        interactionNotified = false;
        OnInteractionStateChanged?.Invoke(false);
    }

    /// <summary>부활 사건에서 자기 행동·경로·게이지를 새 전투의 기본 상태로 복원</summary>
    private void ResetForRevive()
    {
        ResetMotion();
        if (Skills != null) Skills.ResetSkills();
        Groggy = 0f;
        IsInCombat = false;
    }

    /// <summary>사망·재배치·비활성화에서 자기 피격 보호를 비우고 종료만 알림</summary>
    public void ResetHitProtection()
    {
        bool wasActive = hitProtectionActive;
        hitProtectionActive = false;
        hitProtectionEndsAt = 0d;
        hitProtectionTotal = 0f;
        if (wasActive) OnHitProtectionChanged?.Invoke(false);
    }

    /// <summary>유효 피해 사건을 받아 자기 보호 기한을 확정하고 진입만 알림</summary>
    private void BeginHitProtection(DamageInstance damage)
    {
        // 치명 피해는 기존 사망 구독자가 정리하며 무효 설정은 보호를 열지 않음
        if (IsDead || !ValidActionValue(HitProtectionDuration, false)) return;
        UpdateHitProtection();

        // 표현 구독자가 읽기 전에 새 보호의 기한·길이를 먼저 확정
        bool wasActive = hitProtectionActive;
        hitProtectionActive = true;
        hitProtectionTotal = HitProtectionDuration;
        hitProtectionEndsAt = Time.timeAsDouble + hitProtectionTotal;
        if (!wasActive) OnHitProtectionChanged?.Invoke(true);
    }

    /// <summary>같은 게임 시각의 만료를 한 번만 종료 사건으로 전달</summary>
    private void UpdateHitProtection()
    {
        if (hitProtectionActive && HitProtectionRemaining <= 0f) ResetHitProtection();
    }

    /// <summary>실제 사망 때 확정 Line까지 능력 상태 초기화</summary>
    private void ResetDraw()
    {
        if (Skills != null)
            Skills.ResetSkills();
    }

    /// <summary>이동·보호에 사용할 행동 설정이 유한하며 허용된 범위인지 확인</summary>
    private static bool ValidActionValue(float value, bool allowZero)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && (allowZero ? value >= 0f : value > 0f);
    }
}
