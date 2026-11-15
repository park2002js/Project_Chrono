using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>조건·직전 제외·우선순위와 가중 추첨으로 다음 보스 패턴 선택</summary>
public class BossPatternSelector
{
    public IReadOnlyList<BossPattern> Patterns { get; private set; } = Array.Empty<BossPattern>(); // 보스 초기화 때 등록한 패턴 목록의 읽기 창구
    public string LastPatternId { get; private set; } // 직전 실제 시작 패턴을 연속 선택에서 제외할 이름

    private BossPattern[] patterns = Array.Empty<BossPattern>(); // 등록 시 한 번 복사하고 전투 동안 재사용할 패턴 원본
    private BossPattern[] candidates = Array.Empty<BossPattern>(); // 프레임마다 할당하지 않고 다시 채울 최우선 후보 버퍼
    private double[] weights = Array.Empty<double>(); // 후보마다 한 번 조회한 가중치와 누적 구간 계산 버퍼

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

        // 목록 교체는 초기화·시험 설정 경계에서만 수행하고 반복 선택에서는 버퍼 재사용
        ResetHistory();
        patterns = (BossPattern[])values.Clone();
        Patterns = Array.AsReadOnly(patterns);
        candidates = new BossPattern[patterns.Length];
        weights = new double[patterns.Length];
        ResetHistory();
    }

    /// <summary>최고 우선순위의 유효 후보를 가중 추첨하고 후보가 없으면 Idle 유지</summary>
    public BossPattern GetNextPattern(BossCharacter owner)
    {
        // 직전 패턴과 조건 실패를 제외한 뒤 최고 양수 우선순위 후보만 보관
        int count = 0;
        int highestPriority = 0;
        double total = 0d;
        for (int i = 0; i < patterns.Length; i++)
        {
            BossPattern pattern = patterns[i];
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
    }

    /// <summary>보스가 등록된 패턴만 직접 시작할 수 있도록 확인</summary>
    public bool Contains(BossPattern pattern)
    {
        return pattern != null && Array.IndexOf(patterns, pattern) >= 0;
    }

    /// <summary>재전투에서 직전 제외·쿨타임·실행 횟수 함께 초기화</summary>
    public void ResetHistory()
    {
        LastPatternId = null;
        for (int i = 0; i < patterns.Length; i++) patterns[i].ResetHistory();
    }
}
