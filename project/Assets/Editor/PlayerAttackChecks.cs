using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>실제 공격 입력·콤보·판정·행동 중단을 기존 Play Mode 검사에서 확인</summary>
public static class PlayerAttackChecks
{
    /// <summary>이동 검사의 입력 장치를 재사용하고 공격 설정·수치·임시 대상을 원상 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, PlayerInput actions, Keyboard keyboard, Mouse mouse, Camera camera, Action<string> passed)
    {
        // 검사 후 복구할 실제 캐릭터·무기 설정과 입력 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        AttackController attack = player.GetComponent<AttackController>();
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        InputAction attackAction = actions.actions.FindAction("Player/Attack", true);
        Vector2 position = body.position;
        float damage = player.AD;
        float speed = player.SP;
        float attackSpeed = player.AS;
        float health = player.HP;
        float duration = attack.AttackDuration;
        float hitStart = attack.HitWindowStart;
        float hitEnd = attack.HitWindowEnd;
        float cooldown = attack.AttackCooldown;
        float comboDuration = attack.ComboDuration;
        float moveMultiplier = attack.AttackMoveMultiplier;
        Vector2 boxSize = attack.HitboxSize;
        Vector2 boxOffset = attack.HitboxOffset;
        LayerMask targetMask = attack.TargetMask;
        bool wasBlocked = input.BlockInput;
        var fixtures = new List<GameObject>();
        int performed = 0;
        Action<InputAction.CallbackContext> onAttack = context => performed++;
        attackAction.performed += onAttack; // 실제 왼쪽 버튼 입력이 공격 액션까지 도착하는지 수집
        try
        {
            // 맵의 실제 전투 대상과 떨어진 곳에서 짧은 공격 시간으로 검사 준비
            Require(attackAction.enabled && attack.WeaponPivot != null && attack.WeaponMotion != null
                && attack.WeaponAnimator != null && attack.WeaponAnimator.runtimeAnimatorController != null,
                "The Attack action and weapon animation clips must be connected.");
            player.ResetMotion();
            SetPosition(body, new Vector2(40f, 40f));
            player.SP = 0f;
            player.AS = 1f;
            attack.AttackDuration = 0.3f;
            attack.HitWindowStart = 0.08f;
            attack.HitWindowEnd = 0.2f;
            attack.AttackCooldown = 0.04f;
            attack.ComboDuration = 0.35f;
            attack.TargetMask = ~0;
            input.enabled = true;
            input.BlockInput = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            MouseInput(mouse, camera, body.position + Vector2.right * 2f, true);
            yield return Until(() => player.IsAttacking && performed > 0, "left mouse attack callback"); // 실제 버튼이 Input·Character를 거쳐 무기를 시작할 때까지 대기
            Require(input.AttackHeld && attack.ComboIndex == 0 && Vector2.Dot(attack.AttackDirection, Vector2.right) > 0.99f, "The first held attack must use the latest cursor position and begin combo 1.");
            float aimAngle = attack.WeaponPivot.eulerAngles.z;
            float weaponAngle = attack.WeaponMotion.localEulerAngles.z;
            MouseInput(mouse, camera, body.position + Vector2.up * 2f, true);
            yield return Frames(2); // 공격 중 커서를 바꿔도 시작 방향이 보존되는지 관찰
            Require(Vector2.Dot(attack.AttackDirection, Vector2.right) > 0.99f, "Moving the cursor during a swing must not turn its hit direction.");
            Require(Mathf.Abs(Mathf.DeltaAngle(weaponAngle, attack.WeaponMotion.localEulerAngles.z)) > 0.1f
                && Mathf.Abs(Mathf.DeltaAngle(aimAngle, attack.WeaponPivot.eulerAngles.z)) < 0.01f,
                "The weapon clip must animate its child while aim and damage direction stay fixed.");
            MouseInput(mouse, camera, body.position + Vector2.up * 2f, false);
            yield return Until(() => !input.AttackHeld, "left mouse release callback"); // 버튼 해제가 현재 공격 취소와 구별되는지 확인할 준비
            Require(player.IsAttacking, "Releasing the button must allow the current swing to finish.");
            yield return Until(() => !player.IsAttacking, "released swing completion"); // 해제 전에 시작한 한 타의 정상 종료 대기
            yield return Frames(3); // 홀드가 끝난 뒤 다음 타가 자동 시작하지 않는지 관찰
            Require(attack.ComboIndex == 0 && !player.IsAttacking, "Releasing attack must stop the next swing.");
            passed("F-P03 real left mouse input reaches Character, fixes swing direction and finishes the current hit after release");

            // 홀드 반복의 세 콤보 순환과 시간 만료·강제 초기화 확인
            player.CancelAttack();
            MouseInput(mouse, camera, body.position + Vector2.right * 2f, true);
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "held combo 1"); // 홀드를 새로 시작한 첫 타 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 1, "held combo 2"); // 같은 홀드로 두 번째 타가 이어지는지 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 2, "held combo 3"); // 같은 홀드로 세 번째 타가 이어지는지 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "held combo loop"); // 세 번째 타 다음 첫 타로 순환하는지 대기
            MouseInput(mouse, camera, body.position + Vector2.right * 2f, false);
            yield return Until(() => !input.AttackHeld && !attack.IsAttacking, "held combo release"); // 마지막으로 시작한 타만 끝내고 반복을 중지할 때까지 대기
            yield return Until(() => attack.ComboIndex == -1 && attack.ComboRemaining == 0f, "combo timeout"); // 정해진 보존 시간이 지나 콤보가 초기화되는지 대기
            input.enabled = false;
            Start(player, body.position + Vector2.right);
            player.CancelAttack();
            Require(!attack.IsAttacking && attack.ComboIndex == -1 && !attack.HitBox.IsActive, "Forced cancellation must close the hit window and reset the combo.");
            passed("F-P03 held attack cycles combo 1-2-3-1 and release, timeout or cancellation reset the correct state");

            // 현재 공격력·공격속도와 공격 중 느린 이동·대시 제한 확인
            Character target = CreateTarget(fixtures, "AttackStatsTarget", body.position + Vector2.right, TargetKind.Boss, 3);
            attack.AttackCooldown = 0.2f;
            player.AD = 7f;
            Start(player, body.position + Vector2.right);
            float normalBegan = Time.time;
            yield return Until(() => !attack.IsAttacking, "normal attack duration"); // 기본 AS에서 공격 한 타가 끝나는 시간 측정
            float normalElapsed = Time.time - normalBegan;
            float normalCooldownBegan = Time.time;
            yield return Until(() => attack.AttackCooldownRemaining == 0f, "normal attack cooldown"); // AS 1에서 공격 회복 뒤 재사용까지 걸리는 게임 시간 측정
            float normalCooldown = Time.time - normalCooldownBegan;
            Require(Mathf.Abs(target.HP - 993f) < 0.001f, "Current AD must deal damage once even when the target owns multiple Colliders.");
            player.CancelAttack();
            player.AD = 13f;
            player.AS = 2f;
            Start(player, body.position + Vector2.right);
            float fastBegan = Time.time;
            yield return Until(() => !attack.IsAttacking, "fast attack duration"); // AS를 두 배로 바꾼 공격 시간 측정
            float fastElapsed = Time.time - fastBegan;
            float fastCooldownBegan = Time.time;
            yield return Until(() => attack.AttackCooldownRemaining == 0f, "fast attack cooldown"); // AS 2에서 공격 사이 대기가 줄어드는지 게임 시간 측정
            float fastCooldown = Time.time - fastCooldownBegan;
            Require(fastElapsed < normalElapsed * 0.8f && Mathf.Abs(target.HP - 980f) < 0.001f, $"AS must speed the swing and each new attack must read current AD: normal={normalElapsed}, fast={fastElapsed}, HP={target.HP}.");
            Require(normalCooldown > 0f && fastCooldown > 0f && fastCooldown < normalCooldown * 0.8f, "AS must scale the recovery cooldown as well as the swing.");
            player.CancelAttack();
            player.AS = 1f;
            player.SP = 4f;
            attack.AttackDuration = 0.8f;
            attack.AttackMoveMultiplier = 0.25f;
            Start(player, body.position + Vector2.up);
            player.SetMoveInput(Vector2.right);
            Require(!player.ApplyDash(), "Dash must not cancel an active attack.");
            Vector2 begin = body.position;
            float beganAt = Time.time;
            yield return GameSeconds(0.25f); // 공격이 유지되는 동안 요청·누적·적용된 실제 이동 거리 측정
            float elapsed = Time.time - beganAt;
            float expected = player.SP * attack.AttackMoveMultiplier * elapsed;
            float tolerance = player.SP * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) * 2f + 0.02f;
            Require(player.IsAttacking && Mathf.Abs(Vector2.Distance(begin, body.position) - expected) < tolerance, "Character must calculate slowed attack movement through the normal Body queue.");
            player.SetMoveInput(Vector2.zero);
            player.CancelAttack();
            player.SP = 0f;
            passed("F-P03 current AD and AS control damage and timing while Character applies slowed movement and rejects Dash");

