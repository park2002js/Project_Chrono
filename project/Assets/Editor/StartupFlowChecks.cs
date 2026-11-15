using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 Title 씬과 격리된 저장 폴더로 F-S01 시작·복구 흐름을 검사</summary>
[InitializeOnLoad]
public static class StartupFlowChecks
{
    private const string ActiveKey = "Chrono.StartupFlowChecks.Active"; // 도메인 재로드 후 검사 진행 여부를 찾을 SessionState 키
    private const string FinishedKey = "Chrono.StartupFlowChecks.Finished"; // 편집 모드 복귀 시 검사 종료 여부를 찾을 키
    private const string SuccessKey = "Chrono.StartupFlowChecks.Success"; // 배치 종료 코드에 사용할 검사 성공 여부 키
    private const string DirectoryKey = "Chrono.StartupFlowChecks.Directory"; // 사용자 저장과 분리한 검사 폴더 보관 키
    private const string ScenePathKey = "Chrono.StartupFlowChecks.ScenePath"; // 검사 종료 후 복원할 원래 씬 경로 키
    private const string ArtKey = "Chrono.StartupFlowChecks.Art"; // 이번 실행에서 완성 아트 연결 검사만 선택할 키
    private static Stack<IEnumerator> _routines; // 현재 검사와 중첩 대기를 차례로 진행할 실행 스택
    private static CheckReport _report; // 이번 검사 항목·오류·저장 경로를 기록할 결과
    private static double _deadline; // 전체 검사의 무한 대기를 막는 Editor 기준 종료 시각

