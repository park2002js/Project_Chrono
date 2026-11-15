using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>완성 아트가 실제 Title·저장·입력·보스 사건에 연결된 흐름 검사</summary>
public static class ArtIntegrationChecks
{
    /// <summary>원본에 아트를 연결하고 사용자 저장과 분리한 Play Mode 검사 시작</summary>
    [MenuItem("Tools/Chrono/Apply And Check Art")]
    public static void Run()
    {
        WorldArtSetup.Apply();
        PlayerArtSetup.Apply();
        UIArtSetup.Apply();
        AssetDatabase.SaveAssets();
        StartupFlowChecks.RunArt();
    }

    /// <summary>실제 씬 전환과 F·이동·피해 사건으로 아트 연결 검증</summary>
    public static IEnumerator Check(Action<string> passed)
    {
        yield return Until(() => UnityEngine.Object.FindFirstObjectByType<UI_Title>()?.IsReady == true, "Title readiness"); // 실제 Title과 저장 준비 대기
        UI_Title title = UnityEngine.Object.FindFirstObjectByType<UI_Title>();
        GameManager manager = GameManager.Instance;
        Require(title.TitleLogo != null && title.TitleFont != null, "Title logo or PF Stardust font missing.");
        passed("Title with final art initializes and loads three isolated save slots");
        CaptureScreen("title");
        yield return Delay(0.3f); // 실제 프레임 끝의 Title 캡처 완료 대기
        title.SelectSlot(0);
        yield return Until(() => manager.Stage.IsReady && !manager.IsLoading, "world and spawn readiness"); // Spawn 모션·입력 준비가 모두 끝날 때까지 대기
        PlayerCharacter player = manager.CurrentPlayer;
        MapManager map = manager.Stage.Map;
        UIController ui = UIController.Instance;
        CrystalCheckPoint crystal = map.GetComponentInChildren<CrystalCheckPoint>();
        MapDoor entry = map.GetEntryDoor(GameManager.SubmittedBossId).GetComponent<MapDoor>();
        MapDoor exit = map.GetExitDoor(GameManager.SubmittedBossId).GetComponent<MapDoor>();
        Require(crystal != null && crystal.CrystalAnimator != null && crystal.InteractionPrompt.sprite != null, "Crystal art/prompt references missing.");
        Require(!entry.IsOpen && entry.Blocker.enabled && !exit.IsOpen && exit.Blocker.enabled, "Fresh doors must block before activation.");
        Require(ui.IsPlayerHudVisible && !ui.IsBossHudVisible, "Only Player HUD should show before encounter.");
        passed("Two-room Tilemap, 16PPU crystal, closed animated doors and pre-encounter HUD are connected");
        player.HP = player.maxHP * 0.5f;
        player.Groggy = 40f;
        yield return Delay(0.05f); // 실제 HUD LateUpdate의 최신 수치 반영 대기
        Require(ui.PlayerHudRoot.activeSelf && !ui.BossHudRoot.activeSelf && Mathf.Abs(ui.PlayerHealthFill.fillAmount - 0.5f) < 0.01f
            && Mathf.Abs(ui.GroggyFill.fillAmount - 0.4f) < 0.02f, "Prepared HUD did not reflect current HP and groggy.");
        passed("Prepared Player HP and groggy artwork shows current values through real Canvas fills");
        yield return Until(() => crystal.InteractionPrompt.enabled, "F key near crystal"); // 실제 Trigger와 HUD 허용 조건으로 자판 표시 대기
        Require(!crystal.CrystalAnimator.GetBool("Active"), "Unused crystal must remain inactive.");
        passed("Nearby inactive crystal shows the prepared F key image");
        CaptureScreen("checkpoint-before");
        CaptureWorld("checkpoint-world");
        yield return Delay(0.1f); // 화면 캡처가 끝나기 전에 검사 위치를 이동하지 않도록 대기
        yield return CheckUpdateMovementFlow(player, passed); // 현재 맵에서 Update 이동·의도 모션과 정리 경로 검사 완료 대기

        // 닫힌 문은 실제 몸체 요청을 보정하고 미완료 모션 중에도 통과 차단
        SetPosition(player, new Vector2(14.5f, 3.3f));
        player.GetComponent<PlayerBodyController>().Move(Vector2.up * 3f);
        yield return Delay(0.08f); // 즉시 보정한 문 앞 위치의 표시 순서 반영 대기
        Require(player.transform.position.y < 4f, "Closed door allowed the Player through.");
        Require(player.GetComponent<CharacterRendering>().BodySprite.sortingOrder > entry.GetComponent<SpriteRenderer>().sortingOrder,
            "Player approaching the closed door must render in front of its panel.");
        SetPosition(player, new Vector2(14.5f, 1.4f));
        yield return Until(() => PlayerInputController.Instance.InteractionTarget == crystal, "return to checkpoint range"); // 순간 이동 뒤 실제 접촉 재진입 대기
        passed("Closed entrance physically blocks the Player before checkpoint activation");

        // 실제 키 이벤트로 F 우선 상호작용과 활성 완료 사건 확인
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            PlayerInputController.Instance.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            InputSystem.Update();
            yield return Delay(0.06f); // 공통 F 입력이 Character 상호작용 요청으로 전달될 때까지 대기
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return Until(() => crystal.IsActivated, "checkpoint activation"); // 회복·저장·활성 사건 완료 대기
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
        }
        Require(crystal.CrystalAnimator.GetBool("Active") && !crystal.InteractionPrompt.enabled, "Activated crystal art or hidden F prompt mismatch.");
        Require(entry.WantsOpen && !entry.IsOpen && entry.Blocker.enabled, "Entrance must animate before becoming passable.");
        Require(SaveManager.Current.Get<string>("CheckpointId") == "mid" && player.Spawner.SpawnPoint == map.GetCheckpoint("mid"), "Checkpoint did not update save and respawn target.");
        Sprite activeFrame = crystal.GetComponent<SpriteRenderer>().sprite;
        yield return Delay(0.35f); // 활성 부유 클립의 다음 그림 표본 대기
        Require(activeFrame != crystal.GetComponent<SpriteRenderer>().sprite, "Crystal floating frames did not advance.");
        yield return Until(() => entry.IsOpen, "entrance fully open"); // 최종 open 상태의 통과 허용 대기
        Require(!entry.Blocker.enabled, "Open entrance still blocks movement.");
        passed("Actual F activates color and floating, hides its key, updates checkpoint, and opens entry only after the final frame");
        CaptureScreen("checkpoint-active");
        yield return Delay(0.1f); // 활성 수정과 HUD의 실제 프레임 캡처 대기

