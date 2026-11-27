using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기본 공격 클립의 검·FX 표시와 입력 버퍼·콤보·독립 타격 판정 관리</summary>
[DefaultExecutionOrder(-110)]
public class AttackController : MonoBehaviour
{
    public Transform WeaponPivot; // 조준 방향과 어깨 위치를 적용할 부모
    public Transform WeaponMotion; // 공격 곡선이 움직일 검 피벗
    public Animator WeaponAnimator; // 카타나와 검기의 같은 공격 시간축을 평가할 Animator
    public SpriteRenderer WeaponRenderer; // 공격 클립이 움직일 검 이미지
    public SpriteRenderer AttackVfxRenderer; // 공격 클립이 Sprite·표시·위치·회전을 제어할 검기
    public Vector2 AimDirection = Vector2.up; // 북쪽 저작 자세를 기준으로 한 마지막 유효 조준 방향
    public KatanaStrike[] Strikes = // 1·2·3타의 몸·판정 개시·잠금 해제 프레임, 전진 속력·시간, 피해 배율, 판정 중심 거리·반경·사각 크기
    {
        new KatanaStrike(3, 4, 20, 4f, 0.24f, 1f, 0.25f, 2f, Vector2.zero),
        new KatanaStrike(7, 8, 24, 4f, 0.24f, 0.75f, 0.75f, 2.125f, Vector2.zero),
        new KatanaStrike(25, 26, 46, 5f, 0.30f, 1.85f, 2f, 0f, new Vector2(4.5f, 3.75f))
    };
    public float HitDuration = 9f / 60f; // AS와 독립된 판정 수명
    public float AttackCooldown; // 공격 사이에 추가할 간격
    public float ComboDuration = 20f / 60f; // 각 타 종료 후 검의 마지막 자세를 유지할 게임 시간
    public float AttackMoveMultiplier; // 공격 중 일반 입력 이동을0으로 차단할 배율
    public float InputIgnoreDuration = 1f / 60f; // 1·2타에서 다음 클릭 예약을 열기까지의 시간
    public Vector2 ShoulderOffset = new Vector2(0.15625f, 0.375f); // 발에서 어깨까지 거리이며 왼쪽 조준에서 x 반사
    public float InitialHitHeight = -0.0625f; // 첫 질의 뒤 어깨 높이로 따라붙기 전의 발 기준 높이
    public float HoldSide = 0.3125f; // 대기 때 손잡이를 둘 캐릭터 오른쪽 옆구리의 몸 중심에서의 거리
    public float HoldHeight = 0.1875f; // 대기 때 손잡이를 둘 발 기준 허리 높이
    public float HoldDepth = 0.0625f; // 옆을 볼 때 카메라 쪽 허리는 낮게, 반대편 허리는 높게 옮길 거리
    public KatanaSheathe Sheathe = new KatanaSheathe(4, 10, 3, 2, 0.1875f, 3, 6); // 3타의 납도 준비와 발도 직전 점멸의 프레임·거리
    public LayerMask TargetMask = ~0; // 타격 Cast에서 조회할 레이어
    public int ComboIndex { get; private set; } = -1; // 현재0·1·2타이며 콤보가 없으면-1
    public float ComboRemaining { get; private set; } // AS 비적용 콤보 유효 잔여시간
    public float AttackCooldownRemaining { get; private set; } // 추가 간격을 설정한 경우에만 남는 AS 적용 대기
    public bool IsAttacking { get; private set; } // 준비부터 회복까지 무기가 수행 중인 공격
    public bool HasBufferedAttack { get; private set; } // 허용 구간에서 받은 클릭 한 번의 다음 공격 예약
    public float CurrentAttackDuration => Strikes[Mathf.Clamp(ComboIndex, 0, 2)].LockFrame / 60f; // 걷기 잠금이 풀리고 다음 타를 실행할 수 있는 현재 타의 길이
    public bool IsAttackInputAllowed => ComboIndex != 2 && (!IsAttacking || attackElapsed + TimeTolerance >= InputIgnoreDuration); // 1·2타만 다음 공격 예약 허용
    public Vector2 AttackDirection { get; private set; } = Vector2.up; // 공격 시작 때 고정한 조준 방향
    public CollisionBox HitBox { get; private set; } = new CollisionBox(); // 가장 최근 생성된 공격 판정과 적중 대상 조회

