using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>다음 보스 패턴 선택: 시연 동안에는 등록한 패턴을 같은 확률로 한 번씩 모두 돌고, 끄면 조건·직전 제외·우선순위와 가중 추첨</summary>
public class BossPatternSelector
{
    public static bool CycleAllPatterns = true; // 시연용 설정: 켜면 가중치·우선순위·쿨타임을 보지 않고 모든 패턴을 한 번씩 실행한 뒤 다시 섞음
    public string LastPatternId { get; private set; } // 직전 실제 시작 패턴을 연속 선택에서 제외할 이름

    private BossPattern[] patterns = Array.Empty<BossPattern>(); // 등록 시 한 번 복사하고 전투 동안 재사용할 패턴 원본
    private BossPattern[] candidates = Array.Empty<BossPattern>(); // 프레임마다 할당하지 않고 다시 채울 최우선 후보 버퍼
    private double[] weights = Array.Empty<double>(); // 후보마다 한 번 조회한 가중치와 누적 구간 계산 버퍼
    private const float CycleWait = 6f; // 남은 패턴을 지금 실행할 수 없을 때 새 바퀴로 넘기기 전에 기다릴 게임 시간
    private readonly List<BossPattern> remaining = new List<BossPattern>(); // 이번 바퀴에서 아직 실행하지 않은 패턴
    private float waitingSince = -1f; // 남은 패턴을 기다리기 시작한 게임 시각이며 음수는 기다리지 않는 상태

    /// <summary>중복 이름을 검사한 패턴 목록과 추첨 버퍼를 한 번 준비</summary>
    public void SetPatterns(params BossPattern[] values)
    {
        // 잘못된 등록이 기존 목록을 부분 교체하지 않도록 먼저 검사
        if (values == null) throw new ArgumentNullException(nameof(values));
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == null) throw new ArgumentException("A pattern list cannot contain null.", nameof(values));
            for (int j = 0; j < i; j++)
                if (values[j].PatternId == values[i].PatternId)
                    throw new ArgumentException("Pattern IDs must be unique.", nameof(values));
        }

        // 목록은 초기화 경계에서만 교체하고 반복 선택에서는 버퍼 재사용
        ResetHistory();
        patterns = (BossPattern[])values.Clone();
        candidates = new BossPattern[patterns.Length];
        weights = new double[patterns.Length];
        ResetHistory();
    }

    /// <summary>최고 우선순위의 유효 후보를 가중 추첨하고 후보가 없으면 Idle 유지</summary>
    public BossPattern GetNextPattern(BossCharacter owner)
    {
        if (CycleAllPatterns) return GetNextInCycle(owner);

        // 직전 패턴과 조건 실패를 제외한 뒤 최고 양수 우선순위 후보만 보관
        int count = 0;
        int highestPriority = 0;
        double total = 0d;
        for (int i = 0; i < patterns.Length; i++)
        {
            BossPattern pattern = patterns[i];
            pattern.IgnoreCooldown = false;
            if (pattern.PatternId == LastPatternId || !pattern.CanRun(owner)) continue;
            float weight = pattern.GetWeight(owner);
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f) continue;
            int priority = Mathf.Max(0, pattern.GetPriority(owner));
            if (priority < highestPriority) continue;
            if (priority > highestPriority)
            {
                highestPriority = priority;
                count = 0;
                total = 0d;
            }
            candidates[count] = pattern;
            weights[count] = weight;
            total += weight;
            count++;
        }
        if (count == 0) return null;

        // 등록 순서와 무관하게 각 가중치의 누적 구간으로 한 후보 선택
        double selected = UnityEngine.Random.value * total;
        for (int i = 0; i < count; i++)
        {
            selected -= weights[i];
            if (selected < 0d) return candidates[i];
        }
        return candidates[count - 1];
    }

    /// <summary>실제 실행된 패턴만 직전 이력에 반영</summary>
    public void HandlePatternStarted(BossPattern pattern)
    {
        if (pattern == null || Array.IndexOf(patterns, pattern) < 0)
            throw new ArgumentException("Only a registered pattern can be recorded.", nameof(pattern));
        LastPatternId = pattern.PatternId;
        remaining.Remove(pattern);
    }

    /// <summary>보스가 등록된 패턴만 직접 시작할 수 있도록 확인</summary>
    public bool Contains(BossPattern pattern)
    {
        return pattern != null && Array.IndexOf(patterns, pattern) >= 0;
    }

    /// <summary>재전투에서 직전 제외와 쿨타임 이력 초기화</summary>
    public void ResetHistory()
    {
        LastPatternId = null;
        remaining.Clear();
        waitingSince = -1f;
        for (int i = 0; i < patterns.Length; i++) patterns[i].ResetHistory();
    }

    /// <summary>이번 바퀴에 남은 패턴 가운데 지금 실행할 수 있는 것을 같은 확률로 하나 고르고, 모두 실행했으면 새 바퀴를 시작</summary>
    private BossPattern GetNextInCycle(BossCharacter owner)
    {
        // 남은 패턴이 없으면 전체를 다시 채워 새 바퀴 시작
        if (remaining.Count == 0) remaining.AddRange(patterns);
        int count = CollectRunnable(owner);
        if (count > 0)
        {
            waitingSince = -1f;
            return candidates[UnityEngine.Random.Range(0, count)];
        }

        // 남은 패턴이 Idle 행동이 끝나기를 기다리는 것처럼 지금만 실행할 수 없으면 Idle을 유지하며 기다림
        if (waitingSince < 0f) waitingSince = Time.time;
        if (Time.time - waitingSince < CycleWait) return null;

        // 오래 기다려도 실행할 수 없으면 이번 바퀴에서는 건너뛰고 전체를 다시 채움
        waitingSince = -1f;
        remaining.Clear();
        remaining.AddRange(patterns);
        count = CollectRunnable(owner);
        return count > 0 ? candidates[UnityEngine.Random.Range(0, count)] : null;
    }

    /// <summary>남은 패턴 가운데 직전 패턴이 아니고 쿨타임을 뺀 실행 조건을 통과한 것을 후보 버퍼에 모음</summary>
    private int CollectRunnable(BossCharacter owner)
    {
        int count = 0;
        for (int i = 0; i < remaining.Count; i++)
        {
            BossPattern pattern = remaining[i];
            pattern.IgnoreCooldown = true;
            if (pattern.PatternId == LastPatternId || !pattern.CanRun(owner)) continue;
            candidates[count++] = pattern;
        }
        return count;
    }
}
