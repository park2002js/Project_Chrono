using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>실제 Slash 입력·마지막 판정·Line 소비와 시간·상태 복원을 Play Mode에서 확인</summary>
public static class PlayerSlashChecks
{
    /// <summary>기존 입력 장치와 실행기를 재사용하고 Slash 설정·임시 대상·세션 상태 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, PlayerInput actions, Keyboard keyboard, Mouse mouse, Camera camera, Action<string> passed)
    {
        // 검사 후 복원할 실제 Player·표현·입력 연결과 Inspector 수치 보관
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        DrawVfx vfx = player.GetComponent<DrawVfx>();
        SlashPresentationFx slashFx = player.GetComponent<SlashPresentationFx>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        GameCamera gameCamera = GameCamera.Instance;
        UIController ui = UIController.Instance;
        InputAction slashAction = actions.actions.FindAction("Player/Slash", true);
        Vector2 position = body.position;
        Vector2 aim = player.AimWorldPosition;
        Transform cameraTarget = gameCamera.Target;
        float zoom = camera.orthographicSize;
        float follow = gameCamera.FollowDuration;
        float damage = player.AD;
        float health = player.HP;
        float speed = player.SP;
        float attackSpeed = player.AS;
        float groggy = player.Groggy;
        float duration = skills.SlashDuration;
        float spaceDuration = slashFx.SpaceSlashDuration;
        Material spaceMaterial = slashFx.SpaceMaterial;
        float multiplier = skills.SlashDamageMultiplier;
        LayerMask targetMask = skills.SlashTargetMask;
        float lifetime = skills.LineLifetime;
        float cancelCooldown = skills.CancelDrawCooldown;
        float normalCooldown = skills.NormalDrawCooldown;
        float confirmCooldown = skills.ConfirmSlashCooldown;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        bool invulnerable = player.IsInvulnerable;
        bool playerHud = ui.IsPlayerHudVisible;
        bool bossHud = ui.IsBossHudVisible;
        var fixtures = new List<GameObject>();
        int performed = 0;
        Action<InputAction.CallbackContext> onSlash = context => performed++;
        Action beforeInputUpdate = null;
        slashAction.performed += onSlash; // 실제 F 입력이 Slash 액션에 도착하는지 수집
        try
        {
            // 맵의 전투 대상과 떨어진 곳에서 정상 조작·깨끗한 능력 상태 준비
            Require(!skills.IsDrawing && !player.CurrentLine.HasValue, "Slash checks require the clean Line boundary restored by lifetime checks.");
            input.enabled = true;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            skills.ResetSkills();
            player.ResetMotion();
            player.Buffs.RemoveAll();
            player.IsInvulnerable = false;
            player.AD = 11f;
            player.SP = 0f;
            skills.SlashDuration = 0.35f;
            slashFx.SpaceSlashDuration = 0.25f;
            skills.SlashDamageMultiplier = 3f;
            skills.SlashTargetMask = ~0;
            skills.LineLifetime = 5f;
            skills.CancelDrawCooldown = 0f;
            skills.NormalDrawCooldown = 3f;
            skills.ConfirmSlashCooldown = 0.18f;
            SetPosition(body, new Vector2(120f, 120f));
            gameCamera.Follow(player.transform, true);
            gameCamera.FollowDuration = 0.03f;
            Keys(keyboard);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(body.position + Vector2.right) });
            yield return Frames(3); // 이전 검사 입력 해제와 버프 제거가 반영된 뒤 Slash 시작

            // 실제 F는 한 번만 시작하고 게임 시간 정지 중 마지막 순간까지 피해 보류
            Line line = new Line(body.position, Vector2.right, 6f, 0.6f);
            Character inputTarget = CreateTarget(fixtures, "SlashInputTarget", line.StartPoint + Vector2.right * 3f, TargetKind.Boss, 3);
            Projectile moving = CreateProjectile(fixtures, manager.CurrentBoss, body.position + Vector2.down * 5f, ProjectileSlashResponse.Ignore, 0f);
            moving.Initialize(manager.CurrentBoss, Vector2.right, 2f, 8f, 1f);
            skills.SetLine(line);
            Keys(keyboard, Key.F);
            yield return Until(() => player.IsSlashing && performed > 0, "F starts Slash"); // 실제 F가 Input·Character·Skill을 거쳐 Slash를 시작할 때까지 대기
            GameObject flash = vfx.SlashObject;
            Require(!player.CurrentLine.HasValue && skills.LineRemaining == 0f && skills.DrawCooldownRemaining > 0f
                && !vfx.IsExpiryPlaying && SameLine(skills.Slasher.ActiveLine, line), "Slash must consume the Player Line once and keep its geometry only for the running action.");
            Require(manager.IsSlashPaused && Time.timeScale == 0f && input.IsSlashInput && ui.IsSlashMode && gameCamera.IsSlashMode
                && player.IsInvulnerable && !ui.IsPlayerHudVisible && !ui.IsBossHudVisible,
                "Slash must own its pause, input lock, HUD hiding, camera and full damage protection.");
            Require(flash != null && !vfx.IsSlashPlaying && !flash.activeSelf,
                "Slash must reserve its prepared flash until the final hit completes.");
            Require(vfx.IsSlashLineVisible && vfx.LineObject.activeSelf && vfx.LineOpacity == 1f,
                "Consuming the Line must reserve one attack while retaining its visible map path until the final hit.");
            Require(!player.ApplyDamage(new DamageInstance(manager.CurrentBoss, health + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false))
                && player.HP == health, "Full Slash protection must reject even lethal non-Hitbox damage.");
            Require(!player.ApplyAttack() && !player.ApplyDash() && !player.ApplyDraw() && !player.ApplySlash(), "A running Slash must reject overlapping Player actions.");
            yield return Frames(1); // 게임 정지 진입 프레임을 지난 뒤 고정 시간과 대기 값 보관
            float frozenTime = Time.time;
            float frozenCooldown = skills.DrawCooldownRemaining;
            Vector2 frozenPosition = body.position;
            Vector3 frozenBulletPosition = moving.transform.position;
            float frozenBulletLifetime = moving.RemainingLifetime;
            yield return RealSeconds(0.07f); // unscaled 연출만 진행하는 동안 판정·이동·게임 대기 보류 관찰
            Require(player.IsSlashing && Time.time == frozenTime && skills.DrawCooldownRemaining == frozenCooldown
                && body.position == frozenPosition && inputTarget.HP == 1000f,
                "Before the last Slash moment, game time, cooldown, movement and target HP must remain frozen.");
            Require(moving.transform.position == frozenBulletPosition && moving.RemainingLifetime == frozenBulletLifetime,
                "A moving projectile outside the Slash area must freeze its movement and lifetime during the action.");
            Require(camera.orthographicSize < zoom, "Slash camera zoom must advance while game time is frozen.");
            Require(vfx.IsSlashLineVisible && vfx.LineObject.activeSelf,
                "The original map Line must remain visible while unscaled Slash presentation advances.");
            yield return Until(() => !player.IsSlashing, "Slash final hit and completion"); // 마지막 정지 시점의 판정 뒤 입력·시간·표현 복구 대기
            Require(Mathf.Abs(inputTarget.HP - 967f) < 0.001f, "The final one-frame query must apply current AD times the Slash multiplier once to a multi-Collider target.");
            AssertRestored(manager, input, ui, gameCamera, player, vfx, true);
            Require(slashFx.IsSpaceSlashPlaying && slashFx.SpaceMaterial == spaceMaterial && !flash.activeSelf,
                "Successful Slash must reuse the world-space energy VFX without a second opaque LineRenderer bar.");
            ScreenCapture.CaptureScreenshot("Temp/SlashValidation.png");
            Require(camera.orthographicSize == zoom && gameCamera.Target == player.transform && ui.IsPlayerHudVisible == playerHud
                && ui.IsBossHudVisible == bossHud, "Natural completion must restore the previous camera and HUD state.");
            skills.SetLine(line);
            yield return Frames(3); // F를 계속 누른 채 새 경로를 보관해 홀드 재발동이 없는지 관찰
            Require(!player.IsSlashing && player.CurrentLine.HasValue && performed == 1, "Holding F must not consume a newly stored Line after the first Slash.");
            Keys(keyboard);
            skills.ResetSkills();
            passed("F-P07 real F consumes Line once, freezes game state under full protection and hits only at the final unscaled moment before restoring presentation");

