using System;
using UnityEditor;
using UnityEngine;

/// <summary>Player의 별도 효과음 소스와 원작 일곱 적중 이미지를 기존 프리팹에 연결</summary>
public static class PlayerFeedbackSetup
{
    private const string PrefabPath = "Assets/Prefabs/Startup/Player.prefab"; // 기존 기능과 GUID를 유지할 Player 프리팹
    private const string AudioFolder = "Assets/Audio/PlayerFeedback/"; // bank에서 추출한 여섯 PCM 효과음 경로
    private const string HitFolder = "Assets/Art/Player/HitFx/"; // 원본 픽셀을 보존한 일곱 피격 이미지 경로
    private const string MaterialPath = HitFolder + "PlayerHit.mat"; // 모든 재사용 적중 표시가 공유할 Unlit 재질
    private const string ShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default"; // 밝기 속성 없이 원본 RGBA를 표시할 설치된 셰이더

    /// <summary>Player 프리팹만 열어 효과음·적중 에셋을 연결하고 같은 경로에 저장</summary>
    [MenuItem("Tools/Chrono/Connect Player Feedback")]
    public static void Apply()
    {
        // 현재 씬을 바꾸지 않고 기존 프리팹의 모든 연결과 조정값 보존
        AssetDatabase.Refresh();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyToPrefab(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>이미 열린 Player에 에셋 참조를 연결하고 기존 피드백 음량·걸음 거리 유지</summary>
    public static void ApplyToPrefab(GameObject root)
    {
        // 신규 컴포넌트만 클래스 기본값으로 시작하고 재적용 때 조정 수치는 덮지 않음
        if (root == null) throw new ArgumentNullException(nameof(root));
        PlayerFeedback feedback = root.GetComponent<PlayerFeedback>();
        if (feedback == null) feedback = root.AddComponent<PlayerFeedback>();

        // Slash 오디오와 별개인 자기 자식 소스를 한 번 생성하거나 재사용
        Transform audio = root.transform.Find("FeedbackAudio");
        if (audio == null)
        {
            audio = new GameObject("FeedbackAudio").transform;
            audio.SetParent(root.transform, false);
        }
        AudioSource source = audio.GetComponent<AudioSource>();
        if (source == null) source = audio.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 1f;
        feedback.Source = source;

        // 짧은 원본 PCM을 미리 읽고 콤보·적중·세 발소리의 용도별 연결
        feedback.Attack1Clip = LoadAudio(AudioFolder + "AttackSwingA.wav");
        feedback.Attack2Clip = LoadAudio(AudioFolder + "AttackSwingB.wav");
        feedback.HitClip = LoadAudio(AudioFolder + "AttackHit.wav");
        feedback.FootstepClips = new AudioClip[3];
        for (int i = 0; i < feedback.FootstepClips.Length; i++)
            feedback.FootstepClips[i] = LoadAudio(AudioFolder + "FootstepTile" + (i + 1).ToString("00") + ".wav");

        // 일곱 원본 전체 캔버스를 순서대로 연결하며 표시 시간은 런타임이 관리
        feedback.HitFrames = new Sprite[7];
        for (int i = 0; i < feedback.HitFrames.Length; i++)
            feedback.HitFrames[i] = LoadSprite(HitFolder + "Slash_Player" + i + ".png");
        feedback.HitMaterial = LoadMaterial();
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(feedback);
    }

    /// <summary>추출한48kHz stereo WAV를 압축·다운믹스 없이 미리 준비</summary>
    private static AudioClip LoadAudio(string path)
    {
        // 원본 표본을 그대로 재생할 PCM·Decompress On Load 설정 적용
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new InvalidOperationException("Missing Player feedback audio: " + path);
        importer.forceToMono = false;
        importer.loadInBackground = false;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.preloadAudioData = true;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = 48000;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        // 필수 연결이 비어 있는 상태로 저장되지 않도록 실제 가져온 클립 확인
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("Player feedback audio did not import: " + path);
        return clip;
    }

    /// <summary>5×57 전체 여백·중앙 피벗·16PPU를 유지해 한 장의 Sprite로 가져오기</summary>
    private static Sprite LoadSprite(string path)
    {
        // 얇은 피격 섬광의 원본 픽셀이 필터·압축·자동 잘림으로 바뀌지 않도록 설정
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing Player hit frame: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        // 마지막 두 픽셀까지 포함한 공통 캔버스 확인
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (frame == null || frame.rect.width != 5f || frame.rect.height != 57f)
            throw new InvalidOperationException("Player hit frames must keep their 5x57 canvas: " + path);
        return frame;
    }

    /// <summary>밝기 속성이 없는 설치된 Sprite Unlit 재질을 GUID 보존하며 준비</summary>
    private static Material LoadMaterial()
    {
        // 원작의 연결되지 않은 Intensity 곡선 대신 원본 색을 그대로 표시
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException("Missing Player hit shader: " + ShaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "PlayerHit" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }
}
