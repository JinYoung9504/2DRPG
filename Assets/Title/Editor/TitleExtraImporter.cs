// 타이틀 '게임 종료' 버튼 그림을 UI 스프라이트로 자동 설정
using UnityEditor;

public class TitleExtraImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').EndsWith("/Title/Resources/Btn_Quit.png")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
