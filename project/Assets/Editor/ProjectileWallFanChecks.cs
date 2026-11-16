using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 맵의 탄환 벽 충돌과 다섯 총구 부채꼴 발사를 격리된 세션에서 확인</summary>
[InitializeOnLoad]
public static class ProjectileWallFanChecks
{
    private const string ActiveKey = "Chrono.ProjectileWallFan.Active"; // 도메인 재로드 뒤 검사 요청 복원 키
    private const string FinishedKey = "Chrono.ProjectileWallFan.Finished"; // 편집 모드 복귀의 종료 분기 키
    private const string SuccessKey = "Chrono.ProjectileWallFan.Success"; // 배치 종료 코드에 사용할 결과 키
    private const string SceneKey = "Chrono.ProjectileWallFan.Scene"; // 원래 편집 씬으로 복귀할 경로 키
    private const string SaveKey = "Chrono.ProjectileWallFan.Save"; // 사용자 저장과 분리한 검사 저장 경로 키
    private static Stack<IEnumerator> routines; // 중첩 검사·대기를 순서대로 진행할 스택
    private static Report report; // 실제 통과 항목·실패·발사 시각을 저장할 결과
    private static double deadline; // 전체 검사 무한 대기 방지 시각
    private static bool recording; // 대표 부채꼴의 실제 GameCamera 연속 촬영 상태
    private static int lastRecordedFrame = -1; // 같은 프레임과 연속 프레임 사이의 중복 촬영 방지

