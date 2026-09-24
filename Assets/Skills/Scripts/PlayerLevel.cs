// 레벨 / 경험치
//  필요 경험치: 1→2 : 3,  2→3 : 5,  3→4 : 10,  이후 20, 30, 40 ... (10씩 증가)
//  레벨업 시 체력 전부 회복, 최대 체력 +1% (반올림), 기본 공격력 +3
//  [테스트] 에디터에서 F1 = 레벨 +1, F2 = 레벨 -1, F3 = 20레벨, F4 = 경험치 +1
//           또는 Play 중 Inspector 의 Level 값을 직접 바꿔도 됩니다.
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerLevel : MonoBehaviour
{
    [Min(1)] public int level = 1;
    [Min(0)] public int exp = 0;
    public int maxLevel = 99;
    public bool healOnLevelUp = true;

    [Header("레벨업 능력치")]
    public float baseMaxHP = 100f;        // 1레벨 최대 체력
    public float hpGrowthPercent = 1f;    // 레벨업마다 최대 체력 +1% (소수점 반올림)
    public float baseAttack = 5f;         // 1레벨 기본 공격력
    public float attackPerLevel = 3f;     // 레벨업마다 기본 공격력 +3

    public static PlayerLevel Instance { get; private set; }
    public static int CarryLevel = -1, CarryExp = 0;   // 맵 이동 / 이어하기 시 전달

    public event Action OnChanged;
    public event Action<int> OnLevelUp;                // 인자: 새 레벨

    public static int RequiredExp(int lv)
    {
        if (lv <= 1) return 3;
        if (lv == 2) return 5;
        if (lv == 3) return 10;
        return 10 * (lv - 2);          // 4→20, 5→30, 6→40 ...
    }
    public int Required => RequiredExp(level);

    // 레벨별 최대 체력: 1레벨 100 → 레벨업마다 1%씩 증가, 매번 반올림 (100, 101, 102, 103 ...)
    public float MaxHPFor(int lv)
    {
        float m = baseMaxHP;
        for (int i = 1; i < lv; i++) m = Mathf.Floor(m * (1f + hpGrowthPercent / 100f) + 0.5f);
        return m;
    }
    public float AttackFor(int lv) => baseAttack + attackPerLevel * (lv - 1);

    // 레벨에 맞게 최대 체력·공격력 적용
    void ApplyStats(bool fillHP)
    {
        var hp = GetComponent<PlayerHealth>();
        if (hp != null) hp.SetMaxHP(MaxHPFor(level), fillHP);
        var c = GetComponent<SeriaController>();
        if (c != null) { c.attackDamage = AttackFor(level); c.heavyDamage = AttackFor(level); }   // 기본 공격 · 강공격
    }

    void Start() { ApplyStats(false); }

    int lastLevel;

    void Awake()
    {
        Instance = this;
        if (CarryLevel > 0) { level = CarryLevel; exp = CarryExp; }
        CarryLevel = -1; CarryExp = 0;
        lastLevel = level;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 씬이 바뀔 때 다음 맵으로 레벨 전달
        CarryLevel = level; CarryExp = exp;
    }

    public void AddExp(int amount)
    {
        if (amount <= 0 || level >= maxLevel) return;
        exp += amount;
        DamagePopup.ShowText(transform.position + new Vector3(0.5f, 1.6f, 0), "+" + amount + " EXP", new Color(0.6f, 0.9f, 1f));
        while (level < maxLevel && exp >= Required)
        {
            exp -= Required;
            level++;
            LevelUpFx();
        }
        lastLevel = level;
        OnChanged?.Invoke();
    }

    public void SetLevel(int lv)
    {
        int old = level;
        level = Mathf.Clamp(lv, 1, maxLevel); exp = 0;
        ApplyStats(true);
        if (level > old) for (int l = old + 1; l <= level; l++) OnLevelUp?.Invoke(l);
        lastLevel = level;
        OnChanged?.Invoke();
    }

    void LevelUpFx()
    {
        DamagePopup.ShowText(transform.position + new Vector3(0, 2.1f, 0), "LEVEL UP!", new Color(1f, 0.85f, 0.3f));
        ApplyStats(healOnLevelUp);
        OnLevelUp?.Invoke(level);
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Pressed(KeyCode.F1)) SetLevel(level + 1);
        if (Pressed(KeyCode.F2)) SetLevel(level - 1);
        if (Pressed(KeyCode.F3)) SetLevel(20);
        if (Pressed(KeyCode.F4)) AddExp(1);
#endif
        // Inspector 에서 직접 레벨을 바꾼 경우 반영
        if (level != lastLevel) SetLevel(level);
    }

    static bool Pressed(KeyCode k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; if (kb == null) return false;
        switch (k)
        {
            case KeyCode.F1: return kb.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return kb.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return kb.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return kb.f4Key.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(k);
#endif
    }
}
