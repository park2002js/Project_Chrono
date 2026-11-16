using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Player 각성 동안 몸 뒤 불길과 앞쪽 불씨를 재생할 두 파티클과 공유 재질 연결</summary>
public static class FeverAuraSetup
{
    private const string PrefabPath = "Assets/Prefabs/Startup/Player.prefab"; // 기존 Player 연결과 조정값을 유지할 프리팹
    private const string Folder = "Assets/Art/Player/FeverAura/"; // 황금 픽셀 오라의 셰이더·공유 재질 경로
    private const string ShaderName = "Chrono/FeverAura"; // 별도 이미지 없이 불길·불씨 실루엣을 그릴 Unlit 셰이더

    /// <summary>현재 씬을 바꾸지 않고 기존 Player 프리팹에 각성 표시 연결</summary>
    [MenuItem("Tools/Chrono/Connect Fever Aura")]
    public static void Apply()
    {
        // 새 에셋을 가져온 뒤 원래 프리팹의 다른 컴포넌트와 수치 보존
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

    /// <summary>이미 열린 Player의 몸체 정렬을 기준으로 오라만 중복 없이 추가</summary>
    public static void ApplyToPrefab(GameObject root)
    {
        // 다른 효과음·무기·능력 수치를 건드리지 않고 몸체의 고정 정렬만 한 번 조회
        if (root == null) throw new ArgumentNullException(nameof(root));
        CharacterRendering appearance = root.GetComponent<CharacterRendering>();
        if (appearance == null || appearance.BodySprite == null)
            throw new InvalidOperationException("Connect the Player body before Fever aura setup.");
        SpriteRenderer body = appearance.BodySprite;
        Transform holder = root.transform.Find("FeverAura");
        if (holder == null)
        {
            holder = new GameObject("FeverAura").transform;
            holder.SetParent(root.transform, false);
        }
        holder.localPosition = Vector3.zero;
        holder.localRotation = Quaternion.identity;
        holder.localScale = Vector3.one;
        holder.gameObject.layer = root.layer;
        holder.gameObject.SetActive(true);

        // 몸 뒤 가는 불길과 앞쪽 작은 불씨를 별개 공유 재질로 연결
        Material flames = LoadMaterial("FeverFlames.mat", false);
        Material embers = LoadMaterial("FeverEmbers.mat", true);
        ParticleSystem flameSystem = ConfigureEmitter(holder, "Flames", flames, true, body.sortingLayerID, body.sortingOrder - 1);
        ParticleSystem emberSystem = ConfigureEmitter(holder, "Embers", embers, false, body.sortingLayerID, body.sortingOrder + 2);
        FeverAuraFx aura = root.GetComponent<FeverAuraFx>();
        if (aura == null) aura = root.AddComponent<FeverAuraFx>();
        aura.Emitters = new[] { flameSystem, emberSystem };
        EditorUtility.SetDirty(aura);
    }

    /// <summary>동일 이름의 파티클을 재사용하고 발부근의 좁은 영역에서 위로 상승하도록 준비</summary>
    private static ParticleSystem ConfigureEmitter(Transform holder, string name, Material material, bool flames, int layer, int order)
    {
        // 새 파티클 생성과 재적용 모두 같은 자식과 로컬 좌표 사용
        Transform child = holder.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(holder, false);
        }
        child.localPosition = new Vector3(0f, 0.1f, 0f);
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        child.gameObject.layer = holder.gameObject.layer;
        child.gameObject.SetActive(true);
        ParticleSystem system = child.GetComponent<ParticleSystem>();
        if (system == null) system = child.gameObject.AddComponent<ParticleSystem>();
        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        system.useAutoRandomSeed = false;
        system.randomSeed = flames ? 48121u : 48122u;

        // Player를 따라가는 로컬 입자이며 게임 정지 중에는 상승·수명도 정지
        ParticleSystem.MainModule main = system.main;
        main.duration = 2f;
        main.loop = true;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpeed = 1f;
        main.useUnscaledTime = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(flames ? 0.7f : 0.65f, 1f);
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(flames ? 0.18f : 0.04f, flames ? 0.28f : 0.07f);
        main.startSizeY = flames ? new ParticleSystem.MinMaxCurve(0.5f, 0.75f) : main.startSizeX;
        main.startSizeZ = 1f;
        main.startRotation3D = false;
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.maxParticles = flames ? 32 : 24;
        main.stopAction = ParticleSystemStopAction.None;

        // 몸폭 안에서 소량만 방출해 큰 불투명 막이나 얼굴 가림 방지
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = flames ? 20f : 10f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(flames ? 0.8f : 0.65f, 0.06f, 0f);
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(flames ? -0.05f : -0.09f, flames ? 0.05f : 0.09f);
        velocity.y = flames ? new ParticleSystem.MinMaxCurve(1.15f, 1.7f) : new ParticleSystem.MinMaxCurve(1.05f, 1.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 가는 불길만 조금 흔들고 위쪽에서 크기·알파를 함께 줄여 소멸
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = flames;
        noise.separateAxes = true;
        noise.strengthX = 0.045f;
        noise.strengthY = 0.015f;
        noise.strengthZ = 0f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.8f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.18f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0.1f)));
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(BuildGradient(flames));

        // 고정 수의 쿼드를 몸체 앞뒤에 표시하고 종료 시 런타임이 즉시 비움
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = true;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
        }); // 셰이더가 사용하는 위치·입자색·정적 UV만 전달
        EditorUtility.SetDirty(system);
        EditorUtility.SetDirty(renderer);
        return system;
    }

    /// <summary>황금·노랑에서 주황으로 식으며 사라지는 초기 오라 팔레트</summary>
    private static Gradient BuildGradient(bool flames)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.65f, 0.08f), 0f),
            new GradientColorKey(new Color(1f, 0.88f, 0.25f), 0.25f),
            new GradientColorKey(new Color(1f, 0.48f, 0.035f), 0.8f),
            new GradientColorKey(new Color(1f, 0.28f, 0.02f), 1f)
        }, new[]
        {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(flames ? 0.85f : 1f, 0.12f),
            new GradientAlphaKey(flames ? 0.7f : 0.8f, 0.6f),
            new GradientAlphaKey(0f, 1f)
        });
        return gradient;
    }

    /// <summary>이미지 없는 불꽃·불씨 실루엣을 공유할 두 재질을 GUID 보존하며 준비</summary>
    private static Material LoadMaterial(string name, bool ember)
    {
        // 같은 셰이더에서 형태만 고르고 기존 Tint 조정은 보존
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException("Missing Fever aura shader: " + ShaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name);
        if (material == null)
        {
            material = new Material(shader) { name = ember ? "FeverEmbers" : "FeverFlames" };
            AssetDatabase.CreateAsset(material, Folder + name);
        }
        else material.shader = shader;
        material.SetFloat("_EmberMode", ember ? 1f : 0f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
