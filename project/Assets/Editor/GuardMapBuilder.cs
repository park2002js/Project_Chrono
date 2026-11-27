using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>경비 맵 프리팹의 그림·벽·밝기 전환 구역·어둠의 문을 목록 파일대로 다시 맞추는 편집기 도구</summary>
public static class GuardMapBuilder
{
    private const string ManifestPath = "Assets/Editor/GuardMapManifest.json"; // 맵 그림과 벽·구역·지점을 적은 목록
    private const string RequestPath = "Temp/GuardMapBuilder.request"; // 이 파일이 생기면 메뉴 없이 한 번 실행
    private const string LogPath = "Temp/GuardMapBuilder.log"; // 마지막 실행 결과를 남길 위치
    private const string MapPath = "Assets/Prefabs/Startup/Map.prefab"; // 고쳐 쓸 경비 맵 프리팹
    private const string DarkGateName = "DarkGate"; // 심층 통로 끝에 세울 어둠의 문 객체 이름
    private const int DarkGateOrder = 12; // 바닥 그림(11)보다 위, 캐릭터(23)보다 아래인 문의 정렬 순서

    /// <summary>경비실 그림 객체 기준으로 놓는 벽 상자 한 개</summary>
    [Serializable]
    private class WallEntry
    {
        public string name; // 막는 자리를 알리는 이름
        public float x; // 상자 중심의 가로 위치
        public float y; // 상자 중심의 세로 위치
        public float w; // 상자의 가로 크기
        public float h; // 상자의 세로 크기
    }

    /// <summary>목록 파일 전체</summary>
    [Serializable]
    private class Manifest
    {
        public int ppu; // 월드 1유닛에 놓을 원본 픽셀 수
        public string mapTexture; // 맵 한 장짜리 그림의 경로
        public float[] mapPivot; // 그림에서 경비실 바닥의 위쪽 가운데가 놓인 비율
        public string darkGateTexture; // 어둠의 문 그림의 경로
        public float[] darkGate; // 어둠의 문 문턱 가운데의 월드 위치
        public float[] darkGateTrigger; // 문턱 가운데 기준으로 문 속의 진입 감지 범위 x·y·폭·높이
        public string[] remove; // 이전 구성에서 남아 지울 최상위 객체 이름
        public WallEntry[] walls; // 경비실 그림 객체 기준의 고정 벽
        public float[] interiorArea; // 방 안쪽 덮개의 x·y·폭·높이
        public float[] entryArea; // 입구 쪽 덮개의 x·y·폭·높이
        public float[] exitArea; // 출구 쪽 덮개의 x·y·폭·높이
        public float entryLineOffset; // 입구 문턱까지의 세로 거리
        public float exitLineOffset; // 출구 문턱까지의 세로 거리
        public float[] cameraVerticalLimits; // 맵 그림의 아래쪽 끝과 위쪽 끝의 월드 세로 위치
        public float[] entranceSpawn; // 첫 입장 때 Player가 나타나는 위치
        public float[] entranceStop; // 첫 입장 걸음이 끝나는 위치
        public float[] checkpoint; // 새 게임을 시작할 때 Player가 서는 위치
        public float[] respawn; // 수정을 켠 뒤 재도전할 때 Player가 서는 위치
        public float[] crystal; // 체크포인트 수정의 위치
    }

    /// <summary>에디터가 켜져 있는 동안 요청 파일을 지켜보도록 연결</summary>
    [InitializeOnLoadMethod]
    private static void WatchRequest()
    {
        EditorApplication.update += PollRequest; // 요청 파일이 생기면 컴파일·임포트가 끝난 뒤 한 번 실행
    }

