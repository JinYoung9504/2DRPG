// 메뉴: Tools > Monsters > 몬스터 대사 파일 만들기
//  몬스터 종류별 대사 파일을 Assets/Monsters/Resources/MonsterTalk/ 에 만듦 (이미 있으면 그대로 둠)
using UnityEditor;
using UnityEngine;

public class MonsterPortraitImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Monsters/Resources/Portraits/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}

public static class MonsterTalkTools
{
    const string Dir = "Assets/Monsters/Resources/MonsterTalk";
    // (AI 스크립트 이름 = 파일 이름, 표시 이름, 얼굴 그림)
    static readonly (string key, string name, string portrait)[] Types =
    {
        ("SlimeEnemy", "슬라임", "Portrait_Slime"),
        ("BoarEnemy", "멧돼지", "Portrait_Boar"),
        ("MushroomEnemy", "독버섯", "Portrait_Mushroom"),
        ("GolemEnemy", "골렘", "Portrait_Golem"),
        ("TreeEntEnemy", "나무 정령", "Portrait_TreeEnt"),
        ("WolfBoss", "대형 마수 늑대", "Portrait_Wolf"),
    };

    [MenuItem("Tools/Monsters/몬스터 대사 파일 만들기")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        MonsterTalkData first = null; int made = 0;
        foreach (var t in Types)
        {
            string path = $"{Dir}/{t.key}.asset";
            var d = AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<MonsterTalkData>();
                d.monsterName = t.name;
                d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Monsters/Resources/Portraits/{t.portrait}.png");
                if (t.key == "WolfBoss")
                {
                    d.sightRange = 9f; d.oncePerGame = false;   // 보스는 만날 때마다
                    d.onSight = new[]
                    {
                        new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 크르르… 인간 주제에 이 숲에 발을 들이다니." },
                        new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 마을을 위협하는 건 여기까지야!" },
                    };
                }
                AssetDatabase.CreateAsset(d, path); made++;
            }
            if (first == null) first = d;
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(Dir);
        EditorUtility.DisplayDialog("몬스터 대사", $"대사 파일 {made}개를 만들었습니다.\n{Dir}\n\n대사를 비워두면 대화창이 나오지 않습니다.\n(늑대 보스에만 예시 대사가 들어 있습니다)", "확인");
    }
}
