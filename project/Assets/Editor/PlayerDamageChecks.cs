using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>실제 피해 수신과 독립 피격 보호·Sprite 깜빡임·정지·사망 경계 확인</summary>
public static class PlayerDamageChecks
{
    /// <summary>기존 Play Mode 실행기를 재사용하고 피격 설정·색·수치·세션 상태 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Action<string> passed)
    {
        // 실제 수신 영역과 복원할 Player·표현·능력 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Hitbox hitbox = GetPlayerHitbox(player);
        SkillController skills = player.Skills;
        SpriteRenderer[] sprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        Require(sprites.Length > 0 && hitbox != null, "Damage checks require the prepared Player Sprite renderers and actual Hitbox.");
        Color[] originalColors = new Color[sprites.Length];
        Color[] palette = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
            palette[i] = new Color(0.2f + 0.1f * (i % 4), 0.6f - 0.05f * (i % 3), 0.8f, 0.45f + 0.1f * (i % 4));
        }
        Vector2 position = body.position;
        Vector2 aim = player.AimWorldPosition;
        float health = player.HP;
        float speed = player.SP;
        float protectionDuration = player.HitProtectionDuration;
        float flashInterval = rendering.HitFlashInterval;
        Color flashColor = rendering.HitFlashColor;
        float cancelCooldown = skills.CancelDrawCooldown;
        float slashDuration = skills.SlashDuration;
        bool invulnerable = player.IsInvulnerable;
        bool inputEnabled = input.enabled;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        int damageEvents = 0;
        int trueEvents = 0;
        int falseEvents = 0;
        int deathEvents = 0;
        Action<DamageInstance> countDamage = value => damageEvents++;
        Action<bool> countProtection = value => { if (value) trueEvents++; else falseEvents++; };
        Action countDeath = () => deathEvents++;
        Action beforeInputUpdate = null;
        GameObject publisherFixture = null;
        player.OnDamaged += countDamage; // 실제 체력 감소 통지가 발생한 횟수 수집
        player.OnHitProtectionChanged += countProtection; // 보호 진입·종료 통지가 갱신과 구분되는지 수집
        player.OnDie += countDeath; // 실제 치명 피해의 최초 사망 통지 수집
        try
        {
            // 입력 간섭 없이 실제 피해·행동 API를 검사할 정상 Player 상태 준비
            input.enabled = false;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            skills.ResetSkills();
            player.InitializeHealth(player.maxHP);
            player.IsInvulnerable = false;
            player.HitProtectionDuration = 0.55f;
            rendering.HitFlashInterval = 0.06f;
            rendering.HitFlashColor = Color.red;
            skills.CancelDrawCooldown = 0f;
            skills.SlashDuration = 0.3f;
            SetPosition(body, new Vector2(180f, 180f));
            SetColors(sprites, palette);
            yield return Frames(2); // 이전 검사 입력과 위치·색 변경이 반영된 뒤 피해 검사 시작
            int initialDamageEvents = damageEvents;
            float initialHealth = player.HP;
            float[] invalid = { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float amount in invalid)
                Require(!player.ApplyDamage(Damage(manager, amount, false)), "Invalid damage must be rejected before changing HP or publishing events.");
            Require(player.HP == initialHealth && damageEvents == initialDamageEvents && !player.IsHitProtected && !rendering.IsHitFlashing,
                "Rejected damage must not start protection or flashing.");

            // 실제 사각 질의의 첫 피해는 보호를 먼저 시작해 같은 콜백 안의 재진입도 거부
            bool committedBeforeCallback = false;
            bool nestedHitAccepted = false;
            Action<DamageInstance> reenter = value =>
            {
                committedBeforeCallback = player.HP == initialHealth - 3f && player.IsHitProtected;
                nestedHitAccepted = hitbox.ReceiveHit(value);
            };
            player.OnDamaged += reenter; // 최초 수신 콜백에서 같은 Hitbox 피해를 재진입시켜 즉시 보호 확인
            try
            {
                var query = new CollisionBox();
                query.Begin(Damage(manager, 3f, true), ~0);
                Require(query.Query(body.position, Vector2.one, 0f) == 1, "A live unprotected Player must receive the actual Boss box query.");
                Require(query.Query(body.position, Vector2.one, 0f) == 0 && !hitbox.ReceiveHit(Damage(manager, 3f, true)),
                    "The same attack and a new Hitbox hit must both reject additional damage during protection.");
                query.End();
            }
            finally
            {
                player.OnDamaged -= reenter; // 즉시 보호의 재진입 수집 종료
            }
            Require(committedBeforeCallback && !nestedHitAccepted && damageEvents == initialDamageEvents + 1 && player.HP == initialHealth - 3f
                && trueEvents == 1 && rendering.IsHitFlashing && !player.IsInvulnerable,
                "HP and Hitbox-only protection must be committed before external OnDamaged callbacks without changing full invulnerability.");
            yield return GameSeconds(0.08f); // 효과 재피격이 갱신할 일부 소모된 보호 시간 준비
            float beforeRefresh = player.HitProtectionRemaining;
            Require(player.ApplyDamage(Damage(manager, 2f, false)) && player.HitProtectionRemaining > beforeRefresh
                && player.HitProtectionElapsed == 0f && trueEvents == 1 && damageEvents == initialDamageEvents + 2,
                "Effect damage must bypass Hitbox protection and refresh its duration without a duplicate entry notification.");
            passed("F-P08 actual Hitbox damage commits HP and immediate protection before callbacks, blocks duplicate hits and refreshes protection on valid effect damage");

            // 피격은 행동 경직 없이 이동·공격을 허용하며 각 Sprite의 alpha를 유지해 빨강·원본색 반복
            player.ResetMotion();
            SetColors(sprites, palette);
            Require(player.ApplyDamage(Damage(manager, 1f, false)) && player.ApplyAttack(), "Protection must not prevent starting an ordinary attack.");
            player.SP = 2f;
            player.SetMoveInput(Vector2.right);
            float beforeMove = body.position.x;
            yield return GameSeconds(0.08f); // 피격 보호 중 실제 Character 이동과 무기 행동 진행 관찰
            Require(body.position.x > beforeMove + 0.01f && player.IsAttacking, "Taking damage must not impose movement or attack stagger.");
            player.SetMoveInput(Vector2.zero);
            movement.Stop();
            player.CancelAttack();
            bool sawRed = false;
            bool sawOriginal = false;
            float colorDeadline = Time.realtimeSinceStartup + 8f;
            while (player.IsHitProtected)
            {
                if (Time.realtimeSinceStartup > colorDeadline) throw new TimeoutException("F-P08 damage colors did not finish.");
                bool red = SameColor(sprites[0].color, RedWithAlpha(palette[0]));
                bool original = SameColor(sprites[0].color, palette[0]);
                Require(red || original, "A damage flash frame must show red or its captured original color.");
                AssertColors(sprites, palette, red);
                sawRed |= red;
                sawOriginal |= original;
                yield return null;
            }
            yield return Until(() => !rendering.IsHitFlashing, "damage flash finishes"); // 보호 종료 통지 뒤 마지막 색 복구가 반영될 때까지 대기
            AssertColors(sprites, palette, false);
            Require(sawRed && sawOriginal, "Actual Sprite renderers must alternate red and original RGB while preserving each original alpha.");
            passed("F-P08 damage adds no movement or attack stagger and actual body and weapon Sprites alternate red with their own original colors and alpha");

            // Dodge·피격 보호는 Hitbox만 막고 외부 전면 무적은 효과까지 차단
            player.ResetMotion();
            Require(player.ApplyDash() && player.IsDodge && !hitbox.ReceiveHit(Damage(manager, 1f, true)), "Dodge must reject Hitbox damage.");
            Require(player.ApplyDamage(Damage(manager, 1f, false)) && player.IsHitProtected, "Dodge must still allow effect damage and its hit protection.");
            player.CancelDash();
            Require(player.IsHitProtected && !player.IsDodge && !hitbox.ReceiveHit(Damage(manager, 1f, true)), "Ending Dodge must preserve independently owned hit protection.");
            player.IsInvulnerable = true;
            Require(!player.ApplyDamage(Damage(manager, 1f, false)) && !hitbox.ReceiveHit(Damage(manager, 1f, true)), "Full invulnerability must reject both effect and Hitbox damage.");
            player.IsInvulnerable = false;
            Require(player.IsHitProtected && !player.IsInvulnerable && player.ApplyDamage(Damage(manager, 1f, false)),
                "Clearing external invulnerability must not clear hit protection or block effect damage.");

            // Player.Update 전에 끝난 보호는 읽기 API와 실제 Hitbox 수신에서 즉시 만료로 판단
            player.ResetMotion();
            player.HitProtectionDuration = 0.18f;
            Require(hitbox.ReceiveHit(Damage(manager, 1f, true)), "The expiration boundary must begin with one accepted Hitbox hit.");
            double expires = Time.timeAsDouble + player.HitProtectionDuration;
            int beforeFalseEvents = falseEvents;
            int beforeTrueEvents = trueEvents;
            bool boundaryReached = false;
            bool expiredBeforeUpdate = false;
            bool boundaryAccepted = false;
            beforeInputUpdate = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic || Time.timeAsDouble < expires) return;
                expiredBeforeUpdate = !player.IsHitProtected && falseEvents == beforeFalseEvents;
                boundaryAccepted = hitbox.ReceiveHit(Damage(manager, 1f, true));
                boundaryReached = true;
                InputSystem.onBeforeUpdate -= beforeInputUpdate; // 만료 첫 입력 프레임에서 한 번 요청한 뒤 수집 종료
            };
            InputSystem.onBeforeUpdate += beforeInputUpdate; // Player.Update보다 먼저 도착한 실제 Hitbox의 보호 만료 판정 수집
            yield return Until(() => boundaryReached, "hit protection expiry before Player.Update"); // 현재 게임 시각이 보호 만료 기한에 도달한 첫 입력 갱신 대기
            Require(expiredBeforeUpdate && boundaryAccepted && falseEvents == beforeFalseEvents + 1 && trueEvents == beforeTrueEvents + 1,
                "An expired protection must not reject a pre-Update hit and its stale entry must close before a fresh entry is published.");
            passed("F-P08 Dodge, hit protection and full invulnerability keep separate scopes and an expired protection accepts hits before Player Update");

            // Draw·일반 pause·Slash는 같은 게임 시간 보호와 표현 위상을 보존
            player.ResetMotion();
            skills.ResetSkills();
            player.HitProtectionDuration = 0.8f;
            Require(player.ApplyDamage(Damage(manager, 1f, false)) && player.ApplyDraw(), "A protected Player must still enter Draw.");
            yield return Frames(1); // Draw 정지 진입 프레임 뒤 같은 게임 시각에서 보호·색 비교 시작
            float frozenRemaining = player.HitProtectionRemaining;
            float frozenElapsed = player.HitProtectionElapsed;
            Color frozenColor = sprites[0].color;
            yield return RealSeconds(0.08f); // Draw 정지 중 보호·깜빡임의 게임 시간 보존 관찰
            Require(player.HitProtectionRemaining == frozenRemaining && player.HitProtectionElapsed == frozenElapsed && SameColor(sprites[0].color, frozenColor),
                "Draw must freeze both hit protection and its visible flash phase.");
            player.CancelDraw();
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 일반 정지의 시간 샘플이 반영된 뒤 비교 시작
            frozenRemaining = player.HitProtectionRemaining;
            frozenElapsed = player.HitProtectionElapsed;
            frozenColor = sprites[0].color;
            yield return RealSeconds(0.08f); // 일반 pause도 같은 보호·표현 위상을 멈추는지 관찰
            Require(player.HitProtectionRemaining == frozenRemaining && player.HitProtectionElapsed == frozenElapsed && SameColor(sprites[0].color, frozenColor),
                "Ordinary pause must freeze the same protection clock and Sprite color phase.");
            manager.SetGameplayPaused(false);
            yield return GameSeconds(0.08f); // 정지를 해제한 뒤 같은 잔여 시간에서 보호 재개 관찰
            Require(player.HitProtectionRemaining < frozenRemaining, "Hit protection must resume after the pause is released.");
            skills.SetLine(new Line((Vector2)player.transform.position + Vector2.up * 10f, Vector2.right, 6f, 0.25f));
            Require(player.ApplySlash(), "A protected Player must still be allowed to start Slash.");
            yield return Frames(1); // Slash 정지 진입 프레임 뒤 보호 시간을 보관
            frozenRemaining = player.HitProtectionRemaining;
            frozenElapsed = player.HitProtectionElapsed;
            Require(!player.ApplyDamage(Damage(manager, 1f, false)), "Slash full protection must reject effect damage without refreshing hit protection.");
            yield return RealSeconds(0.07f); // unscaled Slash가 진행되는 동안 피격 보호 수명은 정지하는지 관찰
            Require(player.IsSlashing && player.HitProtectionRemaining == frozenRemaining && player.HitProtectionElapsed == frozenElapsed,
                "Slash must freeze existing hit protection while its own full protection remains separate.");
            yield return Until(() => !player.IsSlashing, "protected Slash completes"); // Slash 완료 뒤 이전 피격 보호가 유지되는지 확인
            Require(player.IsHitProtected && !player.IsInvulnerable, "Slash completion must release only Slash full protection.");
            passed("F-P08 Draw, ordinary pause and Slash freeze the hit-protection clock and flash phase while Slash restores its own full protection independently");

            // 표현만 끄면 색을 복구하고 논리 시간은 계속 진행, Player를 끄거나 재배치하면 보호 정리
            player.ResetMotion();
            Require(player.ApplyDamage(Damage(manager, 1f, false)), "Renderer suspension must begin with valid damage.");
            rendering.enabled = false;
            Require(!rendering.IsHitFlashing, "Disabling rendering must release its flash presentation.");
            AssertColors(sprites, palette, false);
            float remainingWhileHidden = player.HitProtectionRemaining;
            yield return GameSeconds(0.08f); // Rendering 비활성화와 무관하게 Player의 보호 시간이 흐르는지 관찰
            Require(player.IsHitProtected && player.HitProtectionRemaining < remainingWhileHidden, "Disabling rendering must not stop Player-owned protection time.");
            Require(player.ApplyDamage(Damage(manager, 1f, false)), "Effect damage must refresh protection even while rendering is disabled.");
            AssertColors(sprites, palette, false);
            rendering.enabled = true;
            Require(rendering.IsHitFlashing, "Re-enabling rendering must recover the current protection state.");
            rendering.enabled = false;
            rendering.enabled = true;
            yield return Frames(2); // 반복 활성화에도 같은 보호 상태의 표현만 복구되는지 관찰
            player.ResetHitProtection();
            AssertColors(sprites, palette, false);
            Require(!rendering.IsHitFlashing && !player.IsHitProtected, "Reset must restore original colors after repeated renderer subscriptions.");
            Require(player.ApplyDamage(Damage(manager, 1f, false)), "Player disable must begin with active protection.");
            player.enabled = false;
            Require(!player.IsHitProtected && player.HitProtectionRemaining == 0f && !rendering.IsHitFlashing,
                "Disabling the Player must synchronously clear its protection and notify presentation.");
            AssertColors(sprites, palette, false);
            player.enabled = true;
            Require(!player.IsHitProtected && player.ApplyDamage(Damage(manager, 1f, false)), "Re-enabled Player must not inherit stale protection.");
            player.ResetMotion();
            Require(!player.IsHitProtected && !rendering.IsHitFlashing, "Motion reset used by relocation must clear hit protection and its colors.");
            AssertColors(sprites, palette, false);
            passed("F-P08 rendering disable restores colors while Player protection continues, re-enable resumes it and Player disable or relocation clears all hit state");

            // 실제 치명 피해는 피해·사망을 한 번씩 알리고 초기화·회복은 피해 알림을 만들지 않음
            player.HP = 5f;
            int damagesBeforeDeath = damageEvents;
            int deathsBeforeDeath = deathEvents;
            Require(player.ApplyDamage(Damage(manager, 10f, false)) && player.IsDead && player.HP == 0f,
                "A valid lethal effect must commit the actual dead state.");
            Require(damageEvents == damagesBeforeDeath + 1 && deathEvents == deathsBeforeDeath + 1
                && !player.IsHitProtected && !rendering.IsHitFlashing && !player.ApplyDamage(Damage(manager, 1f, false)),
                "Fatal damage must publish one death, leave no live flash and reject repeated damage.");
            AssertColors(sprites, palette, false);
            player.InitializeHealth(health);
            player.Heal(1f);
            Require(damageEvents == damagesBeforeDeath + 1 && deathEvents == deathsBeforeDeath + 1 && !player.IsHitProtected,
                "Health initialization and healing must not publish damage or resurrect stale protection.");

            // 보호가 없는 공통 수신자의 피해 콜백이 치명 피해를 재진입해도 최초 사망 통지는 한 번
            publisherFixture = new GameObject("DamagePublisherReentryTarget");
            Character publisher = publisherFixture.AddComponent<Character>();
            publisher.InitializeHealth(10f);
            int publisherDamages = 0;
            int publisherDeaths = 0;
            bool nestedLethalAccepted = false;
            Action<DamageInstance> nestedLethal = value =>
            {
                publisherDamages++;
                if (publisherDamages == 1) nestedLethalAccepted = publisher.ApplyDamage(Damage(manager, 20f, false));
            };
            Action publisherDeath = () => publisherDeaths++;
            publisher.OnDamaged += nestedLethal; // 외부 피해 구독자에서 공통 수신자에게 치명 피해 재진입
            publisher.OnDie += publisherDeath; // 재진입 후 바깥 호출이 사망 알림을 중복하지 않는지 수집
            try
            {
                Require(publisher.ApplyDamage(Damage(manager, 1f, false)) && nestedLethalAccepted && publisher.IsDead
                    && publisherDamages == 2 && publisherDeaths == 1, "Nested lethal damage must publish two real damage events and only one death.");
            }
            finally
            {
                publisher.OnDamaged -= nestedLethal; // 공통 발행자의 재진입 수집 종료
                publisher.OnDie -= publisherDeath; // 최초 사망 수집 종료
            }
            passed("F-P08 real lethal damage publishes death once, restores colors and health initialization stays separate from nested damage notifications");
        }
        finally
        {
            // 실패·중단에서도 입력 경계 수집과 전용 연출·보호 상태 정리
            if (beforeInputUpdate != null) InputSystem.onBeforeUpdate -= beforeInputUpdate; // 만료 경계 수집 해제
            rendering.enabled = player.enabled = true;
            player.CancelSlash();
            player.CancelDraw();
            skills.ResetSkills();
            player.ResetMotion();
            player.OnDamaged -= countDamage; // 실제 피해 통지 수집 종료
            player.OnHitProtectionChanged -= countProtection; // 보호 진입·종료 수집 종료
            player.OnDie -= countDeath; // 실제 사망 수집 종료
            if (publisherFixture != null) UnityEngine.Object.DestroyImmediate(publisherFixture);

            // 기존 Sprite 색·Inspector 설정·체력·위치와 서비스 상태 복원
            player.IsDead = false;
            player.IsCutScene = false;
            player.InitializeHealth(health);
            player.SP = speed;
            player.IsInvulnerable = invulnerable;
            player.HitProtectionDuration = protectionDuration;
            rendering.HitFlashInterval = flashInterval;
            rendering.HitFlashColor = flashColor;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.SlashDuration = slashDuration;
            SetColors(sprites, originalColors);
            SetPosition(body, position);
            player.SetAimPosition(aim);
            manager.SetGameplayPaused(paused);
            input.enabled = inputEnabled;
            input.BlockInput = blocked;
        }
    }

    /// <summary>현재 세션의 Boss를 출처로 실제 수신 API에 전달할 피해 구성</summary>
    private static DamageInstance Damage(GameManager manager, float amount, bool hitbox)
    {
        return new DamageInstance(manager.CurrentBoss, amount, hitbox ? DamageType.Normal : DamageType.Effect, DamageInstance.NextAttackId(), hitbox);
    }

    /// <summary>Player 프리팹에 연결된 실제 활성 피격 영역 조회</summary>
    private static Hitbox GetPlayerHitbox(PlayerCharacter player)
    {
        foreach (Hitbox hitbox in player.GetComponentsInChildren<Hitbox>())
            if (hitbox.Owner == player && hitbox.IsEnabled && hitbox.isActiveAndEnabled) return hitbox;
        return null;
    }

    /// <summary>각 Sprite에 임시 원본색 또는 복원할 색 배열 적용</summary>
    private static void SetColors(SpriteRenderer[] sprites, Color[] colors)
    {
        for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null) sprites[i].color = colors[i];
    }

    /// <summary>빨강 표시에도 피격 전 Sprite의 alpha 유지</summary>
    private static Color RedWithAlpha(Color original)
    {
        return new Color(1f, 0f, 0f, original.a);
    }

    /// <summary>실제 몸체·무기 색과 alpha가 같은 위상으로 표시되는지 확인</summary>
    private static void AssertColors(SpriteRenderer[] sprites, Color[] palette, bool red)
    {
        for (int i = 0; i < sprites.Length; i++)
            Require(SameColor(sprites[i].color, red ? RedWithAlpha(palette[i]) : palette[i]), "Every cached Sprite must preserve its own original color and alpha across the same flash phase.");
    }

    /// <summary>Sprite 색의 네 성분을 허용 오차 안에서 비교</summary>
    private static bool SameColor(Color first, Color second)
    {
        return Mathf.Abs(first.r - second.r) < 0.001f && Mathf.Abs(first.g - second.g) < 0.001f
            && Mathf.Abs(first.b - second.b) < 0.001f && Mathf.Abs(first.a - second.a) < 0.001f;
    }

    /// <summary>Transform과 Rigidbody의 검사 위치를 같은 순간에 적용</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>입력·Player·Rendering의 실제 렌더 프레임 진행 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 상태 전환과 Sprite 갱신이 지정한 횟수만큼 지날 때까지 대기
    }

    /// <summary>보호와 같은 게임 시간으로 대기</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game seconds"); // 실제 Player 보호·이동 시간이 진행될 때까지 대기
    }

    /// <summary>게임 정지 상태에서 실제 시간 경과 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 게임 시간이 멈춘 동안 보호·색 보존 관찰
    }

    /// <summary>기존 검사 루프에서 상태 완료를 기다리고 무한 대기 방지</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P08 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>피해·보호·표현·통지 계약 위반을 기존 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
