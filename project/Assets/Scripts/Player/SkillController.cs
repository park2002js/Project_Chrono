using System;
using UnityEngine;

/// <summary>Draw 편집·Line 수명과 Slash 실행·판정 시점·전용 입력·시간·표시 전환 조정</summary>
[DefaultExecutionOrder(20)]
[RequireComponent(typeof(DrawVfx))]
public class SkillController : MonoBehaviour
{
    public bool IsDrawing { get; private set; } // Draw 전용 입력으로 임시 점을 편집하는 상태
    public bool HasLine => player != null && player.CurrentLine.HasValue; // Player가 보유한 확정 경로의 존재 여부
    public bool CanDraw => !IsDrawing && !IsSlashing && DrawCooldownRemaining <= 0f; // 능력 자체의 모드·재사용 제한
    public bool IsSlashing => Slasher != null && Slasher.IsRunning; // 마지막 한 번의 판정을 기다리는 Slash 실행 상태
    public LineSlasher Slasher { get; private set; } // 이번 Slash 기하·판정·중복 기록을 재사용할 도구
    public float SlashDuration = 0.15f; // 게임 정지 중 검격 연출을 진행할 실제 시간의 초기 튜닝값
    public float SlashDamageMultiplier = 3f; // 현재 공격력에 적용할 Slash 피해의 초기 튜닝 배율
    public LayerMask SlashTargetMask = ~0; // Slash 사각에 포함할 피격 Collider의 레이어
    public float SlashRemaining { get; private set; } // 마지막 정지 시점의 판정까지 남은 실제 시간
    public LineDrawer Drawer { get; private set; } // 임시 점과 고정 길이 기하를 관리하는 자기 편집 도구
    public float DrawCooldownRemaining { get; private set; } // 다음 Draw까지 남은 게임 시간
    public float SlashCooldownRemaining { get; private set; } // 확정 직후 후속 Slash까지 남은 게임 시간
    public float LineLength = 6f; // 고정 경로 길이의 초기 튜닝값
    public float LineWidth = 0.25f; // 확정 경로 너비의 초기 튜닝값
    public float CancelDrawCooldown = 0.35f; // 편집 취소·확정 후 짧은 Draw 대기의 초기 튜닝값
    public float NormalDrawCooldown = 3f; // 보유 경로를 소비한 뒤 일반 Draw 대기의 초기 튜닝값
    public float ConfirmSlashCooldown = 0.15f; // 새 확정 후 Slash 대기의 초기 튜닝값
    public float LineLifetime = 8f; // 새 확정 경로를 유지할 게임 시간의 초기 튜닝값
    public float LineRemaining { get; private set; } // Player의 현재 경로가 만료되기까지 남은 게임 시간
    public float LineElapsed => HasLine ? totalLineLifetime - LineRemaining : 0f; // 현재 경로의 수명에 실제로 진행된 게임 시간

    private PlayerCharacter player; // 공통 시작 상태를 판단하고 요청을 전달할 자기 Player
    private CharacterRendering rendering; // Draw 중 Player를 암전 위에 표시할 자기 렌더링
    private DrawVfx vfx; // 기하 데이터와 별도로 점·선 프리팹을 표시할 표현 컴포넌트
    private PlayerInputController input; // Start에서 한 번 확보할 MainWorld 전용 입력 서비스
    private GameManager gameManager; // 일반 정지와 구분한 Draw 정지를 요청할 앱 관리자
    private UIController ui; // 기존 HUD와 Draw 전용 표시를 전환할 MainWorld UI
    private GameCamera gameCamera; // Draw의 커서 가중 중심·줌과 복구를 요청할 카메라
    private bool ready; // 자기 도구와 외부 서비스의 최초 준비 완료 여부
    private float totalLineLifetime; // 현재 경로를 확정한 순간의 총수명이며 중간 설정 변경과 구분
    private double lastSkillSampleTime; // 같은 프레임의 입력·Update에서 시간을 중복 소비하지 않을 마지막 게임 시각
    private float drawCooldownBase; // 각성 재적용에서 남은 시간을 반복 반감하지 않을 이번 Draw 대기의 정상 길이
    private double lastSlashSampleTime; // 입력이 시작한 같은 프레임의 이전 시간을 Slash에서 차감하지 않을 실제 시각

