using UnityEngine;

/// <summary>고정 무기의 공격 방향·3콤보·애니메이션 시간과 실제 사각 타격 판정 관리</summary>
[DefaultExecutionOrder(10)]
public class AttackController : MonoBehaviour
{
    public Transform WeaponPivot; // 공격 중 고정한 조준 방향을 유지할 무기 부모
    public Transform WeaponMotion; // 공격 클립이 위치와 회전을 바꿀 조준 부모 아래의 검 피벗
    public Animator WeaponAnimator; // WeaponMotion의 공격 곡선을 재생할 조준 부모의 Animator
    public Vector2 AimDirection = Vector2.down; // 공격 전 마지막으로 적용한 유효 조준 방향
    public float AttackDuration = 0.36f; // AS 1에서 한 타의 준비·타격·회복에 쓰는 기본 시간
    public float HitWindowStart = 0.09f; // 공격 시작 후 사각 타격 판정을 여는 초기 튜닝 시점
    public float HitWindowEnd = 0.21f; // 공격 시작 후 사각 타격 판정을 닫는 초기 튜닝 시점
    public float AttackCooldown = 0.08f; // 공격 완료 뒤 다음 공격까지의 AS 적용 대기시간
    public float ComboDuration = 0.8f; // 공격 완료 뒤 다음 콤보를 이어갈 수 있는 게임 시간
    public float AttackMoveMultiplier = 0.35f; // PlayerCharacter가 공격 중 일반 이동량에 적용할 감속 배율
    public float ThirdHitMultiplier = 1.5f; // 3타에서 현재 공격력에 적용할 강화 배율의 초기 튜닝값
    public Vector2 HitboxSize = new Vector2(1.5f, 1f); // 실제 Cast와 Gizmo가 함께 사용할 월드 사각 크기
    public Vector2 HitboxOffset = new Vector2(0.85f, 0f); // 고정한 공격 방향을 X축으로 본 사각 중심의 상대 위치
    public LayerMask TargetMask = ~0; // 실제 피격 Collider를 조회할 레이어 범위
    public int ComboIndex { get; private set; } = -1; // 현재 0·1·2타의 내부 인덱스이며 콤보가 없으면 -1
    public float ComboRemaining { get; private set; } // 다음 콤보를 이어갈 수 있는 남은 게임 시간
    public float AttackCooldownRemaining { get; private set; } // 다음 공격 시작까지 남은 AS 1 기준 시간
    public bool IsAttacking { get; private set; } // 준비·타격·회복을 포함한 공격 행동 진행 여부
    public Vector2 AttackDirection { get; private set; } = Vector2.down; // 이번 공격 시작 순간에 고정한 월드 방향
    public CollisionBox HitBox { get; } = new CollisionBox(); // 공격별 대상 기록과 사각 Cast 버퍼를 재사용할 도구

    private Character owner; // 공격력·공격속도의 원본을 한 번 확보할 같은 객체의 캐릭터
    private DamageInstance damage; // 이번 공격 시작 시점에 확정한 피해와 식별자
    private float attackElapsed; // AS를 적용해 진행한 현재 공격의 경과시간
    private bool openedHitWindow; // 같은 공격에서 판정창을 다시 열어 적중 기록이 지워지는 상황 방지
    private readonly float[] attackClipLengths = new float[3]; // Awake에서 한 번 확보할 1·2·3타 클립의 원본 길이
    private static readonly int[] AttackStateHashes = { Animator.StringToHash("Base Layer.Attack1"), Animator.StringToHash("Base Layer.Attack2"), Animator.StringToHash("Base Layer.Attack3") }; // 문자열을 반복 변환하지 않고 사용할 세 공격 상태
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle"); // 취소·종료 때 복귀할 무기 대기 상태
    private bool weaponAnimationReady; // 세 공격 클립과 피벗 연결의 최초 준비 결과

