// 슬라임 스프라이트 자동 설정 + 메뉴: Tools > Monsters > 슬라임 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class SlimeImporter : AssetPostprocessor
{
    public const int CellW = 160, CellH = 128;
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Monsters/Slime/Slime_")) return;
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
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 6f / CellH)
            };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class SlimeTools
{
    const string Dir = "Assets/Monsters/Slime/";
    const float FeetY = -4.0f;   // 마을 배경 바닥 높이

    static AnimationClip MakeClip(string name, float fps)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Slime_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Slime_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AnimatorController BuildController()
    {
        string path = Dir + "Slime.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = MakeClip("Idle", 8);
        var walk = sm.AddState("Walk"); walk.motion = MakeClip("Walk", 10);
        sm.defaultState = idle;
        var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.duration = 0; t1.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
        var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.duration = 0; t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject CreateSlime(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Slime");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Slime_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;   // 가만히 있어도 접촉 판정 유지
        var body = go.AddComponent<CapsuleCollider2D>();
        body.direction = CapsuleDirection2D.Horizontal; body.size = new Vector2(1.0f, 0.7f); body.offset = new Vector2(0, 0.35f);
        var hit = go.AddComponent<BoxCollider2D>();
        hit.isTrigger = true; hit.size = new Vector2(0.9f, 0.6f); hit.offset = new Vector2(0, 0.32f);
        go.AddComponent<SlimeEnemy>();
        Undo.RegisterCreatedObjectUndo(go, "Create Slime");
        return go;
    }

    [MenuItem("Tools/Monsters/슬라임 배치 (Spawn Slimes)")]
    public static void SpawnSlimes()
    {
        var ctrl = BuildController();
        foreach (var x in new[] { 8f, 15f, 22f })
            CreateSlime(new Vector3(x, FeetY + 0.05f, 0), ctrl);
        Debug.Log("슬라임 3마리 배치 완료 (x = 8, 15, 22). Scene 창에서 끌어서 위치를 바꿀 수 있습니다.");
    }

    [MenuItem("Tools/Monsters/선택 위치에 슬라임 1마리 추가")]
    public static void AddOne()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Slime.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        float x = view != null ? view.pivot.x : 0f;
        Selection.activeGameObject = CreateSlime(new Vector3(x, FeetY + 0.05f, 0), ctrl);
    }
}
