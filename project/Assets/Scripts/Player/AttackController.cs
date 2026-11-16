using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>원작 대검의 공격 곡선·입력 버퍼·콤보 대기와 확정3타 검기·단일 타격 관리</summary>
[DefaultExecutionOrder(10)]
public class AttackController : MonoBehaviour
{
    public Transform WeaponPivot; // 조준 방향·어깨 위치·좌우 반사를 적용할 부모
    public Transform WeaponMotion; // 원작 위치·회전 곡선이 움직일 검 피벗
    public Animator WeaponAnimator; // 공격과 콤보 대기 곡선을 평가할 Animator
    public SpriteRenderer WeaponRenderer; // 몸체 앞뒤로 정렬할 검 이미지
    public SpriteRenderer AttackVfxRenderer; // 검기 표시 풀의 첫 렌더러와 재사용 설정 원본
    public Sprite[] Attack1Vfx; // 반전 전 보라 검기의 네 단계
    public Sprite[] Attack2Vfx; // 반전 전 하늘 검기의 네 단계
    public Sprite[] Attack3Vfx; // 보라단독·두큰호·단계차·양쪽2/3/4단계의 여섯 합성 이미지
    public Vector2 AimDirection = Vector2.down; // 대기 중 마지막 유효 조준 방향
    public float AttackDuration = 0.6f; // 원작1타 길이와 세 공격 시간 배율의 기준
    public float HitWindowStart = 11f / 30f; // 원작1타의 판정·전진 생성 시점 기준
    public float HitWindowEnd = 11f / 30f + 0.15f; // 생성 후 AS와 별도로 유지할 판정 수명의 기준
    public float AttackCooldown; // 원작에는 없는 추가 간격으로 기본값0 유지
    public float ComboDuration = 1f / 6f; // 공격 종료 후 AS와 무관하게 다음 콤보를 받을 시간
    public float AttackMoveMultiplier; // 공격 중 일반 입력 이동을0으로 차단할 배율
    public float InputIgnoreDuration = 1f / 30f; // 1·2타 시작 직후 짧은 클릭을 버릴 원작 구간
    public float ThirdInputBufferTime = 35f / 60f; // 새3타의 검기가 끝난 뒤 다음 입력을 예약할 추론 시점
    public float AttackDashSpeed = 10f / 3f; // 0.3초 선형 감속으로 기본 거리0.5를 이동할 전진 초기 속도
    public float AttackDashDuration = 0.3f; // AS와 별도로 선형 감속하는 전진 시간
    public float ThirdHitMultiplier = 1.5f; // 3타 한 번의 피해에 적용할 기존 강화 배율
    public Vector2 HitboxSize = new Vector2(1.5f, 1f); // 프로젝트의 사각 타격 영역 크기
    public Vector2 HitboxOffset = new Vector2(0.85f, 0f); // 공격 방향을 기준으로 한 타격 영역 오프셋
    public LayerMask TargetMask = ~0; // 타격 Cast에서 조회할 레이어
    public int ComboIndex { get; private set; } = -1; // 현재0·1·2타이며 콤보가 없으면-1
    public float ComboRemaining { get; private set; } // AS 비적용 콤보 유효 잔여시간
    public float AttackCooldownRemaining { get; private set; } // 추가 간격을 설정한 경우에만 남는 AS 적용 대기
    public bool IsAttacking { get; private set; } // 준비부터 회복까지 무기가 수행 중인 공격
    public bool HasBufferedAttack { get; private set; } // 허용 구간에서 받은 클릭 한 번의 다음 공격 예약
    public float CurrentAttackDuration => attackClipLengths[Mathf.Clamp(ComboIndex, 0, 2)] * DurationScale; // 현재 타의 실제 기본 길이
    public float AttackInputOpenTime => (ComboIndex == 2 ? ThirdInputBufferTime : InputIgnoreDuration) * DurationScale; // 현재 공격의 새 클릭 허용 경계
    public bool IsAttackInputAllowed => !IsAttacking || attackElapsed + 0.000001f >= AttackInputOpenTime; // 초기 무시 구간이 끝났는지 조회
    public Vector2 AttackDirection { get; private set; } = Vector2.down; // 공격 시작 때 고정한 조준 방향
    public CollisionBox HitBox { get; private set; } = new CollisionBox(); // 가장 최근 생성된 공격 판정과 적중 대상 조회

