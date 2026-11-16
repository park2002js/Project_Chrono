using UnityEngine;

/// <summary>커서 기준 4방향 이동과 생명주기·외부 시간에 맞춘 발도 모션 표시</summary>
[DefaultExecutionOrder(3)]
public class CharacterRendering : MonoBehaviour
{
    public SpriteRenderer BodySprite; // 현재 방향·모션 프레임을 표시할 몸체 Sprite
    public Transform FacingMarker; // 최종 아트 없이 현재 바라보는 사분면을 확인할 임시 표시
    public PlayerSpriteAnimationSet AnimationSet; // 상태별 Sprite·FPS·개별 프레임 시간을 보관한 단일 모션 원본
    public PlayerSpriteAnimationSet SlashAnimationSet; // 선택된 R02 발도 원본의 준비·대기·납도 프레임 데이터
    public PlayerSpriteAnimationState CurrentState { get; private set; } // 현재 선택한 몸체 모션 상태
    public SpriteRenderer WeaponSprite; // 생명주기·발도 몸체 연출 동안 숨길 고정 검 표시
    public bool IsSlashAnimation { get; private set; } // Skill의 외부 시간으로 발도·대기·납도 자세를 표시하는 상태
    public bool HasSlashAnimation => HasCompleteSlashAnimation(); // Line 소비 전에 준비·대기·납도 프레임 연결을 확인할 상태
    public Vector3 SlashBladeGlintPosition => BodySprite != null ? BodySprite.transform.TransformPoint(new Vector3(-0.21875f, 0.71875f, 0f)) : transform.position; // R02 대기 프레임의 노출 칼날 중앙 월드 위치
    public bool IsLifecycleAnimation { get; private set; } // 일반 이동 모션 대신 생성·사망 모션을 표시하는 상태
    public bool IsDeathAnimation { get; private set; } // 현재 생명주기 모션이 사망인지 구분
    public float LifecycleElapsed { get; private set; } // 실제 시간으로 진행한 현재 생명주기 모션 경과
    public int ActiveAfterimages { get; private set; } // 현재 남아 있는 대시 잔상 수
    public int FacingIndex = 1; // 왼앞0·오앞1·왼뒤2·오뒤3 중 현재 표시할 방향
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
    private int warnedStates; // 설정 누락 경고를 상태별로 한 번만 표시할 비트 목록
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
    private float lifecycleDuration; // Spawner가 정한 생명주기 모션의 실제 시간
    private bool weaponWasVisible; // 생명주기 종료 때 되돌릴 검 표시 여부
    private bool slashWeaponWasVisible; // 발도 종료 때 되돌릴 별도 검 표시 여부

    /// <summary>Character의 이동·조준 표시와 재사용 효과에 필요한 자기 컴포넌트 준비</summary>
    private void Awake()
    {
        // Player 수명 동안 유지되는 자기 컴포넌트와 표시 목록 확보
        player = GetComponent<PlayerCharacter>();
        drawSprites = GetComponentsInChildren<SpriteRenderer>(true);
        drawOrders = new int[drawSprites.Length];
        drawParticles = GetComponentsInChildren<ParticleSystemRenderer>(true);
        drawParticleOrders = new int[drawParticles.Length];
        hitOriginalColors = new Color[drawSprites.Length];

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
            LifecycleElapsed = Mathf.Min(lifecycleDuration, LifecycleElapsed + Time.unscaledDeltaTime);
            ApplyLifecycleFrame();
            return;
        }

