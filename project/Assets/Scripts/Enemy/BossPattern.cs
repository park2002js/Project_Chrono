using System;
using UnityEngine;

/// <summary>보스 패턴의 실행 조건·시간·완료와 아직 진행 중인 행동의 취소</summary>
public abstract class BossPattern
{
    public string PatternId { get; private set; } // 직전 패턴과 선택 후보를 구분할 고유 이름
    public float Weight = 1f; // 같은 우선순위 후보 사이의 상대 추첨 가중치
    public int Priority; // 특정 상황에서 일반 추첨보다 먼저 선택할 양수 우선값
    public float Cooldown; // 실제 시작 시점부터 다음 실행까지 필요한 게임 시간
    public float LastUsedTime { get; private set; } = float.NegativeInfinity; // 실제 실행이 시작된 마지막 게임 시간
    public bool IsRunning { get; private set; } // 완료 또는 취소 전까지 Tick을 받을 실행 상태
    public float Elapsed { get; private set; } // 이번 패턴이 실제로 진행한 게임 시간
    public BossCharacter Owner { get; private set; } // 실행 시작 때 연결한 같은 세션의 보스

    /// <summary>등록할 패턴의 고유 식별자 준비</summary>
    protected BossPattern(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId)) throw new ArgumentException("A boss pattern requires an ID.", nameof(patternId));
        PatternId = patternId;
    }

    /// <summary>공통 쿨타임을 확인하고 파생 패턴이 실행 조건을 추가할 창구</summary>
    public virtual bool CanRun(BossCharacter owner)
    {
        return owner != null && !IsRunning && !float.IsNaN(Cooldown) && !float.IsInfinity(Cooldown)
            && Cooldown >= 0f && Time.time - LastUsedTime >= Cooldown;
    }

    /// <summary>현재 상황에서 같은 우선순위 후보에 적용할 가중치 조회</summary>
    public virtual float GetWeight(BossCharacter owner)
    {
        return Weight;
    }

    /// <summary>현재 상황에만 적용할 우선값 조회</summary>
    public virtual int GetPriority(BossCharacter owner)
    {
        return Priority;
    }

    /// <summary>조건을 통과한 실제 시작만 기록하고 패턴별 준비 실행</summary>
    public bool Begin(BossCharacter owner)
    {
        // 실패한 시작은 쿨타임에 포함하지 않도록 먼저 검사
        if (!CanRun(owner)) return false;
        Owner = owner;
        Elapsed = 0f;
        LastUsedTime = Time.time;
        IsRunning = true;
        owner.HandlePatternStarted(this);

        // 파생 패턴은 이 시점부터 예고·조준·자기 실행 상태만 준비
        OnBegin();
        return true;
    }

    /// <summary>정지·잘못된 시간 요청을 제외하고 현재 패턴만 진행</summary>
    public void Tick(float elapsed)
    {
        if (!IsRunning || float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed <= 0f) return;
        Elapsed += elapsed;
        OnTick(elapsed);
    }

    /// <summary>추가 동작과 발사를 중단하며 이미 생성된 탄환의 수명은 유지</summary>
    public void Cancel()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Owner.HandlePatternCompleted(this);
        OnCancel();
    }

    /// <summary>재도전 때 현재 실행과 이전 사용 이력 초기화</summary>
    public void ResetHistory()
    {
        Cancel();
        Owner = null;
        Elapsed = 0f;
        LastUsedTime = float.NegativeInfinity;
    }

    /// <summary>정상 완료를 한 번 알리고 같은 실행의 후속 Tick 차단</summary>
    protected void Complete()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Owner.HandlePatternCompleted(this);
    }

    /// <summary>파생 패턴의 시작 시 조준·예고와 실행 변수 준비</summary>
    protected abstract void OnBegin();

    /// <summary>파생 패턴의 게임 시간 동작·발사·정상 종료 처리</summary>
    protected abstract void OnTick(float elapsed);

    /// <summary>파생 패턴의 아직 진행 중인 이동·예고만 정리</summary>
    protected virtual void OnCancel() { }
}
