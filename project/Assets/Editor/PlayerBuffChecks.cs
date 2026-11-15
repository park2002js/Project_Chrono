using System;
using System.Collections;
using UnityEngine;

/// <summary>실제 캐릭터 효과 API의 합성·기간·제거 우선과 생명주기 복원 확인</summary>
public static class PlayerBuffChecks
{
    /// <summary>기존 Play Mode 실행기로 효과 검사를 진행하고 Player·설정 상태 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Action<string> passed)
    {
        // 현재 세션과 검사 뒤 복구할 Player 수치·튜닝 보관
        PlayerCharacter player = manager.CurrentPlayer;
        CharacterBuff buffs = player.Buffs;
        float health = player.HP;
        float attack = player.AD;
        float attackSpeed = player.AS;
        float speed = player.SP;
        float groggy = player.Groggy;
        float feverDuration = buffs.FeverDuration;
        float slowDuration = buffs.SlowDuration;
        float slowMultiplier = buffs.SlowSpeedMultiplier;
        float heal = buffs.HealPerAcquisition;
        bool inputEnabled = input.enabled;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        GameObject bossFixture = null;
        GameObject characterFixture = null;
        try
        {
            // 기존 입력과 행동 간섭 없이 실제 효과 적용 경계 준비
            input.enabled = false;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            player.Skills.ResetSkills();
            buffs.Clear();
            buffs.FeverDuration = 2f;
            buffs.SlowDuration = 0.5f;
            buffs.SlowSpeedMultiplier = 0.4f;
            buffs.HealPerAcquisition = 2f;
            player.HP = player.maxHP * 0.5f;
            yield return Frames(1); // 초기 제거의 같은 프레임 우선 경계를 지난 뒤 요청

            // 감속과 복수 각성의 실제 공개 API를 통해 비중첩 합성·개별 제거 확인
            Require(player.ApplyBuff(BuffType.Slow) && !buffs.Has(BuffType.Slow), "Slow must be queued through Character and remain unapplied in the requesting frame.");
            yield return Until(() => buffs.Has(BuffType.Slow), "Slow application"); // 감속이 다음 프레임에 적용될 때까지 대기
            Require(Close(player.SP, player.basicSP * buffs.SlowSpeedMultiplier), "Slow must scale the base movement speed.");
            Require(player.ApplyBuff(BuffType.Fever) && buffs.ApplyFever() && !buffs.IsFever,
                "Two Fever acquisitions must remain queued until a later frame.");
            yield return Until(() => buffs.IsFever, "Fever and Slow composition"); // 여러 획득과 감속이 같은 수신자에 합성될 때까지 대기
            Require(Close(player.HP, player.maxHP * 0.5f + 4f) && Close(player.AD, player.basicAD * buffs.AttackMultiplier)
                && Close(player.AS, player.basicAS * buffs.AttackSpeedMultiplier)
                && Close(player.SP, player.basicSP * buffs.MoveSpeedMultiplier * buffs.SlowSpeedMultiplier),
                "Fever must heal once per acquisition and compose its stats once with Slow.");
            player.RemoveBuff(BuffType.Fever);
            Require(buffs.IsSlow && !buffs.IsFever && Close(player.AD, player.basicAD) && Close(player.AS, player.basicAS)
                && Close(player.SP, player.basicSP * buffs.SlowSpeedMultiplier), "Removing Fever must preserve active Slow.");
            yield return Frames(1); // 같은 프레임 제거 후 각성 재적용 경계 통과
            Require(player.ApplyBuff(BuffType.Fever), "Fever must be available on a later frame after removal.");
            yield return Until(() => buffs.IsFever, "Fever reapplied over Slow"); // 감속 위에 각성이 다시 적용될 때까지 대기
            player.RemoveBuff(BuffType.Slow);
            Require(!buffs.IsSlow && buffs.IsFever && Close(player.SP, player.basicSP * buffs.MoveSpeedMultiplier),
                "Removing Slow must preserve Fever's movement multiplier.");
            passed("F-P11 Character effect APIs defer application, compose Fever and Slow without stacking, heal per acquisition and preserve the other effect during individual removal");

            // 효과 기간 갱신과 전체 정지·자연 만료가 다른 효과를 지우지 않는지 확인
            buffs.Clear();
            buffs.SlowDuration = 0.22f;
            yield return Frames(1); // 개별 효과의 제거 우선 프레임 통과
            Require(buffs.Apply(BuffType.Fever) && buffs.Apply(BuffType.Slow), "Both effects must accept a fresh acquisition.");
            yield return Until(() => buffs.IsFever && buffs.IsSlow, "independent effect lifetimes"); // 두 효과의 적용 완료 대기
            manager.SetGameplayPaused(true);
            float frozenSlow = buffs.SlowRemaining;
            float frozenFever = buffs.FeverRemaining;
            yield return RealSeconds(0.1f); // 게임 정지 동안 두 효과 수명이 유지되는지 관찰
            Require(buffs.SlowRemaining == frozenSlow && buffs.FeverRemaining == frozenFever, "Game pause must freeze both effect lifetimes.");
            manager.SetGameplayPaused(false);
            yield return GameSeconds(0.07f); // 재적용이 갱신할 감속 기간 일부 소모
            float previousSlow = buffs.SlowRemaining;
            float beforeSlowHeal = player.HP;
            Require(player.ApplyBuff(BuffType.Slow), "An active Slow must accept a duration refresh.");
            yield return Until(() => buffs.SlowRemaining > previousSlow, "Slow duration refresh"); // 중첩 대신 감속 기간이 갱신될 때까지 대기
            Require(player.HP == beforeSlowHeal, "Refreshing Slow must not grant Fever's acquisition heal.");
            yield return Until(() => !buffs.IsSlow, "Slow natural expiration"); // 감속의 고유 수명 종료 대기
            Require(buffs.IsFever && Close(player.SP, player.basicSP * buffs.MoveSpeedMultiplier), "Slow expiration must leave Fever active and restore only the Slow contribution.");
            passed("F-P11 effect lifetimes freeze with game time, Slow reacquisition refreshes duration without healing and natural expiration preserves independent Fever");

            // Boss 종료 창구가 부정 효과·예약만 지우고 Boss 그로기 원본을 재사용하는지 확인
            buffs.Clear();
            yield return Frames(1); // 제거 우선 프레임 통과
            Require(buffs.Apply(BuffType.Fever) && buffs.Apply(BuffType.Slow), "Debuff cleanup needs queued beneficial and harmful effects.");
            buffs.ClearDebuffs();
            Require(!buffs.Apply(BuffType.Slow), "Debuff removal must win over same-frame Slow reacquisition.");
            yield return Until(() => buffs.IsFever, "beneficial effect after debuff cleanup"); // 부정 효과 정리 뒤 유익한 예약만 적용되는지 대기
            Require(!buffs.IsSlow && buffs.IsFever, "ClearDebuffs must remove queued Slow and preserve queued Fever.");
            bossFixture = new GameObject("BuffGroggyFixture");
            bossFixture.SetActive(false);
            BossCharacter boss = bossFixture.AddComponent<BossCharacter>();
            bossFixture.SetActive(true);
            boss.InitializeHealth(boss.maxHP);
            boss.GroggyDuration = 0.14f;
            player.Groggy = 0f;
            player.AddGroggy(100f, boss);
            Require(boss.IsGroggy && boss.Buffs.Has(BuffType.Groggy) && player.Groggy == 0f
                && boss.Buffs.GroggyRemaining == boss.GroggyRemaining, "The threshold must enter Boss-owned Groggy immediately and expose the same remaining value through effects.");
            boss.Buffs.Remove(BuffType.Groggy);
            Require(!boss.IsGroggy && boss.GroggyRemaining == 0f && !boss.ApplyBuff(BuffType.Groggy), "Groggy removal must clear the Boss timer and reject a same-frame queued reapplication.");
            yield return Frames(1); // 일반 그로기 효과 요청의 제거 우선 경계 통과
            Require(boss.ApplyBuff(BuffType.Groggy) && !boss.IsGroggy, "A normal Groggy effect request must be deferred.");
            yield return Until(() => boss.IsGroggy, "queued Groggy effect"); // Boss가 그로기 요청을 다음 프레임에 받는지 대기
            yield return Until(() => !boss.IsGroggy, "Boss-owned Groggy expiration"); // 중복 타이머 없이 Boss 원본의 자연 종료 대기
            boss.ResetCombatState();
            Require(!boss.Buffs.Has(BuffType.Groggy) && boss.SlashParts.Count == 0, "Boss reset must clear effect and combat records.");
            passed("F-P11 debuff cleanup preserves Fever and removes queued Slow, while immediate gauge Groggy and deferred effect Groggy share one Boss timer and reset path");

            // 실제 치명 피해와 부활 사건에서 활성·예약 효과 모두 제거
            characterFixture = new GameObject("BuffLifeCycleFixture");
            characterFixture.SetActive(false);
            Character target = characterFixture.AddComponent<Character>();
            CharacterBuff targetBuffs = characterFixture.AddComponent<CharacterBuff>();
            characterFixture.SetActive(true);
            target.InitializeHealth(target.maxHP);
            Require(target.ApplyBuff(BuffType.Fever) && target.ApplyBuff(BuffType.Slow), "A live target must queue effects through its cached component.");
            Require(target.ApplyDamage(new DamageInstance(null, target.maxHP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false)), "Actual lethal damage must reach the effect owner.");
            target.Revive();
            yield return Frames(2); // 사망 전 예약이 부활 뒤 늦게 적용되지 않는지 관찰
            Require(!targetBuffs.IsFever && !targetBuffs.IsSlow && Close(target.AD, target.basicAD) && Close(target.AS, target.basicAS)
                && Close(target.SP, target.basicSP), "Death and revival must leave no active or queued effect state.");
            Require(target.ApplyBuff(BuffType.Fever) && target.ApplyBuff(BuffType.Slow), "A revived target must accept fresh later-frame effects.");
            yield return Until(() => targetBuffs.IsFever && targetBuffs.IsSlow, "fresh effects after revival"); // 같은 인스턴스의 새 효과 적용 대기
            target.ApplyDamage(new DamageInstance(null, target.maxHP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false));
            Require(!targetBuffs.IsFever && !targetBuffs.IsSlow && targetBuffs.FeverRemaining == 0f && targetBuffs.SlowRemaining == 0f
                && Close(target.AD, target.basicAD) && Close(target.AS, target.basicAS) && Close(target.SP, target.basicSP),
                "Death must immediately remove active effect contributions and timers.");
            passed("F-P11 actual death and revival clear active and queued effects, restore base stats and allow fresh effects on the reused character");
        }
        finally
        {
            // 검사 대상과 실제 Player의 수치·튜닝·정지·입력 상태 복구
            if (bossFixture != null) UnityEngine.Object.Destroy(bossFixture);
            if (characterFixture != null) UnityEngine.Object.Destroy(characterFixture);
            manager.SetGameplayPaused(false);
            buffs.Clear();
            buffs.FeverDuration = feverDuration;
            buffs.SlowDuration = slowDuration;
            buffs.SlowSpeedMultiplier = slowMultiplier;
            buffs.HealPerAcquisition = heal;
            player.HP = health;
            player.AD = attack;
            player.AS = attackSpeed;
            player.SP = speed;
            player.Groggy = groggy;
            player.ResetMotion();
            player.Skills.ResetSkills();
            manager.SetGameplayPaused(paused);
            input.enabled = inputEnabled;
            input.BlockInput = blocked;
        }
    }

    /// <summary>수치 합성 결과를 부동소수점 오차 범위에서 비교</summary>
    private static bool Close(float first, float second)
    {
        return Mathf.Abs(first - second) < 0.001f;
    }

    /// <summary>다음 프레임 효과 처리 경계까지 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "frames"); // 예약·제거의 프레임 순서가 진행될 때까지 대기
    }

    /// <summary>게임 시간으로 진행하는 효과 수명 관찰</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game seconds"); // 효과와 같은 게임 시간 경과 대기
    }

    /// <summary>게임 정지 중 실제 시간만 경과시켜 수명 보존 확인</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 일시정지 상태의 효과 잔여 비교 대기
    }

    /// <summary>기존 검사 루프에서 조건을 기다리고 중단 없는 무한 대기 방지</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P11 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>효과 계약 위반을 기존 검사 결과로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
