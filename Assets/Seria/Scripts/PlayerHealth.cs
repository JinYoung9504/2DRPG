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
        CurrentHP = maxHP;
        invincibleUntil = 0f;
        OnRevived?.Invoke();
    }

    // 무적 시간 동안 깜빡임
    void Update()
    {
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
