using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>기존 방·문·수정을 유지하고 남쪽 입구 복도와 최초 입장 위치만 확장</summary>
public static class MapEntranceSetup
{
    private const string MapPath = "Assets/Prefabs/Startup/Map.prefab"; // 기존 배치와 식별자를 보존할 실제 Map 프리팹
    private const string CameraPath = "Assets/Prefabs/Startup/WorldCamera.prefab"; // 확장한 남쪽 바닥까지 화면을 제한할 카메라 프리팹
    private const string SystemsPath = "Assets/Prefabs/Startup/GameSystems.prefab"; // 최초 입장 연출 담당을 추가할 앱 관리자 프리팹
    private const int SouthExtensionRows = 6; // 시작부터 첫 입구문 중심까지3.1을9.1world로 늘릴 추가 행
    private const float OriginalNorthEdge = 77f; // 위치를 유지할 기존 맵의 북쪽 바깥 경계

    /// <summary>원본 아트와 다른 배치를 재생성하지 않고 관련 세 프리팹만 연결</summary>
    [MenuItem("Tools/Chrono/Extend Entrance Corridor")]
    public static void Apply()
    {
        // 기존 에셋을 가져온 뒤 현재 씬을 바꾸지 않고 해당 프리팹만 열기
        AssetDatabase.Refresh();
        foreach (string path in new[] { MapPath, CameraPath, SystemsPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (path == MapPath) ApplyToPrefab(root);
                else if (path == CameraPath) ApplyToCameraPrefab(root);
                else if (root.GetComponent<TutorialController>() == null) root.AddComponent<TutorialController>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>기존 Map의 남쪽에만 여섯 행을 붙이고 시작 위치를 입장 정지점으로 재사용</summary>
    public static void ApplyToPrefab(GameObject root)
    {
        // 기존 체크포인트·Grid가 없으면 무관한 임시 배치를 만들지 않고 중단
        if (root == null) throw new ArgumentNullException(nameof(root));
        MapManager map = root.GetComponent<MapManager>();
        Transform layout = root.transform.Find("LaboratoryLayout");
        Grid grid = layout != null ? layout.GetComponent<Grid>() : null;
        if (map == null || grid == null || !Mathf.Approximately(grid.cellSize.x, 1f) || !Mathf.Approximately(grid.cellSize.y, 1f))
            throw new InvalidOperationException("Entrance extension requires the existing one-world-unit Laboratory Grid and Map.");
        Transform stop = map.GetCheckpoint("start");

        // 기존 타일 레이어는 그대로 두고 같은 Grid 아래의 전용 보충 레이어만 재사용
        Transform extension = layout.Find("EntranceExtension");
        if (extension == null)
        {
            extension = new GameObject("EntranceExtension").transform;
            extension.SetParent(layout, false);
        }
        extension.localPosition = Vector3.zero;
        extension.localRotation = Quaternion.identity;
        extension.localScale = Vector3.one;
        int cells = 0;
        foreach (string name in new[] { "outside", "corridor-floor", "corridor-walls" })
            cells += CopySouthLayer(layout, extension, name);
        if (cells != 216) throw new InvalidOperationException("Entrance extension must add29 outside,5 floor and2 wall cells per row.");

        // 같은 충돌체의 남쪽 끝만 늘려 기존 문·조우·다른 방 경계를 보존
        ExtendBoundaries(root.transform.Find("WorldCollision"));
        Transform spawn = root.transform.Find("EntranceSpawnPoint");
        if (spawn == null)
        {
            spawn = new GameObject("EntranceSpawnPoint").transform;
            spawn.SetParent(root.transform, false);
        }
        spawn.localPosition = root.transform.InverseTransformPoint(stop.position) + Vector3.down * SouthExtensionRows;
        spawn.localRotation = Quaternion.identity;
        spawn.localScale = Vector3.one;
        map.EntranceSpawnPoint = spawn;
        map.EntranceStopPoint = stop;
        EditorUtility.SetDirty(map);
    }

    /// <summary>줌·추적 감쇠·북쪽 제한을 유지하며 추가된 남쪽 바닥만 카메라 범위에 반영</summary>
    public static void ApplyToCameraPrefab(GameObject root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        GameCamera camera = root.GetComponent<GameCamera>();
        if (camera == null) throw new InvalidOperationException("Entrance extension requires the existing GameCamera.");
        camera.VerticalLimits = new Vector2(-SouthExtensionRows, camera.VerticalLimits.y);
        EditorUtility.SetDirty(camera);
    }

    /// <summary>현재 팔레트·Tile GUID·렌더 설정을 그대로 사용해 남쪽 전용 행 복사</summary>
    private static int CopySouthLayer(Transform layout, Transform extension, string name)
    {
        // 원본 레이어의 실제 Tile 참조와 재질을 확보하고 별도 보충 Tilemap 준비
        Transform original = layout.Find(name);
        Tilemap source = original != null ? original.GetComponent<Tilemap>() : null;
        TilemapRenderer sourceRenderer = original != null ? original.GetComponent<TilemapRenderer>() : null;
        if (source == null || sourceRenderer == null) throw new InvalidOperationException("Missing entrance source Tilemap: " + name);
        Transform child = extension.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)).transform;
            child.SetParent(extension, false);
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        child.gameObject.layer = original.gameObject.layer;
        Tilemap target = child.GetComponent<Tilemap>();
        TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
        if (target == null || renderer == null) throw new InvalidOperationException("Invalid entrance extension layer: " + name);
        target.ClearAllTiles();
        target.tileAnchor = source.tileAnchor;
        target.orientation = source.orientation;
        target.orientationMatrix = source.orientationMatrix;
        target.color = source.color;
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder;
        renderer.mode = sourceRenderer.mode;
        renderer.sortOrder = sourceRenderer.sortOrder;

        // 남쪽 여섯 행만 채우며 기존 바닥의 네 행 무늬를 반복
        int count = 0;
        BoundsInt bounds = source.cellBounds;
        for (int y = -SouthExtensionRows; y < 0; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int sample = new Vector3Int(x, (y + SouthExtensionRows) % 4, 0);
                TileBase tile = source.GetTile(sample);
                if (tile == null) continue;
                Vector3Int point = new Vector3Int(x, y, 0);
                target.SetTile(point, tile);
                target.SetTileFlags(point, TileFlags.None);
                target.SetColor(point, source.GetColor(sample));
                target.SetTransformMatrix(point, source.GetTransformMatrix(sample));
                target.SetTileFlags(point, source.GetTileFlags(sample));
                count++;
            }
        target.CompressBounds();
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(renderer);
        return count;
    }

    /// <summary>기존 하단·입구 양옆·바깥 양옆의 다섯 Collider만 고정 남쪽 경계로 확장</summary>
    private static void ExtendBoundaries(Transform collision)
    {
        if (collision == null) throw new InvalidOperationException("Missing existing WorldCollision.");
        int changed = 0;
        foreach (BoxCollider2D wall in collision.GetComponentsInChildren<BoxCollider2D>(true))
        {
            // 같은 이름의 Boundary를 좌표·크기로 구분하며 기존 위쪽 경계는 유지
            Vector2 center = (Vector2)wall.transform.localPosition + wall.offset;
            float top = center.y + wall.size.y * 0.5f;
            bool bottom = Near(center.x, 14.5f) && Near(wall.size.x, 31f) && Near(wall.size.y, 1f) && top <= 0.001f;
            bool entranceSide = (Near(center.x, 6f) || Near(center.x, 23f)) && Near(wall.size.x, 12f) && Near(top, 8f);
            bool outerSide = (Near(center.x, -0.5f) || Near(center.x, 29.5f)) && Near(wall.size.x, 1f) && Near(top, OriginalNorthEdge);
            if (!bottom && !entranceSide && !outerSide) continue;
            float low = bottom ? -SouthExtensionRows - 1f : -SouthExtensionRows;
            float high = bottom ? -SouthExtensionRows : top;
            Vector3 position = wall.transform.localPosition;
            position.y = (low + high) * 0.5f - wall.offset.y;
            wall.transform.localPosition = position;
            wall.size = new Vector2(wall.size.x, high - low);
            EditorUtility.SetDirty(wall.transform);
            EditorUtility.SetDirty(wall);
            changed++;
        }
        if (changed != 5) throw new InvalidOperationException("Entrance extension requires the five existing south/perimeter boundaries.");
    }

    /// <summary>직렬화된 기존 경계의 작은 소수점 오차 허용</summary>
    private static bool Near(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.001f;
    }
}
