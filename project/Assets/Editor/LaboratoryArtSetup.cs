using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>보관된 실험실 아트를 독립 타일·문 애니메이션·배치 프리팹으로 가져오고 검사</summary>
public static class LaboratoryArtSetup
{
    private const string Root = "Assets/Art/Environment/Laboratory"; // 생성 에셋만 갱신할 전용 폴더
    private const string Source = "../art/archive/environment/laboratory/final/unity"; // 편집 원본에서 출력한 최종 입력 폴더

    /// <summary>메뉴 또는 배치 실행에서 기존 씬을 변경하지 않고 아트 패키지를 재생성</summary>
    [MenuItem("Tools/Chrono/Import Laboratory Art")]
    public static void Run()
    {
        // 입력을 보존하며 전용 에셋 폴더에 복사
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before importing art.");
        foreach (string folder in new[] { "Textures", "Data", "Tiles", "Animations", "Prefabs" }) Directory.CreateDirectory($"{Root}/{folder}");
        foreach (string name in new[] { "environment-tileset.png", "entrance-sheet.png", "exit-sheet.png", "layout.json", "entrance-animation.json", "exit-animation.json" })
        {
            string target = $"{Root}/{(name.EndsWith(".png") ? "Textures" : "Data")}/{name}";
            byte[] bytes = File.ReadAllBytes(Path.Combine(Source, name));
            if (!File.Exists(target) || !bytes.SequenceEqual(File.ReadAllBytes(target))) File.WriteAllBytes(target, bytes);
        }
        AssetDatabase.Refresh();
        Layout layout = Read<Layout>("layout.json");
        Require(layout.ppu > 0 && layout.cell_px > 0 && layout.canvas_px.Length == 2, "Invalid layout dimensions.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        Report report = new Report();
        try
        {
            // 고정 이름으로 Sprite와 Tile 참조를 유지하고 독립 문 프리팹 생성
            Sprite[] tiles = Slice("environment-tileset.png", layout.atlas.Select(t => t.name).ToArray(), layout.atlas.Select(t => t.rect).ToArray(), layout.ppu, new Vector2(0.5f, 0.5f));
            Dictionary<int, Tile> tileAssets = new Dictionary<int, Tile>();
            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = Asset<Tile>($"Tiles/{layout.atlas[i].name}.asset");
                tile.sprite = tiles[i];
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tileAssets.Add(layout.atlas[i].id, tile);
            }
            Material material = Asset<Material>("LaboratoryUnlit.mat", () => new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")));
            GameObject entrance = Door("entrance", material, layout.ppu, preview, report);
            GameObject exit = Door("exit", material, layout.ppu, preview, report);

            // 이미지의 좌상단 픽셀 좌표를 Grid의 좌하단 월드 좌표로 변환
            GameObject map = new GameObject("LaboratoryLayout", typeof(Grid));
            SceneManager.MoveGameObjectToScene(map, preview);
            map.GetComponent<Grid>().cellSize = Vector3.one * (layout.cell_px / (float)layout.ppu);
            foreach (Layer layer in layout.layers)
            {
                GameObject child = new GameObject(layer.name, typeof(Tilemap), typeof(TilemapRenderer));
                child.transform.SetParent(map.transform, false);
                Tilemap tilemap = child.GetComponent<Tilemap>();
                TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = layer.order;
                foreach (Cell cell in layer.cells)
                {
                    Require(cell.x % layout.cell_px == 0 && cell.y % layout.cell_px == 0, "Cell is off the native grid.");
                    tilemap.SetTile(new Vector3Int(cell.x / layout.cell_px, (layout.canvas_px[1] - cell.y - layout.cell_px) / layout.cell_px, 0), tileAssets[cell.tile_id]);
                    report.placed_cells++;
                }
            }
            foreach (DoorPlacement door in layout.doors)
            {
                Require(door.initial_state == "closed", "This package starts doors closed.");
                GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(door.type == "entrance" ? entrance : exit, preview);
                child.name = door.name;
                child.transform.SetParent(map.transform, false);
                child.transform.localPosition = new Vector3((door.rect[0] + door.rect[2] * 0.5f) / layout.ppu, (layout.canvas_px[1] - door.rect[1] - door.rect[3]) / (float)layout.ppu, 0f);
                child.GetComponent<SpriteRenderer>().sortingOrder = layout.layers.Max(l => l.order) + 1;
            }
            PrefabUtility.SaveAsPrefabAsset(map, $"{Root}/Prefabs/LaboratoryLayout.prefab");
            AssetDatabase.SaveAssets();

            // 재로드된 배치·타일·문 참조와 충돌 컴포넌트 부재 확인
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/LaboratoryLayout.prefab");
            Require(saved.GetComponentsInChildren<Tilemap>().Length == layout.layers.Length, "Tilemap layer count mismatch.");
            Require(saved.GetComponentsInChildren<Animator>().Length == layout.doors.Length, "Door instance count mismatch.");
            Require(saved.GetComponentsInChildren<Collider2D>().Length == 0, "Art package must not add collision rules.");
            foreach (Layer layer in layout.layers)
            {
                Tilemap tilemap = saved.transform.Find(layer.name).GetComponent<Tilemap>();
                foreach (Cell cell in layer.cells)
                {
                    Require(tilemap.GetTile(new Vector3Int(cell.x / layout.cell_px, (layout.canvas_px[1] - cell.y - layout.cell_px) / layout.cell_px, 0)) == tileAssets[cell.tile_id], "Saved tile placement mismatch.");
                    report.saved_cells_verified++;
                }
            }
            report.tiles = tiles.Length;
            report.tilemap_layers = layout.layers.Length;
            report.door_instances = layout.doors.Length;
            report.ppu = layout.ppu;
            report.camera_orthographic_size_for_180_native_pixels = 90f / layout.ppu;
            report.success = true;
            Debug.Log($"[LaboratoryArtSetup] PASS: {report.tiles} tiles, {report.placed_cells} cells, {report.clips} clips, {report.sampled_frames} sampled frames.");
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            throw;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            string result = Path.GetFullPath(Path.Combine(Source, "../validation/UnityImport.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(result));
            File.WriteAllText(result, JsonUtility.ToJson(report, true));
        }
    }

    /// <summary>투명 프레임도 유지하며 Sprite 이름에 연결된 식별자를 재사용</summary>
    private static Sprite[] Slice(string file, string[] names, int[][] rects, int ppu, Vector2 pivot)
    {
        string path = $"{Root}/Textures/{file}";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 8192;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        // 바뀐 크기와 기존 Sprite GUID를 함께 적용
        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        Dictionary<string, SpriteRect> previous = provider.GetSpriteRects().ToDictionary(r => r.name);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        SpriteRect[] slices = names.Select((name, i) => new SpriteRect { name = name, rect = new Rect(rects[i][0], texture.height - rects[i][1] - rects[i][3], rects[i][2], rects[i][3]), alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = previous.ContainsKey(name) ? previous[name].spriteID : GUID.Generate() }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        Require(sprites.Count == names.Length, $"Missing Sprite slices: {file}");
        return names.Select((name, i) => { Sprite sprite = sprites[name]; Require(sprite.rect.width == rects[i][2] && sprite.rect.height == rects[i][3] && sprite.pixelsPerUnit == ppu, "Sprite dimensions changed during import."); return sprite; }).ToArray();
    }

    /// <summary>상태별 클립과 개폐 Trigger만 가진 문 프리팹 생성 및 프레임 검사</summary>
    private static GameObject Door(string name, Material material, int ppu, Scene preview, Report report)
    {
        AnimationData data = Read<AnimationData>($"{name}-animation.json");
        int width = data.frame_size_px[0], height = data.frame_size_px[1];
        Sprite[] sprites = Slice($"{name}-sheet.png", data.frames.Select(f => $"{name}-{f.frame:00}").ToArray(), data.frames.Select((f, i) => new[] { i * width, 0, width, height }).ToArray(), ppu, new Vector2(0.5f, 0f));
        string controllerPath = $"{Root}/Animations/{name}.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "Open", type = AnimatorControllerParameterType.Trigger }, new AnimatorControllerParameter { name = "Close", type = AnimatorControllerParameterType.Trigger } };
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> states = machine.states.ToDictionary(s => s.state.name, s => s.state);
        GameObject model = new GameObject(name == "entrance" ? "LaboratoryEntrance" : "LaboratoryExit", typeof(SpriteRenderer), typeof(Animator));
        SceneManager.MoveGameObjectToScene(model, preview);
        SpriteRenderer renderer = model.GetComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;

        // JSON의 밀리초를 Sprite 키로 변환
        foreach (Tag tag in data.tags)
        {
            AnimationClip clip = Asset<AnimationClip>($"Animations/{name}-{tag.name}.anim");
            clip.frameRate = 100f;
            List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>();
            int elapsed = 0;
            for (int i = tag.from - 1; i < tag.to; i++)
            {
                keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f, value = sprites[i] });
                elapsed += data.frames[i].duration_ms;
            }
            keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f - 1f / clip.frameRate, value = keys[keys.Count - 1].value });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            Require(Mathf.Abs(clip.length - elapsed / 1000f) < 0.0001f, $"Clip duration mismatch: {name}/{tag.name} {clip.length}");
            if (!states.ContainsKey(tag.name)) states[tag.name] = machine.AddState(tag.name);
            states[tag.name].motion = clip;
            foreach (AnimatorStateTransition old in states[tag.name].transitions) states[tag.name].RemoveTransition(old);
            report.clips++;
        }

        // 조건 판정은 외부 게임 코드가 수행하고 문은 요청 이후의 연출만 재생
        machine.defaultState = states["closed"];
        Transition(states["closed"], states.ContainsKey("activation") ? states["activation"] : states["opening"], "Open");
        if (states.ContainsKey("activation")) Transition(states["activation"], states["opening"]);
        Transition(states["opening"], states["open"]);
        Transition(states["open"], states["closing"], "Close");
        Transition(states["closing"], states["closed"]);
        renderer.sprite = sprites[0];
        model.GetComponent<Animator>().runtimeAnimatorController = controller;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, $"{Root}/Prefabs/{model.name}.prefab");
        UnityEngine.Object.DestroyImmediate(model);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 저장된 상태 전이의 목적지·Trigger·종료 시점 확인
        AnimatorStateMachine savedMachine = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath).layers[0].stateMachine;
        Require(savedMachine.defaultState.name == "closed", "Door must start closed.");
        for (int i = 0; i < data.tags.Length; i++)
        {
            AnimatorState state = savedMachine.states.Single(s => s.state.name == data.tags[i].name).state;
            string trigger = state.name == "closed" ? "Open" : state.name == "open" ? "Close" : null;
            Require(state.transitions.Length == 1, "Each door state must have one outgoing transition.");
            AnimatorStateTransition edge = state.transitions[0];
            Require(edge.destinationState.name == data.tags[(i + 1) % data.tags.Length].name && edge.duration == 0f && edge.hasExitTime == (trigger == null), "Saved door transition mismatch.");
            Require(trigger == null ? edge.conditions.Length == 0 && edge.exitTime == 1f : edge.conditions.Length == 1 && edge.conditions[0].parameter == trigger, "Saved door transition condition mismatch.");
            report.controller_transitions++;
        }

        // 저장한 문 프리팹에 각 클립을 적용하여 투명 프레임까지 재검사
        GameObject sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
        sample.GetComponent<Animator>().enabled = false;
        foreach (Tag tag in data.tags)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Root}/Animations/{name}-{tag.name}.anim");
            int elapsed = 0;
            for (int i = tag.from - 1; i < tag.to; i++)
            {
                clip.SampleAnimation(sample, (elapsed + data.frames[i].duration_ms * 0.5f) / 1000f);
                Require(sample.GetComponent<SpriteRenderer>().sprite == sprites[i], $"Saved prefab animation mismatch: {name}/{i + 1}");
                elapsed += data.frames[i].duration_ms;
                report.sampled_frames++;
            }
            Require(Mathf.Abs(clip.length - elapsed / 1000f) < 0.0001f, $"Saved clip duration mismatch: {name}/{tag.name}");
        }
        UnityEngine.Object.DestroyImmediate(sample);
        return prefab;
    }

    /// <summary>지연 없는 Trigger 또는 클립 종료 전환 생성</summary>
    private static void Transition(AnimatorState from, AnimatorState to, string trigger = null)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = trigger == null;
        transition.exitTime = 1f;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        if (trigger != null) transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    /// <summary>기존 에셋을 유지하고 없는 생성물만 새 GUID로 작성</summary>
    private static T Asset<T>(string relative, Func<T> create = null) where T : UnityEngine.Object
    {
        string path = $"{Root}/{relative}";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = create != null ? create() : typeof(ScriptableObject).IsAssignableFrom(typeof(T)) ? (T)(UnityEngine.Object)ScriptableObject.CreateInstance(typeof(T)) : (T)Activator.CreateInstance(typeof(T));
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>패키지 데이터 읽기</summary>
    private static T Read<T>(string name) { return JsonUtility.FromJson<T>(File.ReadAllText($"{Root}/Data/{name}")); }
    /// <summary>불일치가 있으면 부분 성공 대신 가져오기 중단</summary>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    /// <summary>방·복도 배치 입력</summary>
    [Serializable] private class Layout
    {
        public int ppu; // 픽셀과 월드 단위 비율
        public int cell_px; // 정사각 타일 픽셀 크기
        public int[] canvas_px; // 전체 캔버스 크기
        public AtlasTile[] atlas; // 사용 타일 목록
        public Layer[] layers; // 렌더 순서와 셀 배치
        public DoorPlacement[] doors; // 독립 문 위치
    }
    /// <summary>타일 시트의 개별 영역</summary>
    [Serializable] private class AtlasTile { public int id; // 배치 참조 번호
        public string name; // 재가져오기 때 유지할 이름
        public int[] rect; // 좌상단 기준 픽셀 영역
    }
    /// <summary>타일맵 한 층</summary>
    [Serializable] private class Layer { public string name; // 층 이름
        public int order; // 렌더 순서
        public Cell[] cells; // 타일 배치 목록
    }
    /// <summary>단일 타일 배치</summary>
    [Serializable] private class Cell { public int x; // 좌상단 가로 픽셀 좌표
        public int y; // 좌상단 세로 픽셀 좌표
        public int tile_id; // 참조할 타일 번호
    }
    /// <summary>문 프리팹 배치 입력</summary>
    [Serializable] private class DoorPlacement { public string name; // 문 인스턴스 이름
        public string type; // entrance 또는 exit 종류
        public int[] rect; // 공통 바닥 앵커를 계산할 기존 문 영역
        public string initial_state; // 최초 closed 상태
    }
    /// <summary>문 프레임과 상태 구간</summary>
    [Serializable] private class AnimationData { public int[] frame_size_px; // 한 프레임 캔버스
        public Frame[] frames; // 순서와 밀리초 시간
        public Tag[] tags; // 상태별 구간
    }
    /// <summary>단일 프레임 시간</summary>
    [Serializable] private class Frame { public int frame; // 1부터 시작하는 번호
        public int duration_ms; // 해당 그림의 유지 시간
    }
    /// <summary>애니메이션 상태 구간</summary>
    [Serializable] private class Tag { public string name; // Animator 상태 이름
        public int from; // 시작 프레임 포함
        public int to; // 마지막 프레임 포함
    }
    /// <summary>실제 Unity 가져오기와 샘플링 결과</summary>
    [Serializable] private class Report { public bool success; // 전체 검사 통과 여부
        public string error; // 실패 시 실제 예외
        public int tiles; // 가져온 고유 타일 수
        public int placed_cells; // 타일맵에 배치한 셀 수
        public int saved_cells_verified; // 프리팹 저장 후 타일 참조 대조 수
        public int tilemap_layers; // 정적 층 수
        public int door_instances; // 배치된 문 수
        public int clips; // 생성한 상태별 클립 수
        public int sampled_frames; // 중간 시점 샘플링 수
        public int controller_transitions; // 저장 후 대조한 개폐 상태 전이 수
        public int ppu; // 적용한 픽셀과 월드 단위 비율
        public float camera_orthographic_size_for_180_native_pixels; // 1080p에서 6배 표시에 필요한 직교 크기
    }
}
