using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>기존 Map·UI의 참조를 보존하며 수정·입구·생명주기 표시 연결</summary>
public static class PlayerProgressionSetup
{
    /// <summary>현재 제출 맵에 실제 상호작용과 사망·생성 표시를 연결</summary>
    public static void Run()
    {
        // 기존 시작 배치를 유지하고 새 수정 위치만 추가
        string mapPath = "Assets/Prefabs/Startup/Map.prefab";
        GameObject mapRoot = PrefabUtility.LoadPrefabContents(mapPath);
        try
        {
            MapManager map = mapRoot.GetComponent<MapManager>();
            var checkpoints = new List<MapManager.CheckpointSpawn>(map.Checkpoints);
            MapManager.CheckpointSpawn start = checkpoints.Find(value => value.Id == "start");
            Sprite sprite = start.Position.GetComponentInChildren<SpriteRenderer>().sprite;
            MapManager.CheckpointSpawn mid = checkpoints.Find(value => value.Id == "mid");
            if (mid == null)
            {
                var point = new GameObject("MidCheckpoint").transform;
                point.SetParent(map.transform, false);
                point.localPosition = new Vector3(-2f, 0f, 0f);
                mid = new MapManager.CheckpointSpawn { Id = "mid", Position = point };
                checkpoints.Add(mid);
            }
            map.Checkpoints = checkpoints.ToArray();
            PrepareCrystal(start, "", sprite);
            PrepareCrystal(mid, GameManager.SubmittedBossId, sprite);

            // 수정 뒤의 입구와 실제 조우 사실 전달을 같은 Map에 연결
            foreach (MapManager.BossSpawn boss in map.BossPositions)
            {
                if (boss.EntryDoor == null)
                {
                    var door = new GameObject(boss.BossId + "EntryDoor");
                    door.transform.SetParent(map.transform, false);
                    door.transform.localPosition = new Vector3(-1.1f, 0f, 0f);
                    door.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 8f);
                    Marker(door.transform, sprite, new Vector2(0.15f, 8f), new Color(0.65f, 0.3f, 0.35f));
                    boss.EntryDoor = door;
                }
                if (boss.EncounterArea.GetComponent<EncounterTrigger>() == null)
                    boss.EncounterArea.gameObject.AddComponent<EncounterTrigger>();
            }
            PrefabUtility.SaveAsPrefabAsset(mapRoot, mapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(mapRoot); }

        // 기존 HUD·Draw 표시와 같은 UI 객체에 수명 표시만 추가
        string uiPath = "Assets/Prefabs/Startup/GameUI.prefab";
        GameObject ui = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            UI_Death death = ui.GetComponent<UI_Death>();
            if (death == null) death = ui.AddComponent<UI_Death>();
            string materialPath = "Assets/Prefabs/Startup/DeathGrayscale.mat";
            Material grayscale = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (grayscale == null)
            {
                grayscale = new Material(Shader.Find("Chrono/DeathGrayscale"));
                AssetDatabase.CreateAsset(grayscale, materialPath);
            }
            death.GrayscaleMaterial = grayscale;
            PrefabUtility.SaveAsPrefabAsset(ui, uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(ui); }
    }

    /// <summary>배치된 수정의 실제 Trigger·식별·복구 위치 준비</summary>
    private static void PrepareCrystal(MapManager.CheckpointSpawn point, string bossId, Sprite sprite)
    {
        CrystalCheckPoint crystal = point.Position.GetComponent<CrystalCheckPoint>();
        if (crystal == null)
        {
            CircleCollider2D trigger = point.Position.gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;
            crystal = point.Position.gameObject.AddComponent<CrystalCheckPoint>();
            if (point.Position.GetComponentInChildren<SpriteRenderer>() == null)
                Marker(point.Position, sprite, new Vector2(0.4f, 0.65f), Color.cyan);
        }
        crystal.CheckpointId = point.Id;
        crystal.SpawnPosition = point.Position;
        crystal.BossId = bossId;
    }

    /// <summary>최종 미술 전의 수정·입구 표식을 자식으로 생성</summary>
    private static void Marker(Transform parent, Sprite sprite, Vector2 size, Color color)
    {
        var marker = new GameObject("TemporaryMarker").transform;
        marker.SetParent(parent, false);
        marker.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = marker.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 0;
    }
}
