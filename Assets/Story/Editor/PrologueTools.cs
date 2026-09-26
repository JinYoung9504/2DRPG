// 메뉴: Tools > Story > 오프닝 대사 편집 열기
//  Assets/Story/Resources/Story/Prologue 파일을 만들고(없으면 예시 대사로) 선택해 줌
using UnityEditor;
using UnityEngine;

public static class PrologueTools
{
    const string Path = "Assets/Story/Resources/Story/Prologue.asset";

    [MenuItem("Tools/Story/오프닝 대사 편집 열기")]
    public static void Open()
    {
        var d = AssetDatabase.LoadAssetAtPath<PrologueData>(Path);
        if (d == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Story")) AssetDatabase.CreateFolder("Assets", "Story");
            if (!AssetDatabase.IsValidFolder("Assets/Story/Resources")) AssetDatabase.CreateFolder("Assets/Story", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Story/Resources/Story")) AssetDatabase.CreateFolder("Assets/Story/Resources", "Story");
            d = ScriptableObject.CreateInstance<PrologueData>();
            AssetDatabase.CreateAsset(d, Path); AssetDatabase.SaveAssets();
        }
        Selection.activeObject = d; EditorGUIUtility.PingObject(d);
    }
}
