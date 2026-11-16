using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 경비 패턴 갱신으로 공중 손·중앙 채움·착지 피해와 취소 수명 확인</summary>
[InitializeOnLoad]
public static class GuardHandChecks
{
    private const string ActiveKey = "Chrono.GuardHandChecks.Active"; // Play 재로드 뒤 검사 요청 복원 키
    private const string FinishedKey = "Chrono.GuardHandChecks.Finished"; // 편집 복귀에서 검사 완료 구분 키
    private const string SuccessKey = "Chrono.GuardHandChecks.Success"; // 배치 종료 코드로 전달할 결과 키
    private const string SceneKey = "Chrono.GuardHandChecks.Scene"; // 원래 편집 씬으로 복귀할 경로 키
    private const string SaveKey = "Chrono.GuardHandChecks.Save"; // 실제 사용자 저장과 분리한 검사 저장 경로 키
    private static Stack<IEnumerator> routines; // 중첩 검사·대기를 실제 프레임에서 진행할 스택
    private static Report report; // 통과 항목·실패 사유·실제 캡처 시각 기록
    private static double deadline; // 전체 검사 무한 대기를 막을 Editor 종료 시각
    private static bool recording; // 대표 손 공격의 실제 GameCamera 연속 캡처 여부
    private static GuardHandAttack recordedAttack; // 연속 캡처의 현재 단계를 설명할 실제 실행체
    private static string recordedPattern; // 취소 뒤 Idle 프레임에도 유지할 이번 촬영의 패턴 ID
    private static int lastRecordedFrame = -1; // 같은 실제 렌더 프레임의 중복 캡처 방지

