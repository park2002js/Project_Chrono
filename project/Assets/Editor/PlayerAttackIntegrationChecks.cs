using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>확정 R14 무기·검기와 실제 공격 입력·시간·판정을 별도 Play Mode에서 검사</summary>
[InitializeOnLoad]
public static class PlayerAttackIntegrationChecks
{
    private const string ActiveKey = "Chrono.AttackIntegration.Active"; // 재로드 뒤 이번 공격 검사만 이어갈 실행 키
    private const string FinishedKey = "Chrono.AttackIntegration.Finished"; // Play 종료 뒤 배치 종료를 결정할 완료 키
    private const string SuccessKey = "Chrono.AttackIntegration.Success"; // 배치 종료 코드에 전달할 성공 여부 키
    private const string DashKey = "Chrono.AttackIntegration.Dash"; // 기존 실행기를 대시 전용 검사로 사용할 분기 키
    private const string CameraKey = "Chrono.AttackIntegration.Camera"; // 기존 실행기를 카메라 전용 검사로 사용할 분기 키
    private const string FeverKey = "Chrono.AttackIntegration.Fever"; // 기존 실행기를 각성 오라 검사로 사용할 분기 키
    private const string TutorialKey = "Chrono.AttackIntegration.Tutorial"; // 슬롯 선택 전부터 최초 입장 잠금을 관찰할 분기 키
    private const string EndingKey = "Chrono.AttackIntegration.Ending"; // 경비 사망부터 임시 엔딩·Title 복귀만 검사할 분기 키
    private const string ReportPathKey = "Chrono.AttackIntegration.ReportPath"; // 대시 결과가 기존 공격 결과를 덮지 않도록 구분할 경로 키
    private static readonly BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; // 검사에서 실제 내부 경과시간·표현 경계를 관찰할 조회 범위
    private static Stack<IEnumerator> routines; // 중첩 대기를 포함한 실제 Play 검사 실행 스택
    private static Report report; // 이번 검사 항목과 오류의 독립 결과
    private static double deadline; // 무한 대기 없이 종료할 Editor 시각
    private static bool captureWarmed; // 첫 수동 렌더에서 GPU 준비를 마쳤는지 확인할 표시

