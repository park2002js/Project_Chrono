using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>F-S01에 필요한 최소 프리팹을 만들고 기존 두 씬에 참조를 연결</summary>
public static class StartupSceneSetup
{
    private const string PrefabDirectory = "Assets/Prefabs/Startup"; // 시작 흐름의 공통 프리팹 저장 위치

    /// <summary>최초 코드 연결 시 실행하며 이미 만든 프리팹의 사용자 편집은 보존</summary>
    [MenuItem("Chrono/Prepare Startup Scenes")]
    public static void Run()
    {
        // 기존 프리팹을 재사용하며 없는 시작 구성만 생성
        Directory.CreateDirectory(PrefabDirectory);
        AssetDatabase.Refresh();
        MapManager map = LoadOrCreate("Map", CreateMap);
        PlayerCharacter player = LoadOrCreate("Player", CreatePlayer);
        BossCharacter boss = LoadOrCreate("GuardRobot", CreateBoss);
        UIController ui = LoadOrCreate("GameUI", () => new GameObject("GameUI").AddComponent<UIController>());
        GameCamera camera = LoadOrCreate("WorldCamera", CreateCamera);
        GameManager systems = LoadOrCreate("GameSystems", () =>
        {
            GameManager manager = new GameObject("GameSystems").AddComponent<GameManager>();
            StageManager stage = manager.GetComponent<StageManager>();
            stage.MapPrefab = map;
            stage.PlayerPrefab = player;
            stage.BossPrefab = boss;
            manager.Stage = stage;
            manager.Loading = manager.GetComponent<UI_Loading>();
            return manager;
        });

        // 기존 씬과 meta를 유지하고 필요한 객체만 추가
        var title = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        if (UnityEngine.Object.FindFirstObjectByType<GameManager>() == null)
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDirectory}/GameSystems.prefab"));
        if (UnityEngine.Object.FindFirstObjectByType<UI_Title>() == null)
            new GameObject("UI_Title").AddComponent<UI_Title>();
        EditorSceneManager.SaveScene(title);

        // MainWorld의 독립 HUD·카메라와 빌드 씬 목록 연결
        var world = EditorSceneManager.OpenScene("Assets/Scenes/MainWorld.unity");

        // 게임 카메라 이외의 기존 템플릿 카메라만 비활성화
        foreach (Camera templateCamera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (templateCamera.GetComponent<GameCamera>() == null)
                templateCamera.gameObject.SetActive(false);
        if (UnityEngine.Object.FindFirstObjectByType<UIController>() == null)
            PrefabUtility.InstantiatePrefab(ui.gameObject);
        if (UnityEngine.Object.FindFirstObjectByType<GameCamera>() == null)
            PrefabUtility.InstantiatePrefab(camera.gameObject);
        EditorSceneManager.SaveScene(world);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Title.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MainWorld.unity", true)
        };

        // 에셋 저장 후 Title 복귀와 독립 입력 연결
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        PlayerMovementSetup.ConnectSharedControllers();
        Debug.Log("[StartupSceneSetup] Title/MainWorld and independent world controllers connected.");
    }

    /// <summary>미생성 프리팹만 작성하여 반복 실행 때 기존 참조와 편집을 보존</summary>
    private static T LoadOrCreate<T>(string name, Func<T> create) where T : Component
    {
        // 사용자 편집과 GUID를 유지하도록 기존 프리팹 우선 반환
        string path = $"{PrefabDirectory}/{name}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<T>();

        // 없는 프리팹만 저장하고 제작용 임시 객체 제거
        T instance = create();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance.gameObject, path);
        UnityEngine.Object.DestroyImmediate(instance.gameObject);
        return prefab.GetComponent<T>();
    }

    /// <summary>시작 체크포인트·Boss 배치·조우 영역이 연결된 검증용 공간을 작성</summary>
    private static MapManager CreateMap()
    {
        // 시작점·Boss 위치와 준비 완료 후 활성화할 조우 영역 구성
        MapManager map = new GameObject("Map").AddComponent<MapManager>();
        Transform start = Child(map.transform, "StartCheckpoint", new Vector3(-3f, 0f, 0f));
        Transform boss = Child(map.transform, "GuardRobotSpawn", new Vector3(3f, 0f, 0f));
        Transform encounter = Child(map.transform, "GuardRobotEncounter", new Vector3(2f, 0f, 0f));
        BoxCollider2D trigger = encounter.gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(4f, 6f);
        trigger.enabled = false;
        map.Checkpoints = new[] { new MapManager.CheckpointSpawn { Id = "start", Position = start } };
        map.BossPositions = new[] { new MapManager.BossSpawn { BossId = GameManager.SubmittedBossId, Position = boss, EncounterArea = trigger } };

        // 최종 아트 없이 배치를 확인할 바닥·체크포인트 표시
        Transform floor = Child(map.transform, "Floor", Vector3.zero);
        AddMarker(floor.gameObject, new Color(0.12f, 0.16f, 0.2f), new Vector2(18f, 8f), -10);
        AddMarker(start.gameObject, new Color(0.2f, 0.8f, 0.7f), new Vector2(1.4f, 1.4f), -1);
        return map;
    }

    /// <summary>초기화와 저장 HP 적용을 확인할 Player 프리팹을 작성</summary>
    private static PlayerCharacter CreatePlayer()
    {
        PlayerCharacter player = new GameObject("Player").AddComponent<PlayerCharacter>();
        AddMarker(player.gameObject, new Color(0.5f, 0.75f, 1f), new Vector2(0.65f, 1f), 1);
        player.GetComponent<BoxCollider2D>().size = new Vector2(0.65f, 1f);
        Hitbox hitbox = player.gameObject.AddComponent<Hitbox>();
        hitbox.Owner = player;
        hitbox.TargetKind = TargetKind.Player;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        return player;
    }

    /// <summary>생존·처치 상태와 위치 적용을 확인할 경비 로봇 프리팹을 작성</summary>
    private static BossCharacter CreateBoss()
    {
        BossCharacter boss = new GameObject("GuardRobot").AddComponent<BossCharacter>();
        AddMarker(boss.gameObject, new Color(1f, 0.45f, 0.25f), new Vector2(1.2f, 1.6f), 1);
        boss.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(1.2f, 1.6f);
        Hitbox hitbox = boss.gameObject.AddComponent<Hitbox>();
        hitbox.Owner = boss;
        hitbox.TargetKind = TargetKind.Boss;
        return boss;
    }

    /// <summary>PlayerSpawner에 연결할 게임 전용 2D 카메라를 작성</summary>
    private static GameCamera CreateCamera()
    {
        GameObject root = new GameObject("WorldCamera");
        root.tag = "MainCamera";
        root.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = root.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.05f, 0.07f);
        root.AddComponent<AudioListener>();
        return root.AddComponent<GameCamera>();
    }

    /// <summary>Map 내부 배치의 원본 Transform을 작성</summary>
    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    /// <summary>최종 아트 없이 생성 위치를 확인하도록 Unity 기본 Sprite를 연결</summary>
    private static void AddMarker(GameObject target, Color color, Vector2 size, int order)
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (sprite == null)
            throw new InvalidOperationException("Unity's built-in marker Sprite is unavailable.");
        Transform visual = Child(target.transform, "Visual", Vector3.zero);
        visual.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
        SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
    }
}
