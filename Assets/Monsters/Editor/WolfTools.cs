// 대형 마수 늑대 스프라이트 자동 설정 + 애니메이터 + 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class WolfImporter : AssetPostprocessor
{
    public const int CellW = 560, CellH = 300;
    public const float PPU = 48f;           // 몸 높이 약 240px → 5유닛 (화면 세로의 절반)

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Wolf/Wolf_"), res = p.Contains("/Monsters/Resources/Wolf/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        string file = Path.GetFileNameWithoutExtension(p);
        if (res) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 65; return; }   // 파이어볼 약 3유닛
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int n = w / CellW;
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = PPU;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * CellW, 0, CellW, CellH), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 8f / CellH) };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class WolfTools
{
    const string Dir = "Assets/Monsters/Wolf/";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string name, float fps, bool loop)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Wolf_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Wolf_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Wolf.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", 8, true);
        var run = sm.AddState("Run"); run.motion = Clip("Run", 12, true);
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", 12, false);
        var claw = sm.AddState("Claw"); claw.motion = Clip("Claw", 14, false);
        var death = sm.AddState("Death"); death.motion = Clip("Death", 8, false);
        sm.defaultState = idle;
        var t = hit.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Crimson Wolf (Boss)");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Wolf_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1; sr.flipX = true;                      // 처음엔 왼쪽(플레이어 쪽)을 봄
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 20f; rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.direction = CapsuleDirection2D.Horizontal; body.size = new Vector2(4.2f, 3.4f); body.offset = new Vector2(0, 1.7f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(3.8f, 3.2f); hit.offset = new Vector2(0, 1.6f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 150f; hp.hpBarOffset = new Vector2(0, 5.4f); hp.deathAnimTime = 1.0f; hp.respawnTime = 30f;
        go.AddComponent<WolfBoss>();
        Undo.RegisterCreatedObjectUndo(go, "Create Wolf Boss");
        return go;
    }

    public static void SpawnAt(float x) => Create(new Vector3(x, FeetY + 0.05f, 0), BuildController());
}
