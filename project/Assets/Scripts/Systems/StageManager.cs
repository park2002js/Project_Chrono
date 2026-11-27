using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>MainWorld의 프리팹 생성·초기화 순서와 세션 수명을 관리</summary>
[RequireComponent(typeof(CutsceneController), typeof(TutorialController))]
[RequireComponent(typeof(TempDemoEnding), typeof(BattleMusic))]
public class StageManager : MonoBehaviour
{
    /// <summary>한 세션에 함께 생성할 맵과 Boss의 짝</summary>
    [Serializable]
    public class StageSet
    {
        public string Name; // Inspector에서 구분할 이름
        public MapManager MapPrefab; // 이 묶음에서 생성할 맵
        public BossCharacter BossPrefab; // 이 묶음에서 생성할 Boss
    }

    public MapManager MapPrefab; // 세션의 맵과 배치 정보를 생성할 원본 프리팹
    public PlayerCharacter PlayerPrefab; // 저장 상태를 적용할 Player의 원본 프리팹
    public BossCharacter BossPrefab; // 현재 제출 범위의 Boss를 생성할 원본 프리팹
    public StageSet[] Stages = Array.Empty<StageSet>(); // 골라 쓸 수 있는 맵·Boss 묶음
    public int StageIndex = -1; // 새 게임을 시작할 묶음의 번호이며 범위 밖이면 위의 MapPrefab·BossPrefab 사용
    public int CurrentStageIndex { get; private set; } = -1; // 지금 생성해 둔 묶음의 번호이며 저장의 StageIndex를 따름
    public bool HasNextStage => CurrentStageIndex >= 0 && TryGetStage(CurrentStageIndex + 1, out _); // 지금 묶음의 문 너머로 넘어갈 다음 묶음이 있는지 표시
    public MapManager ActiveMapPrefab => TryGetStage(CurrentStageIndex, out StageSet stage) ? stage.MapPrefab : MapPrefab; // 지금 묶음의 맵 원본
    public BossCharacter ActiveBossPrefab => TryGetStage(CurrentStageIndex, out StageSet stage) ? stage.BossPrefab : BossPrefab; // 지금 묶음의 Boss 원본

    public MapManager Map; // 현재 세션에 생성한 맵의 배치 정보를 제공
    public PlayerCharacter Player; // 현재 세션에 생성하고 저장 상태를 적용한 Player
    public BossCharacter[] Bosses = Array.Empty<BossCharacter>(); // 현재 세션에서 생성한 Boss들을 보관
    public Transform SessionRoot; // 세션 종료 때 생성물을 함께 제거하기 위한 부모 객체
    public bool IsReady; // 모든 월드 객체의 초기화와 연결을 마쳐 게임을 시작할 수 있는지 표시
    public string LastError; // 월드 초기화 실패 이유를 GameManager에 전달
    public float FirstEncounterDuration = 1.8f; // 첫 조우에서 카메라가 Boss에 닿아 머무는 등장 연출의 실제 시간
    public float ReencounterDuration = 1f; // 재조우에서 Boss를 다시 비추는 준비 연출의 실제 시간
    public float BossDeathDuration = 1.2f; // 처치 뒤 문 전환 전에 Boss를 비추는 연출의 최소 실제 시간이며 몸이 사라지는 Boss는 그 연출이 끝날 때까지 늘어남
    public bool IsBossSequencePlaying { get; private set; } // 조우·사망 전환 중 중복 요청을 막는 상태
    public CutsceneController Cutscene { get; private set; } // 자기 앱 객체에 붙은 Boss 연출 담당
    public TutorialController Tutorial { get; private set; } // 실제 Player의 새 게임 최초 입장을 관리할 자기 연출 담당
    public TempDemoEnding DemoEnding { get; private set; } // 경비 처치 뒤 임시 데모 엔딩을 연결할 자기 담당
    public BattleMusic Music { get; private set; } // 전투 시작과 종료에 맞춰 Boss의 배경음을 켜고 끌 자기 재생기
    private BossCharacter encounterBoss; // 현재 Player와 사망 사건을 연결한 전투 상대
    private Coroutine bossSequence; // 세션 이탈 때 중단할 조우·사망 코루틴
    private int sequenceVersion; // 취소된 연출이 뒤늦게 전투를 열지 않도록 구분
    private UIController ui; // 이번 MainWorld 수명 동안 유지할 HUD 참조
    private GameManager manager; // 같은 앱 객체에서 로딩 화면과 세션을 맡는 관리자

