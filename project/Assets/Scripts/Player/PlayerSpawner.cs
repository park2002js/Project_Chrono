using System;
using System.Collections;
using UnityEngine;

/// <summary>StageManager가 만든 Player에 저장 체력·위치를 적용하고 입력·HUD·카메라 연결</summary>
public class PlayerSpawner : MonoBehaviour
{
    public PlayerCharacter Player; // 저장 수치를 적용하고 입력·HUD·카메라에 연결할 Player 인스턴스
    public Transform SpawnPoint; // Player를 배치할 체크포인트의 위치와 회전 기준
    public bool IsReady { get; private set; } // 씬 서비스와 생명주기 구독의 준비 완료 여부
    public bool IsSpawning { get; private set; } // 같은 Player가 생성·기상 표현을 마치기 전인 상태
    public bool IsRespawning { get; private set; } // 사망에서 자동 부활 복구까지 진행 중인 상태
    public float DeathDuration = 0.8f; // 엎드림 뒤 사망 문구를 유지할 실제 시간

    private MapManager map; // Player보다 먼저 생성·검증된 세션 맵의 배치 조회 대상
    private Rigidbody2D body; // 체크포인트 이동 때 Transform과 위치를 맞출 물리 몸체
    private GameManager manager; // Start에서 한 번 확보할 세션·정지 관리자
    private StageManager stage; // 같은 세션의 Boss·전투 상태 복구 담당
    private PlayerInputController input; // 교체하지 않고 잠금만 전환할 MainWorld 입력 서비스
    private GameCamera gameCamera; // 생성·사망 초점과 기존 카메라 복구 담당
    private UI_Death lifeView; // 임시 생성·사망 표시와 흑백·암전 담당
    private CharacterRendering rendering; // 같은 Player의 생성·사망 Sprite를 재생할 표시 컴포넌트
    private Coroutine deathRoutine; // 중단 시 종료할 자기 사망·부활 실행
    private bool subscribed; // Start·재활성화에서 생명주기 구독 중복 방지
    private bool presentationActive; // 자기 생명주기 표시를 한 번만 복구할 소유 상태
    private bool previousInputBlock; // 연출 전 입력 차단을 보존할 상태
    private int lifecycleVersion; // 중단된 실행이 다음 실행의 표시를 복구하지 않도록 구분

    /// <summary>프리팹 복제 직후 저장 체력과 기본 상태를 적용하고 체크포인트에 배치</summary>
    private void Awake()
    {
        // 맵 준비가 끝난 뒤 복제된 Player의 고정 참조 확보
        Player = GetComponent<PlayerCharacter>();
        body = GetComponent<Rigidbody2D>();
        rendering = GetComponent<CharacterRendering>();
        map = MapManager.Instance;
        Player.IsInitialized = false;
        if (map == null || !map.IsInitialized || SaveManager.Current == null)
            throw new InvalidOperationException("Player must be created after Map initialization and save selection.");

        // 저장 체력을 복원하고 기본 이동속도·비전투 상태 설정
        Player.InitializeHealth(SaveManager.Current.Get<float>("PlayerHP"));
        Player.SP = Player.basicSP;
        Player.IsCutScene = false;
        Player.IsInCombat = false;

        // 외부 초기화 호출 없이 최초 저장 체크포인트에 배치
        UpdateCheckpoint(SaveManager.Current.Get<string>("CheckpointId"));
        playerSpawn();
    }

    /// <summary>모든 씬 서비스가 준비된 뒤 고정 참조·생명주기 구독 연결</summary>
    private void Start()
    {
        manager = GameManager.Instance;
        stage = manager != null ? manager.Stage : null;
        input = PlayerInputController.Instance;
        gameCamera = GameCamera.Instance;
        UIController ui = UIController.Instance;
        lifeView = ui != null ? ui.GetComponent<UI_Death>() : null;
        IsReady = manager != null && stage != null && input != null && gameCamera != null && lifeView != null;
        if (!IsReady)
        {
            Debug.LogError($"{name}: Player lifecycle requires prepared scene services and UI_Death.", this);
            return;
        }
        if (input.Player == Player) lifeView.Bind(Player, stage.SessionRoot);
        Subscribe();
    }