            // 지정된 판정 시간과 회전한 박스 오프셋이 실제 피해에 일치하는지 검사
            SetPosition(body, new Vector2(50f, 50f));
            attack.AttackDuration = 0.5f;
            attack.HitWindowStart = 0.18f;
            attack.HitWindowEnd = 0.32f;
            attack.HitboxSize = new Vector2(1.6f, 0.3f);
            attack.HitboxOffset = Vector2.right;
            Character inside = CreateTarget(fixtures, "RotatedAttackInside", body.position + Vector2.up * 1.5f, TargetKind.Boss);
            Character outside = CreateTarget(fixtures, "RotatedAttackOutside", body.position + Vector2.right * 1.5f, TargetKind.Boss);
            Start(player, body.position + Vector2.up);
            yield return GameSeconds(0.07f); // 판정 시작 전 준비 동작에는 피해가 없는지 확인
            Require(inside.HP == 1000f && !attack.HitBox.IsActive, "The windup must not apply early damage.");
            yield return Until(() => inside.HP < 1000f, "rotated hit window"); // 실제 회전 박스가 열린 뒤 대상에게 피해를 전달할 때까지 대기
            Require(Mathf.Abs(attack.HitBox.Angle - 90f) < 0.01f && Vector2.Distance(attack.HitBox.Area.center, body.position + Vector2.up) < 0.001f && outside.HP == 1000f, "Hit geometry must rotate both box and local offset around the Player.");
            yield return Until(() => !attack.HitBox.IsActive, "hit window close"); // 공격 회복 동작 전에 피해 판정만 닫히는지 대기
            Character late = CreateTarget(fixtures, "LateAttackTarget", body.position + Vector2.up, TargetKind.Boss);
            yield return Until(() => !attack.IsAttacking, "attack recovery"); // 판정 종료 뒤 남은 모션이 끝날 때까지 대기
            Require(late.HP == 1000f, "Targets entering after the hit window must remain unharmed.");
            player.CancelAttack();
            float beforeFastSwing = inside.HP;
            float previousCaptureDelta = Time.captureDeltaTime;
            try
            {
                // 배치의 빠른 프레임에서도 한 Update가 공격 전체를 지나도록 게임 시간 고정
                Time.captureDeltaTime = 0.02f;
                yield return Frames(2); // 고정된 deltaTime이 실제 Player Loop에 반영될 때까지 대기
                player.AS = 1000f;
                Start(player, body.position + Vector2.up);
                yield return Until(() => !attack.IsAttacking, "single-update fast swing"); // Editor 관찰 시점과 무관하게 실제 공격 Update의 완료 대기
                Require(Mathf.Abs(inside.HP - (beforeFastSwing - player.AD)) < 0.001f, "A swing crossing the entire hit window in one Update must still query and hit once.");
            }
            finally { Time.captureDeltaTime = previousCaptureDelta; }
            player.AS = 1f;
            player.CancelAttack();
            passed("F-P03 damage opens only during the hit window and uses the rotated rectangle and local offset");

