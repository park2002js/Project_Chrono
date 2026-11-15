using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>공통 Boss 검증용 프리팹·위험물·독립 진입 씬을 준비</summary>
public static class BossCombatSetup
{
    private const string DirectoryPath = "Assets/Prefabs/BossTests"; // 실제 제출 Boss와 분리한 시험용 에셋 위치

    /// <summary>더미와 실제 상속 위험물·출구·컷씬 연결을 저장</summary>
    [MenuItem("Tools/Chrono/Prepare Boss Test Arena")]
    public static void Run()
    {
        // 원본 씬은 닫지 않고 기존 에셋 GUID를 유지하며 필요한 프리팹만 보완
        Directory.CreateDirectory(DirectoryPath);
        AssetDatabase.Refresh();
        Sprite pixel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Graphics/TemporaryDraw/Pixel.png");
        Sprite circle = CreateCircle();
        Projectile[] projectiles =
        {
            CreateProjectile<NormalProjectile>("Normal", circle, Color.red),
            CreateProjectile<GoldProjectile>("Gold", circle, Color.yellow),
            CreateProjectile<LaserProjectile>("Laser", pixel, Color.red),
            CreateProjectile<AreaProjectile>("Area", circle, Color.red)
        };

        // 실제 상속을 가진 더미 몸체와 독립 부위 두 개 준비
        GameObject root = new GameObject("DummyBoss");
        root.SetActive(false);
        try
        {
            BoxCollider2D body = root.AddComponent<BoxCollider2D>();
            body.size = new Vector2(1f, 1f);
            DummyBoss boss = root.AddComponent<DummyBoss>();
            boss.BossId = GameManager.SubmittedBossId;
            boss.BossName = "Dummy Boss";
            boss.maxHP = 300f;
            boss.ProjectilePrefabs = projectiles;
            boss.GroggyDuration = 2f;
            SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = pixel;
            sprite.drawMode = SpriteDrawMode.Sliced;
            sprite.size = Vector2.one;
            sprite.color = new Color(0.65f, 0.35f, 0.8f);
            root.AddComponent<Hitbox>().Owner = boss;
            CreatePart(boss, pixel, 1, -0.8f);
            CreatePart(boss, pixel, 2, 0.8f);
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, DirectoryPath + "/DummyBoss.prefab");
        }
        finally { Object.DestroyImmediate(root); }

        // 앱의 기존 시스템에 연출 담당을 직렬화하고 맵에 출구 연결
        GameObject systems = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Startup/GameSystems.prefab");
        try
        {
            if (systems.GetComponent<CutsceneController>() == null) systems.AddComponent<CutsceneController>();
            PrefabUtility.SaveAsPrefabAsset(systems, "Assets/Prefabs/Startup/GameSystems.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(systems); }
        PrepareMap(pixel);
        PrepareTestScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[BossCombatSetup] DummyBoss, four projectile prefabs, exit and BossTestArena scene prepared.");
    }

    /// <summary>배치 검사에서 에셋 준비 후 기존 Play Mode 검사 실행</summary>
    public static void RunChecks()
    {
        Run();
        StartupFlowChecks.Run();
    }

    /// <summary>타입별 실제 Collider와 Hitbox를 가진 위험물 프리팹 작성</summary>
    private static Projectile CreateProjectile<T>(string name, Sprite sprite, Color color) where T : Projectile
    {
        GameObject root = new GameObject(name);
        try
        {
            Collider2D shape = typeof(T) == typeof(LaserProjectile) ? root.AddComponent<BoxCollider2D>() : root.AddComponent<CircleCollider2D>();
            shape.isTrigger = true;
            T projectile = root.AddComponent<T>();
            projectile.GroggyGain = 25f;
            root.AddComponent<Hitbox>().TargetKind = TargetKind.Projectile;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 2;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = Vector2.one * 0.18f;
            if (shape is CircleCollider2D circle) circle.radius = 0.09f;
            return PrefabUtility.SaveAsPrefabAsset(root, DirectoryPath + "/" + name + ".prefab").GetComponent<Projectile>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>같은 Slash에서 두 부위가 보고되는 기믹 검사 대상 준비</summary>
    private static void CreatePart(DummyBoss boss, Sprite sprite, int id, float x)
    {
        GameObject part = new GameObject("Part" + id);
        part.transform.SetParent(boss.transform, false);
        part.transform.localPosition = new Vector3(x, 0f, 0f);
        part.AddComponent<BoxCollider2D>().size = Vector2.one * 0.35f;
        BossStructure structure = part.AddComponent<BossStructure>();
        structure.Owner = boss;
        structure.PartId = id;
        structure.HP = 100f;
        part.AddComponent<Hitbox>().PartId = id;
        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = Vector2.one * 0.35f;
        renderer.color = Color.cyan;
    }

    /// <summary>기존 입구를 보존하고 처치 전에는 닫힌 출구 한 개 추가</summary>
    private static void PrepareMap(Sprite sprite)
    {
        const string path = "Assets/Prefabs/Startup/Map.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MapManager map = root.GetComponent<MapManager>();
            foreach (MapManager.BossSpawn boss in map.BossPositions)
            {
                if (boss.ExitDoor != null) continue;
                GameObject door = new GameObject(boss.BossId + "ExitDoor");
                door.transform.SetParent(root.transform, false);
                door.transform.localPosition = new Vector3(4.6f, 0f, 0f);
                door.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 8f);
                SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = new Vector2(0.15f, 8f);
                renderer.color = new Color(0.35f, 0.25f, 0.6f);
                boss.ExitDoor = door;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>동일 Title 흐름에서 더미만 교체한 수동 검증 진입 씬 저장</summary>
    private static void PrepareTestScene()
    {
        const string path = "Assets/Scenes/BossTestArena.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            AssetDatabase.CopyAsset("Assets/Scenes/Title.unity", path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameManager manager = root.GetComponent<GameManager>();
                if (manager == null) continue;
                StageManager stage = root.GetComponent<StageManager>();
                stage.BossPrefab = AssetDatabase.LoadAssetAtPath<DummyBoss>(DirectoryPath + "/DummyBoss.prefab");
                manager.SaveDirectory = "Temp/BossArenaSaves";
                PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    /// <summary>장판의 원형 판정과 같은 외곽을 보여 줄 임시 Sprite 생성</summary>
    private static Sprite CreateCircle()
    {
        string path = DirectoryPath + "/Circle.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    texture.SetPixel(x, y, new Vector2(x - 15.5f, y - 15.5f).sqrMagnitude <= 256f ? Color.white : Color.clear);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
