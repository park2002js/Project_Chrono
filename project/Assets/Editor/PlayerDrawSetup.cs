using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>기존 프리팹의 참조를 보존하며 Draw 입력과 이미지 FX 연결</summary>
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

    /// <summary>이미지 FX 연결 후 기존 검사 장치로 Draw·편집·수명·Slash Play Mode 검사 실행</summary>
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
        StartupFlowChecks.RunDraw();
    }

    /// <summary>Draw에 필요한 프리팹·표현·입력을 기존 구성에 추가</summary>
    [MenuItem("Tools/Chrono/Connect Draw Mode")]
    public static void Run()
    {
        // 암전·검격 재질은 유지하고 Draw 전용 알파 텍스처 재질 준비
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
        GameObject shade = PrepareSprite("DrawShade", pixel, new Color(0f, 0f, 0.04f, 0.65f), 50, Vector3.one);
        Material imageMaterial = PrepareImageMaterial(material);
        Material evaporationMaterial = PrepareEvaporationMaterial(imageMaterial);
        GameObject preview = PrepareLine("DrawPreview", imageMaterial, new Color(1f, 1f, 1f, 0.75f), true);
        GameObject line = PrepareLine("DrawLine", imageMaterial, Color.white, true);
        GameObject expired = PrepareLine("DrawExpired", imageMaterial, Color.white, true);
        GameObject slash = PrepareLine("SlashFlash", material, Color.white);

        // Player에서 표현과 능력을 연결하고 기존 몸체·무기·GUID 보존
        string playerPath = "Assets/Prefabs/Startup/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            if (player.GetComponent<SkillController>() == null) player.AddComponent<SkillController>();
            if (player.GetComponent<CharacterBuff>() == null) player.AddComponent<CharacterBuff>();
            DrawVfx vfx = player.GetComponent<DrawVfx>();
            if (vfx.PreviewLinePrefab == null) vfx.PreviewLinePrefab = preview;
            if (vfx.LinePrefab == null) vfx.LinePrefab = line;
            if (vfx.ExpiryLinePrefab == null) vfx.ExpiryLinePrefab = expired;
            vfx.EvaporationMaterial = evaporationMaterial;
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
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerDrawSetup] Draw image FX, logical point editing and shade connected without point visuals.");
    }

    /// <summary>흰 중심·청록 잔광 PNG를 부드러운 알파 경계의 공용 Draw 재질로 연결</summary>
    private static Material PrepareImageMaterial(Material source)
    {
        // 압축·픽셀 필터 없이 텍스처의 연속 알파를 그대로 사용
        string texturePath = "Assets/Art/FX/Draw/draw-line-profile.png";
        AssetDatabase.ImportAsset(texturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer == null) throw new FileNotFoundException("Draw image FX texture is missing.", texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        // 같은 재질 자산과 GUID를 재사용하고 최초 연결 때만 생성
        string path = "Assets/Art/FX/Draw/DrawImageFX.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        Shader shader = Shader.Find("Chrono/DrawImageFx");
        if (shader == null) throw new FileNotFoundException("Draw image FX shader is missing.");
        material.shader = shader;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>작은 원형 알갱이의 재질을 Line 원본 팔레트에 연결</summary>
    private static Material PrepareEvaporationMaterial(Material imageMaterial)
    {
        // 원형은 셰이더로 유지하고 실제 선 이미지에서는 팔레트 색만 참조
        Shader shader = Shader.Find("Chrono/DrawEvaporationParticle");
        if (shader == null) throw new FileNotFoundException("Draw evaporation particle shader is missing.");
        string path = "Assets/Art/FX/Draw/DrawEvaporation.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_LinePaletteTex", imageMaterial.mainTexture);
        EditorUtility.SetDirty(material);
        return material;
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

    /// <summary>지정한 Sprite와 정렬 순서의 암전 프리팹 작성</summary>
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

    /// <summary>로컬 X 길이1·폭1의 선 프리팹을 준비하고 요청한 Draw 스타일만 갱신</summary>
    private static GameObject PrepareLine(string name, Material material, Color color, bool applyStyle = false)
    {
        string path = "Assets/Prefabs/Draw/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            // 기존 프리팹 GUID와 검격 스타일은 보존하고 Draw 재질만 교체
            if (existing.GetComponent<LineRenderer>() != null && (applyStyle || existing.GetComponent<DrawLineVfx>() == null))
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    DrawLineVfx width = contents.GetComponent<DrawLineVfx>();
                    if (width == null) width = contents.AddComponent<DrawLineVfx>();
                    if (applyStyle)
                    {
                        LineRenderer existingLine = contents.GetComponent<LineRenderer>();
                        existingLine.sharedMaterial = material;
                        existingLine.startColor = existingLine.endColor = color;
                        existingLine.textureMode = LineTextureMode.Stretch;
                        existingLine.numCapVertices = 0;
                        width.WidthMultiplier = 3f;
                    }
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
        root.AddComponent<DrawLineVfx>().WidthMultiplier = applyStyle ? 3f : 1f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Draw/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
