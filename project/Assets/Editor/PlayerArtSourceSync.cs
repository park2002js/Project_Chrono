using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

/// <summary>작업 창 없이 Player와 검의 Aseprite 저장을 공식 importer와 게임 참조에 반영</summary>
[InitializeOnLoad]
public static class PlayerArtSourceSync
{
    public const string PlayerRoot = "Assets/Art/Player/Source/"; // 게임에 사용하는 Player 원본의 Unity 복사본
    public const string WeaponRoot = "Assets/Art/Weapons/Source/"; // 검과 칼집 원본의 Unity 복사본
    private const string AnimationSetPath = "Assets/Art/Player/Animations/PlayerSpriteAnimationSet.asset"; // 기존 GUID를 유지할 몸체 애니메이션 데이터
    private const string PlayerPrefabPath = "Assets/Prefabs/Startup/Player.prefab"; // 몸체와 카타나를 표시할 기본 Player
    private static readonly Source[] sources = CreateSources(); // 플레이어 자세와 카타나 원본의 자동 반영
    private static double nextPoll; // Editor에서 파일 확인을 반복할 다음 시각
    private static bool synchronizing; // 재임포트 콜백의 중복 동기화 방지
    private static bool bindingsPending = true; // Sprite 재임포트 후 게임 참조 갱신 요청
    private static string bindingsError; // 같은 참조 갱신 오류의 반복 출력 방지

    /// <summary>원본 파일 한 개의 저장 안정화와 마지막 반영 상태</summary>
    private class Source
    {
        public string Original; // 저장소 기준 편집 원본 경로
        public string Asset; // 공식 Aseprite importer가 읽을 Unity 경로
        public long ObservedStamp; // 저장 중 변경을 감지한 시각
        public long ObservedLength; // 저장 중 변경을 감지한 크기
        public long ImportedStamp = -1; // 마지막 반영한 저장 시각
        public long ImportedLength = -1; // 마지막 반영한 파일 크기
        public double StableSince; // 저장 시각과 크기가 같아진 Editor 시각
        public string Error; // 같은 오류를 반복 출력하지 않을 마지막 내용
    }

    static PlayerArtSourceSync()
    {
        // Editor 수명에 연결해 전용 창과 포커스 전환 없이 저장 확인
        EditorPrefs.SetBool("UnityEditor.U2D.Aseprite/ImportSettings.backgroundImport", true);
        EditorApplication.update += Poll; // Aseprite 저장을 Unity 메인 스레드에서 반영
        AssemblyReloadEvents.beforeAssemblyReload += Stop; // 도메인 재로드 전 이전 콜백 해제
    }

    /// <summary>재컴파일과 임포트가 끝난 Editor 갱신에서만 원본 확인</summary>
    private static void Poll()
    {
        // 플레이 도중에도 데이터 갱신은 허용하고 저장 중인 파일은 다음 갱신까지 대기
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.25;
        Synchronize();
    }

    /// <summary>도메인 재로드 전 자동 반영 콜백 해제</summary>
    private static void Stop()
    {
        EditorApplication.update -= Poll; // 이전 Editor 도메인의 파일 확인 종료
        AssemblyReloadEvents.beforeAssemblyReload -= Stop; // 재로드 알림 정리
    }

    /// <summary>안정된 원본만 복사하고 공식 importer의 Sprite 참조 갱신</summary>
    public static void Synchronize(bool force = false)
    {
        // 임포트가 다시 이 클래스를 호출해도 기존 동기화 한 번만 실행
        if (synchronizing) return;
        synchronizing = true;
        bool ready = true;
        try
        {
            foreach (Source source in sources)
            {
                try
                {
                    // 외부 원본을 덮어쓰지 않고 저장이 끝난 뒤 Unity 복사본만 교체
                    string original = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", source.Original));
                    var info = new FileInfo(original);
                    if (!info.Exists) throw new FileNotFoundException("Player 편집 원본 없음", original);
                    bool observed = info.LastWriteTimeUtc.Ticks != source.ObservedStamp || info.Length != source.ObservedLength;
                    if (observed)
                    {
                        source.ObservedStamp = info.LastWriteTimeUtc.Ticks;
                        source.ObservedLength = info.Length;
                        source.StableSince = EditorApplication.timeSinceStartup;
                    }
                    bool pending = source.ImportedStamp != source.ObservedStamp || source.ImportedLength != source.ObservedLength;
                    if (!pending) continue;
                    if (!force && EditorApplication.timeSinceStartup - source.StableSince < 0.35) { ready = false; continue; }
                    Import(source, original);
                    source.ImportedStamp = source.ObservedStamp;
                    source.ImportedLength = source.ObservedLength;
                    source.Error = null;
                    bindingsPending = true;
                }
                catch (Exception exception)
                {
                    // 불완전한 저장은 다음 갱신에서 재시도하고 이전 정상 Sprite 유지
                    ready = false;
                    string error = source.Original + ": " + exception.Message;
                    if (source.Error != error) Debug.LogError(error);
                    source.Error = error;
                    if (force) throw;
                }
            }

            // 전체 원본이 준비된 경계에서만 기존 게임 데이터와 프리팹 연결
            if (ready && bindingsPending)
            {
                try
                {
                    RebuildBindings();
                    bindingsPending = false;
                    bindingsError = null;
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                }
                catch (Exception exception)
                {
                    if (bindingsError != exception.Message) Debug.LogError(exception.Message);
                    bindingsError = exception.Message;
                    if (force) throw;
                }
            }
        }
        finally { synchronizing = false; }
    }

