// Resources/SeriaMotion (방어·엎드리기 프레임), Resources/SeriaFX (방패 이펙트) 자동 설정
using System.IO;
using UnityEditor;
using UnityEngine;

public class SeriaMotionImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool motion = p.Contains("/Seria/Resources/SeriaMotion/");
        bool fx = p.Contains("/Seria/Resources/SeriaFX/");
        if (p.Contains("/Seria/Resources/SeriaUI/"))
        {
            var ui = (TextureImporter)assetImporter;
            ui.textureType = TextureImporterType.Sprite; ui.spriteImportMode = SpriteImportMode.Single;
            ui.alphaIsTransparency = true; ui.mipmapEnabled = false;
            return;
        }
        if (!motion && !fx) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;

        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];

        int cw, n; Vector2 pivot;
        if (motion) { cw = 256; n = w / cw; ti.spritePixelsPerUnit = 100; pivot = new Vector2(0.5f, 8f / 160f); }   // 기존 세리아 시트와 같은 규격
        else { n = 6; cw = w / n; ti.spritePixelsPerUnit = 85; pivot = new Vector2(0.5f, 0.5f); }

        string file = Path.GetFileNameWithoutExtension(p);
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}
