// Resources/Settings 그림을 UI 스프라이트로 자동 설정
using UnityEditor;
using UnityEngine;

public class SettingsImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Settings/Resources/Settings/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Settings/Resources/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming;          // 긴 음악은 스트리밍 (메모리 절약)
        s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f;
        ai.defaultSampleSettings = s;
    }
}