    /// <summary>Unity 안에서 원본이 재임포트된 경우에도 게임 프레임 참조 복구</summary>
    public static void RequestBindings()
    {
        bindingsPending = true;
    }

    /// <summary>Player 모션·독립 Dash 자세와 검 원본 등록</summary>
    private static Source[] CreateSources()
    {
        // 편집 원본의 실제 파일명을 보존
        string[] playerFiles = {
            "player0_Idle_bw", "player0_Idle_fw", "player0_Idle_low", "player0_idle_upper",
            "player0_move_bw", "player0_move_fw", "player0_move_low", "player0_move_upper",
            "player0_attack_bw", "player0_attack_fw", "player0_attack_low", "player0_attack_upper",
            "player0_dash_bw", "player0_dash_fw", "player0_dash_low", "player0_dash_upper",
            "player0_death", "player0_wakeup", "player0_baldo_OpenEyes", "player0_baldo_CloseEyes"
        };
        var result = new List<Source>();
        foreach (string file in playerFiles)
            result.Add(new Source { Original = "art/archive/player/" + file + ".aseprite", Asset = PlayerRoot + file + ".aseprite" });
        foreach (string file in new[] { "sword_0", "sword_1", "sword_2", "sword_3", "sword_with_hand" })
            result.Add(new Source { Original = "art/archive/weapon/" + file + ".aseprite", Asset = WeaponRoot + file + ".aseprite" });
        return result.ToArray();
    }