    /// <summary>다른 객체의 생성 순서와 무관한 자기 컴포넌트·편집 데이터 준비</summary>
    private void Awake()
    {
        player = GetComponent<PlayerCharacter>();
        rendering = GetComponent<CharacterRendering>();
        vfx = GetComponent<DrawVfx>();
        Drawer = new LineDrawer();
        Slasher = new LineSlasher();
    }

    /// <summary>MainWorld 서비스의 Awake 이후 고정 참조를 한 번 확보</summary>
    private void Start()
    {
        input = PlayerInputController.Instance;
        gameManager = GameManager.Instance;
        ui = UIController.Instance;
        gameCamera = GameCamera.Instance;
        ready = player != null && rendering != null && input != null && gameManager != null && ui != null && gameCamera != null && vfx.Prepare();
        if (!ready)
            Debug.LogError($"{name}: Draw requires the prepared input, UI, camera and VFX references.", this);
    }

    private void OnEnable()
    {
        lastSkillSampleTime = Time.timeAsDouble;
        if (vfx != null) vfx.SetLineSuspended(false);
    }

    /// <summary>게임 시간으로 재사용 제한과 Player에 저장한 경로의 수명·표현 진행</summary>
    private void Update()
    {
        RefreshSkillTime();
        if (!IsSlashing) return;
        double now = Time.unscaledTimeAsDouble;
        SlashRemaining = Mathf.Max(0f, SlashRemaining - (float)Math.Max(0d, now - lastSlashSampleTime));
        lastSlashSampleTime = now;
        if (SlashRemaining > 0f) return;

        // 정지 해제 직전 한 번만 판정하고 피해 콜백에서 취소되었으면 복구를 반복하지 않음
        int attackId = Slasher.AttackId;
        Line line = Slasher.ActiveLine;
        Slasher.ApplyHits(player, gameManager.CurrentBoss);
        if (!IsSlashing || Slasher.AttackId != attackId) return;
        FinishSlash();
        vfx.PlaySlash(line);
    }

    private void OnDisable()
    {
        CancelSlash();
        CancelDraw();
        if (vfx != null) vfx.SetLineSuspended(true);
    }

    /// <summary>Character가 허용한 요청에서 능력 자체의 조건을 확인하고 Draw 전환</summary>
    internal bool ApplyDraw()
    {
        // 입력이 Update보다 먼저 도착해도 만료와 짧은 대기를 먼저 반영
        RefreshSkillTime();

        // 초기 연결·자체 대기와 편집 기하 설정만 확인
        if (!ready || !CanDraw || input.Player != player || !input.HasDrawActions ||
            !ValidTime(CancelDrawCooldown) || !ValidTime(ConfirmSlashCooldown) || !Drawer.Begin(LineLength, LineWidth))
            return false;

        // 일반 입력과 별개의 Draw 모드를 먼저 열고 자기 정지·표현 적용
        IsDrawing = true;
        input.SetDrawMode(true);
        gameManager.SetDrawPaused(true);
        ui.SetDrawMode(true);
        rendering.SetDrawHighlight(true);
        gameCamera.SetDrawAim(true, player.AimWorldPosition);
        vfx.ShowEditing(Drawer);
        return true;
    }

    /// <summary>Draw 전용 커서 입력으로 임시 선과 가중 카메라 위치 갱신</summary>
    public void UpdateDrawAim(Vector2 position)
    {
        if (!IsDrawing) return;
        Drawer.UpdatePreview(position);
        vfx.ShowEditing(Drawer);
        gameCamera.SetDrawAim(true, position);
    }

    /// <summary>Draw 전용 클릭을 임시 점 편집에 전달</summary>
    public bool PlacePoint(Vector2 position)
    {
        if (!IsDrawing) return false;
        bool placed = Drawer.PlacePoint(position);
        vfx.ShowEditing(Drawer);
        return placed;
    }