    /// <summary>공격력·공격속도를 조회할 자기 캐릭터를 한 번 확보</summary>
    private void Awake()
    {
        // 생성 순서에 의존하지 않는 자기 컴포넌트 참조 확보
        owner = GetComponent<Character>();
        if (owner == null)
        {
            Debug.LogError($"{name}: AttackController requires a Character on the same object.", this);
            enabled = false;
            return;
        }

        // 같은 프리팹에 직렬화한 Animator와 세 클립의 길이를 한 번 확인
        PrepareWeaponAnimation();
    }

    /// <summary>공격속도로 타격·회복을 진행하고 판정 구간에서만 실제 피해 조회</summary>
    private void Update()
    {
        // 공격의 게임 시간만 진행하며 사망·컷씬 중단은 Player의 이벤트 연결에서 처리
        float deltaTime = Time.deltaTime;
        if (Time.timeScale <= 0f || deltaTime <= 0f)
            return;
        float attackTime = deltaTime * owner.AS;
        if (!IsFinite(attackTime) || attackTime <= 0f)
        {
            CancelAttack();
            return;
        }

        // 대기 중에는 공격 간격만 AS를 적용하고 콤보 보존은 게임 시간으로 진행
        if (!IsAttacking)
        {
            AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - attackTime);
            ComboRemaining = Mathf.Max(0f, ComboRemaining - deltaTime);
            if (ComboRemaining <= 0f)
                ResetCombo();
            return;
        }

        // 낮은 프레임에서 판정 구간 전체를 지나쳐도 최소 한 번 실제 영역 조회
        float previousTime = attackElapsed;
        attackElapsed += attackTime;
        if (attackElapsed >= HitWindowStart && previousTime < HitWindowEnd)
        {
            OpenHitWindow();
            GetHitArea(out Vector2 center, out float angle);
            HitBox.Query(center, HitboxSize, angle);
            if (!IsAttacking)
                return;
        }
        if (attackElapsed >= HitWindowEnd)
            CloseHitWindow();