    /// <summary>임포트 설정 → 남은 옛 객체 제거 → 그림·벽·구역·문·지점 순서로 경비 맵 프리팹을 다시 맞춤</summary>
    [MenuItem("Project Chrono/Rebuild Guard Map")]
    public static void Rebuild()
    {
        var log = new StringBuilder();
        try
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            AssetDatabase.Refresh();
            ApplyTextureSettings(manifest.mapTexture, new Vector2(manifest.mapPivot[0], manifest.mapPivot[1]), manifest.ppu);
            ApplyTextureSettings(manifest.darkGateTexture, new Vector2(0.5f, 0f), manifest.ppu);
            AssetDatabase.Refresh();
            BuildMap(manifest, log);
            AssetDatabase.SaveAssets();
            log.AppendLine("OK walls=" + manifest.walls.Length);
        }
        catch (Exception exception)
        {
            log.AppendLine("FAILED " + exception);
            Debug.LogException(exception);
        }
        File.WriteAllText(LogPath, log.ToString());
    }

    /// <summary>요청 파일이 있고 에디터가 한가할 때 한 번 실행</summary>
    private static void PollRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Rebuild();
    }

    /// <summary>도트 그림 한 장을 지정한 기준점의 단일 Sprite로 임포트</summary>
    private static void ApplyTextureSettings(string relativePath, Vector2 pivot, int ppu)
    {
        var importer = AssetImporter.GetAtPath("Assets/" + relativePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Texture is not imported: " + relativePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    /// <summary>프리팹을 열어 옛 객체를 지우고 새 그림에 맞는 벽·구역·문·지점으로 교체한 뒤 같은 프리팹에 저장</summary>
    private static void BuildMap(Manifest manifest, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            var map = root.GetComponent<MapManager>();
            var darkness = root.GetComponentInChildren<RoomDarkness>(true);
            if (map == null || darkness == null) throw new InvalidOperationException("Guard map needs MapManager and RoomDarkness.");

            // 이전 구성에서 남은 옛 실험실 배치·정적 보스·옛 경계 벽 제거
            foreach (string name in manifest.remove)
            {
                Transform leftover = root.transform.Find(name);
                if (leftover == null) continue;

                // 옛 배치 아래에 달려 있던 체크포인트 수정은 월드 위치를 지킨 채 맵 최상위로 옮겨 남김
                foreach (CrystalCheckPoint crystal in leftover.GetComponentsInChildren<CrystalCheckPoint>(true))
                {
                    crystal.transform.SetParent(root.transform, true);
                    log.AppendLine("kept " + crystal.name);
                }
                UnityEngine.Object.DestroyImmediate(leftover.gameObject);
                log.AppendLine("removed " + name);
            }

            // 기상실부터 어둠의 문 앞까지 이어진 한 장의 그림으로 교체
            Transform preview = darkness.transform;
            var room = preview.Find("Room").GetComponent<SpriteRenderer>();
            room.sprite = LoadSprite(manifest.mapTexture);

            // 그림의 바닥 밖을 막는 고정 벽을 목록대로 다시 생성
            Transform walls = preview.Find("Walls");
            for (int i = walls.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(walls.GetChild(i).gameObject);
            foreach (WallEntry wall in manifest.walls)
                CreateChild(walls, wall.name, new Vector2(wall.x, wall.y)).AddComponent<BoxCollider2D>().size = new Vector2(wall.w, wall.h);

            // 입구 쪽은 기상실까지, 출구 쪽은 어둠의 문 위까지 덮도록 밝기 전환 구역 확장
            darkness.InteriorArea = ToRect(manifest.interiorArea);
            darkness.EntryArea = ToRect(manifest.entryArea);
            darkness.ExitArea = ToRect(manifest.exitArea);
            darkness.EntryLineOffset = manifest.entryLineOffset;
            darkness.ExitLineOffset = manifest.exitLineOffset;

            // 심층 통로 끝의 어둠의 문이며 객체 위치가 문턱의 가운데
            Transform oldGate = preview.Find(DarkGateName);
            if (oldGate != null) UnityEngine.Object.DestroyImmediate(oldGate.gameObject);
            Vector2 gatePosition = preview.InverseTransformPoint(new Vector3(manifest.darkGate[0], manifest.darkGate[1], 0f));
            var gate = CreateChild(preview, DarkGateName, gatePosition).AddComponent<SpriteRenderer>();
            gate.sprite = LoadSprite(manifest.darkGateTexture);
            gate.sharedMaterial = room.sharedMaterial;
            gate.sortingOrder = DarkGateOrder;

            // 문 속의 진입 감지이며 처치 뒤 Player가 들어오면 다음 묶음으로 넘어감
            var gateArea = gate.gameObject.AddComponent<BoxCollider2D>();
            gateArea.isTrigger = true;
            gateArea.offset = new Vector2(manifest.darkGateTrigger[0], manifest.darkGateTrigger[1]);
            gateArea.size = new Vector2(manifest.darkGateTrigger[2], manifest.darkGateTrigger[3]);
            map.Exit = gate.gameObject.AddComponent<StageExit>();

            // 이 맵에 처음 들어오면 걸어 들어오지 않고 시작 위치에서 깨어남
            map.WakesOnFirstEntry = true;

            // 시작 통로 안의 입장 지점·시작 위치·재도전 위치와 입구 문 앞의 수정
            if (map.EntranceSpawnPoint == null || map.EntranceStopPoint == null) throw new InvalidOperationException("Guard map needs entrance points.");
            map.EntranceSpawnPoint.position = new Vector3(manifest.entranceSpawn[0], manifest.entranceSpawn[1], 0f);
            map.EntranceStopPoint.position = new Vector3(manifest.entranceStop[0], manifest.entranceStop[1], 0f);
            MoveCheckpoint(map, "start", manifest.checkpoint);
            MoveCheckpoint(map, "mid", manifest.respawn);
            var gateCrystal = root.GetComponentInChildren<CrystalCheckPoint>(true);
            if (gateCrystal == null) throw new InvalidOperationException("Guard map needs a checkpoint crystal.");
            gateCrystal.transform.position = new Vector3(manifest.crystal[0], manifest.crystal[1], 0f);

            // 그림 전체에 맞춘 카메라 세로 범위
            map.CameraVerticalLimits = new Vector2(manifest.cameraVerticalLimits[0], manifest.cameraVerticalLimits[1]);
            PrefabUtility.SaveAsPrefabAsset(root, MapPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>저장 식별자가 같은 체크포인트의 배치 위치를 목록의 월드 위치로 이동</summary>
    private static void MoveCheckpoint(MapManager map, string id, float[] position)
    {
        foreach (MapManager.CheckpointSpawn checkpoint in map.Checkpoints)
            if (checkpoint != null && checkpoint.Id == id && checkpoint.Position != null)
            {
                checkpoint.Position.position = new Vector3(position[0], position[1], 0f);
                return;
            }
        throw new InvalidOperationException("Guard map has no checkpoint '" + id + "'.");
    }

    /// <summary>목록 파일의 x·y·폭·높이 네 값을 사각 범위로 변환</summary>
    private static Rect ToRect(float[] values) => new Rect(values[0], values[1], values[2], values[3]);

    /// <summary>Assets 기준 상대 경로의 Sprite를 읽고 없으면 실패 처리</summary>
    private static Sprite LoadSprite(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + relativePath);
        if (sprite == null) throw new InvalidOperationException("Sprite is missing: " + relativePath);
        return sprite;
    }

    /// <summary>부모의 레이어를 물려받는 빈 자식 객체 생성</summary>
    private static GameObject CreateChild(Transform parent, string name, Vector2 localPosition)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.layer = parent.gameObject.layer;
        return child;
    }
}
