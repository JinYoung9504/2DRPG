// 독버섯 스프라이트 자동 설정 + 애니메이터 + 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class MushroomImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Mushroom/Mush_"), res = p.Contains("/Monsters/Resources/Mushroom/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        string file = Path.GetFileNameWithoutExtension(p);
        if (file == "Mush_GasCloud") { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 55; return; }
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw, ch, n; float ppu; Vector2 pivot;
        if (file == "Mush_PoisonIcon") { n = 8; cw = w / n; ch = h; ppu = 170; pivot = new Vector2(0.5f, 0.5f); }
        else { cw = 200; ch = 160; n = w / cw; ppu = 100; pivot = new Vector2(0.5f, 8f / 160f); }
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, ch), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class MushroomTools
{
    const string Dir = "Assets/Monsters/Mushroom/";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string name, float fps, bool loop)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Mush_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Mush_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Mushroom.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", 8, true);
        var jump = sm.AddState("Jump"); jump.motion = Clip("Jump", 12, false);
        var land = sm.AddState("Land"); land.motion = Clip("Land", 16, false);
        var atk = sm.AddState("Attack"); atk.motion = Clip("Attack", 10, false);
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", 16, false);
        var death = sm.AddState("Death"); death.motion = Clip("Death", 8, false);
        sm.defaultState = idle;
        foreach (var s in new[] { land, hit }) { var t = s.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0; }
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Mushroom");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Mush_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.direction = CapsuleDirection2D.Horizontal; body.size = new Vector2(0.9f, 0.9f); body.offset = new Vector2(0, 0.45f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(0.8f, 0.8f); hit.offset = new Vector2(0, 0.42f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 25f; hp.hpBarOffset = new Vector2(0, 1.3f); hp.deathAnimTime = 0.6f;
        go.AddComponent<MushroomEnemy>();
        Undo.RegisterCreatedObjectUndo(go, "Create Mushroom");
        return go;
    }

    public static void SpawnAt(float[] xs)
    {
        var ctrl = BuildController();
        foreach (var x in xs) Create(new Vector3(x, FeetY + 0.05f, 0), ctrl);
    }

    [MenuItem("Tools/Monsters/선택 위치에 독버섯 1마리 추가")]
    public static void AddOne()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Mushroom.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = Create(new Vector3(view != null ? view.pivot.x : 0f, FeetY + 0.05f, 0), ctrl);
    }
}
