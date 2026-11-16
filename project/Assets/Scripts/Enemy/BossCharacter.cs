using System.Collections.Generic;
using UnityEngine;

/// <summary>보스의 공통 전투·패턴 실행·그로기와 같은 Slash의 부위 결과 관리</summary>
[RequireComponent(typeof(BossSpawner))]
[RequireComponent(typeof(CharacterBuff))]
public class BossCharacter : Character
{
    public string BossId = "guard_robot"; // 저장된 처치 기록과 Map의 배치 정보를 연결하는 Boss 식별자
    public string BossName = "Guard Robot"; // Boss HUD에 표시할 이름
    public bool IsInCombat; // Boss의 전투 여부
    public bool IsGroggy; // Boss의 그로기 여부
    public Vector3 BasePosition; // Map의 배치 지점에서 설정한 Boss의 기본 월드 위치
    public float GroggyDuration = 3f; // 그로기 진입 후 행동이 제한될 게임 시간
    public float GroggyRemaining { get; private set; } // 그로기 종료까지 남은 게임 시간
    public CharacterBuff Buffs { get; private set; } // 전투 복원에서 효과·예약을 정리할 자기 효과 컴포넌트
    public BossPatternSelector PatternSelector { get; private set; } // 초기화 때 등록한 조건·가중치 목록으로 다음 패턴을 선택할 도구
    public BossPattern CurrentPattern { get; private set; } // 현재 Tick 또는 취소를 전달할 실행 패턴
    public PlayerCharacter Target { get; private set; } // 전투 시작 경계에서 한 번 연결한 같은 세션의 Player
    public bool PatternsInitialized { get; private set; } // Awake의 구체 패턴 등록이 예외 없이 끝났는지 Spawner가 확인할 상태

    private readonly HashSet<int> slashParts = new HashSet<int>(); // 같은 Slash의 중복 부위 보고를 합칠 집합
    private int currentSlashAttackId; // 현재 부위를 수집하는 공격 번호
    private bool collectingSlash; // 최초 공격 번호와 아직 보고가 없는 상태 구분
    private bool completedSlash; // 같은 공격의 집계 완료 중복 통지 차단

    /// <summary>공통 캐릭터와 전투 복원에 필요한 자기 컴포넌트 준비</summary>
    protected override void Awake()
    {
        // 자기 수명 동안 재사용할 효과·선택 도구와 즉시 중단 사건 준비
        base.Awake();
        Buffs = GetComponent<CharacterBuff>();
        PatternSelector = new BossPatternSelector();
        OnDie += EndEncounter; // 사망 순간 현재 패턴과 전투 대상 정리
        OnCutScene += CancelPattern; // 컷씬 진입 순간 아직 진행 중인 발사·동작 취소

        // 구체 보스는 외부 Player 생성 전 자기 패턴 객체만 한 번 등록
        InitializePatterns();
        PatternsInitialized = true;
    }

