// 레벨로 배우는 스킬: A = 검기(5레벨), S = 번개(10레벨), D = 메테오(20레벨), F = 성검 유성우(30레벨)
using UnityEngine;

public class SeriaSkills : MonoBehaviour
{
    [System.Serializable]
    public class Skill
    {
        public string name;
        public KeyCode key;
        public int unlockLevel;
        public float damage;
        public float cooldown;
        [HideInInspector] public float readyTime;
        public float Remaining => Mathf.Max(0f, readyTime - Time.time);
    }

    public Skill swordWave = new Skill { name = "검기",   key = KeyCode.A, unlockLevel = 5,  damage = 15f,  cooldown = 2f };
    public Skill lightning = new Skill { name = "번개",   key = KeyCode.S, unlockLevel = 10, damage = 50f,  cooldown = 6f };
    public Skill meteor    = new Skill { name = "메테오", key = KeyCode.D, unlockLevel = 20, damage = 100f, cooldown = 15f };
    public Skill swordRain = new Skill { name = "성검 유성우", key = KeyCode.F, unlockLevel = 30, damage = 200f, cooldown = 20f };

    [Header("번개·메테오 조준")]
    public float targetRange = 9f;      // 앞쪽 이 거리 안의 가장 가까운 적 위치에 떨어짐
    public float defaultDistance = 4f;  // 적이 없으면 앞쪽 이 거리에 떨어짐
    public float meteorWidth = 6f;      // 메테오 크기(가로 폭, 유닛) — 화면 가로(약 17.8)의 1/3
    public float meteorSmallDamage = 10f; // 메테오 작은 돌 1개당 데미지 (큰 돌은 메테오 Damage)
    public float swordRainWidth = 7.5f;   // 성검 유성우 마법진 폭 (유닛)

    public Skill[] All => new[] { swordWave, lightning, meteor, swordRain };

    SeriaController ctrl; PlayerLevel lv; SpriteRenderer sr;
    Sprite toastLocked;

    void Awake()
    {
        ctrl = GetComponent<SeriaController>(); sr = GetComponent<SpriteRenderer>();
        lv = GetComponent<PlayerLevel>();
        if (lv == null) lv = gameObject.AddComponent<PlayerLevel>();
        toastLocked = Resources.Load<Sprite>("Skills/Toast_Locked");
        lv.OnLevelUp += newLv =>
        {
            if (newLv == swordWave.unlockLevel) Toast.Show(Resources.Load<Sprite>("Skills/Toast_Learn_SwordWave"), 2f);
            if (newLv == lightning.unlockLevel) Toast.Show(Resources.Load<Sprite>("Skills/Toast_Learn_Lightning"), 2f);
            if (newLv == meteor.unlockLevel) Toast.Show(Resources.Load<Sprite>("Skills/Toast_Learn_Meteor"), 2f);
            if (newLv == swordRain.unlockLevel) Toast.Show(Resources.Load<Sprite>("Skills/Toast_Learn_SwordRain"), 2f);
        };
    }

    public bool Unlocked(Skill s) => lv != null && lv.level >= s.unlockLevel;

    void Update()
    {
        if (ctrl == null || Time.timeScale == 0f || DialogueUI.IsOpen) return;
        foreach (var s in All)
            if (SeriaController.KeyDown(s.key)) TryCast(s);
    }

    void TryCast(Skill s)
    {
        if (!Unlocked(s)) { Toast.Show(toastLocked, 1f); return; }
        if (s.Remaining > 0f || !ctrl.CanAct) return;
        s.readyTime = Time.time + s.cooldown;
        float dir = sr.flipX ? -1f : 1f;

        if (s == swordWave)
        {
            ctrl.PlayCastAnimation("Heavy");
            StartCoroutine(Delay(0.2f, () => SwordWave.Launch(transform.position + new Vector3(dir * 0.8f, 0.75f, 0), dir, s.damage)));
        }
        else if (s == lightning)
        {
            ctrl.PlayCastAnimation("Skill");
            Vector3 p = TargetPoint(dir);
            StartCoroutine(Delay(0.25f, () => StrikeEffect.Cast("Lightning", p, s.damage, new Vector2(2.4f, 3f), 4, 1f, transform.position.x)));
        }
        else if (s == meteor)
        {
            ctrl.PlayCastAnimation("Skill");
            Vector3 p = TargetPoint(dir);
            StartCoroutine(Delay(0.25f, () => MeteorStrike.Cast(p, s.damage, meteorWidth, transform.position.x, meteorSmallDamage)));
        }
        else if (s == swordRain)
        {
            ctrl.PlayCastAnimation("Skill");
            Vector3 p = TargetPoint(dir);
            StartCoroutine(Delay(0.2f, () => SwordRain.Cast(p, s.damage, swordRainWidth, transform.position.x, dir)));
        }
    }

    // 앞쪽에서 가장 가까운 적의 발밑, 없으면 앞쪽 일정 거리의 바닥
    Vector3 TargetPoint(float dir)
    {
        Vector3 me = transform.position; Vector3 best = me + new Vector3(dir * defaultDistance, 0, 0); float bestD = float.MaxValue;
        foreach (var c in Physics2D.OverlapBoxAll(me + new Vector3(dir * targetRange / 2f, 1f, 0), new Vector2(targetRange, 4f), 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e == null || e.IsDead) continue;
            float d = Mathf.Abs(e.transform.position.x - me.x);
            if (d < bestD) { bestD = d; best = new Vector3(e.transform.position.x, me.y, 0); }
        }
        best.y = me.y;
        return best;
    }

    System.Collections.IEnumerator Delay(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }
}