    private Character owner; // 한 번 확보할 공격력·공격속도 원본
    private PlayerCharacter player; // 전진을 시작할 때 이동 입력을 읽을 대상
    private CharacterRendering rendering; // Player 수명 동안 검과 같은 시간으로 샘플링할 몸체 표시
    private DamageInstance damage; // 이번 공격에서 한 번 확정한 피해와 식별자
    private float attackElapsed; // 현재 공격의 AS 적용 경과시간
    private float attackBodyElapsed; // 몸체 공격 시작 이후 AS를 적용하지 않은 게임 경과시간
    private float previousBodySampleTime; // 몸체 시작 경계를 나눌 직전 검 클립의 경과시간
    private bool openedHitWindow; // 한 타에 판정·스윙음을 한 번만 생성할 표시
    private bool trailing; // 잠금이 풀린 뒤에도 남은 검기와 끝 자세를 클립 끝까지 이어 표시하는 상태
    private PlayerFeedback feedback; // 자기 수명 동안 재사용할 공격 소리·적중 섬광 담당
    private Action<Vector2, float> hitFeedback; // 매 질의에서 만들지 않고 재사용할 성공 타격 콜백
    private float dashElapsed; // 별도 강제 전진의 게임 경과시간
    private Vector2 attackDashDirection; // 전진 시작 때 고정한 입력 또는 조준 방향
    private float advanceSpeed; // 첫 전진 틱의 속력
    private float advanceDeceleration; // 전진 속력이 초당 감소할 양
    private float advanceDuration; // 이번 전진의 감속 시간
    private bool weaponAnimationReady; // Animator·렌더러·검기 참조의 최초 확인 결과
    private Vector3 weaponPivotPosition; // 프리팹에서 조절한 북쪽 조준 어깨 기준 위치
    private Vector3 weaponPivotScale; // 프리팹에서 조절한 어깨 크기
    private Vector3 weaponHoldOffset; // 프리팹의 대기 자세에서 어깨로부터 손잡이까지의 거리
    private Vector3 sheatheStartPosition; // 3타를 시작한 순간 검이 있던 월드 위치
    private Quaternion sheatheStartRotation = Quaternion.identity; // 3타를 시작한 순간 검의 월드 회전
    private Quaternion AimRotation => Quaternion.Euler(0f, 0f, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg - 90f); // 어깨의 +Y가 조준 방향을 향하는 회전
    private readonly float[] attackClipLengths = new float[3]; // 최초 한 번 확보할 각 공격의 원본 길이
    private readonly List<AttackHit> hits = new List<AttackHit>(3); // 배속 중 겹친 판정의 수명·방향·대상을 독립 유지할 풀
    private static readonly int[] AttackStateHashes = { Animator.StringToHash("Base Layer.Attack1"), Animator.StringToHash("Base Layer.Attack2"), Animator.StringToHash("Base Layer.Attack3") }; // 공격 상태 이름의 반복 변환 방지
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle"); // 대기 검 자세
    private const float TimeTolerance = 0.000001f; // 60Hz 경계에서 단정밀도 누적 오차 허용

    /// <summary>생성 시 고정 참조·기본 공격 클립·독립 판정 준비</summary>
    private void Awake()
    {
        // 자기 캐릭터와 직렬화 연결만 확인하며 외부 Instance를 조회하지 않음
        owner = GetComponent<Character>();
        player = owner as PlayerCharacter;
        rendering = GetComponent<CharacterRendering>();
        feedback = GetComponent<PlayerFeedback>();
        if (feedback != null) hitFeedback = feedback.PlayImpact;
        if (owner == null) { enabled = false; Debug.LogError(name + ": AttackController requires Character.", this); return; }
        PrepareWeaponAnimation();
        if (!weaponAnimationReady) return;
        hits.Add(new AttackHit { Box = HitBox });
        hits.Add(new AttackHit());
        hits.Add(new AttackHit());

        // 프리팹의 계층과 위치를 유지해 편집한 클립과 실행 결과를 같은 기준으로 평가
        ResetWeaponAnimation();
    }

    /// <summary>비활성 중 평가할 수 없었던 Animator의 공격 정지 상태를 대기 곡선으로 복구</summary>
    private void OnEnable()
    {
        if (weaponAnimationReady) ResetWeaponAnimation();
    }

