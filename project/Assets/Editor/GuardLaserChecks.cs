using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 경비 세션에서 과거 위치 조준·빔 기하·피해와 취소 수명 확인</summary>
[InitializeOnLoad]
public static class GuardLaserChecks
{
    private const string Key = "Chrono.GuardLaserChecks."; // 도메인 재로드를 건너 전달할 이번 검사 상태 접두사
    private static Stack<IEnumerator> routines; // 실제 프레임에서 중첩 대기를 진행할 검사 스택
    private static Report report; // 통과 항목과 조준 이력·촬영 결과
    private static double deadline; // 무한 대기 방지를 위한 실제 Editor 제한 시각

    static GuardLaserChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode; // 실제 Play 경계에서 요청한 검사만 시작·종료
    }

    /// <summary>사용자 저장과 분리한 실제 Title 세션으로 검사 시작</summary>
    [MenuItem("Tools/Chrono/Check Guard Eye Laser")]
    public static void RunBatch()
    {
        // 저장하지 않은 씬을 보존하고 현재 실사용 경비 프리팹부터 준비
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin laser checks outside Play Mode.");
        Scene scene = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (SceneManager.sceneCount != 1 || scene.isDirty || string.IsNullOrEmpty(scene.path)))
            throw new InvalidOperationException("Laser checks require one saved, unmodified scene.");
        SessionState.SetString(Key + "Scene", scene.path);
        GuardBossSetup.Run();
        string directory = Path.GetFullPath(Path.Combine("Temp", "GuardLaserChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        SessionState.SetString(Key + "Save", directory);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(manager != null, "The actual Title requires GameManager.");
        manager.SaveDirectory = directory;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetBool(Key + "Finished", false);
        SessionState.SetBool(Key + "Success", false);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // 일반 Play에는 검사 연결을 만들지 않음
        if (!SessionState.GetBool(Key + "Active", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(Check());
            deadline = EditorApplication.timeSinceStartup + 180d;
            EditorApplication.update += Tick; // 실제 프레임 사이에 순차 검사 진행
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "Finished", false))
        {
            SessionState.SetBool(Key + "Active", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Success", false) ? 0 : 1);
            else EditorSceneManager.OpenScene(SessionState.GetString(Key + "Scene", "Assets/Scenes/Title.unity"));
        }
    }

    private static void Tick()
    {
        try
        {
            // 실제 게임 프레임 대기와 중첩 코루틴을 같은 작은 실행기로 처리
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Guard laser checks exceeded 180 seconds.");
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

    private static IEnumerator Check()
    {
        // 실제 신규 저장과 입구 걷기 튜토리얼까지 완료한 뒤 전투 검사 시작
        yield return Until(() => GameManager.Instance != null && Title() != null && Title().IsReady, "laser Title ready"); // 실제 Title 준비 대기
        GameManager manager = GameManager.Instance;
        Title().SelectSlot(0);
        yield return Until(() => !manager.IsLoading && manager.Stage.IsReady && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsSpawning && !manager.CurrentPlayer.IsCutScene
            && !manager.Stage.Tutorial.IsPlaying && !manager.Stage.Tutorial.HasPendingEntrance, "laser new session entrance ready"); // 입구 걷기와 입력 반환 완료 대기
        StageManager stage = manager.Stage;
        Boss_01 boss = manager.CurrentBoss as Boss_01;
        PlayerCharacter player = manager.CurrentPlayer;
        Require(boss != null && boss.EyeLaser != null && boss.PatternSelector.Patterns.Count == 5, "The actual guard must register five patterns and its eye laser.");
        BossPattern[] production = new BossPattern[5];
        BossPattern pattern = null;
        var ids = new HashSet<string>();
        for (int i = 0; i < production.Length; i++)
        {
            production[i] = boss.PatternSelector.Patterns[i];
            ids.Add(production[i].PatternId);
            if (production[i].PatternId == "F-B07-04") pattern = production[i];
        }
        GuardEyeLaser laser = boss.EyeLaser;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerInputController input = PlayerInputController.Instance;
        bool inputEnabled = input.enabled;
        bool inputBlocked = input.BlockInput;
        int captureRate = Time.captureFramerate;
        Transform review = null;
        GameObject extraHitbox = null;
        DefaultExecutionOrder order = (DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(Boss_01), typeof(DefaultExecutionOrder));
        report.attributeOrder = order != null ? order.order : 0;
        report.importerOrder = MonoImporter.GetExecutionOrder(MonoScript.FromMonoBehaviour(boss));
        Require(pattern != null && ids.SetEquals(new[] { "F-B07-01", "F-B07-02", "F-B07-03", "F-B07-04", "F-B07-05" })
            && report.attributeOrder == 1 && (report.importerOrder == 0 || report.importerOrder == 1),
            "Production IDs and Player-before-Boss order mismatch: IDs=" + string.Join(",", ids)
            + "; attribute=" + report.attributeOrder + "; importer override=" + report.importerOrder);
        CheckReferences(laser);
        Passed("New Title save completes the entrance tutorial and creates all five guard patterns with native eye origins, shared beam materials and explicit Player-before-Boss sampling order");
        input.enabled = false;
        input.BlockInput = false;
        player.ResetMotion();
        player.Buffs.Clear();
        player.HitProtectionDuration = 0f;
        player.IsInvulnerable = true;
        try
        {
            // 입장 공통 F 검사는 기존 회귀를 사용하고 여기서는 실제 문·조우 Trigger부터 연결
            stage.Map.SetEntryDoorOpen(boss.BossId, true);
            yield return Until(() => stage.Map.GetEntryDoor(boss.BossId).GetComponent<MapDoor>().IsOpen, "laser entry opening"); // 실제 문 열림 완료 대기
            SetPosition(body, boss.GetComponent<BossSpawner>().EncounterTrigger.bounds.center);
            yield return Until(() => boss.IsInCombat && !stage.IsBossSequencePlaying, "laser actual encounter"); // 실제 조우 연출 완료 대기
            Time.captureFramerate = 24;

            // 불규칙 시간과 방향 반전의 독립 기대값으로 정확한 과거 보간 증명
            CheckHistory(stage, boss, laser, body);
            Passed("Independent irregular-delta samples and direction reversal match interpolated position at exactly t minus 0.4 game seconds, rather than smoothing toward the current target");

            // 실제 Update에서 이동 경로·조준값과 카메라 출력을 함께 수집
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 실제 선택기에서 눈 충전 시작 대기
            Vulnerable(player);
            var samples = new List<Sample> { new Sample(0f, body.position) };
            if (laser.HistoryTime > 0f) samples.Add(new Sample(laser.HistoryTime, body.position));
            float observed = laser.HistoryTime;
            Vector2 previousDirection = Vector2.down;
            float largestTurn = 0f;
            int captures = 0;
            int chargingSamples = 0;
            for (int i = 0; i < 84; i++)
            {
                float x = i < 42 ? Mathf.Lerp(-2.4f, 2.4f, i / 41f) : Mathf.Lerp(2.4f, -2.4f, (i - 42) / 41f);
                Vector2 position = Arena(boss) + Vector2.right * x;
                SetPosition(body, position);
                yield return Frames(1); // 같은 Player 위치를 실제 Boss.Update가 기록할 때까지 대기
                float now = laser.HistoryTime;
                Require(now > observed && laser.IsRunning, "The live laser must advance one or more fixed capture steps.");
                int steps = Mathf.Max(1, Mathf.RoundToInt((now - observed) * 24f));
                for (int step = 1; step <= steps; step++) samples.Add(new Sample(Mathf.Lerp(observed, now, step / (float)steps), position));
                observed = now;
                Vector2 expected = Interpolate(samples, Mathf.Max(0f, now - 0.4f));
                float error = Vector2.Distance(expected, laser.AimPosition);
                report.samples.Add(new Observation(now, body.position, laser.AimPosition, expected, error, laser.Phase.ToString()));
                Require(error < 0.015f && Mathf.Abs(laser.AimSampleTime - Mathf.Max(0f, now - 0.4f)) < 0.0001f,
                    "Actual Update aiming must match independent fixed-step delayed samples.");
                GameCamera.Instance.Follow(player.transform, true);
                Capture("sequence/frame-" + i.ToString("D3") + ".png");
                report.sequenceFrameTimes.Add(Time.timeAsDouble);
                if (laser.Phase == GuardEyeLaserPhase.Charging)
                {
                    chargingSamples++;
                    Require(player.HP == player.maxHP && HiddenBeams(laser) && laser.ChargeLights[0].enabled && laser.ChargeLights[1].enabled
                        && !laser.ImpactWarnings[0].gameObject.activeSelf && !laser.ImpactWarnings[1].gameObject.activeSelf,
                        "Charging must show both eye lights without airborne beams, ground impacts or damage.");
                    if (chargingSamples == 8) Capture("charging-player-camera.png");
                }
                else if (laser.Phase == GuardEyeLaserPhase.Firing)
                {
                    CheckGeometry(stage.Map, laser);
                    Vector2 direction = ((Vector2)laser.BeamBodies[0].GetPosition(1) - (Vector2)laser.BeamBodies[0].GetPosition(0)).normalized;
                    if (captures > 0) largestTurn = Mathf.Max(largestTurn, Vector2.Angle(previousDirection, direction));
                    previousDirection = direction;
                    GameCamera.Instance.Follow(player.transform, true);
                    if (captures < 40)
                    {
                        Capture("frames/frame-" + captures.ToString("D3") + ".png");
                        report.frameTimes.Add(Time.timeAsDouble);
                        captures++;
                    }
                }
            }
            Require(chargingSamples > 0 && captures == 40 && largestTurn > 0.1f && largestTurn < 12f, "Actual turning beams must produce forty Player-following frames without discontinuous direction jumps.");
            Passed("Actual Boss Update follows a moving and reversing Player at fixed 24 fps with measured 0.4-second lag; charging is harmless and forty continuous Player-camera beam frames are captured");
            review = new GameObject("GuardLaserInspectionAnchor").transform;
            review.position = boss.transform.position + Vector3.up * 0.5f;
            GameCamera.Instance.Follow(review, true);
            Capture("firing-full-boss-inspection.png");
            GameCamera.Instance.Follow(player.transform, true);
            UnityEngine.Object.Destroy(review.gameObject);
            review = null;

            // 두 착탄원과 중복 Player Collider가 겹쳐도 보호 시간 없이 한 번만 성공
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 공통 피해 검사에 새 발사 이력 준비
            Vulnerable(player);
            extraHitbox = new GameObject("LaserDuplicatePlayerHitbox", typeof(CircleCollider2D), typeof(Hitbox));
            extraHitbox.transform.SetParent(player.transform, false);
            extraHitbox.GetComponent<CircleCollider2D>().isTrigger = true;
            extraHitbox.GetComponent<CircleCollider2D>().radius = 0.2f;
            extraHitbox.GetComponent<Hitbox>().Owner = player;
            extraHitbox.GetComponent<Hitbox>().TargetKind = TargetKind.Player;
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser first damaging frame"); // 실제 충전 종료와 첫 범위 판정 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - laser.BeamDamage), "Both ground impact circles and duplicate hitboxes must cause exactly one common damage event.");
            yield return WaitGame(0.3f); // 성공 피해 뒤에도 연속 빔이 추가 피해를 주지 않는지 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - laser.BeamDamage), "Hit protection disabled must still preserve common once-per-pattern damage.");
            UnityEngine.Object.Destroy(extraHitbox);
            extraHitbox = null;
            Passed("Two ground impact circles and multiple Player hitboxes apply one configured damage event with hit protection disabled, and continuous overlap does not apply extra damage");

            // 과거 착탄원 밖으로 이동하면 피하고 종점이 따라온 뒤 실제 피격
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 폭과 과거 위치 회피 검사용 새 충전 시작
            Vulnerable(player);
            yield return Until(() => laser.PhaseElapsed >= laser.ChargeDuration - 0.13f, "laser last charge interval"); // 발사 직전까지 최초 목표 이력 유지
            SetPosition(body, Arena(boss) + Vector2.right * 2f);
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser delayed miss starts"); // 과거 원위치를 겨냥하는 첫 빔 대기
            yield return WaitGame(0.08f); // 현재 Player가 두 착탄원 밖에 있는 초기 발사 구간 확인
            Require(player.HP == player.maxHP && Vector2.Distance(laser.AimPosition, body.position) > 1f,
                "A vulnerable Player outside both delayed ground circles must avoid early damage.");
            yield return Until(() => player.HP < player.maxHP, "laser delayed target catches up"); // 과거 위치가 이동한 목표에 도달하는 실제 판정 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - laser.BeamDamage), "The delayed ground impact must damage the Player only when its circle reaches the target.");
            Passed("A vulnerable Player avoids both delayed ground impact circles by moving away, then receives one hit only after a displayed impact circle reaches that location");

            // 공중 선분을 지나가도 지면 착탄원 밖인 실제 이동 가능 위치는 무피해
            Vector2 ground = new Vector2(boss.transform.position.x, 18f);
            yield return StartLaser(stage, boss, pattern, body, ground); // 아래쪽 지면에 고정한 위치 이력으로 충전 시작
            Vulnerable(player);
            yield return Until(() => laser.PhaseElapsed >= laser.ChargeDuration - 0.08f, "laser middle crossing setup"); // 지면 이력이 유지되는 발사 직전까지 대기
            Vector2 middle = ((Vector2)laser.Eyes[0].position + ground - Vector2.right * (laser.BeamWidth * 0.5f)) * 0.5f;
            SetPosition(body, middle);
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser middle crossing firing"); // 공중 선분이 Player를 지나가는 첫 발사 대기
            CheckGeometry(stage.Map, laser);
            Hitbox playerHitbox = player.GetComponentInChildren<Hitbox>();
            Vector2 from = laser.Eyes[0].position;
            Vector2 end = laser.GetImpactPosition(0);
            Vector2 segment = end - from;
            float along = Vector2.Dot((Vector2)playerHitbox.Bounds.center - from, segment.normalized);
            Vector2 nearest = from + segment.normalized * along;
            Require(along > 0f && along < segment.magnitude && Vector2.Distance(nearest, playerHitbox.Bounds.center) < laser.BeamWidth * 0.5f
                && Vector2.Distance(body.position, end) > 3f && body.position.y < boss.transform.position.y - 0.5f,
                "The vulnerable Player must actually cross the airborne visual beam at a reachable point far from its ground endpoint.");
            yield return WaitGame(0.1f); // 과거 지면 종점이 아직 아래에 남아 있는 구간의 무피해 확인
            Require(player.HP == player.maxHP, "Airborne beam middle overlap must cause no damage outside both ground impact circles.");
            Capture("airborne-middle-no-damage-player-camera.png");
            Passed("A vulnerable Player actually crosses the airborne beam segment at a reachable room location without damage while both ground endpoint circles remain far below");

            // 정지 중 이력·단계·선 끝점을 보존하고 재개 뒤 같은 실행 계속
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 정지 검사용 실제 충전 시작
            player.IsInvulnerable = true;
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser pause firing"); // 표시 중인 빔 준비 완료 대기
            float history = laser.HistoryTime;
            float elapsed = laser.PhaseElapsed;
            Vector2 aim = laser.AimPosition;
            Vector3 endpoint = laser.BeamBodies[0].GetPosition(1);
            Vector3 impactPoint = laser.ImpactWarnings[0].transform.position;
            manager.SetGameplayPaused(true);
            SetPosition(body, Arena(boss) + Vector2.left * 2f);
            yield return WaitReal(0.15f); // 게임 정지와 독립인 실제 시간으로 동결 확인
            Require(laser.HistoryTime == history && laser.PhaseElapsed == elapsed && laser.AimPosition == aim
                && laser.BeamBodies[0].GetPosition(1) == endpoint && laser.ImpactWarnings[0].transform.position == impactPoint && laser.IsRunning,
                "Pause must freeze history, delayed aim, airborne beam, ground impact and firing time.");
            manager.SetGameplayPaused(false);
            yield return Frames(2); // 같은 실행의 실제 게임 시간 재개 대기
            Require(laser.HistoryTime > history && laser.PhaseElapsed > elapsed, "Resume must advance the same beam execution.");
            Passed("Gameplay pause freezes position history, phase time, airborne beams and ground impact positions, and resume continues the same firing execution");

            // 충전과 발사 각각의 그로기는 연속 빔만 닫고 이미 나온 탄을 유지
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 취소 전 기존 탄과 충전 준비
            NormalProjectile normal = UnityEngine.Object.Instantiate(boss.NormalPrefab, new Vector3(22f, 17f, 0f), Quaternion.identity, boss.transform.parent);
            normal.Initialize(boss, Vector2.up, 0f, 0f, 1f);
            boss.EnterGroggy();
            Require(Idle(laser) && normal != null && normal.IsActive, "Groggy during charge must close lights and preserve an existing normal bullet.");
            yield return WaitGame(0.5f); // 취소 뒤 미래 빔이 생기지 않는지 대기
            Require(Idle(laser), "Cancelled charging must not create a future beam.");
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 재도전 뒤 같은 눈의 실제 발사 준비
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser firing cancel"); // 발사 중 즉시 취소 경계 대기
            GoldProjectile gold = UnityEngine.Object.Instantiate(boss.GoldPrefab, new Vector3(22f, 17f, 0f), Quaternion.identity, boss.transform.parent);
            gold.Initialize(boss, Vector2.up, 0f, 6f, 1f);
            boss.EnterGroggy();
            Require(Idle(laser) && gold != null && gold.IsActive, "Groggy during firing must close every beam immediately while leaving an existing Gold active.");
            Passed("Groggy during both charging and firing closes all future beam damage and visual layers immediately while preserving already emitted Normal and Gold bullets");

            // 재도전은 같은 눈·선·광원을 사용하며 이전 조준 이력을 버림
            Transform leftEye = laser.Eyes[0];
            LineRenderer leftBody = laser.BeamBodies[0];
            SpriteRenderer leftLight = laser.ChargeLights[0];
            AreaWarningVfx leftImpact = laser.ImpactWarnings[0];
            SpriteRenderer leftImpactCore = laser.ImpactCores[0];
            yield return StartLaser(stage, boss, pattern, body, Arena(boss) + Vector2.right);
            Require(laser.Eyes[0] == leftEye && laser.BeamBodies[0] == leftBody && laser.ChargeLights[0] == leftLight
                && laser.ImpactWarnings[0] == leftImpact && laser.ImpactCores[0] == leftImpactCore
                && laser.HistoryTime < 0.2f && Vector2.Distance(laser.AimPosition, body.position) < 0.001f,
                "Retry must reuse prepared objects and begin with only the new target history.");
            stage.ResetEncounter();
            Require(Idle(laser), "Encounter reset must immediately hide and reset reused eye visuals.");
            Passed("Retry reuses the same eyes, beam renderers, charge sprites, impact warnings and hot cores, seeds fresh history and restores all visuals on encounter reset");

            // 공통 선택기는 레이저 뒤 같은 패턴을 재선택하지 않고 다른 생산 패턴으로 전환
            boss.PatternSelector.SetPatterns(production);
            foreach (BossPattern candidate in production) candidate.Priority = candidate == pattern ? 1 : 0;
            SetPosition(body, Arena(boss));
            boss.BeginEncounter(player);
            yield return Until(() => boss.CurrentPattern == pattern, "production laser selected"); // 실제 다섯 패턴 등록 중 레이저 선택 대기
            foreach (BossPattern candidate in production) candidate.Priority = candidate.PatternId == "F-B07-01" ? 1 : 0;
            yield return Until(() => !laser.IsRunning && boss.CurrentPattern != null && boss.CurrentPattern != pattern, "production pattern after laser"); // 실제 발사·회복 뒤 직전 제외 전환 대기
            Require(boss.PatternSelector.Patterns.Count == 5 && pattern.UseCount == 1 && boss.CurrentPattern.PatternId == "F-B07-01", "Five-pattern production selector must complete laser and exclude it from the next draw.");
            boss.EndEncounter();
            Passed("The real five-pattern selector runs the complete laser charge, three-second firing and recovery, then excludes it and starts the golden-shot pattern");

            // 사망은 현재 빔과 소유 탄을 닫고 처치된 보스의 재도전을 막음
            yield return StartLaser(stage, boss, pattern, body, Arena(boss)); // 실제 사망 취소 검사용 발사 준비
            yield return Until(() => laser.Phase == GuardEyeLaserPhase.Firing, "laser death firing"); // 발사 도중 사망 경계 대기
            Require(boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Slash, DamageInstance.NextAttackId())) && boss.IsDead && Idle(laser),
                "Boss death must immediately close active eye beams.");
            yield return Until(() => !stage.IsBossSequencePlaying && stage.Map.GetExitDoor(boss.BossId).GetComponent<MapDoor>().IsOpen, "laser boss death exit"); // 실제 처치 연출과 출구 개방 완료 대기
            stage.ResetEncounter();
            Require(boss.IsDead && Idle(laser), "Reset after recorded defeat must retain the dead boss and hidden laser.");
            Passed("Boss death cancels active beams immediately, opens the exit through the actual stage flow and keeps the defeated boss and laser idle on reset");
        }
        finally
        {
            // 실패 경계에서도 게임 시간·입력·카메라와 임시 검사 객체 복원
            Time.captureFramerate = captureRate;
            manager.SetGameplayPaused(false);
            input.enabled = inputEnabled;
            input.BlockInput = inputBlocked;
            if (extraHitbox != null) UnityEngine.Object.Destroy(extraHitbox);
            if (GameCamera.Instance != null && player != null) GameCamera.Instance.Follow(player.transform, true);
            if (review != null) UnityEngine.Object.Destroy(review.gameObject);
            if (boss != null) boss.CancelPattern();
        }
        manager.ReturnToTitle();
        yield return Until(() => Title() != null && Title().IsReady && !manager.IsLoading, "laser Title cleanup"); // 실제 세션 제거와 Title 복귀 완료 대기
        Require(UnityEngine.Object.FindObjectsByType<GuardEyeLaser>(FindObjectsSortMode.None).Length == 0, "Title return must remove the session-owned eye controller and visuals.");
        Passed("Return to Title removes the session-owned eye controller and all of its prepared visuals");
    }

    /// <summary>동일 Editor 콜백에서 불규칙 시간 이력을 독립 계산해 정확한 보간 확인</summary>
    private static void CheckHistory(StageManager stage, Boss_01 boss, GuardEyeLaser laser, Rigidbody2D body)
    {
        // 실제 소유자의 준비 조건을 유지하되 자동 패턴 선택은 이 경계 검사 동안 비움
        stage.ResetEncounter();
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        boss.PatternSelector.SetPatterns(Array.Empty<BossPattern>());
        SetPosition(body, Arena(boss));
        boss.BeginEncounter(stage.Player);
        Require(laser.Begin(stage.Player), "A prepared actual guard must begin its eye controller.");
        var samples = new List<Sample> { new Sample(0f, body.position) };
        float[] deltas = { 0.07f, 0.12f, 0.04f, 0.18f, 0.03f, 0.11f, 0.06f, 0.14f, 0.09f, 0.13f, 0.08f, 0.17f };
        float time = 0f;
        bool interpolated = false;
        for (int i = 0; i < deltas.Length; i++)
        {
            time += deltas[i];
            Vector2 position = Arena(boss) + new Vector2(i < 5 ? i * 0.25f : 1f - (i - 4) * 0.35f, i * 0.04f);
            SetPosition(body, position);
            samples.Add(new Sample(time, position));
            laser.Tick(deltas[i]);
            float sampleTime = Mathf.Max(0f, time - 0.4f);
            Vector2 expected = Interpolate(samples, sampleTime);
            Require(Vector2.Distance(laser.AimPosition, expected) < 0.0001f && Mathf.Abs(laser.HistoryTime - time) < 0.0001f,
                "Irregular Tick must interpolate the exact past sample time, including after a direction reversal.");
            if (sampleTime > 0f && Vector2.Distance(expected, position) > 0.1f) interpolated = true;
            report.irregularSamples.Add(new Observation(time, position, laser.AimPosition, expected, Vector2.Distance(expected, laser.AimPosition), laser.Phase.ToString()));
        }
        Require(interpolated && laser.DelaySeconds == 0.4f, "Irregular samples must exercise a delayed non-current position.");
        laser.Cancel();
        boss.EndEncounter();
    }

    private static IEnumerator StartLaser(StageManager stage, Boss_01 boss, BossPattern pattern, Rigidbody2D body, Vector2 position)
    {
        // 기존 세션 재도전과 공통 선택 이력 초기화로 같은 표시 객체 재사용
        stage.ResetEncounter();
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        boss.PatternSelector.SetPatterns(pattern);
        stage.Player.ResetMotion();
        SetPosition(body, position);
        GameCamera.Instance.Follow(stage.Player.transform, true);
        boss.BeginEncounter(stage.Player);
        yield return Until(() => pattern.IsRunning && boss.EyeLaser.Phase == GuardEyeLaserPhase.Charging, "isolated eye pattern starts"); // 실제 Boss.Update의 선택과 충전 시작 대기
    }

    private static void CheckReferences(GuardEyeLaser laser)
    {
        // 눈의 원화 좌표와 공유 표시 재질·폭 기본값만 확인
        Require(laser.Eyes.Length == 2 && laser.Eyes[0].localPosition == new Vector3(-0.5625f, 3.9375f, 0f)
            && laser.Eyes[1].localPosition == new Vector3(0.4375f, 3.9375f, 0f)
            && laser.ChargeDuration == 1.2f && laser.FiringDuration == 3f && laser.RecoveryDuration == 0.5f
            && laser.BeamWidth == 0.5f && laser.MaxLength == 40f && laser.BeamDamage == 10f && laser.ImpactRadius == 0.35f
            && laser.ImpactWarnings.Length == 2 && laser.ImpactCores.Length == 2 && Idle(laser), "Eye origins and production settings must match authored native pixels and two prepared ground impacts.");
        for (int i = 0; i < 2; i++)
        {
            Require(laser.Eyes[i].GetComponentsInChildren<Collider2D>(true).Length == 0
                && laser.BeamCores[i].sharedMaterial == laser.BeamBodies[i].sharedMaterial
                && laser.BeamGlows[i].sharedMaterial == laser.BeamGlows[0].sharedMaterial
                && laser.BeamCores[i].startColor.r > 0.95f && laser.BeamBodies[i].startColor.g > 0.6f
                && laser.BeamGlows[i].sharedMaterial.mainTexture.filterMode == FilterMode.Bilinear
                && laser.ImpactWarnings[i].Shape == WarningShape.Circle
                && laser.ImpactWarnings[i].CircleMesh == laser.ImpactWarnings[0].CircleMesh
                && laser.ImpactWarnings[i].GetComponentsInChildren<Collider2D>(true).Length == 0
                && laser.ImpactCores[i].transform.parent == laser.ImpactWarnings[i].transform
                && laser.ImpactCores[i].sharedMaterial == laser.BeamCores[i].sharedMaterial,
                "Eyes must reuse three shared unlit layers and a soft alpha glow without extra collision geometry.");
        }
    }

    private static void CheckGeometry(MapManager map, GuardEyeLaser laser)
    {
        // 과거 지면의 좌우 종점까지만 길이를 구하고 실제 표시·원 판정을 대조
        for (int i = 0; i < 2; i++)
        {
            Vector2 origin = laser.Eyes[i].position;
            Vector2 desired = laser.AimPosition + Vector2.right * (laser.BeamWidth * (i == 0 ? -0.5f : 0.5f));
            Vector2 direction = (desired - origin).normalized;
            float expectedLength = Mathf.Min(laser.MaxLength, Vector2.Distance(origin, desired));
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(origin, laser.ImpactRadius, direction, expectedLength))
                if (map.IsWall(hit.collider) && !hit.collider.OverlapPoint(origin)) expectedLength = Mathf.Min(expectedLength, hit.distance);
            LineRenderer core = laser.BeamCores[i];
            LineRenderer beam = laser.BeamBodies[i];
            LineRenderer glow = laser.BeamGlows[i];
            AreaWarningVfx impact = laser.ImpactWarnings[i];
            LineRenderer circle = impact.GetComponent<LineRenderer>();
            Vector2 point = laser.GetImpactPosition(i);
            float shownRadius = Vector2.Distance(impact.transform.TransformPoint(circle.GetPosition(0)), point);
            Require(core.enabled && beam.enabled && glow.enabled && !laser.ChargeLights[i].enabled
                && core.numCapVertices >= 8 && beam.numCapVertices >= 8 && glow.numCapVertices >= 8
                && Mathf.Abs(core.startWidth - laser.BeamWidth * 0.3f) < 0.0001f
                && Mathf.Abs(beam.startWidth - laser.BeamWidth) < 0.0001f && Mathf.Abs(glow.startWidth - laser.BeamWidth * 2f) < 0.0001f
                && Vector2.Distance(beam.GetPosition(0), origin) < 0.0001f
                && Vector2.Distance(beam.GetPosition(1), origin + direction * expectedLength) < 0.002f
                && Mathf.Abs(laser.GetBeamLength(i) - expectedLength) < 0.002f
                && core.GetPosition(1) == beam.GetPosition(1) && glow.GetPosition(1) == beam.GetPosition(1)
                && Vector2.Distance(beam.GetPosition(1), point) < 0.0001f && Vector2.Distance(impact.transform.position, point) < 0.0001f
                && impact.gameObject.activeSelf && laser.ImpactCores[i].enabled && impact.FillRenderer.enabled
                && Vector2.Distance(laser.ImpactCores[i].transform.position, point) < 0.0001f
                && Mathf.Abs(shownRadius - laser.ImpactRadius) < 0.001f && impact.FillRenderer.transform.localScale == Vector3.one,
                "Each airborne beam must stop at its delayed finite ground endpoint, with every beam layer, impact circle and hot core sharing exactly that position and the damage radius.");
        }
    }

    private static Vector2 Interpolate(List<Sample> samples, float time)
    {
        // 기대값은 런타임 원형 버퍼를 읽지 않고 검사 경로에서 독립 계산
        for (int i = 1; i < samples.Count; i++)
            if (time <= samples[i].time) return Vector2.Lerp(samples[i - 1].position, samples[i].position, Mathf.InverseLerp(samples[i - 1].time, samples[i].time, time));
        return samples[samples.Count - 1].position;
    }

    private static bool HiddenBeams(GuardEyeLaser laser)
    {
        for (int i = 0; i < 2; i++) if (laser.BeamCores[i].enabled || laser.BeamBodies[i].enabled || laser.BeamGlows[i].enabled) return false;
        return true;
    }

    private static bool Idle(GuardEyeLaser laser) => !laser.IsRunning && laser.Phase == GuardEyeLaserPhase.Idle && HiddenBeams(laser)
        && !laser.ChargeLights[0].enabled && !laser.ChargeLights[1].enabled
        && !laser.ImpactWarnings[0].gameObject.activeSelf && !laser.ImpactWarnings[1].gameObject.activeSelf
        && !laser.ImpactCores[0].enabled && !laser.ImpactCores[1].enabled;

    private static void Vulnerable(PlayerCharacter player)
    {
        player.IsInvulnerable = false;
        player.HP = player.maxHP;
        player.ResetHitProtection();
    }

    private static Vector2 Arena(Boss_01 boss) => new Vector2(boss.transform.position.x, boss.transform.position.y - 0.6f);

    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    private static void Capture(string name)
    {
        // 실제 GameCamera의 현재 출력만 저장하고 원래 렌더 대상 복원
        Camera camera = GameCamera.Instance.Camera;
        RenderTexture target = RenderTexture.GetTemporary(960, 720, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        var pixels = new Texture2D(960, 720, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            pixels.Apply();
            string path = Path.Combine("Logs/GuardLaserVerification", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(pixels);
        }
    }

    private static UI_Title Title() => UnityEngine.Object.FindFirstObjectByType<UI_Title>(FindObjectsInactive.Include);

    private static IEnumerator Until(Func<bool> condition, string reason)
    {
        double end = EditorApplication.timeSinceStartup + 30d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(reason);
            yield return null;
        }
    }

    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "laser actual render frames"); // 실제 Boss 갱신과 렌더 완료 대기
    }

    private static IEnumerator WaitGame(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    private static IEnumerator WaitReal(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Passed(string message) => report.checks.Add(message);

    private static void Finish(Exception exception)
    {
        // 실패 시 iterator finally도 실행해 입력·게임 시간과 임시 카메라 추적 복원
        EditorApplication.update -= Tick; // 종료 뒤 검사 갱신 연결 해제
        while (routines != null && routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        report.success = exception == null;
        report.error = exception?.ToString() ?? "";
        report.saveDirectory = SessionState.GetString(Key + "Save", "");
        report.captureNote = "sequence/*.png: charge-to-firing sequence; frames/*.png: forty firing frames; both use actual Player-following GameCamera at 960x720 and 24 fps. firing-full-boss-inspection.png uses the same camera temporarily following a named inspection anchor; gameplay camera behavior is unchanged";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GuardLaserVerification-result.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(Key + "Success", report.success);
        SessionState.SetBool(Key + "Finished", true);
        if (exception == null) Debug.Log("[GuardLaserChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[GuardLaserChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    private class Sample
    {
        public float time; // 검사 경로에서 독립 기록한 게임 시각
        public Vector2 position; // 해당 게임 시각의 실제 목표 위치
        public Sample(float at, Vector2 point) { time = at; position = point; }
    }

    [Serializable]
    private class Observation
    {
        public float historyTime; // 현재 실행의 누적 게임 시간
        public Vector2 target; // 같은 프레임의 실제 목표 위치
        public Vector2 aim; // 런타임이 실제 선택한 과거 조준 위치
        public Vector2 expected; // 독립 경로 보간으로 구한 기대 위치
        public float error; // 실제 조준과 독립 기대값의 월드 거리
        public string phase; // 해당 표본의 실제 충전·발사 단계
        public Observation(float time, Vector2 current, Vector2 actual, Vector2 wanted, float distance, string state)
        { historyTime = time; target = current; aim = actual; expected = wanted; error = distance; phase = state; }
    }

    [Serializable]
    private class Report
    {
        public bool success; // 실제 전체 검사 통과 여부
        public List<string> checks = new List<string>(); // 실제 통과한 검사 묶음 설명
        public List<Observation> samples = new List<Observation>(); // 실제 Update의 이동·조준·기대값 표본
        public List<Observation> irregularSamples = new List<Observation>(); // 불규칙 Tick의 독립 과거 보간 표본
        public List<double> frameTimes = new List<double>(); // Player 카메라 연속 PNG의 실제 게임 시각
        public List<double> sequenceFrameTimes = new List<double>(); // 충전부터 발사까지 전체 연속 PNG의 실제 게임 시각
        public string captureNote; // 실제 플레이 시점과 전체 보스 검토 시점의 구분
        public int attributeOrder; // 코드 속성에 지정한 보스의 기본 실행 순서
        public int importerOrder; // 기본 속성과 별도로 저장한 Editor 실행 순서 재정의값
        public string saveDirectory; // 실제 사용자 저장과 분리한 이번 검사 저장 위치
        public string error; // 실패 시 실제 예외 내용
    }
}