            // 재사용 Cast 버퍼 포화·대상 중복과 자기·사망·종류 필터 확인
            Vector2 queryCenter = new Vector2(60f, 60f);
            var targets = new List<Character>();
            for (int i = 0; i < 12; i++)
                targets.Add(CreateTarget(fixtures, "BufferTarget" + i, queryCenter + Vector2.right * i * 0.01f, TargetKind.Boss, i == 0 ? 3 : 1));
            Character dead = CreateTarget(fixtures, "DeadAttackTarget", queryCenter, TargetKind.Boss);
            dead.IsDead = true;
            Character friendly = CreateTarget(fixtures, "FriendlyAttackTarget", queryCenter, TargetKind.Player);
            Character projectile = CreateTarget(fixtures, "ProjectileAttackTarget", queryCenter, TargetKind.Projectile);
            Character special = CreateTarget(fixtures, "SpecialAttackTarget", queryCenter, TargetKind.SpecialStructure);
            Character invulnerable = CreateTarget(fixtures, "InvulnerableAttackTarget", queryCenter, TargetKind.Boss);
            invulnerable.IsInvulnerable = true;
            Character disabled = CreateTarget(fixtures, "DisabledAttackTarget", queryCenter, TargetKind.Boss);
            disabled.GetComponentInChildren<Hitbox>().SetEnabled(false);
            CreateTarget(fixtures, "SelfAttackTarget", queryCenter, TargetKind.Boss).GetComponentInChildren<Hitbox>().Owner = player;
            Physics2D.SyncTransforms();
            float playerHP = player.HP;
            var query = new CollisionBox { Results = new RaycastHit2D[2] };
            query.Begin(new DamageInstance(player, 9f, default, 9001), ~0);
            int hitCount = query.Query(queryCenter, new Vector2(2f, 1f), 30f);
            Require(hitCount == targets.Count && query.Results.Length > 2, "A saturated query must expand its reusable buffer and hit every valid receiver.");
            foreach (Character receiver in targets)
                Require(receiver.HP == 991f, "Multiple Colliders of one Character must receive one damage application per swing.");
            Require(query.Query(queryCenter, new Vector2(2f, 1f), 30f) == 0 && targets[0].HP == 991f, "Repeating the same swing query must not damage a receiver twice.");
            Require(dead.HP == 1000f && friendly.HP == 1000f && projectile.HP == 1000f && special.HP == 1000f && disabled.HP == 1000f && invulnerable.HP == 1000f && player.HP == playerHP, "Dead, friendly, projectile, special, disabled, invulnerable and self targets must reject ordinary Player hits.");
            invulnerable.IsInvulnerable = false;
            Require(query.Query(queryCenter, new Vector2(2f, 1f), 30f) == 1 && invulnerable.HP == 991f, "A previously rejected receiver must remain eligible when its invulnerability ends inside the same swing.");
            query.End();
            Require(query.Query(queryCenter, Vector2.one, 0f) == 0, "A closed CollisionBox must not apply damage.");
            player.ResetMotion();
            Require(player.ApplyDash() && player.IsDodge, "A live Player must open its Dodge window for the hit receiver check.");
            query.Begin(new DamageInstance(manager.CurrentBoss, 1f, default, 9002), ~0);
            Require(query.Query(body.position, Vector2.one, 0f) == 0 && player.HP == playerHP, "The Player Hitbox must reject damage while the Dodge window is active.");
            player.CancelDash();
            Require(query.Query(body.position, Vector2.one, 0f) == 1 && player.HP == playerHP - 1f, "The same open attack must hit once after Dodge ends without an earlier rejected hit consuming it.");
            player.HP = playerHP;
            query.End();
            passed("F-P03 reusable box queries grow without omissions, deduplicate receivers and exclude invalid targets");