    /// <summary>이번 입력을 받기 전에 검사할 공격·예약·회복 경계를 먼저 갱신</summary>
    private void Update()
    {
        // 정지 중에는 표시·공격·콤보의 게임 시간을 모두 보존
        float dt = Time.deltaTime;
        if (!weaponAnimationReady || Time.timeScale <= 0f || dt <= 0f) return;
        if (!IsFinite(owner.AS) || owner.AS <= 0f) { CancelAttack(); return; }

        // 이미 생성한 판정은 공격 클립 배속과 별개로0.15초 동안만 진행
        AdvanceHits(dt);

        // 마지막 검 자세에서 20F가 지나면 3타도 같은 경로로 대기 복귀
        if (!IsAttacking)
        {
            AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - dt * owner.AS);
            if (ComboRemaining > 0f)
            {
                // 잠금이 풀린 뒤에도 남은 검기를 공격 방향에 둔 채 클립 끝까지 재생
                if (trailing)
                {
                    attackElapsed = Mathf.Min(attackClipLengths[ComboIndex], attackElapsed + dt * owner.AS);
                    SampleWeapon(attackElapsed);
                    trailing = attackElapsed + TimeTolerance < attackClipLengths[ComboIndex];
                }
                ComboRemaining = Mathf.Max(0f, ComboRemaining - dt);
                if (ComboRemaining <= TimeTolerance) ResetCombo();
            }
            return;
        }

