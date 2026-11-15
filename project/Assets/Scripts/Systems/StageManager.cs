using System;
using System.Collections;
using UnityEngine;

/// <summary>MainWorld의 프리팹 생성·초기화 순서와 세션 수명을 관리</summary>
[RequireComponent(typeof(CutsceneController))]
public class StageManager : MonoBehaviour
{
    public MapManager MapPrefab; // 세션의 맵과 배치 정보를 생성할 원본 프리팹
    public PlayerCharacter PlayerPrefab; // 저장 상태를 적용할 Player의 원본 프리팹
    public BossCharacter BossPrefab; // 현재 제출 범위의 Boss를 생성할 원본 프리팹

    public MapManager Map; // 현재 세션에 생성한 맵의 배치 정보를 제공
    public PlayerCharacter Player; // 현재 세션에 생성하고 저장 상태를 적용한 Player
    public BossCharacter[] Bosses = Array.Empty<BossCharacter>(); // 현재 세션에서 생성한 Boss들을 보관
    public Transform SessionRoot; // 세션 종료 때 생성물을 함께 제거하기 위한 부모 객체
    public bool IsReady; // 모든 월드 객체의 초기화와 연결을 마쳐 게임을 시작할 수 있는지 표시
    public string LastError; // 월드 초기화 실패 이유를 GameManager에 전달
    public float FirstEncounterDuration = 0.6f; // 첫 조우의 임시 등장 연출 시간
    public float ReencounterDuration = 0.2f; // 재조우의 임시 준비 연출 시간
    public float BossDeathDuration = 0.5f; // 처치 뒤 사체·문 전환 전 임시 연출 시간
    public bool IsBossSequencePlaying { get; private set; } // 조우·사망 전환 중 중복 요청을 막는 상태
    public CutsceneController Cutscene { get; private set; } // 자기 앱 객체에 붙은 Boss 연출 담당
    private BossCharacter encounterBoss; // 현재 Player와 사망 사건을 연결한 전투 상대
    private Coroutine bossSequence; // 세션 이탈 때 중단할 조우·사망 코루틴
    private int sequenceVersion; // 취소된 연출이 뒤늦게 전투를 열지 않도록 구분
    private UIController ui; // 이번 MainWorld 수명 동안 유지할 HUD 참조

    /// <summary>같은 앱 객체의 연출 담당을 최초 생성 때 한 번 확보</summary>
    private void Awake() => Cutscene = GetComponent<CutsceneController>();

    private void OnDisable()
    {
        CancelBossSequence();
        EndEncounter();
    }