    private Character owner; // 한 번 확보할 공격력·공격속도 원본
    private PlayerCharacter player; // 공격 시작의 이동 의도와 기본 이동 배율 조회 대상
    private DamageInstance damage; // 이번 공격에서 한 번 확정한 피해와 식별자
    private float attackElapsed; // 현재 공격의 AS 적용 경과시간
    private bool openedHitWindow; // 두 번 휘두르는3타도 판정을 한 번만 생성할 표시
    private bool spawnedEffect; // 같은 공격의 검기를 반복 생성하지 않을 표시
    private bool playedSecondSwingSound; // 두 번 휘두르는3타의 두 번째 소리 중복 방지
    private PlayerFeedback feedback; // 자기 수명 동안 재사용할 공격 소리·적중 섬광 담당
    private Action<Vector2, float> hitFeedback; // 매 질의에서 만들지 않고 재사용할 성공 타격 콜백
    private float dashElapsed; // 별도 강제 전진의 게임 경과시간
    private Vector2 attackDashDirection; // 전진 시작 때 고정한 입력 또는 조준 방향
    private Vector2 attackMoveInput; // 이번 공격 시작 때 고정한 이동 입력이며0이면 공격 조준 사용
    private float currentAttackDashSpeed; // 타격 시 확정해 다음 타의 준비 중에도 유지할 전진 초기 속도
    private bool attackDashActive; // 일반 이동과 별개로 적용할 전진 진행 여부
    private bool weaponAnimationReady; // Animator·렌더러·검기 참조의 최초 확인 결과
    private int bodySortingOrder; // 같은 Player 몸체를 기준으로 검·검기의 앞뒤를 정할 고정 순서
    private readonly float[] attackClipLengths = new float[3]; // 최초 한 번 확보할 각 공격의 원본 길이
    private readonly List<AttackEffect> effects = new List<AttackEffect>(3); // 동시 재생 검기를 필요 수량까지 재사용할 풀
    private readonly List<AttackHit> hits = new List<AttackHit>(3); // 배속 중 겹친 판정의 수명·방향·대상을 독립 유지할 풀
    private static readonly int[] AttackStateHashes = { Animator.StringToHash("Base Layer.Attack1"), Animator.StringToHash("Base Layer.Attack2"), Animator.StringToHash("Base Layer.Attack3") }; // 공격 상태 이름의 반복 변환 방지
    private static readonly int[] WaitStateHashes = { Animator.StringToHash("Base Layer.Wait2"), Animator.StringToHash("Base Layer.Wait3") }; // 콤보 대기 곡선 상태
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle"); // 원작 대기 호흡 곡선 상태
    private float DurationScale => AttackDuration / 0.6f; // 원작 클립 시간에 공통 적용할 기존 조정값

    /// <summary>생성 시 고정 참조·클립·재사용 검기 표시 준비</summary>
    private void Awake()
    {
        // 자기 캐릭터와 직렬화 연결만 확인하며 외부 Instance를 조회하지 않음
        owner = GetComponent<Character>();
        player = owner as PlayerCharacter;
        feedback = GetComponent<PlayerFeedback>();
        if (feedback != null) hitFeedback = feedback.PlayImpact;
        if (owner == null) { enabled = false; Debug.LogError(name + ": AttackController requires Character.", this); return; }
        PrepareWeaponAnimation();
        if (!weaponAnimationReady) return;
        hits.Add(new AttackHit { Box = HitBox });
        hits.Add(new AttackHit());
        hits.Add(new AttackHit());

        // 원작 검기는 위치만 Player를 따르고 생성 방향은 고정하므로 조준 부모에서 분리
        AttackVfxRenderer.transform.SetParent(transform, false);
        effects.Add(new AttackEffect { Renderer = AttackVfxRenderer });
        CreateEffect();
        CreateEffect();
        ResetWeaponAnimation();
    }