        // 한 경과시간으로 검 곡선과 생성 이벤트를 평가해 높은 FPS에서도 같은 시점 유지
        int attackId = damage.AttackId;
        attackElapsed += dt * owner.AS;
        float start = Strikes[ComboIndex].HitFrame / 60f;
        if (!openedHitWindow && attackElapsed + TimeTolerance >= start)
        {
            OpenHitWindow(Mathf.Max(0f, (attackElapsed - start) / owner.AS));
            StartAttackDash();
            if (feedback != null) feedback.PlayAttackStrike(ComboIndex);
            QueryHit(HitBox, ComboIndex, AttackDirection, InitialHitHeight);
            if (!IsAttacking || damage.AttackId != attackId) return;
        }
        bool finished = attackElapsed + TimeTolerance >= CurrentAttackDuration;
        SampleAttack(finished ? CurrentAttackDuration : attackElapsed);
        if (finished) FinishAttack(Mathf.Max(0f, attackElapsed - CurrentAttackDuration));
    }

    private void OnDisable() { CancelAttack(); }

    /// <summary>새 클릭을 시작하거나 허용 구간의 클릭 한 번을 다음 공격으로 예약</summary>
    internal bool ApplyAttack(Vector2 aimWorldPosition)
    {
        // 첫 프레임 이후의 1·2타 클릭은 버튼을 떼어도 다음 타로 한 번 예약
        if (!HasValidSettings() || !IsFinite(aimWorldPosition)) return false;
        if (IsAttacking)
        {
            if (!IsAttackInputAllowed) return false;
            HasBufferedAttack = true;
            return true;
        }
        if (AttackCooldownRemaining > TimeTolerance || ComboIndex == 2) return false;

        // 남은 검기 표시를 끝내고 최신 조준으로 다음 타를 결정해 한 공격의 피해를 한 번 확정
        trailing = false;
        UpdateAim(aimWorldPosition - (Vector2)transform.position);
        AttackDirection = AimDirection;
        ComboIndex = ComboRemaining > 0f && ComboIndex < 2 ? ComboIndex + 1 : 0;
        ComboRemaining = 0f;
        HasBufferedAttack = false;
        float amount = owner.AD * Strikes[ComboIndex].DamageMultiplier;
        if (!IsFinite(amount) || amount <= 0f) { ResetCombo(); return false; }
        damage = new DamageInstance(owner, amount, DamageType.Normal, DamageInstance.NextAttackId());

        // 이전 타의 판정은 독립 수명을 유지하고 검·FX는 새 공격 클립에서 함께 시작
        attackElapsed = 0f;
        attackBodyElapsed = previousBodySampleTime = 0f;
        openedHitWindow = false;
        IsAttacking = true;
        if (ComboIndex == 2) CaptureSheatheStart();
        SampleAttack(0f);

        // 1·2타 스윙음과 3타 납도음은 최고점이 판정·칼집 합침 프레임에 오도록 시작 순간 재생
        if (feedback != null) feedback.PlayAttackStart(ComboIndex);
        return true;
    }

    /// <summary>프리팹의 북쪽 조준 자세를 현재 방향으로 돌리며 검은 좌우로 뒤집지 않음</summary>
    public void UpdateAim(Vector2 direction)
    {
        if (!weaponAnimationReady || IsAttacking || trailing || !IsFinite(direction) || direction.sqrMagnitude <= 0.000001f) return;
        AimDirection = direction.normalized;
        WeaponPivot.rotation = AimRotation;

        // 대기는 손잡이를 옆구리에, 타 사이의 마지막 자세는 공격 클립의 어깨에 둠
        ApplyPivotPosition(ComboIndex < 0 ? 1f : 0f);
        ApplyWeaponSide();
    }

    /// <summary>한 타의 총 전진 거리를 선형 감속하며 게임 프레임에 나눠 적용</summary>
    public Vector2 GetAttackDisplacement(float deltaTime)
    {
        // 전진 첫 틱에는 이번 프레임의 입력이 전달된 뒤 방향을 확정
        if (deltaTime <= 0f || dashElapsed >= advanceDuration) return Vector2.zero;
        if (dashElapsed == 0f && player != null && player.MoveDirection.sqrMagnitude > TimeTolerance)
            attackDashDirection = player.MoveDirection.normalized;

        // 기본 60Hz에서 1·2타는 14회 0.51204유닛, 3타는 17회 0.78704유닛 이동
        float from = dashElapsed;
        dashElapsed = Mathf.Min(advanceDuration, dashElapsed + deltaTime);
        float distance = PlayerBodyController.GetDeceleratingDistance(from, dashElapsed, advanceSpeed, advanceDeceleration);
        float multiplier = owner.basicSP > 0f ? Mathf.Max(0f, owner.SP / owner.basicSP) : 1f;
        return attackDashDirection * distance * multiplier;
    }

    /// <summary>콤보·예약·마지막 검 자세를 기본 대기로 초기화</summary>
    public void ResetCombo()
    {
        // 진행 중 외부 초기화도 곡선 인덱스와 판정이 남지 않도록 같은 취소 경로 사용
        if (IsAttacking) { CancelAttack(); return; }
        trailing = false;
        ComboIndex = -1;
        ComboRemaining = 0f;
        HasBufferedAttack = false;
        ResetWeaponAnimation();
    }

    /// <summary>사망·컷씬·Draw/Slash·씬 복귀에서 공격과 표시·예약·전진 정리</summary>
    public void CancelAttack()
    {
        // 피해 이벤트로 중단되더라도 모든 독립 판정과 수신 기록을 정리
        foreach (AttackHit hit in hits) { hit.Box.End(); hit.Box.ClearHits(); }
        HitBox.End();
        HitBox.ClearHits();
        IsAttacking = false;
        if (rendering != null)
        {
            rendering.EndAttackAnimation();
            rendering.SetSheathe(KatanaSheatheStage.None, 0f);
        }
        attackElapsed = 0f;
        openedHitWindow = false;
        advanceDuration = 0f;
        AttackCooldownRemaining = 0f;
        ResetCombo();
        if (AttackVfxRenderer != null) AttackVfxRenderer.enabled = false;
        if (WeaponPivot != null) UpdateAim(AimDirection);
    }

    /// <summary>생성 시 공격 곡선과 검기 표시의 고정 참조 확인</summary>
    private void PrepareWeaponAnimation()
    {
        // Inspector 연결 오류를 부분 표시로 숨기지 않고 최초 준비에서 확인
        if (WeaponPivot == null || WeaponMotion == null || WeaponAnimator == null || WeaponRenderer == null || AttackVfxRenderer == null
            || WeaponMotion.parent != WeaponPivot || WeaponAnimator.transform != WeaponPivot || WeaponAnimator.runtimeAnimatorController == null
            || !AttackVfxRenderer.transform.IsChildOf(WeaponPivot))
        { Debug.LogError(name + ": Connect the weapon Animator and its weapon/FX children.", this); return; }
        weaponPivotPosition = WeaponPivot.localPosition;
        weaponPivotScale = WeaponPivot.localScale;
        weaponHoldOffset = WeaponMotion.localPosition;

        // 클립은 잠금이 풀린 뒤 남는 검기까지 담으므로 각 타의 잠금 프레임보다 길 수 있음
        foreach (AnimationClip clip in WeaponAnimator.runtimeAnimatorController.animationClips)
            for (int i = 0; i < 3; i++) if (clip.name == "Attack" + (i + 1)) attackClipLengths[i] = clip.length;
        for (int i = 0; i < 3; i++) if (attackClipLengths[i] <= 0f) { Debug.LogError(name + ": Missing attack clip.", this); return; }
        WeaponAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        WeaponAnimator.applyRootMotion = false;
        weaponAnimationReady = true;
    }

    /// <summary>검·FX 클립과 원작 시작 시점 이후의 몸체 공격을 각 시간 기준으로 평가</summary>
    private void SampleAttack(float time)
    {
        // 검과 FX는 기존 AS 적용 경과시간으로 같은 클립을 평가
        SampleWeapon(time);

        // 시작 경계를 통과한 프레임 분량만 게임 시간으로 환산해 몸체의20FPS 유지
        float bodyStart = Strikes[ComboIndex].BodyFrame / 60f;
        float bodyDelta = Mathf.Max(0f, time - Mathf.Max(previousBodySampleTime, bodyStart));
        attackBodyElapsed += bodyDelta / owner.AS;
        previousBodySampleTime = time;
        if (time + TimeTolerance >= bodyStart && rendering != null) rendering.SampleAttackAnimation(AttackDirection, attackBodyElapsed);
    }

    /// <summary>검과 검기 클립을 지정한 경과시간으로 평가하고 어깨나 칼집 정렬 적용</summary>
    private void SampleWeapon(float time)
    {
        WeaponAnimator.speed = 0f;
        WeaponAnimator.Play(AttackStateHashes[ComboIndex], 0, time / attackClipLengths[ComboIndex]);
        WeaponAnimator.Update(0f);
        ApplyWeaponPose(time);
    }

    /// <summary>클립이 평가한 검 자세에 조준 방향의 어깨나 3타 준비의 칼집 정렬을 적용</summary>
    private void ApplyWeaponPose(float time)
    {
        // 3타는 발도 프레임 전까지 칼집에 맞추고 그 밖에는 조준 방향의 어깨 기준으로 복귀
        if (ComboIndex == 2 && rendering != null && rendering.ScabbardSprite != null && time + TimeTolerance < Strikes[2].HitFrame / 60f)
            ApplySheathe(time);
        else
        {
            if (rendering != null) rendering.SetSheathe(KatanaSheatheStage.None, 0f);
            WeaponPivot.rotation = AimRotation;
            ApplyPivotPosition(GetHoldWeight(time));
        }
        ApplyWeaponSide();
    }

    /// <summary>3타를 시작한 순간의 검 자세를 칼집까지 이어 갈 출발점으로 보관</summary>
    private void CaptureSheatheStart()
    {
        sheatheStartPosition = WeaponMotion.position;
        sheatheStartRotation = WeaponMotion.rotation;
    }

    /// <summary>3타 준비 동안 칼집을 당겼다 밀고 검을 칼집 축에 맞춰 넣으며 합쳐지는 순간과 발도 직전에 점멸</summary>
    private void ApplySheathe(float time)
    {
        // 칼집은 끝 쪽으로 당겼다가 검끝을 받으러 제자리로 밀어 냄
        float frame = time * 60f + 0.001f;
        float pull = frame < Sheathe.PullFrame ? Mathf.SmoothStep(0f, 1f, frame / Mathf.Max(1, Sheathe.PullFrame))
            : 1f - Mathf.SmoothStep(0f, 1f, (frame - Sheathe.PullFrame) / Mathf.Max(1, Sheathe.PushFrame - Sheathe.PullFrame));

        // 넣는 동안은 검을 칼집 밑에, 합쳐지는 순간과 발도 직전은 흰 실루엣, 그 사이는 합쳐진 그림
        bool flash = frame < Sheathe.MergeFrame + Sheathe.MergeFlashFrames || frame >= Strikes[2].HitFrame - Sheathe.DrawFlashFrames;
        KatanaSheatheStage stage = frame < Sheathe.AlignFrame ? KatanaSheatheStage.None
            : frame < Sheathe.MergeFrame ? KatanaSheatheStage.Insert
            : flash ? KatanaSheatheStage.Flash : KatanaSheatheStage.Sheathed;
        rendering.SetSheathe(stage, Sheathe.ScabbardPull * pull);

        // 클립은 칼집 기준으로 넣는 거리만 움직이고 시작 자세에서 칼집 축까지는 월드에서 이어 붙임
        Transform scabbard = rendering.ScabbardSprite.transform;
        float weight = Mathf.SmoothStep(0f, 1f, frame / Mathf.Max(1, Sheathe.AlignFrame));
        Quaternion rotation = Quaternion.Slerp(sheatheStartRotation, scabbard.rotation * WeaponMotion.localRotation, weight);
        Vector3 position = Vector3.Lerp(sheatheStartPosition, scabbard.TransformPoint(WeaponMotion.localPosition), weight);

        // 검이 그 월드 자세에 오도록 어깨를 역산
        WeaponPivot.rotation = rotation * Quaternion.Inverse(WeaponMotion.localRotation);
        WeaponPivot.position += position - WeaponMotion.position;
    }

    /// <summary>대기에서 시작한 1타가 판정 직전까지 옆구리에서 어깨로 옮겨 갈 비율</summary>
    private float GetHoldWeight(float time)
    {
        // 판정 1F 전부터는 공격 클립의 어깨 기준 궤도를 그대로 사용
        float windup = ComboIndex == 0 ? (Strikes[0].HitFrame - 1) / 60f : 0f;
        return windup > 0f ? 1f - Mathf.SmoothStep(0f, 1f, time / windup) : 0f;
    }

    /// <summary>공격 클립의 어깨 위치와 손잡이를 옆구리에 두는 대기 위치를 비율로 섞어 어깨 배치</summary>
    private void ApplyPivotPosition(float holdWeight)
    {
        // 공격 클립의 어깨는 조준 쪽으로 나가 있고 커서가 발보다 위면 기준 높이, 아니면 2px 아래
        float sign = AimDirection.x < 0f ? -1f : 1f;
        Vector3 shoulder = new Vector3(sign * weaponPivotPosition.x,
            weaponPivotPosition.y - (AimDirection.y > 0f ? 0f : 0.125f), weaponPivotPosition.z);

        // 조준 방향의 오른쪽이 캐릭터의 오른쪽 옆구리이며 대기 자세의 손잡이 거리만큼 어깨를 되돌림
        Vector3 hip = new Vector3(HoldSide * AimDirection.y, HoldHeight - HoldDepth * AimDirection.x, shoulder.z);
        Vector3 grip = WeaponPivot.localRotation * Vector3.Scale(weaponPivotScale, weaponHoldOffset);
        WeaponPivot.localPosition = Vector3.Lerp(shoulder, hip - grip, holdWeight);
    }

    /// <summary>검이 놓인 옆구리와 조준 쪽을 표시 담당에 전달해 검의 몸 앞뒤 결정</summary>
    private void ApplyWeaponSide()
    {
        // 검날이 조준선의 왼쪽이면 휘둘러 넘어간 왼쪽 옆구리, 아니면 검을 쥔 오른쪽 옆구리
        if (rendering == null) return;
        bool onLeftSide = Mathf.Sin(WeaponMotion.localEulerAngles.z * Mathf.Deg2Rad) > 0f;

        // 커서가 수직선 위에 정확히 있으면 오른쪽 조준으로 취급
        rendering.SetWeaponSide(onLeftSide, AimDirection.x < -0.0001f);
    }

    /// <summary>걷기와 몸 공격 자세를 풀고 검은 마지막 자세에서 다음 입력 대기</summary>
    private void FinishAttack(float overshoot)
    {
        // 막타 뒤에는 대기가 끝나야 새 1타를 허용
        IsAttacking = false;
        trailing = attackElapsed + TimeTolerance < attackClipLengths[ComboIndex];
        if (rendering != null) rendering.EndAttackAnimation();
        AttackCooldownRemaining = Mathf.Max(0f, AttackCooldown - overshoot);
        ComboRemaining = HasBufferedAttack ? Mathf.Max(TimeTolerance, ComboDuration)
            : Mathf.Max(0f, ComboDuration - overshoot / owner.AS);
        if (ComboRemaining <= 0f) ResetCombo();
    }

    /// <summary>기본 Idle 클립의 검 자세와 FX 숨김 상태로 복귀</summary>
    private void ResetWeaponAnimation()
    {
        if (!weaponAnimationReady || WeaponAnimator == null || !WeaponAnimator.isActiveAndEnabled) return;
        WeaponAnimator.speed = 1f;
        WeaponAnimator.Play(IdleStateHash, 0, 0f);
        WeaponAnimator.Update(0f);
        ApplyWeaponSide();
    }

    /// <summary>이번 타의 판정 수명·방향·피해를 별도 슬롯에 보관</summary>
    private void OpenHitWindow(float elapsedSinceStart)
    {
        // 다음 타의 조준이나 재생 속도 변화와 무관하게 같은 수신자는 한 번만 타격
        openedHitWindow = true;
        AttackHit hit = hits.Find(value => !value.Box.IsActive);
        if (hit == null) { hit = new AttackHit(); hits.Add(hit); }
        HitBox = hit.Box;
        hit.Remaining = Mathf.Max(0f, HitDuration - elapsedSinceStart);
        hit.Direction = AttackDirection;
        hit.Strike = ComboIndex;
        HitBox.Begin(damage, TargetMask);
    }

    /// <summary>타격 순간의 방향 입력과 타별 거리로 감속 전진 시작</summary>
    private void StartAttackDash()
    {
        // 만료에 닿는 틱은 속력이 0이므로 만료 직전까지의 완전한 틱만 이동
        KatanaStrike strike = Strikes[ComboIndex];
        attackDashDirection = AttackDirection;
        advanceDuration = Mathf.Max(0, Mathf.CeilToInt(strike.AdvanceDuration * 60f) - 1) / 60f;
        advanceSpeed = strike.AdvanceSpeed;
        advanceDeceleration = strike.AdvanceSpeed / strike.AdvanceDuration;
        dashElapsed = 0f;
    }

    /// <summary>새 타가 시작돼도 이전 판정의 고정 수명과 공격 방향 유지</summary>
    private void AdvanceHits(float dt)
    {
        for (int i = 0; i < hits.Count; i++)
        {
            AttackHit hit = hits[i];
            if (!hit.Box.IsActive) continue;
            hit.Remaining -= dt;
            if (hit.Remaining <= TimeTolerance) hit.Box.End();
            else QueryHit(hit.Box, hit.Strike, hit.Direction, ShoulderOffset.y);
        }
    }

    /// <summary>현재 발 원점에 어깨와 타별 전방 거리를 더해 검기 크기에 맞춘 원 또는 사각 판정 검사</summary>
    private void QueryHit(CollisionBox box, int strikeIndex, Vector2 direction, float height)
    {
        // 1타의 오른쪽 수평 조준 전방 끝은 발에서 0.15625 + 0.25 + 2유닛
        KatanaStrike strike = Strikes[strikeIndex];
        float x = direction.x < 0f ? -ShoulderOffset.x : ShoulderOffset.x;
        Vector2 center = (Vector2)transform.position + new Vector2(x, height) + direction * strike.HitForward;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 사각 크기가 있으면 x를 조준 방향 길이, y를 폭으로 쓰고 없으면 반경의 원
        if (strike.HitBox.x > 0f && strike.HitBox.y > 0f) box.QueryBox(center, strike.HitBox, angle, hitFeedback);
        else box.QueryCircle(center, strike.HitRadius, angle, hitFeedback);
    }

    /// <summary>세 타의 시간과 기하·피해·속도 설정이 유효한지 확인</summary>
    private bool HasValidSettings()
    {
        // 각 클립 안에 몸과 판정 이벤트가 있고 전진을 계산할 수 있는지 확인
        if (Strikes == null || Strikes.Length != attackClipLengths.Length) return false;
        for (int i = 0; i < Strikes.Length; i++)
        {
            KatanaStrike strike = Strikes[i];
            if (strike.BodyFrame < 0 || strike.HitFrame < 0 || Mathf.Max(strike.BodyFrame, strike.HitFrame) > strike.LockFrame
                || strike.LockFrame / 60f > attackClipLengths[i] + TimeTolerance
                || !IsFinite(strike.AdvanceSpeed) || strike.AdvanceSpeed < 0f || !IsFinite(strike.AdvanceDuration) || strike.AdvanceDuration <= 0f
                || !IsFinite(strike.DamageMultiplier) || strike.DamageMultiplier <= 0f || !IsFinite(strike.HitForward) || !IsFinite(strike.HitBox)
                || !IsFinite(strike.HitRadius) || (strike.HitRadius <= 0f && (strike.HitBox.x <= 0f || strike.HitBox.y <= 0f))) return false;
        }
        return weaponAnimationReady && WeaponAnimator != null && WeaponAnimator.isActiveAndEnabled
            && IsFinite(owner.AD) && owner.AD > 0f && IsFinite(owner.AS) && owner.AS > 0f
            && IsFinite(HitDuration) && HitDuration > 0f && IsFinite(AttackCooldown) && AttackCooldown >= 0f
            && IsFinite(ComboDuration) && ComboDuration >= 0f
            && IsFinite(InputIgnoreDuration) && InputIgnoreDuration >= 0f
            && IsFinite(AttackMoveMultiplier) && AttackMoveMultiplier >= 0f && AttackMoveMultiplier <= 1f
            && IsFinite(ShoulderOffset) && IsFinite(InitialHitHeight)
            && IsFinite(HoldSide) && IsFinite(HoldHeight) && IsFinite(HoldDepth) && IsFinite(Sheathe.ScabbardPull);
    }

    /// <summary>조준 위치와 중심 오프셋 두 성분이 유한한지 확인</summary>
    private static bool IsFinite(Vector2 value) { return IsFinite(value.x) && IsFinite(value.y); }
    /// <summary>게임 수치가 유한한지 확인</summary>
    private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    /// <summary>각 타의 고정 판정 수명·방향·중복 수신자를 독립 보관</summary>
    private class AttackHit
    {
        public CollisionBox Box = new CollisionBox(); // 이 슬롯이 재사용할 판정 버퍼와 독립 피해
        public Vector2 Direction; // 생성 때 확정해 다음 타의 조준으로 바뀌지 않을 방향
        public int Strike; // 생성 때 확정해 판정 모양과 크기를 고를 타 번호
        public float Remaining; // AS와 무관한 판정 잔여 게임 시간
    }
}

