// Assets/Backgrounds 안의 그림을 배경용 스프라이트로 자동 설정
using UnityEditor;
using UnityEngine;

public class BackgroundImporter : AssetPostprocessor
{
    public const float PPU = 92.4f;   // 원경 그림 높이(924px) = 화면 높이 10유닛

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Backgrounds/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        ti.wrapMode = p.EndsWith("_Ground.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // 타일 반복에 필요
        ti.SetTextureSettings(settings);
    }
}
