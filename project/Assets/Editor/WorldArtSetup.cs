using System;
using UnityEditor;
using UnityEngine;

/// <summary>완성된 두 방·문·수정을 기존 Map 식별자와 실제 충돌·조우에 연결</summary>
public static class WorldArtSetup
{
    private const string MapPath = "Assets/Prefabs/Startup/Map.prefab"; // 기존 세션이 참조하는 Map 원본
    private const string LaboratoryPath = "Assets/Art/Environment/Laboratory/Prefabs/LaboratoryLayout.prefab"; // 보관 원본의 두 방 Tilemap 배치

    /// <summary>사용자 원본과 기존 Map GUID를 보존하며 제출 씬의 아트 연결 저장</summary>
    [MenuItem("Tools/Chrono/Apply World Art")]
    public static void Apply()
    {
        // 기존 식별자·생성 위치 객체를 유지하고 임시 도형만 제거
        GameObject root = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            MapManager map = root.GetComponent<MapManager>();
            foreach (string name in new[] { "Floor", "WallBottom", "WallTop", "WallLeft", "WallRight", "guard_robotEntryDoor", "guard_robotExitDoor", "LaboratoryLayout", "WorldCollision" })
            {
                Transform old = root.transform.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            GameObject layout = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LaboratoryPath), root.scene);
            layout.name = "LaboratoryLayout";
            layout.transform.SetParent(root.transform, false);

            // 아래 첫 방에서 전투하고 처치한 뒤 위 다음 방으로 걸어서 이동
            MapDoor entry = ConnectDoor(layout.transform.Find("previous-outer-entrance"), false, false);
            MapDoor exit = ConnectDoor(layout.transform.Find("previous-exit"), true, false);
            ConnectDoor(layout.transform.Find("next-entrance"), false, true);
            ConnectDoor(layout.transform.Find("next-outer-exit"), true, false);
            MapManager.BossSpawn boss = map.BossPositions[0];
            boss.EntryDoor = entry.gameObject;
            boss.ExitDoor = exit.gameObject;
            boss.Position.localPosition = new Vector3(14.5f, 17f, 0f);
            boss.EncounterArea.transform.localPosition = new Vector3(14.5f, 17f, 0f);
            BoxCollider2D encounter = (BoxCollider2D)boss.EncounterArea;
            encounter.size = new Vector2(24f, 17f);
            encounter.offset = Vector2.zero;
            encounter.isTrigger = true;

            // 최초 저장 위치와 수정 활성화 후 재생 위치를 같은 안전한 문 앞에 연결
            foreach (MapManager.CheckpointSpawn point in map.Checkpoints)
            {
                point.Position.localPosition = new Vector3(14.5f, 1.4f, 0f);
                foreach (Transform child in point.Position) UnityEngine.Object.DestroyImmediate(child.gameObject);
                CrystalCheckPoint old = point.Position.GetComponent<CrystalCheckPoint>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                foreach (Collider2D collider in point.Position.GetComponents<Collider2D>()) UnityEngine.Object.DestroyImmediate(collider);
            }
            GameObject crystal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Amethyst/Prefabs/Amethyst.prefab"), root.scene);
            crystal.name = "CheckpointCrystal";
            crystal.transform.SetParent(layout.transform, false);
            crystal.transform.localPosition = new Vector3(12.5f, 1f, 0f);
            crystal.GetComponent<SpriteRenderer>().sortingOrder = 20;
            CircleCollider2D range = crystal.AddComponent<CircleCollider2D>();
            range.radius = 2.2f;
            range.isTrigger = true;
            CrystalCheckPoint checkpoint = crystal.AddComponent<CrystalCheckPoint>();
            checkpoint.CheckpointId = "mid";
            checkpoint.BossId = GameManager.SubmittedBossId;
            checkpoint.CrystalAnimator = crystal.GetComponent<Animator>();
            GameObject key = new GameObject("InteractionF", typeof(SpriteRenderer));
            key.transform.SetParent(crystal.transform, false);
            key.transform.localPosition = new Vector3(0f, 2.65f, 0f);
            SpriteRenderer prompt = key.GetComponent<SpriteRenderer>();
            prompt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Keyboard/key-f-light.png");
            prompt.sharedMaterial = crystal.GetComponent<SpriteRenderer>().sharedMaterial;
            prompt.sortingOrder = 100;
            prompt.enabled = false;
            checkpoint.InteractionPrompt = prompt;