            // 공통 상태와 확정 직후 대기는 원본을 소모하지 않고 거부한 입력도 예약하지 않음
            Require(!player.ApplySlash(), "A Slash without a stored Line must fail.");
            skills.SetLine(line);
            player.SetAimPosition(body.position + Vector2.right);
            Require(player.ApplyAttack() && !player.ApplySlash() && player.CurrentLine.HasValue, "An ordinary attack must prevent Slash without consuming the Line.");
            player.CancelAttack();
            Require(player.ApplyDash() && !player.ApplySlash() && player.CurrentLine.HasValue, "Dash must prevent Slash without consuming the Line.");
            player.ResetMotion();
            player.IsDead = true;
            Require(!player.ApplySlash(), "A dead Player must reject Slash.");
            player.IsDead = false;
            player.IsCutScene = true;
            Require(!player.ApplySlash(), "A cutscene Player must reject Slash.");
            player.IsCutScene = false;
            Require(player.ApplyDraw() && !player.ApplySlash(), "Draw editing must not allow Slash to consume the old Line.");
            player.CancelDraw();
            input.BlockInput = true;
            yield return Press(keyboard, Key.F); // 외부 차단 중 F가 새 행동으로 전달되지 않는지 관찰
            input.BlockInput = false;
            manager.SetGameplayPaused(true);
            Require(!player.ApplySlash() && player.CurrentLine.HasValue, "Ordinary pause must reject a new Slash while retaining its Line.");
            manager.SetGameplayPaused(false);
            skills.ResetSkills();
            Require(player.ApplyDraw() && skills.PlacePoint(line.StartPoint) && skills.PlacePoint(line.StartPoint + Vector2.right)
                && skills.ConfirmLine(), "A real confirmation must establish its short Slash delay.");
            Require(skills.SlashCooldownRemaining > 0f && !player.ApplySlash(), "Confirmation must not permit immediate Slash.");
            yield return Press(keyboard, Key.F); // 확정 직후 거부된 실제 F 누름과 해제 전달
            yield return GameSeconds(0.25f); // 확정 대기가 끝나도 이전의 거부 요청이 재실행되지 않는지 관찰
            Require(!player.IsSlashing && player.CurrentLine.HasValue, "Rejected F input must not be reserved for automatic Slash after cooldown.");
            skills.ResetSkills();
            passed("F-P07 Character restrictions and confirmation delay reject Slash without consuming Line or reserving rejected input");

            // 원래 Line과 표시 배율을 보존한 채 희미한 오라 전체까지 판정하는지 확인
            yield return CheckAuraWidth(manager, player, vfx, camera, fixtures, passed); //30도 오라 경계·폭 스냅샷·다음 시전 배율 확인 대기

            // 실제 Slash 각 전달과 반사 탄의 직진·피격·벽 우선 경계 확인
            yield return CheckGoldReflection(manager, player, fixtures, passed); // 반사각과 실제 빠른 이동·정지·벽 충돌 확인 대기

            // 회전한 경로 내부·끝점·벽 너머를 조회하고 같은 수신자의 여러 Collider는 한 번 처리
            Line diagonal = new Line(body.position + Vector2.up * 8f, new Vector2(1f, 1f), 6f, 1f);
            Vector2 across = new Vector2(-diagonal.Direction.y, diagonal.Direction.x);
            float diagonalHalfWidth = vfx.GetLineEffectWidth(diagonal) * 0.5f;
            Character inside = CreateTarget(fixtures, "SlashRotatedInside", diagonal.StartPoint + diagonal.Direction * 2f + across * 0.3f, TargetKind.Boss, 4);
            Character auraInside = CreateTarget(fixtures, "SlashRotatedAuraInside", diagonal.StartPoint + diagonal.Direction * 3f + across * (diagonalHalfWidth - 0.15f), TargetKind.Boss);
            Character behindWall = CreateTarget(fixtures, "SlashBehindWall", diagonal.StartPoint + diagonal.Direction * 4.5f, TargetKind.Boss);
            Character atEnd = CreateTarget(fixtures, "SlashAtEnd", diagonal.EndPoint - diagonal.Direction * 0.02f, TargetKind.NormalStructure);
            Character outsideWidth = CreateTarget(fixtures, "SlashOutsideWidth", diagonal.StartPoint + diagonal.Direction * 3f + across * (diagonalHalfWidth + 0.2f), TargetKind.Boss);
            Character outsideEnd = CreateTarget(fixtures, "SlashOutsideEnd", diagonal.EndPoint + diagonal.Direction * 0.15f, TargetKind.Boss);
            CreateWall(fixtures, diagonal.StartPoint + diagonal.Direction * 3.5f);
            int hits = 0;
            DamageInstance received = default;
            Action<DamageInstance> countHit = value => { hits++; received = value; };
            inside.OnDamaged += countHit; // 다중 Collider 수신자가 Slash 피해를 한 번만 받는지 수집
            try
            {
                Start(player, diagonal);
                int attackId = skills.Slasher.AttackId;
                yield return Until(() => !player.IsSlashing, "rotated Slash query"); // 실제 마지막 시점의 회전 사각 질의 결과 대기
                Require(inside.HP == 967f && auraInside.HP == 967f && behindWall.HP == 967f && atEnd.HP == 967f && outsideWidth.HP == 1000f && outsideEnd.HP == 1000f,
                    "The rotated Line must hit its interior, end and targets behind walls while excluding targets outside its width or length.");
                Require(hits == 1 && received.Kind == DamageType.Slash && received.Source == player && received.AttackId == attackId,
                    "One Slash must deliver a shared attack identity and one damage event per Character.");
                Require(slashFx.SpaceMaterial == spaceMaterial && slashFx.IsSpaceSlashPlaying && !vfx.IsSlashPlaying,
                    "Repeated successful Slash must reuse the same space-cut material without an opaque temporary bar.");
                yield return Until(() => !slashFx.IsSpaceSlashPlaying, "space-cut energy tail finishes"); // 복원 뒤 남은 unscaled 검격 잔광이 스스로 끝날 때까지 대기
                Require(!flash.activeSelf, "The successful flash tail must hide its reusable object after completion.");
            }
            finally
            {
                inside.OnDamaged -= countHit; // 회전 범위·수신자 중복 수집 해제
            }
            passed("F-P07 one rotated Line query ignores walls, respects its full bounds and deduplicates Character damage with a shared AttackId");

            // 능력의 각 수신 반응은 실제 구조물·투사체·버프 컴포넌트 계약으로 확인
            yield return CheckReceivers(manager, player, fixtures, passed); // 부위 전달·일반 탄 제거·황금 반사와 각성의 실제 수신 처리 대기

