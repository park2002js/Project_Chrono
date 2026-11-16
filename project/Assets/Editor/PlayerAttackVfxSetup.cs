using System;
using UnityEditor;
using UnityEngine;

/// <summary>확정 검기 원본과 색별 합성 단계를 Player의 재사용 렌더러에 연결</summary>
public static class PlayerAttackVfxSetup
{
    private const string Folder = "Assets/Art/Weapons/AttackVfx"; // 원본 두 색과 확정 가림 단계의 투명 이미지 폴더
    private static readonly string[] CombinedNames = { "P1_B0", "P1_B1", "P2_B1", "P2_B2", "P3_B3", "P4_B4" }; // 3타에서 사용하는 보라·파랑 단계 조합 순서

    /// <summary>전체 이미지를 유지한 검기 Sprite와 Player 수명 동안 재사용할 표시 객체 준비</summary>
    public static void ApplyToPrefab(AttackController attack, SpriteRenderer weapon)
    {
        // 원본 단계마다 공통 크기·피벗·16PPU를 적용하고 색별 참조 확보
        attack.Attack1Vfx = LoadSequence("Attack1");
        attack.Attack2Vfx = LoadSequence("Attack2");
        attack.Attack3Vfx = new Sprite[CombinedNames.Length];
        for (int i = 0; i < CombinedNames.Length; i++)
            attack.Attack3Vfx[i] = LoadSprite(Folder + "/" + CombinedNames[i] + ".png");

        // 원작 Shoulder의 SwingPoint(+Y1)를 현재 +X 조준 부모 아래에 같은 거리로 배치
        Transform visual = attack.WeaponPivot.Find("AttackVfx");
        if (visual == null)
        {
            visual = new GameObject("AttackVfx").transform;
            visual.SetParent(attack.WeaponPivot, false);
        }
        visual.localPosition = new Vector3(1f, 0f, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, -90f);
        visual.localScale = Vector3.one;
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = weapon.sharedMaterial;
        renderer.sortingLayerID = weapon.sortingLayerID;
        renderer.sortingOrder = 21;
        renderer.color = Color.white;
        renderer.sprite = attack.Attack1Vfx[0];
        renderer.flipX = false;
        renderer.flipY = false;
        renderer.enabled = false;
        attack.AttackVfxRenderer = renderer;
        attack.WeaponRenderer = weapon;
    }

    /// <summary>한 색의 네 원본 단계를 순서대로 확보</summary>
    private static Sprite[] LoadSequence(string prefix)
    {
        var sprites = new Sprite[4];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = LoadSprite(Folder + "/" + prefix + "_" + (i + 1).ToString("00") + ".png");
        return sprites;
    }

    /// <summary>70×66 전체 여백과 중심을 유지해 자르지 않은 단일 Sprite로 가져오기</summary>
    private static Sprite LoadSprite(string path)
    {
        // 확대·아틀라스 잘림 없이 검기 원본 픽셀과 공통 중심 유지
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing attack VFX: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null || sprite.rect.width != 70f || sprite.rect.height != 66f)
            throw new InvalidOperationException("Attack VFX must keep its 70x66 canvas: " + path);
        return sprite;
    }
}