    /// <summary>기존 확정 Line은 유지하고 이번 편집 점만 초기화</summary>
    public void ResetPoints()
    {
        if (!IsDrawing) return;
        Drawer.ResetPoints();
        vfx.ShowEditing(Drawer);
    }

    /// <summary>유효한 두 점을 새 확정 값으로 교체하고 일반 입력으로 복귀</summary>
    public bool ConfirmLine()
    {
        if (!IsDrawing || !ValidTime(LineLifetime) || LineLifetime <= 0f || !Drawer.Confirm(out Line line)) return false;
        SetLine(line);
        SlashCooldownRemaining = ConfirmSlashCooldown;
        FinishDraw();
        return true;
    }

    /// <summary>기존 확정 Line을 보존하며 진행 중 편집만 종료</summary>
    public void CancelDraw()
    {
        RefreshSkillTime();
        if (!IsDrawing) return;
        Drawer.Cancel();
        FinishDraw();
    }

    /// <summary>확정 경로를 Player에 저장하고 새 수명·표현을 함께 시작하거나 정리</summary>
    public void SetLine(Line? line)
    {
        // 새 경로를 저장하기 전에 총수명 설정의 유효성 확인
        if (line.HasValue && (!ValidTime(LineLifetime) || LineLifetime <= 0f))
            throw new ArgumentOutOfRangeException(nameof(LineLifetime), "Line lifetime must be positive and finite.");
        RefreshSkillTime();

        // 기하 원본은 Player 한 곳에 보관하고 새 확정마다 수명 초기화
        player.CurrentLine = line;
        totalLineLifetime = line.HasValue ? LineLifetime : 0f;
        LineRemaining = totalLineLifetime;
        if (vfx != null) vfx.ShowLine(line);
    }

    /// <summary>현재 시각에 유효한 경로를 한 번 인계하고 소비 후 Draw 대기 적용</summary>
    public bool ConsumeLine(out Line line)
    {
        // 같은 프레임의 자연 만료가 소비보다 먼저 판정되도록 시각 동기화
        RefreshSkillTime();
        line = default;
        if (!HasLine || !ValidTime(NormalDrawCooldown)) return false;

        // 공격 허용·실행은 호출자가 담당하며 여기서는 데이터 인계와 수명 정리만 처리
        line = player.CurrentLine.Value;
        SetLine(null);
        StartDrawCooldown(NormalDrawCooldown);
        return true;
    }

    /// <summary>사망 초기화에서 편집·확정 경로·능력 대기 상태 정리</summary>
    public void ResetSkills()
    {
        CancelSlash();
        CancelDraw();
        SetLine(null);
        DrawCooldownRemaining = 0f;
        drawCooldownBase = 0f;
        SlashCooldownRemaining = 0f;
    }

    /// <summary>Character가 허용한 요청의 스킬 조건을 확인하고 Line을 소비해 Slash 시작</summary>
    internal bool ApplySlash()
    {
        // 소비 전에 표현·연결·피해·자체 대기 설정을 모두 확인
        RefreshSkillTime();
        float amount = player.AD * SlashDamageMultiplier;
        if (!ready || IsDrawing || IsSlashing || SlashCooldownRemaining > 0f || !input.HasSlashAction || !vfx.isActiveAndEnabled 
            || !ValidTime(SlashDuration) || SlashDuration <= 0f || !ValidTime(amount) || amount <= 0f || !ConsumeLine(out Line line))
            return false;

        // 원본을 한 번 인계한 뒤 실행 기하·보호·정지·표현을 같은 경계에서 시작
        Slasher.Execute(line, new DamageInstance(player, amount, DamageType.Slash, DamageInstance.NextAttackId()), SlashTargetMask);
        SlashRemaining = SlashDuration;
        lastSlashSampleTime = Time.unscaledTimeAsDouble;
        input.SetSlashMode(true);
        gameManager.SetSlashPaused(true);
        ui.SetSlashMode(true);
        rendering.SetDrawHighlight(true);
        gameCamera.SetSlashMode(true, player.transform);
        return true;
    }

    /// <summary>사망·컷씬·외부 입력 차단에서 진행 중 Slash 즉시 중단</summary>
    public void CancelSlash()
    {
        if (IsSlashing) FinishSlash();
        else if (vfx != null) vfx.ClearSlash();
    }

