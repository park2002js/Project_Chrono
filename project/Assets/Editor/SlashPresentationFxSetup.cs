using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Slash 전용 화면 패스·섬광 재질·신규 제작 효과음을 기존 Player에 연결</summary>
public static class SlashPresentationFxSetup
{
    /// <summary>현재 2D 렌더러와 Player의 기존 GUID·다른 기능을 보존하며 연출 자산 연결</summary>
    [MenuItem("Tools/Chrono/Connect Slash Presentation FX")]
    public static void Run()
    {
        // 코드 셰이더와 자체 제작 WAV를 최종 게임 에셋에 연결
        Directory.CreateDirectory("Assets/Art/FX/Slash");
        Material space = PrepareMaterial("Assets/Shaders/SlashSpace.shader", "Assets/Art/FX/Slash/SlashSpace.mat");
        Material glint = PrepareMaterial("Assets/Shaders/SlashBladeGlint.shader", "Assets/Art/FX/Slash/SlashBladeGlint.mat");
        AudioClip sheath = PrepareAudio("Assets/Audio/Slash/sheath-clack-r01.wav");
        AudioClip slash = PrepareAudio("Assets/Audio/Slash/space-slash-r01.wav");

        // Quality가 실제 참조하는 Renderer2D에 전용 feature 하나만 추가
        ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/Renderer2D.asset");
        if (renderer == null) throw new InvalidOperationException("The current Renderer2D asset is missing.");
        SlashSpaceRendererFeature feature = null;
        foreach (ScriptableRendererFeature candidate in renderer.rendererFeatures)
            if (candidate is SlashSpaceRendererFeature existing) feature = existing;
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<SlashSpaceRendererFeature>();
            feature.name = "Slash World Space Cut";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.passMaterial = space;
        feature.SetActive(true);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(feature);

        // Player가 정지 시간과 별도로 재사용할 FX·오디오 컴포넌트 연결
        string path = "Assets/Prefabs/Startup/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SlashPresentationFx fx = player.GetComponent<SlashPresentationFx>();
            if (fx == null) fx = player.AddComponent<SlashPresentationFx>();
            fx.SpaceSlashMaterial = space;
            fx.BladeGlintMaterial = glint;
            fx.SheathClip = sheath;
            fx.SpaceSlashClip = slash;
            if (fx.CutColor == new Color(0.7f, 0.9f, 1f, 1f))
                fx.CutColor = new Color(1f, 212f / 255f, 59f / 255f, 1f);
            space.SetColor("_CutColor", fx.CutColor);
            EditorUtility.SetDirty(space);
            AudioSource source = player.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("[SlashPresentationFxSetup] World-only space cut, blade glint and original synthesized sound drafts connected.");
    }

    /// <summary>원본 셰이더를 참조하는 재질을 만들거나 기존 GUID로 재사용</summary>
    private static Material PrepareMaterial(string shaderPath, string materialPath)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) throw new FileNotFoundException("Slash presentation shader is missing.", shaderPath);
        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Slash presentation shader has compile errors: " + shaderPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>짧은 타이밍 기준음을 압축 없이 미리 읽어 재생 지연을 줄임</summary>
    private static AudioClip PrepareAudio(string path)
    {
        AssetDatabase.ImportAsset(path);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new FileNotFoundException("Original synthesized Slash sound draft is missing.", path);
        importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.preloadAudioData = true;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = 48000;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
