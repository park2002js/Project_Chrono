using UnityEngine;

/// <summary>커서 기준 여섯 방향의 기본·공격 모션과 생명주기·발도 표시</summary>
[DefaultExecutionOrder(3)]
public class CharacterRendering : MonoBehaviour
{
    public SpriteRenderer BodySprite; // 현재 방향·모션 프레임을 표시할 몸체 Sprite
    public PlayerSpriteAnimationSet AnimationSet; // 상태별 Sprite·FPS·개별 프레임 시간을 보관한 단일 모션 원본
    public PlayerSpriteAnimationSet SlashAnimationSet; // 발도 중 뜬 눈·감은 눈 몸체와 손이 그려진 검의 프레임
    public PlayerSpriteAnimationState CurrentState { get; private set; } // 현재 선택한 몸체 모션 상태
    public SpriteRenderer WeaponSprite; // 생명주기·발도 몸체 연출 동안 숨길 고정 검 표시
    public SpriteRenderer ScabbardSprite; // 검과 함께 장착하는 일반 전투용 칼집
    public Sprite SheathedSprite; // 검을 칼집에 넣어 손잡이만 나온 합쳐진 그림
    public Sprite SheatheFlashSprite; // 합쳐지는 순간과 발도 직전에 번쩍일 흰 실루엣
    public ScabbardPose[] ScabbardPoses = // FacingIndex 순서로 둔 캐릭터 기준 왼쪽 옆구리의 칼집 자세
    {
        new ScabbardPose(-0.24f, 0.36f, 250f, false), // 북쪽 공통과 같은 값
        new ScabbardPose(-0.24f, 0.36f, 250f, false), // 북쪽: 몸 뒤, 화면 왼쪽 허리에서 입구가 위
        new ScabbardPose(-0.05f, 0.16f, 325f, true), // 왼쪽 위: 카메라 쪽 허리라 몸과 검 위, 입구가 왼쪽 위
        new ScabbardPose(-0.05f, 0.22f, 215f, false), // 오른쪽 위: 몸 뒤, 입구가 오른쪽 위
        new ScabbardPose(0.05f, 0.16f, 35f, true), // 왼쪽 아래: 카메라 쪽 허리라 몸과 검 위, 입구가 왼쪽 아래
        new ScabbardPose(0.05f, 0.22f, 145f, false), // 오른쪽 아래: 몸 뒤, 입구가 오른쪽 아래
        new ScabbardPose(0.3f, 0.2f, 75f, true), // 남쪽 공통과 같은 값
        new ScabbardPose(0.3f, 0.2f, 75f, true) // 남쪽: 몸과 검 위, 화면 오른쪽 허리에서 입구가 아래
    };
    [Header("Slash 전용 칼·칼집 리그")]
    public Transform SlashRig; // 발도 검집 손을 원점으로 칼·칼집·발도 표시를 묶는 Slash 전용 리그
    public SpriteRenderer SlashKatana; // 칼끝이 입구에 닿기 전까지 칼집과 따로 움직일 칼
    public SpriteRenderer SlashScabbard; // 칼끝이 입구에 닿기 전까지 칼과 따로 움직일 칼집
    public SpriteRenderer SlashBaldo; // 칼끝이 입구에 닿은 뒤 손이 그려진 납도 프레임을 표시할 검
    public AnimationClip SlashSwingClip; // 칼·칼집이 각자 돌다가 리그 원점에서 일직선으로 만나는 Slash 전용 클립
    public Vector2 SlashHoldPoint = new Vector2(-6f, 5.5f); // 몸체 피벗에서 발도 검집 손까지의 원본 px 거리이며 얼굴 왼쪽 눈높이
    [Range(0f, 1f)] public float SlashSwingBlend = 0.3f; // 평소 장착 자세에서 클립 궤도로 완전히 넘어갈 스윙 진행 비율
    public bool IsSlashAnimation { get; private set; } // Skill의 외부 시간으로 스윙·발도·납도 자세를 표시하는 상태
    public bool HasSlashAnimation => HasCompleteSlashAnimation(); // Line 소비 전에 스윙·준비·대기·납도 연결을 확인할 상태
    public Vector3 SlashBladeGlintPosition => SlashBaldo != null && SlashBaldo.sprite != null
        ? SlashBaldo.transform.TransformPoint(Vector3.up * (3f / SlashBaldo.sprite.pixelsPerUnit)) : transform.position; // 검집을 쥔 손에서 칼날 쪽으로 3픽셀 떨어진 검집 입구
    public bool IsLifecycleAnimation { get; private set; } // 일반 이동 모션 대신 생성·사망 모션을 표시하는 상태
    public bool IsAttackAnimation { get; private set; } // 무기의 공격 경과시간으로 몸체 모션을 표시하는 상태
    public float LifecycleElapsed { get; private set; } // 실제 시간으로 진행한 현재 생명주기 모션 경과
    public int FacingIndex = 7; // 북쪽·위 사선·아래 사선·남쪽 순서에 좌0·우1을 더한 방향
    public int FrameIndex; // 현재 Sprite 배열에서 표시한 프레임 번호
    public bool IsMoving; // Character가 전달한 일반 이동 의도에 따른 모션 재생 여부
    public bool IsBackpedaling; // 이동·커서 방향이 반대일 때의 이동 프레임 역재생 여부
    public bool IsDrawHighlighted { get; private set; } // Draw 암전 위로 Player 표시 순서를 올린 상태
    public Color HitFlashColor = Color.red; // 피격 보호 중 원본색과 번갈아 표시할 Sprite 색
    public float HitFlashInterval = 0.08f; // 피격색·원본색 한 단계가 유지될 게임 시간의 초기 튜닝값
    public bool IsHitFlashing { get; private set; } // 피격 보호 사건을 받아 원본색을 보관하고 표시 중인 상태