            // 사망·컷씬·외부 차단·일시정지·비활성화·재배치의 행동 수명 확인
            SetPosition(body, new Vector2(70f, 70f));
            attack.AttackDuration = 0.5f;
            Start(player, body.position + Vector2.right);
            attack.OpenHitWindow();
            player.SetAttackInput(true);
            float beforeDeathHP = player.HP;
            int deathEvents = 0;
            Action onDeath = () => deathEvents++;
            player.OnDie += onDeath; // 실제 치명 피해가 최초 사망 통지를 한 번 발생시키는지 수집
            try
            {
                Require(player.ApplyDamage(new DamageInstance(manager.CurrentBoss, player.HP + 1f, DamageType.Effect, 9003, false)), "The death check must use actual lethal damage.");
                Require(player.IsDead && deathEvents == 1 && !attack.IsAttacking && attack.ComboIndex == -1 && !attack.HitBox.IsActive && !player.ApplyAttack(), "OnDie must synchronously cancel the open attack and Character must reject a new one before another Update.");
                Require(!player.ApplyDamage(new DamageInstance(manager.CurrentBoss, 1f, DamageType.Effect, 9004, false)) && deathEvents == 1, "An already dead Character must not repeat its death event.");
            }
            finally
            {
                player.OnDie -= onDeath; // 최초 사망 횟수 수집 종료
            }
            player.InitializeHealth(beforeDeathHP);
            yield return Frames(2); // 사망 이벤트가 기존 홀드 의도까지 지워 복원 후 자동 재시작하지 않는지 관찰
            Require(!attack.IsAttacking && attack.ComboIndex == -1, "Restoring health must not restart the interrupted held attack.");