    /// <summary>게임 시간으로 그로기와 실행 패턴을 진행하고 Idle에서 다음 후보 요청</summary>
    private void Update()
    {
        // 그로기 기간은 전투 개시와 무관하게 같은 단일 타이머로 처리
        if (!IsInitialized || IsDead || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        if (IsGroggy)
        {
            GroggyRemaining = Mathf.Max(0f, GroggyRemaining - Time.deltaTime);
            if (GroggyRemaining <= 0f) ExitGroggy();
            return;
        }

        // 대상 사망·컷씬·전투 종료에서는 패턴이 더 진행되지 않도록 차단
        if (!IsInCombat || IsCutScene || Target == null || Target.IsDead || !Target.isActiveAndEnabled)
        {
            CancelPattern();
            return;
        }
        if (CurrentPattern != null)
        {
            if (CurrentPattern.IsRunning) CurrentPattern.Tick(Time.deltaTime);
            else HandlePatternCompleted(CurrentPattern);
            return;
        }

        // 후보가 없는 동안 Idle을 유지하고 다음 프레임에 현재 조건으로 다시 조회
        BossPattern next = PatternSelector.GetNextPattern(this);
        if (next != null) ApplyPattern(next);
    }

    private void OnDisable()
    {
        EndEncounter();
    }

    private void OnDestroy()
    {
        OnDie -= EndEncounter; // 제거된 보스의 자기 전투 종료 연결 해제
        OnCutScene -= CancelPattern; // 제거된 보스의 자기 행동 취소 연결 해제
        CancelPattern();
    }

    /// <summary>전투 준비가 끝난 뒤 같은 세션의 Player를 한 번 연결</summary>
    public void BeginEncounter(PlayerCharacter player)
    {
        if (!isActiveAndEnabled || !IsInitialized || !PatternsInitialized || IsDead || player == null || player.IsDead || !player.IsInitialized)
            return;
        if (Target != player) CancelPattern();
        Target = player;
        IsInCombat = true;
    }

    /// <summary>현재 행동과 전투 연결만 닫고 필드 생성물 정리는 전투 소유자에게 위임</summary>
    public void EndEncounter()
    {
        // 취소 콜백에서 새 행동을 시작하지 못하도록 전투 허용부터 해제
        IsInCombat = false;
        Target = null;
        CancelPattern();
        ExitGroggy();
    }

    /// <summary>공통 행동 허용과 등록 여부를 확인한 뒤 선택 패턴 실행</summary>
    public bool ApplyPattern(BossPattern pattern)
    {
        // 외부 직접 요청도 Idle·생존·전투·직전 제외 규칙을 동일하게 적용
        if (!isActiveAndEnabled || !IsInitialized || !PatternsInitialized || IsDead || !IsInCombat || IsCutScene || IsGroggy
            || Target == null || Target.IsDead || CurrentPattern != null || !PatternSelector.Contains(pattern)
            || PatternSelector.LastPatternId == pattern.PatternId)
            return false;
        CurrentPattern = pattern;

        // Begin 내부의 즉시 완료·다음 패턴 시작과 예외를 각각 현재 실행으로 구분
        try
        {
            if (!pattern.Begin(this))
            {
                HandlePatternCompleted(pattern);
                return false;
            }
            return true;
        }
        catch
        {
            if (CurrentPattern == pattern) CancelPattern();
            throw;
        }
    }

    /// <summary>현재 패턴의 후속 발사를 즉시 중단하고 다음 실행과 구분</summary>
    public void CancelPattern()
    {
        if (CurrentPattern == null) return;
        BossPattern pattern = CurrentPattern;
        CurrentPattern = null;
        pattern.Cancel();
    }

    /// <summary>피해 수신자 중복 처리와 별도로 이번 Slash의 부위 수집</summary>
    public void ReportSlashPart(int attackId, int partId)
    {
        // 새 공격은 이전 공격의 부위와 완료 상태를 교체
        if (!collectingSlash || currentSlashAttackId != attackId)
        {
            slashParts.Clear();
            currentSlashAttackId = attackId;
            collectingSlash = true;
            completedSlash = false;
        }
        if (!completedSlash)
            slashParts.Add(partId);
    }

    /// <summary>같은 공격의 모든 부위가 모인 뒤 고유 기믹 판단 창구에 통지</summary>
    public void CompleteSlash(int attackId)
    {
        if (!collectingSlash || currentSlashAttackId != attackId || completedSlash) return;
        completedSlash = true;
        HandleSlashHit(attackId);
    }

    /// <summary>모든 부위가 모인 시점에 구체 보스의 동시 Slash 성공 조건 판단</summary>
    public virtual void HandleSlashHit(int attackId) { }

    /// <summary>게이지나 고유 기믹에서 그로기를 요청하고 현재 행동 중단</summary>
    public void EnterGroggy()
    {
        if (!isActiveAndEnabled || !IsInitialized || IsDead || float.IsNaN(GroggyDuration) || float.IsInfinity(GroggyDuration) || GroggyDuration <= 0f)
            return;
        // 재진입에서도 기간을 갱신하고 현재 행동 중단
        IsGroggy = true;
        GroggyRemaining = GroggyDuration;
        CancelPattern();
    }

    /// <summary>기간 종료·효과 제거에서 그로기 원본과 잔여 함께 정리</summary>
    public void ExitGroggy()
    {
        IsGroggy = false;
        GroggyRemaining = 0f;
    }

    /// <summary>재도전에서 기존 Boss의 효과·부위 집계·전투 수치를 기본 상태로 복구</summary>
    public void ResetCombatState()
    {
        // 이전 전투의 효과·상태와 부위 집계 정리
        EndEncounter();
        if (PatternSelector != null) PatternSelector.ResetHistory();
        if (Buffs != null) Buffs.Clear();
        ExitGroggy();
        IsInCombat = false;
        IsCutScene = false;
        IsInvulnerable = false;
        slashParts.Clear();
        currentSlashAttackId = 0;
        collectingSlash = completedSlash = false;

        // 체력과 배치는 Spawner가 맡고 공통 행동 수치만 기본값으로 복귀
        AD = basicAD;
        AS = basicAS;
        SP = basicSP;
    }

    /// <summary>구체 보스가 자기 프리팹의 설정으로 패턴을 한 번 등록할 창구</summary>
    protected virtual void InitializePatterns() { }

    /// <summary>실제 시작을 외부 콜백보다 먼저 기록해 즉시 완료에서도 직전 이력 유지</summary>
    internal void HandlePatternStarted(BossPattern pattern)
    {
        if (CurrentPattern == pattern) PatternSelector.HandlePatternStarted(pattern);
    }

    /// <summary>완료 알림 전에 현재 실행만 해제해 콜백이 시작한 새 패턴 보존</summary>
    internal void HandlePatternCompleted(BossPattern pattern)
    {
        if (CurrentPattern != pattern) return;
        CurrentPattern = null;
    }
}