    private void OnEnable()
    {
        if (IsReady) Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelLifecycle();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>현재 저장 체크포인트로 부활 위치를 조회해 기존 Player 배치</summary>
    public void playerSpawn()
    {
        // 체크포인트와 상호작용할 때 로컬 위치를 갱신해 같은 맵의 배열 재조회 생략
        if (SpawnPoint == null) throw new InvalidOperationException("Player checkpoint is not prepared.");

        PlacePlayer(SpawnPoint);
    }

    /// <summary>최초 입장이나 부활의 지정 위치로 배치하되 저장된 체크포인트 참조는 유지</summary>
    public void PlacePlayer(Transform point)
    {
        if (point == null) throw new ArgumentNullException(nameof(point));

        // Transform·물리 위치와 기본 조준 위치를 지정한 배치점에 맞춰 동기화
        Player.ResetMotion();
        base.transform.position = point.position;
        base.transform.rotation = point.rotation;
        body.position = point.position;
        Player.SetAimPosition((Vector2)point.position + Player.AimDirection);
        if (gameCamera != null && gameCamera.Target == Player.transform) gameCamera.Follow(Player.transform, true);
    }

    /// <summary>씬의 Awake가 끝난 입력·HUD·카메라를 직접 조회해 자신의 Player 연결</summary>
    public void Bind()
    {
        // Player와 공유 객체의 초기화 여부 확인
        if (Player == null || UIController.Instance == null || GameCamera.Instance == null || PlayerInputController.Instance == null)
            throw new InvalidOperationException("Player binding requires initialized Player and MainWorld services.");

        // 공유 객체의 대상을 연결하고 월드 준비 완료 전까지 입력 차단
        UIController.Instance.SetPlayer(Player);
        GameCamera.Instance.Follow(Player.transform, true);
        PlayerInputController.Instance.SetPlayer(Player);
        map.BindPlayer(this);
        if (IsReady) lifeView.Bind(Player, stage.SessionRoot);
    }

    /// <summary>세션 종료 시 자신을 가리키는 입력·HUD·카메라 연결 해제</summary>
    public void Unbind()
    {
        CancelLifecycle();
        // 새 Player로 교체된 연결은 보존하고 자기 대상만 해제
        if (PlayerInputController.Instance != null && PlayerInputController.Instance.Player == Player)
            PlayerInputController.Instance.ClearPlayer();
        if (UIController.Instance != null && UIController.Instance.Player == Player)
            UIController.Instance.SetPlayer(null);
        if (GameCamera.Instance != null && GameCamera.Instance.Target == transform)
            GameCamera.Instance.Follow(null);

        // 이전 체크포인트 배치 참조 정리
        SpawnPoint = null;
    }

    /// <summary>새 수정 활성화나 최초 로드에서 다음 배치 위치를 한 번 갱신</summary>
    public void UpdateCheckpoint(string checkpointId)
    {
        SpawnPoint = map.GetCheckpoint(checkpointId);
    }

    /// <summary>같은 Player를 새로 만든 맵의 체크포인트에 다시 연결하고 그 위치로 배치</summary>
    public void MoveToMap(MapManager next, string checkpointId)
    {
        if (next == null || !next.IsInitialized)
            throw new InvalidOperationException("Player can only move to an initialized Map.");

        // 이전 맵의 배치 참조를 버리고 새 맵의 저장 체크포인트를 한 번 조회
        map = next;
        UpdateCheckpoint(checkpointId);
        playerSpawn();
    }

    /// <summary>로딩 화면이 닫힌 뒤 최초 생성 표현·입력 게이트 진행</summary>
    public IEnumerator PlayInitialSpawn()
    {
        if (!IsReady) throw new InvalidOperationException("Player lifecycle must be ready before initial spawn.");
        int version = ++lifecycleVersion;
        BeginPresentation(false);
        IsSpawning = true;
        if (Player.IsDead) Player.Revive();
        if (version != lifecycleVersion || !presentationActive) yield break;
        double end = Time.unscaledTimeAsDouble + rendering.BeginLifecycleAnimation(false);
        while (version == lifecycleVersion && Time.unscaledTimeAsDouble < end) yield return null;
        if (version == lifecycleVersion) EndPresentation();
    }

    /// <summary>새 게임의 첫 화면에서 누운 자세를 잠시 보인 뒤 일어나고 화면이 평소 시야로 천천히 돌아올 때까지 진행</summary>
    public IEnumerator PlayWakeUp(float holdSeconds, float returnSeconds)
    {
        if (!IsReady) throw new InvalidOperationException("Player lifecycle must be ready before waking up.");
        int version = ++lifecycleVersion;
        BeginPresentation(false);
        gameCamera.SnapDeathZoom();
        IsSpawning = true;
        if (Player.IsDead) Player.Revive();
        if (version != lifecycleVersion || !presentationActive) yield break;

        // 누운 첫 장을 유지한 뒤 원본 시간대로 일어나는 모션 재생
        double end = Time.unscaledTimeAsDouble + rendering.BeginLifecycleAnimation(false, holdSeconds);
        while (version == lifecycleVersion && Time.unscaledTimeAsDouble < end) yield return null;
        if (version != lifecycleVersion) yield break;

        // 평소 자세와 밝은 월드로 돌아온 뒤 가까운 시야에서 평소 시야로 천천히 복귀
        rendering.EndLifecycleAnimation();
        lifeView.Hide();
        double start = Time.unscaledTimeAsDouble;
        float duration = Mathf.Max(0.01f, returnSeconds);
        while (version == lifecycleVersion)
        {
            float progress = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - start) / duration));
            gameCamera.SetDeathZoomReturn(Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) break;
            yield return null;
        }
        if (version == lifecycleVersion) EndPresentation();
    }

    /// <summary>세션 종료·비활성화에서 진행 중 표시·정지 소유 해제</summary>
    public void CancelLifecycle()
    {
        lifecycleVersion++;
        if (deathRoutine != null) StopCoroutine(deathRoutine); // 종료된 Player의 지연 부활 중단
        deathRoutine = null;
        EndPresentation();
    }

    /// <summary>Player의 생명주기 사건에 자기 후속 처리만 연결</summary>
    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        Player.OnDie += HandleDeath; // 실제 사망 한 번에서 저장·연출·자동 복구 시작
        Player.OnRevive += playerSpawn; // 부활 상태 확정 뒤 같은 인스턴스를 수정 위치에 배치
    }

    /// <summary>비활성화·제거에서 자기 생명주기 구독만 해제</summary>
    private void Unsubscribe()
    {
        if (!subscribed || Player == null) return;
        subscribed = false;
        Player.OnDie -= HandleDeath; // 제거된 Spawner의 사망 실행 방지
        Player.OnRevive -= playerSpawn; // 제거된 Spawner의 재배치 연결 해제
    }

    /// <summary>연결된 세션 Player의 실제 사망만 한 번 기록하고 후속 실행</summary>
    private void HandleDeath()
    {
        if (!isActiveAndEnabled || !IsReady || IsRespawning || input.Player != Player) return;
        SaveManager.Current.Set("DeathCount", SaveManager.Current.Get<int>("DeathCount") + 1);
        SaveManager.Save();
        IsRespawning = true;
        int version = ++lifecycleVersion;
        deathRoutine = StartCoroutine(Respawn(version)); // 사망·기상 표현 뒤 같은 Player 자동 복구
    }

    /// <summary>실제 시간의 사망·기상 표현 사이에 전투·Player를 복구</summary>
    private IEnumerator Respawn(int version)
    {
        BeginPresentation(true);
        double end = Time.unscaledTimeAsDouble + rendering.BeginLifecycleAnimation(true);
        while (version == lifecycleVersion && Time.unscaledTimeAsDouble < end) yield return null;
        if (version != lifecycleVersion) yield break;

        // 마지막 엎드림 자세를 유지하며 부활 시점 대기
        end = Time.unscaledTimeAsDouble + Mathf.Max(0.01f, DeathDuration);
        while (version == lifecycleVersion && Time.unscaledTimeAsDouble < end) yield return null;
        if (version != lifecycleVersion) yield break;

        // 저장된 처치·수정·첫 조우는 유지하고 재도전할 살아 있는 전투만 복원
        stage.ResetEncounter();
        IsSpawning = true;
        Player.Revive();
        if (version != lifecycleVersion || !presentationActive) yield break;
        lifeView.Show(false);
        end = Time.unscaledTimeAsDouble + rendering.BeginLifecycleAnimation(false);
        while (version == lifecycleVersion && Time.unscaledTimeAsDouble < end) yield return null;
        if (version == lifecycleVersion)
        {
            EndPresentation();
            deathRoutine = null;
        }
    }

    /// <summary>기존 입력·정지와 구분해 자기 생명주기 표시 시작</summary>
    private void BeginPresentation(bool dead)
    {
        if (!presentationActive)
        {
            previousInputBlock = input.BlockInput;
            presentationActive = true;
        }
        input.BlockInput = true;
        manager.SetDeathPaused(true);
        gameCamera.SetDeathMode(true, Player.transform);
        lifeView.Show(dead);
    }

    /// <summary>자기 정지·표시만 해제하고 기존 Player 연결과 외부 차단 보존</summary>
    private void EndPresentation()
    {
        IsSpawning = IsRespawning = false;
        if (rendering != null) rendering.EndLifecycleAnimation();
        if (!presentationActive) return;
        presentationActive = false;
        if (lifeView != null) lifeView.Hide();
        if (gameCamera != null) gameCamera.SetDeathMode(false);
        if (manager != null) manager.SetDeathPaused(false);
        if (input != null && input.Player == Player) input.BlockInput = previousInputBlock;
    }

}