    /// <summary>도메인 재로드 뒤에도 요청한 검사만 연결</summary>
    static GuardHandChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode; // 실제 Play 진입·종료 경계에서 검사 시작과 결과 처리
    }

    /// <summary>실제 경비 프리팹을 준비하고 새 저장의 Title 세션에서 검사 시작</summary>
    [MenuItem("Tools/Chrono/Check Guard Hand Attack")]
    public static void RunBatch()
    {
        // 저장하지 않은 사용자 씬을 덮어쓰지 않도록 시작 조건 확인
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin hand checks outside Play Mode.");
        Scene original = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (SceneManager.sceneCount != 1 || original.isDirty || string.IsNullOrEmpty(original.path)))
            throw new InvalidOperationException("Hand checks require one saved, unmodified scene.");
        SessionState.SetString(SceneKey, original.path);
        GuardBossSetup.Run();
        string directory = Path.GetFullPath(Path.Combine("Temp", "GuardHandChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        SessionState.SetString(SaveKey, directory);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (manager == null) throw new InvalidOperationException("The actual Title requires GameManager.");
        manager.SaveDirectory = directory;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>실제 Play 진입과 종료에 맞춰 이번 검사만 실행</summary>
    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // 일반 사용자의 Play에는 검사 콜백을 연결하지 않음
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(Check()); // 실제 생성·패턴·피해·수명 경계 검사 순차 실행
            deadline = EditorApplication.timeSinceStartup + 180d;
            EditorApplication.update += Tick; // 실제 프레임 사이에 검사 대기와 연속 촬영 진행
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
            else EditorSceneManager.OpenScene(SessionState.GetString(SceneKey, "Assets/Scenes/Title.unity"));
        }
    }

    /// <summary>중첩 대기를 실제 프레임으로 진행하고 실행 중인 대표 공격만 촬영</summary>
    private static void Tick()
    {
        try
        {
            // 같은 실제 프레임은 한 번만 촬영하고 전체 검사 시간 제한
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Guard hand checks exceeded 180 seconds.");
            if (recording && Time.frameCount != lastRecordedFrame)
            {
                lastRecordedFrame = Time.frameCount;
                int index = report.frameTimes.Count;
                Capture("frames/frame-" + index.ToString("D3") + ".png");
                report.frameTimes.Add(Time.timeAsDouble);
                report.framePhases.Add(recordedAttack.Phase.ToString());
                report.framePatterns.Add(recordedPattern);
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

    /// <summary>실제 두 패턴 등록과 손의 선택·표시·단일 착지·취소·교대 확인</summary>
    private static IEnumerator Check()
    {
        // 실제 Title과 Stage가 현재 프리팹의 Awake·Start 연결을 마칠 때까지 대기
        yield return Until(() => GameManager.Instance != null && Title() != null && Title().IsReady, "hand Title readiness"); // 공유 저장·Title 준비 완료 대기
        GameManager manager = GameManager.Instance;
        Title().SelectSlot(0);
        yield return Until(() => !manager.IsLoading && manager.Stage.IsReady && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsSpawning, "hand session readiness"); // 실제 세션과 최초 기상 모션 완료 대기
        StageManager stage = manager.Stage;
        Boss_01 boss = manager.CurrentBoss as Boss_01;
        PlayerCharacter player = manager.CurrentPlayer;
        Require(boss != null && boss.HandAttack != null && boss.PatternSelector.Patterns.Count == 5,
            "The production guard must register its golden shot, chasing hand, central slam, eye laser and fan.");
        BossPattern[] production = { boss.PatternSelector.Patterns[0], boss.PatternSelector.Patterns[1], boss.PatternSelector.Patterns[2], boss.PatternSelector.Patterns[3], boss.PatternSelector.Patterns[4] };
        BossPattern gold = Find(production, "F-B07-01");
        BossPattern hand = Find(production, "F-B07-02");
        BossPattern central = Find(production, "F-B07-03");
        BossPattern fan = Find(production, "F-B07-05");
        BossPattern eye = Find(production, "F-B07-04");
        GuardHandAttack attack = boss.HandAttack;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Transform[] hands = { attack.Hands[0], attack.Hands[1] };
        Vector3[] homes = { hands[0].localPosition, hands[1].localPosition };
        AreaWarningVfx warning = attack.Warning;
        Mesh fillMesh = warning.FillRenderer.GetComponent<MeshFilter>().sharedMesh;
        Material fillMaterial = warning.FillRenderer.sharedMaterial;
        Require(gold != null && hand != null && central != null && fan != null && eye != null && boss.NormalPrefab != null && attack.Hands.Length == 2 && !warning.gameObject.activeSelf
            && warning.GetComponent<LineRenderer>().sortingOrder == 22 && warning.FillRenderer.sortingOrder == 21
            && boss.GetComponentsInChildren<AreaWarningVfx>(true).Length == 3
            && homes[0] == new Vector3(-4.5f, 0f, 0f) && homes[1] == new Vector3(4.5f, 0f, 0f)
            && boss.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite == AssetDatabase.LoadAssetAtPath<Sprite>(GuardBossSetup.BodySpritePath),
            "The native split body must connect two authored home transforms and exactly one inactive shared warning.");
        for (int i = 0; i < hands.Length; i++)
        {
            SpriteRenderer renderer = hands[i].GetComponent<SpriteRenderer>();
            Require(renderer.sprite.texture.width == 34 && renderer.sprite.texture.height == 40 && renderer.sprite.pixelsPerUnit == 16f
                && renderer.sprite.pivot == new Vector2(17f, 0f) && renderer.sortingOrder == 24 && hands[i].localScale == Vector3.one
                && hands[i].GetComponent<Collider2D>() == null, "Separated hands must retain their natural pixels, bottom pivot and visual-only geometry.");
        }
        Passed("Actual Title/Stage initialization registers guard patterns 01/02/03/04/05 and connects the split body, two hands, Normal/Gold prefabs and one reusable warning");
        PlayerInputController.Instance.enabled = false;
        PlayerInputController.Instance.BlockInput = false;
        player.ResetMotion();
        player.Buffs.Clear();
        player.HitProtectionDuration = 0f;
        player.IsInvulnerable = true;
        int originalCaptureRate = Time.captureFramerate;
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        GameObject extraHitbox = null;
        Transform centralReviewAnchor = null;
        try
        {
            // 전투 전 실제 CollisionBox와 물리 이동으로 전체 몸통 피격·충돌 분리 확인
            CheckBodyHitbox(stage, boss, player, body);

            // 공통 F 검사는 기존 경비 회귀에서 유지하며 여기서는 실제 조우 Trigger부터 진행
            stage.Map.SetEntryDoorOpen(boss.BossId, true);
            yield return Until(() => stage.Map.GetEntryDoor(boss.BossId).GetComponent<MapDoor>().IsOpen, "hand entry door opening"); // 기존 문 모션의 실제 개방 완료 대기
            SetPosition(body, boss.GetComponent<BossSpawner>().EncounterTrigger.bounds.center);
            yield return Until(() => boss.IsInCombat && !stage.IsBossSequencePlaying, "hand actual encounter trigger"); // 실제 조우 Trigger와 컷씬 완료 대기
            Time.captureFramerate = 20;

            // 여러 고정 난수에서 두 손 모두 선택되며 반대 손은 기본 위치를 유지
            bool leftSelected = false;
            bool rightSelected = false;
            for (int seed = 0; seed < 8; seed++)
            {
                yield return StartHand(stage, boss, hand, body, seed); // 실제 Boss Update의 단독 손 패턴 시작 대기
                int selected = attack.SelectedHandIndex;
                Require(selected == 0 || selected == 1, "A hand attack must choose exactly one of its two hands.");
                leftSelected |= selected == 0;
                rightSelected |= selected == 1;
                Require(hands[1 - selected].localPosition == homes[1 - selected], "The unselected hand must remain at its authored home.");
                boss.EnterGroggy();
                Require(Home(attack, homes), "Groggy must return both hands and hide the reusable warning immediately.");
            }
            Require(leftSelected && rightSelected, "Multiple deterministic seeds must exercise both left and right hand choices.");
            Passed("Multiple actual pattern starts choose both hands across deterministic seeds while leaving the other hand home and cancelling cleanly");

            // 대표 공격은 실제 카메라와 고정 캡처 시간으로 전 구간 촬영
            yield return StartHand(stage, boss, hand, body, 17); // 대표 추격 공격 시작 대기
            player.IsInvulnerable = false;
            player.HP = player.maxHP;
            player.ResetHitProtection();
            recordedAttack = attack;
            recordedPattern = "F-B07-02";
            lastRecordedFrame = -1;
            recording = true;
            int selectedHand = attack.SelectedHandIndex;
            Vector2 initialGround = attack.GroundPosition;
            Vector3 warningScale = warning.transform.localScale;
            Require(warning.gameObject.activeSelf && attack.FillProgress <= 0.1f && warning.FillRenderer.transform.localScale.magnitude <= 0.18f
                && Mathf.Abs(warningScale.x - attack.ImpactRadius * 2f) < 0.001f,
                "The following warning must begin empty at the same time as the hand's tracking charge.");
            Capture("empty-warning.png");
            yield return WaitGame(0.2f); // 선택한 손의 초기 상승과 지면 추격 대기
            SetPosition(body, Arena(boss) + Vector2.right * 1.5f);
            GameCamera.Instance.Follow(player.transform, true);
            yield return WaitGame(attack.RiseDuration + 0.1f); // 이동한 지면 목표 추격과 목표 높이 도달 대기
            Require(attack.Phase == GuardHandPhase.Tracking && warning.gameObject.activeSelf
                && Vector2.Distance(initialGround, attack.GroundPosition) > 0.2f
                && Mathf.Abs(hands[selectedHand].position.y - attack.GroundPosition.y - attack.Height) < 0.01f
                && hands[1 - selectedHand].localPosition == homes[1 - selectedHand]
                && Vector2.Distance(warning.transform.position, attack.GroundPosition) < 0.001f && attack.FillProgress > 0f,
                "Tracking must raise only the selected hand while its visible warning follows the ground and charges simultaneously.");
            Capture("tracking.png");
            Passed("The selected hand rises and chases while its initially empty warning follows the same ground and fills, leaving the other hand home");

            // 추적과 채움이 동시에 진행되는 중간 상태에서 정지·재개 확인
            float healthOutside = player.HP;
            Vector2 pauseGround = attack.GroundPosition;

            // 정지는 단계 시간·차오름·손·현재 지면을 모두 보존
            float phaseElapsed = attack.PhaseElapsed;
            float fillProgress = attack.FillProgress;
            Vector3 handPosition = hands[selectedHand].position;
            manager.SetGameplayPaused(true);
            yield return WaitReal(0.15f); // 게임 정지 중 실제 시간 경과 대기
            Require(attack.Phase == GuardHandPhase.Tracking && attack.PhaseElapsed == phaseElapsed && attack.FillProgress == fillProgress
                && hands[selectedHand].position == handPosition && attack.GroundPosition == pauseGround && player.HP == healthOutside,
                "Paused hand warning must preserve its phase, fill, hand and fixed ground.");
            manager.SetGameplayPaused(false);
            yield return Until(() => attack.FillProgress >= 0.4f, "hand fill resumes"); // 실제 게임 시간 재개 후 중앙 채움 진행 대기
            Passed("Pause freezes the airborne hand, following warning and fill progress; resume continues the same attack");
            Require(attack.Phase == GuardHandPhase.Tracking && attack.FillProgress < 0.9f
                && Vector2.Distance(warning.transform.position, attack.GroundPosition) < 0.001f
                && warning.transform.localScale == warningScale && warning.FillRenderer.transform.localScale.x > 0f
                && warning.FillRenderer.transform.localScale.x < 1f && warning.FillRenderer.GetComponent<MeshFilter>().sharedMesh == fillMesh
                && warning.FillRenderer.sharedMaterial == fillMaterial,
                "The warning must follow its ground while only its centered fill expands; outline size and shared assets remain fixed.");
            Capture("half-fill.png");
            Passed("The moving warning preserves its full-size outline while its centered fill and charge timer advance using the same mesh/material");

            // 실제 full 표시 뒤 확정된 0.2초를 유지한 다음에만 하강
            yield return Until(() => attack.Phase == GuardHandPhase.Holding, "full hand warning hold"); // 가득 찬 경고의 실제 표시 시작 대기
            Vector2 locked = attack.GroundPosition;
            double fullAt = Time.timeAsDouble - attack.PhaseElapsed;
            Require(attack.FillProgress == 1f && warning.FillRenderer.transform.localScale == Vector3.one && attack.PhaseElapsed < 0.051f,
                "The full warning must enter its own hold interval before falling.");
            SetPosition(body, locked + Vector2.right * (attack.ImpactRadius + 2f));
            GameCamera.Instance.Follow(player.transform, true);
            Capture("filled-hold.png");
            yield return Until(() => attack.PhaseElapsed >= 0.1f || attack.Phase != GuardHandPhase.Holding, "first half of filled hold"); // 실제 full 유지 구간 중간까지 대기
            Require(attack.Phase == GuardHandPhase.Holding && player.HP == healthOutside && attack.GroundPosition == locked
                && Vector2.Distance(warning.transform.position, locked) < 0.001f,
                "Only the full warning must lock its ground and stay raised through the first 0.1 game seconds after the Player leaves.");
            yield return Until(() => attack.Phase == GuardHandPhase.Falling && attack.PhaseElapsed > 0.01f, "hand falling frame"); // 실제 빠른 하강의 중간 표시 프레임 대기
            Require(Time.timeAsDouble - fullAt >= 0.199f && player.HP == healthOutside,
                "The hand must wait at least 0.2 game seconds after full fill and must not damage before impact.");
            Capture("falling.png");
            Passed("After the fill is visibly complete the hand waits 0.2 game seconds, then falls without early damage");

            // 범위 밖에서는 착지 피해가 없고 착지 뒤 늦게 들어와도 추가 질의 없음
            yield return Until(() => attack.Phase == GuardHandPhase.Recovery, "outside hand impact"); // 실제 지면 도달과 일회 착지 질의 완료 대기
            Require(player.HP == healthOutside && !warning.gameObject.activeSelf
                && Mathf.Abs(hands[selectedHand].position.y - locked.y) < 0.001f,
                "Impact must hide the warning and place the hand on its locked ground without damaging an outside Player.");
            Capture("impact.png");
            SetPosition(body, locked);
            player.ResetHitProtection();
            yield return WaitGame(0.1f); // 착지 뒤 뒤늦은 진입에도 피해가 재발생하지 않는지 확인
            Require(player.HP == healthOutside, "Entering after the one-time impact must not cause late damage.");
            yield return Until(() => !attack.IsRunning, "hand returns home"); // 실제 회복·복귀 모션 완료 대기
            Require(Home(attack, homes), "Normal completion must restore both original hands and reset the reusable warning.");
            Capture("returned.png");
            recording = false;
            Passed("The locked impact deals zero damage outside its circle and on late entry, then the same hand and warning return home");

            // 실제 Player의 여러 피격 Collider가 같은 착지 피해 한 번으로 합쳐지는지 확인
            yield return StartHand(stage, boss, hand, body, 23); // 범위 안 실제 피해를 확인할 새 손 공격 시작 대기
            player.IsInvulnerable = false;
            player.HP = player.maxHP;
            player.ResetHitProtection();
            extraHitbox = new GameObject("HandCheckExtraHitbox");
            extraHitbox.transform.SetParent(player.transform, false);
            extraHitbox.AddComponent<CircleCollider2D>().isTrigger = true;
            extraHitbox.GetComponent<CircleCollider2D>().radius = 0.1f;
            extraHitbox.AddComponent<Hitbox>().Owner = player;
            yield return Until(() => attack.Phase == GuardHandPhase.Holding, "inside warning lock"); // 가득 찬 실제 Player 위치의 새 경고 고정 대기
            SetPosition(body, attack.GroundPosition);
            yield return Until(() => attack.Phase == GuardHandPhase.Recovery, "inside actual hand damage"); // 실제 원형 착지 피해 판정 완료 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - attack.ImpactDamage), "Inside impact must damage the Player once even with multiple Hitboxes.");
            player.ResetHitProtection();
            yield return WaitGame(0.15f); // 보호 시간이 없어도 같은 착지 피해를 반복하지 않는지 확인
            Require(Mathf.Approximately(player.HP, player.maxHP - attack.ImpactDamage), "The same landed hand must not repeatedly damage its Player.");
            UnityEngine.Object.Destroy(extraHitbox);
            extraHitbox = null;
            Passed("A real inside impact applies configured damage exactly once across multiple Player Hitboxes and never repeats on the grounded hand");

            // 지연된 큰 Tick도 실제 full 표시 전 초과 시간을 유지 구간에서 소비하지 않음
            yield return StartHand(stage, boss, hand, body, 24); // 지연 Tick 경계용 실제 패턴 준비 대기
            attack.Tick(attack.ChargeDuration + 0.5f);
            Vector3 heldHand = attack.Hands[attack.SelectedHandIndex].position;
            Require(attack.Phase == GuardHandPhase.Holding && attack.PhaseElapsed == 0f && attack.FillProgress == 1f,
                "A large fill overshoot must enter Holding at zero with the fully visible fill.");
            attack.Tick(0.19f);
            Require(attack.Phase == GuardHandPhase.Holding && attack.Hands[attack.SelectedHandIndex].position == heldHand,
                "The first 0.19 seconds after full display must preserve the raised hand.");
            attack.Tick(0.02f);
            Require(attack.Phase == GuardHandPhase.Falling && attack.Hands[attack.SelectedHandIndex].position.y < heldHand.y,
                "Only the subsequent tick crossing 0.2 may start falling.");
            boss.EnterGroggy();
            Passed("A delayed fill tick starts Holding at zero, preserves height for 0.19 seconds and begins falling only after the next 0.02 seconds");

            // 그로기와 실제 재도전은 미래 착지만 취소하고 기존 Gold 탄은 유지
            yield return StartHand(stage, boss, hand, body, 29); // 취소와 재사용 수명 검사용 실제 공격 시작 대기
            yield return Until(() => attack.ChargeElapsed >= 0.3f, "groggy charging warning"); // 실제 이동 경고가 차오른 뒤 중단 경계 대기
            GoldProjectile existingGold = UnityEngine.Object.Instantiate(boss.GoldPrefab, boss.transform.position + Vector3.right * 8f, Quaternion.identity, boss.transform.parent);
            existingGold.Initialize(boss, Vector2.up, 1f, 10f, 1f);
            Vector3 previousGoldPosition = existingGold.transform.position;
            float beforeCancelHP = player.HP;
            boss.EnterGroggy();
            Require(Home(attack, homes) && existingGold.IsActive, "Groggy must cancel the hand and warning while retaining the already fired gold projectile.");
            yield return WaitGame(0.12f); // 그로기 중 기존 Gold의 독립 비행 대기
            Require(existingGold != null && existingGold.IsActive && existingGold.transform.position != previousGoldPosition && player.HP == beforeCancelHP,
                "Existing gold must keep moving while cancelled hand damage stays closed.");
            yield return StartHand(stage, boss, hand, body, 31); // 같은 손과 경고를 다시 사용한 재도전 시작 대기
            yield return Until(() => attack.ChargeElapsed >= 0.3f, "retry charging warning"); // 실제 재사용 경고의 채움 진행 대기
            stage.ResetEncounter();
            Require(Home(attack, homes) && attack.Warning == warning && attack.Hands[0] == hands[0] && attack.Hands[1] == hands[1]
                && warning.FillRenderer.GetComponent<MeshFilter>().sharedMesh == fillMesh,
                "Retry must restore the same prepared hands, warning and fill mesh without replacement.");
            Passed("Groggy cancels future hand damage while an existing Gold keeps flying; retry restores and reuses the original hands and warning");

            // 중앙 공격은 비활성 조우 영역에서도 같은 중앙과 양손·큰 경고를 재사용
            yield return StartHand(stage, boss, central, body, 35); // 실제 세 번째 패턴과 양손 충전 시작 대기
            Collider2D area = boss.GetComponent<BossSpawner>().EncounterTrigger;
            Vector2 center = area.transform.TransformPoint(area.offset);
            Require(!area.enabled && center.sqrMagnitude > 1f && attack.IsCentral && attack.SelectedHandIndex == -1
                && Vector2.Distance(attack.GroundPosition, center) < 0.001f
                && attack.CurrentRadius == attack.CentralRadius && attack.CurrentDuration == attack.CentralChargeDuration
                && attack.CurrentDamage == attack.CentralDamage && Mathf.Abs(warning.transform.localScale.x - attack.CentralRadius * 2f) < 0.001f,
                "Central slam must use the disabled encounter transform center, both hands and its larger per-run warning settings.");
            player.IsInvulnerable = false;
            player.HP = player.maxHP;
            player.ResetHitProtection();
            SetPosition(body, center + Vector2.up * 3.4f);
            centralReviewAnchor = new GameObject("CentralSlamReviewAnchor").transform;
            centralReviewAnchor.position = center;
            GameCamera.Instance.Follow(centralReviewAnchor, true);
            recordedAttack = attack;
            recordedPattern = "F-B07-03";
            lastRecordedFrame = -1;
            recording = true;
            Capture("central-empty.png");
            yield return WaitGame(0.4f); // 두 손의 실제 중앙 이동과 큰 원 충전 진행 대기
            SetPosition(body, center + Vector2.up * 3.4f + Vector2.right);
            yield return WaitGame(0.15f); // Player 이동에도 중앙 경고가 따라오지 않는지 확인
            Require(attack.IsCentral && attack.Phase == GuardHandPhase.Tracking && attack.FillProgress > 0f && attack.FillProgress < 1f
                && Vector2.Distance(warning.transform.position, center) < 0.001f && Vector2.Distance(attack.GroundPosition, center) < 0.001f,
                "The central warning must fill at its fixed center while both hands move and the Player changes position.");
            SetPosition(body, center + Vector2.up * 3.4f);
            GameCamera.Instance.Follow(centralReviewAnchor, true);
            Capture("central-charging.png");
            yield return Until(() => attack.Phase == GuardHandPhase.Holding, "central full warning"); // 큰 원의 실제 full 표시 완료 대기
            Require(Mathf.Abs(hands[0].position.x - center.x + attack.CentralHandOffset) < 0.01f
                && Mathf.Abs(hands[1].position.x - center.x - attack.CentralHandOffset) < 0.01f
                && Mathf.Abs(hands[0].position.y - center.y - attack.Height) < 0.01f
                && Mathf.Abs(hands[1].position.y - center.y - attack.Height) < 0.01f && attack.FillProgress == 1f,
                "The charged central attack must hold both raised hands at their authored center offsets.");
            Capture("central-full.png");
            double centralFullAt = Time.timeAsDouble - attack.PhaseElapsed;
            yield return Until(() => attack.Phase == GuardHandPhase.Falling && attack.PhaseElapsed > 0.01f, "central falling frame"); // 실제 양손 하강 중간 프레임 대기
            Require(Time.timeAsDouble - centralFullAt >= 0.199f && player.HP == player.maxHP,
                "Central slam must also wait 0.2 seconds after full fill and avoid early damage.");
            Capture("central-falling.png");
            yield return Until(() => attack.Phase == GuardHandPhase.Recovery, "central actual impact"); // 실제 양손 착지와 범위 피해 완료 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - attack.CentralDamage)
                && Mathf.Abs(hands[0].position.y - center.y) < 0.001f && Mathf.Abs(hands[1].position.y - center.y) < 0.001f
                && !warning.gameObject.activeSelf, "Central impact must land both hands and apply its configured inside damage once.");
            Capture("central-impact.png");
            Passed("The third pattern uses the disabled encounter's real center, charges one larger fixed warning, holds both hands for 0.2 seconds and lands with one central damage event");

            // 같은 중앙의 여섯 세로 원점에서 좌우 두 발씩 실제 생성됐는지 확인
            NormalProjectile[] rows = Normals(boss);
            CheckCentralRows(boss, center, rows);
            yield return WaitGame(0.45f); // 실제 회복·복귀와 세로줄 분산을 포함해 중앙 공격을 40프레임 이상 촬영
            Vector3 firstRowPosition = rows[0].transform.position;
            boss.EnterGroggy();
            yield return WaitGame(0.1f); // 착지 후 그로기에도 이미 나온 일반 탄의 독립 비행 대기
            Require(Home(attack, homes) && Normals(boss).Length == 12 && rows[0] != null && rows[0].IsActive
                && rows[0].transform.position != firstRowPosition, "Groggy after impact must return both hands while all twelve emitted normal bullets keep flying.");
            Capture("central-bullets.png");
            recording = false;
            GameCamera.Instance.Follow(player.transform, true);
            UnityEngine.Object.Destroy(centralReviewAnchor.gameObject);
            centralReviewAnchor = null;
            Passed("Central impact emits exactly twelve Owner-bound normal bullets from six centered vertical origins in left/right directions, and groggy preserves their flight");

            // 착지 전 중앙 취소는 충격·후속 세로줄 모두 차단
            yield return StartHand(stage, boss, central, body, 37); // 취소 확인용 실제 중앙 패턴 재시작 대기
            player.IsInvulnerable = false;
            player.HP = player.maxHP;
            player.ResetHitProtection();
            SetPosition(body, center);
            yield return WaitGame(0.3f); // 실제 큰 원의 부분 충전 표시 대기
            boss.EnterGroggy();
            yield return WaitGame(attack.CentralChargeDuration + 0.4f); // 원래 착지 경계 이후까지 취소 유지 확인
            Require(Home(attack, homes) && player.HP == player.maxHP && Normals(boss).Length == 0
                && attack.Warning == warning && attack.Hands[0] == hands[0] && attack.Hands[1] == hands[1],
                "Cancelling a central charge must block its future AOE and bullet callback without replacing prepared visuals.");
            Passed("Cancelling the central charge restores both original hands and warning and prevents all future central damage and bullet rows");

            // 세 실제 패턴의 직전 제외와 Golden 반사·각성·발사자 피해 유지
            stage.ResetEncounter();
            boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
            boss.PatternSelector.SetPatterns(production);
            gold.Priority = 1;
            hand.Priority = 0;
            central.Priority = 0;
            fan.Priority = 0;
            eye.Priority = 0;
            SetPosition(body, Arena(boss));
            player.IsInvulnerable = true;
            player.Buffs.Clear();
            player.HP = player.maxHP - 10f;
            yield return Frames(1); // 같은 프레임 효과 제거 우선 경계 이후 반사 보상 허용 대기
            boss.BeginEncounter(player);
            yield return Until(() => boss.CurrentPattern == gold, "production gold pattern first"); // 실제 공통 선택기의 첫 Gold 시작 대기
            gold.Priority = 0;
            hand.Priority = 1;
            yield return Until(() => Gold(boss) != null, "production golden projectile"); // 실제 첫 패턴의 Gold 발사 대기
            GoldProjectile reflected = Gold(boss);
            float beforeGoldHP = boss.HP;
            Vector2 toBoss = ((Vector2)boss.GetComponentInChildren<Hitbox>().Bounds.center - (Vector2)reflected.transform.position).normalized;
            Vector2 lineDirection = (reflected.Direction.normalized + toBoss).normalized;
            if (lineDirection.sqrMagnitude < 0.0001f) lineDirection = new Vector2(-reflected.Direction.y, reflected.Direction.x);
            Require(reflected.OnSlashed(player, boss, new DamageInstance(player, 10f, DamageType.Slash, DamageInstance.NextAttackId()), lineDirection)
                && reflected.IsReflected && reflected.ReflectionTarget == boss && Vector2.Dot(reflected.Direction, toBoss) > 0.9999f,
                "A valid fixture Line must geometrically reflect the production Gold toward its real guard body Hitbox.");
            yield return Until(() => player.Buffs.IsFever && (reflected == null || !reflected.IsActive), "three-pattern Gold reflection and Fever"); // 실제 다음 프레임 각성과 발사자 도달 피해 대기
            Require(Mathf.Approximately(boss.HP, beforeGoldHP - boss.ProjectileDamage), "Reflected gold must still damage its actual guard Owner once.");
            yield return Until(() => boss.CurrentPattern == hand && hand.UseCount == 1, "actual production hand selection"); // 실제 Gold 완료 뒤 다른 패턴 선택 대기
            hand.Priority = 0;
            central.Priority = 1;
            yield return Until(() => boss.CurrentPattern == central && central.UseCount == 1, "actual production central selection"); // 실제 한 손 복귀 뒤 중앙 패턴 선택 대기
            central.Priority = 0;
            yield return Until(() => gold.UseCount + hand.UseCount + central.UseCount + fan.UseCount + eye.UseCount >= 4, "actual registered-pattern next selection"); // 세 패턴 완료 뒤 기본 가중 선택으로 다음 시작 대기
            Require(gold.UseCount >= 1 && hand.UseCount >= 1 && central.UseCount == 1
                && gold.UseCount + hand.UseCount + central.UseCount + fan.UseCount + eye.UseCount == 4 && boss.CurrentPattern != central,
                "All three production patterns must run and the following default selection must exclude the previous central pattern.");
            boss.EndEncounter();
            Passed("All three production patterns run through actual Boss Update and the next default draw excludes the previous pattern while Golden reflection, Fever and Owner damage remain intact");

            // 실제 Boss 사망은 진행 중 손·경고·소유 탄을 정리하고 처치 상태 유지
            yield return StartHand(stage, boss, hand, body, 41); // 실제 사망으로 취소할 새 손 패턴 시작 대기
            yield return Until(() => attack.ChargeElapsed >= 0.3f, "death charging warning"); // 경고 표시 중 실제 사망 경계 대기
            GoldProjectile deathGold = UnityEngine.Object.Instantiate(boss.GoldPrefab, boss.transform.position + Vector3.right * 8f, Quaternion.identity, boss.transform.parent);
            deathGold.Initialize(boss, Vector2.up, 0f, 5f, 1f);
            Require(boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Slash, DamageInstance.NextAttackId()))
                && boss.IsDead && Home(attack, homes) && (deathGold == null || !deathGold.IsActive),
                "Actual Boss death must cancel its pending hand, hide the warning and clean owned projectiles.");
            yield return Until(() => !stage.IsBossSequencePlaying && stage.Map.GetExitDoor(boss.BossId).GetComponent<MapDoor>().IsOpen,
                "hand boss death cleanup"); // 실제 처치 연출과 출구 개방 완료 대기
            stage.ResetEncounter();
            Require(boss.IsDead && Home(attack, homes), "Reset after recorded defeat must keep the corpse and idle hands.");
            Passed("Actual Boss death closes the pending impact and owned hazards, opens the exit and keeps its corpse and idle hands on reset");
        }
        finally
        {
            // 실패한 검사에서도 시간·난수·촬영과 임시 피격 Collider 복원
            recording = false;
            Time.captureFramerate = originalCaptureRate;
            UnityEngine.Random.state = originalRandom;
            if (extraHitbox != null) UnityEngine.Object.Destroy(extraHitbox);
            if (GameCamera.Instance != null) GameCamera.Instance.Follow(player.transform, true);
            if (centralReviewAnchor != null) UnityEngine.Object.Destroy(centralReviewAnchor.gameObject);
            if (boss != null) boss.CancelPattern();
            manager.SetGameplayPaused(false);
        }
        manager.ReturnToTitle();
        yield return Until(() => Title() != null && Title().IsReady && !manager.IsLoading, "hand check Title cleanup"); // 실제 세션과 손 표시 해제 완료 대기
    }

    /// <summary>기존 손 패턴 객체만 격리하고 실제 Boss Update가 새 실행을 시작하도록 준비</summary>
    private static IEnumerator StartHand(StageManager stage, Boss_01 boss, BossPattern pattern, Rigidbody2D body, int seed)
    {
        // 맵 재생성 없이 같은 보스의 재도전 API와 공통 이력 초기화만 사용
        stage.ResetEncounter();
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        boss.PatternSelector.SetPatterns(pattern);
        stage.Player.ResetMotion();
        SetPosition(body, Arena(boss));
        GameCamera.Instance.Follow(stage.Player.transform, true);
        UnityEngine.Random.InitState(seed);
        boss.BeginEncounter(stage.Player);
        yield return Until(() => pattern.IsRunning && boss.HandAttack.Phase == GuardHandPhase.Tracking, "isolated hand pattern starts"); // 실제 Boss.Update의 선택·Begin 완료 대기
    }

    /// <summary>실제 일반·Slash 공간 질의와 이동으로 전체 몸통의 피격·물리 영역 분리 확인</summary>
    private static void CheckBodyHitbox(StageManager stage, Boss_01 boss, PlayerCharacter player, Rigidbody2D playerBody)
    {
        // 실험실 벽·문과 무관한 위치에서 같은 실제 보스의 두 영역만 비교
        BoxCollider2D solid = boss.GetComponent<BoxCollider2D>();
        Transform hitRoot = boss.transform.Find("BodyHitbox");
        Require(hitRoot != null && boss.GetComponent<Hitbox>() == null && boss.GetComponentsInChildren<Hitbox>(true).Length == 1,
            "The guard must expose exactly one child body Hitbox and remove the old root damage receiver.");
        BoxCollider2D hurt = hitRoot.GetComponent<BoxCollider2D>();
        Require(!solid.isTrigger && solid.size == new Vector2(5.5f, 3f) && solid.offset == new Vector2(0f, 1.5f)
            && hurt.isTrigger && hurt.size == new Vector2(5.75f, 4.875f) && hurt.offset == new Vector2(0f, 2.4375f)
            && hitRoot.localPosition == Vector3.zero && hitRoot.GetComponent<Hitbox>().Owner == boss,
            "The original solid torso must remain unchanged beside the full-art trigger damage bounds.");
        boss.transform.position = new Vector3(400f, 400f, 0f);
        Physics2D.SyncTransforms();
        var query = new CollisionBox();
        Vector2[] points = { new Vector2(400f, 404.6f), new Vector2(397.4f, 403.7f), new Vector2(402.6f, 403.7f) };
        foreach (Vector2 point in points)
        {
            float before = boss.HP;
            query.Begin(new DamageInstance(player, 3f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
            Require(query.Query(point, Vector2.one * 0.2f, 0f) == 1 && Mathf.Approximately(boss.HP, before - 3f)
                && query.Query(point, Vector2.one * 0.2f, 0f) == 0,
                "A real normal attack query must hit each head/upper-shoulder point once per attack.");
            query.End();
        }
        float beforeSlash = boss.HP;
        query.Begin(new DamageInstance(player, 5f, DamageType.Slash, DamageInstance.NextAttackId()), ~0);
        Require(query.QuerySlash(new Vector2(400f, 402.4f), new Vector2(7f, 6f), 0f, player, boss) == 1
            && Mathf.Approximately(boss.HP, beforeSlash - 5f)
            && query.QuerySlash(points[0], Vector2.one * 0.2f, 0f, player, boss) == 0,
            "A full-body Slash query must damage the guard once and reject repeated queries for that attack.");
        query.End();

        // 새 상부 Trigger는 통과하고 기존 하부 solid 범위에서만 실제 이동 차단
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        SetPosition(playerBody, new Vector2(396f, 403.7f));
        movement.Move(Vector2.right * 8f);
        Require(playerBody.position.x > 403.5f, "The full-height damage trigger must not expand the guard's physical blocker.");
        SetPosition(playerBody, new Vector2(396f, 401.5f));
        movement.Move(Vector2.right * 8f);
        Require(playerBody.position.x < 397.3f, "The unchanged solid torso must still block actual Player movement.");
        stage.ResetEncounter();
        SetPosition(playerBody, player.Spawner.SpawnPoint.position);
        Passed("Real normal attacks hit the head and both upper shoulders; full-body Slash damages once while the upper damage trigger stays passable and the original torso blocks movement");
    }

    /// <summary>시간 만료 없는 중앙 세로줄의 좌우 쌍과 같은 생성 원점을 검산</summary>
    private static void CheckCentralRows(Boss_01 boss, Vector2 center, NormalProjectile[] rows)
    {
        // 같은 프레임·속도의 좌우 쌍 중점을 사용해 수명 시계 없이 중앙 생성 위치 확인
        Require(boss.CentralRowCount == 6 && rows.Length == 12 && boss.CentralBulletLifetime == 0f, "Central slam must produce six lifetime-free left/right pairs exactly once.");
        var left = new NormalProjectile[boss.CentralRowCount];
        var right = new NormalProjectile[boss.CentralRowCount];
        float half = (boss.CentralRowCount - 1) * 0.5f;
        foreach (NormalProjectile projectile in rows)
        {
            Vector2 position = projectile.transform.position;
            int index = Mathf.RoundToInt((position.y - center.y) / boss.CentralRowSpacing + half);
            Require(projectile.Owner == boss && projectile.transform.parent == boss.transform.parent
                && projectile.Speed == boss.CentralBulletSpeed && projectile.Damage == boss.CentralBulletDamage
                && projectile.Lifetime == 0f && projectile.RemainingLifetime == 0f
                && index >= 0 && index < boss.CentralRowCount
                && Mathf.Abs(position.y - (center.y + (index - half) * boss.CentralRowSpacing)) < 0.003f,
                "Each central row bullet must preserve its Owner, configured flight and exact centered vertical origin.");
            if (Vector2.Dot(projectile.Direction, Vector2.left) > 0.9999f)
            {
                Require(left[index] == null, "A central vertical origin must not emit two left bullets.");
                left[index] = projectile;
            }
            else
            {
                Require(Vector2.Dot(projectile.Direction, Vector2.right) > 0.9999f && right[index] == null,
                    "A central vertical origin must emit exactly one right bullet.");
                right[index] = projectile;
            }
        }
        for (int i = 0; i < left.Length; i++)
        {
            Require(left[i] != null && right[i] != null, "Every central vertical origin must emit both directions.");
            Vector2 midpoint = (left[i].transform.position + right[i].transform.position) * 0.5f;
            Require(Mathf.Abs(midpoint.x - center.x) < 0.003f, "Each equally moving left/right pair must share the central X origin.");
        }
    }

    /// <summary>현재 보스 소유의 실제 일반 탄만 Golden과 구분해 조회</summary>
    private static NormalProjectile[] Normals(Boss_01 boss)
    {
        var result = new List<NormalProjectile>();
        foreach (NormalProjectile projectile in UnityEngine.Object.FindObjectsByType<NormalProjectile>(FindObjectsSortMode.None))
            if (projectile.GetType() == typeof(NormalProjectile) && projectile.Owner == boss && projectile.IsActive) result.Add(projectile);
        return result.ToArray();
    }

    /// <summary>활성 총구와 몸통 아래에 있는 공통 실전 검사 위치</summary>
    private static Vector2 Arena(Boss_01 boss) => new Vector2(boss.transform.position.x, boss.Muzzles[2].position.y - 4.125f);

    /// <summary>같은 두 손과 원형 예고가 최초 준비 상태로 돌아왔는지 확인</summary>
    private static bool Home(GuardHandAttack attack, Vector3[] homes)
    {
        return !attack.IsRunning && attack.Phase == GuardHandPhase.Idle && attack.SelectedHandIndex == -1
            && !attack.Warning.gameObject.activeSelf && attack.Warning.FillRenderer.transform.localScale == Vector3.one
            && attack.Hands[0].localPosition == homes[0] && attack.Hands[1].localPosition == homes[1];
    }

    /// <summary>실제 등록된 기능 ID에 대응하는 패턴 객체 조회</summary>
    private static BossPattern Find(BossPattern[] patterns, string id)
    {
        foreach (BossPattern pattern in patterns) if (pattern.PatternId == id) return pattern;
        return null;
    }

    /// <summary>현재 보스가 실제로 발사해 유지 중인 Golden 조회</summary>
    private static GoldProjectile Gold(Boss_01 boss)
    {
        foreach (GoldProjectile projectile in UnityEngine.Object.FindObjectsByType<GoldProjectile>(FindObjectsSortMode.None))
            if (projectile.Owner == boss && projectile.IsActive) return projectile;
        return null;
    }

    /// <summary>같은 실제 GameCamera 결과를 추가 가공 없이 PNG로 저장</summary>
    private static void Capture(string name)
    {
        // 카메라·줌·추적 대상을 바꾸지 않고 현재 월드 출력만 읽음
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
            string path = Path.Combine("Logs/GuardHandVerification", name);
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

    /// <summary>실제 Player와 물리 질의의 위치를 같은 시점으로 갱신</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>현재 활성 씬의 Title 조회</summary>
    private static UI_Title Title() => UnityEngine.Object.FindFirstObjectByType<UI_Title>(FindObjectsInactive.Include);

    /// <summary>실제 프레임의 조건 완료를 제한 시간 안에 기다리기</summary>
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

    /// <summary>게임 정지와 독립적인 Editor 실제 시간 대기</summary>
    private static IEnumerator WaitReal(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    /// <summary>일회 효과 예약과 실제 갱신에 필요한 렌더 프레임 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "hand render frames"); // 실제 예약·상태 갱신 완료 대기
    }

    /// <summary>실제로 성립하지 않은 조건만 검사 실패로 기록</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>실제 통과한 검사 설명만 결과에 추가</summary>
    private static void Passed(string message) => report.checks.Add(message);

    /// <summary>실제 결과를 저장하고 검사와 Play Mode 종료</summary>
    private static void Finish(Exception exception)
    {
        // 연속 촬영과 검사 반복을 닫은 뒤 이번 실행의 실제 결과 보존
        recording = false;
        EditorApplication.update -= Tick; // 완료·실패 뒤 검사 갱신 구독 해제
        report.success = exception == null;
        report.error = exception?.ToString() ?? "";
        report.saveDirectory = SessionState.GetString(SaveKey, "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GuardHandVerification-result.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(SuccessKey, report.success);
        SessionState.SetBool(FinishedKey, true);
        if (exception == null) Debug.Log("[GuardHandChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[GuardHandChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>실제 실행 항목·실패와 캡처 프레임의 게임 시간을 저장할 결과</summary>
    [Serializable]
    private class Report
    {
        public bool success; // 모든 실제 검사 조건이 통과했는지 결과
        public List<string> checks = new List<string>(); // 실제 통과한 검사 묶음
        public List<double> frameTimes = new List<double>(); // 연속 PNG를 촬영한 실제 게임 시각
        public List<string> framePhases = new List<string>(); // 각 연속 PNG에서 관찰한 실제 손 단계
        public List<string> framePatterns = new List<string>(); // 각 연속 PNG의 한 손·중앙 패턴 식별자
        public string error; // 실패 시 실제 예외 전체 내용
        public string saveDirectory; // 사용자 저장과 분리한 검사 세션 위치
    }
}
