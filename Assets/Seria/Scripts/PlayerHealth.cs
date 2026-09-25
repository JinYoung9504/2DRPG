// 플레이어 체력 (기본 100)
using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float hitInvincibleTime = 1f;  // 맞은 뒤 무적 시간 (이 동안 연속으로 맞지 않음)
    public bool blinkWhileInvincible = true;

    public static float CarryHP = -1f;   // 맵 이동 시 체력 전달용

    public float CurrentHP { get; private set; }
    public float Percent => maxHP > 0 ? CurrentHP / maxHP * 100f : 0f;
    public bool IsDead => CurrentHP <= 0f;
    public bool IsInvincible => Time.time < invincibleUntil;

    public event Action OnDamaged;
    public event Action OnDied;
    public event Action OnRevived;

    float invincibleUntil;
    SpriteRenderer sr; bool blinking;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        CurrentHP = CarryHP > 0f ? CarryHP : maxHP;   // 최대 체력은 PlayerLevel 이 레벨에 맞춰 다시 정함
        CarryHP = -1f;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || Time.time < invincibleUntil) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        invincibleUntil = Time.time + hitInvincibleTime;
        if (IsDead) OnDied?.Invoke();
        else OnDamaged?.Invoke();
    }

    // 최대 체력 변경 (레벨업 등). fill = true 면 체력을 가득 채움
    public void SetMaxHP(float newMax, bool fill)
    {
        maxHP = Mathf.Max(1f, newMax);
        CurrentHP = fill ? maxHP : Mathf.Min(CurrentHP, maxHP);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    public void Revive()
    {
        CurePoison();
        CurrentHP = maxHP;
        invincibleUntil = 0f;
        OnRevived?.Invoke();
    }

    // ── 독 (지속 데미지): 무적 시간과 상관없이 1초마다 데미지 ──
    public bool IsPoisoned => Time.time < poisonUntil && !IsDead;
    float poisonUntil, poisonDps, nextTick;
    SpriteRenderer poisonIcon; Sprite[] poisonFrames; bool poisonTinted;

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (IsDead) return;
        if (!IsPoisoned) nextTick = Time.time + 1f;          // 첫 틱은 1초 뒤
        poisonUntil = Time.time + duration;                   // 다시 맞으면 시간 초기화
        poisonDps = Mathf.Max(poisonDps * (IsPoisoned ? 1 : 0), damagePerSecond);
    }

    void PoisonUpdate()
    {
        if (!IsPoisoned)
        {
            if (poisonIcon) poisonIcon.enabled = false;
            if (poisonTinted && sr) { sr.color = new Color(1, 1, 1, sr.color.a); poisonTinted = false; }   // 원래 색으로
            return;
        }
        poisonTinted = true;
        if (Time.time >= nextTick)
        {
            nextTick += 1f;
            CurrentHP = Mathf.Max(0f, CurrentHP - poisonDps);
            DamagePopup.ShowText(transform.position + new Vector3(0.3f, 1.6f, 0), Mathf.RoundToInt(poisonDps).ToString(), new Color(0.8f, 0.45f, 1f));
            if (IsDead) { poisonUntil = 0; OnDied?.Invoke(); return; }
        }
        // 머리 위 독 아이콘
        if (poisonFrames == null)
        {
            var a = Resources.LoadAll<Sprite>("Mushroom/Mush_PoisonIcon");
            System.Array.Sort(a, (x, y) => x.name.CompareTo(y.name)); poisonFrames = a;
        }
        if (poisonIcon == null && poisonFrames.Length > 0)
        {
            var g = new GameObject("Poison Icon"); g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0, 1.75f, 0);
            poisonIcon = g.AddComponent<SpriteRenderer>(); poisonIcon.sortingOrder = 60;
        }
        if (poisonIcon != null)
        {
            poisonIcon.enabled = true;
            poisonIcon.sprite = poisonFrames[(int)(Time.time * 10) % poisonFrames.Length];
        }
        if (sr != null && !IsInvincible) sr.color = new Color(0.85f, 0.7f, 1f, sr.color.a);   // 보랏빛
    }

    public void CurePoison() { poisonUntil = 0; poisonTinted = false; if (sr) sr.color = new Color(1, 1, 1, sr.color.a); }

    // 무적 시간 동안 깜빡임
    void Update()
    {
        PoisonUpdate();
        if (sr == null || !blinkWhileInvincible) return;
        bool inv = IsInvincible && !IsDead;
        if (inv)
        {
            var c = sr.color; c.a = Mathf.Repeat(Time.time, 0.16f) < 0.08f ? 0.35f : 1f; sr.color = c;
            blinking = true;
        }
        else if (blinking)
        {
            var c = sr.color; c.a = 1f; sr.color = c; blinking = false;
        }
    }
}