    /// <summary>완전한 Aseprite를 확인하고 첫 임포트에서 픽셀 배율과 기준점 설정</summary>
    private static void Import(Source source, string original)
    {
        // 저장 중 잘린 파일로 마지막 정상 복사본을 덮어쓰지 않음
        byte[] bytes = File.ReadAllBytes(original);
        if (bytes.Length < 128 || BitConverter.ToUInt16(bytes, 4) != 0xA5E0 || BitConverter.ToUInt32(bytes, 0) != bytes.Length)
            throw new IOException("Aseprite 저장 완료 대기");
        using (AsepriteFile file = AsepriteReader.ReadFile(original))
            if (file == null || file.noOfFrames == 0) throw new IOException("Aseprite 프레임 없음");
        bool firstImport = !File.Exists(source.Asset + ".meta");
        bool changed = !File.Exists(source.Asset) || !File.ReadAllBytes(source.Asset).SequenceEqual(bytes);
        if (changed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source.Asset));
            File.WriteAllBytes(source.Asset, bytes);
            AssetDatabase.ImportAsset(source.Asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        // 이후에는 사용자가 기본 Inspector에서 조절한 pivot·PPU를 보존
        if (!firstImport) return;
        var importer = AssetImporter.GetAtPath(source.Asset) as AsepriteImporter;
        if (importer == null) throw new IOException("공식 Aseprite importer 없음");
        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.includeHiddenLayers = false;
        importer.spritePixelsPerUnit = ArtSpriteImportDefaults.PixelsPerUnit;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotSpace = PivotSpaces.Canvas;
        importer.pivotAlignment = SpriteAlignment.Custom;
        importer.customPivotPosition = source.Asset.StartsWith(PlayerRoot, StringComparison.Ordinal) ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.25f);
        if (source.Asset == WeaponRoot + "sword_with_hand.aseprite") importer.customPivotPosition = new Vector2(7.5f / 14f, 19.5f / 52f);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.generatePhysicsShape = false;
        importer.generateAnimationClips = true;
        importer.generateModelPrefab = true;
        importer.SaveAndReimport();
    }

    /// <summary>공식 클립의 키에서 중복 그림도 생략하지 않고 원본 프레임 순서 복원</summary>
    private static Sprite[] ReadFrames(string path)
    {
        // Sprite 이름 정렬 대신 공식 importer가 만든 시간별 연결 사용
        using AsepriteFile file = AsepriteReader.ReadFile(path);
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var frames = new Sprite[file.noOfFrames];
        Sprite[] sprites = assets.OfType<Sprite>().ToArray();
        if (frames.Length == 1) { frames[0] = sprites.FirstOrDefault(); return frames; }
        AnimationClip clip = assets.OfType<AnimationClip>().First();
        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .First(value => value.type == typeof(SpriteRenderer) && value.propertyName == "m_Sprite");
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        double time = 0d;
        for (int i = 0; i < frames.Length; i++)
        {
            for (int key = keys.Length - 1; key >= 0; key--)
                if (keys[key].time <= time + 0.000001d) { frames[i] = keys[key].value as Sprite; break; }
            time += file.frameData[i].frameDuration / 1000d;
        }
        if (frames.Any(value => value == null)) throw new IOException(path + ": 표시할 Sprite 프레임 누락");
        return frames;
    }

    /// <summary>기존 모션 데이터의 GUID와 생명주기·Slash 상태를 보존하며 새 그림 연결</summary>
    private static void RebuildBindings()
    {
        RebuildSlashBindings();

        // enum의 기존 번호는 유지하고 네 원본과 반사로 여섯 시각 방향 구성
        var set = AssetDatabase.LoadAssetAtPath<PlayerSpriteAnimationSet>(AnimationSetPath);
        if (set == null) throw new IOException("PlayerSpriteAnimationSet 없음");
        var states = set.States.ToList();
        bool changed = false;
        string[] bands = { "bw", "upper", "low", "fw" };
        int[] idle = { 2, 17, 19, 0 };
        int[] move = { 6, 21, 23, 4 };
        int[] dash = { 10, 33, 35, 8 };
        for (int band = 0; band < bands.Length; band++)
        {
            string idleName = band == 1 ? "player0_idle_upper" : "player0_Idle_" + bands[band];
            Sprite[] idleFrames = ReadFrames(PlayerRoot + idleName + ".aseprite");
            Sprite[] moveFrames = ReadFrames(PlayerRoot + "player0_move_" + bands[band] + ".aseprite");
            Sprite[] attackFrames = ReadFrames(PlayerRoot + "player0_attack_" + bands[band] + ".aseprite");
            Sprite dashFrame = ReadFrames(PlayerRoot + "player0_dash_" + bands[band] + ".aseprite")[0];
            for (int side = 0; side < 2; side++)
            {
                bool flip = (band == 1 || band == 2) && side == 0;
                changed |= SetState(states, idle[band] + side, idleFrames, 12f, true, flip);
                changed |= SetState(states, move[band] + side, moveFrames, 4f, true, flip);
                changed |= SetState(states, 25 + band * 2 + side, attackFrames, 20f, false, flip);
                changed |= SetState(states, dash[band] + side, new[] { dashFrame }, 1f, false, flip);
                var dashState = states.First(value => (int)value.State == dash[band] + side);
                if (dashState.FlipX != flip) { dashState.FlipX = flip; changed = true; }
            }
        }
        changed |= BindLifecycle(states, PlayerSpriteAnimationState.Death, "player0_death");
        changed |= BindLifecycle(states, PlayerSpriteAnimationState.Spawn, "player0_wakeup");
        if (changed)
        {
            set.States = states.OrderBy(value => (int)value.State).ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            foreach (CharacterRendering active in UnityEngine.Object.FindObjectsByType<CharacterRendering>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                active.RefreshAnimationFrames();
        }

        // 프리팹 전체 구조와 사용자 애니메이션 키를 그대로 두고 초기 Sprite만 교체
        Sprite body = ReadFrames(PlayerRoot + "player0_Idle_bw.aseprite")[0];
        Sprite sword = ReadFrames(WeaponRoot + "sword_0.aseprite")[0];
        Sprite scabbard = ReadFrames(WeaponRoot + "sword_1.aseprite")[0];
        Sprite sheathed = ReadFrames(WeaponRoot + "sword_2.aseprite")[0];
        Sprite sheatheFlash = ReadFrames(WeaponRoot + "sword_3.aseprite")[0];
        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            CharacterRendering rendering = prefab.GetComponent<CharacterRendering>();
            bool prefabChanged = rendering.BodySprite.sprite != body || rendering.WeaponSprite.sprite != sword || rendering.ScabbardSprite.sprite != scabbard
                || rendering.SheathedSprite != sheathed || rendering.SheatheFlashSprite != sheatheFlash;
            if (!prefabChanged) return;
            rendering.BodySprite.sprite = body;
            rendering.BodySprite.flipX = false;
            rendering.WeaponSprite.sprite = sword;
            rendering.ScabbardSprite.sprite = scabbard;
            rendering.SheathedSprite = sheathed;
            rendering.SheatheFlashSprite = sheatheFlash;
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    /// <summary>생성·사망 그림과 각 프레임의 Aseprite 밀리초를 함께 갱신</summary>
    private static bool BindLifecycle(List<PlayerSpriteAnimationSet.StateData> states, PlayerSpriteAnimationState state, string name)
    {
        // 그림을 수정하지 않고 프레임 시간만 바꾼 저장도 반영
        string path = PlayerRoot + name + ".aseprite";
        bool changed = SetState(states, (int)state, ReadFrames(path), 10f, false, false);
        using AsepriteFile file = AsepriteReader.ReadFile(path);
        var durations = new float[file.noOfFrames];
        for (int i = 0; i < durations.Length; i++) durations[i] = file.frameData[i].frameDuration / 1000f;
        var data = states.First(value => value.State == state);
        if (data.FrameDurations != null && data.FrameDurations.SequenceEqual(durations)) return changed;
        data.FrameDurations = durations;
        return true;
    }

    /// <summary>발도 몸체 두 모션과 검집 손이 포함된 무기 모션을 전용 데이터에 연결</summary>
    private static void RebuildSlashBindings()
    {
        // 눈과 손은 서로 다른 속도로 샘플링하므로 각각 원본의 전체 프레임을 유지
        var set = AssetDatabase.LoadAssetAtPath<PlayerSpriteAnimationSet>("Assets/Art/Player/Slash/SlashSpriteAnimationSet.asset");
        if (set == null) throw new IOException("SlashSpriteAnimationSet 없음");
        string[] paths = { PlayerRoot + "player0_baldo_OpenEyes.aseprite", PlayerRoot + "player0_baldo_CloseEyes.aseprite",
            WeaponRoot + "sword_with_hand.aseprite" };
        bool changed = false;
        for (int i = 0; i < paths.Length; i++)
        {
            var state = set.GetState(PlayerSpriteAnimationState.SlashDraw + i);
            if (state == null) throw new IOException("SlashSpriteAnimationSet 상태 누락: " + paths[i]);
            Sprite[] frames = ReadFrames(paths[i]);
            if (state.Frames != null && state.Frames.SequenceEqual(frames)) continue;
            state.Frames = frames;
            changed = true;
        }

        // 재임포트 경계에서만 저장하고 실행 중 표시도 현재 자세에서 갱신
        if (!changed) return;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);
        foreach (CharacterRendering rendering in UnityEngine.Object.FindObjectsByType<CharacterRendering>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rendering.IsSlashAnimation) rendering.RefreshAnimationFrames();
    }

    /// <summary>첫 연결에서만 기준 속도를 설정하고 재저장에서는 사용자 재생 설정 보존</summary>
    private static bool SetState(List<PlayerSpriteAnimationSet.StateData> states, int index, Sprite[] frames, float fps, bool loop, bool flip)
    {
        // 원본 프레임 수가 바뀌어도 기존 상태 번호와 설정 재사용
        var state = states.FirstOrDefault(value => (int)value.State == index);
        bool fresh = state == null || state.Frames == null || state.Frames.Length == 0 ||
            state.Frames.All(value => !AssetDatabase.GetAssetPath(value).StartsWith(PlayerRoot, StringComparison.Ordinal));
        if (!fresh && state.Frames.SequenceEqual(frames)) return false;
        if (state == null) { state = new PlayerSpriteAnimationSet.StateData { State = (PlayerSpriteAnimationState)index }; states.Add(state); }
        state.Frames = frames;
        if (fresh)
        {
            state.FramesPerSecond = fps;
            state.FrameDurations = Array.Empty<float>();
            state.Loop = loop;
            state.FlipX = flip;
        }
        return true;
    }
}

/// <summary>공식 Aseprite 재임포트 완료를 게임의 Sprite 배열 갱신에 연결</summary>
public class PlayerArtSourcePostprocessor : AssetPostprocessor
{
    /// <summary>대상 Aseprite의 임포트가 끝나면 한 번의 참조 갱신 예약</summary>
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        // 다른 게임 자산의 임포트에는 개입하지 않음
        if (importedAssets.Any(path => path.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) &&
            (path.StartsWith(PlayerArtSourceSync.PlayerRoot, StringComparison.Ordinal) || path.StartsWith(PlayerArtSourceSync.WeaponRoot, StringComparison.Ordinal))))
            PlayerArtSourceSync.RequestBindings();
    }
}