    private PlayerSpriteAnimationSet.StateData currentStateData; // 상태 진입 때 한 번 확보해 재생할 모션 데이터
    private float stateElapsed; // 현재 기본 모션 안에서 정방향·역방향으로 흐르는 게임 시간
    private float stateDuration; // 상태 진입 때 계산해 재사용할 전체 프레임 유지 시간
    private bool hasState; // 첫 요청 전의 기본 enum 값과 실제 상태 진입을 구분
    private ulong warnedStates; // 37개 표시 상태의 설정 누락 경고를 한 번만 표시할 비트 목록
    private bool warnedMissingSet; // 모션 원본 누락 경고의 반복 출력 방지
    private PlayerCharacter player; // 대시·피격 보호와 조준 상태를 읽을 자기 Player
    private SpriteRenderer[] drawSprites; // Awake에서 확보할 Player의 몸체·무기 표시 목록
    private int[] drawOrders; // Draw 종료 때 돌려놓을 각 Sprite의 진입 전 표시 순서
    private ParticleSystemRenderer[] drawParticles; // 몸체와 함께 암전 위로 올릴 자기 오라의 입자 표시
    private int[] drawParticleOrders; // Draw 종료에서 복구할 각 입자의 원래 표시 순서
    private Color[] hitOriginalColors; // 새 보호 진입 때 보관해 종료·비활성화에서 복원할 Sprite별 원본색
    private SpriteRenderer[] afterimages = new SpriteRenderer[8]; // Player 수명 동안 한 번 만들어 돌려쓸 잔상 표시
    private Vector3[] afterimagePositions = new Vector3[8]; // Player 이동과 분리해 고정할 잔상의 월드 위치
    private float[] afterimageRemaining = new float[8]; // 각 잔상의 남은 게임 시간
    private int nextAfterimage; // 가장 오래된 잔상부터 재사용할 순환 인덱스
    private float afterimageTimer; // 대시 중 다음 잔상을 남길 때까지 남은 게임 시간
    private const float AfterimageInterval = 1f / 30f; // 대시 자세를 남길 표시 간격
    private const float AfterimageLifetime = 0.25f; // 잔상이 투명해질 때까지의 게임 시간
    private bool dashPose; // 대시 시작 때 선택한 자세를 유지하는 상태
    private double lifecycleStart; // 게임이 멈춘 동안 원본 프레임을 표시할 시작 시각
    private bool weaponWasVisible; // 생명주기 종료 때 되돌릴 검 표시 여부
    private bool slashWeaponWasVisible; // 발도 종료 때 되돌릴 별도 검 표시 여부
    private Sprite[] slashSwordFrames; // 발도 시작에 확보하는 검집 손 기준의 납도 프레임
    private SlashSwingStart katanaStart; // Slash 시작 순간 장착 칼의 리그 기준 자세
    private SlashSwingStart scabbardStart; // Slash 시작 순간 장착 칼집의 리그 기준 자세
    private int bodyOrder; // 검·칼집의 앞뒤 순서를 계산할 몸체의 프리팹 표시 순서
    private bool weaponOnLeftSide; // 검날이 조준선을 넘어 칼집이 있는 왼쪽 옆구리로 간 상태
    private bool weaponAimingLeft; // 커서가 Player를 지나는 수직선의 왼쪽에 있는 상태
    private bool scabbardInFront; // 현재 몸 방향의 칼집 자세가 정한 몸 앞 표시 여부
    private KatanaSheatheStage sheatheStage; // 3타 준비에서 검과 칼집을 그리는 단계
    private bool sheatheMerged; // 칼집 자리에 합쳐진 그림을 그리고 검을 숨긴 상태
    private float scabbardPull; // 3타 준비에서 칼집을 끝 쪽으로 당긴 거리
    private Sprite scabbardSprite; // 합쳐진 그림을 거둘 때 되돌릴 칼집 그림
    private static readonly Vector3 ScabbardHipPoint = new Vector3(-0.0625f, 0.3125f, 0f); // 칼집 피벗에서 입구 안쪽 2px 중심선까지의 거리

    /// <summary>Character의 이동·조준 표시와 재사용 효과에 필요한 자기 컴포넌트 준비</summary>
    private void Awake()
    {
        // Player 수명 동안 유지되는 자기 컴포넌트와 표시 목록 확보
        player = GetComponent<PlayerCharacter>();
        SetSlashRigVisible(false, false);
        drawSprites = GetComponentsInChildren<SpriteRenderer>(true);
        drawOrders = new int[drawSprites.Length];
        drawParticles = GetComponentsInChildren<ParticleSystemRenderer>(true);
        drawParticleOrders = new int[drawParticles.Length];
        hitOriginalColors = new Color[drawSprites.Length];

        // 순서가 먼저인 무기의 요청값과 프리팹의 방향으로 검·칼집의 첫 표시 결정
        bodyOrder = BodySprite != null ? BodySprite.sortingOrder : 0;
        scabbardSprite = ScabbardSprite != null ? ScabbardSprite.sprite : null;
        SetWeaponSide(weaponOnLeftSide, weaponAimingLeft);
        ApplyScabbardPose();

        // 짧은 대시마다 생성·제거하지 않고 같은 여덟 표시를 재사용
        for (int i = 0; i < afterimages.Length; i++)
        {
            var image = new GameObject("DashAfterimage" + i).AddComponent<SpriteRenderer>();
            image.transform.SetParent(transform, false);
            image.enabled = false;
            afterimages[i] = image;
        }
    }

    /// <summary>생성 연출이 먼저 진입하지 않았다면 기본 상태의 첫 자세 표시</summary>
    private void Start()
    {
        // 외부 생성 연출이 선택한 상태는 Start에서 덮어쓰지 않고 유지
        if (!hasState) ChangeState(GetLocomotionState());
    }

