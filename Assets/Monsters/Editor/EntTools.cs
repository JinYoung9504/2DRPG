// 나무 정령 스프라이트 자동 설정 + 애니메이터 생성
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class EntImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Ent/Ent_"), res = p.Contains("/Monsters/Resources/Ent/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        string file = Path.GetFileNameWithoutExtension(p);
        if (file == "Ent_Branch")
        {
            ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 45;
            var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
            set.spriteAlignment = (int)SpriteAlignment.Custom; set.spritePivot = new Vector2(0f, 0.5f);   // 뿌리 쪽 기준
            ti.SetTextureSettings(set); return;
        }
        int cw, ch, n; float ppu; Vector2 pivot;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        if (file == "Ent_Impact") { n = 8; cw = w / n; ch = h; ppu = 35; pivot = new Vector2(0.5f, 0.05f); }
        else { cw = 440; ch = 330; n = w / cw; ppu = 100; pivot = new Vector2(0.5f, 8f / 330f); }
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, ch), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class EntTools
{
    const string Dir = "Assets/Monsters/Ent/";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Ent_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Ent_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Ent.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", 8, true);
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", 12, false);
        var death = sm.AddState("Death"); death.motion = Clip("Death", 8, false);
        var st = sm.AddState("AttackStart"); st.motion = Clip("AttackStart", 10, false);
        var en = sm.AddState("AttackEnd"); en.motion = Clip("AttackEnd", 10, false);
        sm.defaultState = idle;
        var t = hit.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject CreateEnt(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Tree Ent");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Ent_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;   // 고정형 (움직이지 않음)
        var body = go.AddComponent<BoxCollider2D>(); body.size = new Vector2(1.8f, 2.8f); body.offset = new Vector2(0, 1.4f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(1.7f, 2.6f); hit.offset = new Vector2(0, 1.3f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 80f; hp.hpBarOffset = new Vector2(0, 3.4f); hp.deathAnimTime = 1.1f;
        var te = go.AddComponent<TreeEntEnemy>(); te.branchDamage = 30f; te.touchDamage = 15f; te.expReward = 40;
        Undo.RegisterCreatedObjectUndo(go, "Create Tree Ent");
        return go;
    }

    public static void SpawnAt(float[] xs)
    {
#if UNITY_2023_1_OR_NEWER
        var olds = Object.FindObjectsByType<TreeEntEnemy>(FindObjectsSortMode.None);
#else
        var olds = Object.FindObjectsOfType<TreeEntEnemy>();
#endif
        foreach (var o in olds) Undo.DestroyObjectImmediate(o.gameObject);
        var ctrl = BuildController();
        foreach (var x in xs) CreateEnt(new Vector3(x, FeetY, 0), ctrl);
    }

    [MenuItem("Tools/Monsters/선택 위치에 나무 정령 1마리 추가")]
    public static void AddOne()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Ent.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = CreateEnt(new Vector3(view != null ? view.pivot.x : 0f, FeetY, 0), ctrl);
    }
}
