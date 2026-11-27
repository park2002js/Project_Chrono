using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>최종 보스와 유도탄·원형 석대 맵의 임포트 설정·클립 에셋·프리팹 연결을 목록 파일대로 다시 만드는 편집기 도구</summary>
public static class LastBossAssetBuilder
{
    private const string ManifestPath = "Assets/Editor/LastBossAssetManifest.json"; // 텍스처 기준점·클립 구성·맵 벽을 적은 목록
    private const string RequestPath = "Temp/LastBossAssetBuilder.request"; // 이 파일이 생기면 메뉴 없이 한 번 실행
    private const string LogPath = "Temp/LastBossAssetBuilder.log"; // 마지막 실행 결과를 남길 위치
    private const string ArtRoot = "Assets/Art/Enemy/LastBoss/"; // 보스 그림과 클립을 둔 폴더
    private const string BossPath = "Assets/Prefabs/Startup/LastBoss.prefab"; // 새로 만들 최종 보스 프리팹
    private const string MapPath = "Assets/Prefabs/Startup/StonePlatformMap.prefab"; // 새로 만들 원형 석대 맵 프리팹
    private const string MapArtFolder = "Assets/Art/Environment/Wasteland"; // 맵 그림과 다리 애니메이션을 두는 폴더
    private const string MapAnimationFolder = MapArtFolder + "/Animations"; // 다리의 개폐 클립과 컨트롤러를 둘 폴더
    private const string BridgeSpritePath = "Sprite"; // 다리 객체 아래에서 프레임을 바꿔 그릴 자식의 이름
    private const string HomingPath = "Assets/Prefabs/Enemy/HomingBullet.prefab"; // 새로 만들 유도탄 프리팹
    private const string GuardPath = "Assets/Prefabs/Startup/GuardRobot.prefab"; // 공용 기절효과 설정을 읽어 올 경비 프리팹
    private const string GuardMapPath = "Assets/Prefabs/Startup/Map.prefab"; // 체크포인트 수정을 복사해 올 실험실 맵 프리팹
    private const string SystemsPath = "Assets/Prefabs/Startup/GameSystems.prefab"; // 스테이지 묶음을 등록할 StageManager 프리팹
    private const string WarningPath = "Assets/Prefabs/Enemy/Warnings/AttackWarning.prefab"; // 공용 공격 예고 프리팹
    private const string BossId = "last_boss"; // 저장 기록과 맵 배치를 연결할 최종 보스 식별자
    private const float BulletFootOffset = -0.5f; // 탄환 중심에서 앞뒤 정렬의 기준이 될 아래 끝까지의 거리
    private static readonly string[] SharedBulletPaths = { "Assets/Prefabs/Enemy/NormalBullet.prefab", "Assets/Prefabs/Enemy/GoldBullet.prefab",
        "Assets/Prefabs/Enemy/NormalFastBullet.prefab", "Assets/Prefabs/Enemy/GoldFastBullet.prefab" }; // 발끝 기준 정렬을 붙일 공용 탄환 프리팹

    /// <summary>목록 파일의 텍스처 한 장에 적용할 임포트 값</summary>
    [Serializable]
    private class TextureEntry
    {
        public string path; // Assets 기준 상대 경로
        public float pivotX; // 왼쪽 아래 기준의 가로 기준점 비율
        public float pivotY; // 왼쪽 아래 기준의 세로 기준점 비율
    }

    /// <summary>목록 파일의 클립 한 개를 이루는 프레임과 시간</summary>
    [Serializable]
    private class ClipEntry
    {
        public string name; // 코드에서 찾을 클립 이름
        public string path; // 클립 에셋의 Assets 기준 상대 경로
        public string[] frames; // 프레임 텍스처 경로이며 빈 문자열은 빈 프레임
        public float[] durations; // 프레임별 초 단위 시간
        public bool loop; // 반복 여부
        public int loopStart; // 반복 시작 프레임
    }

    /// <summary>맵 그림에 맞춰 정한 고정 벽 한 개</summary>
    [Serializable]
    private class WallEntry
    {
        public string name; // 막는 자리를 알리는 이름
        public string kind; // box 또는 polygon
        public float x; // 월드 가로 위치
        public float y; // 월드 세로 위치
        public float w; // 사각 벽의 가로 크기
        public float h; // 사각 벽의 세로 크기
        public float[] points; // 다각 벽의 꼭짓점을 x·y 순서로 나열한 값
    }

