// 네크로맨서: 스프라이트 자동 설정 + 애니메이터 + 소환수 프리팹 + 성곽3 맵 만들기/배치
//  메뉴: Tools > Monsters > 성곽3 만들기 + 네크로맨서 배치
//        Tools > Monsters > 선택 위치에 네크로맨서 1마리 추가
//  (스켈레톤 검사 · 궁수 업데이트가 먼저 적용되어 있어야 함)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NecromancerImporter : AssetPostprocessor
{
    public const float PPU = 125f;     // 키 약 1.4유닛
    const int Foot = 8;

    static int Count(string file)
    {
        switch (file)
        {
            case "Necro_Idle": return 5;
            case "Necro_Walk": return 7;
            case "Necro_Attack": return 5;
            case "Necro_Summon": return 7;
            case "Necro_Death": return 5;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Necro/Necro_"), fx = p.Contains("/Monsters/Resources/Necro/");
        bool portrait = p.EndsWith("/Portraits/Portrait_Necromancer.png");
        if (!sheet && !fx && !portrait) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        if (portrait) { ti.spriteImportMode = SpriteImportMode.Single; return; }
        if (fx)
        {
            ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100;
            var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
            set.spriteAlignment = (int)(p.EndsWith("Necro_SummonFx.png") ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(set); return;
        }
        string file = Path.GetFileNameWithoutExtension(p);
        int n = Count(file); if (n == 0) return;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw = w / n;
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = PPU;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, (float)Foot / h) };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class NecromancerTools
{
    const string Dir = "Assets/Monsters/Necro/";
    const string Map9 = "Assets/Scenes/Map9_RuinedCastle2.unity";
    const string Map10 = "Assets/Scenes/Map10_RuinedCastle3.unity";
    const string TalkDir = "Assets/Monsters/Resources/MonsterTalk";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Necro_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Necro_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController GetController()
    {
        string path = Dir + "Necromancer.controller";
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (old != null) return old;
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 5, true, new[] { 0, 1, 2, 3, 4, 3, 2, 1 });
        sm.AddState("Walk").motion = Clip("Walk", "Walk", 9, true);
        sm.AddState("Attack").motion = Clip("Attack", "Attack", 12, false);
        sm.AddState("Summon").motion = Clip("Summon", "Summon", 10, false);
        sm.AddState("Death").motion = Clip("Death", "Death", 8, false);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // 소환수 프리팹 (기존 스켈레톤 검사·궁수 설정 그대로)
    static GameObject SummonPrefab(string file, System.Func<GameObject> make)
    {
        string path = Dir + file + ".prefab";
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (old != null) return old;
        var temp = make();
        if (temp == null) return null;
        temp.transform.position = Vector3.zero;
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    public static void GetSummons(out GameObject sword, out GameObject archer)
    {
        sword = SummonPrefab("Summon_SkeletonSoldier", () =>
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Monsters/Skeleton/Skeleton.controller");
            if (c == null) c = SkeletonTools.BuildController();
            return SkeletonTools.Create(Vector3.zero, c);
        });
        archer = SummonPrefab("Summon_SkeletonArcher", () =>
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Monsters/Archer/SkeletonArcher.controller");
            if (c == null) c = SkeletonArcherTools.BuildController();
            return SkeletonArcherTools.Create(Vector3.zero, c);
        });
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl, GameObject sword, GameObject archer)
    {
        var go = new GameObject("Necromancer");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Necro_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 3f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(0.8f, 1.35f); body.offset = new Vector2(0, 0.68f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(0.9f, 1.35f); hit.offset = new Vector2(0, 0.68f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 250f; hp.hpBarOffset = new Vector2(0, 1.75f); hp.deathAnimTime = 1.0f;
        var n = go.AddComponent<NecromancerEnemy>(); n.meleeDamage = 10f; n.expReward = 150;
        n.swordsmanPrefab = sword; n.archerPrefab = archer;
        Undo.RegisterCreatedObjectUndo(go, "Create Necromancer");
        return go;
    }

    static void EnsureTalk()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Monsters/Resources")) AssetDatabase.CreateFolder("Assets/Monsters", "Resources");
        if (!AssetDatabase.IsValidFolder(TalkDir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        string path = TalkDir + "/NecromancerEnemy.asset";
        if (AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path) != null) return;
        var d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "네크로맨서";
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Monsters/Resources/Portraits/Portrait_Necromancer.png");
        d.sightRange = 10f;
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Monsters/성곽3 만들기 + 네크로맨서 배치")]
    public static void BuildCastle3()
    {
        if (!File.Exists(Map9)) { EditorUtility.DisplayDialog("성곽3", "멸망한 왕국 성곽2(Map9_RuinedCastle2)가 없습니다.\n먼저 'Tools > Monsters > 성곽2 만들기 + 스켈레톤 궁수 배치'를 실행하세요.", "확인"); return; }
        bool exists = File.Exists(Map10);
        if (!EditorUtility.DisplayDialog("성곽3", exists
                ? "성곽3(Map10)이 이미 있습니다.\n네크로맨서만 다시 배치할까요?"
                : "성곽2 오른쪽 끝 → 멸망한 왕국 성곽3 (새 맵)\n네크로맨서를 배치합니다. 진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureTalk();
        var ctrl = GetController();

        if (!exists)
        {
            var s9 = EditorSceneManager.OpenScene(Map9);
            var st9 = Find<StageBounds>()[0];
            foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
            Portal("Portal_Right (→ 성곽3)", st9.right - 0.7f, "Map10_RuinedCastle3", "Left");
            EnsureSpawns(st9);
            EditorSceneManager.SaveScene(s9);

            EditorSceneManager.SaveScene(s9, Map10, true);
            var s10 = EditorSceneManager.OpenScene(Map10);
            foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
            foreach (var n in Find<NpcTalker>()) Object.DestroyImmediate(n.gameObject);
            foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
            var st10 = Find<StageBounds>()[0];
            Portal("Portal_Left (→ 성곽2)", st10.left + 0.7f, "Map9_RuinedCastle2", "Right");
            EnsureSpawns(st10);
            var seria = GameObject.Find("Seria"); if (seria != null) seria.transform.position = new Vector3(st10.left + 2f, FeetY + 0.05f, 0);
            EditorSceneManager.SaveScene(s10);

            var list = EditorBuildSettings.scenes.Where(s => s.path != Map10).ToList();
            int i = list.FindIndex(s => s.path == Map9);
            list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(Map10, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        var scene = EditorSceneManager.OpenScene(Map10);
        GetSummons(out var sword, out var archer);
        foreach (var o in Find<NecromancerEnemy>()) Object.DestroyImmediate(o.gameObject);
        var stage = Find<StageBounds>()[0];
        var xs = new[] { 0.5f, 0.85f }.Select(k => Mathf.Lerp(stage.left, stage.right, k)).ToArray();   // 2마리
        foreach (var x in xs) Create(new Vector3(x, FeetY, 0), ctrl, sword, archer);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("성곽3 완료", $"멸망한 왕국 성곽3(Map10)에 네크로맨서 {xs.Length}마리를 배치했습니다.\n(x = {string.Join(", ", xs.Select(x => x.ToString("0.#")))})\n\n체력 250 / 근접 공격 10 / 경험치 150\n멀리서 세리아를 보면 스켈레톤 검사 + 궁수 소환", "확인");
    }

    [MenuItem("Tools/Monsters/선택 위치에 네크로맨서 1마리 추가")]
    public static void AddOne()
    {
        EnsureTalk();
        var ctrl = GetController();
        GetSummons(out var sword, out var archer);
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = Create(new Vector3(view != null ? view.pivot.x : 0f, FeetY, 0), ctrl, sword, archer);
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
    static void EnsureSpawns(StageBounds stage)
    {
        foreach (var (n, x) in new[] { ("Spawn_Left", stage.left + 2f), ("Spawn_Right", stage.right - 2f) })
            if (GameObject.Find(n) == null) new GameObject(n).transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }
    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name); g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>(); p.targetScene = target; p.spawnSide = side;
    }
}
