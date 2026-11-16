using System;
using UnityEngine;

/// <summary>현재 게임에서 사용하는 캐릭터 효과의 구분</summary>
public enum BuffType
{
    Fever, // 금색 탄 반사로 얻는 일시 강화
    Groggy, // Boss가 소유하는 행동 중단 상태
    Slow // 일정 시간 이동속도를 낮추는 효과
}

/// <summary>확정 효과의 예약·기간 갱신·개별 제거와 기본 수치에 대한 비중첩 합성</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30)]
public class CharacterBuff : MonoBehaviour
{
    public bool IsFever { get; private set; } // 각성 수치와 전용 Draw 대기가 적용된 상태
    public event Action<bool> OnFeverChanged; // 각성 진입·해제를 자기 오라 등 표현 구독자에게 알릴 사건
    public float FeverRemaining { get; private set; } // 각성 종료까지 남은 게임 시간
    public float FeverDuration = 8f; // 각성 획득 때마다 갱신할 지속시간
    public float AttackMultiplier = 1.5f; // 기본 공격력에 적용할 각성 배율
    public float AttackSpeedMultiplier = 1.25f; // 기본 공격속도에 적용할 각성 배율
    public float MoveSpeedMultiplier = 1.25f; // 기본 이동속도에 적용할 각성 배율
    public float HealPerAcquisition = 5f; // 금색 탄 하나를 반사할 때 회복할 체력
    public float FeverDrawCooldownMultiplier = 0.5f; // 정상 Draw 대기에 곱할 각성 전용 배율
    public float DrawCooldownMultiplier => IsFever ? FeverDrawCooldownMultiplier : 1f; // 새로 부여하는 Draw 대기에 사용할 현재 배율
    public bool IsSlow { get; private set; } // 감속 배율이 현재 이동속도에 적용된 상태
    public float SlowRemaining { get; private set; } // 감속 종료까지 남은 게임 시간
    public float SlowDuration = 2f; // 새 감속과 재적용에 사용할 지속시간
    [Range(0f, 1f)] public float SlowSpeedMultiplier = 0.5f; // 각성과 합성해 기본 이동속도에 곱할 감속 배율

    private Character target; // Awake에서 확보한 자기 프리팹의 효과 수신자
    private SkillController skill; // 각성 획득 시 현재 Draw 대기 조정을 요청할 자기 능력 컴포넌트
    private BossCharacter boss; // 그로기 상태·수명을 소유하고 행동 중단을 알릴 자기 Boss
    private int pendingCount; // 다음 프레임에 각각 회복할 각성 획득 요청 수
    private int pendingFrame = -1; // 예약한 요청이 들어온 마지막 프레임
    private int previousPendingCount; // 새 요청과 분리해 다음 갱신에서 적용할 이전 프레임 요청
    private int removedFrame = -1; // 같은 프레임의 재적용보다 제거를 우선할 기준
    private int pendingSlowFrame = -1; // 감속 적용을 다음 프레임으로 제한할 요청 시점
    private int removedSlowFrame = -1; // 같은 프레임의 감속 제거를 우선할 기준
    private int pendingGroggyFrame = -1; // 일반 효과 API의 그로기 적용을 다음 프레임으로 제한할 시점
    private int removedGroggyFrame = -1; // 같은 프레임의 그로기 제거를 우선할 기준

    /// <summary>자기 대상과 실제 사망 시 효과 정리 연결</summary>
    private void Awake()
    {
        target = GetComponent<Character>();
        skill = GetComponent<SkillController>();
        boss = GetComponent<BossCharacter>();
        if (target != null)
        {
            target.OnDie += RemoveAll; // 실제 사망 순간 효과·예약 제거
            target.OnRevive += RemoveAll; // 부활에서 이전 효과와 미처리 획득 정리
        }
    }