    /// <summary>Character.Update0 이후 순서3에서 전달된 상태의 몸체 모션과 효과 진행</summary>
    private void Update()
    {
        // 이동 모션 유무와 무관하게 Player가 소유한 보호 경과로 피격색 표시
        ApplyHitFlash();
        UpdateAfterimages();

        // 게임 정지 중에도 생성·사망은 Spawner와 같은 실제 시간으로 진행
        if (IsLifecycleAnimation)
        {
            LifecycleElapsed = Mathf.Clamp((float)(Time.unscaledTimeAsDouble - lifecycleStart), 0f, stateDuration);
            ApplyLifecycleFrame();
            return;
        }

        // 발도 모션은 Skill의 단계 샘플이 갱신하며 일반 이동 모션으로 덮어쓰지 않음
        if (IsSlashAnimation) return;

        // 일반 공격은 AttackController가 검과 같은 경과시간으로 샘플링
        if (IsAttackAnimation) return;

        // 조준이 바뀌어도 대시가 끝날 때까지 이동 시작 방향의 정지 자세 유지
        if (dashPose && player != null && player.IsDashing)
        {
            afterimageTimer -= Time.deltaTime;
            if (Time.deltaTime > 0f && afterimageTimer <= 0f)
            {
                LeaveAfterimage();
                afterimageTimer = AfterimageInterval;
            }
            return;
        }

        // 대시 종료 순간 고정 자세를 풀고 현재 커서 방향으로 복귀
        if (dashPose)
        {
            dashPose = false;
            SetFacing(player != null ? player.AimDirection : Vector2.zero);
        }

        // 상태 전환과 시간 진행을 분리하고 남은 소수 시간을 다음 표시까지 보존
        ChangeState(GetLocomotionState());
        AdvanceLocomotion(Time.deltaTime);
    }

    /// <summary>표시가 활성화될 때 현재 피격 보호와 알림 연결 복구</summary>
    private void OnEnable()
    {
        // 자기 Player가 관리하는 보호 상태의 진입·종료만 구독
        if (player == null) return;
        player.OnHitProtectionChanged += HandleHitProtectionChanged; // 보호 사건을 받아 표시만 갱신
        HandleHitProtectionChanged(player.IsHitProtected);
    }

    /// <summary>표시 종료에서 보호 구독·전용 연출·이전 이동 자세 정리</summary>
    private void OnDisable()
    {
        // 논리 보호는 Player에 남기고 이 표현의 구독·원본색만 정리
        if (player != null) player.OnHitProtectionChanged -= HandleHitProtectionChanged; // 비활성 표현의 상태 알림 해제
        RestoreHitColors();
        IsAttackAnimation = false;
        SetSheathe(KatanaSheatheStage.None, 0f);
        EndSlashAnimation();
        EndLifecycleAnimation();
        ClearAfterimages();

        // 재활성화에서 이전 이동·대시 자세가 남지 않도록 정지 상태 복구
        dashPose = false;
        IsMoving = false;
        IsBackpedaling = false;
        if (AnimationSet != null) ChangeState(GetLocomotionState(), true);
    }

    /// <summary>Draw 중 Player만 암전 위로 표시하고 종료 시 기존 순서 복구</summary>
    public void SetDrawHighlight(bool highlighted)
    {
        // Draw 진입 직전의 표시 순서를 보관하고 종료에서 그대로 복원
        if (IsDrawHighlighted == highlighted) return;
        int bodyOrder = BodySprite != null ? BodySprite.sortingOrder : 0;
        for (int i = 0; i < drawSprites.Length; i++)
        {
            if (drawSprites[i] == null) continue;
            if (highlighted)
            {
                drawOrders[i] = drawSprites[i].sortingOrder;
                drawSprites[i].sortingOrder = 1000 + i;
            }
            else
                drawSprites[i].sortingOrder = drawOrders[i];
        }
        // 오라의 몸 뒤·몸 앞 관계를 유지하며 같은 암전 위로 이동
        int offset = BodySprite != null ? BodySprite.sortingOrder - bodyOrder : 1000;
        for (int i = 0; i < drawParticles.Length; i++)
        {
            if (drawParticles[i] == null) continue;
            if (highlighted)
            {
                drawParticleOrders[i] = drawParticles[i].sortingOrder;
                drawParticles[i].sortingOrder += offset;
            }
            else drawParticles[i].sortingOrder = drawParticleOrders[i];
        }

        // 발끝 기준 정렬 묶음에 들어 있으면 묶음 전체를 암전 위로 올렸다가 되돌림
        if (TryGetComponent(out DepthSorter sorter))
        {
            if (highlighted) sorter.SetOrderOverride(1000);
            else sorter.ClearOrderOverride();
        }
        IsDrawHighlighted = highlighted;
    }

    /// <summary>커서의 북쪽 기준 각도로 여섯 방향의 기본 모션 선택</summary>
    public void SetFacing(Vector2 direction)
    {
        // 전용 연출과 진행 중인 공격·대시는 자기 방향 유지
        if (IsLifecycleAnimation || IsSlashAnimation || IsAttackAnimation || (dashPose && player != null && player.IsDashing)) return;
        SetFacingIndex(direction);

        // 방향이 달라지면 새 상태의 첫 프레임을 요청 즉시 표시
        ChangeState(GetLocomotionState());
    }

    /// <summary>Character가 전달한 일반 이동과 조준으로 걷기·뒷걸음 상태 선택</summary>
    public void SetMovement(Vector2 movement, Vector2 aim)
    {
        // 충돌 결과를 수집하지 않고 이번 일반 이동 의도만 표시 상태에 반영
        IsMoving = movement.sqrMagnitude > 0.0000001f;
        IsBackpedaling = IsMoving && Vector2.Dot(movement, aim) < 0f;

        // 전용 연출이 아니라면 정지·이동 전환의 첫 프레임을 즉시 표시
        if (!IsLifecycleAnimation && !IsSlashAnimation && !IsAttackAnimation && !dashPose) ChangeState(GetLocomotionState());
    }