    /// <summary>MainWorld 씬의 공통 객체 등록을 확인한 뒤 Map·Boss·Player를 생성하고 연결</summary>
    public IEnumerator Generate(SaveData save)
    {
        // 이전 세션 정리 후 씬 서비스의 Start 참조 준비가 끝날 다음 프레임까지 대기
        Clear();
        LastError = null;
        yield return null;

        // 씬 서비스 준비를 확인한 뒤 세션 객체 생성·연결
        try
        {
            if (save == null || MapPrefab == null || PlayerPrefab == null || BossPrefab == null)
                throw new InvalidOperationException("MainWorld requires save data and Map, Player, Boss prefabs.");

            // 씬 서비스의 Awake·Start가 끝난 뒤 Player 생성에 필요한 대상 확인
            if (PlayerInputController.Instance == null || !PlayerInputController.Instance.isActiveAndEnabled || UIController.Instance == null || !UIController.Instance.isActiveAndEnabled || GameCamera.Instance == null || !GameCamera.Instance.isActiveAndEnabled || GameCamera.Instance.Camera == null)
                throw new InvalidOperationException("MainWorld input, UI, and camera must finish initialization before Player creation.");
            PlayerInputController.Instance.BlockInput = true;
            UIController.Instance.SetPlayerHudVisible(false);

            // 씬 서비스가 교체되는 세션 경계에서만 연출·HUD 참조 갱신
            ui = UIController.Instance;
            Cutscene.Prepare();

            // 부모가 없는 세션 루트를 활성 MainWorld 씬에 만들고 모든 복제본을 소유
            SessionRoot = new GameObject("MainWorldSession").transform;
            CreateMap(save);
            CreateBosses(save);
            CreatePlayer();
            Player.GetComponent<PlayerSpawner>().Bind();
            UIController.Instance.SetPlayerHudVisible(false);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }

        // 생성 실패 시 준비 완료 처리 생략
        if (LastError != null)
            yield break;

        // 복제본 초기화·연결을 확인하고 조우 영역과 HUD 활성화
        yield return null;
        try
        {
            if (Map == null || !Map.IsInitialized || Player == null || !Player.IsInitialized || !Player.Spawner.IsReady || Bosses.Length != 1 || Bosses[0] == null || !Bosses[0].IsInitialized || UIController.Instance == null || GameCamera.Instance == null || PlayerInputController.Instance == null || UIController.Instance.Player != Player || GameCamera.Instance.Target != Player.transform || PlayerInputController.Instance.Player != Player)
                throw new InvalidOperationException("A MainWorld object was not initialized or its binding was lost.");

            // 초기화가 모두 끝난 월드의 조우 감지와 Player 표시 허용
            foreach (BossCharacter boss in Bosses)
                boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = !boss.IsDead;
            UIController.Instance.SetPlayerHudVisible(true);
            IsReady = true;
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    /// <summary>Map 프리팹을 복제하고 저장된 체크포인트와 배치를 검증</summary>
    public MapManager CreateMap(SaveData save)
    {
        Map = CreatePrefab(MapPrefab);
        if (MapManager.Instance != Map)
            throw new InvalidOperationException("The created Map must be the active Map instance.");
        Map.Initialize(save);
        return Map;
    }

    /// <summary>현재 제출 범위의 Boss 한 명을 생성하고 처치 기록을 적용</summary>
    public BossCharacter[] CreateBosses(SaveData save)
    {
        BossCharacter boss = CreatePrefab(BossPrefab);
        Bosses = new[] { boss };
        boss.GetComponent<BossSpawner>().Initialize(Map, save);
        boss.OnDie += HandleBossDeath; // 처치 때 현재 Player 효과·기록·전투 연결 정리
        EncounterTrigger encounter = Map.GetEncounterArea(boss.BossId).GetComponent<EncounterTrigger>();
        if (encounter != null) encounter.Initialize(this, boss);
        return Bosses;
    }

    /// <summary>맵 검증 뒤 Player를 복제하고 Awake의 최초 초기화·배치 완료 확인</summary>
    public PlayerCharacter CreatePlayer()
    {
        // PlayerSpawner.Awake가 즉시 조회할 맵과 저장 데이터의 준비 보장
        if (Map == null || !Map.IsInitialized || MapManager.Instance != Map || SaveManager.Current == null)
            throw new InvalidOperationException("Player creation requires an initialized Map instance and selected save.");

        // Instantiate 중 실행되는 Awake가 끝난 결과를 확인하고 외부 바인딩에 전달
        Player = CreatePrefab(PlayerPrefab);
        PlayerSpawner spawner = Player.GetComponent<PlayerSpawner>();
        if (!Player.IsInitialized || spawner.Player != Player || spawner.SpawnPoint == null)
            throw new InvalidOperationException("Player Awake initialization or checkpoint placement failed.");
        return Player;
    }

    /// <summary>생성된 모든 프리팹을 현재 세션 루트 아래에 배치</summary>
    public T CreatePrefab<T>(T prefab) where T : Component
    {
        if (prefab == null || SessionRoot == null)
            throw new InvalidOperationException("Prefab creation requires a prefab and an active session root.");
        return Instantiate(prefab, SessionRoot);
    }

    /// <summary>Title 복귀·실패 시 연결을 해제하고 세션 객체를 모두 제거</summary>
    public void Clear()
    {
        CancelBossSequence();
        EndEncounter();
        ClearBossHazards();
        foreach (BossCharacter boss in Bosses)
            if (boss != null) boss.OnDie -= HandleBossDeath; // 세션을 떠난 Boss의 처치 처리 해제
        // 준비 상태를 취소하고 세션 개체와 공유 서비스의 대상 해제
        IsReady = false;
        if (Player != null)
            Player.GetComponent<PlayerSpawner>()?.Unbind();
        foreach (BossCharacter boss in Bosses)
            if (boss != null)
                boss.GetComponent<BossSpawner>()?.Release();
        if (PlayerInputController.Instance != null)
        {
            PlayerInputController.Instance.BlockInput = true;
            PlayerInputController.Instance.ClearPlayer();
        }
        if (UIController.Instance != null)
            UIController.Instance.Unbind();
        if (GameCamera.Instance != null)
            GameCamera.Instance.Follow(null);

        // 독립 씬 서비스는 유지하고 이번 세션의 생성물만 제거
        if (SessionRoot != null)
        {
            SessionRoot.gameObject.SetActive(false);
            Destroy(SessionRoot.gameObject);
        }
        SessionRoot = null;
        Map = null;
        Player = null;
        Bosses = Array.Empty<BossCharacter>();
    }

    /// <summary>실제 전투 진입에서만 상대 사망·HUD·문을 연결</summary>
    public void BeginEncounter(BossCharacter boss)
    {
        // 중복 진입과 준비되지 않은 외부 대상을 전환 시작 전에 차단
        if (!IsReady || IsBossSequencePlaying || encounterBoss != null || boss == null || boss.IsDead
            || Array.IndexOf(Bosses, boss) < 0 || Player == null || Player.IsDead || Player.IsCutScene) return;
        encounterBoss = boss;
        Player.OnDie += HandlePlayerDeath; // Player 사망에서 연출·전투 연결 종료
        boss.OnDie += EndEncounter; // 상대 사망에서 전투 연결만 종료
        boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = false;
        IsBossSequencePlaying = true;
        bossSequence = StartCoroutine(PlayEncounter(boss, ++sequenceVersion)); // 첫 조우·재조우 연출 완료 뒤 전투 시작
    }

    /// <summary>입력·HUD의 Player 연결은 유지하고 전투 상대의 사건만 해제</summary>
    public void EndEncounter()
    {
        if (Player != null)
        {
            Player.OnDie -= HandlePlayerDeath; // 종료된 전투의 Player 사망 연결 해제
            Player.IsInCombat = false;
        }
        if (encounterBoss != null)
        {
            encounterBoss.OnDie -= EndEncounter; // 종료된 전투 상대의 사망 연결 해제
            encounterBoss.EndEncounter();
        }
        encounterBoss = null;
        if (ui != null) ui.ClearBoss();
    }

    /// <summary>사망 복구에서 같은 Boss·수정·문을 재도전 상태로 복원</summary>
    public void ResetEncounter()
    {
        CancelBossSequence();
        EndEncounter();
        ClearBossHazards();
        foreach (BossCharacter boss in Bosses)
            if (boss != null) boss.GetComponent<BossSpawner>().ResetEncounter();
        if (Map != null) Map.RestoreCheckpointDoors();
    }

    /// <summary>Boss 처치 사건에서 저장·디버프·자기 표시만 정리</summary>
    private void HandleBossDeath()
    {
        BossCharacter boss = Bosses.Length > 0 ? Bosses[0] : null;
        if (boss == null || SaveManager.Current == null) return;
        var defeated = SaveManager.Current.Get<System.Collections.Generic.List<string>>("DefeatedBosses");
        if (defeated.Contains(boss.BossId)) return;
        defeated.Add(boss.BossId);
        CancelBossSequence();
        EndEncounter();
        ClearBossHazards();
        if (Player != null)
        {
            Player.Buffs.ClearDebuffs();
            if (!Player.IsDead) SaveManager.Current.Set("PlayerHP", Player.HP);
        }
        SaveManager.Save();
        boss.GetComponent<BossSpawner>().DisableCombatColliders();
        IsBossSequencePlaying = true;
        bossSequence = StartCoroutine(PlayBossDeath(boss, ++sequenceVersion)); // 처치 연출 뒤 사체 표시와 입구·출구 개방
    }

    /// <summary>첫 조우 완료만 저장하고 연출 뒤 문·HUD·양쪽 행동 시작</summary>
    private IEnumerator PlayEncounter(BossCharacter boss, int ticket)
    {
        var encountered = SaveManager.Current.Get<System.Collections.Generic.List<string>>("EncounteredBosses");
        bool first = !encountered.Contains(boss.BossId);
        yield return Cutscene.Play(Player, boss, first ? CutsceneKind.BossEncounter : CutsceneKind.BossReencounter,
            first ? FirstEncounterDuration : ReencounterDuration); // 등장 연출의 정상 완료와 표시 복구 대기
        if (ticket != sequenceVersion || !IsReady || boss == null || boss.IsDead || Player == null || Player.IsDead) yield break;
        if (!Cutscene.WasCompleted)
        {
            IsBossSequencePlaying = false;
            bossSequence = null;
            EndEncounter();
            boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = true;
            yield break;
        }

        // 저장 요청 완료는 기다리지 않고 준비가 끝난 전투로 전환
        if (first)
        {
            encountered.Add(boss.BossId);
            SaveManager.Save();
        }
        IsBossSequencePlaying = false;
        bossSequence = null;
        Map.SetEntryDoorOpen(boss.BossId, false);
        Map.SetExitDoorOpen(boss.BossId, false);
        ui.SetBoss(boss);
        Player.IsInCombat = true;
        boss.BeginEncounter(Player);
    }

    /// <summary>사망 연출 뒤 같은 프리팹의 사체 표시와 통로 개방</summary>
    private IEnumerator PlayBossDeath(BossCharacter boss, int ticket)
    {
        yield return Cutscene.Play(Player, boss, CutsceneKind.BossDeath, BossDeathDuration); // 사망 초점 연출이 끝난 뒤 통로 복구
        if (ticket != sequenceVersion || boss == null || Map == null) yield break;
        boss.GetComponent<BossSpawner>().ShowDefeated();
        Map.SetEntryDoorOpen(boss.BossId, true);
        Map.SetExitDoorOpen(boss.BossId, true);
        IsBossSequencePlaying = false;
        bossSequence = null;
    }

    /// <summary>Player 사망에서 추가 발사와 조우 완료를 막고 필드 위험물 제거</summary>
    private void HandlePlayerDeath()
    {
        CancelBossSequence();
        EndEncounter();
        ClearBossHazards();
    }

    /// <summary>취소된 연출의 후속 저장·전투 시작을 막고 자기 표시 복구</summary>
    public void CancelBossSequence()
    {
        // 공개 중단도 조우 대상·구독·재진입 영역까지 일관되게 복구
        BossCharacter pendingBoss = IsBossSequencePlaying ? encounterBoss : null;
        bool interruptedDeath = IsBossSequencePlaying && Cutscene != null && Cutscene.CurrentKind == CutsceneKind.BossDeath;
        sequenceVersion++;
        if (bossSequence != null) StopCoroutine(bossSequence); // 이전 조우·사망 실행의 후속 처리 차단
        bossSequence = null;
        IsBossSequencePlaying = false;
        if (Cutscene != null) Cutscene.Cancel();
        if (pendingBoss != null)
        {
            EndEncounter();
            Collider2D trigger = pendingBoss.GetComponent<BossSpawner>().EncounterTrigger;
            if (trigger != null) trigger.enabled = !pendingBoss.IsDead;
        }
        if (interruptedDeath && Map != null)
            foreach (BossCharacter boss in Bosses)
                if (boss != null && boss.IsDead)
                {
                    boss.GetComponent<BossSpawner>().ShowDefeated();
                    Map.SetEntryDoorOpen(boss.BossId, true);
                    Map.SetExitDoorOpen(boss.BossId, true);
                }
    }

    /// <summary>전투 종료 경계에서만 세션 Boss가 만든 탄환·레이저·장판 정리</summary>
    private void ClearBossHazards()
    {
        foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (Array.IndexOf(Bosses, projectile.Owner) >= 0) projectile.Despawn();
    }

    /// <summary>초기화 실패 이유를 보존하고 GameManager가 Title로 복귀할 수 있게 정리</summary>
    private void HandleFailure(Exception exception)
    {
        LastError = exception.Message;
        Clear();
        Debug.LogError($"MainWorld initialization failed: {exception.Message}", this);
    }
}