    /// <summary>이전 프레임의 획득을 적용하고 게임 시간으로 각성 수명 진행</summary>
    private void Update()
    {
        // 현재 프레임의 새 요청을 구분해 적용을 다음 프레임으로 제한
        if (pendingFrame < Time.frameCount)
        {
            previousPendingCount += pendingCount;
            pendingCount = 0;
        }
        int acquisitions = previousPendingCount;
        previousPendingCount = 0;
        bool feverApplied = acquisitions > 0 && removedFrame != Time.frameCount && CanApplyFever();
        if (feverApplied)
        {
            ApplyFeverValues();
            for (int i = 0; i < acquisitions; i++)
                target.Heal(HealPerAcquisition);
        }

        // 감속과 일반 그로기 요청은 다음 프레임에 각각 한 번 적용
        bool slowApplied = pendingSlowFrame >= 0 && pendingSlowFrame < Time.frameCount;
        if (slowApplied)
        {
            pendingSlowFrame = -1;
            slowApplied = removedSlowFrame != Time.frameCount && CanApplySlow();
            if (slowApplied)
            {
                IsSlow = true;
                SlowRemaining = SlowDuration;
                RefreshStats();
            }
        }
        if (pendingGroggyFrame >= 0 && pendingGroggyFrame < Time.frameCount)
        {
            pendingGroggyFrame = -1;
            if (removedGroggyFrame != Time.frameCount && CanApplyTarget() && boss != null)
                boss.EnterGroggy();
        }

        // 게임 정지에서는 효과 수명을 유지하고 개별 만료만 처리
        if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        if (IsFever && !feverApplied)
        {
            FeverRemaining = Mathf.Max(0f, FeverRemaining - Time.deltaTime);
            if (FeverRemaining <= 0f) Remove(BuffType.Fever);
        }
        if (IsSlow && !slowApplied)
        {
            SlowRemaining = Mathf.Max(0f, SlowRemaining - Time.deltaTime);
            if (SlowRemaining <= 0f) Remove(BuffType.Slow);
        }
    }

    private void OnDestroy()
    {
        if (target != null)
        {
            target.OnDie -= RemoveAll; // 프리팹 해제 시 사망 연결 정리
            target.OnRevive -= RemoveAll; // 프리팹 해제 시 부활 연결 정리
        }
    }

    /// <summary>이번 획득을 다음 프레임에 적용하도록 예약</summary>
    public bool ApplyFever()
    {
        if (!CanApplyFever() || removedFrame == Time.frameCount) return false;

        // 이전 프레임 예약은 보존하고 이번 프레임 획득 수를 따로 누적
        if (pendingFrame != Time.frameCount)
        {
            previousPendingCount += pendingCount;
            pendingCount = 0;
            pendingFrame = Time.frameCount;
        }
        pendingCount++;
        return true;
    }

    /// <summary>현재 게임에 확정된 효과를 자기 수신자에 예약</summary>
    public bool Apply(BuffType type)
    {
        if (type == BuffType.Fever) return ApplyFever();
        if (type == BuffType.Slow)
        {
            if (!CanApplySlow() || removedSlowFrame == Time.frameCount) return false;
            if (pendingSlowFrame < 0) pendingSlowFrame = Time.frameCount;
            return true;
        }
        if (type == BuffType.Groggy)
        {
            if (!CanApplyTarget() || boss == null || !Positive(boss.GroggyDuration) || removedGroggyFrame == Time.frameCount) return false;
            if (pendingGroggyFrame < 0) pendingGroggyFrame = Time.frameCount;
            return true;
        }
        return false;
    }

    /// <summary>적용된 효과만 조회하고 아직 처리하지 않은 예약과 구분</summary>
    public bool Has(BuffType type)
    {
        return type == BuffType.Fever ? IsFever : type == BuffType.Slow ? IsSlow : type == BuffType.Groggy && boss != null && boss.IsGroggy;
    }