    /// <summary>Play Mode 도메인 재로드 이후에도 같은 검사 요청을 계속 진행</summary>
    static StartupFlowChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged; // Play Mode 진입·종료에 맞춰 검사 시작과 씬 복원
    }

    /// <summary>메뉴 또는 명령행에서 테스트용 저장 경로를 지정하고 Play Mode로 진입</summary>
    [MenuItem("Tools/Chrono/Check Startup Flow")]
    public static void Run()
    {
        // 검사 시작 조건을 확인하고 사용자 편집 씬 경로 보관
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Startup checks must begin outside Play Mode.");

        // 열린 Editor에서 저장되지 않은 편집을 덮어쓰지 않도록 제한
        if (!Application.isBatchMode)
        {
            Scene originalScene = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount != 1 || originalScene.isDirty || string.IsNullOrEmpty(originalScene.path))
                throw new InvalidOperationException("Startup checks require one saved, unmodified scene. Save your scene before running.");
            SessionState.SetString(ScenePathKey, originalScene.path);
        }

        // 실행마다 별도 저장 폴더를 만들고 실제 Title 구성 확인
        string directory = Path.GetFullPath(Path.Combine("Temp", "StartupFlowChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (manager == null)
            throw new InvalidOperationException("Title scene has no GameManager.");

        // 씬은 저장하지 않고 Play Mode 복제본에만 격리된 경로를 전달
        manager.SaveDirectory = directory;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetString(DirectoryKey, directory);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>같은 격리 저장·Play Mode 검사 장치로 실제 아트 연결 검증</summary>
    public static void RunArt()
    {
        SessionState.SetBool(ArtKey, true);
        Run();
    }

    /// <summary>검사를 시작하고 완료 후 배치는 종료하며 열린 Editor는 원래 씬으로 복귀</summary>
    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        // 이번 실행에서 요청한 검사만 모드 전환에 반응
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        // Play Mode에서는 검사 실행, 종료 뒤에는 배치 종료 또는 편집 씬 복원
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _report = new CheckReport();
            _routines = new Stack<IEnumerator>();
            _routines.Push(CheckStartupFlow()); // Editor 갱신에서 시작 흐름 검사를 순서대로 실행
            _deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick; // Editor 프레임마다 검사 코루틴과 제한 시간 확인
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode)
                EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
            else
                EditorSceneManager.OpenScene(SessionState.GetString(ScenePathKey, "Assets/Scenes/Title.unity"));
        }
    }

    /// <summary>Editor 프레임마다 검사 코루틴을 진행하고 예외·대기 초과를 실패로 기록</summary>
    private static void Tick()
    {
        try
        {
            // 전체 검사에 허용한 최대 대기 시간 확인
            if (EditorApplication.timeSinceStartup > _deadline)
                throw new TimeoutException("Startup flow checks exceeded 180 seconds.");

            // 중첩 검사부터 끝내고 다음 실제 프레임이 필요할 때 반환
            while (_routines.Count > 0)
            {
                IEnumerator routine = _routines.Peek();
                if (!routine.MoveNext())
                {
                    _routines.Pop();
                    (routine as IDisposable)?.Dispose();
                    continue;
                }

                // yield로 받은 하위 검사·조건 대기를 먼저 진행
                if (routine.Current is IEnumerator nested)
                {
                    _routines.Push(nested);
                    continue;
                }

                // 하위 코루틴이 아니면 다음 Editor 프레임까지 검사 진행 양보
                return;
            }

            // 모든 검사와 대기가 끝난 실행을 성공으로 마무리
            Finish(null);
        }
        catch (Exception exception)
        {
            Finish(exception);
        }
    }

    /// <summary>실제 슬롯 선택과 씬 이동을 통해 정상·손상·완료·실패 복구를 검사</summary>
    private static IEnumerator CheckStartupFlow()
    {
        // 아트 검사는 임시 맵 좌표를 가정하는 이전 기능 검사와 독립 실행
        if (SessionState.GetBool(ArtKey, false))
        {
            SessionState.SetBool(ArtKey, false);
            yield return ArtIntegrationChecks.Check(Passed); // 실제 완성 에셋의 상태·충돌·연출 연결 검사 대기
            yield break;
        }

        // 슬롯 선택 전 Title·공통 저장 준비와 초기 참조 상태 확인
        yield return WaitFor(() => GameManager.Instance != null && GetTitle() != null && GetTitle().IsReady, "initial Title readiness"); // GameManager와 Title의 최초 준비 완료 대기
        GameManager manager = GameManager.Instance;
        string expectedDirectory = SessionState.GetString(DirectoryKey, "");
        Require(!string.IsNullOrEmpty(manager.SaveDirectory), "Isolated save path was lost during Play Mode scene backup.");
        Require(Path.GetFullPath(manager.SaveDirectory) == expectedDirectory, "Isolated save path was not preserved.");
        Require(SaveManager.Slots.Length == 3, "Title must load exactly three slots.");
        Require(Array.TrueForAll(SaveManager.Slots, slot => slot == null), "Fresh directory must have three empty slots.");
        Require(SaveManager.Current == null && SaveManager.SelectSlot == -1, "Boot must not select a Current save before user input.");
        Require(SaveManager.IsReady && !SaveManager.IsLoading, "Title became ready before shared save data finished loading.");
        Require(!manager.IsSessionActive, "A session must not begin before a slot is selected.");
        Require(PlayerInputController.Instance == null && UIController.Instance == null && GameCamera.Instance == null, "Title must not retain MainWorld service instances.");
        yield return CheckTitlePreparation(GetTitle()); // Title 자체 준비와 공통 슬롯 대기 검사 완료
        Passed("Title independently waits for its intro and shared slots without selecting Current or reloading data");

        // 빠른 중복 슬롯 선택에도 최초 슬롯으로 한 번만 월드 생성
        UI_Title initialTitle = GetTitle();
        initialTitle.SelectSlot(0);
        initialTitle.SelectSlot(1);
        yield return WaitFor(() => manager.CurrentPlayer != null && manager.CurrentPlayer.IsSpawning, "initial Player spawn presentation"); // 최초 생성 표현의 실제 입력 게이트가 열릴 때까지 대기
        Require(manager.IsLoading && manager.CurrentPlayer.Spawner.IsReady && PlayerInputController.Instance.BlockInput
            && !manager.CurrentPlayer.CanMove && manager.CurrentPlayer.IsInvulnerable && manager.IsDeathPaused,
            "Initial Player spawn must finish service preparation and keep input, protection and game time gated.");
        Passed("F-P09 initial spawn waits for scene references and its unscaled presentation before opening input and game time");
        yield return WaitFor(() => !manager.IsLoading && manager.Stage.IsReady, "fresh MainWorld initialization"); // 신규 슬롯의 MainWorld 생성·연결 완료 대기
        Require(SaveManager.SelectSlot == 0, "A duplicate click changed the active slot while loading.");
        Require(ReferenceEquals(SaveManager.Current, SaveManager.Slots[0]), "Slot selection did not assign its SaveData instance to Current.");
        CheckWorld(manager);
        Require(manager.CurrentPlayer.HP == 100f, "Fresh player HP must use the initial save value.");
        Require(manager.CurrentPlayer.transform.position == manager.Stage.Map.GetCheckpoint("start").position, "Player was not placed at the saved checkpoint.");
        Require(manager.CurrentBoss != null && !manager.CurrentBoss.IsDead, "Fresh boss must be alive.");
        Passed("Fresh slot generates Map, guard robot, Player, UI and camera once");
        yield return PlayerMovementChecks.Run(manager, Passed); // 실제 입력·물리·Sprite 검사 완료 대기
        yield return CheckServiceLifetime(manager); // 공유 객체 중복·Player 교체·제거 검사 완료 대기
        yield return CheckAutomaticPlayerSpawn(manager); // 복제 즉시 Awake 초기화와 저장 체크포인트 재조회 검사 완료 대기
        yield return BossCombatChecks.Run(manager, Passed); // 더미의 실제 상속·패턴·위험물·Slash·그로기 검사 완료 대기
        yield return BossLifecycleChecks.Run(manager, Passed); // 실제 조우·사망·문·저장·재도전 검사 완료 대기

        // 요청 직후 원본을 바꿔도 이미 요청한 스냅샷이 바뀌지 않는지 검사
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        SaveManager.Current.Set("PlayerHP", 63f);
        SaveManager.Current.Set("EncounteredBosses", new List<string> { "guard_robot" });
        SaveManager.Current.Set("SaveCheckScalar", 7);
        SaveManager.Current.Set("SaveCheckScalar", 23.5f);
        SaveManager.Current.Set("SaveCheckList", new List<string> { "at-request" });
        SaveManager.Save();

        // 두 번째 요청은 큐에서 기다리므로 첫 요청의 즉시 직렬화에 가려진 얕은 복사도 검출
        SaveManager.Save();
        SaveManager.Current.Set("PlayerHP", 42f);
        SaveManager.Current.Get<List<string>>("EncounteredBosses").Clear();
        SaveManager.Current.Set("SaveCheckScalar", 0f);
        SaveManager.Current.Get<List<string>>("SaveCheckList").Clear();
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        string savePath = SaveManager.Current.FilePath;
        string backupPath = SaveManager.Current.BackupPath;
        Require(File.Exists(savePath) && File.Exists(backupPath), "Save and its single backup must exist.");
        SaveData previousSave = SaveManager.Current;
        UnityEngine.Object previousPlayer = manager.CurrentPlayer;
        PlayerInputController previousInput = PlayerInputController.Instance;
        GameCamera previousCamera = GameCamera.Instance;
        manager.ReturnToTitle();
        yield return WaitFor(() => IsTitleReady(manager), "return to Title"); // 세션 정리와 Title 슬롯 재로드 완료 대기
        Require(manager.CurrentPlayer == null && !manager.IsSessionActive, "Title retained a world session.");
        Require(previousPlayer == null, "The prior Player survived session cleanup.");
        Require(previousInput == null && previousCamera == null, "The prior scene's input and camera survived Title cleanup.");
        Require(PlayerInputController.Instance == null && UIController.Instance == null && GameCamera.Instance == null, "Leaving MainWorld must clear all scene-owned service instances.");
        Require(SaveManager.Slots[0] != previousSave, "Reload reused the previous session's mutable SaveData.");
        Require(ReferenceEquals(SaveManager.Current, previousSave), "Title reload cleared Current or redirected it to the refreshed slot array.");
        Require(SaveManager.SelectSlot == 0, "Title reload unexpectedly cleared the selected slot index.");
        Require(SaveManager.Slots[0].Get<float>("PlayerHP") == 63f, "Stored snapshot followed a later HP mutation.");
        Require(SaveManager.Slots[0].Get<List<string>>("EncounteredBosses").Contains("guard_robot"), "Stored snapshot followed a later list mutation.");
        Require(SaveManager.Slots[0].Get<float>("SaveCheckScalar") == 23.5f, "An arbitrary key did not preserve its replacement type and requested value.");
        Require(SaveManager.Slots[0].Get<List<string>>("SaveCheckList").Contains("at-request"), "An arbitrary list key followed a later mutation while queued.");
        Passed("Save snapshots preserve arbitrary keys, replacement types and collections across Title reload");

        // Title에서 재로드한 저장 참조와 HP로 세션을 다시 시작
        GetTitle().SelectSlot(0);
        yield return WaitFor(() => !manager.IsLoading && manager.Stage.IsReady, "saved-slot initialization"); // 저장 슬롯의 Player 생성·저장값 적용 완료 대기
        CheckWorld(manager);
        Require(ReferenceEquals(SaveManager.Current, SaveManager.Slots[0]) && !ReferenceEquals(SaveManager.Current, previousSave), "Reselecting a slot did not replace Current with the freshly loaded save.");
        Require(manager.CurrentPlayer.HP == 63f, "Saved HP was not applied to the Player.");
        Require(manager.SessionPlayTime >= 0, "Session play time must be nonnegative.");
        Passed("Saved slot restores Player HP and keeps one persistent manager");
        Require(!ReferenceEquals(PlayerInputController.Instance, previousInput) && !ReferenceEquals(GameCamera.Instance, previousCamera), "Re-entering MainWorld must create new scene-owned input and camera instances.");
        yield return CheckSceneCameraReference(manager); // 재진입한 입력이 새 씬의 카메라로 조준을 투영하는지 확인

        // 연속 저장 요청에서 가장 나중 값이 파일에 남는지 확인
        SaveManager.Current.Set("PlayerHP", 61f);
        SaveManager.Save();
        SaveManager.Current.Set("PlayerHP", 57f);
        SaveManager.Save();
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        manager.ReturnToTitle();
        yield return WaitFor(() => IsTitleReady(manager), "ordered writes reload"); // 연속 저장 결과를 Title에서 다시 읽을 때까지 대기
        Require(SaveManager.Slots[0].Get<float>("PlayerHP") == 57f, "An older write overwrote the most recent save request.");
        Passed("Sequential save requests preserve the newest requested data");

        // 본 파일 손상은 백업으로 복구하고 둘 다 손상되면 빈 슬롯 처리
        File.WriteAllText(savePath, "corrupt primary");
        yield return SaveManager.LoadSlotsAsync().ToCoroutine(); // 본 파일·백업 검사와 슬롯 재로드 완료 대기
        Require(SaveManager.Slots[0] != null && SaveManager.Slots[0].Get<float>("PlayerHP") == 57f, "A valid backup was not loaded after primary corruption.");
        File.WriteAllText(backupPath, "corrupt backup");
        File.WriteAllText(savePath, "corrupt primary again");
        yield return SaveManager.LoadSlotsAsync().ToCoroutine(); // 본 파일·백업 검사와 슬롯 재로드 완료 대기
        Require(SaveManager.Slots[0] == null, "Two invalid files must produce an empty slot.");
        Passed("Invalid sav falls back to bak; two invalid files produce an empty slot");

        // 완료 슬롯의 취소는 보존하고 명시적 확인에서만 새 게임 시작
        SaveManager.NewSaveFile(0);
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        SaveManager.Current.Set("DefeatedBosses", new List<string> { "guard_robot" });
        SaveManager.Save();
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        yield return GetTitle().InitializeAsync().ToCoroutine(); // 변경된 완료 슬롯의 Title 표시 준비 대기
        GetTitle().SelectSlot(0);
        Require(GetTitle().IsRestartConfirmationOpen && !manager.IsSessionActive, "Completed slot must ask before starting over.");
        GetTitle().ConfirmRestart(false);
        Require(!GetTitle().IsRestartConfirmationOpen && SaveManager.Slots[0].Get<List<string>>("DefeatedBosses").Contains("guard_robot"), "Cancel erased a completed save.");
        GetTitle().SelectSlot(0);
        GetTitle().ConfirmRestart(true);
        yield return WaitFor(() => !manager.IsLoading && manager.Stage.IsReady, "confirmed restart"); // 새 게임 확인 후 월드 초기화 완료 대기
        Require(SaveManager.Current.Get<List<string>>("DefeatedBosses").Count == 0, "Confirmed restart retained completed progress.");
        Require(!manager.CurrentBoss.IsDead, "Confirmed restart retained a dead boss.");
        Passed("Completed slot preserves data on cancel and resets only after confirmation");

        // 필수 프리팹 누락 시 Title 복구와 재시도 가능 여부 확인
        manager.ReturnToTitle();
        yield return WaitFor(() => IsTitleReady(manager), "failure-test Title"); // 프리팹 누락 검사 전 Title 준비 완료 대기
        PlayerCharacter playerPrefab = manager.Stage.PlayerPrefab;
        manager.Stage.PlayerPrefab = null;
        GetTitle().SelectSlot(0);
        yield return WaitFor(() => IsTitleReady(manager) && !string.IsNullOrEmpty(manager.LastError), "failed world initialization recovery"); // 초기화 실패 안내와 사용 가능한 Title 복구 대기
        Require(!manager.Stage.IsReady && manager.CurrentPlayer == null && manager.CurrentBoss == null, "Failed initialization leaked session references.");
        Require(Time.timeScale == 1f, "Failed initialization left gameplay time paused.");
        Require(PlayerInputController.Instance == null && UIController.Instance == null && GameCamera.Instance == null, "Initialization failure must release the scene-owned service instances.");
        manager.Stage.PlayerPrefab = playerPrefab;
        GetTitle().SelectSlot(0);
        yield return WaitFor(() => !manager.IsLoading && manager.Stage.IsReady, "retry after initialization failure"); // 누락 프리팹 복원 후 재진입 완료 대기
        CheckWorld(manager);
        Passed("Missing prefab returns to usable Title, clears references and permits retry");

        // 임시 파일 자리에 빈 디렉토리를 만들어 저장·재시도·복구 쓰기를 모두 실패하도록 유도
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        string blockedWritePath = Path.GetFullPath(SaveManager.Current.FilePath + ".tmp");
        Require(blockedWritePath.StartsWith(expectedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Write failure injection escaped the isolated save directory.");
        Require(!File.Exists(blockedWritePath) && !Directory.Exists(blockedWritePath), "Write failure injection would overwrite an existing path.");
        Directory.CreateDirectory(blockedWritePath);
        try
        {
            SaveManager.Current.Set("PlayerHP", 76f);
            SaveManager.Save();
            yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        }
        finally
        {
            Directory.Delete(blockedWritePath);
        }
        SaveManager.Current.Set("PlayerHP", 74f);
        SaveManager.Save();
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기

        // 실패 주입을 제거한 뒤 다음 저장의 성공과 최종 세션 정리 확인
        manager.ReturnToTitle();
        yield return WaitFor(() => IsTitleReady(manager), "final cleanup"); // 최종 세션 제거와 Title 준비 완료 대기
        yield return SaveManager.FlushAsync().ToCoroutine(); // 요청한 저장·백업·복구 처리가 끝날 때까지 대기
        Require(SaveManager.Slots[SaveManager.SelectSlot].Get<float>("PlayerHP") == 74f, "A failed save request prevented the next request from reaching disk.");
        Passed("Save write and recovery failures stay internal and the next save request persists successfully");
        Require(!manager.IsSessionActive && manager.Stage.SessionRoot == null && manager.CurrentPlayer == null && manager.CurrentBoss == null, "Final Title cleanup retained world objects.");
        Require(PlayerInputController.Instance == null && UIController.Instance == null && GameCamera.Instance == null, "Final Title cleanup retained a service instance.");
        Require(SaveManager.IsReady && !SaveManager.IsLoading && SaveManager.Current != null, "Final Title lost its ready slots or Current reference.");
        Passed("Final Title cleanup removes world objects, retains Current and completes the save flush");
    }

    /// <summary>Title이 슬롯 로드를 다시 요청하지 않고 표시 준비와 공통 데이터 준비를 각각 기다리는지 검사</summary>
    private static IEnumerator CheckTitlePreparation(UI_Title title)
    {
        // 이미 준비된 슬롯을 재로드하지 않고 Title 연출만 기다리는지 확인
        float originalDuration = title.IntroDuration;
        SaveData[] readySlots = SaveManager.Slots;
        title.IntroDuration = 0.15f;
        UniTask intro = title.InitializeAsync();
        Require(!title.IsReady, "Title enabled selection before its intro completed.");
        yield return intro.ToCoroutine(); // Title 연출과 선택 허용 상태 전환 완료 대기
        Require(title.IsReady && ReferenceEquals(SaveManager.Slots, readySlots), "Title initialization reloaded shared slots instead of consuming prepared data.");

        // 표시 대기가 없어도 슬롯 준비 전에는 선택 차단
        title.IntroDuration = 0f;
        UniTask load = SaveManager.LoadSlotsAsync();
        Require(SaveManager.IsLoading && !SaveManager.IsReady, "Slot reload did not expose its pending readiness.");
        UniTask preparation = title.InitializeAsync();
        Require(!title.IsReady, "Title enabled selection while shared slots were still loading.");
        yield return UniTask.WhenAll(load, preparation).ToCoroutine(); // 슬롯 재로드와 Title 자체 준비가 모두 끝날 때까지 대기
        Require(title.IsReady && SaveManager.IsReady && SaveManager.Current == null, "Title preparation did not finish independently without selecting Current.");
        title.IntroDuration = originalDuration;
    }

    /// <summary>독립 서비스의 중복 방어와 Player 교체·제거 시 대상 연결 수명을 검사</summary>
    private static IEnumerator CheckServiceLifetime(GameManager manager)
    {
        // 검사 후 복원할 원래 대상·위치·차단 상태와 임시 객체 보관
        PlayerCharacter original = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        UIController ui = UIController.Instance;
        GameCamera camera = GameCamera.Instance;
        Vector2 originalPosition = original.GetComponent<Rigidbody2D>().position;
        bool wasBlocked = input.BlockInput;
        GameObject duplicateInput = null;
        GameObject duplicateUI = null;
        GameObject duplicateCamera = null;
        GameObject focus = null;
        PlayerCharacter first = null;
        PlayerCharacter second = null;
        try
        {
            // 중복 서비스의 Awake·삭제가 원래 Instance와 입력을 건드리는지 확인
            input.BlockInput = true;
            original.SetMoveInput(Vector2.right);
            duplicateInput = UnityEngine.Object.Instantiate(input.gameObject);
            duplicateUI = UnityEngine.Object.Instantiate(ui.gameObject);
            duplicateCamera = UnityEngine.Object.Instantiate(camera.gameObject);
            Require(PlayerInputController.Instance == input && UIController.Instance == ui && GameCamera.Instance == camera, "Duplicate Awake must not replace an existing service instance.");
            yield return WaitFor(() => duplicateInput == null && duplicateUI == null && duplicateCamera == null, "duplicate service cleanup"); // 중복 서비스의 지연 Destroy 완료 대기
            Require(input.Player == original && original.MoveDirection == Vector2.right && ui.Player == original && camera.Target == original.transform, "Rejecting duplicate services must not stop or unbind their existing Player.");
            Require(UnityEngine.Object.FindObjectsByType<PlayerInputController>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<UIController>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsSortMode.None).Length == 1, "Duplicate services must be removed without leaving a second active instance.");
            original.SetMoveInput(Vector2.zero);

            // Player 이외의 외부 Transform으로 카메라 초점 변경 확인
            focus = new GameObject("ServiceCheckCameraFocus");
            focus.transform.position = new Vector3(1f, 2f, 0f);
            camera.Follow(focus.transform, true);
            Require(camera.Target == focus.transform && Vector2.Distance(camera.Camera.transform.position, focus.transform.position) < 0.001f, "An external caller must be able to focus the owned game camera on another target.");
            camera.Follow(original.transform, true);
            Passed("Scene services reject duplicates without disturbing their owner and GameCamera accepts an external focus target");

            // Player 복제만으로 대상을 빼앗지 않고 Bind에서만 연결하는지 확인
            first = UnityEngine.Object.Instantiate(manager.Stage.PlayerPrefab);
            PlayerSpawner firstSpawner = first.GetComponent<PlayerSpawner>();
            Require(first.IsInitialized && firstSpawner.Player == first, "Player Awake must initialize a cloned Player without an explicit initialization call.");
            Require(first.GetComponent<PlayerInputController>() == null && first.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null && input.Player == original && ui.Player == original && camera.Target == original.transform, "Duplicating a Player must neither duplicate services nor silently take over their targets.");
            firstSpawner.Bind();
            Require(input.Player == first && ui.Player == first && camera.Target == first.transform && input.BlockInput, "The parameterless Bind must use registered services and begin with input blocked.");

            // 두 번째 Player 연결 후 이전 Player 제거가 새 연결을 유지하는지 확인
            second = UnityEngine.Object.Instantiate(manager.Stage.PlayerPrefab);
            PlayerSpawner secondSpawner = second.GetComponent<PlayerSpawner>();
            secondSpawner.Bind();
            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitFor(() => first == null, "old Player destruction"); // 이전 Player의 OnDestroy와 연결 해제 완료 대기
            Require(input.Player == second && ui.Player == second && camera.Target == second.transform, "Destroying the previous Player must not clear the newly bound Player.");

            // 현재 Player 제거 뒤 공유 객체는 남고 대상·이동 캐시만 비우는지 확인
            UnityEngine.Object.Destroy(second.gameObject);
            yield return WaitFor(() => second == null, "current Player destruction"); // 현재 Player의 OnDestroy와 대상 정리 완료 대기
            Require(PlayerInputController.Instance == input && UIController.Instance == ui && GameCamera.Instance == camera, "Scene services must outlive deletion of their Player.");
            Require(input.Player == null && ui.Player == null && camera.Target == null && input.MoveInput == Vector2.zero, "Deleting the bound Player must clear its targets and cached movement.");
            Passed("Player duplication, replacement and destruction preserve independent service instances and release only their own targets");
        }
        finally
        {
            // 검사 성공 여부와 무관하게 임시 객체를 제거하고 원래 Player 연결 복원
            if (first != null) UnityEngine.Object.Destroy(first.gameObject);
            if (second != null) UnityEngine.Object.Destroy(second.gameObject);
            if (duplicateInput != null) UnityEngine.Object.Destroy(duplicateInput);
            if (duplicateUI != null) UnityEngine.Object.Destroy(duplicateUI);
            if (duplicateCamera != null) UnityEngine.Object.Destroy(duplicateCamera);
            if (focus != null) UnityEngine.Object.Destroy(focus);
            original.SetMoveInput(Vector2.zero);
            original.transform.position = new Vector3(originalPosition.x, originalPosition.y, original.transform.position.z);
            original.GetComponent<Rigidbody2D>().position = originalPosition;
            original.GetComponent<PlayerSpawner>().Bind();
            input.BlockInput = wasBlocked;
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>Player 복제의 Awake 초기화와 인자 없는 체크포인트 재배치 검사</summary>
    private static IEnumerator CheckAutomaticPlayerSpawn(GameManager manager)
    {
        // 저장 데이터·맵 배치와 원래 입력 상태를 검사 후 복구할 수 있게 보관
        MapManager map = manager.Stage.Map;
        Require(MapManager.Instance == map && map.IsInitialized, "The current Map must register and finish initialization before a Player is cloned.");
        SaveData save = SaveManager.Current;
        string checkpointId = save.Get<string>("CheckpointId");
        float savedHP = save.Get<float>("PlayerHP");
        MapManager.CheckpointSpawn[] checkpoints = map.Checkpoints;
        PlayerInputController input = PlayerInputController.Instance;
        bool wasBlocked = input.BlockInput;
        GameObject checkpoint = null;
        PlayerCharacter clone = null;
        try
        {
            // 임시 체크포인트와 저장값을 먼저 준비하고 Player의 외부 초기화 호출 없이 복제
            input.BlockInput = true;
            checkpoint = new GameObject("AutomaticSpawnCheckCheckpoint");
            checkpoint.transform.position = map.GetCheckpoint(checkpointId).position + new Vector3(1f, 0.5f, 0f);
            var extended = new MapManager.CheckpointSpawn[checkpoints.Length + 1];
            Array.Copy(checkpoints, extended, checkpoints.Length);
            extended[extended.Length - 1] = new MapManager.CheckpointSpawn { Id = "automatic-spawn-check", Position = checkpoint.transform };
            map.Checkpoints = extended;
            save.Set("CheckpointId", "automatic-spawn-check");
            save.Set("PlayerHP", 37f);
            clone = UnityEngine.Object.Instantiate(manager.Stage.PlayerPrefab);
            PlayerSpawner spawner = clone.GetComponent<PlayerSpawner>();
            Require(clone.IsInitialized && clone.HP == 37f && spawner.Player == clone, "Player Awake must apply the selected save HP immediately after Instantiate returns.");
            Require(spawner.SpawnPoint == checkpoint.transform && Vector2.Distance(clone.GetComponent<Rigidbody2D>().position, checkpoint.transform.position) < 0.001f && Vector2.Distance(clone.transform.position, checkpoint.transform.position) < 0.001f, "Player Awake must place both Transform and Rigidbody2D at its saved checkpoint.");
            Require(input.Player == manager.CurrentPlayer, "Automatic initialization must not bind a cloned Player to shared input.");

            // 수정 활성화와 같은 캐시 갱신 뒤 같은 Player가 인자 없이 새 위치 사용
            save.Set("CheckpointId", checkpointId);
            spawner.UpdateCheckpoint(checkpointId);
            spawner.playerSpawn();
            Transform expected = map.GetCheckpoint(checkpointId);
            Require(spawner.SpawnPoint == expected && Vector2.Distance(clone.GetComponent<Rigidbody2D>().position, expected.position) < 0.001f, "Parameterless playerSpawn must use the checkpoint cache updated by activation.");
            Passed("Player Awake applies selected save state and parameterless respawn uses the checkpoint updated at activation");
        }
        finally
        {
            // 사용자 저장 요청 없이 메모리 데이터·임시 배치·Player 복제본 정리
            save.Set("CheckpointId", checkpointId);
            save.Set("PlayerHP", savedHP);
            map.Checkpoints = checkpoints;
            if (clone != null) UnityEngine.Object.Destroy(clone.gameObject);
            if (checkpoint != null) UnityEngine.Object.Destroy(checkpoint);
            input.BlockInput = wasBlocked;
        }
        yield return WaitFor(() => clone == null && checkpoint == null, "automatic spawn fixture cleanup"); // 다음 검사에 임시 Player·체크포인트가 남지 않도록 제거 완료 대기
    }

    /// <summary>MainWorld 재진입 후 입력이 현재 카메라로 화면 좌표를 투영하는지 검사</summary>
    private static IEnumerator CheckSceneCameraReference(GameManager manager)
    {
        // 검사 후 복원할 카메라·입력 상태와 실제 Look 액션 보관
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        GameCamera gameCamera = GameCamera.Instance;
        Camera camera = gameCamera.Camera;
        Transform previousTarget = gameCamera.Target;
        Vector3 previousCameraPosition = camera.transform.position;
        Vector2 previousScreenAim = input.aimInput;
        Vector2 previousWorldAim = input.AimWorldPosition;
        Vector2 previousPlayerAim = player.AimWorldPosition;
        bool wasBlocked = input.BlockInput;
        var lookAction = input.GetComponent<UnityEngine.InputSystem.PlayerInput>().actions.FindAction("Player/Look", true);
        bool lookWasEnabled = lookAction.enabled;
        try
        {
            // 실제 마우스가 검사 좌표를 덮어쓰지 않도록 Look만 잠시 비활성화
            lookAction.Disable();
            input.BlockInput = true;
            gameCamera.Follow(null);
            camera.transform.position = previousCameraPosition + new Vector3(2.75f, -1.5f, 0f);

            // 이동한 현재 카메라의 화면 좌표를 실제 입력 Update가 다시 월드 좌표로 변환
            Vector3 expected = player.transform.position + new Vector3(1.5f, -2.25f, 0f);
            input.aimInput = camera.WorldToScreenPoint(expected);
            input.AimWorldPosition = new Vector2(float.MaxValue, float.MaxValue);
            yield return WaitFor(() => Vector2.Distance(input.AimWorldPosition, expected) < 0.001f && Vector2.Dot(player.AimDirection, ((Vector2)expected - (Vector2)player.transform.position).normalized) > 0.999f, "re-entered scene camera projection"); // 새 카메라 참조로 커서 투영과 Player 방향이 갱신될 때까지 대기
            Passed("Re-entered MainWorld input projects through its new scene camera after one-time reference initialization");
        }
        finally
        {
            // 카메라 대상·화면 입력·조준·액션 상태를 검사 전 상태로 복원
            gameCamera.Follow(previousTarget);
            camera.transform.position = previousCameraPosition;
            input.aimInput = previousScreenAim;
            input.AimWorldPosition = previousWorldAim;
            player.SetAimPosition(previousPlayerAim);
            input.BlockInput = wasBlocked;
            if (lookWasEnabled)
                lookAction.Enable();
        }
    }

    /// <summary>생성 결과와 UI·카메라 연결 및 관리자 중복 여부를 검사</summary>
    private static void CheckWorld(GameManager manager)
    {
        Require(SceneManager.GetActiveScene().name == "MainWorld", "MainWorld must be the active scene.");
        Require(manager.IsSessionActive && manager.Stage.IsReady, "World became available before the session was ready.");
        Require(manager.Stage.Map != null && manager.Stage.Player != null && manager.Stage.Bosses.Length == 1, "Map, Player or single Boss is missing.");
        Require(UIController.Instance != null && GameCamera.Instance != null && PlayerInputController.Instance != null, "Scene service Awake registration must finish before world preparation completes.");
        Require(UIController.Instance.Player == manager.CurrentPlayer, "UI did not bind the generated Player.");
        Require(GameCamera.Instance.Target == manager.CurrentPlayer.transform && GameCamera.Instance.Camera != null, "Camera did not initialize or bind the generated Player.");
        Require(PlayerInputController.Instance.Player == manager.CurrentPlayer, "Input did not bind the generated Player.");
        Require(UnityEngine.Object.FindObjectsByType<UIController>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<PlayerInputController>(FindObjectsSortMode.None).Length == 1, "MainWorld must contain exactly one instance of each scene service.");
        Require(UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length == 1, "More than one GameManager survived scene transition.");
    }

    /// <summary>현재 씬의 Title UI를 조회</summary>
    private static UI_Title GetTitle()
    {
        return UnityEngine.Object.FindFirstObjectByType<UI_Title>();
    }

    /// <summary>실패 복구 또는 세션 종료 후 재선택 가능한 Title 상태인지 조회</summary>
    private static bool IsTitleReady(GameManager manager)
    {
        UI_Title title = GetTitle();
        return SceneManager.GetActiveScene().name == "Title" && !manager.IsLoading && title != null && title.IsReady;
    }

    /// <summary>비동기 씬·슬롯 준비를 제한 시간 동안 대기</summary>
    private static IEnumerator WaitFor(Func<bool> condition, string description)
    {
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException($"Timed out waiting for {description}; editorPaused={EditorApplication.isPaused}, timeScale={Time.timeScale}, scene={SceneManager.GetActiveScene().name}, manager={GameManager.Instance != null}, title={GetTitle() != null}, titleError={GetTitle()?.ErrorMessage}, savesReady={SaveManager.IsReady}, savesLoading={SaveManager.IsLoading}, saveError={SaveManager.LoadError}");
            yield return null;
        }
    }

    /// <summary>깨진 완료 조건을 검사 실패로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>완료한 검증 항목을 결과 파일과 Editor 로그에 기록</summary>
    private static void Passed(string description)
    {
        _report.checks.Add(description);
        Debug.Log("[StartupFlowChecks] PASS: " + description);
    }

    /// <summary>성공·실패 결과를 저장한 뒤 Play Mode를 종료</summary>
    private static void Finish(Exception exception)
    {
        // 검사 실행을 멈추고 성공 여부·오류·격리 폴더 기록
        EditorApplication.update -= Tick; // 완료 후 검사 콜백이 계속 실행되지 않도록 구독 해제
        _report.success = exception == null;
        _report.error = exception?.ToString() ?? "";
        _report.saveDirectory = SessionState.GetString(DirectoryKey, "");

        // Unity 종료 시 비워지는 Temp 대신 무시 대상 Logs에 검사 결과를 보존
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Path.Combine("Logs", "StartupFlowChecks-result.json"), JsonUtility.ToJson(_report, true));

        // 편집 모드 복귀 처리에 결과를 넘기고 Play Mode 종료
        SessionState.SetBool(SuccessKey, _report.success);
        SessionState.SetBool(FinishedKey, true);
        if (exception == null)
            Debug.Log("[StartupFlowChecks] ALL PASSED (" + _report.checks.Count + " checks)");
        else
            Debug.LogError("[StartupFlowChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>이번 실행에서 실제 확인한 항목과 실패 원인을 직렬화</summary>
    [Serializable]
    private class CheckReport
    {
        public bool success; // 이번 검사 실행이 예외 없이 끝났는지 결과 파일에 기록
        public List<string> checks = new List<string>(); // 이번 실행에서 통과한 검사 항목의 설명을 순서대로 기록
        public string error; // 검사 실패의 예외 내용을 기록하며 성공 시 빈 문자열
        public string saveDirectory; // 사용자 저장과 분리한 이번 검사의 임시 저장 폴더 경로
    }
}
