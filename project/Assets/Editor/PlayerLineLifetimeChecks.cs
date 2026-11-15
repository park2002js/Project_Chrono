using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Player가 보관한 확정 Line의 수명·만료와 연결된 표현을 실제 갱신으로 확인</summary>
public static class PlayerLineLifetimeChecks
{
    /// <summary>기존 Draw 검사 뒤 수명 상태와 표현을 확인하고 세션 수치 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Action<string> passed)
    {
        // 확정 경로가 없는 검사 경계와 복원할 Inspector 값을 보관
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        DrawVfx vfx = player.GetComponent<DrawVfx>();
        Require(!player.CurrentLine.HasValue && !skills.IsDrawing, "Lifetime checks require the clean confirmed-Line boundary restored by the Draw checks.");
        float lifetime = skills.LineLifetime;
        float cancelCooldown = skills.CancelDrawCooldown;
        float normalCooldown = skills.NormalDrawCooldown;
        float slashCooldown = skills.ConfirmSlashCooldown;
        float warningFraction = vfx.WarningFraction;
        float slowPeriod = vfx.SlowBlinkPeriod;
        float fastPeriod = vfx.FastBlinkPeriod;
        float expiryDuration = vfx.ExpiryDuration;
        float health = player.HP;
        bool invulnerable = player.IsInvulnerable;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        Action beforeInputUpdate = null;
        try
        {
            // 실제 입력 해제를 반영하고 짧은 검사 수명과 독립 표현 설정 준비
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            skills.ResetSkills();
            skills.CancelDrawCooldown = 0f;
            skills.ConfirmSlashCooldown = 0f;
            skills.LineLifetime = 1.2f;
            vfx.WarningFraction = 0.75f;
            vfx.SlowBlinkPeriod = 0.3f;
            vfx.FastBlinkPeriod = 0.06f;
            vfx.ExpiryDuration = 0.3f;
            yield return Frames(3); // 이전 검사에서 해제한 키가 새 수명 검사에 남지 않도록 대기

            // Draw 확정 결과가 Player 원본으로 저장되고 새 확정은 수명을 처음부터 시작
            Vector2 origin = player.transform.position;
            Line first = Confirm(player, origin + Vector2.right, Vector2.up);
            Require(player.CurrentLine.HasValue && SameLine(player.CurrentLine.Value, first) && skills.HasLine,
                "Draw confirmation must store its only confirmed-Line value on PlayerCharacter.");
            Require(skills.LineRemaining == skills.LineLifetime && skills.LineElapsed == 0f && vfx.LineRemainingRatio == 1f && vfx.IsLineVisible,
                "A new confirmed Line must begin with a full lifetime and an immediately visible VFX.");
            yield return GameSeconds(0.2f); // 실제 Skill.Update가 Player에 저장된 경로의 수명을 진행하는지 관찰
            Require(skills.LineRemaining < skills.LineLifetime && skills.LineElapsed > 0f && SameLine(player.CurrentLine.Value, first),
                "The manager must advance time while the Player retains the same geometry.");
            Line second = Confirm(player, origin + Vector2.up, Vector2.left);
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == skills.LineLifetime && skills.LineElapsed == 0f,
                "A replacement confirmation must replace Player geometry and reset the old timer.");
            passed("F-P06 Draw confirmation stores Line on Player and Skill lifetime restarts only for a new confirmed value");

            // 재Draw 진입·취소와 일반 정지가 기존 Line·경고 위상을 보존
            yield return GameSeconds(0.12f); // 정지 전 이미 일부 소비된 잔여 수명 준비
            Require(player.ApplyDraw(), "A valid Player must enter redraw while preserving its existing Line.");
            yield return Frames(1); // Draw 정지 진입 프레임의 시간 샘플을 지난 뒤 잔여 값 보관
            float remaining = skills.LineRemaining;
            float elapsed = skills.LineElapsed;
            float phase = vfx.LifePhase;
            bool visible = vfx.IsLineVisible;
            yield return RealSeconds(0.12f); // 게임 시간이 멈춘 Draw 편집 중 기존 경로 수명·표현 위상 관찰
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == remaining && skills.LineElapsed == elapsed
                && vfx.LifePhase == phase && vfx.IsLineVisible == visible, "Redraw must freeze the stored Line's lifetime and blink phase.");
            player.CancelDraw();
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == remaining,
                "Cancelling editing must preserve the old Line and its remaining lifetime.");
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 일반 정지의 첫 프레임을 지난 뒤 같은 시간 기준으로 비교
            remaining = skills.LineRemaining;
            phase = vfx.LifePhase;
            yield return RealSeconds(0.12f); // 일반 pause도 경로와 수명 기반 연출을 멈추는지 관찰
            Require(skills.LineRemaining == remaining && vfx.LifePhase == phase, "Ordinary pause must freeze the same Line clock and VFX phase.");
            manager.SetGameplayPaused(false);
            yield return GameSeconds(0.08f); // 정지를 푼 뒤 이전 잔여 시간에서 재개하는지 관찰
            Require(skills.LineRemaining < remaining && SameLine(player.CurrentLine.Value, second), "The existing lifetime must resume after its pause owners release.");
            passed("F-P06 redraw cancellation and ordinary pause preserve Player Line, remaining lifetime and warning phase");

            // 후반 경고는 실제 표시를 깜빡이고 만료에 가까워질수록 주기 단축
            skills.LineLifetime = 2.4f;
            skills.SetLine(second);
            vfx.WarningFraction = 0.9f;
            vfx.SlowBlinkPeriod = 0.4f;
            vfx.FastBlinkPeriod = 0.08f;
            yield return Until(() => vfx.LineRemainingRatio <= 0.85f, "warning begins"); // 수명 후반 경고가 시작되는 실제 게임 시간 대기
            float earlyInterval = vfx.BlinkInterval;
            phase = vfx.LifePhase;
            visible = vfx.IsLineVisible;
            remaining = skills.LineRemaining;
            float ratio = vfx.LineRemainingRatio;
            Require(player.ApplyDraw(), "A warning Line must still permit redraw when Character conditions allow it.");
            Require(vfx.LifePhase == phase && vfx.IsLineVisible == visible && vfx.LineRemainingRatio == ratio && skills.LineRemaining == remaining,
                "Entering redraw must not restart the existing Line VFX, warning ratio or phase.");
            yield return RealSeconds(0.12f); // 이미 경고 중인 선도 재Draw 편집 동안 같은 위상을 유지하는지 관찰
            Require(vfx.LifePhase == phase && vfx.LineRemainingRatio == ratio, "Redraw must preserve an already active lifetime warning.");
            player.CancelDraw();
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 실제 경고 구간에서 정지 진입 프레임을 넘긴 뒤 위상 보관
            phase = vfx.LifePhase;
            visible = vfx.IsLineVisible;
            yield return RealSeconds(0.12f); // 진행 중 경고 깜빡임이 일반 정지에서 함께 멈추는지 관찰
            Require(vfx.LifePhase == phase && vfx.IsLineVisible == visible, "Pause must also preserve an already blinking warning phase.");
            manager.SetGameplayPaused(false);
            bool firstVisibility = vfx.IsLineVisible;
            bool toggled = false;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (skills.HasLine && vfx.LineRemainingRatio > 0.25f)
            {
                toggled |= vfx.IsLineVisible != firstVisibility;
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 warning did not advance.");
                yield return null;
            }
            Require(skills.HasLine && toggled && vfx.BlinkInterval < earlyInterval && vfx.BlinkInterval > 0f,
                "The visible Line must blink with a shorter interval as its remaining ratio decreases.");
            skills.CancelDrawCooldown = 0.3f;
            yield return Until(() => !player.CurrentLine.HasValue, "natural Line expiry"); // Skill이 Player 원본을 비우는 자연 만료 시점 대기
            Require(!skills.HasLine && skills.LineRemaining == 0f && !vfx.IsLineVisible && !vfx.LineObject.activeSelf,
                "Natural expiry must clear the Player-owned value and close its live line VFX.");
            Require(skills.DrawCooldownRemaining > 0f && skills.DrawCooldownRemaining <= skills.CancelDrawCooldown
                && vfx.IsExpiryPlaying && vfx.ExpiryObject != null && vfx.ExpiryObject.activeSelf,
                "Natural expiry must apply the short Draw cooldown and start its separate expiration VFX once.");
            yield return Until(() => !vfx.IsExpiryPlaying, "expiration VFX finishes"); // 수명 종료 연출이 정해진 시간 뒤 정리되는지 대기
            Require(!vfx.ExpiryObject.activeSelf, "Completed expiration VFX must hide its reusable object.");
            yield return GameSeconds(0.1f); // 이미 비워진 Line이 다음 프레임마다 소멸 연출을 다시 만들지 않는지 관찰
            Require(!player.CurrentLine.HasValue && !vfx.IsExpiryPlaying && !vfx.ExpiryObject.activeSelf,
                "An expired empty Line must not restart its expiration effect.");
            passed("F-P06 lifetime warning accelerates actual blinking and natural expiry clears Player Line with one short cooldown and VFX");

            // 교체·수동 삭제·능력 비활성화·사망은 낡은 수명이나 자연 소멸 연출을 남기지 않음
            skills.CancelDrawCooldown = 0f;
            vfx.WarningFraction = 0.1f;
            skills.LineLifetime = 0.25f;
            skills.SetLine(first);
            yield return GameSeconds(0.12f); // 교체 직전 소모된 짧은 이전 수명 준비
            skills.LineLifetime = 1.2f;
            skills.SetLine(second);
            yield return GameSeconds(0.2f); // 이전 만료 시각을 지나도 새 경로가 유지되는지 관찰
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining > 0f && !vfx.IsExpiryPlaying,
                "Replacing a Line must invalidate the previous lifetime and expiration effect.");
            skills.SetLine(null);
            Require(!player.CurrentLine.HasValue && skills.LineRemaining == 0f && !vfx.IsExpiryPlaying && !vfx.LineObject.activeSelf,
                "Explicit clearing must remove geometry and lifetime without playing natural expiration VFX.");
            skills.SetLine(first);
            skills.enabled = false;
            remaining = skills.LineRemaining;
            phase = vfx.LifePhase;
            yield return RealSeconds(0.12f); // 비활성 능력이 수명을 진행하거나 숨긴 표현을 다시 켜지 않는지 관찰
            Require(player.CurrentLine.HasValue && skills.LineRemaining == remaining && vfx.LifePhase == phase && !vfx.LineObject.activeSelf,
                "A disabled manager must preserve its data and clock while suspending presentation.");
            skills.enabled = true;
            Require(skills.LineRemaining == remaining && vfx.LifePhase == phase && vfx.LineObject.activeSelf,
                "Re-enabling the manager must restore the same lifetime phase and current presentation.");
            vfx.enabled = false;
            remaining = skills.LineRemaining;
            yield return GameSeconds(0.12f); // 표현만 꺼도 관리자의 실제 수명은 계속 진행하는지 관찰
            Require(player.CurrentLine.HasValue && skills.LineRemaining < remaining && !vfx.LineObject.activeSelf,
                "Disabling presentation must hide the line without stopping its manager-owned lifetime.");
            vfx.enabled = true;
            Require(vfx.LineObject.activeSelf && Mathf.Abs(vfx.LineRemainingRatio - skills.LineRemaining / skills.LineLifetime) < 0.001f,
                "Re-enabled presentation must restore the latest manager-owned lifetime state.");
            player.IsInvulnerable = false;
            Require(player.ApplyDamage(new DamageInstance(manager.CurrentBoss, player.HP + 1f, DamageType.Effect, 9201, false)),
                "The lifetime cleanup check must use actual lethal damage.");
            Require(player.IsDead && !player.CurrentLine.HasValue && skills.LineRemaining == 0f && skills.DrawCooldownRemaining == 0f
                && !vfx.LineObject.activeSelf && !vfx.IsExpiryPlaying,
                "Death must synchronously reset the Player-owned Line and all ability timers without natural expiration VFX.");
            player.InitializeHealth(health);
            passed("F-P06 replacement, explicit clearing, manager suspension and actual death leave no stale Line clock or expiration VFX");

            // 유효 경로의 인계는 원본·수명·표현을 비우고 긴 Draw 대기만 적용
            skills.ResetSkills();
            skills.LineLifetime = 1.2f;
            skills.CancelDrawCooldown = 0.24f;
            skills.NormalDrawCooldown = 3f;
            skills.SetLine(first);
            Require(skills.ConsumeLine(out Line consumed) && SameLine(consumed, first) && !player.CurrentLine.HasValue
                && skills.LineRemaining == 0f && !vfx.LineObject.activeSelf && !vfx.IsExpiryPlaying
                && skills.DrawCooldownRemaining == skills.NormalDrawCooldown,
                "A valid Line handoff must return its geometry, clear ownership and lifetime, and apply the normal cooldown without natural expiration VFX.");
            remaining = skills.DrawCooldownRemaining;
            Require(!skills.ConsumeLine(out _) && skills.DrawCooldownRemaining == remaining && !vfx.IsExpiryPlaying,
                "A second same-frame handoff must fail without changing cooldown or playing expiration VFX.");

            // Input 갱신 직전 실제 만료 시각에 요청해 Skill.Update보다 먼저 지난 경로 거부
            skills.ResetSkills();
            skills.LineLifetime = 0.2f;
            skills.SetLine(second);
            double expirationTime = Time.timeAsDouble + skills.LineLifetime;
            bool boundaryReached = false;
            bool pendingBeforeSkillUpdate = false;
            bool consumedExpiredLine = false;
            bool expirationApplied = false;
            beforeInputUpdate = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic || Time.timeAsDouble < expirationTime) return;

                // 갱신 전 남아 있는 경로가 API의 시각 검사에서 자연 만료되는지 수집
                pendingBeforeSkillUpdate = player.CurrentLine.HasValue && skills.LineRemaining > 0f;
                consumedExpiredLine = skills.ConsumeLine(out _);
                expirationApplied = !player.CurrentLine.HasValue && skills.LineRemaining == 0f && vfx.IsExpiryPlaying
                    && skills.DrawCooldownRemaining > 0f && skills.DrawCooldownRemaining <= skills.CancelDrawCooldown;
                boundaryReached = true;
                InputSystem.onBeforeUpdate -= beforeInputUpdate; // 실제 만료 경계에서 한 번 요청한 뒤 수집 종료
            };
            InputSystem.onBeforeUpdate += beforeInputUpdate; // Dynamic Input 직전의 경로 인계·만료 순서 수집
            yield return Until(() => boundaryReached, "handoff before Skill.Update at expiration"); // 게임 시간이 만료 기한에 도달한 첫 입력 갱신 대기
            Require(pendingBeforeSkillUpdate && !consumedExpiredLine && expirationApplied,
                "An overdue Line must be rejected and naturally expired by the handoff API before Skill.Update can clear it.");
            remaining = skills.DrawCooldownRemaining;
            Require(!skills.ConsumeLine(out _) && skills.DrawCooldownRemaining == remaining && vfx.IsExpiryPlaying,
                "Repeated handoff after boundary expiry must not restart its short cooldown or expiration VFX.");
            passed("F-P06 Line handoff returns live geometry once with the normal cooldown and rejects overdue geometry before Skill.Update with natural expiry");
        }
        finally
        {
            // 검사 상태를 지우고 기존 서비스·체력·튜닝값 복구
            if (beforeInputUpdate != null) InputSystem.onBeforeUpdate -= beforeInputUpdate; // 실패·중단에서도 만료 경계 수집 해제
            skills.enabled = true;
            vfx.enabled = true;
            player.IsCutScene = false;
            player.IsDead = false;
            skills.ResetSkills();
            skills.LineLifetime = lifetime;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.NormalDrawCooldown = normalCooldown;
            skills.ConfirmSlashCooldown = slashCooldown;
            vfx.WarningFraction = warningFraction;
            vfx.SlowBlinkPeriod = slowPeriod;
            vfx.FastBlinkPeriod = fastPeriod;
            vfx.ExpiryDuration = expiryDuration;
            player.HP = health;
            player.IsInvulnerable = invulnerable;
            manager.SetDrawPaused(false);
            manager.SetGameplayPaused(paused);
            input.BlockInput = blocked;
        }
    }

    /// <summary>실제 Draw 확정 API로 시작점·방향을 Player 보관 경로로 변환</summary>
    private static Line Confirm(PlayerCharacter player, Vector2 start, Vector2 direction)
    {
        SkillController skills = player.Skills;
        Require(player.ApplyDraw(), "Character must permit the Draw confirmation used by lifetime checks.");
        Require(skills.PlacePoint(start) && skills.PlacePoint(start + direction), "Two valid points must be accepted.");
        Line expected = skills.Drawer.PreviewLine.Value;
        Require(skills.ConfirmLine(), "The manager must accept valid confirmed geometry.");
        return expected;
    }

    /// <summary>확정 경로의 데이터가 같은 값인지 비교</summary>
    private static bool SameLine(Line first, Line second)
    {
        return Vector2.Distance(first.StartPoint, second.StartPoint) < 0.001f && Vector2.Distance(first.Direction, second.Direction) < 0.001f
            && Mathf.Abs(first.Length - second.Length) < 0.001f && Mathf.Abs(first.Width - second.Width) < 0.001f;
    }

    /// <summary>실제 게임 시간이 흐르는 동안 수명 진행 관찰</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game time"); // Skill.Update에 사용하는 게임 시간 경과 대기
    }

    /// <summary>게임 시간 정지와 별도로 실제 경과 시간 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real time"); // 정지 중 데이터·VFX 위상 보존을 관찰할 실제 시간 대기
    }

    /// <summary>지정한 렌더 프레임만큼 입력·시간 변경 반영 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 상태 전환 프레임의 시간 샘플이 반영될 때까지 대기
    }

    /// <summary>기존 Editor 실행기에서 상태 완료와 제한 시간 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>수명·원본 데이터·표현 계약 위반을 기존 검사 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
