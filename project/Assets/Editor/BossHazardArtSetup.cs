using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>픽셀 탄환과 반투명 채움·테두리 원형 경고를 기존 시험 참조와 함께 준비</summary>
public static class BossHazardArtSetup
{
    public const string BulletPath = "Assets/Prefabs/Enemy/NormalBullet.prefab"; // 일반 탄환의 실사용 프리팹 위치
    public const string GoldPath = "Assets/Prefabs/Enemy/GoldBullet.prefab"; // 금색 반사 탄환의 실사용 프리팹 위치
    public const string AreaPath = "Assets/Prefabs/Enemy/WarningArea.prefab"; // 원형 경고선 장판의 실사용 프리팹 위치
    public const string SpritePath = "Assets/Art/Enemy/Projectiles/normal-bullet.png"; // 픽셀 탄환의 출력 원본 위치
    public const string GoldSpritePath = "Assets/Art/Enemy/Projectiles/gold-bullet.png"; // 같은 형태의 금색 탄환 출력 원본 위치
    public const string NormalHaloPath = "Assets/Art/Enemy/Projectiles/normal-halo.png"; // 일반 탄환 주변의 부드러운 빨간 후광 원본 위치
    public const string GoldHaloPath = "Assets/Art/Enemy/Projectiles/gold-halo.png"; // 반사 가능 탄환 주변의 부드러운 금색 후광 원본 위치
    public const string CircleWarningPath = "Assets/Prefabs/Enemy/Warnings/CircleWarning.prefab"; // 원·타원으로 늘려 사용할 순수 예고 표시 위치
    public const string RectangleWarningPath = "Assets/Prefabs/Enemy/Warnings/RectangleWarning.prefab"; // 정사각형·직사각형 순수 예고 표시 위치
    public const string SectorWarningPath = "Assets/Prefabs/Enemy/Warnings/SectorWarning.prefab"; // 각도를 조절할 부채꼴 순수 예고 표시 위치
    public const string StraightWarningPath = "Assets/Prefabs/Enemy/Warnings/StraightWarning.prefab"; // 양끝이 열린 직선 띠 순수 예고 표시 위치
    private const string MaterialPath = "Assets/Art/Environment/Laboratory/LaboratoryUnlit.mat"; // 기존 URP 비조명 재질을 재사용할 위치

    /// <summary>이미 제작한 PNG를 가져오고 기존 GUID와 더미의 프리팹 참조를 보존</summary>
    [MenuItem("Tools/Chrono/Prepare Boss Hazard Art")]
    public static void Run()
    {
        // 새 사용 위치만 준비하며 맵과 다른 보스 시험 자산은 수정하지 않음
        Directory.CreateDirectory("Assets/Prefabs/Enemy");
        AssetDatabase.Refresh();
        Sprite sprite = ImportBulletSprite(SpritePath);
        Sprite goldSprite = ImportBulletSprite(GoldSpritePath);
        Sprite normalHalo = ImportBulletSprite(NormalHaloPath, FilterMode.Bilinear);
        Sprite goldHalo = ImportBulletSprite(GoldHaloPath, FilterMode.Bilinear);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (sprite == null || goldSprite == null || normalHalo == null || goldHalo == null
            || material == null || material.shader == null)
            throw new InvalidOperationException("The pixel Sprites and existing URP unlit material are required.");

        // 기존 시험 프리팹을 직접 보완해 더미가 가지고 있던 참조를 그대로 유지
        PrepareBullet<NormalProjectile>("Assets/Prefabs/BossTests/Normal.prefab", sprite, normalHalo, material);
        PrepareBullet<GoldProjectile>("Assets/Prefabs/BossTests/Gold.prefab", goldSprite, goldHalo, material);
        PrepareArea("Assets/Prefabs/BossTests/Area.prefab", material, 0.5f);
        PrepareBullet<NormalProjectile>(BulletPath, sprite, normalHalo, material);
        PrepareBullet<GoldProjectile>(GoldPath, goldSprite, goldHalo, material);
        PrepareArea(AreaPath, material, 0.8f);
        PrepareWarningShapes(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossHazardArtSetup] Expanded normal/gold pixel bullets and translucent filled circular warning prefabs prepared.");
    }

    /// <summary>피해 판정 없이 모양과 크기만 재사용할 네 종류의 예고 프리팹 준비</summary>
    private static void PrepareWarningShapes(Material material)
    {
        // 공유 단위 도형은 에셋으로 한 번 만들고 모든 순수 표시에서 재사용
        Directory.CreateDirectory("Assets/Art/Enemy/Warnings");
        Directory.CreateDirectory("Assets/Prefabs/Enemy/Warnings");
        AssetDatabase.Refresh();
        Mesh circle = PrepareWarningMesh("Assets/Art/Enemy/Warnings/Circle.asset", true);
        Mesh rectangle = PrepareWarningMesh("Assets/Art/Enemy/Warnings/Rectangle.asset", false);
        PrepareWarning(CircleWarningPath, WarningShape.Circle, circle, rectangle, material);
        PrepareWarning(RectangleWarningPath, WarningShape.Rectangle, circle, rectangle, material);
        PrepareWarning(SectorWarningPath, WarningShape.Sector, circle, rectangle, material);
        PrepareWarning(StraightWarningPath, WarningShape.OpenStrip, circle, rectangle, material);
    }