            // layout.json의 보행 사각형 합집합 밖을 막고 문은 별도 모션 충돌로 관리
            Transform collision = new GameObject("WorldCollision").transform;
            collision.SetParent(root.transform, false);
            int[] bands = { 0, 8, 26, 55, 73, 77 };
            for (int i = 0; i < bands.Length - 1; i++)
            {
                float left = i == 1 || i == 3 ? 2f : 12f;
                float right = i == 1 || i == 3 ? 27f : 17f;
                AddWall(collision, new Rect(0f, bands[i], left, bands[i + 1] - bands[i]));
                AddWall(collision, new Rect(right, bands[i], 29f - right, bands[i + 1] - bands[i]));
            }
            AddWall(collision, new Rect(-1f, -1f, 31f, 1f));
            AddWall(collision, new Rect(-1f, 77f, 31f, 1f));
            AddWall(collision, new Rect(-1f, 0f, 1f, 77f));
            AddWall(collision, new Rect(29f, 0f, 1f, 77f));
            if (prompt.sprite == null || map.Checkpoints.Length != 2) throw new InvalidOperationException("World art requires F sprite and existing start/mid checkpoints.");
            // 전체 월드 아트를 다시 연결해도 남쪽 입장 복도와 두 배치점 유지
            MapEntranceSetup.ApplyToPrefab(root);
            PrefabUtility.SaveAsPrefabAsset(root, MapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 원본 320×180 픽셀 시야와 완성 맵 위의 기존 Boss 표시 보장
        string cameraPath = "Assets/Prefabs/Startup/WorldCamera.prefab";
        GameObject cameraRoot = PrefabUtility.LoadPrefabContents(cameraPath);
        try
        {
            cameraRoot.GetComponent<GameCamera>().DefaultZoom = 5.625f;
            cameraRoot.GetComponent<GameCamera>().VerticalLimits = new Vector2(0f, 77f);
            cameraRoot.GetComponent<Camera>().orthographicSize = 5.625f;
            MapEntranceSetup.ApplyToCameraPrefab(cameraRoot);
            PrefabUtility.SaveAsPrefabAsset(cameraRoot, cameraPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cameraRoot); }
        string bossPath = "Assets/Prefabs/Startup/GuardRobot.prefab";
        GameObject bossRoot = PrefabUtility.LoadPrefabContents(bossPath);
        try
        {
            foreach (SpriteRenderer sprite in bossRoot.GetComponentsInChildren<SpriteRenderer>()) sprite.sortingOrder = 20;
            PrefabUtility.SaveAsPrefabAsset(bossRoot, bossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(bossRoot); }
        AssetDatabase.SaveAssets();
        Debug.Log("[WorldArtSetup] Map, animated doors, crystal, F prompt and camera connected.");
    }

    /// <summary>원본 문 인스턴스에 완료 상태를 관찰하는 충돌 담당 추가</summary>
    private static MapDoor ConnectDoor(Transform door, bool exit, bool open)
    {
        BoxCollider2D collider = door.gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(5f, 1f);
        collider.offset = new Vector2(0f, exit ? 1f : 1.5f);
        MapDoor motion = door.gameObject.AddComponent<MapDoor>();
        motion.InitiallyOpen = open;
        motion.OpeningState = exit ? "activation" : "opening";
        door.GetComponent<SpriteRenderer>().sortingOrder = 19;
        return motion;
    }

    /// <summary>타일 보행 경계 밖의 사각형에만 정적 충돌체 배치</summary>
    private static void AddWall(Transform parent, Rect rect)
    {
        GameObject wall = new GameObject("Boundary");
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = rect.center;
        wall.AddComponent<BoxCollider2D>().size = rect.size;
    }
}