            // 명시적 중단은 자기 시간·표시·보호만 복구하고 기존 외부 상태 보존
            skills.ResetSkills();
            player.IsInvulnerable = true;
            Start(player, line);
            manager.SetGameplayPaused(true);
            yield return Until(() => !player.IsSlashing, "Slash completes under external pause"); // 일반 pause와 별개로 Slash의 unscaled 연출이 끝날 때까지 대기
            Require(manager.IsGameplayPaused && !manager.IsSlashPaused && Time.timeScale == 0f && player.IsInvulnerable,
                "Slash completion must preserve an external pause and pre-existing full invulnerability.");
            manager.SetGameplayPaused(false);
            player.IsInvulnerable = false;
            Action[] interrupt = { () => player.IsCutScene = true, () => input.BlockInput = true, () => skills.enabled = false,
                () => input.enabled = false, () => player.enabled = false, () => input.ClearPlayer() };
            foreach (Action cancel in interrupt)
            {
                skills.ResetSkills();
                Start(player, line);
                cancel();
                AssertRestored(manager, input, ui, gameCamera, player, vfx);
                Require(!player.CurrentLine.HasValue && !skills.Slasher.IsRunning && !skills.Slasher.HitBox.IsActive,
                    "Interrupting Slash must not restore its consumed Line or leave an open hit query.");
                skills.enabled = input.enabled = player.enabled = true;
                player.IsCutScene = false;
                if (input.Player != player) input.SetPlayer(player);
                input.BlockInput = false;
            }
            passed("F-P07 completion preserves external pause and prior protection while cutscene, block, disable and target release immediately restore owned Slash state");

            // 첫 피해 콜백에서 컷씬으로 전환하면 같은 질의의 남은 피해와 완료 덮어쓰기 중단
            skills.ResetSkills();
            Line interruptedLine = new Line(body.position + Vector2.down * 8f, Vector2.right, 6f, 1f);
            Character first = CreateTarget(fixtures, "SlashInterruptedFirst", interruptedLine.StartPoint + Vector2.right, TargetKind.Boss);
            Character second = CreateTarget(fixtures, "SlashInterruptedSecond", interruptedLine.StartPoint + Vector2.right * 2f, TargetKind.Boss);
            int interruptedHits = 0;
            bool closedInCallback = false;
            Action<DamageInstance> interruptOnHit = value =>
            {
                interruptedHits++;
                player.IsCutScene = true;
                closedInCallback = !player.IsSlashing && !skills.Slasher.IsRunning && !skills.Slasher.HitBox.IsActive;
            };
            first.OnDamaged += interruptOnHit; // 첫 실제 수신 피해에서 Slash를 컷씬으로 중단
            second.OnDamaged += interruptOnHit; // 물리 결과 순서와 무관하게 첫 대상이 중단을 발생하도록 연결
            try
            {
                Start(player, interruptedLine);
                yield return Until(() => player.IsCutScene, "Slash hit callback interruption"); // 마지막 판정의 첫 피해 콜백이 Slash를 중단할 때까지 대기
                Require(interruptedHits == 1 && first.HP + second.HP == 1967f && closedInCallback,
                    "Cancellation in the first damage callback must synchronously stop the remaining query targets.");
                AssertRestored(manager, input, ui, gameCamera, player, vfx);
                Require(player.IsCutScene && !player.CurrentLine.HasValue, "Late Slash completion must not overwrite the new cutscene or restore consumed geometry.");
            }
            finally
            {
                first.OnDamaged -= interruptOnHit; // 피해 도중 컷씬 중단 수집 해제
                second.OnDamaged -= interruptOnHit; // 두 번째 수신자의 중단 연결도 함께 해제
                player.IsCutScene = false;
            }
            passed("F-P07 cancellation inside the first hit callback stops later damage and leaves the new cutscene intact after Slash Update returns");

