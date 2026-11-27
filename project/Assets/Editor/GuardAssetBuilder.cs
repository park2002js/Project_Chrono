using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>가드와 공용 전투 효과의 임포트 설정·클립 에셋·프리팹 연결을 목록 파일대로 다시 만드는 편집기 도구</summary>
public static class GuardAssetBuilder
{
    private const string ManifestPath = "Assets/Editor/GuardAssetManifest.json"; // 텍스처 기준점과 클립 구성을 적은 목록
    private const string RequestPath = "Temp/GuardAssetBuilder.request"; // 이 파일이 생기면 메뉴 없이 한 번 실행
    private const string LogPath = "Temp/GuardAssetBuilder.log"; // 마지막 실행 결과를 남길 위치
    private const string GuardPath = "Assets/Prefabs/Startup/GuardRobot.prefab"; // 자식 구성을 다시 만들 경비 프리팹
    private const string WarningPath = "Assets/Prefabs/Enemy/Warnings/AttackWarning.prefab"; // 공용 공격 예고 프리팹
    private const string NormalRedPath = "Assets/Prefabs/Enemy/NormalBullet.prefab"; // 빨강 일반탄
    private const string NormalGoldPath = "Assets/Prefabs/Enemy/GoldBullet.prefab"; // 황금 일반탄
    private const string FastRedPath = "Assets/Prefabs/Enemy/NormalFastBullet.prefab"; // 빨강 특수탄
    private const string FastGoldPath = "Assets/Prefabs/Enemy/GoldFastBullet.prefab"; // 황금 특수탄
    private const float FastBulletRadius = 0.39f; // 특수탄을 처음 만들 때 쓸 몸체 판정 반지름이며 일반탄 값을 따라가지 않음
    private const string RendererPath = "Assets/Settings/Renderer2D.asset"; // 착지 왜곡 패스를 추가할 렌더러
    private const string MapPath = "Assets/Prefabs/Startup/Map.prefab"; // 경비 조우 영역에 전투 구역을 붙일 맵 프리팹
    private const string SystemsPath = "Assets/Prefabs/Startup/GameSystems.prefab"; // 전투 배경음 재생기를 붙일 공용 객체 프리팹
    private const float Pixel = 1f / 16f; // 원본 1px의 월드 길이
    private const int ShadowOrder = 14; // 그림자를 실험실 바닥(최대 10)과 예고(12~13) 위, 캐릭터 아래에 놓을 정렬 순서
    private const float BodyFootOffset = 0.875f; // 몸의 앞뒤 기준선을 기준점에서 총구 높이까지 올린 거리이며 총구에서 나온 탄환이 몸 앞에 그려지게 함
    private const float ChargeDepthGap = 0.02f; // 주먹 충전 효과를 손의 바로 뒤·바로 앞에 두려고 손의 기준선에서 어긋나게 할 거리

    /// <summary>목록 파일의 텍스처 한 장에 적용할 임포트 값</summary>
    [Serializable]
    private class TextureEntry
    {
        public string path; // Assets 기준 상대 경로
        public float pivotX; // 왼쪽 아래 기준의 가로 기준점 비율
        public float pivotY; // 왼쪽 아래 기준의 세로 기준점 비율
        public int borderLeft; // 9분할 왼쪽 테두리
        public int borderBottom; // 9분할 아래쪽 테두리
        public int borderRight; // 9분할 오른쪽 테두리
        public int borderTop; // 9분할 위쪽 테두리
    }

    /// <summary>목록 파일의 클립 한 개를 이루는 프레임과 시간</summary>
    [Serializable]
    private class ClipEntry
    {
        public string path; // 클립 에셋의 Assets 기준 상대 경로
        public string[] frames; // 프레임 텍스처 경로이며 빈 문자열은 빈 프레임
        public float[] durations; // 프레임별 초 단위 시간
        public bool loop; // 반복 여부
        public int loopStart; // 반복 시작 프레임
    }

    /// <summary>목록 파일의 음원 한 개</summary>
    [Serializable]
    private class AudioEntry
    {
        public string name; // 파일 이름
        public string path; // Assets 기준 상대 경로
        public bool streaming; // 긴 음원을 메모리에 풀지 않고 흘려 재생하도록 임포트할지 여부
    }