    /// <summary>완료·중단에서 자기 Slash가 소유한 상태만 복구</summary>
    private void FinishSlash()
    {
        // 외부 콜백이 새 실행을 요청하기 전에 현재 판정을 먼저 종료
        Slasher.Complete();
        SlashRemaining = 0f;
        if (vfx != null) vfx.ClearSlash();
        if (rendering != null) rendering.SetDrawHighlight(false);

        // 기존 일반 정지·외부 무적은 덮지 않고 자기 표시·입력·정지만 해제
        if (input != null) input.SetSlashMode(false);
        if (gameCamera != null) gameCamera.SetSlashMode(false);
        if (ui != null) ui.SetSlashMode(false);
        if (gameManager != null) gameManager.SetSlashPaused(false);
    }

    /// <summary>Draw가 소유한 입력·시간·표시만 진입 전 상태로 복구</summary>
    private void FinishDraw()
    {
        // 재진입과 취소 콜백 전에 편집 상태를 먼저 종료
        IsDrawing = false;
        StartDrawCooldown(CancelDrawCooldown);
        if (vfx != null) vfx.ClearEditing();
        if (rendering != null) rendering.SetDrawHighlight(false);

        // 서비스가 해제 중이어도 남은 서비스의 자기 Draw 상태는 복구
        if (input != null) input.SetDrawMode(false);
        if (gameCamera != null) gameCamera.Restore();
        if (ui != null) ui.SetDrawMode(false);
        if (gameManager != null) gameManager.SetDrawPaused(false);
    }

    /// <summary>자연 만료에서만 경로를 해제하고 짧은 Draw 대기·전용 소멸 표현 시작</summary>
    private void ExpireLine()
    {
        Line expired = player.CurrentLine.Value;
        SetLine(null);
        StartDrawCooldown(CancelDrawCooldown);
        vfx.PlayLineExpiration(expired);
    }

    /// <summary>정지·비활성 시간을 제외하고 경로·대기·표현에 같은 게임 시간 적용</summary>
    private void RefreshSkillTime()
    {
        // 콜백의 재진입과 같은 프레임의 후속 Update보다 먼저 마지막 시각 갱신
        double now = Time.timeAsDouble;
        float elapsed = (float)Math.Max(0d, now - lastSkillSampleTime);
        lastSkillSampleTime = now;
        if (!isActiveAndEnabled || elapsed <= 0f) return;

        // 이미 존재하던 표현을 먼저 진행해 이번 만료에서 생성할 표현의 시간은 보존
        DrawCooldownRemaining = Mathf.Max(0f, DrawCooldownRemaining - elapsed);
        SlashCooldownRemaining = Mathf.Max(0f, SlashCooldownRemaining - elapsed);
        vfx.TickVisuals(elapsed);

        // 원본 데이터는 Player에서 조회하고 같은 경로의 타이머만 관리
        if (!HasLine) return;
        LineRemaining = Mathf.Max(0f, LineRemaining - elapsed);
        if (LineRemaining <= 0f)
            ExpireLine();
        else
            vfx.UpdateLineLifetime(LineRemaining, totalLineLifetime, elapsed);
    }

    /// <summary>각성 획득 때 현재 대기를 정상 길이 기준으로 조정하되 반복 반감 방지</summary>
    public void RefreshDrawCooldown()
    {
        RefreshSkillTime();
        DrawCooldownRemaining = Mathf.Min(DrawCooldownRemaining, drawCooldownBase * player.Buffs.DrawCooldownMultiplier);
    }

    /// <summary>기존 대기보다 긴 새 Draw 대기만 적용하고 정상 길이 보관</summary>
    private void StartDrawCooldown(float duration)
    {
        float adjusted = duration * player.Buffs.DrawCooldownMultiplier;
        if (adjusted < DrawCooldownRemaining) return;
        drawCooldownBase = duration;
        DrawCooldownRemaining = adjusted;
    }

    /// <summary>능력 대기 설정이 유한한 0 이상 게임 시간인지 확인</summary>
    private static bool ValidTime(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
