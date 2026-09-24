// 플레이어 체력 (기본 100)
using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float invincibleTime = 0.5f;   // 맞은 뒤 잠깐 무적

    public float CurrentHP { get; private set; }
    public float Percent => maxHP > 0 ? CurrentHP / maxHP * 100f : 0f;
    public bool IsDead => CurrentHP <= 0f;

    public event Action OnDamaged;
    public event Action OnDied;
    public event Action OnRevived;

    float invincibleUntil;

    void Awake() { CurrentHP = maxHP; }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || Time.time < invincibleUntil) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        invincibleUntil = Time.time + invincibleTime;
        if (IsDead) OnDied?.Invoke();
        else OnDamaged?.Invoke();
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
}