            // 컷씬 진입 통지를 한 번만 발행하고 열린 판정과 유지 의도를 즉시 정리
            Start(player, body.position + Vector2.right);
            attack.OpenHitWindow();
            player.SetAttackInput(true);
            int cutsceneEvents = 0;
            Action onCutScene = () => cutsceneEvents++;
            player.OnCutScene += onCutScene; // false에서 true로 바뀐 컷씬 진입 횟수 수집
            try
            {
                player.IsCutScene = true;
                Require(cutsceneEvents == 1 && !attack.IsAttacking && attack.ComboIndex == -1 && !attack.HitBox.IsActive && !player.ApplyAttack(), "OnCutScene must synchronously cancel the open attack and Character must reject a new one before another Update.");
                player.IsCutScene = true;
                Require(cutsceneEvents == 1, "Assigning the same cutscene state must not emit another entry event.");
                player.IsCutScene = false;
                yield return Frames(2); // 컷씬 종료만으로 이전 홀드 공격이 다시 시작하지 않는지 관찰
                Require(!attack.IsAttacking && attack.ComboIndex == -1 && cutsceneEvents == 1, "Ending a cutscene must not emit an entry event or restart the old attack intent.");
                player.IsCutScene = true;
                Require(cutsceneEvents == 2, "A later false-to-true transition must emit one fresh entry event.");
                player.IsCutScene = false;
            }
            finally
            {
                player.OnCutScene -= onCutScene; // 컷씬 진입 횟수 수집 종료
            }

