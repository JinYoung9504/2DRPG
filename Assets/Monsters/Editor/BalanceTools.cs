// 메뉴: Tools > Monsters > 새 능력치 일괄 적용
//  이미 맵에 배치된 몬스터 · 소환수 프리팹에 새 능력치를 한 번에 적용
//   네크로맨서 체력 250 / 경험치 150
//   대형 마수 늑대 체력 300 / 경험치 200
//   타락한 거대 나무정령 체력 500 / 경험치 300
//   뱀파이어 체력 900
//   아타칸 체력 3000 / 대검 20% / 검기 50% / 소환 무작위 2종
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BalanceTools
{
    [MenuItem("Tools/Monsters/새 능력치 일괄 적용")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string first = EditorSceneManager.GetActiveScene().path;
        int count = 0;

        // 1) 모든 맵(빌드 목록)의 배치된 몬스터
        foreach (var bs in EditorBuildSettings.scenes)
        {
            if (!System.IO.File.Exists(bs.path)) continue;
            var scene = EditorSceneManager.OpenScene(bs.path);
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var eh in root.GetComponentsInChildren<EnemyHealth>(true)) if (Fix(eh.gameObject)) n++;
            if (n > 0) { EditorSceneManager.SaveScene(scene); count += n; }
        }

        // 2) 소환수 프리팹 (뱀파이어가 부르는 네크로맨서, 아타칸이 부르는 보스들)
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Monsters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var eh in root.GetComponentsInChildren<EnemyHealth>(true)) changed |= Fix(eh.gameObject);
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); count++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (!string.IsNullOrEmpty(first)) EditorSceneManager.OpenScene(first);
        EditorUtility.DisplayDialog("새 능력치 적용", $"몬스터 {count}곳에 새 능력치를 적용했습니다.", "확인");
    }

    static bool Fix(GameObject g)
    {
        var hp = g.GetComponent<EnemyHealth>(); if (hp == null) return false;
        bool ch = false;
        var n = g.GetComponent<NecromancerEnemy>();
        if (n != null) { hp.maxHP = 250f; if (n.expReward > 0) n.expReward = 150; ch = true; }
        var w = g.GetComponent<WolfBoss>();
        if (w != null) { hp.maxHP = 300f; w.expReward = 200; ch = true; }
        var t = g.GetComponent<TreeBossEnemy>();
        if (t != null) { hp.maxHP = 500f; t.expReward = 300; ch = true; }
        var v = g.GetComponent<VampireBoss>();
        if (v != null) { hp.maxHP = 900f; ch = true; }
        var a = g.GetComponent<AtakhanBoss>();
        if (a != null) { hp.maxHP = 3000f; a.swingRatio = 0.2f; a.waveRatio = 0.5f; a.summonKinds = 2; ch = true; }
        if (ch) { EditorUtility.SetDirty(hp); foreach (var mb in g.GetComponents<MonoBehaviour>()) EditorUtility.SetDirty(mb); }
        return ch;
    }
}