        // 현재 공격속도와 행동 길이에 맞춰 Animator의 곡선 재생 속도 갱신
        ApplyWeaponAnimationSpeed();
        if (attackElapsed >= AttackDuration)
            FinishAttack(Mathf.Max(0f, attackElapsed - AttackDuration), owner.AS);
    }

    /// <summary>비활성화 시 다음 활성화에 공격과 콤보가 남지 않도록 정리</summary>
    private void OnDisable()
    {
        CancelAttack();
    }

    /// <summary>실제 타격 조회와 동일한 사각 영역을 Editor에 표시</summary>
    private void OnDrawGizmosSelected()
    {
        GetHitArea(out Vector2 center, out float angle);
        HitBox.DrawGizmos(center, HitboxSize, angle);
    }

    /// <summary>Player가 허용한 공격 요청의 커서 방향을 고정하고 다음 콤보 시작</summary>
    internal bool ApplyAttack(Vector2 aimWorldPosition)
    {
        // Player가 시작을 허용한 요청에 대해 무기의 중복·쿨타임·계산 수치만 확인
        if (IsAttacking || AttackCooldownRemaining > 0f || !HasValidSettings() || !IsFinite(aimWorldPosition))
            return false;

        // 공격 시작 시점의 커서 방향과 콤보·피해를 확정
        UpdateAim(aimWorldPosition - (Vector2)transform.position);
        AttackDirection = AimDirection;
        ComboIndex = ComboRemaining > 0f ? (ComboIndex + 1) % 3 : 0;
        ComboRemaining = 0f;
        int attackId = DamageInstance.NextAttackId();
        float amount = owner.AD * (ComboIndex == 2 ? ThirdHitMultiplier : 1f);
        if (!IsFinite(amount) || amount <= 0f)
        {
            ResetCombo();
            return false;
        }
        damage = new DamageInstance(owner, amount, DamageType.Normal, attackId);

        // 이전 타격 기록을 비우고 준비 자세부터 새 공격 진행
        HitBox.End();
        HitBox.ClearHits();
        attackElapsed = 0f;
        openedHitWindow = false;
        IsAttacking = true;
        ApplyWeaponAnimationSpeed();
        WeaponAnimator.Play(AttackStateHashes[ComboIndex], 0, 0f);
        WeaponAnimator.Update(0f);
        return true;
    }

    /// <summary>공격 중에는 고정 방향을 유지하고 대기 중에만 커서 방향으로 무기 회전</summary>
    public void UpdateAim(Vector2 direction)
    {
        // 무효한 커서 좌표와 공격 도중 방향 변경 제외
        if (IsAttacking || !IsFinite(direction) || !IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude <= 0.000001f)
            return;

        // 다음 공격과 대기 자세에 사용할 유효 조준 방향 보관
        AimDirection = direction.normalized;
        if (WeaponPivot != null)
            WeaponPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
    }

    /// <summary>진행 중인 공격의 첫 타격 구간에서만 공간 질의 준비</summary>
    public void OpenHitWindow()
    {
        if (!IsAttacking || openedHitWindow)
            return;
        openedHitWindow = true;
        HitBox.Begin(damage, TargetMask);
    }

    /// <summary>현재 공격의 추가 타격 판정 종료</summary>
    public void CloseHitWindow()
    {
        HitBox.End();
    }

    /// <summary>유지시간 만료나 강제 종료 시 다음 공격을 1타부터 시작하도록 정리</summary>
    public void ResetCombo()
    {
        ComboIndex = -1;
        ComboRemaining = 0f;
    }

    /// <summary>사망·컷씬·외부 차단·재배치에서 공격 판정과 남은 행동 상태 정리</summary>
    public void CancelAttack()
    {
        // 진행 중 판정과 공격 재사용 상태 정리
        CloseHitWindow();
        HitBox.ClearHits();
        IsAttacking = false;
        attackElapsed = 0f;
        openedHitWindow = false;
        AttackCooldownRemaining = 0f;
        ResetCombo();

        // 공격 도중 멈춘 무기를 마지막 조준 방향의 대기 자세로 복구
        if (WeaponPivot != null)
            WeaponPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
        ResetWeaponAnimation();
    }

    /// <summary>공격 완료 뒤 다음 공격 간격과 콤보 보존시간 설정</summary>
    private void FinishAttack(float overshoot, float attackSpeed)
    {
        // 한 프레임에서 공격 종료를 넘긴 시간도 다음 대기에 반영
        CloseHitWindow();
        IsAttacking = false;
        AttackCooldownRemaining = Mathf.Max(0f, AttackCooldown - overshoot);
        ComboRemaining = Mathf.Max(0f, ComboDuration - overshoot / attackSpeed);
        if (ComboRemaining <= 0f)
            ResetCombo();

        // 다음 프레임 커서 갱신 전까지 시작 방향의 대기 자세 유지
        if (WeaponPivot != null)
            WeaponPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(AttackDirection.y, AttackDirection.x) * Mathf.Rad2Deg);
        ResetWeaponAnimation();
    }

    /// <summary>생성 시 같은 프리팹의 조준·모션 연결과 세 클립의 길이 확인</summary>
    private void PrepareWeaponAnimation()
    {
        // Animator는 조준 부모를 건드리지 않고 바로 아래 모션 피벗만 제어
        if (WeaponPivot == null || WeaponMotion == null || WeaponAnimator == null
            || WeaponMotion.parent != WeaponPivot || WeaponAnimator.transform != WeaponPivot
            || WeaponAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError($"{name}: Connect WeaponPivot, its WeaponMotion child and the weapon Animator.", this);
            return;
        }

        // Player 수명 동안 고정된 클립을 최초 한 번 조회하고 실제 배속 계산에 재사용
        foreach (AnimationClip clip in WeaponAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Attack1") attackClipLengths[0] = clip.length;
            else if (clip.name == "Attack2") attackClipLengths[1] = clip.length;
            else if (clip.name == "Attack3") attackClipLengths[2] = clip.length;
        }
        for (int i = 0; i < attackClipLengths.Length; i++)
        {
            if (attackClipLengths[i] > 0f && IsFinite(attackClipLengths[i])) continue;
            Debug.LogError($"{name}: Missing weapon animation Attack{i + 1}.", this);
            return;
        }

        // 판정 타이머와 같은 게임 시간을 사용하고 화면 밖에서도 공격 자세 진행
        WeaponAnimator.updateMode = AnimatorUpdateMode.Normal;
        WeaponAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        WeaponAnimator.applyRootMotion = false;
        WeaponAnimator.keepAnimatorStateOnDisable = false;
        weaponAnimationReady = true;
    }

    /// <summary>각 클립의 원본 길이를 현재 공격 시간과 공격속도에 맞춰 재생</summary>
    private void ApplyWeaponAnimationSpeed()
    {
        WeaponAnimator.speed = attackClipLengths[ComboIndex] / AttackDuration * owner.AS;
    }

    /// <summary>종료·비활성화·판정 콜백 중 취소에서도 검의 대기 자세를 즉시 복구</summary>
    private void ResetWeaponAnimation()
    {
        // 활성 Animator는 대기 상태를 즉시 평가해 다음 갱신의 공격 자세 재적용 방지
        if (weaponAnimationReady && WeaponAnimator != null)
        {
            WeaponAnimator.speed = 1f;
            if (WeaponAnimator.isActiveAndEnabled)
            {
                WeaponAnimator.Play(IdleStateHash, 0, 0f);
                WeaponAnimator.Update(0f);
            }
        }

        // 비활성화 도중에도 마지막 공격 위치·회전이 남지 않도록 모션 피벗 복구
        if (WeaponMotion != null)
        {
            WeaponMotion.localPosition = Vector3.zero;
            WeaponMotion.localRotation = Quaternion.identity;
        }
    }

    /// <summary>공격 시작 방향으로 오프셋을 회전해 Cast와 Gizmo의 공통 기하 계산</summary>
    private void GetHitArea(out Vector2 center, out float angle)
    {
        Vector2 direction = IsAttacking ? AttackDirection : AimDirection;
        if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            direction = Vector2.down;
        angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        center = (Vector2)transform.position + direction * HitboxOffset.x + normal * HitboxOffset.y;
    }

    /// <summary>공격 시간·형상·원본 수치가 실행 가능한 범위인지 확인</summary>
    private bool HasValidSettings()
    {
        return weaponAnimationReady && WeaponAnimator != null && WeaponAnimator.isActiveAndEnabled
            && IsFinite(owner.AD) && owner.AD > 0f && IsFinite(owner.AS) && owner.AS > 0f
            && IsFinite(AttackDuration) && AttackDuration > 0f && IsFinite(HitWindowStart) && HitWindowStart >= 0f
            && IsFinite(HitWindowEnd) && HitWindowEnd > HitWindowStart && HitWindowEnd <= AttackDuration
            && IsFinite(AttackCooldown) && AttackCooldown >= 0f && IsFinite(ComboDuration) && ComboDuration >= 0f
            && IsFinite(ThirdHitMultiplier) && ThirdHitMultiplier > 0f
            && IsFinite(AttackMoveMultiplier) && AttackMoveMultiplier >= 0f && AttackMoveMultiplier <= 1f
            && IsFinite(HitboxSize) && HitboxSize.x > 0f && HitboxSize.y > 0f && IsFinite(HitboxOffset);
    }

    /// <summary>입력 방향·형상의 두 성분이 유한한지 확인</summary>
    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>수치가 NaN이나 무한이 아닌지 확인</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