/// <summary>카타나 한 타의 60Hz 이벤트와 평지 감속 전진 설정</summary>
[Serializable]
public struct KatanaStrike
{
    public int BodyFrame; // 몸 공격 자세를 시작할 프레임
    public int HitFrame; // 원형 판정·공격음·강제 전진을 시작할 프레임
    public int LockFrame; // 걷기 잠금을 풀고 다음 타를 실행할 수 있는 프레임
    public float AdvanceSpeed; // 첫 전진 틱에서 초당 이동할 거리
    public float AdvanceDuration; // 전진 속력을 0까지 줄이는 게임 시간
    public float DamageMultiplier; // 이 타 한 번의 피해에 곱할 배율
    public float HitForward; // 어깨에서 조준 방향으로 판정 중심까지의 거리
    public float HitRadius; // 원형 판정의 반경이며 사각 판정을 쓰는 타는 0
    public Vector2 HitBox; // 사각 판정의 조준 방향 길이(x)와 폭(y)이며 원형 판정을 쓰는 타는 0

    /// <summary>카타나 한 타의 이벤트·잠금 프레임과 전진·피해 배율·판정 모양을 설정</summary>
    public KatanaStrike(int bodyFrame, int hitFrame, int lockFrame, float advanceSpeed, float advanceDuration,
        float damageMultiplier, float hitForward, float hitRadius, Vector2 hitBox)
    {
        // Inspector에 표시할 기본값을 공격별로 지정
        BodyFrame = bodyFrame;
        HitFrame = hitFrame;
        LockFrame = lockFrame;
        AdvanceSpeed = advanceSpeed;
        AdvanceDuration = advanceDuration;
        DamageMultiplier = damageMultiplier;
        HitForward = hitForward;
        HitRadius = hitRadius;
        HitBox = hitBox;
    }
}