    /// <summary>효과음 한 개</summary>
    [Serializable]
    private class AudioEntry
    {
        public string name; // 코드가 찾을 이름
        public string path; // Assets 기준 음원 경로
    }

    /// <summary>복도와 석대를 잇는 다리의 프레임과 배치</summary>
    [Serializable]
    private class BridgeEntry
    {
        public string[] frames; // 내려간 모습부터 다 올라온 모습까지의 텍스처 경로
        public float[] position; // 프레임 위쪽 가운데의 월드 위치
        public float[] blocker; // 내려가 있는 동안 막을 영역의 월드 중심 x·y와 크기 w·h
        public float frameSeconds; // 프레임 한 장의 재생 시간
    }

    /// <summary>석판 위에 세울 빛의 문의 그림과 배치</summary>
    [Serializable]
    private class GateEntry
    {
        public string texture; // 문틀과 빛을 함께 그린 텍스처 경로
        public string spillTexture; // 문 앞 바닥에 번지는 빛의 텍스처 경로
        public float[] position; // 문턱 가운데의 월드 위치
        public float[] trigger; // 문턱 가운데 기준으로 문 속의 진입 감지 범위 x·y·폭·높이
    }

    /// <summary>목록 파일 전체</summary>
    [Serializable]
    private class Manifest
    {
        public int ppu; // 월드 1유닛에 놓을 원본 픽셀 수
        public TextureEntry[] textures; // 임포트 값을 맞출 텍스처
        public ClipEntry[] clips; // 만들거나 갱신할 클립
        public string mapTexture; // 맵 전경 텍스처 경로
        public string backdropTexture; // 맵 바깥을 덮을 단색 텍스처 경로
        public WallEntry[] walls; // 맵의 고정 벽
        public float[] cameraVerticalLimits; // 맵 그림의 아래쪽 끝과 위쪽 끝의 월드 세로 위치
        public float[] entranceSpawn; // 첫 입장 때 Player가 나타나는 위치
        public float[] entranceStop; // 첫 입장 걸음이 끝나는 위치
        public float[] checkpoint; // 새 게임을 시작할 때 Player가 서는 위치
        public float[] respawn; // 수정을 켠 뒤 재도전할 때 Player가 서는 위치
        public float[] crystal; // 체크포인트 수정의 위치
        public float[] bossSpawn; // 보스의 기본 위치
        public float[] encounter; // 조우 영역의 중심 x·y와 크기 w·h
        public BridgeEntry bridge; // 수정을 켜면 올라오고 전투 동안 내려가는 다리
        public BridgeEntry exitBridge; // 처치 뒤에 올라와 석대 위쪽의 석판으로 이어지는 다리
        public GateEntry lightGate; // 석판 위에 선 빛의 문
        public string battleMusic; // 전투 배경음 경로이며 비어 있으면 연결하지 않음
        public AudioEntry[] audio; // 보스와 유도탄에 지정할 효과음
    }

    /// <summary>에디터가 켜져 있는 동안 요청 파일을 지켜보도록 연결</summary>
    [InitializeOnLoadMethod]
    private static void WatchRequest()
    {
        EditorApplication.update += PollRequest; // 요청 파일이 생기면 컴파일·임포트가 끝난 뒤 한 번 실행
    }

