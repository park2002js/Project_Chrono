using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>확정 UI 원본의 레이어·배치를 기존 Title과 GameUI 프리팹에 연결</summary>
public static class UIArtSetup
{
    private const string Art = "Assets/Art/UI/"; // 최종 UI와 원본 레이어에서 출력한 게임용 PNG 위치
    private const string FontPath = "Assets/Font/PF스타더스트 3.0.ttf"; // 프로젝트에 준비된 PF스타더스트 원본
    private const string HudPrefab = "Assets/Prefabs/Startup/GameUI.prefab"; // 기존 게임 HUD의 GUID를 유지할 저장 위치

    /// <summary>반복 호출해도 기존 연결을 보존하면서 이번 UI 아트만 갱신</summary>
    [MenuItem("Chrono/Apply Prepared UI Art")]
    public static void Apply()
    {
        // 원본 PNG와 레이어 출력을 픽셀 Sprite로 가져오기
        foreach (string file in Directory.GetFiles(Art + "HUD/Layers", "*.png")) ImportSprite(file.Replace('\\', '/'));
        ImportSprite(Art + "Title/title-logo.png");
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) throw new InvalidOperationException("PF스타더스트 font is missing.");

        // 기존 HUD 프리팹 안에만 Canvas와 네 Player 정보·Boss 바 구성
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefab);
        try
        {
            UIController ui = root.GetComponent<UIController>();
            RectTransform canvasRect = Rect(root.transform, "ArtCanvas");
            Canvas canvas = Component<Canvas>(canvasRect.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = Component<CanvasScaler>(canvasRect.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 최종 preview의 앵커·크기를 그대로 적용하고 네 정보는 같은 표시 루트 사용
            RectTransform player = Rect(canvasRect, "PlayerHUD");
            Stretch(player);
            ui.PlayerHudRoot = player.gameObject;
            ui.PlayerHealthFill = Bar(player, "PlayerHP", "player-hp", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 114f), new Vector2(540f, 66f), new Vector2(18f, -18f), new Vector2(504f, 30f));
            // 원본 Background 층에 합쳐진 예시 채움 64px만 #47AA9D→#5D5C6E로 바꾼 파생 empty PNG 사용
            RectTransform groggy = ImageRect(player, "Groggy", Art + "HUD/Layers/groggy-empty.png", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 66f), new Vector2(294f, 42f)).rectTransform;
            ui.GroggyFill = Fill(groggy, "Fill", Art + "HUD/Layers/groggy-full.png", Vector2.zero, new Vector2(294f, 42f));
            ui.DrawIcon = ImageRect(player, "Draw", Art + "HUD/draw.png", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-210f, 78f), new Vector2(102f, 102f));
            ui.SlashIcon = ImageRect(player, "Slash", Art + "HUD/slash.png", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-72f, 78f), new Vector2(102f, 102f));
            ui.DrawCooldown = Cooldown(ui.DrawIcon.rectTransform);
            ui.SlashCooldown = Cooldown(ui.SlashIcon.rectTransform);
            ImageRect(player, "DrawKey", Art + "Keyboard/key-shift-dark.png", new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-261f, 18f), new Vector2(48f, 48f));
            ImageRect(player, "SlashKey", Art + "Keyboard/key-f-dark.png", new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-123f, 18f), new Vector2(48f, 48f));

            // Boss는 기존 조우 흐름에서 연결될 때만 독립 표시
            RectTransform boss = Rect(canvasRect, "BossHUD");
            Stretch(boss);
            ui.BossHudRoot = boss.gameObject;
            ui.BossHealthFill = Bar(boss, "BossHP", "boss-hp", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(750f, 72f), new Vector2(18f, -18f), new Vector2(714f, 36f));
            player.gameObject.SetActive(false);
            boss.gameObject.SetActive(false);
            ui.AimCursor = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Cursor/aimCursor.png");
            ui.UIFont = font;
            ui.UseWorldInteractionPrompt = true;
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 기존 Title 씬 객체와 저장·버튼 동작을 유지하고 로고·글꼴만 연결
        var previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            UI_Title title = UnityEngine.Object.FindFirstObjectByType<UI_Title>();
            if (title == null) throw new InvalidOperationException("UI_Title is missing from Title scene.");
            title.TitleLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Title/title-logo.png");
            title.TitleFont = font;
            EditorUtility.SetDirty(title);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            // 배치 시작은 활성 씬 목록이 비어 있으므로 방금 저장한 Title을 유지
            foreach (var previous in previousScenes)
            {
                if (!previous.isLoaded || !previous.isActive) continue;
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                break;
            }
        }
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("[UIArtSetup] Prepared Title logo/font, four player HUD elements, boss HP and aim cursor connected.");
    }

    /// <summary>최종 프리팹의 아트 참조·미리보기 좌표·입력 안내 연결 검사</summary>
    public static void Validate()
    {
        UIController ui = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).GetComponent<UIController>();
        if (ui.PlayerHealthFill == null || ui.GroggyFill == null || ui.BossHealthFill == null || ui.DrawIcon == null || ui.SlashIcon == null || ui.AimCursor == null || ui.UIFont == null)
            throw new InvalidOperationException("Prepared UI art references are incomplete.");
        if (ui.PlayerHealthFill.transform.parent.GetComponent<RectTransform>().anchoredPosition != new Vector2(18f, 114f) || ui.BossHealthFill.transform.parent.GetComponent<RectTransform>().anchoredPosition != new Vector2(0f, -36f))
            throw new InvalidOperationException("HUD placement differs from the approved preview.");
        if (ui.PlayerHudRoot.transform.Find("DrawKey").GetComponent<Image>().sprite.name != "key-shift-dark" || ui.PlayerHudRoot.transform.Find("SlashKey").GetComponent<Image>().sprite.name != "key-f-dark")
            throw new InvalidOperationException("Default skill key sprites are not connected.");

        // 임포터 Read/Write 설정을 바꾸지 않고 임시 디코드로 빈 그로기 배경의 잔존 채움 검사
        Texture2D empty = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!empty.LoadImage(File.ReadAllBytes(Art + "HUD/Layers/groggy-empty.png")) || empty.width != 49 || empty.height != 7)
                throw new InvalidOperationException("Empty groggy PNG could not be decoded at its original size.");
            foreach (Color32 pixel in empty.GetPixels32())
                if (pixel.a > 0 && pixel.r == 71 && pixel.g == 170 && pixel.b == 157)
                    throw new InvalidOperationException("Empty groggy background still contains baked preview fill.");
        }
        finally { UnityEngine.Object.DestroyImmediate(empty); }
    }

    /// <summary>프레임과 수치 채움을 분리하되 사용자 원본의 테두리를 최상단에 보존</summary>
    private static Image Bar(Transform parent, string name, string file, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Vector2 fillPosition, Vector2 fillSize)
    {
        RectTransform bar = ImageRect(parent, name, Art + "HUD/Layers/" + file + "-empty.png", anchor, pivot, position, size).rectTransform;
        Image fill = Fill(bar, "Fill", Art + "HUD/Layers/" + file + "-fill.png", fillPosition, fillSize);
        ImageRect(bar, "Frame", Art + "HUD/Layers/" + file + "-frame.png", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, size);
        return fill;
    }

    /// <summary>현재 수치만으로 가로 채움을 조절할 Image 구성</summary>
    private static Image Fill(Transform parent, string name, string file, Vector2 position, Vector2 size)
    {
        Image image = ImageRect(parent, name, file, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        return image;
    }

    /// <summary>아이콘 내부에 아래에서 위로 차는 재사용 시간 가림막 구성</summary>
    private static Image Cooldown(RectTransform parent)
    {
        RectTransform rect = Rect(parent, "Cooldown");
        Place(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 6f), new Vector2(90f, 90f));
        Image image = Component<Image>(rect.gameObject);
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.color = new Color(0f, 0f, 0f, 0.65f);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Vertical;
        image.fillOrigin = 0;
        image.fillAmount = 0f;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>기존 이름의 RectTransform을 재사용하고 필요한 Sprite만 연결</summary>
    private static Image ImageRect(Transform parent, string name, string path, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name);
        Place(rect, anchor, pivot, position, size);
        Image image = Component<Image>(rect.gameObject);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (image.sprite == null) throw new InvalidOperationException("Missing UI sprite: " + path);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>같은 UI 제작 루트를 재사용해 중복 계층 생성 방지</summary>
    private static RectTransform Rect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>최종 배치표의 고정 앵커·피벗·오프셋·크기 적용</summary>
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>그룹 좌표를 부모 Canvas 전체와 일치</summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>이번 UI 연결에 필요한 컴포넌트만 없는 경우 추가</summary>
    private static T Component<T>(GameObject target) where T : UnityEngine.Component
    {
        T result = target.GetComponent<T>();
        return result != null ? result : target.AddComponent<T>();
    }

    /// <summary>원본 픽셀·알파를 유지하는 UI Single Sprite 임포트</summary>
    private static void ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }
}
