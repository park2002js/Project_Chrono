using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>현재 경비 프리팹의 GUID와 기존 맵을 보존하며 다섯 패턴과 눈 레이저 연결</summary>
public static class GuardBossSetup
{
    public const string SpritePath = "Assets/Art/Enemy/Guard/guard-front.png"; // 사용자 최신 경비 정면 PNG의 실제 게임 사용 위치
    public const string BodySpritePath = "Assets/Art/Enemy/Guard/guard-body.png"; // 양손을 숨긴 같은 원본 크기의 경비 몸통 위치
    public const string LeftHandSpritePath = "Assets/Art/Enemy/Guard/guard-hand-left.png"; // 왼손의 독립 원본 픽셀 Sprite 위치
    public const string RightHandSpritePath = "Assets/Art/Enemy/Guard/guard-hand-right.png"; // 오른손의 독립 원본 픽셀 Sprite 위치
    public const string LaserGlowPath = "Assets/Art/Enemy/Guard/guard-laser-glow.png"; // 연분홍 레이저 바깥 빛의 부드러운 알파 단면
    public const string LaserGlowMaterialPath = "Assets/Art/Enemy/Guard/GuardLaserGlow.mat"; // 기존 비조명 셰이더로 단면을 표시할 공용 재질
    public const string PrefabPath = "Assets/Prefabs/Startup/GuardRobot.prefab"; // 기존 Stage가 참조하던 경비 프리팹 위치
    private const string SystemsPath = "Assets/Prefabs/Startup/GameSystems.prefab"; // 바뀐 파생 컴포넌트를 재연결할 기존 시스템 프리팹 위치
    private const string MapPath = "Assets/Prefabs/Startup/Map.prefab"; // 보스 배치 Transform만 조정할 기존 맵 위치
    private static readonly Vector3 SpawnPosition = new Vector3(14.5f, 23.5f, 0f); // 출구를 몸체로 가릴 첫 실전 배치 위치
    private static readonly Vector3[] MuzzlePositions = // 원본 PNG의 다섯 총구 중심을 16 PPU로 환산한 로컬 좌표
    {
        new Vector3(-1.96875f, 1.125f, 0f), new Vector3(-0.96875f, 1.125f, 0f), new Vector3(0.03125f, 1.125f, 0f),
        new Vector3(1.03125f, 1.125f, 0f), new Vector3(2.03125f, 1.125f, 0f)
    };