    /// <summary>Play 진입·종료 때 이 검사의 실행 상태만 복원</summary>
    static PlayerAttackIntegrationChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged; // 도메인 재로드 뒤 공격 검사 실행·종료 연결
    }

    /// <summary>사용자 저장과 분리한 Title에서 공격 통합 검사 시작</summary>
    [MenuItem("Tools/Chrono/Check Confirmed Player Attacks")]
    public static void Run()
    {
        RunSession(false);
    }

    /// <summary>기존 격리 Play 실행기로 대시 검사만 시작</summary>
    public static void RunDash()
    {
        RunSession(true);
    }

    /// <summary>같은 실제 Title 준비·종료 경로로 카메라 검사만 시작</summary>
    public static void RunCamera()
    {
        RunSession(false, true);
    }

    /// <summary>같은 Title·저장 격리 경로로 각성 오라 검사만 시작</summary>
    public static void RunFever()
    {
        RunSession(false, false, true);
    }

    /// <summary>실제 새 슬롯의 최초 입장부터 저장·중단·재시작까지 검사</summary>
    public static void RunTutorial()
    {
        RunSession(false, false, false, true);
    }

    /// <summary>기존 격리 저장·Play 실행기로 임시 엔딩만 확인</summary>
    public static void RunEnding()
    {
        RunSession(false, ending: true);
    }

    /// <summary>검사 종류만 분리하고 같은 실제 Title 준비 경로 사용</summary>
    private static void RunSession(bool dash, bool camera = false, bool fever = false, bool tutorial = false, bool ending = false)
    {
        // 실행 중인 Play와 편집 씬을 건드리지 않고 독립 검증 준비
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Attack integration checks must start outside Play Mode.");
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run the attack integration check in its isolated batch project.");
        Directory.CreateDirectory("Logs/AttackIntegration");
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(manager != null, "The isolated Title must own GameManager.");
        manager.SaveDirectory = Path.GetFullPath(Path.Combine("Temp", "AttackIntegrationSaves", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(manager.SaveDirectory);
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetBool(DashKey, dash);
        SessionState.SetBool(CameraKey, camera);
        SessionState.SetBool(FeverKey, fever);
        SessionState.SetBool(TutorialKey, tutorial);
        SessionState.SetBool(EndingKey, ending);
        SessionState.SetString(ReportPathKey, ending ? "Logs/TempDemoEnding-result.json" : tutorial ? "Logs/TutorialEntry-result.json" : fever ? "Logs/FeverAura-result.json" : camera ? "Logs/PlayerCameraFollow-result.json"
            : dash ? "Logs/PlayerDashIntegration-result.json" : "Logs/PlayerAttackIntegration-result.json");
        EditorApplication.EnterPlaymode();
    }

    /// <summary>격리본에최종프리팹을연결한뒤같은공격통합검사시작</summary>
    public static void ApplyAndRun()
    {
        PlayerArtSetup.Apply();
        AssetDatabase.SaveAssets();
        Run();
    }

    /// <summary>기존 프리팹에 피드백만 최소 연결한 뒤 현재 공격 검사 실행</summary>
    public static void ApplyFeedbackAndRun()
    {
        PlayerFeedbackSetup.Apply();
        Run();
    }

    /// <summary>독립 검사만 Play 상태 변화에 반응하도록 실행·종료 분리</summary>
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // 다른 Editor 검사와 SessionState를 공유하지 않고 이 요청만 소비
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(CheckSession()); // 실제 Title·Player 생성 이후 공격 흐름 검사 실행
            deadline = EditorApplication.timeSinceStartup + 180d;
            EditorApplication.update += Tick; // Editor 프레임마다 실제 게임 상태와 대기 조건 확인
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
        }
    }

    /// <summary>중첩 IEnumerator를 진행하고 실제 프레임 대기와 오류를 보고</summary>
    private static void Tick()
    {
        try
        {
            // 정해진 시각 안에 실행된 검사만 완료로 처리
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Attack integration checks exceeded 180 seconds.");
            while (routines.Count > 0)
            {
                IEnumerator current = routines.Peek();
                if (!current.MoveNext())
                {
                    routines.Pop();
                    (current as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>실제 슬롯 선택으로 준비한 Player에서 공격 검사와 씬 정리 수행</summary>
    private static IEnumerator CheckSession()
    {
        // Title과 저장 준비가 끝난 실제 세션으로 진입
        yield return Until(() => GameManager.Instance != null && GetTitle() != null && GetTitle().IsReady, "Title readiness"); // 서비스·Title 슬롯 로드 완료 대기
        GameManager manager = GameManager.Instance;
        if (SessionState.GetBool(EndingKey, false))
        {
            yield return TempDemoEndingChecks.Run(manager, Passed); // 경비 처치·중단·재연결·엔딩의 전용 검사 완료 대기
            yield break;
        }
        if (SessionState.GetBool(TutorialKey, false))
        {
            yield return TutorialEntryChecks.Run(manager, Passed); // 최초 슬롯 선택·입장·재진입·Title 정리의 전용 검사 완료 대기
            yield break;
        }
        GetTitle().SelectSlot(0);
        yield return Until(() => manager.Stage != null && manager.Stage.IsReady && manager.CurrentPlayer != null && !manager.IsLoading, "MainWorld readiness"); // 실제 맵·Player·생성 모션 준비 완료 대기
        Passed("Title selection prepares the connected Player and attack components");
        if (SessionState.GetBool(FeverKey, false))
        {
            yield return FeverAuraChecks.Run(manager, Passed); // 기존 Buff와 실제 오라·Draw·생명주기·화면 검사 완료 대기
            manager.ReturnToTitle();
            yield return Until(() => GetTitle() != null && GetTitle().IsReady && !manager.IsLoading, "Fever Title return"); // 각성 검사 세션의 Player·카메라·입자 해제 대기
            Require(manager.CurrentPlayer == null && GameCamera.Instance == null
                && UnityEngine.Object.FindObjectsByType<FeverAuraFx>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Title return must release the Player and every scene-owned Fever aura.");
            Passed("Title return releases the Player, cached camera and all Fever aura emitters");
            yield break;
        }
        if (SessionState.GetBool(CameraKey, false))
        {
            yield return DrawCameraChecks.RunFollow(manager, Passed); // 현행 카메라의 접근 시간·컷씬·즉시 배치 검사 완료 대기
            manager.ReturnToTitle();
            yield return Until(() => GetTitle() != null && GetTitle().IsReady && !manager.IsLoading, "Camera Title return"); // 카메라 검사 세션의 Player·카메라 정리 대기
            Require(manager.CurrentPlayer == null && GameCamera.Instance == null, "Camera verification must release its Player and world camera.");
            Passed("Camera verification returns to Title and releases the cached scene camera");
            yield break;
        }
        if (SessionState.GetBool(DashKey, false))
        {
            yield return PlayerDashIntegrationChecks.Check(manager, Passed); // 대시와 실바닥 표현의 표적 검사 완료 대기
            manager.ReturnToTitle();
            yield return Until(() => GetTitle() != null && GetTitle().IsReady && !manager.IsLoading, "Dash Title return"); // 대시 검사 세션의 Player·카메라 해제 대기
            Require(manager.CurrentPlayer == null && GameCamera.Instance == null, "Dash verification must release its isolated Player and world camera.");
            Passed("Dash verification returns to Title and releases its isolated Player and world camera");
            yield break;
        }
        yield return CheckPlayer(manager); // 현재 프리팹의 모션·입력·시간·판정 검사 완료 대기

        // 공격 중 세션을 닫아 이전 Player와 검기가 Title에 남지 않는지 확인
        PlayerCharacter player = manager.CurrentPlayer;
        AttackController attack = player.GetComponent<AttackController>();
        PlayerInputController input = PlayerInputController.Instance;
        PlayerFeedback feedback = player.Feedback;
        AudioSource feedbackSource = feedback.Source;
        input.enabled = false;
        player.SetAimPosition((Vector2)player.transform.position + Vector2.up * 2f);
        Require(player.ApplyAttack(), "An attack must be active before Title cleanup.");
        yield return Until(() => attack.AttackVfxRenderer.enabled && attack.ActiveHitWindows > 0, "active attack before GameObject disable"); // 실제 검기와 판정이 열린 공격을 비활성화할 준비 대기
        feedback.PlayImpact((Vector2)player.transform.position + Vector2.up, 0f);
        player.gameObject.SetActive(false);
        Require(!attack.IsAttacking && !attack.HasBufferedAttack && attack.ActiveHitWindows == 0 && NoAttackFx(player)
            && ActiveImpactCount(feedback) == 0 && !feedbackSource.isPlaying,
            "GameObject disable must clear attack, hit spark, feedback audio, buffered click and hit windows.");
        player.gameObject.SetActive(true);
        yield return Frames(1); // 자식 Animator를 포함한 재활성화와 대기 상태 복구 완료 대기
        Require(!attack.IsAttacking && !attack.HasBufferedAttack && attack.ActiveHitWindows == 0 && NoAttackFx(player)
            && ActiveImpactCount(feedback) == 0 && feedback.Source == feedbackSource && !feedbackSource.isPlaying
            && Mathf.Abs(attack.WeaponAnimator.speed - 1f) < 0.00001f && attack.WeaponAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"),
            "Reactivation must restore idle at speed1 without an old hit, buffered input or VFX.");
        Require(player.ApplyAttack(), "A reactivated Player must be able to start a fresh attack.");
        manager.ReturnToTitle();
        yield return Until(() => GetTitle() != null && GetTitle().IsReady && !manager.IsLoading, "Title return"); // 실제 공격·맵·공유 입력 연결 해제 완료 대기
        Require(manager.CurrentPlayer == null && GameCamera.Instance == null && feedback == null && feedbackSource == null
            && UnityEngine.Object.FindObjectsByType<PlayerFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
            "Returning to Title must release the Player, feedback source, hit sparks and world camera.");
        Passed("GameObject reactivation restores idle and cached feedback without stale sparks or sound, then Title releases Player and all effects");
    }

    /// <summary>비활성화·재활성화 경계에서 모든 공격 검기가 꺼졌는지 확인</summary>
    private static bool NoAttackFx(PlayerCharacter player)
    {
        foreach (SpriteRenderer renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.gameObject.name.StartsWith("AttackVfx", StringComparison.Ordinal) && renderer.enabled) return false;
        return true;
    }

    /// <summary>확정 공격의 실제 연결과 입력 흐름을 같은 Player에서 검사</summary>
    private static IEnumerator CheckPlayer(GameManager manager)
    {
        // 테스트 도중 불필요한 조우와 입력만 멈추고 실제 Player Loop 유지
        PlayerCharacter player = manager.CurrentPlayer;
        AttackController attack = player.GetComponent<AttackController>();
        PlayerInputController input = PlayerInputController.Instance;
        PlayerSpawner spawner = player.Spawner;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        bool inputEnabled = input.enabled;
        bool spawnerEnabled = spawner.enabled;
        float previousCapture = Time.captureDeltaTime;
        float previousSpeed = player.SP;
        float previousAttackSpeed = player.AS;
        Vector2 previousPosition = body.position;
        input.enabled = false;
        spawner.enabled = false;
        player.ResetMotion();
        SetPosition(body, new Vector2(40f, 40f));
        Time.captureDeltaTime = 1f / 120f;
        try
        {
            // 모든 세 공격이 실제 Controller와 Renderer를 갖춘 상태인지 확인
            Require(attack != null && attack.WeaponAnimator != null && attack.WeaponMotion != null,
                "The Player must connect its attack Animator and weapon pivot.");
            Require(Mathf.Abs(attack.ComboDuration - 1f / 6f) < 0.00001f && attack.AttackCooldown == 0f,
                "Reference combo validity must be one sixth of a game second with no extra attack cooldown.");
            Require(attack.AttackMoveMultiplier == 0f, "Normal movement input must be blocked during a greatsword attack.");
            Passed("Reference combo validity, zero added cooldown and blocked input movement are configured");
            Require(Mathf.Abs(player.basicSP - 4.4f) < 0.0001f && Mathf.Abs(player.SP - 4.4f) < 0.0001f,
                "The connected Player must use the reference base movement speed4.4.");
            CheckWeaponPoses(player, attack);
            CheckVfx(player, attack);
            yield return CheckRuntime(player, attack, body); // 실제 공격 입력·속도·효과 경계 검사 완료 대기
            yield return CheckAttackSnapshot(player, attack, body); // 공격 시작 입력의 전진 배율·방향과 다음 타 독립성 검사 대기
            yield return CheckFeedback(manager, player, attack, body); // 실제 재생 알림·성공 피해·걷기 거리와 효과 수명 검사 대기
            yield return CheckActionInput(player, attack, input, body); // 실제 Input System 클릭·해제·홀드의 단일 통합 검사 대기
        }
        finally
        {
            // 이 검사가 바꾼 수치·위치·시간·연결 상태만 복구
            player.ResetMotion();
            player.SP = previousSpeed;
            player.AS = previousAttackSpeed;
            SetPosition(body, previousPosition);
            input.enabled = inputEnabled;
            spawner.enabled = spawnerEnabled;
            Time.captureDeltaTime = previousCapture;
        }
    }

    /// <summary>실제 입력·판정의 상세 검사를 같은 시점 원본으로 진행</summary>
    private static IEnumerator CheckRuntime(PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 초기 입력 게이트와 버퍼는 구현 계약이 연결된 뒤 실제 Player API로 검사
        player.SetAttackInput(false);
        player.SetAimPosition(body.position + Vector2.up * 2f);
        Require(player.ApplyAttack(), "The first direct input must start an attack.");
        Require(Read<float>(attack, "CurrentAttackDuration") > 0f, "Attack duration must be available for the current combo.");
        Require(attack.ComboIndex == 0 && !Read<bool>(attack, "IsAttackInputAllowed"), "The first attack must begin with its input ignore interval closed.");
        player.SetAttackInput(true);
        Require(!player.ApplyAttack(), "The initial ignored click must be rejected by the actual Character press API.");
        player.SetAttackInput(false);
        Require(!Read<bool>(attack, "HasBufferedAttack"), "A press inside the initial ignore interval must not create a buffered attack.");
        yield return Until(() => !attack.IsAttacking, "initial swing completion"); // 무시한 초기 입력이 다음 타를 예약하지 않는지 첫 공격 종료 대기
        yield return Frames(2); // 마지막 공격·입력 Update가 추가 예약을 만들지 않는지 관찰
        Require(!attack.IsAttacking && attack.ComboIndex == 0, "An ignored early press must not play a delayed second attack.");
        Passed("A press inside the initial ignore interval is discarded without a delayed buffered swing");
        player.CancelAttack();
        yield return CheckTimingAndMovement(player, attack, body); // AS·콤보 유효시간·입력 이동 차단·강제 전진 검사 완료 대기
        yield return CheckBufferedInput(player, attack, body); // 실제 무기 요청의 버퍼 허용과 클릭 해제 보존 검사 완료 대기
        yield return CheckHitLifetime(player, attack, body); // AS와 별개인 판정 수명과3타 중복 피해 검사 완료 대기
    }

    /// <summary>실제재생시간·원작입력이동차단과별도강제전진대조</summary>
    private static IEnumerator CheckTimingAndMovement(PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 각 타의 기본 길이와 AS2 실제 게임 시간의 절반 재생 확인
        float[] durations = { 0.6f, 22f / 30f, 53f / 60f };
        float speed = player.SP;
        player.SP = 0f;
        for (int combo = 0; combo < 3; combo++)
        {
            foreach (float scale in new[] { 1f, 2f })
            {
                player.AS = scale;
                BeginCombo(player, attack, combo, Vector2.up);
                Require(Mathf.Abs(attack.CurrentAttackDuration - durations[combo]) < 0.0001f,
                    "The connected clip duration must preserve combo" + (combo + 1));
                float start = Time.time;
                yield return Until(() => !attack.IsAttacking, "combo duration " + combo + " AS" + scale); // 실제 공격 완료까지 게임 시간 측정
                float elapsed = Time.time - start;
                Require(Mathf.Abs(elapsed - durations[combo] / scale) <= Time.deltaTime * 2.1f + 0.003f,
                    "Attack duration differs: combo=" + combo + ", AS=" + scale + ", actual=" + elapsed);
                Require(attack.AttackCooldownRemaining == 0f, "Reference attacks must not add an idle cooldown after their clip.");
            }
        }
        Passed("All three real swing durations preserve source clip time and scale with AS while adding no cooldown");

        // 콤보 대기는 빠른 공격속도에서도 게임 시간1/6초를 유지
        player.AS = 2f;
        BeginCombo(player, attack, 0, Vector2.up);
        yield return Until(() => !attack.IsAttacking, "combo timeout setup"); // 빠른 첫 타의 종료 경계 대기
        float remaining = attack.ComboRemaining;
        float began = Time.time;
        Require(remaining > 0.14f && remaining <= 1f / 6f, "Combo validity must begin near one sixth of a game second.");
        yield return Until(() => attack.ComboIndex == -1, "combo expiration"); // AS 비적용 콤보 만료까지 게임 시간 측정
        Require(Mathf.Abs(Time.time - began - remaining) <= Time.deltaTime * 2.1f + 0.003f,
            "AS2 must not halve the combo input validity interval.");
        Passed("Combo validity remains one sixth of a game second at AS2 and expiry resets the next attack to1");

        // 원래 이동속도 입력은 타격 전 준비 동안 정지하며 별도 전진만 실제 위치에 반영
        player.AS = 1f;
        player.SP = speed;
        player.CancelAttack();
        SetPosition(body, new Vector2(40f, 40f));
        player.SetMoveInput(Vector2.right);
        BeginCombo(player, attack, 0, Vector2.up);
        Vector2 origin = body.position;
        yield return Until(() => attack.AttackElapsed >= 0.15f, "attack windup movement"); // 타격 전 순수 입력 이동 차단 구간 관찰
        Require(Vector2.Distance(origin, body.position) < 0.0001f, "Normal movement input must produce no displacement during attack windup.");
        player.SetMoveInput(Vector2.left);
        player.SetAimPosition(body.position + Vector2.down * 3f);
        yield return Until(() => attack.AttackElapsed > attack.HitWindowStart + 0.01f, "attack dash direction capture"); // 중간 입력·커서 변경과 무관하게 처음 오른쪽 방향으로 전진할 때까지 대기
        player.SetMoveInput(Vector2.zero);
        yield return Until(() => !attack.IsAttacking, "attack forward movement"); // 타격 이벤트 뒤 AS 독립 강제 전진의 종료 대기
        yield return GameSeconds(0.12f); // 공격 끝보다 조금 늦게 끝나는0.3초 전진을 모두 소비할 때까지 대기
        player.SetMoveInput(Vector2.zero);
        Vector2 displacement = body.position - origin;
        Require(Mathf.Abs(displacement.y) < 0.002f && Mathf.Abs(displacement.x - 0.5f) < 0.025f,
            "Attack dash must preserve its start input despite later key/cursor changes and integrate to0.5, actual=" + displacement);
        player.CancelAttack();
        Passed("Attack windup blocks normal movement and advances0.5 along its start input despite later key and cursor changes");
    }

    /// <summary>입력 유무와 무관한 전진 거리와 AS2에서 이전 전진·다음 시작 입력의 분리 검사</summary>
    private static IEnumerator CheckAttackSnapshot(PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 검사용 위치·속도만 사용하고 세 공격의 무입력 전진을 각각 관측
        float speed = player.SP;
        float attackSpeed = player.AS;
        Require(Mathf.Abs(attack.AttackDashSpeed - 10f / 3f) < 0.00001f && Mathf.Abs(attack.AttackDashDuration - 0.3f) < 0.00001f,
            "Every attack must use the shared speed10/3 and duration.3 for distance0.5.");
        try
        {
            player.SP = player.basicSP;
            player.AS = 1f;
            for (int combo = 0; combo < 3; combo++)
            {
                player.ResetMotion();
                SetPosition(body, new Vector2(40f, 40f));
                BeginCombo(player, attack, combo, Vector2.up);
                Vector2 origin = body.position;
                yield return Until(() => attack.AttackElapsed >= 0.1f, "stationary windup" + combo); // 타격 전 무입력 시작 상태의 조작 변경 시점 대기
                player.SetMoveInput(Vector2.right);
                player.SetAimPosition(body.position + Vector2.right * 3f);
                yield return Until(() => !attack.IsAttacking, "stationary attack finish" + combo); // 중간에 입력을 넣어도 처음 조준 방향·공통 거리 유지 관찰
                player.SetMoveInput(Vector2.zero);
                yield return GameSeconds(0.12f); // 공격 종료 뒤 남은 고정 전진 시간까지 소비 대기
                Vector2 displacement = body.position - origin;
                Require(Mathf.Abs(displacement.x) < 0.002f && Mathf.Abs(displacement.y - 0.5f) < 0.015f,
                    "Stationary combo" + (combo + 1) + " must retain its start aim and advance0.5 after later move/cursor input, actual=" + displacement);
            }
            Passed("All three stationary attacks advance0.5 along their start cursor even when movement and cursor change before the hit event");

            // 시작 직후 이동 키를 떼어도 원래 이동 전진 거리는 유지
            for (int combo = 0; combo < 3; combo++)
            {
                player.ResetMotion();
                SetPosition(body, new Vector2(40f, 40f));
                player.SetMoveInput(Vector2.right);
                BeginCombo(player, attack, combo, Vector2.up);
                player.SetMoveInput(Vector2.zero);
                yield return Until(() => !attack.IsAttacking, "released moving attack finish" + combo); // 타격 전에 이동 입력을 해제한 공격의 모션 종료 대기
                yield return GameSeconds(0.12f); // 해제와 무관하게 남은 전체 전진 거리 적용 대기
                Require(Vector2.Distance(body.position, new Vector2(40.5f, 40f)) < 0.015f,
                    "Moving combo" + (combo + 1) + " must retain shared distance0.5 and start direction after release.");
            }
            Passed("All three moving attacks retain their original direction and shared0.5 distance when movement is released during windup");

            // 다음 타의 새 무입력 스냅샷이 이전 타의 진행 중 전진을 덮지 않는지 확인
            player.ResetMotion();
            player.AS = 2f;
            SetPosition(body, new Vector2(40f, 40f));
            player.SetMoveInput(Vector2.right);
            BeginCombo(player, attack, 0, Vector2.up);
            player.SetMoveInput(Vector2.zero);
            yield return Until(() => !attack.IsAttacking, "AS2 previous advance remains"); // 모션이 먼저 끝나도 첫 오른쪽 전진이 남아있는 경계 대기
            player.SetAimPosition(body.position + Vector2.up * 3f);
            Require(player.ApplyAttack() && attack.ComboIndex == 1, "AS2 second attack must begin with a new stationary snapshot.");
            Vector2 before = body.position;
            yield return Frames(3); // 다음 타 타격 전까지 이전 오른쪽 전진이 계속되는지 관찰
            Vector2 tail = body.position - before;
            Require(tail.x > 0.01f && Mathf.Abs(tail.y) < 0.002f,
                "The next stationary snapshot must preserve the previous active rightward advance, actual=" + tail);
            player.SetMoveInput(Vector2.left);
            yield return Until(() => attack.AttackElapsed >= 7f / 30f, "AS2 second hit advance"); // 새 타격 이벤트에서만 두 번째 전진으로 바뀌는 시각 대기
            Vector2 secondOrigin = body.position;
            player.SetMoveInput(Vector2.zero);
            yield return Until(() => !attack.IsAttacking, "AS2 second attack completion"); // 두 번째 모션 종료 뒤 남은 전진을 검사할 준비 대기
            yield return GameSeconds(0.15f); // 두 번째 고정.3초 전진이 끝날 때까지 대기
            Vector2 second = body.position - secondOrigin;
            Require(Mathf.Abs(second.x) < 0.002f && Mathf.Abs(second.y - 0.5f) < 0.015f,
                "The next AS2 attack must begin its own shared-distance upward advance only at the hit event, actual=" + second);
            Passed("AS2 preserves the previous moving attack's residual advance until the next hit, then uses that attack's own stationary start snapshot");
        }
        finally
        {
            // 이후 기존 콤보·판정 검사에 새 스냅샷이나 이동 의도를 남기지 않음
            player.ResetMotion();
            player.SP = speed;
            player.AS = attackSpeed;
        }
    }

    /// <summary>허용뒤클릭버퍼와3타의확정검기종료경계대조</summary>
    private static IEnumerator CheckBufferedInput(PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 처음 무시 구간이 끝난 새 클릭은 해제 뒤에도 다음 타 한 번으로 소비
        float speed = player.SP;
        player.SP = 0f;
        player.AS = 1f;
        BeginCombo(player, attack, 0, Vector2.up);
        yield return Until(() => attack.AttackElapsed >= 0.06f, "first input acceptance"); // 원작1/30초 입력 무시 구간 종료 대기
        Require(player.ApplyAttack() && attack.HasBufferedAttack, "An accepted click during attack1 must reserve attack2.");
        player.SetAttackInput(false);
        yield return Until(() => attack.IsAttacking && attack.ComboIndex == 1, "buffered second attack"); // 버튼 해제 후 예약한 두 번째 타의 실제 시작 대기
        Require(!attack.HasBufferedAttack, "The buffered click must be consumed once at the next swing.");
        yield return Until(() => !attack.IsAttacking, "buffered second completion"); // 한 번 예약한 타의 종료 대기
        yield return Frames(2); // 예약 한 번으로 세 번째 타까지 반복하지 않는지 관찰
        Require(!attack.IsAttacking && attack.ComboIndex == 1, "One released buffered click must not repeat a third swing.");
        Passed("A click after the ignore interval survives release and starts exactly one next combo attack");

        // 새3타는 검기 종료35번째까지 입력을 버리고36번째부터 다음1타 예약 허용
        BeginCombo(player, attack, 2, Vector2.up);
        Require(Mathf.Abs(attack.AttackInputOpenTime - 35f / 60f) < 0.00001f,
            "Attack3's inferred input opening must follow the confirmed VFX final frame35.");
        yield return Until(() => attack.AttackElapsed >= 0.2f, "third early input"); // 두 번 휘두르는3타의 이른 클릭 시점 대기
        Require(!player.ApplyAttack() && !attack.HasBufferedAttack, "Attack3 must ignore an early click before VFX completion.");
        yield return Until(() => attack.IsAttackInputAllowed, "third input opening"); // 검기가 끝난 뒤 첫 입력 허용 경계 대기
        Require(player.ApplyAttack() && attack.HasBufferedAttack, "Attack3 must buffer a click after its VFX completes.");
        yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "third buffered restart"); // 마지막 회복 뒤 새1타가 예약으로 시작할 때까지 대기
        player.CancelAttack();
        player.SP = speed;
        Passed("Attack3 ignores early input until frame36 and a late buffered click restarts attack1 only after recovery");
    }

    /// <summary>AS2공격교체뒤잔존판정과3타수신자당1회피해대조</summary>
    private static IEnumerator CheckHitLifetime(PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 전진을 제외하고 서로 다른 방향의 판정 수명만 실제 충돌로 확인
        float speed = player.SP;
        float damage = player.AD;
        player.SP = 0f;
        player.AD = 10f;
        player.AS = 2f;
        SetPosition(body, new Vector2(50f, 50f));
        BeginCombo(player, attack, 0, Vector2.up);
        yield return Until(() => !attack.IsAttacking, "AS2 first recovery"); // 첫 판정0.15초가 아직 끝나지 않은 공격 모션 종료 대기
        Require(Read<int>(attack, "ActiveHitWindows") > 0, "AS2 must retain the first hit window after the0.3-second clip ends.");
        player.SetAimPosition(body.position + Vector2.right * 2f);
        Require(player.ApplyAttack() && attack.ComboIndex == 1, "The second attack must start inside combo validity with no added cooldown.");
        Character late = CreateTarget("OldHitWindowTarget", body.position + Vector2.up * attack.HitboxOffset.x, 1);
        yield return Until(() => late.HP < 1000f, "previous hit after second start"); // 새 타가 시작된 뒤 들어온 적이 이전 방향의 살아있는 판정에 맞을 때까지 대기
        Require(late.HP == 990f && attack.AttackElapsed < 0.1f,
            "The original up-facing hit must remain alive before the new right-facing hit is generated.");
        player.CancelAttack();
        UnityEngine.Object.Destroy(late.gameObject);
        Passed("AS2 preserves the original0.15-game-second hit window and direction across the next combo start");

        // 두 검기가 교차해도같은적의복수Collider에는강화피해한번만적용
        player.AS = 1f;
        SetPosition(body, new Vector2(60f, 60f));
        Character target = CreateTarget("Attack3SingleDamage", body.position + Vector2.up * attack.HitboxOffset.x, 3);
        int applications = 0;
        int impactSounds = 0;
        Action<DamageInstance> damaged = value => applications++;
        Action<PlayerSoundCue, AudioClip> played = (cue, clip) => { if (cue == PlayerSoundCue.Hit) impactSounds++; };
        target.OnDamaged += damaged; // 두시각휘두르기의실제피해적용횟수수집
        player.Feedback.OnSoundPlayed += played; // 실제3타의 성공 수신자당 피격음 횟수 수집
        try
        {
            BeginCombo(player, attack, 2, Vector2.up);
            foreach (int frame in new[] { 23, 25, 26, 30, 33 })
            {
                float at = (frame - 1 + 0.25f) / 60f;
                yield return Until(() => attack.AttackElapsed + 0.000001f >= at, "third rendered frame" + frame); // 실제3타의확정검기표본시각까지대기
                CaptureWorld("attack3-frame" + frame);
            }
            yield return Until(() => !attack.IsAttacking, "third damage completion"); // 확정3타의전체시각모션과단일판정종료대기
            Require(Mathf.Abs(target.HP - (1000f - 10f * attack.ThirdHitMultiplier)) < 0.0001f && applications == 1 && impactSounds == 1,
                "Attack3 must apply one stronger damage and one impact sound despite two visual swings and three target colliders.");
            Passed("Attack3 applies exactly one stronger damage and one impact sound to a receiver with three colliders");
        }
        finally
        {
            target.OnDamaged -= damaged; // 임시수신자의피해횟수수집해제
            player.Feedback.OnSoundPlayed -= played; //3타 피격음 횟수 수집 해제
            UnityEngine.Object.Destroy(target.gameObject);
            player.CancelAttack();
            player.SP = speed;
            player.AD = damage;
            player.AS = 1f;
        }
    }

    /// <summary>실제 효과음 호출과 피해·충돌·걷기 경계를 같은 Player에서 검사</summary>
    private static IEnumerator CheckFeedback(GameManager manager, PlayerCharacter player, AttackController attack, Rigidbody2D body)
    {
        // 자기 소리 관측 구독과 검사 뒤 복구할 수치 보관
        PlayerFeedback feedback = player.Feedback;
        Require(feedback != null && feedback.Source != null && feedback.Source != player.GetComponent<AudioSource>()
            && feedback.Source.transform.IsChildOf(player.transform), "Feedback must cache its own child audio source separately from Slash.");
        FeedbackReport evidence = new FeedbackReport();
        float speed = player.SP;
        float attackSpeed = player.AS;
        float step = feedback.StepDistance;
        Action<PlayerSoundCue, AudioClip> sound = (cue, clip) => evidence.sounds.Add(new SoundSample
        { cue = cue, clip = clip, path = AssetDatabase.GetAssetPath(clip), gameTime = Time.timeAsDouble, attackTime = attack.AttackElapsed });
        feedback.OnSoundPlayed += sound; // 실제 PlayOneShot 직후의 종류·클립·게임 시각 수집
        try
        {
            // 가져온 여섯 PCM과 일곱 피격 이미지가 실제 프리팹에 연결됐는지 확인
            Require(feedback.FootstepClips != null && feedback.FootstepClips.Length == 3, "Feedback must connect three tile sound variants.");
            AudioClip[] clips = { feedback.Attack1Clip, feedback.Attack2Clip, feedback.HitClip,
                feedback.FootstepClips[0], feedback.FootstepClips[1], feedback.FootstepClips[2] };
            Require(feedback.HitFrames != null && feedback.HitFrames.Length == 7 && feedback.HitMaterial != null
                && feedback.StepDistance > 0f && feedback.Attack1Clip != feedback.Attack2Clip, "Feedback must connect two distinct swings, hit, tile variants and seven hit sprites.");
            foreach (AudioClip clip in clips) { Require(clip != null, "Every feedback audio role must have a real clip."); clip.LoadAudioData(); }
            yield return Until(() => Array.TrueForAll(clips, clip => clip.loadState == AudioDataLoadState.Loaded), "feedback PCM loading"); // Unity 오디오 데이터의 실제 로드 완료 대기
            foreach (AudioClip clip in clips)
            {
                float[] pcm = new float[clip.samples * clip.channels];
                Require(clip.samples > 0 && clip.frequency > 0 && clip.GetData(pcm, 0), "Feedback PCM must be readable: " + clip.name);
                float peak = 0f;
                foreach (float value in pcm) peak = Mathf.Max(peak, Mathf.Abs(value));
                Require(peak > 0.000001f, "Feedback PCM must contain non-silent samples: " + clip.name);
            }
            foreach (Sprite frame in feedback.HitFrames) Require(frame != null && frame.rect.width > 0 && frame.rect.height > 0, "Each hit frame must be a real imported sprite.");
            yield return CheckSwingSounds(manager, player, attack, body, feedback, evidence); // 두 공격속도의 실제 A·B·A→B와 취소·정지 검사 대기
            Passed("Imported feedback PCM is non-silent and real swing events play A/B/A-then-B at both attack speeds, with pause and cancellation gates");
            yield return CheckImpactSuccess(player, body, feedback, evidence); // 성공 피해의 중복 제거·거부·취소 재진입과 Cast 접점 검사 대기
            Passed("Successful damage produces one impact and hit sound per receiver; invulnerable, dead and Dodge rejection stay silent and callback snapshots survive cancellation");
            yield return CheckImpactVisuals(manager, player, body, feedback, evidence); // 피격 섬광 방향·반경·월드 고정·7프레임 수명 검사 대기
            Passed("Hit sparks keep the reference perpendicular angle and random bounds, stay world-fixed, show seven20Hz frames and expire at.35 game seconds");
            yield return CheckFootsteps(manager, player, body, feedback, evidence); // 실제 일반 이동 거리와 벽·강제 이동·정지·컷씬의 발소리 검사 대기
            Passed("Tile footsteps follow actual normal walking distance and variants, with no wall, dash, attack-forward, idle, paused or cutscene false steps");
            yield return CheckFeedbackClone(player, feedback, evidence); // 복제 Player가 자기 소리·표시만 정리하는지 검사 대기
            Passed("A second prefab owns independent feedback and its removal preserves the original Player, source, effects and cached world bindings");
        }
        finally
        {
            // 관측 구독과 임시 조정값만 복구하고 실제 재생 기록 별도 보관
            feedback.OnSoundPlayed -= sound; // 실제 소리 재생 관측 종료
            manager.SetGameplayPaused(false);
            player.IsCutScene = false;
            player.ResetMotion();
            ClearFeedback(feedback);
            player.SP = speed;
            player.AS = attackSpeed;
            feedback.StepDistance = step;
            File.WriteAllText("Logs/PlayerFeedbackIntegration-metrics.json", JsonUtility.ToJson(evidence, true));
        }
    }

    /// <summary>실제 공격 경과시간으로 콤보 효과음 순서와 AS 적용 확인</summary>
    private static IEnumerator CheckSwingSounds(GameManager manager, PlayerCharacter player, AttackController attack, Rigidbody2D body, PlayerFeedback feedback, FeedbackReport evidence)
    {
        // 일반 이동·피해 없이 공격 소리만 두 배속에서 분리 관측
        player.SP = 0f;
        SetPosition(body, new Vector2(80f, 80f));
        foreach (float scale in new[] { 1f, 2f })
        {
            player.AS = scale;
            for (int combo = 0; combo < 3; combo++)
            {
                player.ResetMotion();
                ClearFeedback(feedback);
                int from = evidence.sounds.Count;
                BeginCombo(player, attack, combo, Vector2.up);
                yield return Until(() => !attack.IsAttacking, "feedback combo" + combo + " AS" + scale); // 실제 클립의 전체 소리 이벤트가 발생할 때까지 대기
                List<SoundSample> played = evidence.sounds.GetRange(from, evidence.sounds.Count - from);
                Require(played.Count == (combo == 2 ? 2 : 1), "Each swing must emit only its designated one or two sound events.");
                Require(played[0].clip == (combo == 1 ? feedback.Attack2Clip : feedback.Attack1Clip)
                    && played[0].cue == (combo == 1 ? PlayerSoundCue.Attack2 : PlayerSoundCue.Attack1), "Swing A/B selection differs from the combo.");
                float expected = combo == 0 ? 11f / 30f : combo == 1 ? 7f / 30f : 22f / 60f;
                Require(Mathf.Abs(played[0].attackTime - expected) <= Time.deltaTime * scale * 2.1f + 0.001f, "First swing audio is outside its actual attack event frame.");
                if (combo == 2)
                {
                    Require(played[1].cue == PlayerSoundCue.Attack2 && played[1].clip == feedback.Attack2Clip
                        && Mathf.Abs(played[1].attackTime - 24f / 60f) <= Time.deltaTime * scale * 2.1f + 0.001f,
                        "Attack3 must play B at its second visual swing.");
                    Require(Math.Abs((played[1].gameTime - played[0].gameTime) - 2d / 60d / scale) <= Time.deltaTime * 2.1d + 0.001d,
                        "Attack3 A-to-B game-time spacing must scale with AS.");
                }
            }
        }

        // 첫 휘두르기 뒤 취소한 세 번째 공격은 두 번째 소리를 남기지 않음
        player.AS = 1f;
        player.ResetMotion();
        ClearFeedback(feedback);
        int start = evidence.sounds.Count;
        BeginCombo(player, attack, 2, Vector2.up);
        yield return Until(() => evidence.sounds.Count > start, "third first swing sound"); // 실제 A 재생 직후 취소할 시점 대기
        player.CancelAttack();
        yield return GameSeconds(0.08f); // 취소하지 않았다면 B가 나왔을 시간까지 관찰
        Require(evidence.sounds.Count == start + 1, "Cancelling attack3 after A must prevent B.");
        BeginCombo(player, attack, 2, Vector2.up);
        yield return Until(() => attack.AttackElapsed > 0.1f, "paused sound windup"); // 소리가 나기 전 정지할 준비 대기
        manager.SetGameplayPaused(true);
        int count = evidence.sounds.Count;
        yield return Frames(4); // 게임 정지 중 실제 렌더 프레임이 지나도 소리가 새로 발생하지 않는지 관찰
        Require(evidence.sounds.Count == count, "Paused attack time must not advance new swing sounds.");
        manager.SetGameplayPaused(false);
        player.ResetMotion();
    }

    /// <summary>실제 피해 승인과 원본 Cast 접점·효과 콜백을 함께 확인</summary>
    private static IEnumerator CheckImpactSuccess(PlayerCharacter player, Rigidbody2D body, PlayerFeedback feedback, FeedbackReport evidence)
    {
        // 같은 수신자의 세 Collider를 여러 번 조회해도 실제 피해·피격음은 한 번
        player.ResetMotion();
        ClearFeedback(feedback);
        Character target = CreateTarget("FeedbackCollisionReceiver", new Vector2(80f, 81f), 3);
        CollisionBox box = new CollisionBox();
        Vector2 point = Vector2.zero;
        float angle = 0f;
        int impacts = 0;
        Action<Vector2, float> hit = (position, rotation) => { point = position; angle = rotation; impacts++; feedback.PlayImpact(position, rotation); };
        try
        {
            int sounds = CountCue(evidence, PlayerSoundCue.Hit);
            box.Begin(new DamageInstance(player, 5f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
            Require(box.Query(new Vector2(80f, 81f), Vector2.one, 37f, hit) == 1, "One receiver with three colliders must receive one successful damage.");
            for (int i = 0; i < 3; i++) box.Query(new Vector2(80f, 81f), Vector2.one, 37f, hit);
            Require(target.HP == 995f && impacts == 1 && CountCue(evidence, PlayerSoundCue.Hit) == sounds + 1,
                "Repeated overlap must emit exactly one successful hit sound and effect.");
            Bounds bounds = TargetBounds(target);
            float contactTolerance = Physics2D.defaultContactOffset + 0.001f;
            evidence.correctedContact = point;
            evidence.contactTargetBounds = bounds;
            Require(Vector2.Distance(bounds.ClosestPoint(point), point) <= contactTolerance,
                "Corrected BoxCast contact must remain on the actual target collider, actual=" + point + ", target=" + bounds);
            bool foundRaw = false;
            foreach (RaycastHit2D result in box.Results)
            {
                if (result.collider == null || !result.collider.TryGetComponent(out Hitbox receiver) || receiver.Owner != target) continue;
                if (Vector2.Distance(result.collider.ClosestPoint(result.point), point) > 0.0001f) continue;
                evidence.rawContact = result.point;
                foundRaw = true;
                break;
            }
            Require(foundRaw, "The forwarded contact must be the receiver's ClosestPoint of an unchanged raw Cast result.");
            SpriteRenderer visual = ActiveImpact(feedback);
            Require(Vector2.Distance(visual.transform.position, point) <= 0.2001f && Mathf.Abs(angle - 37f) < 0.00001f,
                "Successful impact must use its raw contact and original query angle.");

            // 거부된 무적·사망과 실제 Player Dodge에는 성공 표시나 소리가 없음
            int before = impacts;
            target.IsInvulnerable = true;
            box.Begin(new DamageInstance(player, 5f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
            Require(box.Query(new Vector2(80f, 81f), Vector2.one, 0f, hit) == 0 && impacts == before, "Invulnerable damage must not emit feedback.");
            target.IsInvulnerable = false;
            target.IsDead = true;
            box.Begin(new DamageInstance(player, 5f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
            Require(box.Query(new Vector2(80f, 81f), Vector2.one, 0f, hit) == 0 && impacts == before, "Dead targets must not emit feedback.");
            Hitbox playerHitbox = player.GetComponentInChildren<Hitbox>();
            player.DodgeRemaining = 0.1f;
            box.Begin(new DamageInstance(null, 1f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
            Require(box.Query(playerHitbox.Bounds.center, playerHitbox.Bounds.size, 0f, hit) == 0 && impacts == before,
                "Dodge rejection through the actual Player hitbox must remain silent.");
            player.CancelDash();

            // 피해 콜백이 새 질의를 열고 수신자를 옮겨도 이전 성공의 접점은 유지
            target.IsDead = false;
            Vector2 original = target.transform.position;
            Action<DamageInstance> redirect = damage =>
            {
                target.transform.position = new Vector3(900f, 900f, 0f);
                player.CancelAttack();
                box.Begin(new DamageInstance(player, 1f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
                box.Query(new Vector2(950f, 950f), Vector2.one, -70f);
            };
            target.OnDamaged += redirect; // 성공 콜백 안의 취소·다른 질의·대상 이동 재현
            try
            {
                box.Begin(new DamageInstance(player, 5f, DamageType.Normal, DamageInstance.NextAttackId()), ~0);
                Require(box.Query(original, Vector2.one, 21f, hit) == 1 && impacts == before + 1 && Mathf.Abs(angle - 21f) < 0.00001f
                    && Vector2.Distance(bounds.ClosestPoint(point), point) <= contactTolerance,
                    "A successful damage callback must retain its original contact and angle across cancellation and re-entry.");
            }
            finally { target.OnDamaged -= redirect; } // 임시 피해 재진입 연결 해제
        }
        finally
        {
            box.End();
            UnityEngine.Object.Destroy(target.gameObject);
            player.ResetMotion();
        }
        yield return Frames(1); // 임시 피해 수신자의 물리 제거 완료 대기
    }

    /// <summary>원작 피격 섬광의 무작위 기하와 실제 시간 기반 Sprite 수명 검사</summary>
    private static IEnumerator CheckImpactVisuals(GameManager manager, PlayerCharacter player, Rigidbody2D body, PlayerFeedback feedback, FeedbackReport evidence)
    {
        // 정해진 난수 상태에서 네 공격 방향의 수직 오차와 위치 반경 확인
        ClearFeedback(feedback);
        UnityEngine.Random.State random = UnityEngine.Random.state;
        Vector2 point = new Vector2(80f, 81f);
        try
        {
            UnityEngine.Random.InitState(271828);
            foreach (float angle in new[] { 0f, 90f, 180f, 270f })
            {
                feedback.PlayImpact(point, angle);
                System.Collections.IList visuals = Read<System.Collections.IList>(feedback, "visuals");
                object slot = visuals[evidence.impacts.Count];
                SpriteRenderer renderer = Read<SpriteRenderer>(slot, "Renderer");
                float sign = Mathf.Cos(angle * Mathf.Deg2Rad) < -0.000001f ? -1f : 1f;
                float ideal = angle - 90f + 90f * sign;
                Require(renderer.enabled && renderer.sprite == feedback.HitFrames[0] && renderer.sharedMaterial == feedback.HitMaterial
                    && renderer.sortingOrder == 30 && renderer.transform.localScale.x == sign
                    && Mathf.Abs(Mathf.DeltaAngle(renderer.transform.eulerAngles.z, ideal)) <= 10.001f
                    && Vector2.Distance(renderer.transform.position, point) <= 0.2001f,
                    "Impact random position, perpendicular angle, flip, first frame or sorting differs from the reference.");
                evidence.impacts.Add(new ImpactSample { attackAngle = angle, position = renderer.transform.position, rotation = renderer.transform.eulerAngles.z });
            }
        }
        finally { UnityEngine.Random.state = random; }
        SetPosition(body, body.position + Vector2.right);
        yield return Frames(2); // 공격자가 이동한 뒤에도 섬광의 월드 위치가 유지되는지 관찰
        foreach (object slot in Read<System.Collections.IList>(feedback, "visuals"))
            if (Read<bool>(slot, "Active")) Require(Vector3.Distance(Read<SpriteRenderer>(slot, "Renderer").transform.position, Read<Vector3>(slot, "Position")) < 0.00001f,
                "A hit spark must keep its original world position when the Player moves.");

        // 실제 게임 시각의 일곱 이미지와 정지·만료 경계 확인
        ClearFeedback(feedback);
        feedback.PlayImpact(point, 0f);
        SpriteRenderer visual = ActiveImpact(feedback);
        double started = Time.timeAsDouble;
        manager.SetGameplayPaused(true);
        yield return Frames(4); // 게임 정지 중 섬광의 Sprite와 수명이 진행하지 않는지 관찰
        Require(Time.timeAsDouble == started && visual.sprite == feedback.HitFrames[0] && visual.enabled, "Paused game time must preserve the hit frame and lifetime.");
        manager.SetGameplayPaused(false);
        for (int frame = 1; frame < 7; frame++)
        {
            int expected = frame;
            yield return Until(() => Time.timeAsDouble - started >= expected * 0.05d + 0.005d, "impact frame" + frame); // 원작20Hz 피격 프레임의 실제 표시 구간 대기
            Require(visual.enabled && visual.sprite == feedback.HitFrames[frame], "Hit effect frame differs at index" + frame);
            if (frame == 2) CaptureWorld("hit-feedback-visible");
        }
        yield return Until(() => Time.timeAsDouble - started >= 0.36d, "impact expiration"); // 일곱 번째 이미지 이후.35초 수명이 끝날 때까지 대기
        Require(!visual.enabled && ActiveImpactCount(feedback) == 0, "Seven hit frames must finish after.35 game seconds.");
    }

    /// <summary>원래 걷기 거리와 강제 이동·벽·정지의 발소리 분리 검사</summary>
    private static IEnumerator CheckFootsteps(GameManager manager, PlayerCharacter player, Rigidbody2D body, PlayerFeedback feedback, FeedbackReport evidence)
    {
        // 정상 이동에서 실제1.2거리마다 소리와 순환 변형을 관측
        player.ResetMotion();
        ClearFeedback(feedback);
        player.SP = player.basicSP;
        feedback.StepDistance = 1.2f;
        SetPosition(body, new Vector2(100f, 100f));
        int start = evidence.sounds.Count;
        player.SetMoveInput(Vector2.right);
        yield return Until(() => body.position.x >= 102.45f, "normal walking distance"); // 실제 Body 이동으로 두 발소리 간격을 지날 때까지 대기
        player.SetMoveInput(Vector2.zero);
        evidence.walkingDistance = body.position.x - 100f;
        List<SoundSample> steps = evidence.sounds.GetRange(start, evidence.sounds.Count - start);
        Require(steps.Count == 2 && steps[0].cue == PlayerSoundCue.Footstep && steps[1].cue == PlayerSoundCue.Footstep
            && steps[0].clip != steps[1].clip && Array.IndexOf(feedback.FootstepClips, steps[0].clip) >= 0,
            "Actual walking must emit two alternating tile steps after2.45 units.");

        // 벽 접근 거리는 보폭보다 짧고 막힌 뒤의 의도 걷기는 소리를 만들지 않음
        player.ResetMotion();
        ClearFeedback(feedback);
        SetPosition(body, new Vector2(100f, 100f));
        GameObject wall = new GameObject("FeedbackFootstepWall");
        wall.transform.position = new Vector3(101f, 100f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.05f, 3f);
        Physics2D.SyncTransforms();
        try
        {
            int before = CountCue(evidence, PlayerSoundCue.Footstep);
            player.SetMoveInput(Vector2.right);
            yield return GameSeconds(0.7f); // 벽까지 이동한 뒤 계속 밀어도 발소리가 추가되지 않는지 관찰
            player.SetMoveInput(Vector2.zero);
            Require(CountCue(evidence, PlayerSoundCue.Footstep) == before, "Wall-blocked movement intent must not create false footsteps.");
        }
        finally { UnityEngine.Object.Destroy(wall); }
        yield return Frames(1); // 임시 벽의 물리 제거 완료 대기

        // 작은 검사 보폭으로 강제 이동의 잘못된 거리 누적도 감지
        feedback.StepDistance = 0.1f;
        player.ResetMotion();
        ClearFeedback(feedback);
        int count = CountCue(evidence, PlayerSoundCue.Footstep);
        player.SetMoveInput(Vector2.right);
        Require(player.ApplyDash(), "Feedback dash fixture must begin.");
        yield return Until(() => !player.IsDashing, "silent dash end"); //2.5거리의 강제 대시가 보폭으로 계산되지 않는지 관찰
        player.SetMoveInput(Vector2.zero);
        player.ResetMotion();
        player.SetAimPosition(body.position + Vector2.up);
        Require(player.ApplyAttack(), "Feedback attack-forward fixture must begin.");
        yield return Until(() => !player.IsAttacking, "silent attack-forward end"); // 공격의0.5 강제 전진이 발소리로 새지 않는지 관찰
        yield return GameSeconds(0.15f); // 남은 공격 전진과 정지 구간까지 발소리 여부 관찰
        manager.SetGameplayPaused(true);
        player.SetMoveInput(Vector2.right);
        yield return Frames(4); // 시간 정지 중 이동 의도만으로 소리가 생기지 않는지 관찰
        manager.SetGameplayPaused(false);
        player.IsCutScene = true;
        player.SetMoveInput(Vector2.right);
        yield return Frames(4); // 컷씬의 조작 제한 중 발소리가 생기지 않는지 관찰
        player.IsCutScene = false;
        player.SetMoveInput(Vector2.zero);
        Require(CountCue(evidence, PlayerSoundCue.Footstep) == count, "Dash, attack advance, idle, pause and cutscene must not create normal footsteps.");
        feedback.StepDistance = 1.2f;
        player.ResetMotion();
    }

    /// <summary>복제 프리팹의 소리·섬광·제거가 기존 Player 연결을 건드리지 않는지 확인</summary>
    private static IEnumerator CheckFeedbackClone(PlayerCharacter player, PlayerFeedback feedback, FeedbackReport evidence)
    {
        // 복제의 Start 연결 전에 자기 Spawner를 끄고 독립 준비 결과만 관측
        ClearFeedback(feedback);
        feedback.PlayImpact((Vector2)player.transform.position + Vector2.up, 0f);
        AudioSource source = feedback.Source;
        int sounds = evidence.sounds.Count;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Startup/Player.prefab");
        GameObject clone = UnityEngine.Object.Instantiate(prefab);
        try
        {
            PlayerCharacter duplicate = clone.GetComponent<PlayerCharacter>();
            duplicate.Spawner.enabled = false;
            Require(duplicate.Feedback != feedback && duplicate.Feedback.Source != source && duplicate.Feedback.Source.transform.IsChildOf(clone.transform),
                "Each cloned Player prefab must own independent cached feedback and audio.");
            duplicate.Feedback.PlayImpact(new Vector2(500f, 500f), 0f);
            Require(evidence.sounds.Count == sounds && ActiveImpactCount(feedback) == 1, "Clone feedback must not replay the original Player source or effects.");
        }
        finally { UnityEngine.Object.Destroy(clone); }
        yield return Frames(1); // 복제 Player의 비활성화·오디오 정리와 제거 완료 대기
        Require(feedback.Source == source && ActiveImpactCount(feedback) == 1 && PlayerInputController.Instance.Player == player
            && GameCamera.Instance.Target == player.transform, "Removing a duplicate must preserve the original source, effect and cached world bindings.");
        ClearFeedback(feedback);
    }

    /// <summary>이 검사에서 누적한 실제 소리 종류의 발생 횟수 조회</summary>
    private static int CountCue(FeedbackReport evidence, PlayerSoundCue cue)
    {
        int count = 0;
        foreach (SoundSample sound in evidence.sounds) if (sound.cue == cue) count++;
        return count;
    }

    /// <summary>소리·섬광 풀은 유지하고 이번 표시와 걷기 누적만 정리</summary>
    private static void ClearFeedback(PlayerFeedback feedback)
    {
        feedback.enabled = false;
        feedback.enabled = true;
    }

    /// <summary>실제 활성 섬광의 첫 Renderer 조회</summary>
    private static SpriteRenderer ActiveImpact(PlayerFeedback feedback)
    {
        foreach (object slot in Read<System.Collections.IList>(feedback, "visuals"))
            if (Read<bool>(slot, "Active")) return Read<SpriteRenderer>(slot, "Renderer");
        throw new InvalidOperationException("Expected an active impact renderer.");
    }

    /// <summary>자기 피드백 풀의 활성 표시만 집계</summary>
    private static int ActiveImpactCount(PlayerFeedback feedback)
    {
        int count = 0;
        foreach (object slot in Read<System.Collections.IList>(feedback, "visuals")) if (Read<bool>(slot, "Active")) count++;
        return count;
    }

    /// <summary>한 수신자의 실제 Collider들을 포함하는 피해 접점 비교 범위 계산</summary>
    private static Bounds TargetBounds(Character target)
    {
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
        return bounds;
    }

    /// <summary>실제InputAction의짧은클릭·해제버퍼·홀드콤보를같은입력장치로대조</summary>
    private static IEnumerator CheckActionInput(PlayerCharacter player, AttackController attack, PlayerInputController input, Rigidbody2D body)
    {
        // 실제 PlayerInput에 검사 전용 장치만 연결하고 이전 바인딩을 보관
        PlayerInput actions = input.GetComponent<PlayerInput>();
        InputDevice[] previousDevices = new InputDevice[actions.devices.Count];
        for (int i = 0; i < previousDevices.Length; i++) previousDevices[i] = actions.devices[i];
        string previousScheme = actions.currentControlScheme;
        bool blocked = input.BlockInput;
        bool enabled = input.enabled;
        float speed = player.SP;
        InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode editor = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Camera camera = GameCamera.Instance.Camera;
        try
        {
            // 포커스에 의존하지 않는 실제 액션 콜백으로 첫 공격 시작
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.enabled = true;
            input.BlockInput = false;
            actions.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.SP = 0f;
            player.AS = 1f;
            player.CancelAttack();
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, true);
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "actual first mouse attack"); // 실제 performed 콜백이 입력 계층·Character를 거쳐 첫 타를 시작할 때까지 대기

            // 최초 무시 구간 안에서 누르고 바로 뗀 두 번째 클릭은 예약하지 않음
            Require(attack.AttackElapsed < attack.InputIgnoreDuration, "The short click fixture must occur inside the initial ignore interval.");
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, false);
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, true);
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, false);
            yield return Frames(1); // 한 번 저장된 press 사실을 실제 입력 Update에서 소비할 때까지 대기
            Require(!attack.HasBufferedAttack && !input.AttackHeld, "A real short early click must be ignored after release.");
            yield return Until(() => !attack.IsAttacking, "actual ignored click completion"); // 무시된 실제 클릭이 다음 타로 실행되지 않는지 첫 타 완료 대기
            yield return Frames(2); // 공격 종료 뒤 추가 요청이 없는지 관찰
            Require(!attack.IsAttacking && attack.ComboIndex == 0, "The ignored InputAction click must not start a delayed second attack.");

            // 허용 이후 클릭은 마우스를 떼어도 두 번째 타 한 번으로 이어짐
            player.CancelAttack();
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, true);
            yield return Until(() => attack.IsAttacking && attack.AttackElapsed >= 0.06f, "actual input gate opening"); // 첫 타의 원작 입력 무시 구간 종료 대기
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, false);
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, true);
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, false);
            yield return Frames(1); // 실제 입력 Update가 늦은 누름을 버퍼에 전달할 때까지 대기
            Require(attack.HasBufferedAttack && !input.AttackHeld, "An accepted real click must remain buffered after button release.");
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 1, "actual buffered combo2"); // 해제된 클릭의 예약이 실제 두 번째 타를 시작할 때까지 대기
            yield return Until(() => !attack.IsAttacking, "actual buffered combo2 finish"); // 단일 예약의 두 번째 타 완료 대기
            yield return Frames(2); // 같은 클릭으로 세 번째 타가 반복되지 않는지 관찰
            Require(!attack.IsAttacking && attack.ComboIndex == 1, "One accepted real click must not repeat beyond one buffered swing.");

            // 한 번 누른 홀드는 새 클릭을 합성하지 않고1→2→3→1을 이어감
            player.CancelAttack();
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, true);
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "actual held combo1"); // 홀드 첫 타의 실제 시작 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 1, "actual held combo2"); // 같은 홀드의 두 번째 타 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 2, "actual held combo3"); // 같은 홀드의 세 번째 타 대기
            yield return Until(() => attack.IsAttacking && attack.ComboIndex == 0, "actual held restart1"); // 마지막 회복 뒤 같은 홀드가 새 첫 타를 시작할 때까지 대기
            MouseStateEvent(mouse, camera, body.position + Vector2.up * 2f, false);
            yield return Until(() => !attack.IsAttacking && !input.AttackHeld, "actual held release"); // 해제 직전에 시작한 한 타만 끝내고 홀드 반복을 멈출 때까지 대기
            Passed("Actual Input System ignores a short early click, retains one released late click and holds the1-2-3-1 combo until release");
        }
        finally
        {
            // 검사 장치와 입력 환경을 원래 상태로 돌리고 진행 중 공격 정리
            player.CancelAttack();
            input.BlockInput = blocked;
            input.enabled = enabled;
            player.SP = speed;
            if (!string.IsNullOrEmpty(previousScheme) && previousDevices.Length > 0)
                actions.SwitchCurrentControlScheme(previousScheme, previousDevices);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editor;
        }
    }

    /// <summary>실제마우스장치의위치·버튼상태를액션에전달</summary>
    private static void MouseStateEvent(Mouse mouse, Camera camera, Vector2 world, bool held)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(world) }.WithButton(MouseButton.Left, held));
        InputSystem.Update();
    }

    /// <summary>기존색원본과실제렌더러의모든검기단계·피벗·방향대조</summary>
    private static void CheckVfx(PlayerCharacter player, AttackController attack)
    {
        // 프로젝트가실제로참조한Sprite의PNG에서원본픽셀을직접읽기
        Sprite[] purple = Read<Sprite[]>(attack, "Attack1Vfx");
        Sprite[] blue = Read<Sprite[]>(attack, "Attack2Vfx");
        Sprite[] combined = Read<Sprite[]>(attack, "Attack3Vfx");
        Require(purple.Length == 4 && blue.Length == 4 && combined.Length == 6, "The confirmed4+4+6 VFX bank must be connected.");
        Color32[][] p = new Color32[4][];
        Color32[][] b = new Color32[4][];
        for (int index = 0; index < 4; index++)
        {
            p[index] = SpritePixels(purple[index]);
            b[index] = SpritePixels(blue[index]);
        }
        int[,] stages = { { 1, 0 }, { 1, 1 }, { 2, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 } };
        for (int index = 0; index < 6; index++)
        {
            Color32[] actual = SpritePixels(combined[index]);
            int pi = stages[index, 0];
            int bi = stages[index, 1];
            for (int y = 0; y < 66; y++)
            {
                for (int x = 0; x < 70; x++)
                {
                    Color32 pp = pi > 0 ? p[pi - 1][y * 70 + x] : default;
                    Color32 bp = bi > 0 ? b[bi - 1][y * 70 + 69 - x] : default;
                    Color32 expected = pp.a > 0 && bp.a > 0 ? (x < 35 ? bp : pp) : (pp.a > 0 ? pp : bp);
                    Color32 value = actual[y * 70 + x];
                    Require(value.a == expected.a && (value.a == 0 || value.Equals(expected)),
                        "Combined VFX changed a source or non-overlap pixel at image=" + index + ", x=" + x + ", y=" + y);
                }
            }
        }
        Passed("All six connected combined sprites preserve complete originals and only overlap pixels occlude blue-left/purple-right");

        // 확정54칸의실제표시함수에서사용하는Sprite와생성피벗확인
        PreviewManifest preview = JsonUtility.FromJson<PreviewManifest>(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "art/archive/weapon/player-combo-preview-r14/manifest.json")));
        SpriteRenderer renderer = Read<SpriteRenderer>(attack, "AttackVfxRenderer");
        MethodInfo spawn = typeof(AttackController).GetMethod("SpawnEffect", InstanceMembers);
        Require(spawn != null && renderer != null, "The runtime VFX spawn and renderer must be connected.");
        foreach (PreviewFrame frame in preview.frames)
        {
            BeginCombo(player, attack, 2, Vector2.up);
            int expectedIndex = -1;
            for (int index = 0; index < 6; index++)
                if (stages[index, 0] == frame.fx_purple_image && stages[index, 1] == frame.fx_blue_image) expectedIndex = index;
            if (expectedIndex >= 0)
            {
                spawn.Invoke(attack, new object[] { 2, (frame.frame - 23) / 60f });
                Require(renderer.enabled && renderer.sprite == combined[expectedIndex] && !renderer.flipX,
                    "Attack3 runtime sprite differs at frame" + frame.frame);
                Vector2 offset = renderer.transform.position - player.transform.position;
                Require(Vector2.Distance(offset, new Vector2(2.5f, 24f) / 16f) < 0.00001f,
                    "Up-facing VFX pivot must match the approved Player-relative(+2.5,+24)px offset.");
            }
            else Require(!renderer.enabled, "Attack3 has visible VFX outside frames23..35 at" + frame.frame);
        }
        for (int combo = 0; combo < 2; combo++)
        {
            for (int stage = 0; stage < 4; stage++)
            {
                BeginCombo(player, attack, combo, Vector2.up);
                spawn.Invoke(attack, new object[] { combo, stage * 0.05f });
                Require(renderer.enabled && renderer.sprite == (combo == 0 ? purple[stage] : blue[stage])
                    && renderer.flipX == (combo == 1), "Attack1/2 must keep the four original stages and flip only attack2.");
            }
        }
        player.CancelAttack();
        Passed("All54 confirmed attack3 VFX slots and both four-stage first attacks match the runtime sprite selection and shared pivot");
    }

    /// <summary>표시되는Sprite의완전한원본픽셀과import피벗규격확인</summary>
    private static Color32[] SpritePixels(Sprite sprite)
    {
        Require(sprite != null && sprite.rect.width == 70f && sprite.rect.height == 66f
            && Vector2.Distance(sprite.pivot, new Vector2(35f, 33f)) < 0.001f && sprite.pixelsPerUnit == 16f,
            "Every VFX sprite must retain70x66, pivot35/33 and16PPU.");
        string path = AssetDatabase.GetAssetPath(sprite);
        Texture2D pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(pixels.LoadImage(File.ReadAllBytes(path)), "Cannot decode connected VFX image " + path);
            Require(pixels.width == 70 && pixels.height == 66, "VFX source canvas must stay70x66.");
            return pixels.GetPixels32();
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels); }
    }

    /// <summary>독립된타격수신자와같은Character의복수Collider준비</summary>
    private static Character CreateTarget(string name, Vector2 position, int colliders)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        Character target = root.AddComponent<Character>();
        target.maxHP = 1000f;
        target.InitializeHealth(1000f);
        for (int index = 0; index < colliders; index++)
        {
            GameObject part = new GameObject("HitPart" + index);
            part.transform.SetParent(root.transform, false);
            BoxCollider2D collider = part.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.08f;
            collider.isTrigger = true;
            Hitbox hitbox = part.AddComponent<Hitbox>();
            hitbox.Owner = target;
            hitbox.TargetKind = TargetKind.Boss;
        }
        Physics2D.SyncTransforms();
        return target;
    }

    /// <summary>표본검사를위한콤보만준비한뒤실제캐릭터공격API로시작</summary>
    private static void BeginCombo(PlayerCharacter player, AttackController attack, int combo, Vector2 direction)
    {
        player.CancelAttack();
        SetProperty(attack, "ComboIndex", combo - 1);
        SetProperty(attack, "ComboRemaining", combo == 0 ? 0f : 0.1f);
        player.SetAimPosition((Vector2)player.transform.position + direction * 2f);
        Require(player.ApplyAttack() && attack.ComboIndex == combo, "Cannot start combo" + (combo + 1));
    }

    /// <summary>프레임간게임시간으로동작수명대기</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game seconds"); // timeScale과실제Player Loop에의한게임시간진행대기
    }

    /// <summary>실제월드카메라의현재검·검기를흰배경검토표본으로저장</summary>
    private static void CaptureWorld(string name)
    {
        Camera camera = GameCamera.Instance.Camera;
        PlayerCharacter player = GameManager.Instance.CurrentPlayer;
        RenderTexture texture = RenderTexture.GetTemporary(512, 512, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize;
        CameraClearFlags clear = camera.clearFlags;
        Color background = camera.backgroundColor;
        Texture2D pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = player.transform.position + new Vector3(0f, 1f, -10f);
            camera.orthographicSize = 3.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.targetTexture = texture;
            if (!captureWarmed)
            {
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                captureWarmed = true;
            }
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/AttackIntegration/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            camera.transform.position = position;
            camera.orthographicSize = size;
            camera.clearFlags = clear;
            camera.backgroundColor = background;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>원작1·2타와확정3타의실제클립을네방향에서모든표본과대조</summary>
    private static void CheckWeaponPoses(PlayerCharacter player, AttackController attack)
    {
        // 원본 분석과확정 R14의검 좌표를현재리포지토리에서직접로드
        string root = RepositoryRoot();
        ReferenceMotion reference = JsonUtility.FromJson<ReferenceMotion>(File.ReadAllText(Path.Combine(root,
            "docs/archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115/motion_curves_and_frames.json")));
        PreviewManifest preview = JsonUtility.FromJson<PreviewManifest>(File.ReadAllText(Path.Combine(root,
            "art/archive/weapon/player-combo-preview-r14/manifest.json")));
        SpriteRenderer visual = attack.WeaponMotion.GetComponentInChildren<SpriteRenderer>(true);
        Require(visual != null, "The animated motion pivot must own the visible sword.");
        Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        int count = 0;
        for (int combo = 0; combo < 3; combo++)
        {
            AnimationClip clip = FindClip(attack, "Attack" + (combo + 1));
            foreach (Vector2 direction in directions)
            {
                player.CancelAttack();
                SetProperty(attack, "ComboIndex", combo - 1);
                SetProperty(attack, "ComboRemaining", combo == 0 ? 0f : 0.1f);
                player.SetAimPosition((Vector2)player.transform.position + direction * 2f);
                Require(player.ApplyAttack() && attack.ComboIndex == combo, "A sampled combo must start through the real Character API.");
                float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                int samples = combo < 2 ? reference.clips[combo].frames60.Length : preview.frames.Length;
                for (int index = 0; index < samples; index++)
                {
                    float time;
                    Vector2 grip;
                    float angle;
                    if (combo < 2)
                    {
                        MotionFrame frame = reference.clips[combo].frames60[index];
                        time = frame.clip_time_seconds;
                        grip = new Vector2(frame.up_aim_player_relative_pivot_x, frame.up_aim_player_relative_pivot_y);
                        angle = frame.euler_z_degrees;
                    }
                    else
                    {
                        PreviewFrame frame = preview.frames[index];
                        time = frame.time_seconds;
                        grip = new Vector2(frame.grip_pixels[0], frame.grip_pixels[1]) / 16f;
                        angle = frame.angle_degrees;
                    }
                    clip.SampleAnimation(attack.WeaponAnimator.gameObject, time);
                    float side = direction.x < 0f ? -1f : 1f;
                    Vector2 shoulder = new Vector2(side * 0.15625f, direction.y > 0f ? 0.5f : 0.375f);
                    Vector2 local = grip - new Vector2(0.15625f, 0.5f);
                    Vector2 converted = new Vector2(local.y, -local.x * side);
                    Vector2 expected = shoulder + (Vector2)(Quaternion.Euler(0f, 0f, rotation + 90f) * converted);
                    Vector2 actual = attack.WeaponMotion.position - player.transform.position;
                    Require(Vector2.Distance(actual, expected) < 0.001f,
                        "Grip differs at combo=" + combo + ", direction=" + direction + ", sample=" + index + ", actual=" + actual + ", expected=" + expected);
                    float radians = angle * Mathf.Deg2Rad;
                    Vector2 bladeLocal = new Vector2(Mathf.Cos(radians), side * Mathf.Sin(radians));
                    Vector2 expectedBlade = Quaternion.Euler(0f, 0f, rotation + 90f) * bladeLocal;
                    Vector2 actualBlade = visual.transform.TransformVector(Vector3.down).normalized;
                    Require(Vector2.Dot(actualBlade, expectedBlade) > 0.99999f,
                        "Sword blade differs at combo=" + combo + ", direction=" + direction + ", sample=" + index + ", actual=" + actualBlade + ", expected=" + expectedBlade);
                    count++;
                }
            }
        }
        player.CancelAttack();
        Passed("All " + count + " sampled sword pivots and angles match reference attacks1/2 and R14 attack3 in four aim directions");
    }

    /// <summary>같은실제Animator에연결된공격클립만조회</summary>
    private static AnimationClip FindClip(AttackController attack, string name)
    {
        foreach (AnimationClip clip in attack.WeaponAnimator.runtimeAnimatorController.animationClips)
            if (clip.name == name) return clip;
        throw new InvalidOperationException("Missing connected clip: " + name);
    }

    /// <summary>검사용강제콤보표본만실제프로퍼티의내부설정자에전달</summary>
    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        if (property == null) throw new MissingMemberException(target.GetType().Name, name);
        property.SetValue(target, value);
    }

    /// <summary>격리프로젝트에서도원본분석과아트파일을찾을리포지토리루트확인</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Application.dataPath);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        if (directory == null) throw new DirectoryNotFoundException("Attack reference repository root not found.");
        return directory.FullName;
    }

    /// <summary>공개·내부 상태를 중복 타이머 없이 실제 구현에서 조회</summary>
    private static T Read<T>(object target, string name)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null) return (T)property.GetValue(target);
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) return (T)field.GetValue(target);
        throw new MissingMemberException(type.Name, name);
    }

    /// <summary>현재 씬의 실제 Title UI 조회</summary>
    private static UI_Title GetTitle()
    {
        return UnityEngine.Object.FindFirstObjectByType<UI_Title>();
    }

    /// <summary>실제 렌더 프레임의 지정 횟수 진행 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 실제 Player Loop가 지정 프레임 수만큼 진행할 때까지 대기
    }

    /// <summary>실제 상태가 바뀔 때까지 게임과 Editor를 진행하며 제한 시간 적용</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = EditorApplication.timeSinceStartup + 30d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end)
                throw new TimeoutException("Attack integration timed out: " + description);
            yield return null;
        }
    }

    /// <summary>움직임 검사에서 물리·Transform을 같은 기준점에 배치</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>관찰한 계약이 깨졌을 때 결과에 실패 원인 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>이번 실행에서 통과한 실제 검사만 기록</summary>
    private static void Passed(string message)
    {
        report.checks.Add(message);
        Debug.Log("[PlayerAttackIntegrationChecks] PASS: " + message);
    }

    /// <summary>실행 결과를 독립 로그에 보존하고 Play Mode 종료</summary>
    private static void Finish(Exception error)
    {
        // 실패 중단에서도 중첩 코루틴의 finally가 검사 대상 상태를 복구
        EditorApplication.update -= Tick; // 완료 후 검사 갱신 구독 해제
        while (routines != null && routines.Count > 0)
        {
            try { (routines.Pop() as IDisposable)?.Dispose(); }
            catch (Exception cleanup) { if (error == null) error = cleanup; }
        }
        report.success = error == null;
        report.error = error?.ToString() ?? "";
        Directory.CreateDirectory("Logs");
        File.WriteAllText(SessionState.GetString(ReportPathKey, "Logs/PlayerAttackIntegration-result.json"), JsonUtility.ToJson(report, true));
        SessionState.SetBool(FinishedKey, true);
        SessionState.SetBool(SuccessKey, report.success);
        if (report.success) Debug.Log("[PlayerAttackIntegrationChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[PlayerAttackIntegrationChecks] FAILED: " + error);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>이번 실행의 검사 사실과 실패 원인을 저장할 데이터</summary>
    [Serializable]
    private sealed class Report
    {
        public bool success; // 실제 검사 실행이 모두 끝났는지 결과
        public List<string> checks = new List<string>(); // 통과한 동작 검사의 설명
        public string error; // 실행에서 관찰한 실패 원인
    }

    /// <summary>실제 소리 호출과 피격·걷기 관측 결과</summary>
    [Serializable]
    private sealed class FeedbackReport
    {
        public List<SoundSample> sounds = new List<SoundSample>(); // 실제 PlayOneShot 직후 기록한 종류·클립·게임 시각
        public List<ImpactSample> impacts = new List<ImpactSample>(); // 네 방향 피격 섬광의 실제 위치·회전
        public Vector2 rawContact; //0거리 Cast가 실제 수신자에서 보고한 원본 접점
        public Vector2 correctedContact; // 실제 수신 shape의 ClosestPoint로 보정해 전달한 접점
        public Bounds contactTargetBounds; // 원본·보정 접점을 비교할 실제 수신 Collider 합집합
        public float walkingDistance; // 두 발소리를 관측한 정상 이동의 실제 거리
    }

    /// <summary>실제 한 번의 오디오 재생 호출</summary>
    [Serializable]
    private sealed class SoundSample
    {
        public PlayerSoundCue cue; // 공격A·공격B·피격·발소리의 종류
        public AudioClip clip; // 실제 PlayOneShot에 전달한 클립 참조
        public string path; // 독립 결과에서도 클립을 구분할 자산 경로
        public double gameTime; // 실제 재생 호출 시점의 게임 시간
        public float attackTime; // 소리 호출 때 공격의 AS 적용 경과시간
    }

    /// <summary>원작 오차 범위와 비교한 한 피격 표시</summary>
    [Serializable]
    private sealed class ImpactSample
    {
        public float attackAngle; // 성공 질의에서 전달한 우리 기준 공격 방향
        public Vector3 position; // 무작위 반경을 적용해 확정한 월드 위치
        public float rotation; // 수직 방향과 무작위 각도 오차를 적용한 실제 Z회전
    }

    /// <summary>원작곡선분석의두공격표본목록</summary>
    [Serializable]
    private sealed class ReferenceMotion
    {
        public ReferenceClip[] clips; // 원작1·2타의실제곡선에서추출한표본
    }

    /// <summary>한공격의기존60칸시점표본</summary>
    [Serializable]
    private sealed class ReferenceClip
    {
        public MotionFrame[] frames60; // 검피벗·각도대조에사용할원본시점목록
    }

    /// <summary>원작한시점의Player상대무기피벗과회전</summary>
    [Serializable]
    private sealed class MotionFrame
    {
        public float clip_time_seconds; // 원작클립시작부터의표본시각
        public float up_aim_player_relative_pivot_x; // 위조준기준Player상대가로피벗
        public float up_aim_player_relative_pivot_y; // 위조준기준Player상대세로피벗
        public float euler_z_degrees; // 위조준기준원작검Z회전
    }

    /// <summary>확정3타검토판의표본목록</summary>
    [Serializable]
    private sealed class PreviewManifest
    {
        public PreviewFrame[] frames; // 사용자가지정한54프레임의검포즈와검기단계
    }

    /// <summary>확정3타한그림의실제클립대조값</summary>
    [Serializable]
    private sealed class PreviewFrame
    {
        public int frame; // 첫그림부터센사용자순번
        public float time_seconds; // 합친3타시작부터의표본시각
        public float[] grip_pixels; // Player상대검손잡이의원본픽셀좌표
        public float angle_degrees; // 원본위조준검의Z회전
        public int fx_purple_image; // 표시할보라단계이며없는칸은0
        public int fx_blue_image; // 표시할파랑단계이며없는칸은0
    }
}
