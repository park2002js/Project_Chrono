using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;

/// <summary>실제 수정·조우·황금 단발과 그로기·사망 복구를 격리된 저장에서 확인</summary>
[InitializeOnLoad]
public static class GuardBossChecks
{
    private const string ActiveKey = "Chrono.GuardBossChecks.Active"; // 도메인 재로드 뒤 검사 요청 복원 키
    private const string FinishedKey = "Chrono.GuardBossChecks.Finished"; // 편집 모드 복귀에서 검사 종료 구분 키
    private const string SuccessKey = "Chrono.GuardBossChecks.Success"; // 배치 종료 코드로 전달할 실제 검사 결과 키
    private const string SceneKey = "Chrono.GuardBossChecks.Scene"; // 검사 후 복귀할 원래 편집 씬 경로 키
    private const string SaveKey = "Chrono.GuardBossChecks.Save"; // 사용자 저장과 분리한 임시 저장 경로 키
    private static Stack<IEnumerator> routines; // 중첩 검사와 대기를 차례로 실행할 스택
    private static Report report; // 실제 통과 항목과 실패 사유를 보관할 결과
    private static double deadline; // 검사 무한 대기를 막을 Editor 종료 시각

    /// <summary>Play Mode 재로드 이후에도 요청한 검사만 실행</summary>
    static GuardBossChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode; // 실제 Play 진입과 종료에서 검사 시작·결과 처리
    }

    /// <summary>경비 프리팹을 준비하고 격리된 실제 Title 세션으로 검사 시작</summary>
    [MenuItem("Tools/Chrono/Check Guard Boss First Shot")]
    public static void RunBatch()
    {
        // 저장하지 않은 사용자 씬을 검사 진입으로 덮어쓰지 않도록 제한
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin Guard checks outside Play Mode.");
        Scene original = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (SceneManager.sceneCount != 1 || original.isDirty || string.IsNullOrEmpty(original.path)))
            throw new InvalidOperationException("Guard checks require one saved, unmodified scene.");
        SessionState.SetString(SceneKey, original.path);
        GuardBossSetup.Run();
        string directory = Path.GetFullPath(Path.Combine("Temp", "GuardBossChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        SessionState.SetString(SaveKey, directory);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (manager == null) throw new InvalidOperationException("The actual Title must contain GameManager.");
        manager.SaveDirectory = directory;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>요청한 Play 진입에서 검사하고 종료 뒤 배치 코드 또는 원래 씬 복귀</summary>
    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // 일반 Play 실행에는 검사 상태를 연결하지 않음
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(Check()); // 실제 생성·전투·복구 흐름을 순서대로 실행
            deadline = EditorApplication.timeSinceStartup + 150d;
            EditorApplication.update += Tick; // 실제 렌더 프레임 사이에서 검사 대기와 전체 제한 확인
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
            else EditorSceneManager.OpenScene(SessionState.GetString(SceneKey, "Assets/Scenes/Title.unity"));
        }
    }

    /// <summary>실제 프레임을 양보하며 중첩 검사를 진행하고 예외 저장</summary>
    private static void Tick()
    {
        try
        {
            // 하위 조건 대기부터 진행하고 무한 검사 방지
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Guard boss checks exceeded 150 seconds.");
            while (routines.Count > 0)
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); (routine as IDisposable)?.Dispose(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>사용자가 만나는 실제 생성·F·Trigger·패턴·사망 경계 확인</summary>
    private static IEnumerator Check()
    {
        // 새 슬롯의 실제 Map·Player·파생 경비가 준비될 때까지 대기
        yield return Until(() => GameManager.Instance != null && Title() != null && Title().IsReady, "Title readiness"); // 공유 저장과 Title 준비 완료 대기
        GameManager manager = GameManager.Instance;
        Title().SelectSlot(0);
        yield return Until(() => !manager.IsLoading && manager.Stage.IsReady && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsSpawning, "Guard session readiness"); // 맵·경비·Player 생성과 초기 기상 완료 대기
        StageManager stage = manager.Stage;
        Boss_01 boss = manager.CurrentBoss as Boss_01;
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Require(boss != null && boss.PatternsInitialized, "The live session must create the derived Guard before isolating its regression.");
        BossPattern goldPattern = null;
        foreach (BossPattern candidate in boss.PatternSelector.Patterns)
            if (candidate.PatternId == "F-B07-01") goldPattern = candidate;
        Require(goldPattern != null, "The production guard must retain F-B07-01 for its isolated golden-shot regression.");
        boss.PatternSelector.SetPatterns(goldPattern);
        Require(boss != null && boss.PatternsInitialized && boss.Muzzles.Length == 5 && boss.MuzzleIndex == 2
            && boss.PatternSelector.Patterns.Count == 1 && boss.PatternSelector.Patterns[0].PatternId == "F-B07-01"
            && boss.SP == 0f && boss.basicSP == 0f && boss.transform.position == stage.Map.GetBossPosition(boss.BossId).position,
            "The live session must instantiate the configured stationary Boss_01 at its updated map position.");
        SpriteRenderer bossVisual = boss.transform.Find("Visual").GetComponent<SpriteRenderer>();
        Require(bossVisual.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(GuardBossSetup.BodySpritePath)
            && bossVisual.transform.localScale == Vector3.one && bossVisual.sprite.pixelsPerUnit == 16f,
            "The actual guard must display its latest native Sprite at natural 16 PPU scale.");
        Passed("Actual Title session creates the production guard; this regression explicitly isolates F-B07-01 while preserving its native body and five muzzles");
        BossSpawner spawner = boss.GetComponent<BossSpawner>();
        GameObject entry = stage.Map.GetEntryDoor(boss.BossId);
        GameObject exit = stage.Map.GetExitDoor(boss.BossId);
        CrystalCheckPoint crystal = null;
        foreach (CrystalCheckPoint candidate in stage.Map.GetComponentsInChildren<CrystalCheckPoint>(true))
            if (candidate.BossId == boss.BossId && !candidate.IsActivated) crystal = candidate;
        Require(crystal != null && !DoorOpen(entry) && !boss.IsInCombat && Shots(boss).Length == 0,
            "A fresh session must begin before the guard's entry crystal and combat activation.");
        Keyboard originalKeyboard = Keyboard.current;
        PlayerInput actions = input.GetComponent<PlayerInput>();
        InputDevice[] previousDevices = new InputDevice[actions.devices.Count];
        for (int i = 0; i < previousDevices.Length; i++) previousDevices[i] = actions.devices[i];
        string previousScheme = actions.currentControlScheme;
        InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>("GuardBossCheckKeyboard");
        Mouse mouse = InputSystem.AddDevice<Mouse>("GuardBossCheckMouse");
        InputAction slashAction = actions.actions.FindAction("Player/Slash", true);
        int fPerformed = 0;
        Action<InputAction.CallbackContext> onFPerformed = context => fPerformed++;
        slashAction.performed += onFPerformed; // 가상 F가 실제 액션에 전달됐는지 입력 경계 확인
        Transform captureAnchor = null;
        try
        {
            // 실제 수정 Trigger와 Input Actions의 F 입력으로 입구를 개방
            actions.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            input.enabled = true;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            player.Buffs.Clear();
            player.IsInvulnerable = true;
            SetPosition(body, (Vector2)crystal.transform.position + Vector2.left * 3f);
            yield return Frames(3); // 이전 수정 접촉과 키 상태의 물리 갱신 대기
            SetPosition(body, crystal.transform.position);
            yield return Until(() => input.InteractionTarget == crystal, "actual entry crystal trigger"); // 실제 수정 진입 후보 등록 대기
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            InputSystem.Update();
            yield return Frames(2); // 실제 F 액션 콜백의 수정 활성화 요청 대기
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Require(fPerformed == 1 && (crystal.IsActivating || crystal.IsActivated),
                "The paired F must perform once and begin crystal activation; performed=" + fPerformed
                + ", actionEnabled=" + slashAction.enabled + ", canMove=" + player.CanMove + ", blocked=" + input.BlockInput
                + ", activating=" + crystal.IsActivating + ", activated=" + crystal.IsActivated);
            yield return Until(() => crystal.IsActivated && !player.IsInteracting && DoorOpen(entry), "crystal F activation and door opening"); // 수정 완료와 실제 입구 개방 모션 종료 대기
            input.enabled = false;
            Require(SaveManager.Current.Get<List<string>>("ActivatedCheckpoints").Contains(crystal.CheckpointId)
                && !boss.IsInCombat && Shots(boss).Length == 0, "F activation must open the entry and save its checkpoint before boss combat.");
            yield return Enter(stage, boss, body, false); // 실제 Player의 조우 영역 진입과 첫 연출 정상 종료 대기
            Require(UIController.Instance.IsBossHudVisible && boss.Target == player && player.IsInCombat && !DoorOpen(entry)
                && SaveManager.Current.Get<List<string>>("EncounteredBosses").Contains(boss.BossId),
                "Encounter completion must close entry, show boss HUD, record encounter and bind the cached Player target.");
            yield return WaitGame(0.2f); // 총구의 활성 거리 밖에서는 선택·발사를 기다리는지 확인
            Require(boss.CurrentPattern == null && boss.PatternSelector.Patterns[0].UseCount == 0 && Shots(boss).Length == 0,
                "A distant Player must enter combat without triggering the first shot outside its activation range.");
            Passed("Actual crystal F and physical encounter start combat and HUD; the distant guard waits until the Player approaches within activation range");

            // 준비 중 게임 정지는 발사와 패턴 경과 시간을 함께 유지
            Approach(boss, body);
            yield return Until(() => boss.CurrentPattern != null, "approach activates the first guard warmup"); // 실제 총구 거리 조건을 통과한 패턴 선택 대기
            BossPattern pattern = boss.PatternSelector.Patterns[0];
            Require(pattern.IsRunning && pattern.Elapsed < boss.Warmup && Shots(boss).Length == 0, "The first guard pattern must begin with its configured warmup.");
            float beforePause = pattern.Elapsed;
            manager.SetGameplayPaused(true);
            yield return WaitReal(0.2f); // 게임 시간 정지 중 실제 시간 경과 대기
            Require(pattern.Elapsed == beforePause && Shots(boss).Length == 0, "Paused warmup must not advance or fire.");
            manager.SetGameplayPaused(false);
            Passed("Pausing during the guard warmup freezes elapsed game time and prevents firing until resumed");
            yield return Until(() => Shots(boss).Length > 0, "first guard golden shot"); // 실제 Boss Update가 단발을 생성할 때까지 대기
            GoldProjectile shot = Shots(boss)[0];
            Vector3 origin = shot.transform.position - (Vector3)(shot.Direction * shot.Speed * (shot.Lifetime - shot.RemainingLifetime));
            Vector2 expectedDirection = ((Vector2)player.transform.position - (Vector2)boss.Muzzles[boss.MuzzleIndex].position).normalized;
            Require(Shots(boss).Length == 1 && shot.Owner == boss && shot.transform.parent == boss.transform.parent
                && Vector3.Distance(origin, boss.Muzzles[2].position) < 0.002f && Vector2.Dot(shot.Direction, expectedDirection) > 0.9999f
                && shot.Speed == boss.BulletSpeed && shot.Damage == boss.ProjectileDamage && pattern.UseCount == 1
                && shot.GetComponent<SpriteRenderer>().sortingOrder == 30 && shot.GetComponent<ProjectileVfx>().Halo.sortingOrder == 29
                && shot.GetComponent<SpriteRenderer>().sortingOrder > bossVisual.sortingOrder
                && shot.GetComponent<ProjectileVfx>().Halo.sortingOrder > bossVisual.sortingOrder
                && shot.GetComponent<ProjectileVfx>().Halo.sortingOrder > 19,
                "The actual gold shot must come from muzzle 2 once, retain Owner and fly along its snapshot Player direction.");
            Passed("The first actual golden projectile reconstructs to the center muzzle, keeps guard ownership and snapshots the Player direction exactly once");

            // 실제 월드의 보스·문·총구·탄을 카메라와 연속 PNG로 보관
            Camera captureCamera = GameCamera.Instance.Camera;
            Vector3 muzzleInView = captureCamera.WorldToViewportPoint(boss.Muzzles[boss.MuzzleIndex].position);
            Require(GameCamera.Instance.Target == player.transform && muzzleInView.z > 0f
                && muzzleInView.x > 0f && muzzleInView.x < 1f && muzzleInView.y > 0f && muzzleInView.y < 1f,
                "The first firing muzzle must be visible in the actual Player-following game camera.");
            Capture(captureCamera, "player-view-first-shot.png");
            captureAnchor = new GameObject("GuardVerificationReviewAnchor").transform;
            captureAnchor.position = boss.transform.position + Vector3.up * 0.5f;
            GameCamera.Instance.Follow(captureAnchor, true);
            SpriteRenderer doorSprite = exit.GetComponentInChildren<SpriteRenderer>();
            Require(doorSprite != null && bossVisual.bounds.Intersects(doorSprite.bounds), "The moved guard Sprite must overlap the exit door's screen-space footprint.");
            Capture(captureCamera, "placement-and-first-shot.png");
            for (int i = 0; i < 12; i++)
            {
                report.frameTimes.Add(Time.timeAsDouble);
                Capture(captureCamera, "shot-" + i.ToString("D3") + ".png");
                yield return WaitGame(0.05f); // 실제 게임 프레임에서 발사 후 비행 진행 대기
            }
            GameCamera.Instance.Follow(player.transform, true);
            Passed("The actual Player-following GameCamera captures the visible shot; its temporary review anchor separately shows the guard, exit and twelve flight frames");

            // 실제 입사 방향과 BodyHitbox 중심 사이의 반사 접선으로 Gold 보상·충돌 확인
            float bossHP = boss.HP;
            player.HP = player.maxHP - 10f;
            DamageInstance slash = new DamageInstance(player, 10f, DamageType.Slash, DamageInstance.NextAttackId());
            Require(shot != null, "The guard's shot must survive until the reflection fixture.");
            Vector2 toBoss = ((Vector2)boss.GetComponentInChildren<Hitbox>().Bounds.center - (Vector2)shot.transform.position).normalized;
            Vector2 lineDirection = (shot.Direction.normalized + toBoss).normalized;
            if (lineDirection.sqrMagnitude < 0.0001f) lineDirection = new Vector2(-shot.Direction.y, shot.Direction.x);
            Require(shot.OnSlashed(player, null, slash, lineDirection) && shot.IsReflected && shot.ReflectionTarget == boss
                && Vector2.Dot(shot.Direction, toBoss) > 0.9999f
                && !shot.OnSlashed(player, boss, slash, lineDirection), "The guard's spawned gold shot must mirror its incoming direction along the fixture Line and reject duplicate Slash.");
            yield return Until(() => player.Buffs.IsFever && (shot == null || !shot.IsActive), "guard gold reflection and Fever"); // 각성 예약과 실제 반사탄의 발사자 도달 완료 대기
            Require(Mathf.Approximately(boss.HP, bossHP - boss.ProjectileDamage), "The reflected guard shot must damage its original Owner once.");
            yield return Until(() => !pattern.IsRunning && boss.CurrentPattern == null, "first pattern recovery completion"); // 단발 뒤 회복 완료와 Idle 복귀 대기
            yield return WaitGame(Mathf.Max(0.1f, boss.PatternCooldown + 0.1f - (Time.time - pattern.LastUsedTime))); // 쿨다운 경계 이후에도 직전 패턴 제외가 유지되는지 확인
            Require(pattern.UseCount == 1 && boss.CurrentPattern == null && Shots(boss).Length == 0,
                "The single registered pattern must stay Idle after one use without resetting the common previous-pattern exclusion.");
            Passed("The guard's gold shot reflects, grants Fever and damages its Owner once; recovery then preserves the current one-pattern Idle rule");

            // 재도전은 같은 객체의 이력을 지우며 준비 중 그로기는 미래 발사만 취소
            ResetForRetry(stage, player, body);
            Require(pattern.UseCount == 0 && boss.PatternSelector.LastPatternId == null && boss.CurrentPattern == null,
                "Retry must clear the existing guard pattern history.");
            yield return Enter(stage, boss, body); // 실제 재진입 연출 뒤 새 준비 구간 대기
            Require(pattern.IsRunning && pattern.Elapsed < boss.Warmup, "Retry must make the first pattern runnable again.");
            boss.EnterGroggy();
            yield return WaitGame(boss.Warmup + 0.15f); // 취소된 발사의 원래 경계가 지나도록 대기
            Require(boss.IsGroggy && boss.CurrentPattern == null && !pattern.IsRunning && Shots(boss).Length == 0,
                "Groggy during warmup must cancel the pending shot completely.");
            Passed("Living retry reuses the same guard and clears usage history; groggy before firing cancels the pending shot");

            // 발사 뒤 그로기는 기존 탄의 비행을 유지하고 실제 Player 사망에서 전투 정리
            ResetForRetry(stage, player, body);
            yield return Enter(stage, boss, body); // 같은 파생 보스의 두 번째 재진입 대기
            yield return Until(() => Shots(boss).Length == 1, "retry golden shot"); // 실제 재도전 단발 생성 대기
            GoldProjectile existingShot = Shots(boss)[0];
            Vector3 previousPosition = existingShot.transform.position;
            boss.EnterGroggy();
            yield return WaitGame(0.12f); // 그로기 중 기존 탄의 독립 비행 진행 대기
            Require(existingShot != null && existingShot.IsActive && existingShot.transform.position != previousPosition
                && boss.IsGroggy && boss.CurrentPattern == null, "Groggy must leave the already fired projectile alive and moving.");
            player.IsInvulnerable = false;
            player.ResetHitProtection();
            Require(player.ApplyDamage(new DamageInstance(boss, player.HP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false))
                && player.IsDead && !boss.IsInCombat && (existingShot == null || !existingShot.IsActive),
                "Actual Player death must end the encounter and clean guard-owned hazards.");
            yield return Until(() => !player.IsDead && !player.IsSpawning && !player.Spawner.IsRespawning, "actual Player automatic retry"); // 실제 사망·기상 모션과 같은 Player 자동 복구 완료 대기
            Require(manager.CurrentBoss == boss && !boss.IsDead && boss.HP == boss.maxHP && pattern.UseCount == 0
                && boss.PatternSelector.LastPatternId == null && boss.transform.position == boss.BasePosition && spawner.EncounterTrigger.enabled,
                "Automatic retry must restore the same guard's health, position, trigger and first-shot history.");
            player.IsInvulnerable = true;
            Passed("Groggy retains the fired gold shot; actual Player death removes owned hazards and automatically restores the same guard for retry");

            // 자동 복구 뒤 재발사와 실제 Boss 처치의 사체·문·처치 저장 경계 확인
            yield return Enter(stage, boss, body); // 자동 부활 뒤 실제 재조우와 패턴 재시작 대기
            yield return Until(() => Shots(boss).Length == 1, "post-death retry shot"); // 초기화된 같은 경비의 실제 재발사 대기
            Require(pattern.UseCount == 1 && boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Slash, DamageInstance.NextAttackId())),
                "The same guard must fire after retry and accept a real killing hit.");
            yield return Until(() => !stage.IsBossSequencePlaying && DoorOpen(entry) && DoorOpen(exit), "guard death and both doors opening"); // 처치 연출 및 입구·출구 개방 모션 완료 대기
            Require(boss.IsDead && !boss.IsInCombat && Shots(boss).Length == 0 && !boss.GetComponent<Collider2D>().enabled
                && SaveManager.Current.Get<List<string>>("DefeatedBosses").Contains(boss.BossId),
                "Guard death must record defeat, clean hazards, disable its body and open both doors.");
            stage.ResetEncounter();
            Require(boss.IsDead && !spawner.EncounterTrigger.enabled && !boss.GetComponent<Collider2D>().enabled,
                "Retry after recorded defeat must preserve the guard corpse.");
            Passed("After automatic retry the guard fires again; actual Boss death records defeat, clears hazards, opens both doors and preserves the corpse on reset");
        }
        finally
        {
            // 실패한 검사에서도 가상 입력과 자기 캡처 카메라만 정리
            slashAction.performed -= onFPerformed; // F 입력 경계 수집 종료
            if (!string.IsNullOrEmpty(previousScheme)) actions.SwitchCurrentControlScheme(previousScheme, previousDevices);
            else
            {
                actions.user.UnpairDevices();
                actions.user.ActivateControlScheme((string)null);
                foreach (InputDevice device in previousDevices) InputUser.PerformPairingWithDevice(device, actions.user);
            }
            actions.ActivateInput();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            if (captureAnchor != null) UnityEngine.Object.Destroy(captureAnchor.gameObject);
            if (GameCamera.Instance != null) GameCamera.Instance.Follow(player.transform, true);
            manager.SetGameplayPaused(false);
        }
        manager.ReturnToTitle();
        yield return Until(() => Title() != null && Title().IsReady && !manager.IsLoading, "Guard check Title cleanup"); // 실제 세션 해제와 Title 복귀 완료 대기
        Passed("The isolated guard verification session releases its combat objects and returns to Title");
    }

    /// <summary>실제 조우 Collider 밖에서 안으로 이동해 연출과 첫 패턴 시작 대기</summary>
    private static IEnumerator Enter(StageManager stage, Boss_01 boss, Rigidbody2D body, bool approach = true)
    {
        // 새 진입 사건이 발생하도록 현재 체크포인트에서 접촉 해제 후 영역 중앙으로 이동
        SetPosition(body, stage.Player.Spawner.SpawnPoint.position);
        yield return Frames(3); // 실제 물리 프레임에서 이전 조우 접촉 해제 대기
        SetPosition(body, boss.GetComponent<BossSpawner>().EncounterTrigger.bounds.center);
        yield return Until(() => boss.IsInCombat && !stage.IsBossSequencePlaying, "actual Guard trigger and completed presentation"); // 실제 Trigger·조우 연출 정상 종료 대기
        if (approach)
        {
            Approach(boss, body);
            yield return Until(() => boss.CurrentPattern != null, "approach activates retry pattern"); // 접근 뒤 실제 총구 거리 조건과 첫 패턴 선택 대기
        }
    }

    /// <summary>실제 Player 추적 화면에 총구가 보이는 활성 거리 안으로 접근</summary>
    private static void Approach(Boss_01 boss, Rigidbody2D body)
    {
        // 몸통 판정 아래에서 지정 총구와의 거리를 활성 반경보다 조금 짧게 배치
        SetPosition(body, new Vector2(boss.transform.position.x, boss.Muzzles[boss.MuzzleIndex].position.y - boss.ActivationRange + 0.375f));
        GameCamera.Instance.Follow(body.transform, true);
    }

    /// <summary>영역 밖에서 같은 Boss의 기존 재도전 복원 API 사용</summary>
    private static void ResetForRetry(StageManager stage, PlayerCharacter player, Rigidbody2D body)
    {
        // 전투 진입이 자동으로 반복되지 않도록 Player를 체크포인트에 먼저 배치
        SetPosition(body, player.Spawner.SpawnPoint.position);
        stage.ResetEncounter();
        player.ResetMotion();
    }

    /// <summary>현재 경비가 실제로 발사하고 아직 유지하는 황금 탄만 조회</summary>
    private static GoldProjectile[] Shots(Boss_01 boss)
    {
        // Editor 검사의 관찰 범위만 조회하며 런타임 패턴에는 검사 상태를 추가하지 않음
        var shots = new List<GoldProjectile>();
        foreach (GoldProjectile projectile in UnityEngine.Object.FindObjectsByType<GoldProjectile>(FindObjectsSortMode.None))
            if (projectile.Owner == boss && projectile.IsActive) shots.Add(projectile);
        return shots.ToArray();
    }

    /// <summary>실제 Unity 월드 카메라 결과를 후가공 없이 PNG로 저장</summary>
    private static void Capture(Camera camera, string name)
    {
        // 현재 렌더 대상을 보관하고 실제 1280×960 화면 읽기
        RenderTexture target = RenderTexture.GetTemporary(1280, 960, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D pixels = new Texture2D(1280, 960, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Logs/GuardBossVerification");
            File.WriteAllBytes(Path.Combine("Logs/GuardBossVerification", name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(pixels);
        }
    }

    /// <summary>문 실제 개방 모션과 통과 Collider 해제를 함께 조회</summary>
    private static bool DoorOpen(GameObject door)
    {
        MapDoor motion = door.GetComponent<MapDoor>();
        return motion != null ? motion.IsOpen && !motion.Blocker.enabled : !door.activeSelf;
    }

    /// <summary>물리와 Transform을 같은 위치로 즉시 동기화</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>비활성 객체를 포함해 현재 Title 조회</summary>
    private static UI_Title Title() => UnityEngine.Object.FindFirstObjectByType<UI_Title>(FindObjectsInactive.Include);

    /// <summary>조건이 실제 프레임에서 완료되는지 제한 시간 안에 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string reason)
    {
        double end = EditorApplication.timeSinceStartup + 30d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(reason);
            yield return null;
        }
    }

    /// <summary>실제 게임 시간 경과 대기</summary>
    private static IEnumerator WaitGame(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    /// <summary>게임 정지와 독립된 실제 Editor 시간 경과 대기</summary>
    private static IEnumerator WaitReal(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    /// <summary>입력과 물리 갱신에 필요한 실제 렌더 프레임 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 실제 Player·입력·Trigger 프레임 진행 대기
    }

    /// <summary>관찰한 조건 불성립만 실제 검사 실패로 기록</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>실제로 통과한 검사 설명을 결과에 추가</summary>
    private static void Passed(string message) => report.checks.Add(message);

    /// <summary>실제 결과를 저장하고 Play Mode 종료 후 배치 코드 전달</summary>
    private static void Finish(Exception exception)
    {
        // 결과 기록 이후에는 같은 검사가 계속 실행되지 않도록 콜백 해제
        EditorApplication.update -= Tick; // 완료·실패 뒤 검사 갱신 구독 해제
        report.success = exception == null;
        report.error = exception?.ToString() ?? "";
        report.saveDirectory = SessionState.GetString(SaveKey, "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GuardBossVerification-result.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(SuccessKey, report.success);
        SessionState.SetBool(FinishedKey, true);
        if (exception == null) Debug.Log("[GuardBossChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[GuardBossChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>실제 통과 항목·실패 예외와 캡처 프레임 시각을 저장할 결과</summary>
    [Serializable]
    private class Report
    {
        public bool success; // 모든 실제 검사가 통과했는지 결과
        public List<string> checks = new List<string>(); // 이번 실행에서 실제 통과한 항목
        public List<double> frameTimes = new List<double>(); // 연속 PNG를 캡처한 실제 게임 시각
        public string error; // 실패 시 실제 예외 전체 내용
        public string saveDirectory; // 사용자 저장과 분리한 검사 저장 위치
    }
}