            // 같은 사각 질의의 첫 피해 콜백에서 공격이 중단되면 나머지 대상은 보호
            SetPosition(body, new Vector2(75f, 75f));
            Character interruptedFirst = CreateTarget(fixtures, "InterruptedQueryTargetA", body.position + Vector2.right, TargetKind.Boss);
            Character interruptedSecond = CreateTarget(fixtures, "InterruptedQueryTargetB", body.position + Vector2.right, TargetKind.Boss);
            int interruptedHits = 0;
            bool closedDuringDamage = false;
            Action<DamageInstance> interruptOnDamage = value =>
            {
                interruptedHits++;
                player.IsCutScene = true;
                closedDuringDamage = !attack.IsAttacking && !attack.HitBox.IsActive && attack.ComboIndex == -1;
            };
            interruptedFirst.OnDamaged += interruptOnDamage; // 첫 수신 피해에서 공격자를 컷씬으로 전환
            interruptedSecond.OnDamaged += interruptOnDamage; // 물리 결과 순서와 무관하게 먼저 맞은 대상이 공격 중단
            try
            {
                Start(player, body.position + Vector2.right);
                yield return Until(() => player.IsCutScene, "hit callback cutscene interruption"); // 실제 Attack.Update의 첫 타격이 컷씬을 시작하고 질의를 빠져나올 때까지 대기
                Require(interruptedHits == 1 && Mathf.Abs(interruptedFirst.HP + interruptedSecond.HP - (2000f - player.AD)) < 0.001f, "Ending the attack inside its first damage callback must stop the same query before another receiver takes damage.");
                Require(closedDuringDamage && !attack.IsAttacking && !attack.HitBox.IsActive && attack.HitBox.HitTargets.Count == 0 && attack.ComboIndex == -1 && attack.AttackCooldownRemaining == 0f, "Query-time cutscene entry must synchronously clear the action and remain cancelled after Attack.Update returns from its query.");
                float idleAngle = Mathf.Atan2(attack.AimDirection.y, attack.AimDirection.x) * Mathf.Rad2Deg;
                Require(Mathf.Abs(Mathf.DeltaAngle(attack.WeaponPivot.eulerAngles.z, idleAngle)) < 0.01f
                    && attack.WeaponMotion.localPosition == Vector3.zero
                    && Mathf.Abs(Mathf.DeltaAngle(attack.WeaponMotion.localEulerAngles.z, 0f)) < 0.01f,
                    "Attack.Update must not restore the cancelled swing after a damage callback interrupts it.");
            }
            finally
            {
                interruptedFirst.OnDamaged -= interruptOnDamage; // 질의 도중 중단을 발생시킨 피해 구독 해제
                interruptedSecond.OnDamaged -= interruptOnDamage; // 다른 임시 수신자의 중단 구독도 함께 해제
                player.IsCutScene = false;
            }