    /// <summary>비활성 중 평가할 수 없었던 Animator의 공격 정지 상태를 대기 곡선으로 복구</summary>
    private void OnEnable()
    {
        if (weaponAnimationReady) ResetWeaponAnimation();
    }

    /// <summary>공격 곡선·실제 판정·AS 독립 검기와 콤보 유효시간 진행</summary>
    private void Update()
    {
        // 정지 중에는 표시·공격·콤보의 게임 시간을 모두 보존
        float dt = Time.deltaTime;
        if (!weaponAnimationReady || Time.timeScale <= 0f || dt <= 0f) return;
        if (!IsFinite(owner.AS) || owner.AS <= 0f) { CancelAttack(); return; }
        AdvanceEffects(dt);

        // 이미 생성한 판정은 공격 클립 배속과 별개로0.15초 동안만 진행
        AdvanceHits(dt);

        // 공격 사이에는 강제 쿨다운 없이 AS 독립 콤보 대기 곡선 평가
        if (!IsAttacking)
        {
            AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - dt * owner.AS);
            if (ComboRemaining > 0f)
            {
                ComboRemaining = Mathf.Max(0f, ComboRemaining - dt);
                if (ComboRemaining > 0f) SampleWait();
                else ResetCombo();
            }
            return;
        }

        // 한 경과시간으로 검 곡선과 생성 이벤트를 평가해 높은 FPS에서도 같은 시점 유지
        int attackId = damage.AttackId;
        float previous = attackElapsed;
        attackElapsed += dt * owner.AS;
        float start = GetHitStart();
        if (!openedHitWindow && previous <= start && attackElapsed >= start)
        {
            OpenHitWindow();
            StartAttackDash();
            GetHitArea(out Vector2 center, out float angle);
            HitBox.Query(center, HitboxSize, angle, hitFeedback);
            if (!IsAttacking || damage.AttackId != attackId) return;
        }
        float effectStart = (ComboIndex == 0 ? 11f / 30f : ComboIndex == 1 ? 7f / 30f : 22f / 60f) * DurationScale;
        if (!spawnedEffect && attackElapsed >= effectStart)
        {
            spawnedEffect = true;
            SpawnEffect(ComboIndex, (attackElapsed - effectStart) / (ComboIndex == 2 ? DurationScale : owner.AS));
        }
        if (!IsAttacking || damage.AttackId != attackId) return;