    /// <summary>기존 프리팹과 Map의 필요한 참조·Transform만 저장</summary>
    [MenuItem("Tools/Chrono/Prepare Guard Boss Patterns")]
    public static void Run()
    {
        // 아트와 기존 Gold 프리팹의 준비 여부를 먼저 확인
        if (!File.Exists(SpritePath)) throw new FileNotFoundException("The latest guard PNG must be prepared first.", SpritePath);
        BossHazardArtSetup.Run();
        ImportSprite(SpritePath, new Vector2(0.5f, 16f / 120f));
        Sprite sprite = ImportSprite(BodySpritePath, new Vector2(0.5f, 16f / 120f));
        Sprite leftHand = ImportSprite(LeftHandSpritePath, new Vector2(0.5f, 0f));
        Sprite rightHand = ImportSprite(RightHandSpritePath, new Vector2(0.5f, 0f));
        GoldProjectile gold = AssetDatabase.LoadAssetAtPath<GoldProjectile>(BossHazardArtSetup.GoldPath);
        NormalProjectile normal = AssetDatabase.LoadAssetAtPath<NormalProjectile>(BossHazardArtSetup.BulletPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Laboratory/LaboratoryUnlit.mat");
        if (sprite == null || sprite.texture.width != 192 || sprite.texture.height != 120 || gold == null || normal == null || material == null)
            throw new InvalidOperationException("Guard setup requires the 192x120 Sprite, current GoldProjectile and existing unlit material.");

        // 기존 정적 Boss를 같은 프리팹 안의 파생 타입으로 교체하고 공통 수치 유지
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool wasActive = root.activeSelf;
            root.SetActive(false);
            Boss_01 boss = root.GetComponent<Boss_01>();
            if (boss == null)
            {
                BossCharacter previous = root.GetComponent<BossCharacter>();
                if (previous == null) throw new InvalidOperationException("The existing guard prefab must contain BossCharacter.");
                string state = JsonUtility.ToJson(previous);
                UnityEngine.Object.DestroyImmediate(previous);
                boss = root.AddComponent<Boss_01>();
                JsonUtility.FromJsonOverwrite(state, boss);
            }
            boss.basicSP = boss.SP = 0f;
            boss.GoldPrefab = gold;
            boss.NormalPrefab = normal;
            boss.MuzzleIndex = 2;
            boss.ActivationRange = 4.5f;
            boss.Warmup = 0.8f;
            boss.Recovery = 1f;
            boss.PatternCooldown = 2f;
            boss.BulletSpeed = 6f;
            boss.BulletLifetime = 6f;
            boss.ProjectileDamage = 10f;
            boss.CentralRowCount = 6;
            boss.CentralRowSpacing = 1f;
            boss.CentralBulletSpeed = 4.5f;
            boss.CentralBulletLifetime = 0f;
            boss.CentralBulletDamage = 10f;
            boss.FanWarmup = 0.4f;
            boss.FanBurstInterval = 0.3f;
            boss.FanRecovery = 0.5f;
            boss.FanBulletCount = 5;
            boss.FanSpread = 60f;
            boss.FanBulletSpeed = 6f;
            boss.FanBulletLifetime = 0f;
            boss.FanBulletDamage = 10f;
            boss.Muzzles = new Transform[MuzzlePositions.Length];
            for (int i = 0; i < MuzzlePositions.Length; i++)
            {
                Transform muzzle = root.transform.Find("Muzzle_" + (i + 1).ToString("D2"));
                if (muzzle == null)
                {
                    muzzle = new GameObject("Muzzle_" + (i + 1).ToString("D2")).transform;
                    muzzle.SetParent(root.transform, false);
                }
                muzzle.localPosition = MuzzlePositions[i];
                muzzle.localScale = Vector3.one;
                boss.Muzzles[i] = muzzle;
            }

            // 최신 원본의 자연 배율·하단 피벗과 몸통 판정만 연결
            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("The existing guard must retain its Visual child.");
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.sortingOrder = 20;
            PrepareHands(root, boss, leftHand, rightHand, material);
            PrepareEyes(root, boss, material);
            BoxCollider2D body = root.GetComponent<BoxCollider2D>();
            body.size = new Vector2(5.5f, 3f);
            body.offset = new Vector2(0f, 1.5f);
            body.isTrigger = false;
            PrepareBodyHitbox(root, boss);
            foreach (Hitbox hitbox in root.GetComponentsInChildren<Hitbox>(true)) hitbox.Owner = boss;
            root.GetComponent<BossSpawner>().Boss = boss;
            root.SetActive(wasActive);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 프리팹 GUID는 유지하고 실제 Stage의 컴포넌트 참조만 새 파생 타입으로 연결
        GameObject systems = PrefabUtility.LoadPrefabContents(SystemsPath);
        try
        {
            systems.GetComponent<StageManager>().BossPrefab = AssetDatabase.LoadAssetAtPath<Boss_01>(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(systems, SystemsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(systems); }
        GameObject mapRoot = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            MapManager map = mapRoot.GetComponent<MapManager>();
            bool placed = false;
            foreach (MapManager.BossSpawn placement in map.BossPositions)
                if (placement.BossId == GameManager.SubmittedBossId)
                {
                    placement.Position.localPosition = SpawnPosition;
                    placed = true;
                }
            if (!placed) throw new InvalidOperationException("The existing map must retain its guard spawn record.");
            PrefabUtility.SaveAsPrefabAsset(mapRoot, MapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(mapRoot); }
        AssetDatabase.SaveAssets();
        Debug.Log("[GuardBossSetup] Existing GuardRobot connects five patterns, five muzzles, reusable hands/warning and both eye beams.");
    }

    /// <summary>기존 픽셀과 자연 배율을 보존하며 몸통·손의 원점만 구분해 가져오기</summary>
    private static Sprite ImportSprite(string path, Vector2 pivot)
    {
        // 이전 합성본과 새 분리 원본은 각각 기존 GUID를 유지
        if (!File.Exists(path)) throw new FileNotFoundException("The split guard PNG must be prepared first.", path);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>동일 경비 수명에서 양손과 원형 예고 한 개를 미리 연결</summary>
    private static void PrepareHands(GameObject root, Boss_01 boss, Sprite left, Sprite right, Material material)
    {
        // 손 Sprite는 별도 Collider 없이 몸통과 같은 16 PPU 자연 배율 사용
        if (left == null || right == null || left.texture.width != 34 || left.texture.height != 40
            || right.texture.width != 34 || right.texture.height != 40)
            throw new InvalidOperationException("Both split guard hands must be 34x40 pixel Sprites.");
        Transform[] hands = new Transform[2];
        for (int i = 0; i < hands.Length; i++)
        {
            string name = i == 0 ? "HandLeft" : "HandRight";
            Transform hand = root.transform.Find(name);
            if (hand == null)
            {
                hand = new GameObject(name, typeof(SpriteRenderer)).transform;
                hand.SetParent(root.transform, false);
            }
            hand.localPosition = new Vector3(i == 0 ? -4.5f : 4.5f, 0f, 0f);
            hand.localRotation = Quaternion.identity;
            hand.localScale = Vector3.one;
            SpriteRenderer renderer = hand.GetComponent<SpriteRenderer>();
            renderer.sprite = i == 0 ? left : right;
            renderer.sharedMaterial = material;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.sortingOrder = 24;
            hands[i] = hand;
        }

        // 원형 예고는 발사 때마다 생성하지 않고 Boss 아래 비활성 상태로 보관
        Transform warningRoot = root.transform.Find("HandWarning");
        if (warningRoot == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHazardArtSetup.CircleWarningPath);
            GameObject warning = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            warning.name = "HandWarning";
            warningRoot = warning.transform;
        }
        warningRoot.localPosition = Vector3.zero;
        warningRoot.localRotation = Quaternion.identity;
        warningRoot.localScale = new Vector3(2.7f, 2.7f, 1f);
        AreaWarningVfx visual = warningRoot.GetComponent<AreaWarningVfx>();
        LineRenderer outline = warningRoot.GetComponent<LineRenderer>();
        outline.sortingOrder = 22;
        visual.Configure(WarningShape.Circle, new Color(1f, 0.12f, 0.16f, 0.45f));
        visual.FillRenderer.sortingOrder = 21;
        visual.FillRenderer.transform.localScale = Vector3.one;
        warningRoot.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(warningRoot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(warningRoot.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.FillRenderer);

        // 패턴 등록은 Controller Awake 순서와 무관하게 이 직렬화 참조만 사용
        GuardHandAttack attack = root.GetComponent<GuardHandAttack>();
        if (attack == null) attack = root.AddComponent<GuardHandAttack>();
        attack.Hands = hands;
        attack.Warning = visual;
        attack.Height = 2.5f;
        attack.RiseDuration = 0.35f;
        attack.TrackingSpeed = 10f;
        attack.ChargeDuration = 2.2f;
        attack.FallDuration = 0.12f;
        attack.ImpactRadius = 1.35f;
        attack.ImpactDamage = 10f;
        attack.RecoveryDuration = 0.35f;
        attack.ReturnDuration = 0.35f;
        attack.CentralChargeDuration = 1.2f;
        attack.CentralRadius = 3.5f;
        attack.CentralDamage = 15f;
        attack.CentralHandOffset = 1.1f;
        boss.HandAttack = attack;
    }

    /// <summary>기존 몸통의 물리 Collider와 전체 아트 피격 영역을 분리</summary>
    private static void PrepareBodyHitbox(GameObject root, Boss_01 boss)
    {
        // 외부 참조와 직접 호출이 없는 이전 root Hitbox만 새 단일 자식으로 이전
        Hitbox previous = root.GetComponent<Hitbox>();
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        Transform target = root.transform.Find("BodyHitbox");
        if (target == null)
        {
            target = new GameObject("BodyHitbox", typeof(BoxCollider2D), typeof(Hitbox)).transform;
            target.SetParent(root.transform, false);
        }
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        BoxCollider2D shape = target.GetComponent<BoxCollider2D>();
        shape.isTrigger = true;
        shape.size = new Vector2(5.75f, 4.875f);
        shape.offset = new Vector2(0f, 2.4375f);
        Hitbox hitbox = target.GetComponent<Hitbox>();
        hitbox.Owner = boss;
        hitbox.TargetKind = TargetKind.Boss;
        hitbox.PartId = 0;
    }

    /// <summary>실제 눈 픽셀 중심에 충전 표시와 재사용할 빔 세 겹을 연결</summary>
    private static void PrepareEyes(GameObject root, Boss_01 boss, Material material)
    {
        // 원본 8×2 흰 눈 두 곳의 중심을 기존 하단 피벗과 같은 16 PPU로 환산
        Material glowMaterial = PrepareLaserGlow(material);
        Sprite chargeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/BossTests/Circle.png");
        if (chargeSprite == null) throw new InvalidOperationException("Eye charging requires the existing white circle Sprite.");
        var eyes = new Transform[2];
        var cores = new LineRenderer[2];
        var bodies = new LineRenderer[2];
        var glows = new LineRenderer[2];
        var lights = new SpriteRenderer[2];
        for (int i = 0; i < eyes.Length; i++)
        {
            string name = i == 0 ? "EyeLeft" : "EyeRight";
            Transform eye = root.transform.Find(name);
            if (eye == null)
            {
                eye = new GameObject(name).transform;
                eye.SetParent(root.transform, false);
            }
            eye.localPosition = new Vector3(i == 0 ? -0.5625f : 0.4375f, 3.9375f, 0f);
            eye.localRotation = Quaternion.identity;
            eye.localScale = Vector3.one;
            eyes[i] = eye;
            cores[i] = PrepareEyeLine(eye, "Core", material, new Color(1f, 0.97f, 1f, 0.96f), 34);
            bodies[i] = PrepareEyeLine(eye, "Body", material, new Color(1f, 0.72f, 0.84f, 0.72f), 33);
            glows[i] = PrepareEyeLine(eye, "Glow", glowMaterial, new Color(1f, 0.3f, 0.65f, 0.28f), 32);
            Transform light = eye.Find("ChargeLight");
            if (light == null)
            {
                light = new GameObject("ChargeLight", typeof(SpriteRenderer)).transform;
                light.SetParent(eye, false);
            }
            light.localPosition = Vector3.zero;
            light.localRotation = Quaternion.identity;
            light.localScale = Vector3.one * 0.6f;
            SpriteRenderer charge = light.GetComponent<SpriteRenderer>();
            charge.sprite = chargeSprite;
            charge.sharedMaterial = material;
            charge.color = new Color(1f, 0.78f, 0.9f, 0.9f);
            charge.sortingOrder = 35;
            charge.enabled = false;
            lights[i] = charge;
        }

        // 다른 객체의 Awake 순서에 의존하지 않도록 같은 프리팹의 참조를 모두 직렬화
        GuardEyeLaser laser = root.GetComponent<GuardEyeLaser>();
        if (laser == null) laser = root.AddComponent<GuardEyeLaser>();
        laser.Eyes = eyes;
        laser.BeamCores = cores;
        laser.BeamBodies = bodies;
        laser.BeamGlows = glows;
        laser.ChargeLights = lights;
        PrepareImpacts(root, laser, chargeSprite, material);
        laser.ChargeDuration = 1.2f;
        laser.FiringDuration = 3f;
        laser.RecoveryDuration = 0.5f;
        laser.BeamWidth = 0.5f;
        laser.MaxLength = 40f;
        laser.BeamDamage = 10f;
        boss.EyeLaser = laser;
    }

    /// <summary>지면 착탄원의 같은 경계·채움·밝은 중심을 미리 준비해 재사용</summary>
    private static void PrepareImpacts(GameObject root, GuardEyeLaser laser, Sprite circle, Material material)
    {
        // 실제 피해 반경과 같은 단위 원 프리팹을 두 눈의 종점에 연결
        var warnings = new AreaWarningVfx[2];
        var cores = new SpriteRenderer[2];
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHazardArtSetup.CircleWarningPath);
        if (prefab == null) throw new InvalidOperationException("Eye impacts require the prepared shared circle warning.");
        for (int i = 0; i < warnings.Length; i++)
        {
            string name = i == 0 ? "LaserImpactLeft" : "LaserImpactRight";
            Transform point = root.transform.Find(name);
            if (point == null)
            {
                GameObject created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                created.name = name;
                point = created.transform;
            }
            point.localPosition = Vector3.zero;
            point.localRotation = Quaternion.identity;
            point.localScale = new Vector3(0.7f, 0.7f, 1f);
            AreaWarningVfx warning = point.GetComponent<AreaWarningVfx>();
            warning.FillOpacityMultiplier = 0.8f;
            warning.OutlineWidth = 0.04f;
            warning.Configure(WarningShape.Circle, new Color(1f, 0.48f, 0.68f, 0.95f));
            LineRenderer outline = point.GetComponent<LineRenderer>();
            outline.sortingOrder = 36;
            warning.FillRenderer.sortingOrder = 35;
            warning.FillRenderer.transform.localScale = Vector3.one;
            Transform hot = point.Find("HotCore");
            if (hot == null)
            {
                hot = new GameObject("HotCore", typeof(SpriteRenderer)).transform;
                hot.SetParent(point, false);
            }
            hot.localPosition = Vector3.zero;
            hot.localRotation = Quaternion.identity;
            hot.localScale = Vector3.one * 0.4f;
            SpriteRenderer core = hot.GetComponent<SpriteRenderer>();
            core.sprite = circle;
            core.sharedMaterial = material;
            core.color = new Color(1f, 0.98f, 1f, 0.98f);
            core.sortingOrder = 37;
            core.enabled = false;
            point.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(point);
            PrefabUtility.RecordPrefabInstancePropertyModifications(point.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(warning);
            PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
            PrefabUtility.RecordPrefabInstancePropertyModifications(warning.FillRenderer);
            warnings[i] = warning;
            cores[i] = core;
        }
        laser.ImpactWarnings = warnings;
        laser.ImpactCores = cores;
        laser.ImpactRadius = 0.35f;
    }

    /// <summary>월드 원점·끝점만 갱신할 비활성 레이저 선을 한 번 준비</summary>
    private static LineRenderer PrepareEyeLine(Transform parent, string name, Material material, Color color, int order)
    {
        // 빔 본체와 바깥 빛은 같은 위치를 사용하며 재질과 폭·알파만 구분
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(LineRenderer)).transform;
            child.SetParent(parent, false);
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        LineRenderer line = child.GetComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.zero);
        line.loop = false;
        line.numCapVertices = 8;
        line.numCornerVertices = 0;
        line.widthMultiplier = 1f;
        line.textureMode = LineTextureMode.Stretch;
        line.startColor = line.endColor = color;
        line.sortingOrder = order;
        line.enabled = false;
        return line;
    }

    /// <summary>프로파일 이미지와 기존 비조명 셰이더의 공용 Glow 재질 준비</summary>
    private static Material PrepareLaserGlow(Material original)
    {
        // 코드로 재현 가능한 흰 RGB·부드러운 단면 알파를 최초 한 번 출력
        if (!File.Exists(LaserGlowPath))
        {
            var texture = new Texture2D(8, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                float radius = Mathf.Abs((y + 0.5f) / texture.height * 2f - 1f);
                float alpha = Mathf.Pow(Mathf.Max(0f, 1f - radius * radius), 2f);
                for (int x = 0; x < texture.width; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(LaserGlowPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(LaserGlowPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(LaserGlowPath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(LaserGlowMaterialPath);
        if (material == null)
        {
            material = new Material(original) { name = "GuardLaserGlow" };
            AssetDatabase.CreateAsset(material, LaserGlowMaterialPath);
        }
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(LaserGlowPath));
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }
}
