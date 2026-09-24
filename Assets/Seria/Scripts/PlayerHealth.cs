// 플레이어 체력 (기본 100)
using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float invincibleTime = 0.5f;   // 맞은 뒤 잠깐 무적

    public static float CarryHP = -1f;   // 맵 이동 시 체력 전달용

    public float CurrentHP { get; private set; }
    public float Percent => maxHP > 0 ? CurrentHP / maxHP * 100f : 0f;
    public bool IsDead => CurrentHP <= 0f;

    public event Action OnDamaged;
    public event Action OnDied;
    public event Action OnRevived;

    float invincibleUntil;

    void Awake()
    {
        CurrentHP = CarryHP > 0f ? Mathf.Min(CarryHP, maxHP) : maxHP;
        CarryHP = -1f;
    }

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