        // 새3타의 두 번째 검기가 시작되는 시점에 B 소리를 이어 재생
        if (ComboIndex == 2 && !playedSecondSwingSound && attackElapsed >= 24f / 60f * DurationScale)
        {
            playedSecondSwingSound = true;
            if (feedback != null) feedback.PlayAttackSound(1);
        }
        if (!IsAttacking || damage.AttackId != attackId) return;
        SampleAttack(Mathf.Min(attackElapsed, CurrentAttackDuration));
        if (attackElapsed >= CurrentAttackDuration) FinishAttack(attackElapsed - CurrentAttackDuration);
    }

    private void OnDisable() { CancelAttack(); }

    /// <summary>새 클릭을 시작하거나 허용 구간의 클릭 한 번을 다음 공격으로 예약</summary>
    internal bool ApplyAttack(Vector2 aimWorldPosition)
    {
        // 첫1/30초의 짧은 클릭은 저장하지 않고 허용 뒤 클릭은 해제와 무관하게 한 번 예약
        if (!HasValidSettings() || !IsFinite(aimWorldPosition)) return false;
        if (IsAttacking)
        {
            if (!IsAttackInputAllowed) return false;
            HasBufferedAttack = true;
            return true;
        }
        if (AttackCooldownRemaining > 0f) return false;

        // 최신 조준으로 다음 타를 결정하고 한 공격의 피해를 한 번 확정
        UpdateAim(aimWorldPosition - (Vector2)transform.position);
        AttackDirection = AimDirection;
        attackMoveInput = player != null ? player.MoveDirection : Vector2.zero;
        ComboIndex = ComboRemaining > 0f && ComboIndex < 2 ? ComboIndex + 1 : 0;
        ComboRemaining = 0f;
        HasBufferedAttack = false;
        float amount = owner.AD * (ComboIndex == 2 ? ThirdHitMultiplier : 1f);
        if (!IsFinite(amount) || amount <= 0f) { ResetCombo(); return false; }
        damage = new DamageInstance(owner, amount, DamageType.Normal, DamageInstance.NextAttackId());

        // 이전 타의 판정·검기는 AS 독립 수명이 끝날 때까지 유지하고 새 행동만 준비
        attackElapsed = 0f;
        openedHitWindow = spawnedEffect = false;
        playedSecondSwingSound = false;
        IsAttacking = true;
        SampleAttack(0f);
        return true;
    }

    /// <summary>대기 중 조준에 원작 어깨 위치와 좌우 반사 행렬 적용</summary>
    public void UpdateAim(Vector2 direction)
    {
        if (IsAttacking || !IsFinite(direction) || direction.sqrMagnitude <= 0.000001f) return;
        AimDirection = direction.normalized;
        float sign = AimDirection.x < 0f ? -1f : 1f;
        WeaponPivot.localPosition = new Vector3(sign * 0.15625f, AimDirection.y > 0f ? 0.5f : 0.375f, 0f);
        WeaponPivot.localScale = new Vector3(1f, sign, 1f);
        WeaponPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);

        // 공격 밖의 대기·이동 중 검은 얼굴을 가리지 않도록 몸체 뒤에 표시
        if (WeaponRenderer != null)
            WeaponRenderer.sortingOrder = bodySortingOrder - 1;
    }

    /// <summary>생성된 공격 판정을 AS와 별개인 원작 수명으로 한 번 준비</summary>
    public void OpenHitWindow()
    {
        if (!IsAttacking || openedHitWindow) return;
        openedHitWindow = true;
        AttackHit hit = hits.Find(value => !value.Box.IsActive);
        if (hit == null) { hit = new AttackHit(); hits.Add(hit); }
        HitBox = hit.Box;
        hit.Remaining = Mathf.Max(0.0001f, HitWindowEnd - HitWindowStart);
        hit.Direction = AttackDirection;
        HitBox.Begin(damage, TargetMask);
    }

    public void CloseHitWindow()
    {
        foreach (AttackHit hit in hits) { hit.Box.End(); hit.Remaining = 0f; }
        HitBox.End();
    }

    /// <summary>전진의 선형 감속을 구간 적분해 Character의 일반 변위에 합산</summary>
    public Vector2 GetAttackDisplacement(float deltaTime)
    {
        if (!attackDashActive || deltaTime <= 0f || AttackDashDuration <= 0f) return Vector2.zero;
        float from = dashElapsed;
        float to = Mathf.Min(AttackDashDuration, from + deltaTime);
        float distance = currentAttackDashSpeed * ((to - from) - (to * to - from * from) / (2f * AttackDashDuration));
        dashElapsed = to;
        if (to >= AttackDashDuration) attackDashActive = false;
        float multiplier = owner.basicSP > 0f ? Mathf.Max(0f, owner.SP / owner.basicSP) : 1f;
        return attackDashDirection * distance * multiplier;
    }

    public void ResetCombo()
    {
        // 진행 중 외부 초기화도 곡선 인덱스와 판정이 남지 않도록 같은 취소 경로 사용
        if (IsAttacking) { CancelAttack(); return; }
        ComboIndex = -1;
        ComboRemaining = 0f;
        HasBufferedAttack = false;
        if (!IsAttacking) ResetWeaponAnimation();
    }

    /// <summary>사망·컷씬·Draw/Slash·씬 복귀에서 공격과 표시·예약·전진 정리</summary>
    public void CancelAttack()
    {
        CloseHitWindow();
        foreach (AttackHit hit in hits) hit.Box.ClearHits();
        HitBox.ClearHits();
        IsAttacking = false;
        attackElapsed = 0f;
        openedHitWindow = spawnedEffect = false;
        attackDashActive = false;
        AttackCooldownRemaining = 0f;
        foreach (AttackEffect effect in effects) { effect.Active = false; if (effect.Renderer != null) effect.Renderer.enabled = false; }
        ResetCombo();
        if (WeaponPivot != null) UpdateAim(AimDirection);
    }

    /// <summary>생성 시 공격 곡선과 검기 표시의 고정 참조 확인</summary>
    private void PrepareWeaponAnimation()
    {
        // Inspector 연결 오류를 부분 표시로 숨기지 않고 최초 준비에서 확인
        if (WeaponPivot == null || WeaponMotion == null || WeaponAnimator == null || WeaponRenderer == null || AttackVfxRenderer == null
            || WeaponMotion.parent != WeaponPivot || WeaponAnimator.transform != WeaponPivot || WeaponAnimator.runtimeAnimatorController == null
            || Attack1Vfx == null || Attack1Vfx.Length != 4 || Attack2Vfx == null || Attack2Vfx.Length != 4 || Attack3Vfx == null || Attack3Vfx.Length != 6)
        { Debug.LogError(name + ": Connect weapon animation and all attack VFX arrays.", this); return; }
        // 원작 SortingGroup의 내부 순서를 우리 몸체의 전역 순서 기준으로 변환
        CharacterRendering appearance = GetComponent<CharacterRendering>();
        if (appearance == null || appearance.BodySprite == null)
        { Debug.LogError(name + ": Connect the Player body sorting reference.", this); return; }
        bodySortingOrder = appearance.BodySprite.sortingOrder;
        WeaponRenderer.sortingLayerID = appearance.BodySprite.sortingLayerID;
        AttackVfxRenderer.sortingLayerID = appearance.BodySprite.sortingLayerID;
        AttackVfxRenderer.sortingOrder = bodySortingOrder + 1;

        // Player 수명 동안 고정된 클립 길이·대기 상태·검기 이미지를 한 번 확보
        foreach (AnimationClip clip in WeaponAnimator.runtimeAnimatorController.animationClips)
            for (int i = 0; i < 3; i++) if (clip.name == "Attack" + (i + 1)) attackClipLengths[i] = clip.length;
        for (int i = 0; i < 3; i++) if (attackClipLengths[i] <= 0f) { Debug.LogError(name + ": Missing attack clip.", this); return; }
        foreach (Sprite[] frames in new[] { Attack1Vfx, Attack2Vfx, Attack3Vfx })
            foreach (Sprite frame in frames) if (frame == null) { Debug.LogError(name + ": Missing attack VFX sprite.", this); return; }
        for (int i = 0; i < WaitStateHashes.Length; i++)
            if (!WeaponAnimator.HasState(0, WaitStateHashes[i])) { Debug.LogError(name + ": Missing combo wait state.", this); return; }
        WeaponAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        WeaponAnimator.applyRootMotion = false;
        weaponAnimationReady = true;
    }

    private void SampleAttack(float time)
    {
        WeaponAnimator.speed = 0f;
        WeaponAnimator.Play(AttackStateHashes[ComboIndex], 0, time / CurrentAttackDuration);
        WeaponAnimator.Update(0f);
        int order = ComboIndex == 0 ? (time / DurationScale >= 11f / 30f ? 1 : -1)
            : ComboIndex == 1 ? (time / DurationScale >= 0.3f ? -1 : 1)
            : (time / DurationScale < 22f / 60f || time / DurationScale >= 27f / 60f ? -1 : 1);
        WeaponRenderer.sortingOrder = bodySortingOrder + order;
    }

    private void SampleWait()
    {
        if (ComboIndex < 0 || ComboIndex > 1) return;
        WeaponAnimator.speed = 0f;
        WeaponAnimator.Play(WaitStateHashes[ComboIndex], 0, 1f - ComboRemaining / Mathf.Max(0.0001f, ComboDuration));
        WeaponAnimator.Update(0f);
        WeaponRenderer.sortingOrder = bodySortingOrder - 1;
    }

    private void FinishAttack(float overshoot)
    {
        IsAttacking = false;
        AttackCooldownRemaining = Mathf.Max(0f, AttackCooldown - overshoot);
        if (ComboIndex < 2)
        {
            // 이미 허용한 클릭은 긴 프레임의 대기시간 초과보다 먼저 다음 타에 전달
            ComboRemaining = HasBufferedAttack ? Mathf.Max(0.000001f, ComboDuration)
                : Mathf.Max(0f, ComboDuration - overshoot / owner.AS);
            if (ComboRemaining > 0f) SampleWait();
            else ResetCombo();
        }
        else
        {
            ComboIndex = -1;
            ComboRemaining = 0f;
            ResetWeaponAnimation();
        }
    }

    /// <summary>원작 Idle 곡선으로 복귀하고 검을 몸체 뒤에 표시</summary>
    private void ResetWeaponAnimation()
    {
        if (!weaponAnimationReady || WeaponAnimator == null || !WeaponAnimator.isActiveAndEnabled) return;
        WeaponAnimator.speed = 1f;
        WeaponAnimator.Play(IdleStateHash, 0, 0f);
        WeaponAnimator.Update(0f);
        if (WeaponRenderer != null) WeaponRenderer.sortingOrder = bodySortingOrder - 1;
    }

    private float GetHitStart()
    {
        float source = ComboIndex == 0 ? 11f / 30f : ComboIndex == 1 ? 7f / 30f : 22f / 60f;
        return source * DurationScale * (HitWindowStart / (11f / 30f));
    }

    private void StartAttackDash()
    {
        // 각 타 시작의 입력으로 방향을 결정하고 이전 타 전진은 새 타격이 시작될 때까지 보존
        bool hasMoveInput = attackMoveInput.sqrMagnitude > 0.000001f;
        attackDashDirection = hasMoveInput ? attackMoveInput.normalized : AttackDirection;
        currentAttackDashSpeed = AttackDashSpeed;
        dashElapsed = 0f;
        attackDashActive = AttackDashDuration > 0f && currentAttackDashSpeed > 0f;
    }

    private AttackEffect CreateEffect()
    {
        var go = new GameObject("AttackVfx" + effects.Count);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = AttackVfxRenderer.sharedMaterial;
        renderer.sortingLayerID = AttackVfxRenderer.sortingLayerID;
        renderer.sortingOrder = bodySortingOrder + 1;
        renderer.enabled = false;
        var effect = new AttackEffect { Renderer = renderer };
        effects.Add(effect);
        return effect;
    }

    private void SpawnEffect(int attack, float age)
    {
        // 겹친 고정 수명 검기는 다른 슬롯을 사용하고 이후 동일 객체 재사용
        AttackEffect effect = effects.Find(value => !value.Active) ?? CreateEffect();
        effect.Attack = attack;
        effect.Age = age;
        effect.Active = true;
        Transform visual = effect.Renderer.transform;
        visual.localPosition = transform.InverseTransformPoint(WeaponPivot.TransformPoint(Vector3.right));
        visual.localRotation = Quaternion.Euler(0f, 0f, WeaponPivot.eulerAngles.z - 90f);
        visual.localScale = new Vector3(AttackDirection.x < 0f ? -1f : 1f, 1f, 1f);
        effect.Renderer.flipX = attack == 1;
        effect.Renderer.flipY = false;
        effect.Renderer.sortingOrder = bodySortingOrder + 1;
        DisplayEffect(effect);
        if (feedback != null) feedback.PlayAttackSound(attack == 1 ? 1 : 0);
    }

    private void AdvanceEffects(float dt)
    {
        // 원작1·2타는AS독립0.2초,새3타는확정모션프레임을따라진행
        foreach (AttackEffect effect in effects)
        {
            if (!effect.Active) continue;
            effect.Age += effect.Attack == 2 ? dt * owner.AS / DurationScale : dt;
            DisplayEffect(effect);
        }
    }

    /// <summary>새 타가 시작돼도 이전 판정의 고정 수명과 공격 방향 유지</summary>
    private void AdvanceHits(float dt)
    {
        for (int i = 0; i < hits.Count; i++)
        {
            AttackHit hit = hits[i];
            if (!hit.Box.IsActive) continue;
            GetHitArea(hit.Direction, out Vector2 center, out float angle);
            hit.Box.Query(center, HitboxSize, angle, hitFeedback);
            if (!hit.Box.IsActive) continue;
            hit.Remaining -= dt;
            if (hit.Remaining <= 0f) hit.Box.End();
        }
    }

    private void DisplayEffect(AttackEffect effect)
    {
        float age = effect.Age;
        int index = effect.Attack < 2 ? Mathf.FloorToInt((age + 0.000001f) * 20f)
            : age < 2f / 60f - 0.000001f ? 0 : age < 3f / 60f - 0.000001f ? 1
            : age < 4f / 60f - 0.000001f ? 2 : age < 7f / 60f - 0.000001f ? 3
            : age < 10f / 60f - 0.000001f ? 4 : age < 13f / 60f - 0.000001f ? 5 : 6;
        Sprite[] frames = effect.Attack == 0 ? Attack1Vfx : effect.Attack == 1 ? Attack2Vfx : Attack3Vfx;
        effect.Active = index >= 0 && index < frames.Length;
        effect.Renderer.enabled = effect.Active;
        effect.Renderer.color = Color.white;
        if (effect.Active) effect.Renderer.sprite = frames[index];
    }

    private void GetHitArea(out Vector2 center, out float angle)
    {
        Vector2 direction = IsAttacking || HitBox.IsActive ? AttackDirection : AimDirection;
        GetHitArea(direction, out center, out angle);
    }

    private void GetHitArea(Vector2 direction, out Vector2 center, out float angle)
    {
        if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f) direction = Vector2.down;
        angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        center = (Vector2)transform.position + direction * HitboxOffset.x + normal * HitboxOffset.y;
    }

    private bool HasValidSettings()
    {
        return weaponAnimationReady && WeaponAnimator != null && WeaponAnimator.isActiveAndEnabled
            && IsFinite(owner.AD) && owner.AD > 0f && IsFinite(owner.AS) && owner.AS > 0f
            && IsFinite(AttackDuration) && AttackDuration > 0f && IsFinite(HitWindowStart) && HitWindowStart >= 0f
            && IsFinite(HitWindowEnd) && HitWindowEnd > HitWindowStart && IsFinite(AttackCooldown) && AttackCooldown >= 0f
            && IsFinite(ComboDuration) && ComboDuration >= 0f && IsFinite(ThirdHitMultiplier) && ThirdHitMultiplier > 0f
            && IsFinite(InputIgnoreDuration) && InputIgnoreDuration >= 0f && IsFinite(ThirdInputBufferTime) && ThirdInputBufferTime >= 0f
            && IsFinite(AttackDashSpeed) && AttackDashSpeed >= 0f && IsFinite(AttackDashDuration) && AttackDashDuration >= 0f
            && IsFinite(AttackMoveMultiplier) && AttackMoveMultiplier >= 0f && AttackMoveMultiplier <= 1f
            && IsFinite(HitboxSize) && HitboxSize.x > 0f && HitboxSize.y > 0f && IsFinite(HitboxOffset);
    }

    private static bool IsFinite(Vector2 value) { return IsFinite(value.x) && IsFinite(value.y); }
    private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    /// <summary>생성 방향을 유지하며 Player 위치만 추종하는 재사용 검기 한 개</summary>
    private class AttackEffect
    {
        public SpriteRenderer Renderer; // 이 슬롯이 재사용할 검기 표시
        public int Attack; // 참조할1·2·3타 Sprite 배열
        public float Age; // 원작 검기는 게임 시간·3타는 모션 시간으로 진행할 표시 경과
        public bool Active; // 현재 표시 중인 슬롯 여부
    }

    /// <summary>각 타의 고정 판정 수명·방향·중복 수신자를 독립 보관</summary>
    private class AttackHit
    {
        public CollisionBox Box = new CollisionBox(); // 이 슬롯이 재사용할 판정 버퍼와 독립 피해
        public Vector2 Direction; // 생성 때 확정해 다음 타의 조준으로 바뀌지 않을 방향
        public float Remaining; // AS와 무관한 판정 잔여 게임 시간
    }
}