    /// <summary>같은 앱 객체의 연출 담당을 최초 생성 때 한 번 확보</summary>
    private void Awake()
    {
        manager = GetComponent<GameManager>();
        Cutscene = GetComponent<CutsceneController>();
        Tutorial = GetComponent<TutorialController>();
        DemoEnding = GetComponent<TempDemoEnding>();
        Music = GetComponent<BattleMusic>();
        if (Music == null) Music = gameObject.AddComponent<BattleMusic>(); // 프리팹에 빠져 있어도 재생기 확보
    }

    private void OnDisable()
    {
        if (DemoEnding != null) DemoEnding.Unbind();
        if (Tutorial != null) Tutorial.Cancel();
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
            // 저장에 기록된 묶음을 따르고 기록이 없는 예전 저장은 새 게임의 시작 묶음 사용
            CurrentStageIndex = save != null && save.Values.ContainsKey("StageIndex") ? save.Get<int>("StageIndex") : StageIndex;
            if (save == null || ActiveMapPrefab == null || PlayerPrefab == null || ActiveBossPrefab == null)
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
            Tutorial.Prepare();
            DemoEnding.Bind();
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
            ConfirmWorld();
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    /// <summary>Player는 그대로 두고 Map과 Boss만 다음 묶음으로 바꾼 뒤 새 맵의 체크포인트와 입구에 연결</summary>
    public IEnumerator Advance(SaveData save)
    {
        // 준비 상태를 내리고 떠나는 맵의 전투 연결·Boss·Map 정리
        LastError = null;
        IsReady = false;
        try
        {
            if (save == null || !HasNextStage || Player == null || Player.IsDead || SessionRoot == null)
                throw new InvalidOperationException("Stage advance requires save data, a next stage and a living Player.");
            ReleaseMapAndBosses();

            // 유지한 Player의 이동·능력·약화 효과를 비우고 다음 묶음의 시작 기록 저장 요청
            Player.ResetMotion();
            if (Player.Skills != null) Player.Skills.ResetSkills();
            if (Player.Buffs != null) Player.Buffs.ClearDebuffs();
            CurrentStageIndex++;
            save.Set("StageIndex", CurrentStageIndex);
            save.Set("CheckpointId", "start");
            save.Set("ActivatedCheckpoints", new List<string> { "start" });
            save.Set("EntranceCompleted", false);
            save.Set("PlayerHP", Player.HP);
            SaveManager.Save();
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        if (LastError != null)
            yield break;
        yield return null; // 떠난 Map의 OnDestroy가 전역 조회 창구를 비울 때까지 대기

        // 다음 묶음의 Map·Boss를 만들고 유지한 Player를 새 맵에 다시 연결
        try
        {
            CreateMap(save);
            CreateBosses(save);
            PlayerSpawner spawner = Player.Spawner;
            spawner.MoveToMap(Map, save.Get<string>("CheckpointId"));
            spawner.Bind();
            Tutorial.Prepare();
            DemoEnding.Bind();
            UIController.Instance.SetPlayerHudVisible(false);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        if (LastError != null)
            yield break;

        // 새 복제본의 Start가 끝난 다음 프레임에 연결을 확인하고 조우 영역 활성화
        yield return null;
        try
        {
            ConfirmWorld();
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    /// <summary>Map 프리팹을 복제하고 저장된 체크포인트와 배치를 검증</summary>
    public MapManager CreateMap(SaveData save)
    {
        Map = CreatePrefab(ActiveMapPrefab);
        if (MapManager.Instance != Map)
            throw new InvalidOperationException("The created Map must be the active Map instance.");
        Map.Initialize(save);
        if (Map.Exit != null) Map.Exit.Initialize(this);

        // 맵이 자기 세로 범위를 지정했으면 카메라가 그 안에서만 움직이도록 교체
        if (Map.CameraVerticalLimits.y > Map.CameraVerticalLimits.x) GameCamera.Instance.VerticalLimits = Map.CameraVerticalLimits;
        return Map;
    }

    /// <summary>현재 제출 범위의 Boss 한 명을 생성하고 처치 기록을 적용</summary>
    public BossCharacter[] CreateBosses(SaveData save)
    {
        BossCharacter boss = CreatePrefab(ActiveBossPrefab);
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
        if (DemoEnding != null) DemoEnding.Unbind();
        if (Tutorial != null) Tutorial.Cancel();
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
        if (Music != null) Music.Stop();
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

    /// <summary>처치가 끝난 맵의 문에 Player가 들어오면 다음 묶음으로 넘기거나 마지막 묶음의 엔딩 시작</summary>
    public void EnterExit()
    {
        // 전투·연출·전환 중이거나 살아 있는 Boss가 남은 맵에서는 떠나지 않음
        if (!IsReady || IsBossSequencePlaying || encounterBoss != null || manager.IsLoading
            || Player == null || Player.IsDead || Player.IsCutScene) return;
        foreach (BossCharacter boss in Bosses)
            if (boss == null || !boss.IsDead) return;

        // 다음 묶음이 있으면 로딩 뒤에서 맵을 바꾸고 없으면 데모 엔딩으로 인계
        if (HasNextStage) manager.AdvanceStage();
        else DemoEnding.Play();
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
        Music.Play(boss.BattleMusic);
    }

    /// <summary>사망 연출 동안 몸이 사라지는 Boss를 투명하게 만들고 연출 뒤 처치된 모습 확정과 통로 개방</summary>
    private IEnumerator PlayBossDeath(BossCharacter boss, int ticket)
    {
        // 몸이 사라지는 Boss는 다 사라지고 빈 자리를 비출 때까지 연출을 이어 감
        BossSpawner spawner = boss.GetComponent<BossSpawner>();
        spawner.BeginDefeatFade();
        yield return Cutscene.Play(Player, boss, CutsceneKind.BossDeath, Mathf.Max(BossDeathDuration, spawner.DefeatSequenceDuration)); // 사망 초점 연출이 끝난 뒤 통로 복구
        if (ticket != sequenceVersion || boss == null || Map == null) yield break;
        spawner.ShowDefeated();
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

    /// <summary>복제본 초기화·연결을 확인하고 조우 영역과 HUD를 열어 준비 완료 표시</summary>
    private void ConfirmWorld()
    {
        // 걸어 들어오는 미완 입장 중에는 준비된 고정 시점, 그 외에는 Player 추적 연결 확인
        if (Map == null || !Map.IsInitialized || Player == null || !Player.IsInitialized || !Player.Spawner.IsReady || Bosses.Length != 1 || Bosses[0] == null || !Bosses[0].IsInitialized || UIController.Instance == null || GameCamera.Instance == null || PlayerInputController.Instance == null || UIController.Instance.Player != Player || GameCamera.Instance.Target != (Tutorial.HasPendingEntrance && Tutorial.WalksIn ? Map.EntranceStopPoint : Player.transform) || PlayerInputController.Instance.Player != Player)
            throw new InvalidOperationException("A MainWorld object was not initialized or its binding was lost.");

        // 초기화가 모두 끝난 월드의 조우 감지와 Player 표시 허용
        foreach (BossCharacter boss in Bosses)
            boss.GetComponent<BossSpawner>().EncounterTrigger.enabled = !boss.IsDead;
        UIController.Instance.SetPlayerHudVisible(!Tutorial.HasPendingEntrance);
        IsReady = true;
    }

    /// <summary>Player와 세션 루트는 남기고 지금 맵의 전투 연결·위험물·Boss·Map만 제거</summary>
    private void ReleaseMapAndBosses()
    {
        // 떠나는 맵의 연출·전투 연결과 Boss가 만든 위험물부터 정리
        DemoEnding.Unbind();
        Tutorial.Cancel();
        CancelBossSequence();
        EndEncounter();
        ClearBossHazards();

        // Boss와 Map은 같은 프레임에 꺼 두고 프레임 끝에 제거
        foreach (BossCharacter boss in Bosses)
        {
            if (boss == null) continue;
            boss.OnDie -= HandleBossDeath; // 떠나는 맵의 Boss 처치 처리 해제
            boss.GetComponent<BossSpawner>()?.Release();
            boss.gameObject.SetActive(false);
            Destroy(boss.gameObject);
        }
        Bosses = Array.Empty<BossCharacter>();
        if (Map != null)
        {
            Map.gameObject.SetActive(false);
            Destroy(Map.gameObject);
        }
        Map = null;
    }

    /// <summary>번호가 가리키는 묶음에 맵과 Boss가 모두 있으면 그 묶음을 반환</summary>
    private bool TryGetStage(int index, out StageSet stage)
    {
        stage = Stages != null && index >= 0 && index < Stages.Length ? Stages[index] : null;
        return stage != null && stage.MapPrefab != null && stage.BossPrefab != null;
    }

    /// <summary>초기화 실패 이유를 보존하고 GameManager가 Title로 복귀할 수 있게 정리</summary>
    private void HandleFailure(Exception exception)
    {
        LastError = exception.Message;
        Clear();
        Debug.LogError($"MainWorld initialization failed: {exception.Message}", this);
    }
}
