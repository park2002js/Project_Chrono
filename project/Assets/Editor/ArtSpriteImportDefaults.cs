using System;
using UnityEditor;
using UnityEditor.U2D.Aseprite;

/// <summary>게임 아트 폴더의 새 스프라이트에 기본 픽셀 배율 적용</summary>
public class ArtSpriteImportDefaults : AssetPostprocessor
{
    internal const float PixelsPerUnit = 16f; // 게임 월드 1유닛에 표시할 원본 픽셀 수

    /// <summary>기존 임포트 설정이 없는 스프라이트의 픽셀 배율 초기화</summary>
    private void OnPreprocessAsset()
    {
        // 임포터가 처음 만들어질 때만 게임용 아트의 기본값 설정
        if (!assetPath.StartsWith("Assets/Art/", StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
        if (assetImporter is AsepriteImporter aseprite)
            aseprite.spritePixelsPerUnit = PixelsPerUnit;
        else if (assetImporter is TextureImporter texture && texture.textureType == TextureImporterType.Sprite)
            texture.spritePixelsPerUnit = PixelsPerUnit;
    }
}
