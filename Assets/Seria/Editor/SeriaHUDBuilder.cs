// 메뉴: Tools > Seria > HUD 만들기 (체력바 + 스킬 아이콘)
using UnityEditor;
using UnityEngine;

public static class SeriaHUDBuilder
{
    [MenuItem("Tools/Seria/HUD 만들기 (Build HUD)")]
    public static void BuildHUD()
    {
        var player = GameObject.Find("Seria");
        if (player == null) { Debug.LogError("씬에 'Seria' 오브젝트가 없습니다. 먼저 Tools > Seria > 애니메이션 만들기 를 실행하세요."); return; }
        if (player.GetComponent<PlayerHealth>() == null) Undo.AddComponent<PlayerHealth>(player);

        var old = GameObject.Find("HUD");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var hudGo = new GameObject("HUD");
        Undo.RegisterCreatedObjectUndo(hudGo, "Build HUD");
        var hud = hudGo.AddComponent<SeriaHUD>();
        hud.player = player.GetComponent<SeriaController>();
        hud.health = player.GetComponent<PlayerHealth>();
        hud.skillIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Seria/UI/Seria_SkillIcon.png");
        if (hud.skillIcon == null) Debug.LogWarning("스킬 아이콘을 찾을 수 없습니다: Assets/Seria/UI/Seria_SkillIcon.png");

        Selection.activeGameObject = hudGo;
        Debug.Log("HUD 생성 완료! Play 하면 좌측 하단에 체력바와 스킬 아이콘이 나타납니다.");
    }
}
