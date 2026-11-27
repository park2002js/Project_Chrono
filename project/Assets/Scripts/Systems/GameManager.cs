using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>앱의 저장 데이터 준비와 세션 시작·씬 전환·종료를 조정</summary>
[RequireComponent(typeof(StageManager), typeof(UI_Loading))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // 외부에서 GameManager 기능에 접근하는 단일 인스턴스
    public StageManager Stage; // MainWorld 생성·초기화·해제를 요청할 관리자
    public UI_Loading Loading; // 씬 전환·종료 대기 상태를 표시할 로딩 UI
    public bool IsLoading; // 씬 전환 또는 MainWorld 준비 중 중복 진입을 차단하는 상태
    public bool IsSessionActive; // 슬롯 선택부터 세션 종료까지 플레이 시간을 집계할지 표시
    public bool IsGameplayPaused; // 일반 정지 요청 여부이며 Draw의 별도 정지와 구분
    public bool IsDrawPaused { get; private set; } // Draw가 요청한 정지이며 일반 정지와 독립적으로 유지
    public bool IsSlashPaused { get; private set; } // Slash 연출이 소유한 정지이며 다른 정지 요청과 독립적으로 유지
    public bool IsDeathPaused { get; private set; } // 사망·부활 연출이 소유한 정지이며 기존 정지와 독립적으로 유지
    public bool IsBossCutscenePaused { get; private set; } // Boss 조우·사망 연출이 소유한 독립 게임 시간 정지
    public string LastError; // 세션 시작 또는 씬·월드 초기화 실패 이유를 외부에 제공
    public float StageLoadingSeconds = 1f; // 문에 들어간 뒤 다음 맵이 먼저 준비돼도 로딩 화면을 유지할 최소 실제 시간
    public PlayerCharacter CurrentPlayer => Stage != null ? Stage.Player : null; // 현재 세션에서 생성한 Player를 외부에 제공
    public BossCharacter CurrentBoss => Stage != null && Stage.Bosses.Length > 0 ? Stage.Bosses[0] : null; // 현재 세션의 첫 Boss를 외부에 제공
    public double SessionPlayTime => _savedPlayTime + (IsSessionActive ? Time.realtimeSinceStartupAsDouble - _sessionStartedAt : 0d); // 저장된 시간에 시간 정지와 무관한 현재 세션 경과 초를 합산

    private bool _isQuitting; // 종료 요청 중 중복 전환과 월드 준비 재개를 막는 상태
    private bool _allowQuit; // 저장 대기가 끝나 운영체제 종료 요청을 허용하는 상태
    private double _savedPlayTime; // 이전 저장 또는 세션 종료까지 누적된 플레이 시간
    private double _sessionStartedAt; // 현재 세션의 실제 경과 시간을 계산할 시작 시각
    private bool _enterFailed; // 마지막 월드 진입이 같은 세션 안에서 실패해 Title 복구가 필요한지 표시

    /// <summary>단일 전역 시스템을 설정하고 UI와 독립적으로 저장 데이터 로드를 시작</summary>
    private void Awake()
    {
        // 중복 시스템을 제거하고 기존 전역 관리자 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 씬 전환에도 유지할 관리자와 자체 컴포넌트 등록
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Stage = GetComponent<StageManager>();
        Loading = GetComponent<UI_Loading>();
        Application.wantsToQuit += HandleQuitRequested; // 창 닫기 요청도 저장 완료 후 종료 흐름으로 연결

        // Title UI와 독립적으로 공용 슬롯 데이터 준비
        SaveManager.Initialize(SaveData.CommonPath);
        SaveManager.LoadSlotsAsync().Forget(); // 부팅 흐름을 막지 않고 Title과 함께 공용 슬롯 로드 시작
    }

    /// <summary>시간 정지와 무관한 세션 시간을 저장 데이터에 반영</summary>
    private void Update()
    {
        if (IsSessionActive && SaveManager.Current != null)
            SaveManager.Current.Set("PlayTime", SessionPlayTime);
    }

    private void OnDestroy()
    {
        // 중복 객체 제거가 현재 전역 관리자에 영향을 주지 않도록 제한
        if (Instance != this)
            return;

        // 현재 관리자의 종료 연결과 전역 상태 정리
        Application.wantsToQuit -= HandleQuitRequested; // 파괴된 관리자가 후속 종료 요청을 받지 않도록 구독 해제
        Instance = null;
        Time.timeScale = 1f;
    }

    /// <summary>데모의 끝까지 본 슬롯인지 저장값으로 확인하며 기록이 없는 예전 저장은 진행 중으로 봄</summary>
    public static bool IsSlotCompleted(SaveData data)
    {
        return data != null && data.Values.ContainsKey("DemoCompleted") && data.Get<bool>("DemoCompleted");
    }

    /// <summary>외부의 슬롯 선택 요청으로 Current를 설정하고 씬 전환 수락 여부를 반환</summary>
    public bool StartGame(int slot, bool newGame = false)
    {
        // 중복 전환·미준비 데이터·잘못된 슬롯 요청 거부
        if (IsLoading || IsSessionActive || _isQuitting || !SaveManager.IsReady)
            return false;
        if (slot < 0 || slot >= SaveManager.Slots.Length)
            return false;

        // 완료 슬롯은 새 게임 확인을 거친 요청만 수락
        SaveData existing = SaveManager.Slots[slot];
        if (!newGame && IsSlotCompleted(existing))
            return false;

        // 선택 데이터를 지정하거나 새 게임의 기본 저장값 생성
        try
        {
            if (newGame || existing == null)
                SaveManager.NewSaveFile(slot, Stage.StageIndex);
            else
                SaveManager.SetCurrent(slot);
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            return false;
        }

        // 세션 계측과 로딩 상태를 먼저 설정한 뒤 월드 준비 시작
        _savedPlayTime = SaveManager.Current.Get<double>("PlayTime");
        _sessionStartedAt = Time.realtimeSinceStartupAsDouble;
        IsSessionActive = true;
        IsLoading = true;
        LastError = null;
        SetGameplayPaused(true);
        Loading.Show();
        StartCoroutine(LoadWorld()); // MainWorld 로드·생성·실패 복구를 프레임에 걸쳐 진행
        return true;
    }

    /// <summary>맵 끝의 문에 들어온 Player를 유지한 채 로딩 화면 뒤에서 다음 묶음으로 전환을 시작하고 수락 여부 반환</summary>
    public bool AdvanceStage()
    {
        // 중복 전환·세션 밖 요청·다음 묶음이 없는 요청 거부
        if (IsLoading || !IsSessionActive || _isQuitting || !Stage.IsReady || !Stage.HasNextStage)
            return false;

        // 조작과 게임 시간을 멈추고 로딩 화면 아래에서 맵 교체 시작
        IsLoading = true;
        LastError = null;
        PlayerInputController.Instance.BlockInput = true;
        UIController.Instance.SetPlayerHudVisible(false);
        SetGameplayPaused(true);
        Loading.Show();
        StartCoroutine(LoadNextStage()); // 맵 교체·입장·실패 복구를 프레임에 걸쳐 진행
        return true;
    }

    /// <summary>세션 계측을 끝내고 생성물을 해제한 뒤 Title로 복귀</summary>
    public void ReturnToTitle()
    {
        // 진행 중 전환이나 종료와 Title 복귀 요청의 중복 방지
        if (IsLoading || !IsSessionActive || _isQuitting)
            return;

        // 플레이 시간 계측을 끝내고 Title 복귀 시작
        EndSession();
        StartCoroutine(LoadTitle(null)); // 세션 정리·슬롯 재로드·Title 씬 전환 진행
    }

    /// <summary>게임 시간 변경을 단일 창구에서 적용</summary>
    public void SetGameplayPaused(bool paused)
    {
        IsGameplayPaused = paused;
        ApplyGameplayTime();
    }

    /// <summary>Draw가 소유한 정지만 변경하고 다른 정지 요청 유지</summary>
    public void SetDrawPaused(bool paused)
    {
        IsDrawPaused = paused;
        ApplyGameplayTime();
    }

    /// <summary>Slash의 정지만 변경하고 일반·Draw 정지 소유 유지</summary>
    public void SetSlashPaused(bool paused)
    {
        IsSlashPaused = paused;
        ApplyGameplayTime();
    }

    /// <summary>사망 연출의 정지만 변경하고 다른 정지 요청 유지</summary>
    public void SetDeathPaused(bool paused)
    {
        IsDeathPaused = paused;
        ApplyGameplayTime();
    }

    /// <summary>Boss 연출의 정지만 변경하고 다른 정지 소유 유지</summary>
    public void SetBossCutscenePaused(bool paused)
    {
        IsBossCutscenePaused = paused;
        ApplyGameplayTime();
    }

    /// <summary>저장·백업·복구가 끝난 뒤 앱 종료를 허용</summary>
    public void QuitGame()
    {
        // 종료 대기 코루틴의 중복 실행 방지
        if (_isQuitting)
            return;

        // 게임 진행을 멈추고 남은 저장 처리 후 종료
        _isQuitting = true;
        EndSession();
        SetGameplayPaused(true);
        Loading.Show();
        StartCoroutine(WaitForQuit()); // 저장 큐가 비워질 때까지 종료를 미루는 코루틴 실행
    }

    /// <summary>MainWorld 씬과 모든 세션 객체가 준비된 뒤 게임 시간을 시작</summary>
    private IEnumerator LoadWorld()
    {
        // 씬 로드 후 저장 데이터를 적용한 월드 생성 대기
        AsyncOperation operation = BeginSceneLoad("MainWorld");
        if (operation != null)
        {
            yield return operation; // MainWorld 씬 로드와 씬 객체의 초기화 완료 대기
            if (_isQuitting)
                yield break;
            yield return Stage.Generate(SaveManager.Current); // 저장 데이터로 Map·Boss·Player를 생성하고 연결할 때까지 대기
            if (_isQuitting)
                yield break;
            yield return EnterWorld(); // 로딩을 닫고 입장 또는 기상 뒤 조작 인계까지 대기
            if (!_enterFailed)
                yield break;
        }

        // 월드 준비 실패 시 세션을 끝내고 Title로 복구
        EndSession();
        yield return LoadTitle(LastError ?? "MainWorld initialization failed."); // 실패 안내와 세션 정리를 거쳐 Title로 복귀할 때까지 대기
    }

    /// <summary>Player는 유지하고 Map·Boss만 다음 묶음으로 바꾼 뒤 그 맵의 입구에서 게임 시간을 다시 시작</summary>
    private IEnumerator LoadNextStage()
    {
        // 로딩 화면이 한순간만 스치지 않도록 표시 시각을 잰 채 맵 교체
        double shownAt = Time.realtimeSinceStartupAsDouble;
        yield return Stage.Advance(SaveManager.Current); // Map·Boss를 다음 묶음으로 바꾸고 Player를 다시 연결할 때까지 대기
        while (!_isQuitting && Time.realtimeSinceStartupAsDouble - shownAt < StageLoadingSeconds)
            yield return null; // 다음 맵이 먼저 준비돼도 로딩 화면의 최소 표시 시간까지 대기
        if (_isQuitting)
            yield break;
        yield return EnterWorld(); // 로딩을 닫고 새 맵의 입장 뒤 조작 인계까지 대기
        if (!_enterFailed)
            yield break;

        // 맵 교체 실패 시 세션을 끝내고 Title로 복구
        EndSession();
        yield return LoadTitle(LastError ?? "Stage initialization failed."); // 실패 안내와 세션 정리를 거쳐 Title로 복귀할 때까지 대기
    }

    /// <summary>준비된 월드에서 로딩을 닫고 입장 또는 기상이 끝나면 조작을 넘기며 같은 세션 안의 실패만 표시</summary>
    private IEnumerator EnterWorld()
    {
        // 월드 생성이 실패했으면 호출자가 Title로 복구하도록 이유만 전달
        _enterFailed = true;
        if (!Stage.IsReady)
        {
            LastError = Stage.LastError;
            yield break;
        }

        // 미완 입장은 게임 시간을 먼저 열고 입장 담당에게, 그 외는 체크포인트 기상에 맡김
        PlayerCharacter initialPlayer = Stage.Player;
        bool entrance = Stage.Tutorial.HasPendingEntrance;
        Loading.Hide();
        if (entrance)
        {
            IsLoading = false;
            SetGameplayPaused(false);
            yield return Stage.Tutorial.PlayEntrance(); // 깨어나거나 걸어 들어오는 입장과 완료 기록까지 대기
        }
        else
            yield return initialPlayer.Spawner.PlayInitialSpawn(); // 기존 저장은 체크포인트 기상 입력 게이트까지 대기

        // 중단된 이전 세션이 Title이나 새 Player의 입력을 뒤늦게 열지 않도록 차단
        if (_isQuitting || !IsSessionActive || !Stage.IsReady || Stage.Player != initialPlayer)
        {
            _enterFailed = false;
            yield break;
        }
        if (entrance && !Stage.Tutorial.WasCompleted)
        {
            LastError = Stage.LastError = Stage.Tutorial.LastError ?? "Player entrance was cancelled.";
            yield break;
        }

        // 입장 또는 기상이 끝난 실제 Player로 조작 인계
        IsLoading = false;
        SetGameplayPaused(false); // 게임 시간 정지 해제
        UIController.Instance.SetPlayerHudVisible(true);
        PlayerInputController.Instance.BlockInput = false;
        _enterFailed = false;
    }

    /// <summary>세션을 정리하고 슬롯 데이터 재로드와 Title 씬 전환을 시작</summary>
    private IEnumerator LoadTitle(string error)
    {
        // 게임 진행을 잠그고 기존 세션 객체 정리
        IsLoading = true;
        LastError = error;
        SetGameplayPaused(true);
        Loading.Show();
        Stage.Clear();

        // Current는 유지하며 새 Title에서 선택할 슬롯 목록만 디스크에서 갱신
        SaveManager.LoadSlotsAsync().Forget(); // Title 씬 전환과 함께 최신 슬롯 목록 로드 시작
        yield return null;

        // Title 씬 전환을 마친 뒤 화면과 게임 시간 복구
        AsyncOperation operation = BeginSceneLoad("Title");
        if (operation != null)
        {
            yield return operation; // Title 씬 로드 완료 후 로딩 화면을 닫기 위한 대기
            if (_isQuitting)
                yield break;
            IsLoading = false;
            Loading.Hide();
            SetGameplayPaused(false);
            yield break;
        }

        // Title 로드 요청까지 실패하면 로딩 화면에 복구 불가 사유 표시
        IsLoading = false;
        Loading.ShowFailure(LastError);
    }

    /// <summary>일반 정지와 Draw 정지를 합쳐 게임 시간에 적용</summary>
    private void ApplyGameplayTime()
    {
        Time.timeScale = IsGameplayPaused || IsDrawPaused || IsSlashPaused || IsDeathPaused || IsBossCutscenePaused ? 0f : 1f;
    }

    /// <summary>누락된 씬과 로드 요청 예외를 무한 로딩 대신 오류로 변환</summary>
    private AsyncOperation BeginSceneLoad(string sceneName)
    {
        try
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException($"Scene is not in the build list: {sceneName}");
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            Debug.LogError(LastError);
            return null;
        }
    }

    /// <summary>세션 시간을 고정하며 디스크 저장은 확정된 저장 이벤트에서만 요청</summary>
    private void EndSession()
    {
        // 이미 끝난 세션의 시간 중복 반영 방지
        if (!IsSessionActive)
            return;

        // 누적 시간을 고정하고 입력 차단·메모리 저장값 갱신
        _savedPlayTime = SessionPlayTime;
        IsSessionActive = false;
        if (PlayerInputController.Instance != null)
            PlayerInputController.Instance.BlockInput = true;
        if (SaveManager.Current != null)
        {
            SaveManager.Current.Set("PlayTime", _savedPlayTime);
        }
    }

    /// <summary>운영체제 창 닫기도 진행 중 저장 완료를 기다리도록 연결</summary>
    private bool HandleQuitRequested()
    {
        if (_allowQuit)
            return true;
        QuitGame();
        return false;
    }

    /// <summary>시간 정지 중에도 저장 큐의 종료를 기다린 뒤 앱을 종료</summary>
    private IEnumerator WaitForQuit()
    {
        // 저장 중 파일이 끊기지 않도록 큐와 복구 처리 완료 대기
        yield return SaveManager.FlushAsync().ToCoroutine(); // 진행 중인 저장·백업·복구가 모두 끝날 때까지 종료 보류

        // 저장 완료 후 Editor 또는 빌드 실행 종료 허용
        _allowQuit = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