    /// <summary>요청한 Play Mode 검사만 재로드 뒤 연결</summary>
    static ProjectileWallFanChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode; // 실제 Play 진입과 종료에 맞춰 검사 실행·복귀
    }

    /// <summary>현재 프리팹과 실제 Title을 사용해 격리된 새 저장에서 검사 시작</summary>
    [MenuItem("Tools/Chrono/Check Projectile Walls And Guard Fan")]
    public static void RunBatch()
    {
        // 저장하지 않은 사용자 씬을 검사 시작으로 덮어쓰지 않도록 제한
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin wall/fan checks outside Play Mode.");
        Scene original = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (SceneManager.sceneCount != 1 || original.isDirty || string.IsNullOrEmpty(original.path)))
            throw new InvalidOperationException("Wall/fan checks require one saved, unmodified scene.");
        SessionState.SetString(SceneKey, original.path);
        GuardBossSetup.Run();
        string directory = Path.GetFullPath(Path.Combine("Temp", "ProjectileWallFanChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        SessionState.SetString(SaveKey, directory);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (manager == null) throw new InvalidOperationException("Actual Title requires GameManager.");
        manager.SaveDirectory = directory;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>이번 Play 진입에서 검사를 실행하고 종료 뒤 원래 씬 또는 배치 코드 전달</summary>
    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // 요청 없는 일반 Play에는 개입하지 않음
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(Check()); // 실제 세션·벽·부채꼴 검사 순차 실행
            deadline = EditorApplication.timeSinceStartup + 180d;
            EditorApplication.update += Tick; // 실제 프레임 사이에서 검사와 제한 시간 진행
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
            else EditorSceneManager.OpenScene(SessionState.GetString(SceneKey, "Assets/Scenes/Title.unity"));
        }
    }

    /// <summary>중첩 대기를 실제 프레임으로 진행하고 대표 부채꼴만 촬영</summary>
    private static void Tick()
    {
        try
        {
            // 20fps 게임 진행 중 두 프레임마다 실제 카메라를 기록
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Wall/fan checks exceeded 180 seconds.");
            if (recording && (lastRecordedFrame < 0 || Time.frameCount - lastRecordedFrame >= 2))
            {
                lastRecordedFrame = Time.frameCount;
                Capture("fan/frame-" + report.fanFrameTimes.Count.ToString("D3") + ".png");
                report.fanFrameTimes.Add(Time.timeAsDouble);
            }
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

    /// <summary>실제 Map 벽·문과 기존 피해·반사·05 발사 순서를 확인</summary>
    private static IEnumerator Check()
    {
        // 실제 세션이 프리팹과 맵 벽 캐시를 준비할 때까지 대기
        yield return Until(() => GameManager.Instance != null && Title() != null && Title().IsReady, "wall/fan Title readiness"); // 공유 저장과 Title 준비 완료 대기
        GameManager manager = GameManager.Instance;
        Title().SelectSlot(0);
        yield return Until(() => !manager.IsLoading && manager.Stage.IsReady && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsSpawning, "wall/fan session readiness"); // 실제 Map·Boss·Player 생성과 기상 완료 대기
        StageManager stage = manager.Stage;
        MapManager map = stage.Map;
        Boss_01 boss = manager.CurrentBoss as Boss_01;
        PlayerCharacter player = manager.CurrentPlayer;
        Require(boss != null && boss.PatternSelector.Patterns.Count == 5, "Guard must register patterns 01/02/03/04/05.");
        var production = new BossPattern[5];
        BossPattern fan = null;
        var ids = new HashSet<string>();
        for (int i = 0; i < production.Length; i++)
        {
            production[i] = boss.PatternSelector.Patterns[i];
            ids.Add(production[i].PatternId);
            if (production[i].PatternId == "F-B07-05") fan = production[i];
        }
        Require(fan != null && ids.SetEquals(new[] { "F-B07-01", "F-B07-02", "F-B07-03", "F-B07-04", "F-B07-05" })
            && boss.FanBulletCount == 5 && boss.FanBurstInterval == 0.3f && boss.FanBulletLifetime == 0f && boss.CentralBulletLifetime == 0f,
            "Production guard must retain its four patterns and lifetime-free central/fan bullet settings.");
        Transform walls = map.transform.Find("WorldCollision");
        Require(walls != null && walls.GetComponentsInChildren<Collider2D>().Length > 0
            && map.IsWall(walls.GetComponentInChildren<Collider2D>()) && !map.IsWall(boss.GetComponent<Collider2D>())
            && !map.IsWall(player.GetComponent<Collider2D>()), "Map walls must be cached separately from Owner and Player bodies.");
        Passed("Actual Title/Stage prepares the real wall set, five guard patterns and lifetime-free central/fan bullets while excluding character bodies from walls");
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerInputController.Instance.enabled = false;
        PlayerInputController.Instance.BlockInput = false;
        player.ResetMotion();
        player.HitProtectionDuration = 0f;
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        boss.PatternSelector.SetPatterns();
        int previousCaptureRate = Time.captureFramerate;
        Vector3 bossPosition = boss.transform.position;
        try
        {
            Time.captureFramerate = 20;
            manager.SetGameplayPaused(false);
            player.IsInvulnerable = false;
            SetPosition(body, player.Spawner.SpawnPoint.position);

            // 실제 경비방의 좌·우·상·하 경계는 고속 이동도 즉시 막음
            var cardinal = new List<NormalProjectile>();
            foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                cardinal.Add(Spawn(boss, new Vector2(10f, 17f), direction, 600f, 0f, 1f));
            yield return Frames(2); // 한 프레임보다 긴 전체 방 길이 Cast와 종료 완료 대기
            Require(cardinal.TrueForAll(projectile => projectile == null || !projectile.IsActive), "Actual cardinal room walls must stop all high-speed normal shots.");
            NormalProjectile throughOwner = Spawn(boss, boss.Muzzles[2].position, Vector2.down, 6f, 0f, 1f);
            yield return WaitGame(0.1f); // 발사자 몸체 내부에서 시작한 탄의 실제 비행 대기
            Require(throughOwner != null && throughOwner.IsActive && throughOwner.transform.position.y < boss.Muzzles[2].position.y - 0.3f,
                "The firing Owner's solid body must not be misclassified as a projectile wall.");
            throughOwner.Despawn();
            Passed("Real room walls stop cardinal high-speed flights while a projectile can leave its Owner's solid torso");

            // 닫힌 문 양끝은 막고 완전히 열린 같은 문은 통과
            SetPosition(body, new Vector2(10f, 17f));
            MapDoor door = map.GetEntryDoor(boss.BossId).GetComponent<MapDoor>();
            BoxCollider2D doorShape = (BoxCollider2D)door.Blocker;
            Vector2 doorCenter = door.transform.TransformPoint(doorShape.offset);
            Require(map.IsWall(door.Blocker), "The existing door blocker must belong to the cached wall set.");
            door.SetOpen(false, true);
            NormalProjectile leftEnd = Spawn(boss, doorCenter + new Vector2(-2f, 2f), Vector2.down, 60f, 0f, 1f);
            NormalProjectile rightEnd = Spawn(boss, doorCenter + new Vector2(2f, 2f), Vector2.down, 60f, 0f, 1f);
            yield return Frames(2); // 실제 닫힌 문의 양쪽 끝 접촉과 종료 대기
            Require((leftEnd == null || !leftEnd.IsActive) && (rightEnd == null || !rightEnd.IsActive), "Both closed door ends must stop projectiles.");
            door.SetOpen(true, true);
            NormalProjectile passage = Spawn(boss, doorCenter + Vector2.up * 2f, Vector2.down, 8f, 0f, 1f);
            yield return Until(() => passage == null || !passage.IsActive || passage.transform.position.y < doorCenter.y - 1f, "open door passage"); // 실제 열린 문 아래까지 비행 대기
            Require(passage != null && passage.IsActive && passage.transform.position.y < doorCenter.y - 1f,
                "Disabling the open door blocker must permit flight through its former closed shape.");
            passage.Despawn();
            Passed("The cached closed door blocks shots at both ends and the same fully opened door permits flight");

            // 같은 고속 Cast에서 벽 앞 Player만 피해를 받고 벽 뒤 Player는 보호
            player.HP = player.maxHP;
            player.ResetHitProtection();
            SetPosition(body, new Vector2(4f, 17f));
            NormalProjectile inFront = Spawn(boss, new Vector2(8f, 17f), Vector2.left, 1000f, 0f, 11f);
            yield return Frames(2); // 벽보다 가까운 실제 Player 적중 대기
            Require((inFront == null || !inFront.IsActive) && Mathf.Approximately(player.HP, player.maxHP - 11f),
                "A Player before the closest wall must receive exactly the configured hit.");
            player.HP = player.maxHP;
            player.ResetHitProtection();
            SetPosition(body, new Vector2(1f, 17f));
            NormalProjectile behind = Spawn(boss, new Vector2(8f, 17f), Vector2.left, 1000f, 0f, 11f);
            yield return Frames(2); // 실제 왼쪽 벽과 뒤쪽 Player가 같은 Cast에 수집되는 경계 대기
            Require((behind == null || !behind.IsActive) && player.HP == player.maxHP, "The nearest wall must block damage to a Player behind it.");
            SetPosition(body, new Vector2(31f, 17f));
            NormalProjectile tunnel = Spawn(boss, new Vector2(14.5f, 17f), Vector2.right, 1000f, 0f, 11f);
            yield return Frames(2); // 한 Update 변위가 벽 두께와 방 폭을 넘는 고속 경로 대기
            Require((tunnel == null || !tunnel.IsActive) && player.HP == player.maxHP,
                "A one-frame flight beyond the wall must not tunnel through it and hit the far Player.");
            Passed("Actual nearest-wall ordering damages a Player in front, protects a Player behind and prevents high-speed wall tunnelling");

            // 시간 만료 없는 탄은 5초 뒤에도 유지되고 정지 뒤 재개하여 벽에서 종료
            NormalProjectile unlimited = Spawn(boss, new Vector2(14.5f, 17f), Vector2.right, 0.05f, 0f, 1f);
            yield return WaitGame(5.2f); // 이전 5초 수명을 넘는 실제 게임 시간 대기
            Require(unlimited != null && unlimited.IsActive && unlimited.Lifetime == 0f && unlimited.RemainingLifetime == 0f,
                "Lifetime zero must keep a projectile alive beyond the previous five-second limit.");
            Vector3 pausedPosition = unlimited.transform.position;
            manager.SetGameplayPaused(true);
            yield return WaitReal(0.15f); // 시간 정지 중 실제 시간 경과 대기
            Require(unlimited.IsActive && unlimited.transform.position == pausedPosition && unlimited.RemainingLifetime == 0f,
                "Pause must freeze unlimited-life flight without turning zero into an expiry.");
            manager.SetGameplayPaused(false);
            unlimited.Speed = 600f;
            yield return Frames(2); // 재개한 무제한 탄이 실제 벽에서 종료될 때까지 대기
            Require(unlimited == null || !unlimited.IsActive, "Unlimited-life flight must still stop on wall collision.");
            NormalProjectile finite = Spawn(boss, new Vector2(400f, 400f), Vector2.right, 1f, 0.2f, 1f);
            yield return WaitGame(0.05f); // 맵 밖 기존 양수 수명의 실제 비행 진행 대기
            Require(finite != null && finite.IsActive && finite.transform.position.x > 400f, "Positive-lifetime fixtures must retain their existing flight.");
            yield return WaitGame(0.25f); // 같은 양수 수명의 정상 시간 만료 대기
            Require(finite == null || !finite.IsActive, "Positive lifetime must still expire normally outside the map.");
            Passed("Zero lifetime survives beyond five seconds, pauses and resumes until a wall hit; existing positive-lifetime flight and expiry remain intact");

            // 일반 Slash와 무제한 Golden 반사·각성은 기존 일회 계약 유지
            SetPosition(body, new Vector2(10f, 17f));
            player.Buffs.Clear();
            player.Groggy = 0f;
            player.HP = player.maxHP - 20f;
            yield return Frames(1); // 같은 프레임 효과 제거 우선 경계를 지난 반사 보상 허용 대기
            DamageInstance slash = new DamageInstance(player, 10f, DamageType.Slash, DamageInstance.NextAttackId());
            NormalProjectile sliced = Spawn(boss, new Vector2(14.5f, 17f), Vector2.down, 0f, 0f, 1f);
            Require(sliced.OnSlashed(player, boss, slash, Vector2.right) && !sliced.IsActive && !sliced.OnSlashed(player, boss, slash, Vector2.right)
                && player.Groggy == sliced.GroggyGain, "Unlimited normal shots must retain one Slash removal and one groggy reward.");
            GoldProjectile reflected = UnityEngine.Object.Instantiate(boss.GoldPrefab, new Vector3(14.5f, 17f, 0f), Quaternion.identity, boss.transform.parent);
            reflected.Initialize(boss, Vector2.down, 0f, 0f, 8f);
            float ownerHP = boss.HP;
            Vector2 toBoss = ((Vector2)boss.GetComponentInChildren<Hitbox>().Bounds.center - (Vector2)reflected.transform.position).normalized;
            Vector2 lineDirection = (reflected.Direction.normalized + toBoss).normalized;
            if (lineDirection.sqrMagnitude < 0.0001f) lineDirection = new Vector2(-reflected.Direction.y, reflected.Direction.x);
            Require(reflected.OnSlashed(player, boss, slash, lineDirection) && reflected.IsReflected && reflected.RemainingLifetime == 0f
                && Vector2.Dot(reflected.Direction, toBoss) > 0.9999f
                && !reflected.OnSlashed(player, boss, slash, lineDirection), "Unlimited Golden reflection must follow the fixture Line, retain zero remaining time and reject duplicate Slash.");
            yield return Until(() => player.Buffs.IsFever && (reflected == null || !reflected.IsActive), "unlimited gold Owner impact"); // 실제 각성과 벽 없는 Owner 도달 피해 대기
            Require(Mathf.Approximately(boss.HP, ownerHP - 8f)
                && Mathf.Approximately(player.HP, player.maxHP - 20f + player.Buffs.HealPerAcquisition),
                "Golden reflection must still grant Fever/heal once and damage its closer Owner.");
            boss.transform.position = new Vector3(30f, 17f, 0f);
            Physics2D.SyncTransforms();
            GoldProjectile blockedGold = UnityEngine.Object.Instantiate(boss.GoldPrefab, new Vector3(14.5f, 17f, 0f), Quaternion.identity, boss.transform.parent);
            blockedGold.Initialize(boss, Vector2.right, 0f, 0f, 8f);
            ownerHP = boss.HP;
            Require(blockedGold.OnSlashed(player, boss, slash, Vector2.right) && Vector2.Dot(blockedGold.Direction, Vector2.right) > 0.9999f,
                "A parallel Line must preserve rightward reflection toward the nearer wall even with an Owner behind it.");
            yield return Until(() => blockedGold == null || !blockedGold.IsActive, "reflected gold wall stop"); // 실제 오른쪽 벽이 Owner보다 먼저 반사탄을 차단하는 경계 대기
            Require(boss.HP == ownerHP, "A reflected Golden must stop on the nearer wall without damaging its Owner behind it.");
            boss.transform.position = bossPosition;
            Physics2D.SyncTransforms();
            Passed("Normal Slash and unlimited Golden reflection retain one-time rewards/Fever/Owner damage, while a nearer wall stops the reflected Golden");

            // 단독 05를 실제 Boss Update로 실행하며 모든 생성 ID와 묶음 시각 관찰
            StartFan(stage, boss, fan, body);
            yield return Until(() => fan.IsRunning, "first fan pattern start"); // 실제 공통 선택기와 05 Begin 완료 대기
            recording = true;
            lastRecordedFrame = -1;
            var seen = new HashSet<int>();
            bool pauseChecked = false;
            double fanDeadline = EditorApplication.timeSinceStartup + 60d;
            while (fan.IsRunning || seen.Count < 50)
            {
                Require(EditorApplication.timeSinceStartup < fanDeadline, "Fan sequence did not complete all ten observed bursts.");
                var fresh = new List<NormalProjectile>();
                foreach (NormalProjectile projectile in Normals(boss))
                    if (seen.Add(projectile.GetInstanceID())) fresh.Add(projectile);
                if (fresh.Count > 0)
                {
                    int burst = report.burstTimes.Count;
                    Require(burst < 10, "The fan pattern must not generate more than ten bursts.");
                    CheckBurst(boss, fresh, burst);
                    float elapsed = fan.Elapsed;
                    if (burst == 0) Require(elapsed >= boss.FanWarmup - 0.001f && elapsed <= boss.FanWarmup + 0.051f, "First fan burst must follow its configured warmup.");
                    else
                    {
                        double gap = elapsed - report.burstTimes[burst - 1];
                        Require(gap >= boss.FanBurstInterval - 0.001f && gap <= boss.FanBurstInterval + 0.001f,
                            "At the fixed capture step each fan burst must follow the previous one by 0.3 game seconds: " + gap);
                    }
                    report.burstTimes.Add(elapsed);
                    report.muzzleIndices.Add(burst % 5);
                    if (!pauseChecked)
                    {
                        pauseChecked = true;
                        Vector3 position = fresh[0].transform.position;
                        manager.SetGameplayPaused(true);
                        yield return WaitReal(0.15f); // 실제 부채꼴 묶음 사이 게임 정지 대기
                        Require(fan.Elapsed == elapsed && seen.Count == 5 && Normals(boss).Length == 5 && fresh[0].transform.position == position,
                            "Pause between fan bursts must freeze its schedule and already fired bullets.");
                        manager.SetGameplayPaused(false);
                        Passed("Pausing between fan bursts freezes the actual pattern timer and existing bullet flight without adding a burst");
                    }
                }
                if (!fan.IsRunning && seen.Count == 50) break;
                yield return Frames(1); // 실제 다음 프레임의 탄 생성·비행·벽 종료 관찰 대기
            }
            recording = false;
            Require(report.burstTimes.Count == 10 && seen.Count == 50 && fan.UseCount == 1 && boss.CurrentPattern == null
                && fan.Elapsed >= report.burstTimes[9] + boss.FanRecovery - 0.001f,
                "One fan execution must generate ten five-bullet bursts and finish only after the last recovery.");
            Capture("fan-finished.png");
            Passed("The actual fan emits 50 unique bullets in ten timed bursts through muzzle order 0/1/2/3/4 twice, with five down-facing 60-degree directions and configured flight/mask");

            // 그로기는 미래 묶음만 닫고 재도전은 같은 패턴을 처음 총구부터 복원
            StartFan(stage, boss, fan, body);
            yield return Until(() => Normals(boss).Length >= 10, "two fan bursts before groggy"); // 실제 두 묶음이 생성된 경계 대기
            NormalProjectile[] retained = Normals(boss);
            Require(retained.Length == 10, "The groggy boundary must follow exactly two fan bursts.");
            Vector3 retainedPosition = retained[0].transform.position;
            boss.EnterGroggy();
            yield return WaitGame(0.7f); // 취소된 다음 두 묶음의 원래 발사 시각 이후까지 대기
            Require(!fan.IsRunning && boss.CurrentPattern == null && Normals(boss).Length == 10
                && retained[0] != null && retained[0].IsActive && retained[0].transform.position != retainedPosition,
                "Groggy must cancel all future fan bursts while preserving the already emitted bullets.");
            stage.ResetEncounter();
            boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
            Require(Normals(boss).Length == 0 && fan.UseCount == 0 && boss.PatternSelector.LastPatternId == null,
                "Retry must clean old fan bullets and reset its pattern history.");
            StartFan(stage, boss, fan, body);
            yield return Until(() => Normals(boss).Length == 5, "fan first burst after retry"); // 같은 객체의 재도전 첫 총구 발사 대기
            Require(fan.UseCount == 1, "The fan pattern must restart after retry without creating a new pattern object.");
            stage.ResetEncounter();
            boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
            boss.PatternSelector.SetPatterns(production);
            Require(boss.PatternSelector.Patterns.Count == 5 && !boss.HandAttack.IsRunning && !boss.HandAttack.Warning.gameObject.activeSelf,
                "Restoration must keep all five production patterns and idle hands.");
            Passed("Groggy cancels future fan bursts while existing bullets fly; retry clears them, restarts the same fan and restores all five production patterns");

            // 실제 Player 추적 GameCamera에서 방 오른쪽 벽 도달·종료 연속 촬영
            player.IsInvulnerable = true;
            SetPosition(body, new Vector2(23f, 17f));
            GameCamera.Instance.Follow(player.transform, true);
            NormalProjectile wallShot = Spawn(boss, new Vector2(24f, 17f), Vector2.right, 8f, 0f, 1f);
            for (int i = 0; i < 12; i++)
            {
                Capture("wall/frame-" + i.ToString("D3") + ".png");
                report.wallFrameTimes.Add(Time.timeAsDouble);
                yield return WaitGame(0.05f); // 실제 게임 프레임의 벽 접촉 전후 비행 대기
            }
            Require(wallShot == null || !wallShot.IsActive, "The filmed normal shot must terminate at the actual room wall.");
            Passed("The actual Player-following GameCamera records the fan and twelve wall-contact frames without altering the map or camera settings");
        }
        finally
        {
            // 실패한 검사에서도 촬영·시간·이동한 Owner와 패턴 수명 복원
            recording = false;
            Time.captureFramerate = previousCaptureRate;
            if (boss != null)
            {
                boss.EndEncounter();
                boss.transform.position = bossPosition;
            }
            manager.SetGameplayPaused(false);
        }
        manager.ReturnToTitle();
        yield return Until(() => Title() != null && Title().IsReady && !manager.IsLoading, "wall/fan Title cleanup"); // 실제 세션과 무제한 탄 전체 정리 완료 대기
        Passed("The isolated wall/fan verification releases its lifetime-free hazards and returns to Title");
    }

    /// <summary>기존 05 객체만 격리하고 실제 Boss Update의 실행을 준비</summary>
    private static void StartFan(StageManager stage, Boss_01 boss, BossPattern fan, Rigidbody2D body)
    {
        // 맵과 캐시는 재생성하지 않고 같은 보스의 재도전 경계 사용
        stage.ResetEncounter();
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        boss.PatternSelector.SetPatterns(fan);
        stage.Player.ResetMotion();
        stage.Player.IsInvulnerable = true;
        SetPosition(body, new Vector2(boss.transform.position.x, boss.Muzzles[2].position.y - 4.125f));
        GameCamera.Instance.Follow(stage.Player.transform, true);
        boss.BeginEncounter(stage.Player);
    }

    /// <summary>새로 관찰한 한 묶음의 총구·방향·비행 설정 확인</summary>
    private static void CheckBurst(Boss_01 boss, List<NormalProjectile> fresh, int burst)
    {
        // 첫 관찰까지 최대 두 프레임의 비행을 허용하되 총구에서 벗어난 수직 오차는 거부
        Require(fresh.Count == 5, "Each fan burst must create exactly five distinct normal projectiles.");
        fresh.Sort((first, second) => first.Direction.x.CompareTo(second.Direction.x));
        Vector2 muzzle = boss.Muzzles[burst % 5].position;
        for (int i = 0; i < fresh.Count; i++)
        {
            NormalProjectile projectile = fresh[i];
            float angle = -boss.FanSpread * 0.5f + boss.FanSpread * i / (fresh.Count - 1);
            Vector2 expected = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
            Vector2 displacement = (Vector2)projectile.transform.position - muzzle;
            float travel = Vector2.Dot(displacement, projectile.Direction);
            Vector2 sideways = displacement - projectile.Direction * travel;
            Require(projectile.Owner == boss && projectile.transform.parent == boss.transform.parent
                && projectile.Lifetime == 0f && projectile.RemainingLifetime == 0f
                && projectile.Speed == boss.FanBulletSpeed && projectile.Damage == boss.FanBulletDamage
                && projectile.CollisionMask.value == -1 && Vector2.Dot(projectile.Direction, expected) > 0.9999f
                && sideways.sqrMagnitude < 0.00001f && travel >= -0.003f
                && travel <= projectile.Speed * Mathf.Max(0.05f, Time.deltaTime) * 2f + 0.01f,
                "Observed fan bullet must originate at the scheduled muzzle with its down-facing spread, Owner, mask and unlimited flight settings.");
        }
    }

    /// <summary>현재 실사용 일반 탄 프리팹으로 실제 발사 API 실행</summary>
    private static NormalProjectile Spawn(Boss_01 boss, Vector2 position, Vector2 direction, float speed, float life, float damage)
    {
        NormalProjectile projectile = UnityEngine.Object.Instantiate(boss.NormalPrefab, position, Quaternion.identity, boss.transform.parent);
        projectile.Initialize(boss, direction, speed, life, damage);
        return projectile;
    }

    /// <summary>같은 Owner의 일반 탄만 Golden과 구분해 조회</summary>
    private static NormalProjectile[] Normals(Boss_01 boss)
    {
        var result = new List<NormalProjectile>();
        foreach (NormalProjectile projectile in UnityEngine.Object.FindObjectsByType<NormalProjectile>(FindObjectsSortMode.None))
            if (projectile.GetType() == typeof(NormalProjectile) && projectile.Owner == boss && projectile.IsActive) result.Add(projectile);
        return result.ToArray();
    }

    /// <summary>동일 실제 GameCamera의 월드 화면을 추가 가공 없이 저장</summary>
    private static void Capture(string name)
    {
        Camera camera = GameCamera.Instance.Camera;
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
            string path = Path.Combine("Logs/ProjectileWallFanVerification", name);
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

    /// <summary>실제 Transform과 물리 질의 위치를 같은 시점으로 갱신</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>현재 씬의 Title 조회</summary>
    private static UI_Title Title() => UnityEngine.Object.FindFirstObjectByType<UI_Title>(FindObjectsInactive.Include);

    /// <summary>실제 프레임의 조건 완료를 제한 시간 안에 대기</summary>
    private static IEnumerator Until(Func<bool> condition, string reason)
    {
        double end = EditorApplication.timeSinceStartup + 30d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(reason);
            yield return null;
        }
    }

    /// <summary>게임 시간으로 진행되는 실제 동작 경과 대기</summary>
    private static IEnumerator WaitGame(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    /// <summary>게임 정지와 별개인 실제 Editor 시간 경과 대기</summary>
    private static IEnumerator WaitReal(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    /// <summary>실제 Update가 실행될 렌더 프레임 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "wall/fan render frames"); // 실제 생성·비행·종료 프레임 진행 대기
    }

    /// <summary>실제로 성립하지 않은 조건을 검사 실패로 기록</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>실제 통과한 항목만 결과에 추가</summary>
    private static void Passed(string message) => report.checks.Add(message);

    /// <summary>실제 결과를 저장하고 Play 종료 뒤 배치 결과 전달</summary>
    private static void Finish(Exception exception)
    {
        // 촬영·검사 반복을 닫고 실제 성공 여부와 실패 사유 보존
        recording = false;
        EditorApplication.update -= Tick; // 완료·실패 뒤 검사 갱신 구독 해제
        report.success = exception == null;
        report.error = exception?.ToString() ?? "";
        report.saveDirectory = SessionState.GetString(SaveKey, "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ProjectileWallFanVerification-result.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(SuccessKey, report.success);
        SessionState.SetBool(FinishedKey, true);
        if (exception == null) Debug.Log("[ProjectileWallFanChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[ProjectileWallFanChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>실제 통과 항목·실패와 생성·촬영 시각을 저장할 결과</summary>
    [Serializable]
    private class Report
    {
        public bool success; // 모든 실제 검사 조건의 통과 여부
        public List<string> checks = new List<string>(); // 실제 통과한 검사 묶음
        public List<double> burstTimes = new List<double>(); // 실제 관찰한 열 묶음의 패턴 경과 시각
        public List<int> muzzleIndices = new List<int>(); // 실제 각 묶음에 대응한 다섯 총구 순서
        public List<double> fanFrameTimes = new List<double>(); // 대표 부채꼴 연속 PNG의 게임 시각
        public List<double> wallFrameTimes = new List<double>(); // 벽 접촉 연속 PNG의 게임 시각
        public string error; // 실패 시 실제 예외 전체 내용
        public string saveDirectory; // 사용자 저장과 분리한 검사 세션 위치
    }
}
