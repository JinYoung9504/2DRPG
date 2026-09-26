// Resources/Skills 안의 그림 자동 설정 (이펙트 시트는 프레임별로 자르기)
using System.IO;
using UnityEditor;
using UnityEngine;

public class SkillFXImporter : AssetPostprocessor
{
    // 이름: (프레임 수, Pixels Per Unit, 피벗)
    static bool Info(string file, out int frames, out float ppu, out Vector2 pivot)
    {
        frames = 0; ppu = 100; pivot = new Vector2(0.5f, 0.5f);
        switch (file)
        {
            case "FX_SwordWave": frames = 9; ppu = 80; pivot = new Vector2(0.5f, 0.5f); return true;    // 가운데 기준
            case "FX_Lightning": frames = 8; ppu = 42; pivot = new Vector2(0.5f, 0.03f); return true;   // 바닥 기준 (높이 약 6유닛)
            case "FX_Meteor":    frames = 8; ppu = 70; pivot = new Vector2(0.5f, 0.03f); return true;
            case "FX_MeteorSky": frames = 2; ppu = 70; pivot = new Vector2(0.5f, 0.03f); return true;     // 하늘 마법진
            case "FX_SwordRainCircle": frames = 3; ppu = 45; pivot = new Vector2(0.5f, 0.5f); return true;   // 하늘 마법진 (열리는 3단계)
            case "FX_SwordRainBlade":  frames = 4; ppu = 120; pivot = new Vector2(0.5f, 0.02f); return true; // 떨어지는 검 (칼끝 기준)
            case "FX_SwordRainImpact": frames = 1; ppu = 90; pivot = new Vector2(0.5f, 0.1f); return true;   // 검이 꽂힐 때 빛
            case "FX_BlinkTrail": frames = 8; ppu = 90; pivot = new Vector2(1f, 0.5f); return true;     // 섬광보 궤적 (도착 지점 기준)
            case "FX_BlinkStart": frames = 1; ppu = 190; pivot = new Vector2(0.5f, 0.5f); return true;   // 출발 지점 빛
            case "FX_BlinkEnd":   frames = 1; ppu = 170; pivot = new Vector2(0.5f, 0.5f); return true;   // 도착 지점 빛
            case "FX_MeteorSmall": frames = 1; ppu = 37; pivot = new Vector2(0.475f, 0.04f); return true; // 작은 돌 (돌 아래쪽 기준)
        }
        return false;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Skills/Resources/Skills/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = FilterMode.Bilinear;

        string file = Path.GetFileNameWithoutExtension(p);
        if (!Info(file, out int n, out float ppu, out Vector2 pivot)) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100; return; }

        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = ppu;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw = w / n;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}