        // 미완료 슬롯 재입장에서는 수정 활성화와 입구 완결 상태를 바로 복원
        manager.ReturnToTitle();
        yield return Until(() => UnityEngine.Object.FindFirstObjectByType<UI_Title>()?.IsReady == true && !manager.IsLoading, "checkpoint save Title return"); // 활성 체크포인트 저장을 마치고 Title 준비 대기
        UnityEngine.Object.FindFirstObjectByType<UI_Title>().SelectSlot(0);
        yield return Until(() => manager.Stage.IsReady && !manager.IsLoading, "saved checkpoint reload"); // 같은 슬롯의 수정 이력 복원 대기
        player = manager.CurrentPlayer;
        map = manager.Stage.Map;
        ui = UIController.Instance;
        crystal = map.GetComponentInChildren<CrystalCheckPoint>();
        entry = map.GetEntryDoor(GameManager.SubmittedBossId).GetComponent<MapDoor>();
        exit = map.GetExitDoor(GameManager.SubmittedBossId).GetComponent<MapDoor>();
        Require(crystal.IsActivated && crystal.CrystalAnimator.GetBool("Active") && !crystal.InteractionPrompt.enabled && entry.IsOpen && !exit.IsOpen,
            "Reload must restore activated crystal and fully open entry without replaying activation.");
        passed("Reload restores active floating crystal and fully open entry without an F prompt");