            // Skill.Update 이전 만료 경계에서도 실제 Slash 시작은 원본을 자연 만료시키고 거부
            skills.ResetSkills();
            skills.CancelDrawCooldown = 0.24f;
            skills.LineLifetime = 0.2f;
            skills.SetLine(line);
            double expires = Time.timeAsDouble + skills.LineLifetime;
            bool boundaryReached = false;
            bool pendingBeforeUpdate = false;
            bool acceptedExpired = false;
            beforeInputUpdate = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic || Time.timeAsDouble < expires) return;
                pendingBeforeUpdate = player.CurrentLine.HasValue && skills.LineRemaining > 0f;
                acceptedExpired = player.ApplySlash();
                boundaryReached = true;
                InputSystem.onBeforeUpdate -= beforeInputUpdate; // 만료 첫 입력 프레임에서 한 번 요청한 뒤 경계 수집 종료
            };
            InputSystem.onBeforeUpdate += beforeInputUpdate; // 실제 Slash 시작 요청과 Skill.Update 사이의 만료 순서 수집
            yield return Until(() => boundaryReached, "expired Slash before Skill.Update"); // 게임 시각이 만료 기한에 도달한 첫 Dynamic 입력 갱신 대기
            Require(pendingBeforeUpdate && !acceptedExpired && !player.IsSlashing && !player.CurrentLine.HasValue
                && !manager.IsSlashPaused && !vfx.IsSlashPlaying && vfx.IsExpiryPlaying && skills.DrawCooldownRemaining > 0f
                && skills.DrawCooldownRemaining <= skills.CancelDrawCooldown,
                "An already expired Line must naturally expire and reject Slash before consuming or pausing gameplay.");
            passed("F-P07 the actual Slash start rejects a Line already expired before Skill.Update and applies only natural expiry state");
        }
        finally
        {
            // 실패·중단에서도 이벤트·임시 대상을 정리하고 정상 Player 연결 복구
            slashAction.performed -= onSlash; // 실제 F 액션 수집 종료
            if (beforeInputUpdate != null) InputSystem.onBeforeUpdate -= beforeInputUpdate; // 실패 시에도 만료 경계 콜백 해제
            Keys(keyboard);
            input.enabled = skills.enabled = player.enabled = true;
            player.CancelSlash();
            vfx.ClearSlash();
            player.IsCutScene = false;
            player.IsDead = false;
            skills.ResetSkills();
            player.ResetMotion();
            player.Buffs.RemoveAll();
            if (input.Player != player) input.SetPlayer(player);
            foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);

            // Inspector 튜닝값·기존 수치·위치·화면과 시간 소유 복원
            skills.SlashDuration = duration;
            slashFx.SpaceSlashDuration = spaceDuration;
            skills.SlashDamageMultiplier = multiplier;
            skills.SlashTargetMask = targetMask;
            skills.LineLifetime = lifetime;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.NormalDrawCooldown = normalCooldown;
            skills.ConfirmSlashCooldown = confirmCooldown;
            player.AD = damage;
            player.AS = attackSpeed;
            player.SP = speed;
            player.HP = health;
            player.Groggy = groggy;
            player.IsInvulnerable = invulnerable;
            player.SetAimPosition(aim);
            SetPosition(body, position);
            gameCamera.Follow(cameraTarget, true);
            gameCamera.FollowDuration = follow;
            camera.orthographicSize = zoom;
            ui.SetPlayerHudVisible(playerHud);
            ui.SetBossHudVisible(bossHud);
            manager.SetGameplayPaused(paused);
            input.BlockInput = blocked;
        }
    }

    /// <summary>30도 오라 경계와 원본·표시·판정 폭의 시전별 보존 확인</summary>
    private static IEnumerator CheckAuraWidth(GameManager manager, PlayerCharacter player, DrawVfx vfx, Camera camera, List<GameObject> fixtures, Action<string> passed)
    {
        // 다른 수신 검사와 겹치지 않는 곳에 기존 폭 밖·오라 안의 작은 중복 피격 영역 준비
        SkillController skills = player.Skills;
        DrawLineVfx style = vfx.LineObject.GetComponent<DrawLineVfx>();
        float previousMultiplier = style.WidthMultiplier;
        Line line = new Line((Vector2)player.transform.position + new Vector2(-12f, -8f),
            new Vector2(Mathf.Cos(Mathf.PI / 6f), Mathf.Sin(Mathf.PI / 6f)), 6f, 0.25f);
        Vector2 normal = new Vector2(-line.Direction.y, line.Direction.x);
        Character left = CreateTarget(fixtures, "SlashAuraLeft", line.StartPoint + line.Direction * 2f + normal * 0.31f, TargetKind.Boss, 3);
        Character right = CreateTarget(fixtures, "SlashAuraRight", line.StartPoint + line.Direction * 4f - normal * 0.31f, TargetKind.Boss, 2);
        Character outsideWidth = CreateTarget(fixtures, "SlashAuraOutside", line.StartPoint + line.Direction * 3f + normal * 0.5f, TargetKind.Boss);
        Character outsideEnd = CreateTarget(fixtures, "SlashAuraBeyondEnd", line.EndPoint + line.Direction * 0.15f, TargetKind.Boss);
        float expectedDamage = player.AD * skills.SlashDamageMultiplier;
        int leftHits = 0;
        int rightHits = 0;
        int leftAttackId = 0;
        int rightAttackId = 0;
        Action<DamageInstance> onLeft = damage => { leftHits++; leftAttackId = damage.AttackId; };
        Action<DamageInstance> onRight = damage => { rightHits++; rightAttackId = damage.AttackId; };
        left.OnDamaged += onLeft; // 중복 Collider를 가진 오라 왼쪽의 일회 피해 수집
        right.OnDamaged += onRight; // 오라 반대쪽의 일회 피해와 동일 공격 번호 수집
        try
        {
            // 원본 폭은 보관하고 실제 표시와 판정 폭만0.75로 일치
            style.WidthMultiplier = 3f;
            skills.ResetSkills();
            Start(player, line);
            int firstAttackId = skills.Slasher.AttackId;
            Require(SameLine(skills.Slasher.ActiveLine, line) && Mathf.Approximately(skills.Slasher.HitWidth, 0.75f),
                "Slash must preserve its raw 0.25 Line and snapshot the full 0.75 image FX width.");
            AssertRenderedLineWidth(vfx.LineObject, line, camera, 0.75f);
            Require(leftHits == 0 && rightHits == 0, "Aura receivers must remain unharmed until the final Slash query.");

            // 테스트용 스타일 변경이 이번 판정을 바꾸지 않고 다음 시전에서만 적용되는지 확인
            style.WidthMultiplier = 4f;
            yield return Frames(2); // 변경한 스타일이 표시에는 반영되지만 현재 판정 폭은 고정되는지 관찰
            Require(player.IsSlashing && SameLine(skills.Slasher.ActiveLine, line) && Mathf.Approximately(skills.Slasher.HitWidth, 0.75f),
                "Changing the style during a cast must not replace the active hit width snapshot.");
            AssertRenderedLineWidth(vfx.LineObject, line, camera, 1f);
            yield return Until(() => !player.IsSlashing, "30 degree aura query"); // 최초 폭으로 실행할 실제 마지막 사각 판정 대기
            Require(Mathf.Approximately(left.HP, 1000f - expectedDamage) && Mathf.Approximately(right.HP, 1000f - expectedDamage)
                && outsideWidth.HP == 1000f && outsideEnd.HP == 1000f && leftHits == 1 && rightHits == 1
                && leftAttackId == firstAttackId && rightAttackId == firstAttackId,
                "Both faint aura sides must hit once beyond the old width while excluding the new width and length exterior.");
            Require(Mathf.Approximately(skills.Slasher.HitBox.Area.height, 0.75f) && Mathf.Approximately(skills.Slasher.HitBox.Area.width, line.Length)
                && Mathf.Abs(skills.Slasher.HitBox.Angle - 30f) < 0.01f && skills.Slasher.HitWidth == 0f,
                "The actual query must retain its start-time width and angle, then clear its running snapshot.");
            Require(skills.Slasher.ApplyHits(player, manager.CurrentBoss) == 0, "A completed Slash must reject another aura query.");

            // 다음 Slash는 새 배율의1.0 폭을 적용하고 끝점 밖은 계속 제외
            skills.ResetSkills();
            Start(player, line);
            int secondAttackId = skills.Slasher.AttackId;
            Require(SameLine(skills.Slasher.ActiveLine, line) && Mathf.Approximately(skills.Slasher.HitWidth, 1f),
                "The next Slash must use the new multiplier without widening the stored Line itself.");
            AssertRenderedLineWidth(vfx.LineObject, line, camera, 1f);
            yield return Until(() => !player.IsSlashing, "next aura width query"); // 다음 시전에서 넓어진 오라의 실제 판정 대기
            Require(Mathf.Approximately(skills.Slasher.HitBox.Area.height, 1f) && Mathf.Approximately(outsideWidth.HP, 1000f - expectedDamage)
                && outsideEnd.HP == 1000f && leftHits == 2 && rightHits == 2 && firstAttackId != secondAttackId
                && leftAttackId == secondAttackId && rightAttackId == secondAttackId && skills.Slasher.HitWidth == 0f,
                "Only the next Slash may reach the wider aura receiver while retaining deduplication and the same end boundary.");
            Require(skills.Slasher.ApplyHits(player, manager.CurrentBoss) == 0, "The second completed Slash must remain a single query.");
            passed("F-P07 full image FX aura hits both 30 degree sides once, excludes bounds, preserves raw geometry and snapshots width per cast");
        }
        finally
        {
            // 수집과 스타일을 되돌리고 진행 중인 테스트 공격 정리
            left.OnDamaged -= onLeft; // 오라 왼쪽 피해 수집 종료
            right.OnDamaged -= onRight; // 오라 오른쪽 피해 수집 종료
            style.WidthMultiplier = previousMultiplier;
            skills.ResetSkills();
        }
    }

    /// <summary>판정 폭 확대가 선 표시를 다시 곱하거나 실제 끝점을 바꾸지 않는지 확인</summary>
    private static void AssertRenderedLineWidth(GameObject visual, Line line, Camera camera, float expectedWidth)
    {
        // 표시 Transform에는 확장한 판정 폭이 아닌 원본 Line 폭 유지
        LineRenderer renderer = visual.GetComponent<LineRenderer>();
        Require(renderer != null && renderer.positionCount == 2 && Mathf.Abs(visual.transform.localScale.y - line.Width) < 0.001f,
            "The image FX transform must keep the raw Line width instead of receiving the expanded hit width.");
        Vector3 first = renderer.GetPosition(0);
        Vector3 last = renderer.GetPosition(1);
        if (!renderer.useWorldSpace)
        {
            first = renderer.transform.TransformPoint(first);
            last = renderer.transform.TransformPoint(last);
        }
        Require(Vector2.Distance(first, line.StartPoint) < 0.002f && Vector2.Distance(last, line.EndPoint) < 0.002f,
            "The rendered Line start, end and length must survive hit rectangle widening.");

        // 실제 메시를 선의 수직축에 투영해 표시 배율의 중복 적용 검출
        Mesh mesh = new Mesh();
        try
        {
            renderer.BakeMesh(mesh, camera, true);
            Require(mesh.vertexCount >= 4, "The Slash marker must produce actual ribbon geometry.");
            Vector2 normal = new Vector2(-line.Direction.y, line.Direction.x);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Vector3 vertex in mesh.vertices)
            {
                float projection = Vector2.Dot(vertex, normal);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            Require(Mathf.Abs(maximum - minimum - expectedWidth) < 0.005f,
                $"The actual ribbon width must apply the FX style once: expected={expectedWidth}, actual={maximum - minimum}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    /// <summary>Slash 각도 전달과 위치에 무관한 반사·직진·실제 피격 경계 확인</summary>
    private static IEnumerator CheckGoldReflection(GameManager manager, PlayerCharacter player, List<GameObject> fixtures, Action<string> passed)
    {
        // 기존 전투 대상과 분리한 작은 Boss 피격 영역과 실제 맵 벽 준비
        var root = new GameObject("SlashAngleReflectionBoss");
        fixtures.Add(root);
        BossCharacter boss = root.AddComponent<BossCharacter>();
        boss.maxHP = 1000f;
        boss.InitializeHealth(1000f);
        AddHitPart(boss, 1);
        AddHitPart(boss, 2);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 playerPosition = body.position;
        float playerHealth = player.HP;
        bool invulnerable = player.IsInvulnerable;
        bool paused = manager.IsGameplayPaused;
        MapManager map = MapManager.Instance;
        Transform walls = map != null ? map.transform.Find("WorldCollision") : null;
        BoxCollider2D wall = walls != null ? walls.GetComponentInChildren<BoxCollider2D>() : null;
        Require(wall != null && map.IsWall(wall) && wall.enabled && wall.gameObject.activeInHierarchy,
            "Reflection checks require an active BoxCollider already registered in the real map wall cache.");
        Vector3 wallPosition = wall.transform.position;
        Quaternion wallRotation = wall.transform.rotation;
        Vector3 wallScale = wall.transform.localScale;
        Vector2 wallSize = wall.size;
        Vector2 wallOffset = wall.offset;
        int bossHits = 0;
        Action<DamageInstance> onBossHit = damage => bossHits++;
        boss.OnDamaged += onBossHit; // 실제 원궤적의 중복 피격 영역이 피해를 한 번 전달하는지 수집
        try
        {
            // 수평·수직·30도·반대 Line 방향을 실제 사각 Query로 전달
            manager.SetGameplayPaused(true);
            float[] angles = { 0f, 90f, 30f, 210f };
            Vector2[] expected = { Vector2.up, Vector2.down, new Vector2(-Mathf.Sqrt(3f) * 0.5f, 0.5f), new Vector2(-Mathf.Sqrt(3f) * 0.5f, 0.5f) };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector2 center = new Vector2(400f, 400f + i * 10f);
                Vector2 tangent = new Vector2(Mathf.Cos(angles[i] * Mathf.Deg2Rad), Mathf.Sin(angles[i] * Mathf.Deg2Rad));
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                boss.transform.position = center + Vector2.up * 20f;
                GoldProjectile before = CreateGoldReflection(fixtures, boss, center - tangent * 2f + normal * 0.1f, Vector2.down, 0.2f);
                GoldProjectile after = CreateGoldReflection(fixtures, boss, center + tangent * 2f - normal * 0.1f, Vector2.down, 0.2f);
                Vector3 beforePosition = before.transform.position;
                Vector3 afterPosition = after.transform.position;
                DamageInstance damage = new DamageInstance(player, 1f, DamageType.Slash, DamageInstance.NextAttackId());
                var query = new CollisionBox();
                query.Begin(damage, ~0);
                Physics2D.SyncTransforms();
                Require(query.QuerySlash(center, new Vector2(6f, 0.75f), angles[i], player, boss) == 2
                    && Vector2.Distance(before.Direction, expected[i]) < 0.0001f && Vector2.Distance(after.Direction, expected[i]) < 0.0001f
                    && before.transform.position == beforePosition && after.transform.position == afterPosition
                    && before.ReflectionSpeed == 6f && after.ReflectionSpeed == 6f && before.RemainingLifetime >= 1f && after.RemainingLifetime >= 1f,
                    "Actual Slash query angle must reflect incoming direction equally on both sides of its center without moving the bullet or changing reflection speed.");
                Require(query.QuerySlash(center, new Vector2(6f, 0.75f), angles[i], player, boss) == 0
                    && !before.OnSlashed(player, boss, damage, tangent) && !after.OnSlashed(player, boss, damage, -tangent),
                    "Duplicate Colliders and another Slash must not reflect an already reflected gold bullet again.");
                query.End();
                UnityEngine.Object.DestroyImmediate(before.gameObject);
                UnityEngine.Object.DestroyImmediate(after.gameObject);
            }
            passed("F-P07 actual Slash angles reflect horizontal, vertical and 30 degree incidence independently of center position and reversed Line direction");

            // 유도 없이 직진하며 일시정지는 위치와 반사 후 양수 수명도 함께 보존
            Vector2 origin = new Vector2(400f, 500f);
            boss.transform.position = origin + new Vector2(4f, 1f);
            GoldProjectile miss = CreateGoldReflection(fixtures, boss, origin, Vector2.left, 0.2f);
            Require(miss.Reflect(boss, player, Vector2.up) && miss.Direction == Vector2.right, "A vertical mirror must turn left incidence into rightward flight.");
            manager.SetGameplayPaused(false);
            yield return GameSeconds(0.06f); // Boss 중심과 다른 직선 경로를 실제 Update로 비행시키는 동안 관찰
            Require(miss != null && miss.IsActive && miss.transform.position.x > origin.x && Mathf.Abs(miss.transform.position.y - origin.y) < 0.0001f
                && miss.Direction == Vector2.right && boss.HP == 1000f, "A reflected bullet must stay straight and miss an off-path Boss instead of homing.");
            manager.SetGameplayPaused(true);
            Vector3 frozenPosition = miss.transform.position;
            float frozenLifetime = miss.RemainingLifetime;
            boss.transform.position += Vector3.up * 2f;
            yield return RealSeconds(0.07f); // 목표가 이동해도 정지한 반사탄의 방향·위치·수명은 그대로인지 관찰
            Require(miss.transform.position == frozenPosition && miss.RemainingLifetime == frozenLifetime && miss.Direction == Vector2.right,
                "Pause must freeze reflected movement and lifetime while moving the Owner cannot steer its direction.");
            manager.SetGameplayPaused(false);
            yield return Until(() => miss == null || !miss.IsActive, "straight reflected finite expiry"); // 빗나간 반사탄이 보존한 양수 수명으로 자연 종료되는지 대기
            Require(boss.HP == 1000f && bossHits == 0, "An off-path Boss must never receive reflected damage from proximity or target tracking.");
            passed("F-P07 reflected gold keeps its straight direction and finite lifetime, misses an off-path moving Owner and freezes completely during pause");

            // 원 반경과 작은 Boss 범위 바로 밖의 초고속 경로는 적중으로 확장하지 않음
            origin += Vector2.up * 5f;
            boss.transform.position = origin + new Vector2(4f, 0.14f);
            GoldProjectile fastMiss = CreateGoldReflection(fixtures, boss, origin, Vector2.left, 0.2f);
            fastMiss.ReflectionSpeed = 10000f;
            Require(fastMiss.Reflect(boss, player, Vector2.up), "A near-miss fixture must enter reflected straight flight.");
            yield return Until(() => fastMiss == null || !fastMiss.IsActive || fastMiss.transform.position.x > origin.x + 5f, "fast reflected near miss"); // 고속 경로가 작은 피격 영역 옆을 실제로 지나칠 때까지 대기
            Require(fastMiss != null && fastMiss.IsActive && boss.HP == 1000f && bossHits == 0,
                "Continuous reflection collision must not damage a Boss beyond the circle radius and actual hitbox bounds.");
            fastMiss.Despawn();

            // 초고속 반사는 Player를 통과하되 작은 Boss의 실제 Hitbox를 놓치지 않음
            origin += Vector2.up * 10f;
            boss.transform.position = origin + Vector2.right * 4f;
            Hitbox playerHitbox = null;
            foreach (Hitbox candidate in player.GetComponentsInChildren<Hitbox>())
                if (candidate.Owner == player && candidate.IsEnabled) { playerHitbox = candidate; break; }
            Require(playerHitbox != null && !player.IsHitProtected && !player.IsDodge, "Player immunity must be checked with a real unprotected Player hitbox.");
            Vector2 hitboxOffset = (Vector2)playerHitbox.Bounds.center - body.position;
            SetPosition(body, origin + Vector2.right * 2f - hitboxOffset);
            player.IsInvulnerable = false;
            bool crossesPlayer = false;
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(origin, 0.08f, Vector2.right, 3f))
                if (hit.collider != null && hit.collider.GetComponent<Hitbox>() == playerHitbox) crossesPlayer = true;
            Require(crossesPlayer, "The reflected test path must actually cross the live Player hitbox.");
            GoldProjectile fast = CreateGoldReflection(fixtures, boss, origin, Vector2.left, 0f);
            fast.ReflectionSpeed = 10000f;
            Require(fast.Reflect(boss, player, Vector2.up) && fast.RemainingLifetime == 0f, "Zero-lifetime reflection must retain unlimited flight.");
            Physics2D.SyncTransforms();
            yield return Until(() => fast == null || !fast.IsActive, "fast reflected Boss hitbox crossing"); // 한 프레임 거리보다 작은 Boss 피격 영역의 연속 경로 접촉 대기
            Require(boss.HP == 993f && bossHits == 1 && player.HP == playerHealth,
                "A high-speed reflected circle must hit its allowed Boss once through duplicate hitboxes while passing through Player unharmed.");
            SetPosition(body, playerPosition);

            // 같은 맵 캐시의 벽을 옮겨 Boss 전·후와 동거리 접촉의 우선순위 확인
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;
            wall.offset = Vector2.zero;
            wall.size = new Vector2(0.08f, 4f);
            for (int i = 0; i < 3; i++)
            {
                origin += Vector2.up * 10f;
                boss.InitializeHealth(1000f);
                bossHits = 0;
                boss.transform.position = origin + Vector2.right * (i == 0 ? 2f : 4f);
                wall.transform.position = origin + Vector2.right * (i == 1 ? 2f : 4f);
                Physics2D.SyncTransforms();
                GoldProjectile blocked = CreateGoldReflection(fixtures, boss, origin, Vector2.left, 0f);
                blocked.ReflectionSpeed = 10000f;
                Require(blocked.Reflect(boss, player, Vector2.up), "A wall must not reject a valid reflection before its flight starts.");
                yield return Until(() => blocked == null || !blocked.IsActive, "reflected nearest wall or Boss contact"); // 실제 한 번의 원궤적 질의가 가장 가까운 접촉을 처리할 때까지 대기
                Require(boss.HP == (i == 0 ? 993f : 1000f) && bossHits == (i == 0 ? 1 : 0),
                    "Only a Boss strictly before the nearest wall may receive reflected damage; a nearer or tied wall must win.");
            }
            passed("F-P07 high-speed reflected gold hits real Boss bounds once, never harms Player and gives nearer or equal-distance walls priority");
        }
        finally
        {
            // 실제 맵·Player·시간 상태를 되돌리고 임시 Boss 수집 해제
            boss.OnDamaged -= onBossHit; // 실제 반사 피해 수집 종료
            wall.transform.SetPositionAndRotation(wallPosition, wallRotation);
            wall.transform.localScale = wallScale;
            wall.size = wallSize;
            wall.offset = wallOffset;
            SetPosition(body, playerPosition);
            player.HP = playerHealth;
            player.IsInvulnerable = invulnerable;
            manager.SetGameplayPaused(paused);
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>각도·경로 검사에 사용할 실제 Gold 탄과 중복 피격 영역 준비</summary>
    private static GoldProjectile CreateGoldReflection(List<GameObject> fixtures, BossCharacter boss, Vector2 position, Vector2 direction, float lifetime)
    {
        // 표시와 무관한 실제 Gold 수명·이동을 작은 Trigger로 구성
        var root = new GameObject("SlashAngleGold");
        fixtures.Add(root);
        root.transform.position = position;
        root.AddComponent<CircleCollider2D>().radius = 0.04f;
        GoldProjectile projectile = root.AddComponent<GoldProjectile>();
        projectile.Initialize(boss, direction, 0f, lifetime, 7f);
        projectile.ReflectionSpeed = 6f;
        projectile.GroggyGain = 0f;
        projectile.GrantsFever = false;
        root.AddComponent<Hitbox>();
        var duplicate = new GameObject("DuplicateGoldCollider");
        duplicate.transform.SetParent(root.transform, false);
        duplicate.AddComponent<CircleCollider2D>().radius = 0.04f;
        duplicate.AddComponent<Hitbox>();
        Physics2D.SyncTransforms();
        return projectile;
    }

    /// <summary>실제 부위 집계·탄환 제거와 반사·각성 획득의 연결 확인</summary>
    private static IEnumerator CheckReceivers(GameManager manager, PlayerCharacter player, List<GameObject> fixtures, Action<string> passed)
    {
        // 실제 반사 목표와 각성 설정을 보관하고 독립 수신 대상을 준비
        SkillController skills = player.Skills;
        CharacterBuff buffs = player.Buffs;
        BossCharacter reflectionBoss = manager.CurrentBoss;
        Require(reflectionBoss != null && !reflectionBoss.IsDead && !reflectionBoss.IsGroggy,
            "Projectile checks require the live session Boss before any groggy encounter.");
        float bossHealth = reflectionBoss.HP;
        Vector3 bossPosition = reflectionBoss.transform.position;
        Rigidbody2D bossBody = reflectionBoss.GetComponent<Rigidbody2D>();
        float playerHealth = player.HP;
        float groggy = player.Groggy;
        float feverDuration = buffs.FeverDuration;
        float heal = buffs.HealPerAcquisition;
        float damage = player.AD;
        float speed = player.SP;
        float attackSpeed = player.AS;
        int completed = 0;
        int completedParts = 0;
        int completedAttackId = 0;
        int groggyEvents = 0;
        bool finalResponses = false;
        Line line = new Line((Vector2)player.transform.position + Vector2.right * 15f, Vector2.right, 6f, 1f);
        var bossObject = new GameObject("SlashPartCollectionBoss");
        fixtures.Add(bossObject);
        bossObject.transform.position = line.StartPoint + Vector2.right;
        BossCharacter partsBoss = bossObject.AddComponent<BossCharacter>();
        partsBoss.maxHP = 1000f;
        partsBoss.InitializeHealth(1000f);
        AddHitPart(partsBoss, 11);
        AddHitPart(partsBoss, 12);
        AddHitPart(partsBoss, 11);
        BossStructure normal = CreateStructure(fixtures, partsBoss, line.StartPoint + Vector2.right * 2f, 21, true);
        BossStructure special = CreateStructure(fixtures, partsBoss, line.StartPoint + Vector2.right * 2.5f, 22, false);
        Projectile ordinary = CreateProjectile(fixtures, reflectionBoss, line.StartPoint + Vector2.right * 3f, ProjectileSlashResponse.Destroy, 30f);
        Projectile goldenFirst = CreateProjectile(fixtures, reflectionBoss, line.StartPoint + Vector2.right * 4f, ProjectileSlashResponse.Reflect, 35f);
        Projectile goldenSecond = CreateProjectile(fixtures, reflectionBoss, line.StartPoint + Vector2.right * 4.5f, ProjectileSlashResponse.Reflect, 35f);
        goldenFirst.Direction = goldenSecond.Direction = Vector2.right;
        Projectile ignored = CreateProjectile(fixtures, reflectionBoss, line.StartPoint + Vector2.right * 5f, ProjectileSlashResponse.Ignore, 100f);
        Action<int> onCompleted = attackId =>
        {
            completed++;
            completedAttackId = attackId;
            completedParts = partsBoss.SlashParts.Count;
            finalResponses = !ordinary.IsActive && goldenFirst.IsReflected && goldenSecond.IsReflected && ignored.IsActive
                && reflectionBoss.IsGroggy && player.Groggy == 0f && !buffs.IsFever && Time.timeScale == 0f;
        };
        Action onGroggy = () => groggyEvents++;
        partsBoss.OnSlashCompleted += onCompleted; // 모든 부위가 모인 뒤 Boss가 받은 공격·탄환 처리 결과 수집
        reflectionBoss.OnGroggy += onGroggy; // 탄환별 보상이 상한에 도달한 순간 그로기를 한 번 발동하는지 수집
        try
        {
            // 반사 목표를 경로 밖에 두고 각 탄의 마지막 정지 시점 반응 확인
            Physics2D.SyncTransforms();
            float leftExtent = 0f;
            foreach (Collider2D shape in reflectionBoss.GetComponentsInChildren<Collider2D>())
                if (shape.enabled && shape.gameObject.activeInHierarchy)
                    leftExtent = Mathf.Max(leftExtent, reflectionBoss.transform.position.x - shape.bounds.min.x);
            reflectionBoss.transform.position = line.EndPoint + Vector2.right * (leftExtent + 0.25f);
            if (bossBody != null) bossBody.position = reflectionBoss.transform.position;
            player.Groggy = 0f;
            player.HP = player.maxHP * 0.5f;
            buffs.FeverDuration = 0.8f;
            buffs.HealPerAcquisition = 2f;
            Physics2D.SyncTransforms();
            skills.ResetSkills();
            Start(player, line);
            int attackId = skills.Slasher.AttackId;
            yield return Until(() => !player.IsSlashing, "Slash receiver reactions"); // 마지막 한 번의 질의가 부위·구조물·탄환에 전달될 때까지 대기
            Require(completed == 1 && completedParts == 4 && completedAttackId == attackId && partsBoss.LastCompletedSlashAttackId == attackId
                && partsBoss.HP == 967f && normal.HP == 967f && special.HP == 967f,
                "Boss body damage must be deduplicated while distinct body and structure parts are reported before one completion event.");
            Require(finalResponses && groggyEvents == 1 && goldenFirst.ReflectionTarget == reflectionBoss && goldenSecond.ReflectionTarget == reflectionBoss,
                "The final paused query must remove ordinary bullets, reflect gold bullets, ignore configured bullets and trigger one groggy at 100.");
            yield return Until(() => buffs.IsFever, "gold reflection Fever next-frame application"); // 금색 반사의 예약된 각성이 다음 프레임에 적용될 때까지 대기
            Require(Mathf.Approximately(player.HP, player.maxHP * 0.5f + 4f)
                && Mathf.Approximately(player.AD, player.basicAD * buffs.AttackMultiplier)
                && Mathf.Approximately(player.AS, player.basicAS * buffs.AttackSpeedMultiplier)
                && Mathf.Approximately(player.SP, player.basicSP * buffs.MoveSpeedMultiplier),
                "Two gold bullets must heal once each but apply one non-stacking set of Fever stats.");
            float healedHealth = player.HP;
            yield return Until(() => reflectionBoss.HP <= bossHealth - 2f, "reflected projectiles reach Boss"); // 반사 탄이 게임 시간으로 Boss까지 이동해 각각 한 번 피해를 주는지 대기
            Require(player.HP == healedHealth && reflectionBoss.HP == bossHealth - 2f,
                "Reflected bullets must damage only their Boss target and must not damage the Player.");

            // 각성 중 새 Slash의 Draw 대기 배율과 버프 게임 시간 정지 확인
            skills.ResetSkills();
            Start(player, new Line((Vector2)player.transform.position + Vector2.left * 15f, Vector2.left, 6f, 1f));
            Require(skills.DrawCooldownRemaining == skills.NormalDrawCooldown * buffs.FeverDrawCooldownMultiplier,
                "A Slash consumed during Fever must apply the dedicated normal Draw cooldown multiplier once.");
            yield return Frames(1); // Slash 정지 진입 프레임의 버프 시간 샘플을 지난 뒤 비교 시작
            float frozenFever = buffs.FeverRemaining;
            yield return RealSeconds(0.07f); // unscaled Slash가 진행되는 동안 각성 수명이 멈추는지 관찰
            Require(player.IsSlashing && buffs.FeverRemaining == frozenFever, "Fever lifetime must freeze with the game during Slash.");
            yield return Until(() => !player.IsSlashing, "Fever Slash completes"); // 각성 중 Slash가 게임 시간과 별개로 끝날 때까지 대기
            yield return GameSeconds(0.08f); // 재획득 전 일부 소모된 각성 기간 준비
            float previousFever = buffs.FeverRemaining;
            Require(buffs.ApplyFever(), "A new acquisition must be accepted after the original Fever frame.");
            yield return Until(() => buffs.FeverRemaining > previousFever, "Fever refresh"); // 새 획득이 다음 프레임에 기간·회복을 갱신할 때까지 대기
            Require(player.HP == healedHealth + 2f && player.AD == player.basicAD * buffs.AttackMultiplier
                && buffs.DrawCooldownMultiplier == buffs.FeverDrawCooldownMultiplier, "Reacquisition must refresh and heal without stacking stats or repeatedly halving Draw cooldown.");

            // 같은 프레임의 제거는 예약한 각성보다 우선하며 기본 수치 복구
            Require(buffs.ApplyFever(), "The removal-priority check must first queue an acquisition.");
            buffs.RemoveAll();
            Require(!buffs.ApplyFever() && !buffs.IsFever, "Removal must reject same-frame reacquisition.");
            yield return Frames(2); // 제거 전에 예약한 획득이 다음 프레임에 되살아나지 않는지 관찰
            Require(!buffs.IsFever && player.AD == player.basicAD && player.AS == player.basicAS && player.SP == player.basicSP,
                "Removed Fever must leave no queued reacquisition and must restore base stats.");
            passed("F-P07 Boss parts complete once, structures receive Slash, bullets remove or reflect with one groggy and gold Fever heals per acquisition without stat stacking");
        }
        finally
        {
            // 실제 Boss·Player 수치와 각성 설정을 되돌리고 수집 연결 해제
            partsBoss.OnSlashCompleted -= onCompleted; // 부위·수신 반응 집계 종료
            reflectionBoss.OnGroggy -= onGroggy; // 그로기 발동 수집 종료
            player.CancelSlash();
            player.Buffs.RemoveAll();
            buffs.FeverDuration = feverDuration;
            buffs.HealPerAcquisition = heal;
            player.AD = damage;
            player.AS = attackSpeed;
            player.SP = speed;
            player.HP = playerHealth;
            player.Groggy = groggy;
            reflectionBoss.HP = bossHealth;
            reflectionBoss.IsGroggy = false;
            reflectionBoss.transform.position = bossPosition;
            if (bossBody != null) bossBody.position = bossPosition;
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>동일 Boss의 개별 부위를 판정할 작은 Trigger 준비</summary>
    private static void AddHitPart(BossCharacter boss, int partId)
    {
        var part = new GameObject("SlashBossPart" + partId);
        part.transform.SetParent(boss.transform, false);
        BoxCollider2D collider = part.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.08f;
        collider.isTrigger = true;
        Hitbox hitbox = part.AddComponent<Hitbox>();
        hitbox.Owner = boss;
        hitbox.PartId = partId;
    }

    /// <summary>캐릭터와 별개인 구조물에 독립 체력·부위 번호와 중복 Collider 연결</summary>
    private static BossStructure CreateStructure(List<GameObject> fixtures, BossCharacter boss, Vector2 position, int partId, bool acceptsNormal)
    {
        var root = new GameObject("SlashStructure" + partId);
        fixtures.Add(root);
        root.transform.position = position;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.08f;
        collider.isTrigger = true;
        BossStructure structure = root.AddComponent<BossStructure>();
        structure.Owner = boss;
        structure.PartId = partId;
        structure.AcceptsNormalAttack = acceptsNormal;
        structure.Reset(1000f);
        root.AddComponent<Hitbox>();
        var duplicate = new GameObject("DuplicateStructureCollider");
        duplicate.transform.SetParent(root.transform, false);
        BoxCollider2D duplicateShape = duplicate.AddComponent<BoxCollider2D>();
        duplicateShape.size = Vector2.one * 0.08f;
        duplicateShape.isTrigger = true;
        duplicate.AddComponent<Hitbox>();
        return structure;
    }

    /// <summary>현재 게임의 실제 탄환 수신·반사 컴포넌트로 검사 대상 준비</summary>
    private static Projectile CreateProjectile(List<GameObject> fixtures, BossCharacter boss, Vector2 position, ProjectileSlashResponse response, float groggyGain)
    {
        var root = new GameObject("SlashProjectile" + response);
        fixtures.Add(root);
        root.transform.position = position;
        root.AddComponent<CircleCollider2D>().radius = 0.04f;
        Projectile projectile = root.AddComponent<Projectile>();
        projectile.Initialize(boss, Vector2.up, 0f, 8f, 1f);
        projectile.SlashResponse = response;
        projectile.GroggyGain = groggyGain;
        projectile.ReflectionSpeed = 35f;
        root.AddComponent<Hitbox>();
        var duplicate = new GameObject("DuplicateProjectileCollider");
        duplicate.transform.SetParent(root.transform, false);
        duplicate.AddComponent<CircleCollider2D>().radius = 0.04f;
        duplicate.AddComponent<Hitbox>();
        Physics2D.SyncTransforms();
        return projectile;
    }

    /// <summary>확정 경로를 보관하고 Character의 실제 Slash 시작 API 호출</summary>
    private static void Start(PlayerCharacter player, Line line)
    {
        player.Skills.SetLine(line);
        Require(player.ApplySlash(), "The Character must accept a fresh valid Slash after the previous action is cleared.");
    }

    /// <summary>Slash가 소유한 입력·시간·표현·보호가 모두 복구됐는지 확인</summary>
    private static void AssertRestored(GameManager manager, PlayerInputController input, UIController ui, GameCamera camera, PlayerCharacter player, DrawVfx vfx, bool successEffect = false)
    {
        SlashPresentationFx effect = player.GetComponent<SlashPresentationFx>();
        Require(!player.IsSlashing && !manager.IsSlashPaused && !input.IsSlashInput && !ui.IsSlashMode && !camera.IsSlashMode
            && !vfx.IsSlashPlaying && !vfx.SlashObject.activeSelf && !vfx.IsSlashLineVisible && effect.IsSpaceSlashPlaying == successEffect,
            "Slash completion or interruption must restore all owned state and show energy VFX only on success.");
    }

    /// <summary>일반 수신자를 생성하고 여러 Collider를 같은 Character로 연결</summary>
    private static Character CreateTarget(List<GameObject> fixtures, string name, Vector2 position, TargetKind kind, int colliderCount = 1)
    {
        var root = new GameObject(name);
        fixtures.Add(root);
        root.transform.position = position;
        Character target = root.AddComponent<Character>();
        target.maxHP = 1000f;
        target.InitializeHealth(1000f);
        for (int i = 0; i < colliderCount; i++)
        {
            var part = new GameObject("HitPart" + i);
            part.transform.SetParent(root.transform, false);
            BoxCollider2D collider = part.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.08f;
            collider.isTrigger = true;
            Hitbox hitbox = part.AddComponent<Hitbox>();
            hitbox.Owner = target;
            hitbox.TargetKind = kind;
            hitbox.PartId = i;
        }
        Physics2D.SyncTransforms();
        return target;
    }

    /// <summary>Line 중간의 벽이 Slash를 차폐하지 않는지 확인할 실제 Collider 준비</summary>
    private static void CreateWall(List<GameObject> fixtures, Vector2 position)
    {
        var wall = new GameObject("SlashNonOccludingWall");
        fixtures.Add(wall);
        wall.transform.position = position;
        wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.4f;
        Physics2D.SyncTransforms();
    }

    /// <summary>경로 소비 뒤 실행 중인 기하 값과 원본이 같은지 비교</summary>
    private static bool SameLine(Line first, Line second)
    {
        return Vector2.Distance(first.StartPoint, second.StartPoint) < 0.001f && Vector2.Distance(first.Direction, second.Direction) < 0.001f
            && Mathf.Abs(first.Length - second.Length) < 0.001f && Mathf.Abs(first.Width - second.Width) < 0.001f;
    }

    /// <summary>키보드의 현재 전체 상태를 실제 Input System 이벤트로 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    /// <summary>한 키 누름과 해제를 독립 요청으로 처리</summary>
    private static IEnumerator Press(Keyboard keyboard, Key key)
    {
        Keys(keyboard, key);
        yield return Frames(2); // 실제 입력 콜백과 Player의 요청 전달 대기
        Keys(keyboard);
        yield return Frames(2); // 다음 입력을 위한 해제 반영 대기
    }

    /// <summary>Transform과 Rigidbody의 위치를 공간 질의 전에 함께 적용</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>지정한 렌더 프레임만큼 실제 입력·Player 갱신 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 입력·능력·카메라 갱신이 지정한 프레임만큼 진행될 때까지 대기
    }

    /// <summary>게임 시간 기준 대기가 실제 정지 정책을 따르는지 관찰</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game seconds"); // 게임 시간으로만 진행하는 확정 대기 종료 관찰
    }

    /// <summary>게임 정지와 별개로 전용 연출이 진행되는 실제 시간 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 게임 시간이 멈춘 채 unscaled 연출 진행 관찰
    }

    /// <summary>상태 전이를 기존 실행기에서 기다리고 무한 대기 방지</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P07 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>Slash의 입력·판정·복원 계약 위반을 기존 결과 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
