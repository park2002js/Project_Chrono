using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>기존 프리팹의 참조를 보존하며 Draw 입력·임시 표현만 연결</summary>
public static class PlayerDrawSetup
{
    /// <summary>명시적으로 만든 검사 요청 파일이 있을 때만 다음 Editor 갱신에서 실행</summary>
    [InitializeOnLoadMethod]
    private static void RunRequestedChecks()
    {
        string request = "Temp/RunDrawChecks";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += RunChecks; // 도메인 재로드 완료 후 자산 연결·기존 Play 검사 시작
    }

    /// <summary>기존 자산 연결 후 전투·피격·수정·부활·효과와 흑백 재질을 포함한 통합 Play Mode 검사 실행</summary>
    [MenuItem("Tools/Chrono/Run Draw And Slash Checks")]
    public static void RunChecks()
    {
        Run();
        EditorApplication.delayCall += BeginChecksWhenReady; // 자산 저장·가져오기를 끝낸 다음 Editor 갱신에서 Play 진입
    }

    /// <summary>스크립트·에셋 가져오기 중 Play 진입으로 초기화가 끊기지 않도록 대기</summary>
    private static void BeginChecksWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BeginChecksWhenReady; // 현재 가져오기·컴파일이 끝난 뒤 검사 시작 재개
            return;
        }
        EditorApplication.isPaused = false;
        if (File.Exists("Temp/RunDrawChecks")) File.Delete("Temp/RunDrawChecks");
        StartupFlowChecks.Run();
    }

    /// <summary>Draw에 필요한 프리팹·표현·입력을 기존 구성에 추가</summary>
    [MenuItem("Tools/Chrono/Connect Draw Mode")]
    public static void Run()
    {
        // 임시 시각 자산을 재사용하고 처음에만 생성
        Directory.CreateDirectory("Assets/Prefabs/Draw");
        Directory.CreateDirectory("Assets/Graphics/TemporaryDraw");
        Sprite pixel = PreparePixel();
        string materialPath = "Assets/Graphics/TemporaryDraw/Draw.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        GameObject point = PrepareSprite("DrawPoint", pixel, new Color(0.3f, 1f, 1f), 100, Vector3.one * 0.18f);
        GameObject shade = PrepareSprite("DrawShade", pixel, new Color(0f, 0f, 0.04f, 0.65f), 50, Vector3.one);
        GameObject preview = PrepareLine("DrawPreview", material, new Color(0.3f, 0.9f, 1f, 0.6f));
        GameObject line = PrepareLine("DrawLine", material, new Color(0.3f, 1f, 0.85f));
        GameObject expired = PrepareLine("DrawExpired", material, new Color(1f, 0.55f, 0.2f));
        GameObject slash = PrepareLine("SlashFlash", material, Color.white);

        // Player에서 표현과 능력을 연결하고 기존 몸체·무기·GUID 보존
        string playerPath = "Assets/Prefabs/Startup/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            if (player.GetComponent<SkillController>() == null) player.AddComponent<SkillController>();
            if (player.GetComponent<CharacterBuff>() == null) player.AddComponent<CharacterBuff>();
            DrawVfx vfx = player.GetComponent<DrawVfx>();
            if (vfx.PointPrefab == null) vfx.PointPrefab = point;
            if (vfx.PreviewLinePrefab == null) vfx.PreviewLinePrefab = preview;
            if (vfx.LinePrefab == null) vfx.LinePrefab = line;
            if (vfx.ExpiryLinePrefab == null) vfx.ExpiryLinePrefab = expired;
            if (vfx.SlashPrefab == null) vfx.SlashPrefab = slash;
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        // 전용 Draw 화면을 기존 HUD에 추가
        string uiPath = "Assets/Prefabs/Startup/GameUI.prefab";
        GameObject ui = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            UI_Draw view = ui.GetComponent<UI_Draw>();
            if (view == null) view = ui.AddComponent<UI_Draw>();
            if (view.ShadePrefab == null) view.ShadePrefab = shade;
            PrefabUtility.SaveAsPrefabAsset(ui, uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(ui); }

        // 실제 PlayerInput의 두 맵을 UnityEvents에 연결
        string inputPath = "Assets/Prefabs/Startup/PlayerInput.prefab";
        GameObject input = PrefabUtility.LoadPrefabContents(inputPath);
        try
        {
            PlayerMovementSetup.ConnectInputActions(input, input.GetComponent<PlayerInputController>());
            PrefabUtility.SaveAsPrefabAsset(input, inputPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(input); }
        PlayerProgressionSetup.Run();
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerDrawSetup] Draw actions, editable points, line prefabs and shade connected.");
    }

    /// <summary>임시 Sprite의 단위 크기를 명시한 흰 픽셀 자산 준비</summary>
    private static Sprite PreparePixel()
    {
        string path = "Assets/Graphics/TemporaryDraw/Pixel.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        if (!File.Exists(path))
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 1f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>지정한 Sprite와 정렬 순서의 임시 점·암전 프리팹 작성</summary>
    private static GameObject PrepareSprite(string name, Sprite pixel, Color color, int order, Vector3 scale)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Draw/" + name + ".prefab");
        if (existing != null) return existing;
        var root = new GameObject(name);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = order;
        root.transform.localScale = scale;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Draw/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>교체 규격인 로컬 X 길이1·폭1의 임시 선 프리팹 작성</summary>
    private static GameObject PrepareLine(string name, Material material, Color color)
    {
        string path = "Assets/Prefabs/Draw/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            // 이미 만든 임시 선의 스타일·GUID를 보존하며 필수 폭 보정만 보완
            if (existing.GetComponent<LineRenderer>() != null && existing.GetComponent<DrawLineVfx>() == null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    contents.AddComponent<DrawLineVfx>();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return existing;
        }
        var root = new GameObject(name);
        LineRenderer renderer = root.AddComponent<LineRenderer>();
        renderer.sharedMaterial = material;
        renderer.useWorldSpace = false;
        renderer.positionCount = 2;
        renderer.SetPosition(0, Vector3.zero);
        renderer.SetPosition(1, Vector3.right);
        renderer.startWidth = renderer.endWidth = 1f;
        renderer.startColor = renderer.endColor = color;
        renderer.sortingOrder = 100;
        root.AddComponent<DrawLineVfx>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Draw/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