/// <summary>3타의 납도 준비와 발도 직전 점멸의 60Hz 프레임·거리 설정</summary>
[Serializable]
public struct KatanaSheathe
{
    public int AlignFrame; // 검끝이 칼집 입구에 닿아 칼집 축에 정렬되는 프레임
    public int MergeFrame; // 밀어 넣던 검이 칼집과 합쳐지며 흰빛이 되는 프레임
    public int MergeFlashFrames; // 합쳐질 때 흰 실루엣을 유지할 프레임 수
    public int DrawFlashFrames; // 발도 직전에 흰 실루엣으로 점멸할 프레임 수
    public float ScabbardPull; // 준비 시작에 칼집을 끝 쪽으로 당길 거리
    public int PullFrame; // 칼집을 가장 뒤로 당긴 프레임
    public int PushFrame; // 당긴 칼집을 제자리로 밀어 검끝을 받는 프레임

    /// <summary>3타 준비의 단계별 프레임과 칼집을 당길 거리를 설정</summary>
    public KatanaSheathe(int alignFrame, int mergeFrame, int mergeFlashFrames, int drawFlashFrames, float scabbardPull, int pullFrame, int pushFrame)
    {
        // Inspector에 표시할 기본값 지정
        AlignFrame = alignFrame;
        MergeFrame = mergeFrame;
        MergeFlashFrames = mergeFlashFrames;
        DrawFlashFrames = drawFlashFrames;
        ScabbardPull = scabbardPull;
        PullFrame = pullFrame;
        PushFrame = pushFrame;
    }
}