    /// <summary>임포트 설정 → 클립 → 재질 → 유도탄 → 보스 → 맵 → 스테이지 묶음 순서로 전부 다시 구성</summary>
    [MenuItem("Project Chrono/Rebuild Last Boss Assets")]
    public static void Rebuild()
    {
        var log = new StringBuilder();
        try
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            ApplyTextureSettings(manifest, log);
            Dictionary<string, SpriteClip> clips = BuildClips(manifest, log);
            Material spriteMaterial = FindSpriteMaterial();
            Material goldMaterial = EnsureMaterial(ArtRoot + "SpriteGoldOutline.mat", "Chrono/SpriteGoldOutline");
            EnsureDepthSorter("Assets/Prefabs/Startup/Player.prefab", 0f);
            foreach (string path in SharedBulletPaths) EnsureDepthSorter(path, BulletFootOffset);
            Dictionary<string, AudioClip> sounds = LoadSounds(manifest);
            HomingProjectile homing = BuildHoming(clips, spriteMaterial, sounds);
            Boss_Last boss = BuildBoss(clips, spriteMaterial, goldMaterial, homing, LoadBattleMusic(manifest), sounds);
            MapManager map = BuildMap(manifest, spriteMaterial);
            RegisterStage(map, boss);
            AssetDatabase.SaveAssets();
            log.AppendLine("OK textures=" + manifest.textures.Length + " clips=" + clips.Count + " walls=" + manifest.walls.Length);
        }
        catch (Exception exception)
        {
            log.AppendLine("FAILED " + exception);
            Debug.LogException(exception);
        }
        File.WriteAllText(LogPath, log.ToString());
        Debug.Log("LastBossAssetBuilder: " + log);
    }

    /// <summary>요청 파일이 있고 에디터가 한가할 때 한 번 실행</summary>
    private static void PollRequest()
    {
        // 플레이 중이나 플레이를 드나드는 순간에는 만들지 않음: 그때 붙인 컴포넌트는 Awake가 실행되어 프리팹이 꺼진 채 저장됨
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying || Application.isPlaying) return;
        File.Delete(RequestPath);
        Rebuild();
    }

    /// <summary>목록의 텍스처를 16PPU·Point·단일 Sprite로 맞추고 기준점 적용</summary>
    private static void ApplyTextureSettings(Manifest manifest, StringBuilder log)
    {
        int changed = 0;
        AssetDatabase.Refresh();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (TextureEntry entry in manifest.textures)
            {
                var importer = AssetImporter.GetAtPath("Assets/" + entry.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Texture is not imported: " + entry.path);
                var pivot = new Vector2(entry.pivotX, entry.pivotY);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                // 이미 같은 값이면 다시 임포트하지 않음
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && Mathf.Approximately(importer.spritePixelsPerUnit, manifest.ppu) && importer.filterMode == FilterMode.Point
                    && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled
                    && settings.spriteAlignment == (int)SpriteAlignment.Custom && (settings.spritePivot - pivot).sqrMagnitude < 0.0000001f
                    && settings.spriteMeshType == SpriteMeshType.FullRect && importer.npotScale == TextureImporterNPOTScale.None
                    && importer.wrapMode == TextureWrapMode.Clamp)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = manifest.ppu;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
        log.AppendLine("texture settings changed=" + changed);
    }

    /// <summary>목록의 클립마다 SpriteClip 에셋을 만들거나 프레임과 시간을 갱신</summary>
    private static Dictionary<string, SpriteClip> BuildClips(Manifest manifest, StringBuilder log)
    {
        var result = new Dictionary<string, SpriteClip>();
        Directory.CreateDirectory(ArtRoot + "Clips");
        AssetDatabase.Refresh();
        foreach (ClipEntry entry in manifest.clips)
        {
            string path = "Assets/" + entry.path;
            var clip = AssetDatabase.LoadAssetAtPath<SpriteClip>(path);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteClip>();
                AssetDatabase.CreateAsset(clip, path);
            }
            var frames = new Sprite[entry.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                if (string.IsNullOrEmpty(entry.frames[i])) continue;
                frames[i] = LoadSprite(entry.frames[i]);
            }
            clip.Frames = frames;
            clip.Durations = entry.durations;
            clip.Loop = entry.loop;
            clip.LoopStart = entry.loopStart;
            EditorUtility.SetDirty(clip);
            result[entry.name] = clip;
        }
        log.AppendLine("clips=" + result.Count);
        return result;
    }

    /// <summary>기존 월드 Sprite가 쓰는 재질을 찾아 새 렌더러에도 같은 재질 사용</summary>
    private static Material FindSpriteMaterial()
    {
        foreach (string path in new[] { "Assets/Prefabs/Startup/Player.prefab", GuardPath })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    && renderer.sharedMaterial.shader.name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0
                    && !renderer.sharedMaterial.shader.name.StartsWith("Chrono/", StringComparison.Ordinal))
                    return renderer.sharedMaterial;
        }
        throw new InvalidOperationException("No default sprite material was found on the Player or Guard prefab.");
    }

    /// <summary>지정한 셰이더의 재질 에셋을 한 번 만들고 이후에는 그대로 사용</summary>
    private static Material EnsureMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Shader is missing: " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    /// <summary>금색 테두리 몸체·밝은 몸체·꼬리·폭발을 연결한 유도탄 프리팹 생성</summary>
    private static HomingProjectile BuildHoming(Dictionary<string, SpriteClip> clips, Material material, Dictionary<string, AudioClip> sounds)
    {
        var root = new GameObject("HomingBullet");
        try
        {
            // 몸체 판정과 Slash 전용 피격 영역
            var shape = root.AddComponent<CircleCollider2D>();
            shape.isTrigger = true;
            shape.radius = 0.45f;
            var projectile = root.AddComponent<HomingProjectile>();

            // 이미 조정한 속도·회전·접촉 수치는 그대로 두고 처음 만들 때만 기본값 사용
            var existing = AssetDatabase.LoadAssetAtPath<HomingProjectile>(HomingPath);
            if (existing != null) EditorUtility.CopySerialized(existing, projectile);
            else
            {
                projectile.CollisionRadius = 0.3f;
                projectile.Lifetime = 6f;
                projectile.Speed = 4.84f;
            }
            projectile.TrailClips = new[] { clips["homing-trail-a"], clips["homing-trail-b"] };
            projectile.ExplosionClip = clips["homing-explosion"];
            projectile.ExplosionSound = Sound(sounds, "homing-explosion");
            projectile.SpriteMaterial = material;
            var hitbox = root.AddComponent<Hitbox>();
            hitbox.TargetKind = TargetKind.Projectile;
            root.AddComponent<DepthSorter>().FootOffset = BulletFootOffset;

            // 반복 재생하는 몸체와 수명이 끝나갈 때 깜빡일 밝은 몸체
            SpriteClipPlayer body = AddPlayer(root.transform, "Body", 30, material, Vector2.zero, true);
            body.Clip = clips["homing-body"];
            body.PlayOnEnable = true;
            body.GetComponent<SpriteRenderer>().sprite = clips["homing-body"].Frames[0];
            SpriteClipPlayer bright = AddPlayer(root.transform, "Bright", 31, material, Vector2.zero, false);
            bright.Clip = clips["homing-bright"];
            bright.PlayOnEnable = true;
            projectile.Bright = bright.gameObject;
            return PrefabUtility.SaveAsPrefabAsset(root, HomingPath).GetComponent<HomingProjectile>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>목록의 효과음을 이름으로 찾을 수 있게 불러옴</summary>
    private static Dictionary<string, AudioClip> LoadSounds(Manifest manifest)
    {
        var result = new Dictionary<string, AudioClip>();
        if (manifest.audio == null) return result;
        foreach (AudioEntry entry in manifest.audio)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/" + entry.path);
            if (clip == null) throw new InvalidOperationException("Sound is missing: " + entry.path);
            result[entry.name] = clip;
        }
        return result;
    }

    /// <summary>이름으로 효과음을 찾고 목록에 없으면 null 반환</summary>
    private static AudioClip Sound(Dictionary<string, AudioClip> sounds, string name)
    {
        return sounds.TryGetValue(name, out AudioClip clip) ? clip : null;
    }

    /// <summary>전투 배경음을 흘려 재생하도록 임포트하고 불러오며 목록에 경로가 없으면 null 반환</summary>
    private static AudioClip LoadBattleMusic(Manifest manifest)
    {
        if (string.IsNullOrEmpty(manifest.battleMusic)) return null;
        string path = "Assets/" + manifest.battleMusic;

        // 긴 음원을 통째로 메모리에 올리지 않도록 설정
        if (AssetImporter.GetAtPath(path) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("Battle music is missing: " + manifest.battleMusic);
        return clip;
    }

    /// <summary>몸·그림자·손 효과·피격 영역과 패턴에 필요한 참조를 연결한 최종 보스 프리팹 생성</summary>
    private static Boss_Last BuildBoss(Dictionary<string, SpriteClip> clips, Material material, Material goldMaterial, HomingProjectile homing, AudioClip music,
        Dictionary<string, AudioClip> sounds)
    {
        // 이미 조정한 수치는 그대로 두도록 기존 프리팹의 본체 값을 먼저 복사
        var root = new GameObject("LastBoss");
        try
        {
            var boss = root.AddComponent<Boss_Last>();
            var existing = AssetDatabase.LoadAssetAtPath<Boss_Last>(BossPath);
            if (existing != null) EditorUtility.CopySerialized(existing, boss);
            else
            {
                boss.BossId = BossId;
                boss.BossName = "Last Boss";
                boss.maxHP = boss.HP = 3600f; // 공격력 100의 3단 콤보(360) 10회분
                boss.basicSP = boss.SP = 0f;
                boss.PatternGap = 1.5f;
            }
            if (music != null) boss.BattleMusic = music; // 목록에 음원이 있으면 전투 동안 반복 재생할 배경음으로 연결

            // 동작마다 낼 효과음이며 목록에 없는 소리는 비워 두어 재생하지 않음
            boss.VanishSound = Sound(sounds, "vanish");
            boss.AppearSound = Sound(sounds, "appear");
            boss.RiftOpenSound = Sound(sounds, "rift-open");
            boss.RiftCloseSound = Sound(sounds, "rift-close");
            boss.CounterSound = Sound(sounds, "counter");
            boss.FallSound = Sound(sounds, "fall");
            boss.LandSound = Sound(sounds, "land");
            boss.CrystalBurstSound = Sound(sounds, "crystal-burst");
            boss.AmethystCastSound = Sound(sounds, "amethyst-cast");
            boss.AmethystRiseSound = Sound(sounds, "amethyst-rise");
            boss.HitSound = Sound(sounds, "hit");
            boss.GroggySound = Sound(sounds, "groggy");

            // 바닥 그림자와 높이를 바꿀 몸 묶음
            SpriteRenderer shadow = AddSprite(root.transform, "Shadow", null, 14, material, Vector2.zero);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            DepthSorter.DrawOnGround(shadow.gameObject, 14);
            if (root.GetComponent<DepthSorter>() == null) root.AddComponent<DepthSorter>();
            Transform visual = CreateChild(root.transform, "Visual", Vector2.zero).transform;
            SpriteRenderer body = AddSprite(visual, "Body", clips["body-idle"].Frames[0], 20, material, Vector2.zero);
            SpriteClipPlayer handBack = AddPlayer(root.transform, "HandFxBack", 19, material, Vector2.zero, false);
            SpriteClipPlayer handFront = AddPlayer(root.transform, "HandFxFront", 21, material, Vector2.zero, false);

            // 지상에 있을 때만 열리는 몸 피격 영역
            GameObject hitObject = CreateChild(root.transform, "BodyHitbox", Vector2.zero);
            var hitShape = hitObject.AddComponent<BoxCollider2D>();
            hitShape.isTrigger = true;
            hitShape.offset = new Vector2(0.1f, 1.3f);
            hitShape.size = new Vector2(1.3f, 2.4f);
            var hitbox = hitObject.AddComponent<Hitbox>();
            hitbox.Owner = boss;
            hitbox.TargetKind = TargetKind.Boss;

            // 몸·효과·탄환·예고 참조
            boss.Body = body;
            boss.Visual = visual;
            boss.Shadow = shadow;
            boss.BodyHitbox = hitbox;
            boss.HandFxBack = handBack;
            boss.HandFxFront = handFront;
            var all = new List<SpriteClip>(clips.Values);
            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            boss.Clips = all.ToArray();
            boss.PillarSprite = LoadSprite("Art/Enemy/LastBoss/Objects/pillar/pillar.png");
            boss.PillarShadowSprite = LoadSprite("Art/Enemy/LastBoss/Objects/pillar/pillar-shadow.png");
            boss.CrystalShadowSprite = LoadSprite("Art/Enemy/LastBoss/Objects/crystal/crystal-shadow.png");
            boss.SpriteMaterial = material;
            boss.GoldMaterial = goldMaterial;
            boss.WarningPrefab = AssetDatabase.LoadAssetAtPath<AreaWarningVfx>(WarningPath);
            boss.NormalPrefab = AssetDatabase.LoadAssetAtPath<NormalProjectile>("Assets/Prefabs/Enemy/NormalBullet.prefab");
            boss.GoldFastPrefab = AssetDatabase.LoadAssetAtPath<GoldProjectile>("Assets/Prefabs/Enemy/GoldFastBullet.prefab");
            boss.HomingPrefab = homing;
            if (boss.WarningPrefab == null || boss.NormalPrefab == null || boss.GoldFastPrefab == null)
                throw new InvalidOperationException("Shared warning or bullet prefabs are missing.");

            // 공용 기절효과는 경비의 설정을 그대로 가져오고 흰빛 대상과 위치만 이 보스에 맞춤
            var groggy = root.AddComponent<BossGroggyFx>();
            var guard = AssetDatabase.LoadAssetAtPath<BossGroggyFx>(GuardPath);
            if (guard == null) throw new InvalidOperationException("The guard prefab has no groggy effect to copy.");
            EditorUtility.CopySerialized(guard, groggy);
            groggy.FlashTargets = new[] { body };
            groggy.CenterOffset = new Vector2(0f, 1.3f);
            groggy.StarOffset = new Vector2(0f, 3.1f);
            return PrefabUtility.SaveAsPrefabAsset(root, BossPath).GetComponent<Boss_Last>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>전경 그림·고정 벽·문·체크포인트·조우 영역을 가진 원형 석대 맵 프리팹 생성</summary>
    private static MapManager BuildMap(Manifest manifest, Material material)
    {
        var root = new GameObject("StonePlatformMap");
        GameObject guardMap = PrefabUtility.LoadPrefabContents(GuardMapPath);
        try
        {
            var map = root.AddComponent<MapManager>();

            // 맵 바깥을 덮는 단색 바탕과 한 장짜리 전경
            SpriteRenderer backdrop = AddSprite(root.transform, "Backdrop", LoadSprite(manifest.backdropTexture), -1, material, new Vector2(0f, -9f));
            backdrop.transform.localScale = new Vector3(600f, 600f, 1f);
            AddSprite(root.transform, "Background", LoadSprite(manifest.mapTexture), 0, material, Vector2.zero);

            // 석대 윗면·다리·복도 바닥 밖을 막는 고정 벽
            Transform collision = CreateChild(root.transform, "WorldCollision", Vector2.zero).transform;
            foreach (WallEntry wall in manifest.walls)
            {
                GameObject boundary = CreateChild(collision, wall.name, new Vector2(wall.x, wall.y));
                if (wall.kind == "box") boundary.AddComponent<BoxCollider2D>().size = new Vector2(wall.w, wall.h);
                else
                {
                    var points = new Vector2[wall.points.Length / 2];
                    for (int i = 0; i < points.Length; i++) points[i] = new Vector2(wall.points[i * 2], wall.points[i * 2 + 1]);
                    boundary.AddComponent<PolygonCollider2D>().points = points;
                }
            }

            // 온전한 복도 안의 출발·재생성 위치와 석대 가운데의 보스 위치
            Transform entranceSpawn = CreateChild(root.transform, "EntranceSpawnPoint", Point(manifest.entranceSpawn)).transform;
            Transform entranceStop = CreateChild(root.transform, "EntranceStopPoint", Point(manifest.entranceStop)).transform;
            Transform start = CreateChild(root.transform, "StartCheckpoint", Point(manifest.checkpoint)).transform;
            Transform mid = CreateChild(root.transform, "MidCheckpoint", Point(manifest.respawn)).transform;
            Transform bossSpawn = CreateChild(root.transform, "LastBossSpawn", Point(manifest.bossSpawn)).transform;

            // 석대 안쪽으로 들어와야 닫히는 문과 겹치지 않는 조우 영역
            GameObject encounter = CreateChild(root.transform, "LastBossEncounter", Point(manifest.encounter));
            var trigger = encounter.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(manifest.encounter[2], manifest.encounter[3]);
            encounter.AddComponent<EncounterTrigger>();

            // 수정을 켜면 올라와 길이 되고 전투가 시작되면 내려가 길을 끊는 입구 다리
            GameObject bridge = BuildBridge(root.transform, manifest.bridge, material, "Bridge", "bridge");

            // 처치 뒤에 올라와 석대 위쪽의 석판으로 이어지는 출구 다리
            GameObject exitBridge = BuildBridge(root.transform, manifest.exitBridge, material, "ExitBridge", "exit-bridge");

            // 석판 위에 선 빛의 문이며 객체 위치가 문턱의 가운데, 빛 번짐은 다리(1)보다 위·문은 캐릭터(23)보다 아래
            GameObject gate = CreateChild(root.transform, "LightGate", Point(manifest.lightGate.position));
            AddSprite(gate.transform, "Spill", LoadSprite(manifest.lightGate.spillTexture), 2, material, Vector2.zero);
            AddSprite(gate.transform, "Gate", LoadSprite(manifest.lightGate.texture), 12, material, Vector2.zero);

            // 문 속의 진입 감지이며 처치 뒤 Player가 들어오면 데모 엔딩으로 넘어감
            var gateArea = gate.AddComponent<BoxCollider2D>();
            gateArea.isTrigger = true;
            gateArea.offset = new Vector2(manifest.lightGate.trigger[0], manifest.lightGate.trigger[1]);
            gateArea.size = new Vector2(manifest.lightGate.trigger[2], manifest.lightGate.trigger[3]);
            map.Exit = gate.AddComponent<StageExit>();

            // 실험실 맵의 체크포인트 수정을 그대로 복사해 이 보스의 입구와 연결
            CrystalCheckPoint source = guardMap.GetComponentInChildren<CrystalCheckPoint>(true);
            if (source == null) throw new InvalidOperationException("The laboratory map has no checkpoint crystal to copy.");
            GameObject crystal = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
            crystal.name = "CheckpointCrystal";
            crystal.transform.localPosition = Point(manifest.crystal);
            var checkpoint = crystal.GetComponent<CrystalCheckPoint>();
            checkpoint.CheckpointId = "mid";
            checkpoint.BossId = BossId;

            // 수정은 밑동 높이로 앞뒤를 가리고 상호작용 안내 그림은 항상 위에 그림
            if (crystal.GetComponent<DepthSorter>() == null) crystal.AddComponent<DepthSorter>();
            if (checkpoint.InteractionPrompt != null) DepthSorter.DrawOnGround(checkpoint.InteractionPrompt.gameObject, 60);


            // 배치 정보와 카메라 세로 범위
            map.Checkpoints = new[]
            {
                new MapManager.CheckpointSpawn { Id = "start", Position = start },
                new MapManager.CheckpointSpawn { Id = "mid", Position = mid },
            };
            map.BossPositions = new[]
            {
                new MapManager.BossSpawn { BossId = BossId, Position = bossSpawn, EncounterArea = trigger, EntryDoor = bridge, ExitDoor = exitBridge },
            };
            map.EntranceSpawnPoint = entranceSpawn;
            map.EntranceStopPoint = entranceStop;
            map.CameraVerticalLimits = Point(manifest.cameraVerticalLimits);
            if (!root.activeSelf) throw new InvalidOperationException("The map root was deactivated while building; rebuild outside play mode.");
            return PrefabUtility.SaveAsPrefabAsset(root, MapPath).GetComponent<MapManager>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(guardMap);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>목록 파일의 x·y 두 값을 월드 위치로 변환</summary>
    private static Vector2 Point(float[] values) => new Vector2(values[0], values[1]);

    /// <summary>프레임 그림·개폐 Animator·내려가 있는 동안의 벽을 가진 다리를 문 객체로 생성</summary>
    private static GameObject BuildBridge(Transform parent, BridgeEntry entry, Material material, string objectName, string clipPrefix)
    {
        // 바닥 그림 바로 위, 그림자와 캐릭터 아래에 그림
        Vector2 position = Point(entry.position);
        GameObject bridge = CreateChild(parent, objectName, position);
        AddSprite(bridge.transform, BridgeSpritePath, LoadSprite(entry.frames[0]), 1, material, Vector2.zero);
        bridge.AddComponent<Animator>().runtimeAnimatorController = BuildBridgeAnimator(entry, clipPrefix);

        // 다 올라오기 전까지 다리가 놓일 자리를 통째로 막는 벽
        var blocker = bridge.AddComponent<BoxCollider2D>();
        blocker.offset = new Vector2(entry.blocker[0], entry.blocker[1]) - position;
        blocker.size = new Vector2(entry.blocker[2], entry.blocker[3]);
        bridge.AddComponent<MapDoor>();
        return bridge;
    }

    /// <summary>내려간 채 정지·올라옴·올라온 채 정지·내려감 네 상태의 클립과 Animator 컨트롤러를 만들거나 갱신</summary>
    private static AnimatorController BuildBridgeAnimator(BridgeEntry entry, string clipPrefix)
    {
        // 올라오는 순서의 프레임과 그 역순
        if (!AssetDatabase.IsValidFolder(MapAnimationFolder)) AssetDatabase.CreateFolder(MapArtFolder, "Animations");
        var rising = new Sprite[entry.frames.Length];
        for (int i = 0; i < rising.Length; i++) rising[i] = LoadSprite(entry.frames[i]);
        var sinking = (Sprite[])rising.Clone();
        Array.Reverse(sinking);
        AnimationClip closed = WriteSpriteAnimation(clipPrefix + "-closed", new[] { rising[0] }, entry.frameSeconds);
        AnimationClip opening = WriteSpriteAnimation(clipPrefix + "-opening", rising, entry.frameSeconds);
        AnimationClip open = WriteSpriteAnimation(clipPrefix + "-open", new[] { rising[rising.Length - 1] }, entry.frameSeconds);
        AnimationClip closing = WriteSpriteAnimation(clipPrefix + "-closing", sinking, entry.frameSeconds);

        // MapDoor가 이름으로 재생하는 네 상태이며 움직임이 끝나면 정지 상태로 넘어감
        string path = MapAnimationFolder + "/" + clipPrefix + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (string trigger in new[] { "Open", "Close" })
            if (!Array.Exists(controller.parameters, parameter => parameter.name == trigger)) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        AnimatorState closedState = machine.AddState("closed");
        AnimatorState openingState = machine.AddState("opening");
        AnimatorState openState = machine.AddState("open");
        AnimatorState closingState = machine.AddState("closing");
        closedState.motion = closed;
        openingState.motion = opening;
        openState.motion = open;
        closingState.motion = closing;
        machine.defaultState = closedState;
        foreach ((AnimatorState from, AnimatorState to) in new[] { (openingState, openState), (closingState, closedState) })
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    /// <summary>다리 그림의 Sprite를 프레임 순서대로 바꾸는 반복 없는 클립을 만들거나 갱신</summary>
    private static AnimationClip WriteSpriteAnimation(string name, Sprite[] frames, float frameSeconds)
    {
        string path = MapAnimationFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        // 끝 장도 한 장 시간만큼 보이도록 같은 그림의 키를 하나 더 둠
        clip.ClearCurves();
        clip.frameRate = 1f / frameSeconds;
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * frameSeconds, value = frames[Mathf.Min(i, frames.Length - 1)] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(BridgeSpritePath, typeof(SpriteRenderer), "m_Sprite"), keys);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>프리팹 최상위에 발끝 기준 공통 정렬이 없으면 붙이고 발끝 거리를 지정</summary>
    private static void EnsureDepthSorter(string path, float footOffset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.TryGetComponent(out DepthSorter sorter)) sorter = root.AddComponent<DepthSorter>();
            else if (Mathf.Approximately(sorter.FootOffset, footOffset)) return;
            sorter.FootOffset = footOffset;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>StageManager에 경비와 최종 보스의 묶음을 등록하고 처음 한 번만 최종 보스를 기본으로 선택</summary>
    private static void RegisterStage(MapManager map, Boss_Last boss)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SystemsPath);
        try
        {
            var stage = root.GetComponentInChildren<StageManager>(true);
            if (stage == null) throw new InvalidOperationException("GameSystems prefab has no StageManager.");
            bool first = stage.Stages == null || stage.Stages.Length < 2;
            stage.Stages = new[]
            {
                new StageManager.StageSet { Name = "Guard Robot + Laboratory", MapPrefab = stage.MapPrefab, BossPrefab = stage.BossPrefab },
                new StageManager.StageSet { Name = "Last Boss + Stone Platform", MapPrefab = map, BossPrefab = boss },
            };
            if (first) stage.StageIndex = 1;
            PrefabUtility.SaveAsPrefabAsset(root, SystemsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Assets 기준 경로의 Sprite를 불러오고 없으면 실패 처리</summary>
    private static Sprite LoadSprite(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + relativePath);
        if (sprite == null) throw new InvalidOperationException("Sprite is missing: " + relativePath);
        return sprite;
    }

    /// <summary>부모와 같은 레이어의 빈 자식 생성</summary>
    private static GameObject CreateChild(Transform parent, string name, Vector2 localPosition)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.layer = parent.gameObject.layer;
        return child;
    }

    /// <summary>Sprite 렌더러 하나를 가진 자식 생성</summary>
    private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, int order, Material material, Vector2 localPosition)
    {
        var renderer = CreateChild(parent, name, localPosition).AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>클립 재생기를 가진 자식을 만들고 필요하면 꺼 둠</summary>
    private static SpriteClipPlayer AddPlayer(Transform parent, string name, int order, Material material, Vector2 localPosition, bool active)
    {
        SpriteRenderer renderer = AddSprite(parent, name, null, order, material, localPosition);
        var player = renderer.gameObject.AddComponent<SpriteClipPlayer>();
        renderer.gameObject.SetActive(active);
        return player;
    }
}
