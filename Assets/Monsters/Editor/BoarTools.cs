// 멧돼지 스프라이트 자동 설정 + 메뉴: Tools > Monsters > 멧돼지 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class BoarImporter : AssetPostprocessor
{
    public const int CellW = 256, CellH = 160;
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Monsters/Boar/Boar_")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 100;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(assetPath)) fs.Read(head, 0, 24);
        int width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        int count = width / CellW;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = $"{baseName}_{i}", rect = new Rect(i * CellW, 0, CellW, CellH),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 8f / CellH)
            };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class BoarTools
{
    const string Dir = "Assets/Monsters/Boar/";
    const float FeetY = -4.0f;

    static AnimationClip MakeClip(string name, float fps)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Boar_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Boar_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Boar.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = MakeClip("Idle", 5);
        var walk = sm.AddState("Walk"); walk.motion = MakeClip("Walk", 10);
        sm.defaultState = idle;
        var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.duration = 0; t1.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
        var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.duration = 0; t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject CreateBoar(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Boar");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Boar_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 3f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>();
        body.direction = CapsuleDirection2D.Horizontal; body.size = new Vector2(1.6f, 1.0f); body.offset = new Vector2(0, 0.5f);
        var hit = go.AddComponent<BoxCollider2D>();
        hit.isTrigger = true; hit.size = new Vector2(1.5f, 0.9f); hit.offset = new Vector2(0, 0.5f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 15f; hp.hpBarOffset = new Vector2(0, 1.55f);
        go.AddComponent<BoarEnemy>();
        Undo.RegisterCreatedObjectUndo(go, "Create Boar");
        return go;
    }

    [MenuItem("Tools/Monsters/멧돼지 배치 (Spawn Boars)")]
    public static void SpawnBoars() => SpawnAt(new[] { -12f, -22f });

    public static void SpawnAt(float[] xs)
    {
#if UNITY_2023_1_OR_NEWER
        var olds = Object.FindObjectsByType<BoarEnemy>(FindObjectsSortMode.None);
#else
        var olds = Object.FindObjectsOfType<BoarEnemy>();
#endif
        foreach (var o in olds) Undo.DestroyObjectImmediate(o.gameObject);
        var ctrl = BuildController();
        foreach (var x in xs)
            CreateBoar(new Vector3(x, FeetY + 0.05f, 0), ctrl);
        Debug.Log($"멧돼지 {xs.Length}마리 배치 완료. Scene 창에서 끌어서 위치를 바꿀 수 있습니다.");
    }

    [MenuItem("Tools/Monsters/선택 위치에 멧돼지 1마리 추가")]
    public static void AddOne()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Boar.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = CreateBoar(new Vector3(view != null ? view.pivot.x : 0f, FeetY + 0.05f, 0), ctrl);
    }
}
