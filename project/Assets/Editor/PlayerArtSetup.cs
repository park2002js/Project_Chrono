using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>보관된 Player 최종 시트와 현재 검 원본의 출력을 기존 Player 프리팹에 연결</summary>
public static class PlayerArtSetup
{
    private const string PlayerRoot = "Assets/Art/Player/"; // 최종 원본과 픽셀이 같은 여덟 시트 경로
    private const string PrefabPath = "Assets/Prefabs/Startup/Player.prefab"; // 기존 GUID·기능 연결을 유지할 Player 프리팹
    private const string AnimationFolder = "Assets/Art/Player/Animations"; // Player Sprite 상태 세트를 저장할 에셋 폴더
    private const string AnimationSetPath = AnimationFolder + "/PlayerSpriteAnimationSet.asset"; // 몸체 재생의 단일 상태 데이터

    /// <summary>20px 셀·16PPU로 최종 Player 프레임을 준비하고 기존 컴포넌트에 연결</summary>
    [MenuItem("Tools/Chrono/Connect Player Art")]
    public static void Apply()
    {
        // 시트의 왼쪽부터 이름과 Sprite ID를 보존해 프레임 분리
        AssetDatabase.Refresh();
        Sprite[] idleFront = Slice(PlayerRoot + "idle-left-front.png", 20, 20, 3, new Vector2(0.5f, 0f));
        Sprite[] idleBack = Slice(PlayerRoot + "idle-left-back.png", 20, 20, 4, new Vector2(0.5f, 0f));
        Sprite[] moveFront = Slice(PlayerRoot + "move-left-front.png", 20, 20, 6, new Vector2(0.5f, 0f));
        Sprite[] moveBack = Slice(PlayerRoot + "move-left-back.png", 20, 20, 6, new Vector2(0.5f, 0f));
        Sprite dashFront = Slice(PlayerRoot + "dash-left-front.png", 20, 20, 1, new Vector2(0.5f, 0f))[0];
        Sprite dashBack = Slice(PlayerRoot + "dash-left-back.png", 20, 20, 1, new Vector2(0.5f, 0f))[0];
        Sprite[] death = Slice(PlayerRoot + "death-front.png", 20, 20, 6, new Vector2(0.5f, 0f));
        Sprite[] spawn = Slice(PlayerRoot + "spawn-front.png", 20, 20, 6, new Vector2(0.5f, 0f));
        Sprite[] swords = Slice("Assets/Art/Weapons/weapon.png", 7, 29, 3, new Vector2(0.5f, 25.5f / 29f));

        // 원작처럼 몸체 Sprite 상태와 무기 Transform 클립을 별도 에셋으로 준비
        PlayerSpriteAnimationSet animationSet = BuildAnimationSet(idleFront, idleBack, moveFront, moveBack, dashFront, dashBack, death, spawn);
        RuntimeAnimatorController weaponController = PlayerWeaponAnimationSetup.EnsureAssets();

        // 기존 Player 컴포넌트와 참조를 그대로 사용하고 최종 아트만 연결
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CharacterRendering rendering = root.GetComponent<CharacterRendering>();
            rendering.AnimationSet = animationSet;
            rendering.BodySprite.sprite = idleFront[0];
            rendering.BodySprite.flipX = true;
            rendering.BodySprite.color = Color.white;
            rendering.BodySprite.sortingOrder = 20;
            rendering.BodySprite.transform.localScale = Vector3.one;
            rendering.BodySprite.transform.localPosition = Vector3.zero;
            if (rendering.FacingMarker != null) rendering.FacingMarker.gameObject.SetActive(false);

            // 조준 피벗 아래의 무기 모션만 Animator가 쓰도록 계층 분리
            AttackController attack = root.GetComponent<AttackController>();
            attack.WeaponPivot.localPosition = new Vector3(0f, 0.5f, 0f);
            Transform motion = attack.WeaponPivot.Find("WeaponMotion");
            if (motion == null)
            {
                motion = new GameObject("WeaponMotion").transform;
                motion.SetParent(attack.WeaponPivot, false);
            }
            Transform visual = attack.WeaponPivot.Find("WeaponVisual");
            if (visual != null) visual.SetParent(motion, false);
            SpriteRenderer weapon = motion.GetComponentInChildren<SpriteRenderer>(true);
            if (weapon == null) throw new InvalidOperationException("Player weapon visual is missing.");
            motion.localPosition = Vector3.zero;
            motion.localRotation = Quaternion.identity;
            attack.WeaponMotion = motion;
            Animator weaponAnimator = attack.WeaponPivot.GetComponent<Animator>();
            if (weaponAnimator == null) weaponAnimator = attack.WeaponPivot.gameObject.AddComponent<Animator>();
            weaponAnimator.runtimeAnimatorController = weaponController;
            attack.WeaponAnimator = weaponAnimator;

            // 7×29 검의 손잡이를 모션 기준점에 놓고 Sprite 자체의 세로 방향 복원
            weapon.sprite = swords[0];
            weapon.color = Color.white;
            weapon.sortingOrder = 21;
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            weapon.transform.localScale = Vector3.one;
            rendering.WeaponSprite = weapon;

            // 보행 충돌은 발 아래만 검사하고 사망·기상은 실제 시트의 여섯 자세 표시
            BoxCollider2D feet = root.GetComponent<BoxCollider2D>();
            feet.size = new Vector2(0.5f, 0.35f);
            feet.offset = new Vector2(0f, 0.175f);
            PlayerSpawner spawner = root.GetComponent<PlayerSpawner>();
            spawner.DeathAnimationDuration = 1.42f;
            spawner.DeathDuration = 0.8f;
            spawner.SpawnDuration = 1.42f;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerArtSetup] Connected 33 Player frames, four dash directions and the current sheathed sword at 16 PPU.");
    }

    /// <summary>방향별 몸체 상태와 생명주기 시간을 재실행 가능한 단일 에셋에 연결</summary>
    private static PlayerSpriteAnimationSet BuildAnimationSet(Sprite[] idleFront, Sprite[] idleBack, Sprite[] moveFront, Sprite[] moveBack,
        Sprite dashFront, Sprite dashBack, Sprite[] death, Sprite[] spawn)
    {
        // 기존 에셋 식별자를 유지하고 첫 연결 때만 생성
        if (!AssetDatabase.IsValidFolder(AnimationFolder)) AssetDatabase.CreateFolder("Assets/Art/Player", "Animations");
        PlayerSpriteAnimationSet set = AssetDatabase.LoadAssetAtPath<PlayerSpriteAnimationSet>(AnimationSetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<PlayerSpriteAnimationSet>();
            AssetDatabase.CreateAsset(set, AnimationSetPath);
        }

        // 정지·이동은 상태 FPS, 사망·생성은 각 그림의 유지시간으로 재생
        set.States = new[]
        {
            State(PlayerSpriteAnimationState.IdleLeftFront, idleFront, 5f, true, false),
            State(PlayerSpriteAnimationState.IdleRightFront, idleFront, 5f, true, true),
            State(PlayerSpriteAnimationState.IdleLeftBack, idleBack, 5f, true, false),
            State(PlayerSpriteAnimationState.IdleRightBack, idleBack, 5f, true, true),
            State(PlayerSpriteAnimationState.MoveLeftFront, moveFront, 10f, true, false),
            State(PlayerSpriteAnimationState.MoveRightFront, moveFront, 10f, true, true),
            State(PlayerSpriteAnimationState.MoveLeftBack, moveBack, 10f, true, false),
            State(PlayerSpriteAnimationState.MoveRightBack, moveBack, 10f, true, true),
            State(PlayerSpriteAnimationState.DashLeftFront, new[] { dashFront }, 10f, false, false),
            State(PlayerSpriteAnimationState.DashRightFront, new[] { dashFront }, 10f, false, true),
            State(PlayerSpriteAnimationState.DashLeftBack, new[] { dashBack }, 10f, false, false),
            State(PlayerSpriteAnimationState.DashRightBack, new[] { dashBack }, 10f, false, true),
            State(PlayerSpriteAnimationState.Death, death, 50f, false, false, new[] { 0.22f, 0.22f, 0.22f, 0.07f, 0.07f, 0.62f }),
            State(PlayerSpriteAnimationState.Spawn, spawn, 50f, false, false, new[] { 0.22f, 0.18f, 0.14f, 0.14f, 0.14f, 0.60f })
        };
        EditorUtility.SetDirty(set);
        return set;
    }

    /// <summary>한 Sprite 상태의 프레임·속도·방향 반전을 구성</summary>
    private static PlayerSpriteAnimationSet.StateData State(PlayerSpriteAnimationState state, Sprite[] frames, float fps, bool loop,
        bool flipX, float[] frameDurations = null)
    {
        return new PlayerSpriteAnimationSet.StateData
        {
            State = state, Frames = frames, FramesPerSecond = fps, FrameDurations = frameDurations ?? Array.Empty<float>(), Loop = loop, FlipX = flipX
        };
    }

    /// <summary>Point·비압축·FullRect 설정으로 시트를 잘라 기존 Sprite 식별자를 유지</summary>
    private static Sprite[] Slice(string path, int width, int height, int count, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing final art: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        // 최신 Sprite Editor API로 이름별 GUID를 재사용해 프리팹 참조 보존
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        Dictionary<string, SpriteRect> old = provider.GetSpriteRects().ToDictionary(value => value.name);
        string prefix = System.IO.Path.GetFileNameWithoutExtension(path);
        SpriteRect[] rects = Enumerable.Range(0, count).Select(index =>
        {
            string name = prefix + "_" + index.ToString("00");
            return new SpriteRect { name = name, rect = new Rect(index * width, 0f, width, height),
                alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = old.ContainsKey(name) ? old[name].spriteID : GUID.Generate() };
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(value => new SpriteNameFileIdPair(value.name, value.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(value => value.name).ToArray();
        if (sprites.Length != count) throw new InvalidOperationException("Missing animation frames: " + path);
        return sprites;
    }
}