    /// <summary>지정한 효과와 예약만 지우고 다른 효과의 합성 수치 유지</summary>
    public void Remove(BuffType type)
    {
        // 종류별 예약과 같은 프레임 재적용을 먼저 차단
        bool changed = false;
        if (type == BuffType.Fever)
        {
            pendingCount = previousPendingCount = 0;
            removedFrame = Time.frameCount;
            changed = IsFever;
            IsFever = false;
            FeverRemaining = 0f;
        }
        else if (type == BuffType.Slow)
        {
            pendingSlowFrame = -1;
            removedSlowFrame = Time.frameCount;
            changed = IsSlow;
            IsSlow = false;
            SlowRemaining = 0f;
        }
        else if (type == BuffType.Groggy)
        {
            pendingGroggyFrame = -1;
            removedGroggyFrame = Time.frameCount;
            if (boss != null) boss.ExitGroggy();
        }

        // 남아 있는 효과를 기본 수치에 다시 합성해 제거 순서와 무관한 결과 유지
        if (changed)
        {
            RefreshStats();
            if (type == BuffType.Fever) OnFeverChanged?.Invoke(false);
        }
    }

    /// <summary>Boss 처치 시 유익한 각성은 유지하고 부정 효과·예약만 제거</summary>
    public void ClearDebuffs()
    {
        Remove(BuffType.Slow);
        Remove(BuffType.Groggy);
    }

    /// <summary>사망·전투 복원에서 효과와 예약 전체 제거</summary>
    public void Clear()
    {
        RemoveAll();
    }

    /// <summary>효과·예약을 즉시 제거하고 같은 프레임 재획득보다 제거 우선</summary>
    public void RemoveAll()
    {
        // 재적용 예약을 먼저 지워 제거 뒤 콜백에서도 되살아나지 않도록 처리
        Remove(BuffType.Fever);
        Remove(BuffType.Slow);
        Remove(BuffType.Groggy);
    }

    /// <summary>재획득에서도 배율을 다시 곱하지 않고 기본 수치와 기간 갱신</summary>
    private void ApplyFeverValues()
    {
        // 재획득은 기간·수치만 갱신하고 오라를 처음부터 다시 재생하지 않음
        bool entered = !IsFever;
        IsFever = true;
        FeverRemaining = FeverDuration;
        RefreshStats();
        if (skill != null)
            skill.RefreshDrawCooldown();
        if (entered) OnFeverChanged?.Invoke(true);
    }

    /// <summary>생존 대상과 각성 설정의 유효성 확인</summary>
    private bool CanApplyFever()
    {
        return CanApplyTarget()
            && Positive(FeverDuration) && Positive(AttackMultiplier) && Positive(AttackSpeedMultiplier)
            && Positive(MoveSpeedMultiplier) && Nonnegative(HealPerAcquisition) && Nonnegative(FeverDrawCooldownMultiplier)
            && Nonnegative(target.basicAD) && Positive(target.basicAS) && Nonnegative(target.basicSP);
    }

    /// <summary>기본 수치와 현재 강화·감속을 한 곳에서 합성</summary>
    private void RefreshStats()
    {
        if (target == null) return;
        target.AD = target.basicAD * (IsFever ? AttackMultiplier : 1f);
        target.AS = target.basicAS * (IsFever ? AttackSpeedMultiplier : 1f);
        target.SP = target.basicSP * (IsFever ? MoveSpeedMultiplier : 1f) * (IsSlow ? SlowSpeedMultiplier : 1f);
    }

    /// <summary>효과를 적용할 자기 대상의 생존·준비 상태 확인</summary>
    private bool CanApplyTarget()
    {
        return isActiveAndEnabled && target != null && target.IsInitialized && !target.IsDead;
    }

    /// <summary>감속이 유한한 기간과 0~1 이동 배율을 사용하는지 확인</summary>
    private bool CanApplySlow()
    {
        return CanApplyTarget() && Positive(SlowDuration) && Nonnegative(SlowSpeedMultiplier)
            && SlowSpeedMultiplier <= 1f && Nonnegative(target.basicSP);
    }

    /// <summary>각성 설정이 유한한 0 이상 값인지 확인</summary>
    private static bool Nonnegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>시간·배율 설정이 유한한 양수인지 확인</summary>
    private static bool Positive(float value)
    {
        return Nonnegative(value) && value > 0f;
    }
}