    /// <summary>목록 파일 전체</summary>
    [Serializable]
    private class Manifest
    {
        public int ppu; // 월드 1유닛에 놓을 원본 픽셀 수
        public TextureEntry[] textures; // 임포트 값을 맞출 텍스처
        public ClipEntry[] clips; // 만들거나 갱신할 클립
        public AudioEntry[] audio; // 연결할 음원
    }

    /// <summary>에디터가 켜져 있는 동안 요청 파일을 지켜보도록 연결</summary>
    [InitializeOnLoadMethod]
    private static void WatchRequest()
    {
        EditorApplication.update += PollRequest; // 요청 파일이 생기면 컴파일·임포트가 끝난 뒤 한 번 실행
    }

    /// <summary>임포트 설정 → 클립 → 재질 → 프리팹 → 렌더러 순서로 전부 다시 구성</summary>
    [MenuItem("Project Chrono/Rebuild Guard Assets")]
    public static void Rebuild()
    {
        var log = new StringBuilder();
        try
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            ApplyTextureSettings(manifest, log);
            Dictionary<string, SpriteClip> clips = BuildClips(manifest, log);
            Dictionary<string, AudioClip> audio = LoadAudio(manifest);
            Material spriteMaterial = FindSpriteMaterial();
            Material flashMaterial = EnsureMaterial("Assets/Art/FX/Groggy/SpriteFlashSpread.mat", "Chrono/SpriteFlashSpread");
            Material distortionMaterial = EnsureMaterial("Assets/Art/Enemy/Guard/ImpactDistortion.mat", "Chrono/ImpactDistortion");
            AreaWarningVfx warning = BuildWarning(spriteMaterial);
            UpdateNormalBullet(NormalRedPath, clips["normal-red"]);
            UpdateNormalBullet(NormalGoldPath, clips["normal-gold"]);
            BuildFastBullet(NormalRedPath, FastRedPath, clips["special-red"]);
            BuildFastBullet(NormalGoldPath, FastGoldPath, clips["special-gold"]);
            BuildGuard(clips, audio, spriteMaterial, flashMaterial, warning);
            EnsureArena(spriteMaterial);
            EnsureBattleMusic();
            EnsureRendererFeature(distortionMaterial);
            AssetDatabase.SaveAssets();
            log.AppendLine("OK textures=" + manifest.textures.Length + " clips=" + clips.Count);
        }
        catch (Exception exception)
        {
            log.AppendLine("FAILED " + exception);
            Debug.LogException(exception);
        }
        File.WriteAllText(LogPath, log.ToString());
        Debug.Log("GuardAssetBuilder: " + log);
    }

    /// <summary>요청 파일이 있고 에디터가 한가할 때 한 번 실행</summary>
    private static void PollRequest()
    {
        // 플레이를 빠져나오는 순간에도 프리팹을 건드리지 않도록 플레이 상태를 모두 확인
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying || Application.isPlaying) return;
        File.Delete(RequestPath);
        Rebuild();
    }

    /// <summary>목록의 텍스처를 16PPU·Point·단일 Sprite로 맞추고 기준점과 9분할 테두리 적용</summary>
    private static void ApplyTextureSettings(Manifest manifest, StringBuilder log)
    {
        int changed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (TextureEntry entry in manifest.textures)
            {
                var importer = AssetImporter.GetAtPath("Assets/" + entry.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Texture is not imported: " + entry.path);
                var pivot = new Vector2(entry.pivotX, entry.pivotY);
                var border = new Vector4(entry.borderLeft, entry.borderBottom, entry.borderRight, entry.borderTop);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                // 이미 같은 값이면 다시 임포트하지 않음
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && Mathf.Approximately(importer.spritePixelsPerUnit, manifest.ppu) && importer.filterMode == FilterMode.Point
                    && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled
                    && settings.spriteAlignment == (int)SpriteAlignment.Custom && (settings.spritePivot - pivot).sqrMagnitude < 0.0000001f
                    && settings.spriteBorder == border && settings.spriteMeshType == SpriteMeshType.FullRect
                    && importer.npotScale == TextureImporterNPOTScale.None && importer.wrapMode == TextureWrapMode.Clamp)
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
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteBorder = border;
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
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + entry.frames[i]);
                if (frames[i] == null) throw new InvalidOperationException("Sprite frame is missing: " + entry.frames[i]);
            }
            clip.Frames = frames;
            clip.Durations = entry.durations;
            clip.Loop = entry.loop;
            clip.LoopStart = entry.loopStart;
            EditorUtility.SetDirty(clip);
            result[Path.GetFileNameWithoutExtension(path)] = clip;
        }
        log.AppendLine("clips=" + result.Count);
        return result;
    }

    /// <summary>목록의 음원을 파일 이름으로 찾을 수 있게 불러오고 긴 음원은 흘려 재생하도록 임포트</summary>
    private static Dictionary<string, AudioClip> LoadAudio(Manifest manifest)
    {
        var result = new Dictionary<string, AudioClip>();
        foreach (AudioEntry entry in manifest.audio)
        {
            if (entry.streaming && AssetImporter.GetAtPath("Assets/" + entry.path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.Streaming)
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/" + entry.path);
            if (clip == null) throw new InvalidOperationException("Audio clip is missing: " + entry.path);
            result[entry.name] = clip;
        }
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

    /// <summary>마스크 Sprite와 재질을 연결한 공용 공격 예고 프리팹 생성</summary>
    private static AreaWarningVfx BuildWarning(Material spriteMaterial)
    {
        var root = new GameObject("AttackWarning");
        try
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var warning = root.AddComponent<AreaWarningVfx>();
            var existing = AssetDatabase.LoadAssetAtPath<AreaWarningVfx>(WarningPath);
            if (existing != null) EditorUtility.CopySerialized(existing, warning);
            warning.RectangleSprite = LoadSprite("Art/Enemy/Warnings/warning-rect-9slice.png");
            warning.OpenLineSprite = LoadSprite("Art/Enemy/Warnings/warning-open-line.png");
            warning.SpriteMaterial = spriteMaterial;
            return PrefabUtility.SaveAsPrefabAsset(root, WarningPath).GetComponent<AreaWarningVfx>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>기존 일반탄 프리팹의 그림을 새 4장 클립으로 바꾸고 이전 광원 자식 제거</summary>
    private static void UpdateNormalBullet(string path, SpriteClip clip)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform halo = root.transform.Find("Halo");
            if (halo != null) UnityEngine.Object.DestroyImmediate(halo.gameObject);
            var pulse = root.GetComponent<ProjectileVfx>();
            if (pulse != null) UnityEngine.Object.DestroyImmediate(pulse);
            var renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) throw new InvalidOperationException("Bullet prefab has no body renderer: " + path);
            renderer.sprite = clip.Frames[0];
            var player = root.GetComponent<SpriteClipPlayer>();
            if (player == null) player = root.AddComponent<SpriteClipPlayer>();
            player.Clip = clip;
            player.PlayOnEnable = true;
            var projectile = root.GetComponent<Projectile>();
            projectile.Visual = null;
            projectile.FaceDirection = false;
            projectile.SlashTrail = null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>같은 색 일반탄을 바탕으로 진행 방향을 보는 그림과 Slash 전용 후방 판정을 가진 특수탄 프리팹 생성</summary>
    private static void BuildFastBullet(string sourcePath, string path, SpriteClip clip)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

        // 이미 조정한 특수탄 몸체 판정은 그대로 두고 처음 만들 때만 기본값 사용
        float bodyRadius = FastBulletRadius;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && existing.TryGetComponent(out CircleCollider2D existingBody)) bodyRadius = existingBody.radius;
        GameObject root = UnityEngine.Object.Instantiate(source);
        try
        {
            if (root.TryGetComponent(out CircleCollider2D body)) body.radius = bodyRadius;
            // 몸체 렌더러를 회전시킬 자식으로 옮김
            root.name = Path.GetFileNameWithoutExtension(path);
            var rootRenderer = root.GetComponent<SpriteRenderer>();
            Material material = rootRenderer.sharedMaterial;
            int order = rootRenderer.sortingOrder;
            UnityEngine.Object.DestroyImmediate(root.GetComponent<SpriteClipPlayer>());
            UnityEngine.Object.DestroyImmediate(rootRenderer);
            GameObject visual = CreateChild(root.transform, "Visual", Vector2.zero);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.sprite = clip.Frames[0];
            var player = visual.AddComponent<SpriteClipPlayer>();
            player.Clip = clip;
            player.PlayOnEnable = true;

            // 이동 거리만큼 뒤로 늘어날 Slash 전용 판정
            GameObject trail = CreateChild(visual.transform, "SlashTrail", Vector2.zero);
            var box = trail.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.125f, 1.125f);
            var trailHitbox = trail.AddComponent<Hitbox>();
            trailHitbox.TargetKind = TargetKind.Projectile;
            var projectile = root.GetComponent<Projectile>();
            projectile.Visual = visual.transform;
            projectile.FaceDirection = true;
            projectile.SlashTrail = box;
            projectile.TrailWidth = 1.125f;
            projectile.Speed = 50f;
            projectile.Lifetime = 0f;
            projectile.ReflectionSpeed = 50f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>경비 프리팹의 자식을 새 부위·효과 구성으로 바꾸고 각 실행체의 참조 연결</summary>
    private static void BuildGuard(Dictionary<string, SpriteClip> clips, Dictionary<string, AudioClip> audio, Material material, Material flashMaterial,
        AreaWarningVfx warning)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GuardPath);
        try
        {
            // 이전 피격 자식의 레이어를 새 수정 판정에 물려주고 자식을 모두 비움
            Transform oldHitbox = root.transform.Find("CrystalHitbox");
            if (oldHitbox == null) oldHitbox = root.transform.Find("BodyHitbox");
            int hitLayer = oldHitbox != null ? oldHitbox.gameObject.layer : root.layer;
            for (int i = root.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            // 128×51 캔버스의 아래 가운데를 기준점으로 겹쳐 놓는 몸 부위이며 한 덩어리로 다른 캐릭터·탄환과 앞뒤를 가림
            Transform visual = CreateChild(root.transform, "Visual", Vector2.zero).transform;
            AddDepthSorter(visual.gameObject, BodyFootOffset, null);
            SpriteRenderer torso = AddSprite(visual, "Torso", LoadPart("torso"), 20, material, Vector2.zero);
            SpriteRenderer head = AddSprite(visual, "Head", LoadPart("head"), 21, material, Vector2.zero);
            SpriteRenderer eyeLeft = AddSprite(visual, "EyeLeft", LoadPart("eye-left"), 22, material, Vector2.zero);
            SpriteRenderer eyeRight = AddSprite(visual, "EyeRight", LoadPart("eye-right"), 22, material, Vector2.zero);
            SpriteRenderer muzzleLeft = AddSprite(visual, "MuzzleLeft", LoadPart("muzzle-left"), 22, material, Vector2.zero);
            SpriteRenderer muzzleRight = AddSprite(visual, "MuzzleRight", LoadPart("muzzle-right"), 22, material, Vector2.zero);
            SpriteRenderer crystal = AddSprite(visual, "Crystal", LoadPart("crystal"), 22, material, Vector2.zero);
            SpriteClipPlayer crystalFx = AddPlayer(visual, "CrystalFx", 23, material, Vector2.zero, true);
            crystalFx.Clip = clips["crystal-active"];
            crystalFx.PlayOnEnable = true;
            crystalFx.GetComponent<SpriteRenderer>().sprite = clips["crystal-active"].Frames[0];
            SpriteClipPlayer eyeFx = AddPlayer(visual, "EyeFx", 23, material, Vector2.zero, false);
            SpriteClipPlayer flashLeft = AddPlayer(visual, "MuzzleFlashLeft", 23, material, Vector2.zero, false);
            SpriteClipPlayer flashRight = AddPlayer(visual, "MuzzleFlashRight", 23, material, Vector2.zero, false);

            // 손과 합친 주먹은 주먹 바닥 가운데를 기준점으로 독립 이동
            var shadowColor = new Color(0f, 0f, 0f, 0.35f);
            SpriteRenderer bodyShadow = AddSprite(root.transform, "BodyShadow", null, ShadowOrder, material, new Vector2(-0.03125f, 0.1875f));
            SpriteRenderer shadowLeft = AddSprite(root.transform, "HandShadowLeft", null, ShadowOrder, material, new Vector2(-2.65625f, 0.0625f));
            SpriteRenderer shadowRight = AddSprite(root.transform, "HandShadowRight", null, ShadowOrder, material, new Vector2(2.59375f, 0.0625f));
            bodyShadow.color = shadowLeft.color = shadowRight.color = shadowColor;
            SpriteRenderer handLeft = AddSprite(root.transform, "HandLeft", LoadPart("fist-left"), 24, material, new Vector2(-2.65625f, 0.0625f));
            SpriteRenderer handRight = AddSprite(root.transform, "HandRight", LoadPart("fist-right"), 24, material, new Vector2(2.59375f, 0.0625f));
            SpriteRenderer combined = AddSprite(root.transform, "CombinedFist", LoadPart("fist-combined"), 24, material, Vector2.zero);
            combined.enabled = false;
            SpriteClipPlayer chargeBack = AddPlayer(root.transform, "ChargeBack", 23, material, Vector2.zero, false);
            SpriteClipPlayer chargeFront = AddPlayer(root.transform, "ChargeFront", 26, material, Vector2.zero, false);

            // 손과 합친 주먹은 그림자가 놓인 바닥 지점으로 앞뒤를 가리고, 충전 효과는 같은 지점에서 손의 바로 뒤와 바로 앞에 그림
            AddDepthSorter(handLeft.gameObject, 0f, shadowLeft.transform);
            AddDepthSorter(handRight.gameObject, 0f, shadowRight.transform);
            AddDepthSorter(combined.gameObject, 0f, shadowLeft.transform);
            AddDepthSorter(chargeBack.gameObject, ChargeDepthGap, shadowLeft.transform);
            AddDepthSorter(chargeFront.gameObject, -ChargeDepthGap, shadowLeft.transform);

            // 총구·눈의 중심 위치와 그 위의 효과
            var muzzlePoints = new[] { new Vector2(-0.84375f, 0.875f), new Vector2(0.78125f, 0.875f) };
            var eyePoints = new[] { new Vector2(-0.40625f, 2.46875f), new Vector2(0.34375f, 2.46875f) };
            var muzzles = new Transform[2];
            var eyes = new Transform[2];
            var muzzleCharges = new SpriteClipPlayer[2];
            var cores = new LineRenderer[2];
            var halos = new LineRenderer[2];
            var laserDefaults = new GuardEyeLaserDefaults();
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "Left" : "Right";
                muzzles[i] = CreateChild(root.transform, "MuzzlePoint" + side, muzzlePoints[i]).transform;
                eyes[i] = CreateChild(root.transform, "EyePoint" + side, eyePoints[i]).transform;

                // 준비 효과는 몸과 한 덩어리로 정렬하고, 왼쪽 총구 기준으로 그려 오른쪽에서는 총구 그림처럼 좌우로 뒤집음
                muzzleCharges[i] = AddPlayer(visual, "MuzzleCharge" + side, 25, material, muzzlePoints[i], false);
                muzzleCharges[i].GetComponent<SpriteRenderer>().flipX = i == 1;
                halos[i] = AddLine(root.transform, "BeamHalo" + side, 39, material, laserDefaults.HaloWidth, laserDefaults.HaloColor);
                cores[i] = AddLine(root.transform, "BeamCore" + side, 40, material, laserDefaults.CoreWidth, laserDefaults.CoreColor);
            }
            SpriteRenderer impactGlow = AddSprite(root.transform, "ImpactGlow", null, 37, material, Vector2.zero);
            SpriteRenderer impactZone = AddSprite(root.transform, "ImpactZone", null, 41, material, Vector2.zero);
            impactGlow.color = laserDefaults.GlowColor;
            impactZone.color = laserDefaults.ZoneColor;
            impactGlow.enabled = impactZone.enabled = false;
            ParticleSystem sparks = AddParticles(root.transform, "Sparks", 42, material);
            ParticleSystem smoke = AddParticles(root.transform, "Smoke", 42, material);
            AudioSource handAudio = AddAudio(root.transform, "HandAudio");
            AudioSource laserAudio = AddAudio(root.transform, "LaserAudio");
            AudioSource muzzleAudio = AddAudio(root.transform, "MuzzleAudio");

            // 수정 크기의 피격 판정과 몸 아래쪽의 진입 차단
            GameObject hitbox = CreateChild(root.transform, "CrystalHitbox", new Vector2(-0.03125f, 0.8125f));
            hitbox.layer = hitLayer;
            var hitShape = hitbox.AddComponent<BoxCollider2D>();
            hitShape.isTrigger = true;
            hitShape.size = new Vector2(11f * Pixel, 12f * Pixel);
            var hit = hitbox.AddComponent<Hitbox>();
            hit.Owner = root.GetComponent<BossCharacter>();
            hit.TargetKind = TargetKind.Boss;
            var body = root.GetComponent<BoxCollider2D>();
            if (body != null)
            {
                body.offset = new Vector2(-0.03125f, 0.8f);
                body.size = new Vector2(3.4f, 1.4f);
            }

            // 보스 본체: 총구·탄환·예고·실행체 연결
            var boss = root.GetComponent<Boss_01>();
            boss.Muzzles = muzzles;
            boss.NormalPrefab = AssetDatabase.LoadAssetAtPath<NormalProjectile>(NormalRedPath);
            boss.GoldFastPrefab = AssetDatabase.LoadAssetAtPath<GoldProjectile>(FastGoldPath);
            boss.WarningPrefab = warning;
            boss.MuzzleFlashes = new[] { flashLeft, flashRight };
            boss.MuzzleFlashClips = new[] { clips["muzzle-left"], clips["muzzle-right"] };
            boss.MuzzleCharges = muzzleCharges;
            boss.MuzzleChargeClip = clips["muzzle-charge"];
            boss.MuzzleAudio = muzzleAudio;
            boss.MuzzleChargeSound = audio["guard-muzzle-charge.mp3"];
            boss.MuzzleFireSound = audio["guard-muzzle-fire.mp3"];
            boss.BattleMusic = audio["guard-bgm.ogg"];
            if (boss.PatternGap <= 0f) boss.PatternGap = 2.2f;
            if (boss.NormalPrefab == null || boss.GoldFastPrefab == null) throw new InvalidOperationException("Bullet prefabs are missing their projectile components.");

            // 두 손 실행체
            var hand = root.GetComponent<GuardHandAttack>();
            if (hand == null) hand = root.AddComponent<GuardHandAttack>();
            hand.Hands = new[] { handLeft.transform, handRight.transform };
            hand.HandRenderers = new[] { handLeft, handRight };
            hand.HandShadows = new[] { shadowLeft, shadowRight };
            hand.BodyShadow = bodyShadow;
            hand.HandSprites = new[] { handLeft.sprite, handRight.sprite };
            hand.HandSmearClips = new[] { clips["fist-smear-left"], clips["fist-smear-right"] };
            hand.CombinedRenderer = combined;
            hand.CombinedSprite = combined.sprite;
            hand.CombinedSmearClip = clips["fist-smear-both"];
            hand.ChargeBack = chargeBack;
            hand.ChargeFront = chargeFront;
            hand.ChargeBackClip = clips["fist-charge-back"];
            hand.ChargeFrontClip = clips["fist-charge-front"];
            hand.StompRingClip = clips["stomp-ring"];
            hand.BurstClip = clips["slam-burst"];
            hand.CrackClip = clips["slam-crack"];
            hand.DustClip = clips["slam-dust"];
            hand.GroundHitClip = clips["ground-hit"];
            hand.WaveTelegraphClip = clips["wave-telegraph"];
            hand.WarningPrefab = warning;
            hand.SpriteMaterial = material;
            hand.Audio = handAudio;
            hand.SlamClip = audio["guard-fist-slam.ogg"];
            hand.ChargedSlamClip = audio["guard-fist-slam-charged.mp3"];
            hand.ChargeClip = audio["guard-fist-charge.mp3"];
            hand.WaveBlastClip = audio["guard-wave-blast.mp3"];
            boss.HandAttack = hand;

            // 눈 레이저 실행체
            var laser = root.GetComponent<GuardEyeLaser>();
            if (laser == null) laser = root.AddComponent<GuardEyeLaser>();
            laser.Eyes = eyes;
            laser.BeamCores = cores;
            laser.BeamHalos = halos;
            laser.EyeFx = eyeFx;
            laser.EyeTellClip = clips["eye-laser-tell"];
            laser.EyeFireClip = clips["eye-laser-fire"];
            laser.ImpactZone = impactZone;
            laser.ImpactGlow = impactGlow;
            laser.Sparks = sparks;
            laser.Smoke = smoke;
            laser.Audio = laserAudio;
            laser.ChargeClip = audio["guard-laser-charge.ogg"];
            laser.FireClip = audio["guard-laser-fire.ogg"];
            boss.EyeLaser = laser;

            // 공용 기절효과
            var groggy = root.GetComponent<BossGroggyFx>();
            if (groggy == null) groggy = root.AddComponent<BossGroggyFx>();
            groggy.OrbClip = clips["groggy-orb"];
            groggy.ContactClip = clips["groggy-contact"];
            groggy.StarClip = clips["groggy-star"];
            groggy.SpriteMaterial = material;
            groggy.FlashMaterial = flashMaterial;
            groggy.FlashTargets = new[] { torso, head, eyeLeft, eyeRight, muzzleLeft, muzzleRight, crystal, crystalFx.GetComponent<SpriteRenderer>(), handLeft, handRight };
            PrefabUtility.SaveAsPrefabAsset(root, GuardPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>프리팹에 미리 넣어 둘 레이저 표시값을 실행 코드의 기본값에서 읽어 옴</summary>
    private class GuardEyeLaserDefaults
    {
        public readonly float CoreWidth; // 중심 기둥의 지름
        public readonly float HaloWidth; // 외곽 기둥의 지름
        public readonly Color CoreColor; // 중심 기둥과 접점의 색
        public readonly Color HaloColor; // 외곽 기둥의 색
        public readonly Color GlowColor; // 접점 주변 빛의 색
        public readonly Color ZoneColor; // 지면 피해 타원의 색

        /// <summary>임시 객체에 컴포넌트를 붙여 선언된 기본값을 읽음</summary>
        public GuardEyeLaserDefaults()
        {
            var probe = new GameObject("LaserDefaults") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                probe.SetActive(false);
                var laser = probe.AddComponent<GuardEyeLaser>();
                CoreWidth = laser.BeamRadius * 2f;
                HaloWidth = laser.BeamRadius * 2f * laser.HaloScale;
                CoreColor = laser.CoreColor;
                HaloColor = laser.HaloColor;
                GlowColor = new Color(laser.CoreColor.r, laser.CoreColor.g, laser.CoreColor.b, laser.GlowOpacity);
                ZoneColor = laser.ImpactZoneColor;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }
    }

    /// <summary>맵의 경비 조우 영역에 조절식 전투 구역이 없으면 붙이고 표시 재질을 연결하며 체크포인트 수정에 공통 앞뒤 정렬 적용</summary>
    private static void EnsureArena(Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            Transform encounter = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "GuardRobotEncounter") encounter = child;
            if (encounter == null) throw new InvalidOperationException("Map prefab has no GuardRobotEncounter object.");

            // 이미 조정한 구역 크기와 여백은 그대로 두고 참조만 맞춤
            var arena = encounter.GetComponent<BossArena>();
            if (arena == null) arena = encounter.gameObject.AddComponent<BossArena>();
            arena.SpriteMaterial = material;

            // 체크포인트 수정은 밑동 높이로 앞뒤를 가리고 상호작용 안내 그림은 지금의 순서 그대로 항상 위에 그림
            foreach (CrystalCheckPoint checkpoint in root.GetComponentsInChildren<CrystalCheckPoint>(true))
            {
                if (checkpoint.GetComponent<DepthSorter>() == null) AddDepthSorter(checkpoint.gameObject, 0f, null);
                if (checkpoint.InteractionPrompt != null) DepthSorter.DrawOnGround(checkpoint.InteractionPrompt.gameObject, checkpoint.InteractionPrompt.sortingOrder);
            }
            PrefabUtility.SaveAsPrefabAsset(root, MapPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>렌더러에 착지 왜곡 패스가 없으면 추가하고 재질 연결</summary>
    private static void EnsureRendererFeature(Material material)
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (data == null) throw new InvalidOperationException("Renderer data is missing: " + RendererPath);
        foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            if (feature is ImpactDistortionRendererFeature existing)
            {
                existing.SourceMaterial = material;
                EditorUtility.SetDirty(existing);
                return;
            }

        // URP 편집기와 같은 방식으로 하위 에셋을 만들고 목록과 식별자 표에 등록
        var created = ScriptableObject.CreateInstance<ImpactDistortionRendererFeature>();
        created.name = "ImpactDistortion";
        created.SourceMaterial = material;
        AssetDatabase.AddObjectToAsset(created, data);
        var serialized = new SerializedObject(data);
        SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
        SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = created;

        // 식별자 표가 비어 있거나 어긋나 있어도 목록 순서대로 전부 다시 기록
        map.arraySize = features.arraySize;
        for (int i = 0; i < features.arraySize; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(features.GetArrayElementAtIndex(i).objectReferenceValue, out string _, out long localId);
            map.GetArrayElementAtIndex(i).longValue = localId;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    /// <summary>Assets 기준 경로의 Sprite를 불러오고 없으면 실패 처리</summary>
    private static Sprite LoadSprite(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + relativePath);
        if (sprite == null) throw new InvalidOperationException("Sprite is missing: " + relativePath);
        return sprite;
    }

    /// <summary>경비 부위 그림 한 장 불러오기</summary>
    private static Sprite LoadPart(string name)
    {
        return LoadSprite("Art/Enemy/Guard/Parts/" + name + ".png");
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

    /// <summary>StageManager가 있는 공용 객체에 전투 배경음 재생기가 없으면 추가</summary>
    private static void EnsureBattleMusic()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SystemsPath);
        try
        {
            StageManager stage = root.GetComponentInChildren<StageManager>(true);
            if (stage == null) throw new InvalidOperationException("GameSystems prefab has no StageManager.");
            if (stage.GetComponent<BattleMusic>() != null) return;
            stage.gameObject.AddComponent<BattleMusic>();
            PrefabUtility.SaveAsPrefabAsset(root, SystemsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>발끝 높이로 앞뒤를 가리는 공통 정렬을 붙이고 기준선과 발끝으로 삼을 바닥 지점 지정</summary>
    private static void AddDepthSorter(GameObject target, float footOffset, Transform groundPoint)
    {
        var sorter = target.AddComponent<DepthSorter>();
        sorter.FootOffset = footOffset;
        sorter.GroundPoint = groundPoint;
        target.GetComponent<SortingGroup>().sortingOrder = DepthSorter.SharedOrder;
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

    /// <summary>두 점을 잇는 빔 한 층을 꺼진 상태로 생성</summary>
    private static LineRenderer AddLine(Transform parent, string name, int order, Material material, float width, Color color)
    {
        var line = CreateChild(parent, name, Vector2.zero).AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.sortingOrder = order;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 6;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.enabled = false;
        return line;
    }

    /// <summary>코드로만 방출하는 월드 공간 입자 생성</summary>
    private static ParticleSystem AddParticles(Transform parent, string name, int order, Material material)
    {
        GameObject child = CreateChild(parent, name, Vector2.zero);
        var particles = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        return particles;
    }

    /// <summary>자동 재생하지 않는 2D 오디오 소스 생성</summary>
    private static AudioSource AddAudio(Transform parent, string name)
    {
        var source = CreateChild(parent, name, Vector2.zero).AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }
}