        // 발도 모션은 Skill의 단계 샘플이 갱신하며 일반 이동 모션으로 덮어쓰지 않음
        if (IsSlashAnimation) return;

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
        IsDrawHighlighted = highlighted;
    }

    /// <summary>커서 위치로 사분면을 선택하고 축 위에서는 이전 방향 유지</summary>
    public void SetFacing(Vector2 direction)
    {
        // 생명주기·발도·진행 중인 대시는 자기 전용 방향 유지
        if (IsLifecycleAnimation || IsSlashAnimation || (dashPose && player != null && player.IsDashing)) return;

        // 커서 방향을 좌우·앞뒤로 나눠 표시 방향 결정
        bool right = Mathf.Abs(direction.x) < 0.0001f ? FacingIndex % 2 == 1 : direction.x > 0f;
        bool back = Mathf.Abs(direction.y) < 0.0001f ? FacingIndex >= 2 : direction.y > 0f;
        FacingIndex = (back ? 2 : 0) + (right ? 1 : 0);

        // 최종 Sprite가 없어도 방향을 확인할 수 있도록 임시 표시 이동
        if (FacingMarker != null)
            FacingMarker.localPosition = new Vector3(right ? 0.22f : -0.22f, back ? 0.3f : -0.3f, 0f);

        // 사분면이 달라지면 새 상태의 첫 프레임을 요청 즉시 표시
        ChangeState(GetLocomotionState());
    }

    /// <summary>Character가 전달한 일반 이동과 조준으로 걷기·뒷걸음 상태 선택</summary>
    public void SetMovement(Vector2 movement, Vector2 aim)
    {
        // 충돌 결과를 수집하지 않고 이번 일반 이동 의도만 표시 상태에 반영
        IsMoving = movement.sqrMagnitude > 0.0000001f;
        IsBackpedaling = IsMoving && Vector2.Dot(movement, aim) < 0f;

        // 전용 연출이 아니라면 정지·이동 전환의 첫 프레임을 즉시 표시
        if (!IsLifecycleAnimation && !IsSlashAnimation && !dashPose) ChangeState(GetLocomotionState());
    }

    /// <summary>대시 이동 요청 전에 방향별 자세를 고정하고 첫 잔상 표시</summary>
    public void BeginDash(Vector2 direction)
    {
        // 생명주기·발도가 소유한 표시는 대시 요청으로 덮어쓰지 않고 유지
        if (IsLifecycleAnimation || IsSlashAnimation) return;

        // 대시 시작 방향의 상태로 전환하고 현재 자세를 첫 잔상에 복사
        dashPose = false;
        SetFacing(direction);
        dashPose = true;
        ChangeState((PlayerSpriteAnimationState)((int)PlayerSpriteAnimationState.DashLeftFront + FacingIndex), true);
        afterimageTimer = AfterimageInterval;
        LeaveAfterimage();
    }

    /// <summary>Spawner가 정한 기간으로 사망 또는 생성의 첫 자세부터 표시</summary>
    public void BeginLifecycleAnimation(bool dead, float duration)
    {
        // 생명주기 표시가 시작되면 이전 발도의 검 숨김 상태부터 복구
        EndSlashAnimation();

        // 기존 검 표시를 보관하고 Spawner의 실제 시간에 맞춰 전용 상태 진입
        if (!IsLifecycleAnimation && WeaponSprite != null) weaponWasVisible = WeaponSprite.enabled;
        IsLifecycleAnimation = true;
        IsDeathAnimation = dead;
        LifecycleElapsed = 0f;
        lifecycleDuration = duration > 0f && !float.IsInfinity(duration) ? Mathf.Max(0.01f, duration) : 0.01f;
        dashPose = false;
        ClearAfterimages();
        if (WeaponSprite != null) WeaponSprite.enabled = false;
        ChangeState(dead ? PlayerSpriteAnimationState.Death : PlayerSpriteAnimationState.Spawn, true);
    }

    /// <summary>중단·완료에서 생성·사망 표시를 끝내고 기존 검·정지 모션 복구</summary>
    public void EndLifecycleAnimation()
    {
        // 생성·사망의 종료 여부는 Spawner가 결정하고 표시는 정지 상태로 복귀
        if (!IsLifecycleAnimation) return;
        IsLifecycleAnimation = false;
        if (WeaponSprite != null) WeaponSprite.enabled = weaponWasVisible;
        IsMoving = false;
        IsBackpedaling = false;
        ChangeState(GetLocomotionState(), true);
    }

    /// <summary>R02 발도 모션을 시작하고 몸체에 포함된 검과 별도 검의 중복 표시 방지</summary>
    public bool BeginSlashAnimation()
    {
        // 전체 준비·대기·납도 데이터가 연결된 경우에만 전용 표시 진입
        if (IsLifecycleAnimation || !HasSlashAnimation) return false;

        // 기존 방향·무기 표시를 보관하고 준비 모션의 첫 자세 표시
        if (!IsSlashAnimation && WeaponSprite != null) slashWeaponWasVisible = WeaponSprite.enabled;
        IsSlashAnimation = true;
        dashPose = false;
        IsMoving = false;
        IsBackpedaling = false;
        ClearAfterimages();
        if (WeaponSprite != null) WeaponSprite.enabled = false;
        ChangeState(PlayerSpriteAnimationState.SlashDraw, true);
        return true;
    }

    /// <summary>빠른 확대 구간의 진행률로 몸 낮춤부터 일부 발도까지 표시</summary>
    public void SetSlashDrawProgress(float progress)
    {
        SampleSlashAnimation(PlayerSpriteAnimationState.SlashDraw, progress);
    }

    /// <summary>느린 확대·칼날 반짝임 동안 검을 일부 꺼낸 자세 유지</summary>
    public void HoldSlashAnimation()
    {
        SampleSlashAnimation(PlayerSpriteAnimationState.SlashHold, 0f);
    }

    /// <summary>외부 납도 진행률로 일부 꺼낸 칼이 완전히 닫히는 자세까지 표시</summary>
    public void SetSlashSheatheProgress(float progress)
    {
        SampleSlashAnimation(PlayerSpriteAnimationState.SlashSheathe, progress);
    }

    /// <summary>발도 완료·취소에서 별도 검과 현재 방향의 일반 몸체 표시 복구</summary>
    public void EndSlashAnimation()
    {
        // 자기 발도가 숨긴 검만 복구하고 새 입력이 일반 모션을 선택하도록 준비
        if (!IsSlashAnimation) return;
        IsSlashAnimation = false;
        if (WeaponSprite != null) WeaponSprite.enabled = slashWeaponWasVisible;
        IsMoving = false;
        IsBackpedaling = false;
        ChangeState(GetLocomotionState(), true);
    }

    /// <summary>실제 시간의 소유자인 Skill이 전달한 구간 진행률로 발도 프레임 선택</summary>
    private void SampleSlashAnimation(PlayerSpriteAnimationState state, float progress)
    {
        // 외부 샘플의 구간 전환에서만 데이터를 확보하고 현재 구간은 같은 참조 재사용
        if (!IsSlashAnimation || float.IsNaN(progress) || float.IsInfinity(progress)) return;
        ChangeState(state);
        if (currentStateData == null || stateDuration <= 0f) return;
        float duration = stateDuration;
        if (state == PlayerSpriteAnimationState.SlashSheathe)
        {
            // 닫힌 마지막 자세는 구간 완료에 맞춰 표시해 납도 소리보다 먼저 닫히는 현상 방지
            if (progress >= 1f)
            {
                ApplyFrame(currentStateData.Frames.Length - 1);
                return;
            }
            duration -= currentStateData.GetFrameDuration(currentStateData.Frames.Length - 1);
        }
        stateElapsed = Mathf.Clamp01(progress) * Mathf.Max(0f, duration);
        ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>발도 전용 상태의 Sprite 누락을 소비 전 시작 경계에서 확인</summary>
    private bool HasCompleteSlashAnimation()
    {
        // 세 구간이 모두 있어야 모션이 중간에 끊기거나 기존 자세로 대체되지 않음
        if (SlashAnimationSet == null) return false;
        for (int state = (int)PlayerSpriteAnimationState.SlashDraw; state <= (int)PlayerSpriteAnimationState.SlashSheathe; state++)
        {
            PlayerSpriteAnimationSet.StateData data = SlashAnimationSet.GetState((PlayerSpriteAnimationState)state);
            if (data == null || data.Frames == null || data.Frames.Length == 0) return false;
            for (int i = 0; i < data.Frames.Length; i++)
                if (data.Frames[i] == null) return false;
        }
        return true;
    }

    /// <summary>현재 사분면과 전달된 이동 여부에 맞는 기본 모션 상태 선택</summary>
    private PlayerSpriteAnimationState GetLocomotionState()
    {
        // enum의 방향 순서는 기존 FacingIndex와 같은 왼앞·오앞·왼뒤·오뒤
        FacingIndex = Mathf.Clamp(FacingIndex, 0, 3);
        int first = IsMoving ? (int)PlayerSpriteAnimationState.MoveLeftFront : (int)PlayerSpriteAnimationState.IdleLeftFront;
        return (PlayerSpriteAnimationState)(first + FacingIndex);
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
        PlayerSpriteAnimationSet set = (int)state >= (int)PlayerSpriteAnimationState.SlashDraw ? SlashAnimationSet : AnimationSet;
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

        // 이동속도 배율과 무관한 고정 FPS를 사용하고 뒷걸음만 역재생
        float direction = IsMoving && IsBackpedaling ? -1f : 1f;
        stateElapsed += deltaTime * direction;
        stateElapsed = currentStateData.Loop
            ? Mathf.Repeat(stateElapsed, stateDuration) : Mathf.Clamp(stateElapsed, 0f, stateDuration);
        ApplyFrame(currentStateData.GetFrameIndex(stateElapsed));
    }

    /// <summary>외부 연출 기간에 프레임별 유지 시간 비율을 맞춰 생성·사망 표시</summary>
    private void ApplyLifecycleFrame()
    {
        // 마지막 프레임은 반복하지 않고 Spawner의 종료 요청까지 유지
        if (currentStateData == null || stateDuration <= 0f) return;
        float time = LifecycleElapsed / lifecycleDuration * stateDuration;
        ApplyFrame(currentStateData.GetFrameIndex(time));
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
        PlayerSpriteAnimationSet set = (int)CurrentState >= (int)PlayerSpriteAnimationState.SlashDraw ? SlashAnimationSet : AnimationSet;
        if (set == null)
        {
            if (!warnedMissingSet) Debug.LogWarning("Player AnimationSet이 없어 기존 몸체 Sprite 유지", this);
            warnedMissingSet = true;
            return;
        }

        // 상태별 누락 프레임이나 시간 설정을 진입 시 검사하고 중복 경고 방지
        int stateBit = 1 << (int)CurrentState;
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
        ActiveAfterimages = 0;
        for (int i = 0; i < afterimages.Length; i++)
        {
            if (afterimageRemaining[i] <= 0f) continue;
            afterimageRemaining[i] = Mathf.Max(0f, afterimageRemaining[i] - Time.deltaTime);
            SpriteRenderer image = afterimages[i];
            image.transform.position = afterimagePositions[i];
            image.enabled = afterimageRemaining[i] > 0f;
            image.color = new Color(0.55f, 0.85f, 1f, 0.6f * afterimageRemaining[i] / AfterimageLifetime);
            if (image.enabled) ActiveAfterimages++;
        }
    }

    /// <summary>재배치·비활성화에서 이전 위치의 잔상 제거</summary>
    private void ClearAfterimages()
    {
        // 다음 대시에서 같은 표시 객체를 재사용할 수 있도록 수명만 초기화
        ActiveAfterimages = 0;
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