            // 외부 입력 차단·정지·컴포넌트 수명·재배치 경로의 기존 동작 유지
            Start(player, body.position + Vector2.right);
            input.BlockInput = true;
            Require(!attack.IsAttacking && attack.ComboIndex == -1, "External input blocking must cancel the current attack immediately.");
            input.BlockInput = false;
            Start(player, body.position + Vector2.right);
            manager.SetGameplayPaused(true);
            float pausedCombo = attack.ComboRemaining;
            yield return RealSeconds(0.12f); // 게임 시간 정지 중 실제 Editor 프레임이 지나도 공격이 유지되는지 관찰
            Require(attack.IsAttacking && attack.ComboRemaining == pausedCombo && !player.ApplyAttack(), "Pause must preserve the swing and stop new attack requests.");
            manager.SetGameplayPaused(false);
            player.CancelAttack();
            Start(player, body.position + Vector2.right);
            attack.enabled = false;
            Require(!attack.IsAttacking && attack.ComboIndex == -1 && !attack.HitBox.IsActive, "Disabling the weapon must close its hit window and combo.");
            attack.enabled = true;
            Start(player, body.position + Vector2.right);
            player.enabled = false;
            Require(!attack.IsAttacking && attack.ComboIndex == -1, "Disabling Character must cancel its weapon action.");
            player.enabled = true;
            Start(player, body.position + Vector2.right);
            movement.enabled = false;
            yield return Until(() => !attack.IsAttacking, "disabled body attack cancellation"); // 물리 대상 비활성화를 Character가 확인한 뒤 공격을 정리할 때까지 대기
            movement.enabled = true;
            Start(player, body.position + Vector2.right);
            player.GetComponent<PlayerSpawner>().playerSpawn();
            Require(!attack.IsAttacking && attack.ComboIndex == -1 && !attack.HitBox.IsActive, "Checkpoint reposition must reset attack and hit state.");
            passed("F-P03 death and cutscene events immediately interrupt attacks, stop in-flight queries and preserve block, pause, disable or checkpoint behavior");
        }
        finally
        {
            // 검사 임시 오브젝트·장치 이벤트와 실제 입력·행동 상태 정리
            attackAction.performed -= onAttack; // 공격 콜백 횟수 수집 종료
            foreach (GameObject fixture in fixtures)
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
            manager.SetGameplayPaused(false);
            MouseInput(mouse, camera, position + Vector2.down, false);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            input.enabled = true;
            input.BlockInput = true;
            player.IsDead = false;
            player.IsCutScene = false;
            player.enabled = true;
            movement.enabled = true;
            attack.enabled = true;
            player.ResetMotion();

            // 프리팹 조정값과 캐릭터 현재 수치·위치 복원
            player.AD = damage;
            player.SP = speed;
            player.AS = attackSpeed;
            player.HP = health;
            attack.AttackDuration = duration;
            attack.HitWindowStart = hitStart;
            attack.HitWindowEnd = hitEnd;
            attack.AttackCooldown = cooldown;
            attack.ComboDuration = comboDuration;
            attack.AttackMoveMultiplier = moveMultiplier;
            attack.HitboxSize = boxSize;
            attack.HitboxOffset = boxOffset;
            attack.TargetMask = targetMask;
            SetPosition(body, position);
            input.BlockInput = wasBlocked;
        }
    }

    /// <summary>전투 수신자만 만들어 PlayerSpawner와 공유 서비스 초기화를 건드리지 않는 판정 대상 준비</summary>
    private static Character CreateTarget(List<GameObject> fixtures, string name, Vector2 position, TargetKind kind, int colliderCount = 1)
    {
        // 수신자를 먼저 준비한 뒤 Collider별 Hitbox를 같은 Character에 연결
        var root = new GameObject(name);
        fixtures.Add(root);
        root.transform.position = position;
        Character receiver = root.AddComponent<Character>();
        receiver.maxHP = 1000f;
        receiver.InitializeHealth(1000f);
        for (int i = 0; i < colliderCount; i++)
        {
            var part = new GameObject("HitPart" + i);
            part.transform.SetParent(root.transform, false);
            BoxCollider2D collider = part.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.08f;
            collider.isTrigger = true;
            Hitbox hitbox = part.AddComponent<Hitbox>();
            hitbox.Owner = receiver;
            hitbox.TargetKind = kind;
        }
        Physics2D.SyncTransforms();
        return receiver;
    }

    /// <summary>조준 좌표와 실제 Character 행동 허용 API로 검사할 공격 시작</summary>
    private static void Start(PlayerCharacter player, Vector2 aim)
    {
        player.SetAimPosition(aim);
        Require(player.ApplyAttack(), "The Character must accept a fresh attack after previous state cleanup.");
    }

    /// <summary>실제 마우스 상태 전체를 전달해 Look과 Attack 액션을 함께 구동</summary>
    private static void MouseInput(Mouse mouse, Camera camera, Vector2 world, bool held)
    {
        MouseState state = new MouseState { position = camera.WorldToScreenPoint(world) }.WithButton(MouseButton.Left, held);
        InputSystem.QueueStateEvent(mouse, state);
    }

    /// <summary>위치 변경 즉시 Transform과 Rigidbody2D의 공간 질의를 일치</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>지정한 렌더 프레임 수만큼 실제 게임 갱신 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int frame = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= frame, "render frames"); // 입력·Character·무기 Update가 지정한 횟수만큼 지날 때까지 대기
    }

    /// <summary>공격 타이머와 같은 게임 시간을 기준으로 상태 관찰</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game seconds"); // 일시정지의 영향을 받는 공격 경과 시간 대기
    }

    /// <summary>일시정지 검사에서 게임 시간과 별개로 실제 프레임 진행 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 게임 시간이 멈춘 채 실제 시간이 지나도록 대기
    }

    /// <summary>기존 Editor 검사 루프에서 실제 상태 변화와 무한 대기 방지</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException("F-P03 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>공격·피격 계약 위반을 기존 검사 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
