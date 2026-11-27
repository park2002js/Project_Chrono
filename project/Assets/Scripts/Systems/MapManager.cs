using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Map 프리팹의 체크포인트·Boss·조우 영역 배치 원본을 조회</summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance; // 생성이 끝난 맵의 체크포인트·Boss 배치를 외부에서 조회할 창구

    /// <summary>저장 CheckpointId와 연결되는 Player 배치 위치</summary>
    [Serializable]
    public class CheckpointSpawn
    {
        public string Id = "start"; // 저장된 CheckpointId와 배치 위치를 연결할 식별자
        public Transform Position; // 해당 체크포인트에서 Player를 배치할 위치
    }

    /// <summary>Boss 식별자와 기본 위치·조우 영역을 연결</summary>
    [Serializable]
    public class BossSpawn
    {
        public string BossId = "guard_robot"; // Boss의 저장 기록과 배치 정보를 연결할 식별자
        public Transform Position; // 해당 Boss를 처음 배치할 위치
        public Collider2D EncounterArea; // 해당 Boss의 조우 진입을 감지할 트리거 영역
        public GameObject EntryDoor; // 수정 활성화·전투 진입·재도전에 맞춰 여닫을 입구
        public GameObject ExitDoor; // Boss 처치 기록에 따라 열릴 출구
        [NonSerialized] public MapDoor EntryMotion; // 맵 초기화에서 확보해 재사용할 입구 모션과 충돌 담당
        [NonSerialized] public MapDoor ExitMotion; // 맵 초기화에서 확보해 재사용할 출구 모션과 충돌 담당
    }

    public CheckpointSpawn[] Checkpoints = Array.Empty<CheckpointSpawn>(); // 체크포인트 식별자로 Player 배치 위치를 찾기 위한 목록
    public BossSpawn[] BossPositions = Array.Empty<BossSpawn>(); // Boss 식별자로 기본 위치와 조우 영역을 찾기 위한 목록
    public Transform EntranceSpawnPoint; // 새 게임의 최초 입장 걷기를 시작할 복도 아래 위치
    public Transform EntranceStopPoint; // 최초 입장 걷기를 멈추고 조작 권한을 인계할 위치
    public StageExit Exit; // 이 맵을 떠나는 문의 진입 감지이며 없으면 떠날 수 없는 맵
    public bool WakesOnFirstEntry; // 처음 들어올 때 걸어 들어오는 대신 시작 위치에서 깨어나는 맵인지 표시
    public Vector2 CameraVerticalLimits; // 이 맵에서 카메라가 움직일 세로 범위이며 위 값이 아래 값보다 클 때만 적용
    public bool IsInitialized; // 배치 정보와 저장 체크포인트 검증이 완료됐는지 표시
    private CrystalCheckPoint[] crystals = Array.Empty<CrystalCheckPoint>(); // 이번 Map에서 초기화·구독할 수정 목록
    private PlayerSpawner playerSpawner; // Player 생성 뒤 한 번 연결해 활성 체크포인트 캐시를 갱신할 대상
    private SaveData saveData; // 이번 세션에서 유지되는 활성 수정·처치 기록의 원본
    private readonly HashSet<Collider2D> walls = new HashSet<Collider2D>(); // 이번 맵의 벽과 문에서 탄환을 막을 물리 Collider 목록

    /// <summary>Player 생성 전에 이번 세션의 Map 조회 대상 등록</summary>
    private void Awake()
    {
        // 기존 맵을 유지하고 중복 생성된 맵만 제거
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // 배치 검증은 StageManager가 Player 복제 전에 Initialize로 완료
        Instance = this;
        IsInitialized = false;
    }

    private void OnDestroy()
    {
        foreach (CrystalCheckPoint crystal in crystals)
            if (crystal != null) crystal.OnActivated -= HandleCheckpointActivated; // 해제된 Map의 수정 상태 구독 정리
        if (Instance == this)
            Instance = null;
    }

    /// <summary>배치 데이터와 저장 체크포인트를 검증하고 조우 영역을 준비 상태로 잠금</summary>
    public void Initialize(SaveData save)
    {
        // 필수 배치 목록과 저장 데이터 존재 확인
        IsInitialized = false;
        if (save == null || Checkpoints == null || BossPositions == null)
            throw new InvalidOperationException("Map requires save data and spawn lists.");
        saveData = save;

        // 체크포인트 식별자 중복과 배치 위치 누락 검사
        var checkpointIds = new HashSet<string>();
        foreach (CheckpointSpawn checkpoint in Checkpoints)
        {
            if (checkpoint == null || string.IsNullOrWhiteSpace(checkpoint.Id) || checkpoint.Position == null || !checkpointIds.Add(checkpoint.Id))
                throw new InvalidOperationException("Map checkpoint identifiers and positions must be valid and unique.");
        }

        // Boss 배치·조우 영역을 검증하고 월드 준비까지 Trigger 비활성화
        var bossIds = new HashSet<string>();
        foreach (BossSpawn boss in BossPositions)
        {
            if (boss == null || string.IsNullOrWhiteSpace(boss.BossId) || boss.Position == null || boss.EncounterArea == null || !bossIds.Add(boss.BossId))
                throw new InvalidOperationException("Map Boss identifiers, positions, and encounter areas must be valid and unique.");
            if (!boss.EncounterArea.isTrigger)
                throw new InvalidOperationException($"{boss.BossId}: encounter area must be a trigger.");
            boss.EncounterArea.enabled = false;
            boss.EntryMotion = boss.EntryDoor != null ? boss.EntryDoor.GetComponent<MapDoor>() : null;
            boss.ExitMotion = boss.ExitDoor != null ? boss.ExitDoor.GetComponent<MapDoor>() : null;
        }

        // 저장 체크포인트가 현재 맵에 있을 때만 초기화 완료 처리
        string checkpointId = save.Get<string>("CheckpointId");
        GetCheckpoint(checkpointId);
        List<string> activated = save.Values.ContainsKey("ActivatedCheckpoints") ? save.Get<List<string>>("ActivatedCheckpoints") : null;
        if (activated == null)
        {
            activated = new List<string>();
            save.Set("ActivatedCheckpoints", activated);
        }
        if (!activated.Contains(checkpointId)) activated.Add(checkpointId);
        IsInitialized = true;

        // Actor 생성 전에 수정 이력을 적용하고 새 활성화 사건만 구독
        crystals = GetComponentsInChildren<CrystalCheckPoint>(true);
        foreach (CrystalCheckPoint crystal in crystals)
        {
            crystal.Initialize(this, save);
            crystal.OnActivated -= HandleCheckpointActivated; // 재초기화에서도 같은 Map 구독 중복 방지
            if (!crystal.IsActivated) crystal.OnActivated += HandleCheckpointActivated; // 새 수정 활성화 뒤 문·로컬 배치 갱신
        }
        RestoreCheckpointDoors();

        // 같은 맵 수명에서 벽과 비활성 문을 한 번 확보하고 발사 중에는 집합만 조회
        walls.Clear();
        foreach (Collider2D shape in GetComponentsInChildren<Collider2D>(true))
            if (!shape.isTrigger) walls.Add(shape);
    }

    /// <summary>이번 맵이 보유한 벽과 문의 물리 Collider인지 조회</summary>
    public bool IsWall(Collider2D shape)
    {
        // Boss와 Player 및 경고 Trigger는 맵 물리 목록에 포함하지 않음
        return shape != null && walls.Contains(shape);
    }

    /// <summary>Map 준비 뒤 생성된 Player의 배치 캐시를 한 번 연결</summary>
    public void BindPlayer(PlayerSpawner spawner) => playerSpawner = spawner;

    /// <summary>저장된 활성 수정의 입구를 열고 나머지 입구는 닫아 재도전 준비</summary>
    public void RestoreCheckpointDoors()
    {
        foreach (BossSpawn boss in BossPositions)
        {
            bool defeated = saveData.Get<List<string>>("DefeatedBosses").Contains(boss.BossId);
            SetDoor(boss.EntryDoor, boss.EntryMotion, defeated, true);
            SetDoor(boss.ExitDoor, boss.ExitMotion, defeated, true);
        }
        foreach (CrystalCheckPoint crystal in crystals)
            if (crystal != null && crystal.IsActivated && !string.IsNullOrEmpty(crystal.BossId))
            {
                BossSpawn boss = GetBossSpawn(crystal.BossId);
                SetDoor(boss.EntryDoor, boss.EntryMotion, true, true);
            }
    }

    /// <summary>해당 Boss 입구의 실제 표시·충돌을 함께 전환</summary>
    public void SetEntryDoorOpen(string bossId, bool open)
    {
        if (string.IsNullOrEmpty(bossId)) return;
        BossSpawn boss = GetBossSpawn(bossId);
        SetDoor(boss.EntryDoor, boss.EntryMotion, open, false);
    }

    /// <summary>입구 연결과 실제 활성 상태를 조회</summary>
    public GameObject GetEntryDoor(string bossId) => GetBossSpawn(bossId).EntryDoor;

    /// <summary>처치·재도전에서 해당 Boss 출구의 표시·충돌을 함께 전환</summary>
    public void SetExitDoorOpen(string bossId, bool open)
    {
        BossSpawn boss = GetBossSpawn(bossId);
        SetDoor(boss.ExitDoor, boss.ExitMotion, open, false);
    }

    /// <summary>검증과 연출에서 실제 출구 객체 조회</summary>
    public GameObject GetExitDoor(string bossId) => GetBossSpawn(bossId).ExitDoor;

    /// <summary>완성 문의 모션을 사용하고 아트 없는 기존 검증 객체의 동작 유지</summary>
    private static void SetDoor(GameObject door, MapDoor motion, bool open, bool immediate)
    {
        if (motion != null) motion.SetOpen(open, immediate);
        else if (door != null) door.SetActive(!open);
    }

    /// <summary>수정이 알린 활성화에서 배치 캐시·입구만 갱신</summary>
    private void HandleCheckpointActivated(CrystalCheckPoint crystal)
    {
        if (playerSpawner != null) playerSpawner.UpdateCheckpoint(crystal.CheckpointId);
        SetEntryDoorOpen(crystal.BossId, true);
        crystal.OnActivated -= HandleCheckpointActivated; // 일회 활성화가 끝난 수정의 Map 구독 해제
    }

    /// <summary>저장 식별자에 대응하는 체크포인트의 Player 배치 위치를 반환</summary>
    public Transform GetCheckpoint(string checkpointId)
    {
        foreach (CheckpointSpawn checkpoint in Checkpoints)
            if (checkpoint != null && checkpoint.Id == checkpointId && checkpoint.Position != null)
                return checkpoint.Position;
        throw new InvalidOperationException($"Checkpoint '{checkpointId}' is not configured in this Map.");
    }

    /// <summary>BossSpawner가 사용할 Boss 기본 위치를 반환</summary>
    public Transform GetBossPosition(string bossId)
    {
        return GetBossSpawn(bossId).Position;
    }

    /// <summary>BossSpawner가 초기화할 조우 영역을 반환</summary>
    public Collider2D GetEncounterArea(string bossId)
    {
        return GetBossSpawn(bossId).EncounterArea;
    }

    /// <summary>Boss 식별자에 대응하는 배치 원본을 조회</summary>
    private BossSpawn GetBossSpawn(string bossId)
    {
        foreach (BossSpawn boss in BossPositions)
            if (boss != null && boss.BossId == bossId && boss.Position != null && boss.EncounterArea != null)
                return boss;
        throw new InvalidOperationException($"Boss '{bossId}' is not configured in this Map.");
    }
}