    /// <summary>중심 기준 가로·세로 1의 원판 또는 사각형 공유 메시 준비</summary>
    private static Mesh PrepareWarningMesh(string path, bool circle)
    {
        // 이미 저장한 단위 도형은 GUID와 참조를 유지
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        int count = circle ? 96 : 4;
        var vertices = new Vector3[circle ? count + 1 : count];
        var colors = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[circle ? count * 3 : 6];
        if (circle)
        {
            // 지름 1의 원판을 같은 중심의 삼각형 부채로 구성
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.5f;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % count + 1;
            }
        }
        else
        {
            // 양끝 열린 띠도 같은 사각 채움을 재사용하며 테두리만 별도 표시
            vertices[0] = new Vector3(-0.5f, -0.5f, 0f);
            vertices[1] = new Vector3(0.5f, -0.5f, 0f);
            vertices[2] = new Vector3(0.5f, 0.5f, 0f);
            vertices[3] = new Vector3(-0.5f, 0.5f, 0f);
            triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }
        for (int i = 0; i < vertices.Length; i++)
        {
            colors[i] = Color.white;
            uv[i] = (Vector2)vertices[i] + Vector2.one * 0.5f;
        }
        Mesh mesh = new Mesh { name = circle ? "WarningUnitCircle" : "WarningUnitRectangle", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>공유 채움·공유 재질과 모양별 경계선만 가진 표시 프리팹 저장</summary>
    private static void PrepareWarning(string path, WarningShape shape, Mesh circle, Mesh rectangle, Material material)
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            // root 크기 1에서 시작해 이후 x·y Transform 크기로만 표시 범위 확대
            root.transform.localScale = Vector3.one;
            LineRenderer primary = root.GetComponent<LineRenderer>();
            if (primary == null) primary = root.AddComponent<LineRenderer>();
            PrepareWarningLine(primary, material);
            Transform fill = root.transform.Find("Fill");
            if (fill == null)
            {
                fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                fill.SetParent(root.transform, false);
            }
            fill.localPosition = Vector3.zero;
            fill.localScale = Vector3.one;
            MeshRenderer fillRenderer = fill.GetComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = material;
            fillRenderer.sortingOrder = primary.sortingOrder - 1;
            AreaWarningVfx visual = root.GetComponent<AreaWarningVfx>();
            if (visual == null) visual = root.AddComponent<AreaWarningVfx>();
            visual.CircleMesh = circle;
            visual.RectangleMesh = rectangle;
            visual.FillRenderer = fillRenderer;
            visual.FillOpacityMultiplier = 0.45f;
            visual.OutlineWidth = 0.04f;
            if (shape == WarningShape.OpenStrip)
            {
                Transform second = root.transform.Find("SecondaryOutline");
                if (second == null)
                {
                    second = new GameObject("SecondaryOutline", typeof(LineRenderer)).transform;
                    second.SetParent(root.transform, false);
                }
                visual.SecondaryOutline = second.GetComponent<LineRenderer>();
                PrepareWarningLine(visual.SecondaryOutline, material);
            }
            visual.Configure(shape, new Color(1f, 0.12f, 0.16f, 0.45f), 90f);

            // 부채꼴의 가변 메시만 생성 수명에 속하므로 임시 참조를 프리팹에 저장하지 않음
            if (shape == WarningShape.Sector) fill.GetComponent<MeshFilter>().sharedMesh = null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>모든 예고의 선이 같은 재질과 로컬 지면 좌표를 사용하도록 준비</summary>
    private static void PrepareWarningLine(LineRenderer line, Material material)
    {
        // 실제 모양과 닫힘 여부는 구체 표시 Configure에서 설정
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = 1f;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.sortingOrder = 5;
    }

    /// <summary>같은 규격의 일반·금색 PNG를 보간 없는 자연 픽셀 Sprite로 준비</summary>
    private static Sprite ImportBulletSprite(string path, FilterMode filter = FilterMode.Point)
    {
        // 두 탄환의 형식과 피벗을 같은 가져오기 규칙으로 유지
        if (!File.Exists(path)) throw new FileNotFoundException("The pixel bullet PNG must be prepared first.", path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = filter;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>기존 일반·금색 탄환의 구체 타입을 유지하며 Sprite와 실제 판정 크기 연결</summary>
    private static void PrepareBullet<T>(string path, Sprite sprite, Sprite halo, Material material) where T : NormalProjectile
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(typeof(T) == typeof(GoldProjectile) ? "GoldBullet" : "NormalBullet");
        try
        {
            // 원본 색이 표현되도록 흰색 배율과 자연 크기를 사용
            root.transform.localScale = Vector3.one;
            CircleCollider2D shape = root.GetComponent<CircleCollider2D>();
            if (shape == null) shape = root.AddComponent<CircleCollider2D>();
            shape.isTrigger = true;
            shape.radius = 0.39f;
            shape.offset = Vector2.zero;
            T projectile = root.GetComponent<T>();
            if (projectile == null) projectile = root.AddComponent<T>();
            projectile.CollisionRadius = shape.radius;
            projectile.GroggyGain = 25f;
            Hitbox hitbox = root.GetComponent<Hitbox>();
            if (hitbox == null) hitbox = root.AddComponent<Hitbox>();
            hitbox.TargetKind = TargetKind.Projectile;
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = 30;

            // 본체와 판정은 고정하고 같은 크기의 색 변형과 뒤쪽 후광만 표시
            SpriteRenderer haloRenderer = PrepareBulletLayer(root.transform, "Halo", halo, material, 29);
            Transform previousRing = root.transform.Find("InteractionRing");
            if (previousRing != null) UnityEngine.Object.DestroyImmediate(previousRing.gameObject);
            ProjectileVfx visual = root.GetComponent<ProjectileVfx>();
            if (visual == null) visual = root.AddComponent<ProjectileVfx>();
            visual.Halo = haloRenderer;
            visual.MinimumOpacity = 0.7f;
            visual.PulseFrequency = 1f;
            visual.ResetPulse();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>본체와 같은 원점·배율의 후광 Sprite 자식 준비</summary>
    private static SpriteRenderer PrepareBulletLayer(Transform parent, string name, Sprite sprite, Material material, int order)
    {
        // 실제 색은 PNG에 두고 렌더러 배율과 공용 재질은 모든 탄환에서 재사용
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(SpriteRenderer)).transform;
            child.SetParent(parent, false);
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.color = Color.white;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>같은 반경의 Collider와 옅은 빨간 채움 및 진한 원형선 연결</summary>
    private static void PrepareArea(string path, Material material, float telegraphDuration)
    {
        // 프리팹과 런타임이 함께 참조할 단위 반경 원판을 에셋으로 한 번 저장
        const int segments = 96;
        const string meshPath = "Assets/Art/Enemy/Projectiles/CircleFill.asset";
        Mesh fillMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (fillMesh == null)
        {
            var vertices = new Vector3[segments + 1];
            var colors = new Color[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            colors[0] = Color.white;
            uv[0] = Vector2.one * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                colors[i + 1] = Color.white;
                uv[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].y) * 0.5f + Vector2.one * 0.5f;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            fillMesh = new Mesh { name = "CircleFill", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            fillMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(fillMesh, meshPath);
        }

        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject("WarningArea");
        try
        {
            // 픽셀 Sprite 채움을 공유 원판으로 바꾸고 실제 원형 판정 유지
            root.transform.localScale = Vector3.one;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                UnityEngine.Object.DestroyImmediate(renderer);
            CircleCollider2D shape = root.GetComponent<CircleCollider2D>();
            if (shape == null) shape = root.AddComponent<CircleCollider2D>();
            shape.radius = 1f;
            shape.offset = Vector2.zero;
            shape.isTrigger = true;
            AreaProjectile area = root.GetComponent<AreaProjectile>();
            if (area == null) area = root.AddComponent<AreaProjectile>();
            area.Radius = 1f;
            area.TelegraphDuration = telegraphDuration;
            area.TelegraphColor = new Color(1f, 0.12f, 0.16f, 0.45f);
            area.ActiveColor = new Color(1f, 0.12f, 0.16f, 0.8f);
            area.GroggyGain = 25f;
            Hitbox hitbox = root.GetComponent<Hitbox>();
            if (hitbox == null) hitbox = root.AddComponent<Hitbox>();
            hitbox.TargetKind = TargetKind.Projectile;
            LineRenderer line = root.GetComponent<LineRenderer>();
            if (line == null) line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 1f;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.sortingOrder = 5;
            line.alignment = LineAlignment.TransformZ;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;

            // 내부는 같은 원의 단위 메시를 공유하며 선보다 한 단계 아래에 표시
            Transform fill = root.transform.Find("Fill");
            if (fill == null)
            {
                fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                fill.SetParent(root.transform, false);
            }
            fill.localPosition = Vector3.zero;
            fill.localRotation = Quaternion.identity;
            MeshFilter filter = fill.GetComponent<MeshFilter>();
            MeshRenderer fillRenderer = fill.GetComponent<MeshRenderer>();
            filter.sharedMesh = fillMesh;
            fillRenderer.sharedMaterial = material;
            CircleWarningVfx visual = root.GetComponent<CircleWarningVfx>();
            if (visual == null) visual = root.AddComponent<CircleWarningVfx>();
            visual.FillRenderer = fillRenderer;
            visual.FillOpacityMultiplier = 0.45f;
            visual.Configure(area.Radius, area.TelegraphColor);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