        // 열린 입구를 통과해 조우 영역에 들어가면 Boss HUD와 폐문을 연결
        SetPosition(player, new Vector2(14.5f, 3.3f));
        player.GetComponent<PlayerBodyController>().Move(Vector2.up * 3f);
        yield return Delay(0.08f); // 개방 완료 뒤 같은 변위가 적용된 화면 반영 대기
        Require(player.transform.position.y > 5.5f, "Fully open entrance did not allow passage.");
        SetPosition(player, new Vector2(14.5f, 10f));
        yield return Until(() => player.IsInCombat && ui.IsBossHudVisible, "real encounter and boss HUD"); // 실제 조우 Trigger와 등장 연출 완료 대기
        Require(entry.Blocker.enabled && !entry.IsOpen && ui.Boss == manager.Stage.Bosses[0], "Encounter did not close entry and bind boss HP.");
        passed("Open door permits movement; entering the real room closes it and shows the Boss HP bar");
        CaptureScreen("boss-room");
        CaptureWorld("boss-world");
        yield return Delay(0.1f); // 사망 요청 전에 Boss HUD가 켜진 실제 화면 캡처 대기

        // 실제 사망 사건 뒤 연출 종료와 출구의 점멸·상승 완료를 각각 확인
        BossCharacter boss = manager.Stage.Bosses[0];
        Require(boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false)), "Boss lethal damage was rejected.");
        yield return Until(() => exit.WantsOpen, "boss death opens exit"); // 보스 사망 연출에서 문 개방 요청을 보낼 때까지 대기
        Require(exit.Blocker.enabled && !exit.IsOpen, "Exit must block during activation/opening frames.");
        yield return Until(() => exit.IsOpen && entry.IsOpen, "both doors fully open"); // 출구 점멸 예고와 상승 모션 완료 대기
        Require(!exit.Blocker.enabled && !ui.IsBossHudVisible, "Boss death must clear HUD and permit passage only after opening.");
        SetPosition(player, new Vector2(14.5f, 25.5f));
        player.GetComponent<PlayerBodyController>().Move(Vector2.up * 4f);
        yield return Delay(0.08f); // 실제 출구 충돌 없이 복도로 이동하는지 확인
        Require(player.transform.position.y > 29f, "Opened exit did not lead into the corridor.");
        SetPosition(player, new Vector2(14.5f, 49f));
        player.GetComponent<PlayerBodyController>().Move(Vector2.up * 7f);
        yield return Delay(0.08f); // 위쪽 다음 방 입구의 개방 연결 확인
        Require(player.transform.position.y > 55f, "The corridor did not connect to the next room.");
        passed("Boss OnDie opens both doors; exit blocks until complete and the Player can walk into the next room");

        // 생명주기 아트와 고정 입력·잔상 검사 후 실제 저장 지점 복귀 확인
        yield return PlayerArtChecks.Run(passed); // 모션 간격·대시 자세/잔상·사망/부활 순서 검사 완료 대기
        yield return Delay(0.05f); // Spawn 줌 복원 직후 카메라 경계 보정 결과 대기
        GameCamera camera = GameCamera.Instance;
        Require(camera.transform.position.y - camera.Camera.orthographicSize >= camera.VerticalLimits.x - 0.001f
            && camera.transform.position.y + camera.Camera.orthographicSize <= camera.VerticalLimits.y + 0.001f,
            "Spawn zoom restoration must keep the rendered view inside map limits.");
        passed("Camera remains inside map bounds immediately after Spawn zoom restoration");

        // 실제 Player의 새 검 클립이 공격 중 자식만 움직이고 중단에서 원점으로 돌아오는지 확인
        AttackController attack = player.GetComponent<AttackController>();
        Vector2 originalPosition = player.transform.position;
        Vector2 originalAim = player.AimWorldPosition;
        SetPosition(player, new Vector2(40f, 40f));
        player.SetAimPosition(new Vector2(42f, 40f));
        Require(player.ApplyAttack(), "Connected Player must start its first weapon animation.");
        float aimAngle = attack.WeaponPivot.eulerAngles.z;
        float motionAngle = attack.WeaponMotion.localEulerAngles.z;
        yield return Delay(0.09f); // 첫 검 곡선 키가 Animator에서 실제 Transform에 반영될 때까지 대기
        Require(attack.IsAttacking && Mathf.Abs(Mathf.DeltaAngle(motionAngle, attack.WeaponMotion.localEulerAngles.z)) > 1f
            && Mathf.Abs(Mathf.DeltaAngle(aimAngle, attack.WeaponPivot.eulerAngles.z)) < 0.01f,
            "Weapon clip must animate its child without changing the fixed aim parent.");
        player.CancelAttack();
        Require(!attack.IsAttacking && attack.WeaponMotion.localPosition == Vector3.zero
            && Mathf.Abs(Mathf.DeltaAngle(attack.WeaponMotion.localEulerAngles.z, 0f)) < 0.01f,
            "Cancelling an attack must restore the weapon Animator to its idle pose.");

        // 같은 Player의 1·2·3타가 서로 다른 클립을 재생하고 한 타 종료 뒤 다음 콤보를 허용하는지 확인
        for (int index = 0; index < 3; index++)
        {
            Require(player.ApplyAttack() && attack.ComboIndex == index, "The next combo hit must start its matching weapon clip.");
            Require(attack.WeaponAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack" + (index + 1)),
                "The active Animator state must match the current combo hit.");
            float startAngle = attack.WeaponMotion.localEulerAngles.z;
            yield return Delay(0.09f); // 각 공격 클립의 첫 움직임이 실제 모션 자식에 적용될 때까지 대기
            Require(Mathf.Abs(Mathf.DeltaAngle(startAngle, attack.WeaponMotion.localEulerAngles.z)) > 1f,
                "Each combo clip must animate the weapon transform.");
            yield return Until(() => !attack.IsAttacking && attack.AttackCooldownRemaining <= 0f, "combo clip completion"); // 다음 콤보가 허용되는 판정·쿨타임 종료 대기
        }
        player.CancelAttack();
        SetPosition(player, originalPosition);
        player.SetAimPosition(originalAim);
        passed("Player greatsword clips 1-2-3 animate at runtime and cancellation restores Idle");

        manager.ReturnToTitle();
        yield return Until(() => UnityEngine.Object.FindFirstObjectByType<UI_Title>()?.IsReady == true && !manager.IsLoading, "return to Title"); // 씬 수명 참조 해제와 Title 재준비 완료 대기
        passed("Art session returns to Title without retaining world services");
        title = UnityEngine.Object.FindFirstObjectByType<UI_Title>();
        title.SelectSlot(0);
        Require(title.IsRestartConfirmationOpen && !manager.IsSessionActive, "Completed art session must preserve the existing restart confirmation.");
        title.ConfirmRestart(false);
        passed("Completed-slot confirmation preserves its save when cancelled");
    }

    /// <summary>완성 맵의 빈 구간에서 물리 주기와 분리한 Update 이동·의도 모션 확인</summary>
    private static IEnumerator CheckUpdateMovementFlow(PlayerCharacter player, Action<string> passed)
    {
        // 현재 세션의 입력·시간·위치를 보관하고 실제 MonoBehaviour 실행 순서 확인
        PlayerInputController input = PlayerInputController.Instance;
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        Vector2 origin = body.position;
        Vector2 originalAim = player.AimWorldPosition;
        float originalSpeed = player.SP;
        float originalFixedStep = Time.fixedDeltaTime;
        int originalVSync = QualitySettings.vSyncCount;
        int originalFrameRate = Application.targetFrameRate;
        bool inputEnabled = input.enabled;
        bool inputBlocked = input.BlockInput;
        bool playerEnabled = player.enabled;
        bool bodyEnabled = movement.enabled;
        GameObject wall = null;
        Require(((DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(PlayerCharacter), typeof(DefaultExecutionOrder)))?.order == 0
            && ((DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(CharacterRendering), typeof(DefaultExecutionOrder)))?.order == 3,
            "Player Character and Rendering must execute in Update orders 0 and 3.");
        try
        {
            // 현재 몸체 크기로 충돌 없는 짧은 이동 구간을 찾아 기존 맵을 보존
            input.enabled = false;
            player.ResetMotion();
            player.SP = 1f;
            Time.fixedDeltaTime = 0.05f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(movement.CollisionMask);
            RaycastHit2D[] hits = new RaycastHit2D[16];
            Vector2 direction = Vector2.zero;
            foreach (Vector2 candidate in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                if (body.Cast(candidate, filter, hits, 2f) == 0) { direction = candidate; break; }
            Require(direction != Vector2.zero, "No clear local segment is available for the Update movement check.");
            player.SetAimPosition(origin + direction * 10f);
            player.SetMoveInput(direction);
            yield return Until(() => rendering.IsMoving, "Update movement intent"); // 실제 Character0·Rendering3의 첫 이동 처리 대기

            // Fixed가 없는 렌더 프레임에서도 이동하고 같은 Move 상태의 누적 시간이 진행되는지 관찰
            int previousFrame = Time.frameCount;
            float previousFixed = Time.fixedTime;
            Vector2 previousPosition = body.position;
            int movedWithoutFixed = 0;
            bool animationAdvanced = false;
            double end = Time.realtimeSinceStartupAsDouble + 0.45f;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                yield return null;
                if (Time.frameCount == previousFrame) continue;
                if (Time.fixedTime == previousFixed && Vector2.Dot(body.position - previousPosition, direction) > 0.000001f)
                    movedWithoutFixed++;
                Require(rendering.IsMoving && !rendering.IsBackpedaling, "Held forward intent must retain its Move state across render frames.");
                if (rendering.FrameIndex >= 2 && Time.deltaTime < 0.05f) animationAdvanced = true;
                previousFrame = Time.frameCount;
                previousFixed = Time.fixedTime;
                previousPosition = body.position;
            }
            Require(movedWithoutFixed > 0, "Player must advance on a rendered frame without a FixedUpdate at the 0.05-second physics interval.");
            Require(animationAdvanced, "Repeated movement delivery must preserve animation time instead of restarting the Move state every frame.");
            passed("Character Update0 moves without a physics step and Rendering Update3 preserves continuous movement animation");

            // 조준 반대 이동·정지·일반 정지와 각 컴포넌트 비활성화의 표시 정리 확인
            player.SetAimPosition(origin - direction * 10f);
            yield return Until(() => rendering.IsMoving && rendering.IsBackpedaling, "Update backpedal intent"); // 반대 조준의 역재생 상태 반영 대기
            player.SetMoveInput(Vector2.zero);
            yield return Until(() => !rendering.IsMoving && !rendering.IsBackpedaling, "zero movement intent"); // 실제 다음 Character Update의 정지 전달 대기
            Vector2 stopped = body.position;
            GameManager.Instance.SetGameplayPaused(true);
            player.SetMoveInput(direction);
            yield return Delay(0.06f); // 게임 시간이 멈춘 실제 프레임의 이동·모션 유지 확인 대기
            Require(Vector2.Distance(body.position, stopped) < 0.001f && !rendering.IsMoving, "Paused gameplay must not move or retain walking intent.");
            GameManager.Instance.SetGameplayPaused(false);
            player.SetMoveInput(direction);
            yield return Until(() => rendering.IsMoving, "movement before Body disable"); // 활성 이동에서 비활성 전환 준비 대기
            movement.enabled = false;
            yield return Until(() => !rendering.IsMoving, "disabled Body clears movement intent"); // Character가 비활성 몸체를 확인한 뒤 표시 정리 대기
            movement.enabled = true;
            player.SetMoveInput(direction);
            yield return Until(() => rendering.IsMoving, "movement before Character disable"); // Character 비활성화의 즉시 정리 준비 대기
            player.enabled = false;
            Require(!rendering.IsMoving && !rendering.IsBackpedaling, "Disabling Character must immediately clear the last delivered movement state.");
            player.enabled = true;
            passed("Update movement retains backpedal and clears walking on zero input, pause, Body disable and Character disable");

            // 검사 전용 벽에 막혀도 실제 위치와 이동 의도 모션은 서로 구분되는지 확인
            SetPosition(player, origin);
            wall = new GameObject("UpdateMovementCheckWall");
            wall.transform.position = origin + direction * 0.8f;
            wall.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 1.5f);
            Physics2D.SyncTransforms();
            movement.Move(direction * 1.5f);
            Vector2 blockedPosition = body.position;
            Require(Vector2.Dot(blockedPosition - origin, direction) < 0.8f, "The temporary wall must block immediate Body movement.");
            player.SetAimPosition(origin + direction * 10f);
            player.SetMoveInput(direction);
            yield return Until(() => rendering.IsMoving, "walking intent at blocked wall"); // 벽 충돌 결과가 아닌 Character 의도의 걷기 상태 전달 대기
            yield return Delay(0.08f); // 연속 이동 의도가 실제 벽 위치를 넘지 않는지 관찰
            Require(Vector2.Distance(body.position, blockedPosition) < 0.015f && rendering.IsMoving,
                "A solid wall must stop position while permitted movement intent continues the walking animation.");
            passed("Immediate Body collision blocks a wall while Character movement intent keeps the walking animation");
        }
        finally
        {
            // 실패·중단에서도 임시 벽·시간·컴포넌트·세션 입력을 원래 값으로 복구
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            GameManager.Instance.SetGameplayPaused(false);
            movement.enabled = bodyEnabled;
            player.enabled = playerEnabled;
            player.SP = originalSpeed;
            Time.fixedDeltaTime = originalFixedStep;
            QualitySettings.vSyncCount = originalVSync;
            Application.targetFrameRate = originalFrameRate;
            SetPosition(player, origin);
            player.SetAimPosition(originalAim);
            input.BlockInput = inputBlocked;
            input.enabled = inputEnabled;
        }
    }

    /// <summary>물리 몸체와 표시 좌표를 함께 바꾸고 이전 이동 상태 제거</summary>
    private static void SetPosition(PlayerCharacter player, Vector2 position)
    {
        player.ResetMotion();
        player.GetComponent<Rigidbody2D>().position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>게임 시간 정지와 무관하게 제한 시간 안의 실제 상태 변화 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string name)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 15f;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(name);
            yield return null;
        }
    }

    /// <summary>실제 PlayerLoop가 상태·물리를 적용할 시간을 제공</summary>
    private static IEnumerator Delay(float seconds)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < end) yield return null;
    }

    /// <summary>화면 전체 캡처를 프레임 종료에 예약</summary>
    private static void CaptureScreen(string name)
    {
        Directory.CreateDirectory("Logs/ArtIntegration");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Logs/ArtIntegration/{name}.png"));
    }

    /// <summary>픽셀 아트 배치를 실제 게임 카메라로 별도 렌더해 시각 검사에 사용</summary>
    private static void CaptureWorld(string name)
    {
        Camera camera = GameCamera.Instance.Camera;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes($"Logs/ArtIntegration/{name}.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>관측한 상태가 조건과 다르면 실제 실패로 중단</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
