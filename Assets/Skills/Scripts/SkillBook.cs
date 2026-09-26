// 배운 스킬 목록 (마을 NPC 에게 배움)
//  - 새 게임 시작 시 초기화, 배우는 즉시 저장(PlayerPrefs) → 이어하기에서도 유지
//  - 스킬 구분: 단축키 이름 (A / S / D / F)
using System.Collections.Generic;
using UnityEngine;

public static class SkillBook
{
    const string Key = "Seria_LearnedSkills";
    static HashSet<string> learned;
    public static event System.Action OnChanged;

    static HashSet<string> Set
    {
        get
        {
            if (learned == null)
            {
                learned = new HashSet<string>();
                foreach (var s in PlayerPrefs.GetString(Key, "").Split(',')) if (s.Length > 0) learned.Add(s);
            }
            return learned;
        }
    }

    public static bool Has(string id) => Set.Contains(id);

    public static void Learn(string id)
    {
        if (!Set.Add(id)) return;
        PlayerPrefs.SetString(Key, string.Join(",", Set)); PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void Reset()                // 새 게임
    {
        Set.Clear(); PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