    /// <summary>무기가 전달한 AS 비적용 게임 경과시간으로 몸체 공격 프레임 표시</summary>
    public void SampleAttackAnimation(Vector2 direction, float elapsed)
    {
        // 생명주기·발도 중에는 이전 공격 요청이 전용 표시를 덮어쓰지 않음
        if (IsLifecycleAnimation || IsSlashAnimation || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        IsAttackAnimation = true;
        SetFacingIndex(direction);
        ChangeState((PlayerSpriteAnimationState)((int)PlayerSpriteAnimationState.AttackLeftBack + FacingIndex));

        // 매 타의0초 요청도 같은 경로로 첫 프레임에 복귀하고 끝에서는 마지막 자세 유지
        stateElapsed = Mathf.Max(0f, elapsed);
        if (currentStateData != null) ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>공격 완료·취소에서 몸체를 현재 방향의 정지·이동 모션으로 복구</summary>
    public void EndAttackAnimation()
    {
        // 생성·사망·발도가 소유한 상태는 공격 종료로 변경하지 않음
        if (!IsAttackAnimation) return;
        IsAttackAnimation = false;
        if (!IsLifecycleAnimation && !IsSlashAnimation && !dashPose) ChangeState(GetLocomotionState(), true);
    }

    /// <summary>3타 준비의 단계와 칼집을 당긴 거리를 받아 검·칼집의 그림과 순서를 갱신</summary>
    public void SetSheathe(KatanaSheatheStage stage, float pull)
    {
        // 표시 준비 전이나 같은 요청의 반복, 전용 연출 중의 준비 요청은 생략
        if (drawSprites == null || ScabbardSprite == null || (stage == sheatheStage && Mathf.Approximately(pull, scabbardPull))) return;
        if (stage != KatanaSheatheStage.None && (IsLifecycleAnimation || IsSlashAnimation)) return;

        // 합쳐진 단계에서는 칼집 자리에 합친 그림이나 흰 실루엣을 그리고 검을 숨김
        bool merged = (stage == KatanaSheatheStage.Flash || stage == KatanaSheatheStage.Sheathed) && SheathedSprite != null && SheatheFlashSprite != null;
        if (merged != sheatheMerged)
        {
            sheatheMerged = merged;
            if (WeaponSprite != null) WeaponSprite.enabled = !merged && ScabbardSprite.enabled;
        }
        ScabbardSprite.sprite = !merged ? scabbardSprite : stage == KatanaSheatheStage.Flash ? SheatheFlashSprite : SheathedSprite;

        // 당긴 거리와 넣는 동안의 검 순서를 현재 방향의 칼집 자세에 반영
        sheatheStage = stage;
        scabbardPull = pull;
        ApplyScabbardPose();
    }

    /// <summary>검이 놓인 옆구리와 조준 쪽을 받아 검의 몸 앞뒤 표시를 갱신</summary>
    public void SetWeaponSide(bool onLeftSide, bool aimingLeft)
    {
        // Awake 전의 요청은 값만 보관하고 표시 목록이 준비된 뒤 적용
        weaponOnLeftSide = onLeftSide;
        weaponAimingLeft = aimingLeft;
        if (drawSprites != null) ApplyEquipmentOrders();
    }

    /// <summary>원본 재import 경계에서 현재 모션의 교체된 프레임과 길이를 다시 확보</summary>
    public void RefreshAnimationFrames()
    {
        // 현재 경과시간은 유지하고 새 모션 데이터와 길이만 재확보
        if (!hasState) return;
        PlayerSpriteAnimationSet set = CurrentState >= PlayerSpriteAnimationState.SlashDraw && CurrentState <= PlayerSpriteAnimationState.SlashSheathe
            ? SlashAnimationSet : AnimationSet;
        currentStateData = set != null ? set.GetState(CurrentState) : null;
        stateDuration = currentStateData != null ? currentStateData.GetTotalDuration() : 0f;
        stateElapsed = stateDuration > 0f ? currentStateData.Loop && !IsAttackAnimation
            ? Mathf.Repeat(stateElapsed, stateDuration) : Mathf.Clamp(stateElapsed, 0f, stateDuration) : 0f;
        if (IsSlashAnimation && HasSlashAnimation)
            slashSwordFrames = SlashAnimationSet.GetState(PlayerSpriteAnimationState.SlashSheathe).Frames;

        // 새 모션의 같은 시점을 표시
        WarnInvalidState();
        if (IsLifecycleAnimation) ApplyLifecycleFrame();
        else if (currentStateData != null) ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>대시 이동 요청 전에 방향별 자세를 고정하고 첫 잔상 표시</summary>
    public void BeginDash(Vector2 direction)
    {
        // 생명주기·발도가 소유한 표시는 대시 요청으로 덮어쓰지 않고 유지
        if (IsLifecycleAnimation || IsSlashAnimation) return;

        // 조준 대신 실제 대시 방향의 독립 자세를 고정하고 첫 잔상에 복사
        SetFacingIndex(direction);
        dashPose = true;
        PlayerSpriteAnimationState first;
        switch (FacingIndex / 2)
        {
            case 0: first = PlayerSpriteAnimationState.DashLeftBack; break;
            case 1: first = PlayerSpriteAnimationState.DashLeftUpper; break;
            case 2: first = PlayerSpriteAnimationState.DashLeftLower; break;
            default: first = PlayerSpriteAnimationState.DashLeftFront; break;
        }
        ChangeState((PlayerSpriteAnimationState)((int)first + FacingIndex % 2), true);
        afterimageTimer = AfterimageInterval;
        LeaveAfterimage();
    }

    /// <summary>사망 또는 생성의 첫 자세를 표시하고 첫 자세를 유지하는 시간을 더한 전체 재생 시간 반환</summary>
    public float BeginLifecycleAnimation(bool dead, float firstFrameHold = 0f)
    {
        // 생명주기 표시가 시작되면 3타 준비의 합쳐진 그림과 이전 발도의 검 숨김 상태부터 복구
        SetSheathe(KatanaSheatheStage.None, 0f);
        EndSlashAnimation();

        // 전용 자세에서는 평소 무기를 숨기고 실제 시간으로 재생
        IsAttackAnimation = false;
        if (!IsLifecycleAnimation && WeaponSprite != null) weaponWasVisible = WeaponSprite.enabled;
        IsLifecycleAnimation = true;
        LifecycleElapsed = 0f;
        firstFrameHold = Mathf.Max(0f, firstFrameHold);
        lifecycleStart = Time.unscaledTimeAsDouble + firstFrameHold;
        dashPose = false;
        ClearAfterimages();
        SetWeaponVisible(false);
        ChangeState(dead ? PlayerSpriteAnimationState.Death : PlayerSpriteAnimationState.Spawn, true);
        return stateDuration + firstFrameHold;
    }

    /// <summary>중단·완료에서 생성·사망 표시를 끝내고 기존 검·정지 모션 복구</summary>
    public void EndLifecycleAnimation()
    {
        // 생성·사망의 종료 여부는 Spawner가 결정하고 표시는 정지 상태로 복귀
        if (!IsLifecycleAnimation) return;
        IsLifecycleAnimation = false;
        SetWeaponVisible(weaponWasVisible);
        IsMoving = false;
        IsBackpedaling = false;
        ChangeState(GetLocomotionState(), true);
    }

    /// <summary>뜬 눈 몸체로 전환하고 장착 칼·칼집의 자세를 스윙 시작점으로 보관</summary>
    public bool BeginSlashAnimation()
    {
        // 전체 스윙·준비·대기·납도 데이터가 연결된 경우에만 전용 표시 진입
        if (IsLifecycleAnimation || !HasSlashAnimation) return false;

        // 리그 원점을 얼굴 왼쪽의 검집 손 위치에 두고 숨기기 전의 장착 자세를 리그 기준으로 보관
        SetSheathe(KatanaSheatheStage.None, 0f);
        bool wasSlashing = IsSlashAnimation;
        SlashRig.SetPositionAndRotation(BodySprite.transform.TransformPoint((Vector3)SlashHoldPoint / BodySprite.sprite.pixelsPerUnit), Quaternion.identity);
        if (!wasSlashing)
        {
            katanaStart = CaptureSwingStart(WeaponSprite);
            scabbardStart = CaptureSwingStart(ScabbardSprite);
        }

        // 기존 방향·무기 표시를 보관하고 스윙의 첫 자세 표시
        IsAttackAnimation = false;
        if (!wasSlashing && WeaponSprite != null) slashWeaponWasVisible = WeaponSprite.enabled;
        IsSlashAnimation = true;
        dashPose = false;
        IsMoving = false;
        IsBackpedaling = false;
        ClearAfterimages();
        SetWeaponVisible(false);
        ChangeState(PlayerSpriteAnimationState.SlashDraw, true);
        slashSwordFrames = SlashAnimationSet.GetState(PlayerSpriteAnimationState.SlashSheathe).Frames;
        SlashBaldo.sprite = slashSwordFrames[0];
        SlashBaldo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        SampleSlashSwing(0f, 0f);
        return true;
    }

    /// <summary>칼과 칼집이 장착 자세에서 출발해 클립 궤도로 각자 돌다가 칼끝이 입구에 닿는 진행률 표시</summary>
    public void SampleSlashSwing(float eyeElapsed, float progress)
    {
        // 몸은 뜬 눈 모션을 반복
        if (!IsSlashAnimation) return;
        ApplySlashEyes(false, eyeElapsed);

        // 클립을 먼저 평가하고 스윙 초반에는 Slash 시작 순간의 장착 자세에서 이어지게 섞음
        progress = Mathf.Clamp01(progress);
        SlashSwingClip.SampleAnimation(SlashRig.gameObject, progress * SlashSwingClip.length);
        float startWeight = SlashSwingBlend > 0f ? 1f - Mathf.SmoothStep(0f, 1f, progress / SlashSwingBlend) : 0f;
        BlendSwingStart(SlashKatana, katanaStart, startWeight);
        BlendSwingStart(SlashScabbard, scabbardStart, startWeight);
        SetSlashRigVisible(true, false);
    }

    /// <summary>칼끝이 입구에 닿은 뒤 눈 모션을 반복하며 검집 손을 고정한 채 납도 진행률 표시</summary>
    public void SampleSlashPose(bool eyesClosed, float eyeElapsed, float sheatheProgress)
    {
        // 몸은 해당 눈 모션의 첫 장부터 반복하고 무기는 닫힘 시점에만 마지막 장 표시
        if (!IsSlashAnimation) return;
        ApplySlashEyes(eyesClosed, eyeElapsed);
        int last = slashSwordFrames.Length - 1;
        SlashBaldo.sprite = slashSwordFrames[Mathf.FloorToInt(Mathf.Clamp01(sheatheProgress) * last)];
        SetSlashRigVisible(false, true);
    }

    /// <summary>발도 완료·취소에서 Slash 리그를 숨기고 현재 방향의 일반 몸체 표시 복구</summary>
    public void EndSlashAnimation()
    {
        // 자기 발도가 숨긴 검만 복구하고 새 입력이 일반 모션을 선택하도록 준비
        if (!IsSlashAnimation) return;
        IsSlashAnimation = false;
        SetSlashRigVisible(false, false);
        slashSwordFrames = null;
        SetWeaponVisible(slashWeaponWasVisible);
        IsMoving = false;
        IsBackpedaling = false;
        ChangeState(GetLocomotionState(), true);
    }

    /// <summary>발도 전용 상태의 Sprite 누락을 소비 전 시작 경계에서 확인</summary>
    private bool HasCompleteSlashAnimation()
    {
        // 스윙 리그와 세 구간이 모두 있어야 모션이 중간에 끊기거나 기존 자세로 대체되지 않음
        if (SlashAnimationSet == null || BodySprite == null || BodySprite.sprite == null || SlashRig == null || SlashSwingClip == null || SlashSwingClip.length <= 0f
            || SlashKatana == null || SlashScabbard == null || SlashBaldo == null || SlashKatana.transform.parent != SlashRig
            || SlashScabbard.transform.parent != SlashRig || SlashBaldo.transform.parent != SlashRig) return false;
        for (int state = (int)PlayerSpriteAnimationState.SlashDraw; state <= (int)PlayerSpriteAnimationState.SlashSheathe; state++)
        {
            PlayerSpriteAnimationSet.StateData data = SlashAnimationSet.GetState((PlayerSpriteAnimationState)state);
            if (data == null || data.Frames == null || data.Frames.Length == 0) return false;
            for (int i = 0; i < data.Frames.Length; i++)
                if (data.Frames[i] == null) return false;
        }
        return true;
    }

    /// <summary>북쪽0도에서30·90·150도 경계로 원본 종류와 좌우 방향 선택</summary>
    private void SetFacingIndex(Vector2 direction)
    {
        // 중심이나 잘못된 좌표는 마지막 유효 방향 유지
        if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.y)
            || direction.sqrMagnitude <= 0.000001f) return;
        float angle = Vector2.Angle(Vector2.up, direction);
        int band = angle < 30f ? 0 : angle < 90f ? 1 : angle < 150f ? 2 : 3;
        FacingIndex = band * 2 + (band == 0 || band == 3 || direction.x >= 0f ? 1 : 0);
        ApplyScabbardPose();
    }

    /// <summary>몸체가 향하는 방향에서 캐릭터의 왼쪽 옆구리에 칼집을 기울여 배치</summary>
    private void ApplyScabbardPose()
    {
        // 설정이 없는 방향에서는 프리팹에 둔 자세 유지
        if (ScabbardSprite == null || ScabbardPoses == null || FacingIndex < 0 || FacingIndex >= ScabbardPoses.Length) return;

        // 입구에서 끝으로 가는 축을 돌린 뒤 축 위의 허리 지점을 옆구리에 맞춤
        ScabbardPose pose = ScabbardPoses[FacingIndex];
        Quaternion rotation = Quaternion.Euler(0f, 0f, pose.Angle - 90f);
        Transform scabbard = ScabbardSprite.transform;
        scabbard.localRotation = rotation;
        scabbard.localPosition = (Vector3)pose.Hip - rotation * ScabbardHipPoint + rotation * Vector3.up * scabbardPull;
        scabbardInFront = pose.InFront;
        ApplyEquipmentOrders();
    }

    /// <summary>검의 몸 앞뒤를 정하고 몸의 같은 쪽에서는 검을 칼집 위에 표시</summary>
    private void ApplyEquipmentOrders()
    {
        // 오른손 쪽 대기 자세는 왼쪽 조준에서 몸 뒤, 넘어간 왼쪽 옆구리는 왼쪽 조준이거나 칼집이 몸 앞이면 몸 앞
        bool weaponBehindBody = weaponOnLeftSide ? !weaponAimingLeft && !scabbardInFront : weaponAimingLeft;

        // 뒤에서부터 칼집·검·몸·칼집·검 순서이며 칼집에 밀어 넣는 검은 칼집 바로 밑
        int scabbardOrder = bodyOrder + (scabbardInFront ? 2 : -2);
        SetEquipmentOrder(ScabbardSprite, scabbardOrder);
        SetEquipmentOrder(WeaponSprite, sheatheStage == KatanaSheatheStage.Insert ? scabbardOrder - 1 : bodyOrder + (weaponBehindBody ? -1 : 3));
    }

    /// <summary>검·칼집의 표시 순서를 바꾸되 Draw 강조 중에는 종료 때 복구할 값에 기록</summary>
    private void SetEquipmentOrder(SpriteRenderer sprite, int order)
    {
        // 암전 위로 올린 순서는 유지하고 복구 목록만 새 순서로 교체
        if (sprite == null) return;
        int index = IsDrawHighlighted ? System.Array.IndexOf(drawSprites, sprite) : -1;
        if (index >= 0) drawOrders[index] = order;
        else sprite.sortingOrder = order;
    }

    /// <summary>현재 각도 구간과 이동 여부에 맞는 기본 모션 상태 선택</summary>
    private PlayerSpriteAnimationState GetLocomotionState()
    {
        // 기존 enum 값과 저장된 생명주기·발도 상태를 유지하며 새 사선 상태만 분기
        FacingIndex = Mathf.Clamp(FacingIndex, 0, 7);
        PlayerSpriteAnimationState first;
        switch (FacingIndex / 2)
        {
            case 0: first = IsMoving ? PlayerSpriteAnimationState.MoveLeftBack : PlayerSpriteAnimationState.IdleLeftBack; break;
            case 1: first = IsMoving ? PlayerSpriteAnimationState.MoveLeftUpper : PlayerSpriteAnimationState.IdleLeftUpper; break;
            case 2: first = IsMoving ? PlayerSpriteAnimationState.MoveLeftLower : PlayerSpriteAnimationState.IdleLeftLower; break;
            default: first = IsMoving ? PlayerSpriteAnimationState.MoveLeftFront : PlayerSpriteAnimationState.IdleLeftFront; break;
        }
        return (PlayerSpriteAnimationState)((int)first + FacingIndex % 2);
    }

    /// <summary>상태 진입에서만 데이터를 조회하고 첫 프레임부터 표시</summary>
    private void ChangeState(PlayerSpriteAnimationState state, bool restart = false)
    {
        // 같은 상태의 매 프레임 요청에서는 데이터 검색과 시간 초기화 생략
        if (!restart && hasState && CurrentState == state) return;
        hasState = true;
        CurrentState = state;
        stateElapsed = 0f;
        FrameIndex = 0;

        // 상태 데이터와 전체 길이는 진입 경계에서 한 번 확보
        PlayerSpriteAnimationSet set = state >= PlayerSpriteAnimationState.SlashDraw && state <= PlayerSpriteAnimationState.SlashSheathe
            ? SlashAnimationSet : AnimationSet;
        currentStateData = set != null ? set.GetState(state) : null;
        stateDuration = currentStateData != null ? currentStateData.GetTotalDuration() : 0f;
        WarnInvalidState();
        ApplyFrame(0);
    }

    /// <summary>게임 시간의 남은 소수 구간을 보존하며 전진·뒷걸음 프레임 샘플링</summary>
    private void AdvanceLocomotion(float deltaTime)
    {
        // 데이터 누락이나 정지 시간에는 현재 자세 유지
        if (currentStateData == null || stateDuration <= 0f || deltaTime <= 0f) return;

        // 원작 MOVE의4FPS에 현재 이동속도를 곱하고 뒷걸음만 역재생
        float direction = IsMoving && IsBackpedaling ? -1f : 1f;
        float speed = IsMoving && player != null ? Mathf.Max(0f, player.SP) : 1f;
        stateElapsed += deltaTime * direction * speed;
        stateElapsed = currentStateData.Loop
            ? Mathf.Repeat(stateElapsed, stateDuration) : Mathf.Clamp(stateElapsed, 0f, stateDuration);
        ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>검을 장착하거나 전용 연출에서 검과 칼집을 함께 숨김</summary>
    private void SetWeaponVisible(bool visible)
    {
        // 두 장비의 연출 진입·복귀를 같은 프레임에 반영
        if (WeaponSprite != null) WeaponSprite.enabled = visible;
        if (ScabbardSprite != null) ScabbardSprite.enabled = visible;
    }

    /// <summary>Slash 몸체의 뜬 눈·감은 눈 모션을 외부 경과시간으로 반복 표시</summary>
    private void ApplySlashEyes(bool eyesClosed, float eyeElapsed)
    {
        // 상태가 바뀔 때만 데이터를 다시 확보하고 같은 시간축으로 반복
        ChangeState(eyesClosed ? PlayerSpriteAnimationState.SlashHold : PlayerSpriteAnimationState.SlashDraw);
        stateElapsed = Mathf.Repeat(eyeElapsed, stateDuration);
        ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>장착 표시의 화면 위치·각도·반전을 리그 기준으로 보관하고 숨겨진 장비는 클립 자세만 쓰게 표시</summary>
    private SlashSwingStart CaptureSwingStart(SpriteRenderer equipment)
    {
        // 부모의 음수 배율까지 반영한 축으로 각도와 좌우 반전을 계산
        if (equipment == null || !equipment.enabled) return default;
        Transform source = equipment.transform;
        Vector3 up = SlashRig.InverseTransformDirection(source.TransformDirection(Vector3.up));
        Vector3 right = SlashRig.InverseTransformDirection(source.TransformDirection(Vector3.right));
        return new SlashSwingStart
        {
            Valid = true,
            Position = SlashRig.InverseTransformPoint(source.position),
            Angle = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg,
            Mirrored = (right.x * up.y - right.y * up.x < 0f) != equipment.flipX
        };
    }

    /// <summary>클립이 정한 자세에 시작 자세를 비율만큼 섞고 반전은 절반을 넘을 때까지만 유지</summary>
    private static void BlendSwingStart(SpriteRenderer target, SlashSwingStart start, float weight)
    {
        // 장착 자세가 없으면 클립 자세 그대로 사용
        Transform pose = target.transform;
        if (!start.Valid || weight <= 0f)
        {
            target.flipX = false;
            return;
        }
        float angle = Mathf.LerpAngle(pose.localEulerAngles.z, start.Angle, weight);
        pose.SetLocalPositionAndRotation(Vector3.Lerp(pose.localPosition, start.Position, weight), Quaternion.Euler(0f, 0f, angle));
        target.flipX = start.Mirrored && weight > 0.5f;
    }

    /// <summary>스윙 중에는 칼·칼집을, 칼끝이 닿은 뒤에는 손이 그려진 발도 검을 표시</summary>
    private void SetSlashRigVisible(bool swing, bool baldo)
    {
        // 리그 연결이 빠진 경우에도 남은 표시만 정리
        if (SlashKatana != null) SlashKatana.enabled = swing;
        if (SlashScabbard != null) SlashScabbard.enabled = swing;
        if (SlashBaldo != null) SlashBaldo.enabled = baldo;
    }

    /// <summary>원본 Aseprite 프레임 시간으로 생성·사망 표시</summary>
    private void ApplyLifecycleFrame()
    {
        // 마지막 프레임은 반복하지 않고 Spawner의 종료 요청까지 유지
        if (currentStateData == null || stateDuration <= 0f) return;
        ApplyFrame(currentStateData.GetFrameIndex(LifecycleElapsed));
    }

    /// <summary>선택한 모션의 Sprite와 반전을 적용하고 누락 프레임은 기존 표시 유지</summary>
    private void ApplyFrame(int index)
    {
        // 프레임 관찰값은 현재 데이터의 유효 범위 안으로 제한
        if (currentStateData == null || currentStateData.Frames == null || currentStateData.Frames.Length == 0) return;
        FrameIndex = Mathf.Clamp(index, 0, currentStateData.Frames.Length - 1);
        if (BodySprite == null) return;

        // 누락 Sprite 때문에 몸체 전체가 사라지지 않도록 마지막 유효 표시 보존
        Sprite sprite = currentStateData.Frames[FrameIndex];
        if (sprite != null) BodySprite.sprite = sprite;
        BodySprite.flipX = currentStateData.FlipX;
    }

    /// <summary>상태 진입에서 잘못된 데이터를 한 번 알리고 안전한 표시·시간값 사용</summary>
    private void WarnInvalidState()
    {
        // 모션 원본 자체의 누락은 상태 수와 무관하게 한 번 안내
        PlayerSpriteAnimationSet set = CurrentState >= PlayerSpriteAnimationState.SlashDraw && CurrentState <= PlayerSpriteAnimationState.SlashSheathe
            ? SlashAnimationSet : AnimationSet;
        if (set == null)
        {
            if (!warnedMissingSet) Debug.LogWarning("Player AnimationSet이 없어 기존 몸체 Sprite 유지", this);
            warnedMissingSet = true;
            return;
        }

        // 상태별 누락 프레임이나 시간 설정을 진입 시 검사하고 중복 경고 방지
        ulong stateBit = 1UL << (int)CurrentState;
        if ((warnedStates & stateBit) != 0) return;
        bool missingFrames = currentStateData == null || currentStateData.Frames == null || currentStateData.Frames.Length == 0;
        bool invalidTime = false;
        if (!missingFrames)
        {
            for (int i = 0; i < currentStateData.Frames.Length; i++)
                if (currentStateData.Frames[i] == null) missingFrames = true;
            invalidTime = !(currentStateData.FramesPerSecond > 0f) || float.IsInfinity(currentStateData.FramesPerSecond);
            float[] durations = currentStateData.FrameDurations;
            if (durations != null && durations.Length > 0)
            {
                invalidTime |= durations.Length != currentStateData.Frames.Length;
                for (int i = 0; i < durations.Length; i++)
                    invalidTime |= !(durations[i] > 0f) || float.IsInfinity(durations[i]);
            }
        }
        if (!missingFrames && !invalidTime) return;
        warnedStates |= stateBit;
        Debug.LogWarning("Player AnimationSet의 " + CurrentState + " 설정 확인 필요: 누락 Sprite는 기존 표시 유지, 유효하지 않은 시간은 FPS 간격으로 대체", this);
    }

    /// <summary>현재 대시 자세·방향·월드 위치를 가장 오래된 잔상에 복사</summary>
    private void LeaveAfterimage()
    {
        // 표시 원본이 없으면 잔상 생성만 생략하고 대시 논리는 그대로 유지
        if (BodySprite == null) return;

        // 가장 오래된 표시를 현재 자세로 덮어써 별도 생성 없이 재사용
        SpriteRenderer image = afterimages[nextAfterimage];
        image.sprite = BodySprite.sprite;
        image.sharedMaterial = BodySprite.sharedMaterial;
        image.flipX = BodySprite.flipX;
        image.flipY = BodySprite.flipY;
        image.sortingLayerID = BodySprite.sortingLayerID;
        image.sortingOrder = BodySprite.sortingOrder - 1;
        image.transform.rotation = BodySprite.transform.rotation;
        image.transform.localScale = BodySprite.transform.localScale;
        afterimagePositions[nextAfterimage] = BodySprite.transform.position;
        image.transform.position = afterimagePositions[nextAfterimage];
        image.color = new Color(0.55f, 0.85f, 1f, 0.6f);
        image.enabled = true;
        afterimageRemaining[nextAfterimage] = AfterimageLifetime;
        nextAfterimage = (nextAfterimage + 1) % afterimages.Length;
    }

    /// <summary>고정 월드 위치에서 잔상을 감쇠하고 만료 표시만 비활성화</summary>
    private void UpdateAfterimages()
    {
        // 게임 정지 중에는 남은 수명과 색을 유지
        for (int i = 0; i < afterimages.Length; i++)
        {
            if (afterimageRemaining[i] <= 0f) continue;
            afterimageRemaining[i] = Mathf.Max(0f, afterimageRemaining[i] - Time.deltaTime);
            SpriteRenderer image = afterimages[i];
            image.transform.position = afterimagePositions[i];
            image.enabled = afterimageRemaining[i] > 0f;
            image.color = new Color(0.55f, 0.85f, 1f, 0.6f * afterimageRemaining[i] / AfterimageLifetime);
        }
    }

    /// <summary>재배치·비활성화에서 이전 위치의 잔상 제거</summary>
    private void ClearAfterimages()
    {
        // 다음 대시에서 같은 표시 객체를 재사용할 수 있도록 수명만 초기화
        for (int i = 0; i < afterimages.Length; i++)
        {
            afterimageRemaining[i] = 0f;
            if (afterimages[i] != null) afterimages[i].enabled = false;
        }
    }

    /// <summary>보호 진입·종료 알림에서 자기 원본색과 표시 상태만 준비</summary>
    private void HandleHitProtectionChanged(bool active)
    {
        // 보호 종료 사건에서 저장한 원본색으로 복구
        if (!active)
        {
            RestoreHitColors();
            return;
        }

        // 이미 깜빡이는 색을 원본으로 다시 저장하지 않고 새 진입에서만 보관
        if (!IsHitFlashing)
            for (int i = 0; i < drawSprites.Length; i++)
                if (drawSprites[i] != null) hitOriginalColors[i] = drawSprites[i].color;
        IsHitFlashing = true;
        ApplyHitFlash();
    }

    /// <summary>독립 보호 타이머를 만들지 않고 Player 경과에 맞춰 빨강·원본색 교대</summary>
    private void ApplyHitFlash()
    {
        // 보호의 실제 경과로 단계만 선택하고 추가 타이머 없이 원본 알파 유지
        if (!IsHitFlashing || player == null) return;
        bool red = Mathf.FloorToInt(player.HitProtectionElapsed / Mathf.Max(0.01f, HitFlashInterval)) % 2 == 0;
        for (int i = 0; i < drawSprites.Length; i++)
        {
            if (drawSprites[i] == null) continue;
            Color color = red ? HitFlashColor : hitOriginalColors[i];
            color.a = hitOriginalColors[i].a;
            if (drawSprites[i].color != color) drawSprites[i].color = color;
        }
    }

    /// <summary>피격 표시를 끝내고 각 Sprite가 진입 전에 사용하던 색 복원</summary>
    private void RestoreHitColors()
    {
        // 깜빡임 진입 이전의 Sprite별 색으로 복원
        if (!IsHitFlashing) return;
        IsHitFlashing = false;
        for (int i = 0; i < drawSprites.Length; i++)
            if (drawSprites[i] != null) drawSprites[i].color = hitOriginalColors[i];
    }
}

/// <summary>Slash 시작 순간 장착 칼 또는 칼집의 리그 기준 자세</summary>
public struct SlashSwingStart
{
    public bool Valid; // 시작 순간 장비가 보여 섞을 자세가 있는지 여부
    public Vector3 Position; // 리그 원점 기준 피벗 위치
    public float Angle; // 스프라이트 위쪽이 향하는 화면 각도
    public bool Mirrored; // 부모 음수 배율이나 flipX로 좌우가 뒤집힌 상태
}

/// <summary>몸체 한 방향에서 칼집이 걸리는 허리 지점·기울기·몸 앞뒤</summary>
[System.Serializable]
public struct ScabbardPose
{
    public Vector2 Hip; // 발 기준으로 칼집의 허리 지점이 놓일 위치
    public float Angle; // 칼집 입구에서 끝을 향하는 방향의 화면 각도
    public bool InFront; // 카메라 쪽 허리에 걸려 몸체 앞에 그릴지 여부

    /// <summary>한 방향의 칼집 자세를 설정</summary>
    public ScabbardPose(float hipX, float hipY, float angle, bool inFront)
    {
        // Inspector에 표시할 방향별 기본값 지정
        Hip = new Vector2(hipX, hipY);
        Angle = angle;
        InFront = inFront;
    }
}

/// <summary>3타 준비에서 검과 칼집을 그리는 단계</summary>
public enum KatanaSheatheStage
{
    None, // 검과 칼집을 따로 그리는 평소 상태
    Insert, // 검을 칼집 바로 밑에 그려 넣은 부분을 가리는 상태
    Flash, // 칼집 자리에 흰 실루엣을 그리는 상태
    Sheathed // 칼집 자리에 검이 들어간 합쳐진 그림을 그리는 상태
}
