// 몬스터 공용 체력: 데미지 숫자, 피격 번쩍임, 머리 위 체력바, 사망 연출, 리스폰
using System;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 10f;
    public float respawnTime = 5f;
    [Header("보스")]
    public bool isBoss;                    // 보스는 쓰러지면 다시 나타나지 않음 (저장에도 기록)
    public string bossId = "";
    public float deathAnimTime = 0f;      // 0보다 크면: 납작해지는 기본 연출 대신 AI 가 재생하는 사망 애니메이션을 이 시간만큼 기다린 뒤 사라짐        // 죽은 뒤 이 시간 후 원래 자리에서 다시 등장 (0 이면 리스폰 안 함)
    public Vector2 hpBarOffset = new Vector2(0f, 1.05f);

    public float CurrentHP { get; private set; }
    public bool IsDead => CurrentHP <= 0f;
    public float StunnedUntil { get; private set; }
    public bool IsStunned => !IsDead && Time.time < StunnedUntil;
    public event Action OnStunned;

    public event Action<float> OnHurt;    // 인자: 밀려날 방향 (-1 왼쪽 / 1 오른쪽)
    public event Action OnDied;
    public event Action OnRespawned;

    SpriteRenderer sr; Rigidbody2D rb; Collider2D[] cols;
    Vector3 homePos, baseScale; Color baseColor;
    Transform barRoot, barFill; float barHideTime;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>(); rb = GetComponent<Rigidbody2D>(); cols = GetComponents<Collider2D>();
        CurrentHP = maxHP; homePos = transform.position; baseScale = transform.localScale;
        baseColor = sr != null ? sr.color : Color.white;
        BuildBar();
    }

    // 스턴: 일정 시간 아무 행동도 못 함 (패링당했을 때)
    public void Stun(float seconds)
    {
        if (IsDead) return;
        StunnedUntil = Time.time + seconds;
        DamagePopup.ShowText(transform.position + new Vector3(0, hpBarOffset.y + 0.35f, 0), "STUN", new Color(1f, 0.95f, 0.4f));
        OnStunned?.Invoke();
        if (stunRoutine != null) StopCoroutine(stunRoutine);
        stunRoutine = StartCoroutine(StunFx());
    }
    Coroutine stunRoutine;

    IEnumerator StunFx()
    {
        // 머리 위에서 도는 노란 별(점) 3개 + 푸르스름하게 어두워짐
        var stars = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var g = new GameObject("StunStar"); g.transform.SetParent(transform, false);
            var r = g.AddComponent<SpriteRenderer>(); r.sprite = Pixel(); r.color = new Color(1f, 0.9f, 0.3f); r.sortingOrder = 60;
            g.transform.localScale = new Vector3(0.12f, 0.12f, 1);
            stars[i] = g.transform;
        }
        while (IsStunned)
        {
            float t = Time.time * 5f;
            var s = transform.localScale;
            for (int i = 0; i < 3; i++)
            {
                float ang = t + i * 2.094f;
                stars[i].localPosition = new Vector3((Mathf.Cos(ang) * 0.4f - 0.06f) / Mathf.Max(0.01f, s.x), (hpBarOffset.y - 0.15f + Mathf.Sin(ang) * 0.12f) / Mathf.Max(0.01f, s.y), 0);
            }
            if (sr) sr.color = Color.Lerp(baseColor, new Color(0.6f, 0.65f, 0.95f, baseColor.a), 0.5f + 0.2f * Mathf.Sin(Time.time * 10f));
            yield return null;
        }
        foreach (var st in stars) if (st) Destroy(st.gameObject);
        if (sr && !IsDead) sr.color = baseColor;
        stunRoutine = null;
    }

    void Start()
    {
        // 이미 쓰러뜨린 보스는 등장하지 않음
        if (isBoss && GameProgress.IsBossDefeated(BossKey)) { Destroy(gameObject); return; }
        MonsterTalk.Attach(this);                                   // 몬스터 대사 파일이 있으면 대화 기능 연결
    }
    string BossKey => string.IsNullOrEmpty(bossId) ? gameObject.name : bossId;

    public void TakeDamage(float amount, float attackerX)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        DamagePopup.Show(transform.position + new Vector3(0, hpBarOffset.y + 0.2f, 0), amount);
        if (sr != null) StartCoroutine(Flash());
        barHideTime = Time.time + 3f;

        if (IsDead) { OnDied?.Invoke(); StartCoroutine(DieRoutine()); }
        else
        {
            float dir = Mathf.Sign(transform.position.x - attackerX); if (dir == 0) dir = 1;
            OnHurt?.Invoke(dir);
        }
    }

    IEnumerator Flash()
    {
        sr.color = new Color(1f, 0.45f, 0.45f, baseColor.a);
        yield return new WaitForSeconds(0.1f);
        if (!IsDead && !IsStunned) sr.color = baseColor;
    }

    IEnumerator DieRoutine()
    {
        foreach (var c in cols) c.enabled = false;
        if (rb != null) rb.simulated = false;
        if (deathAnimTime > 0f)
        {
            barRoot.gameObject.SetActive(false);
            yield return new WaitForSeconds(deathAnimTime);
            for (float t = 0; t < 0.5f; t += Time.deltaTime) { if (sr) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t / 0.5f)); yield return null; }
        }
        // 납작하게 퍼지면서 사라짐
        else for (float t = 0; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            transform.localScale = new Vector3(baseScale.x * (1f + 0.5f * k), baseScale.y * (1f - 0.8f * k), baseScale.z);
            if (sr) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
            yield return null;
        }
        if (sr) sr.enabled = false;
        barRoot.gameObject.SetActive(false);

        if (isBoss) { GameProgress.MarkBossDefeated(BossKey); Destroy(gameObject); yield break; }
        if (respawnTime <= 0f) { Destroy(gameObject); yield break; }
        yield return new WaitForSeconds(respawnTime);

        // 리스폰
        transform.position = homePos; transform.localScale = baseScale;
        CurrentHP = maxHP; StunnedUntil = 0f;
        if (sr) { sr.enabled = true; sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0); }
        foreach (var c in cols) c.enabled = true;
        if (rb != null) rb.simulated = true;
        OnRespawned?.Invoke();
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            if (sr) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * t / 0.4f);
            yield return null;
        }
        if (sr) sr.color = baseColor;
    }

    // ── 머리 위 작은 체력바 (맞았을 때 3초간 표시) ──
    static Sprite pixel;
    static Sprite Pixel()
    {
        if (pixel == null) pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
        return pixel;
    }

    void BuildBar()
    {
        barRoot = new GameObject("HP Bar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = hpBarOffset;
        SpriteRenderer Part(string n, Color c, int order, Vector3 pos, Vector3 scale)
        {
            var g = new GameObject(n); g.transform.SetParent(barRoot, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.AddComponent<SpriteRenderer>(); r.sprite = Pixel(); r.color = c; r.sortingOrder = order;
            return r;
        }
        Part("Back", new Color(0, 0, 0, 0.7f), 50, new Vector3(-0.42f, 0, 0), new Vector3(0.84f, 0.1f, 1));
        barFill = Part("Fill", new Color(0.9f, 0.2f, 0.25f), 51, new Vector3(-0.4f, 0, 0), new Vector3(0.8f, 0.06f, 1)).transform;
        barRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (barRoot == null || IsDead) return;
        bool show = Time.time < barHideTime;
        if (barRoot.gameObject.activeSelf != show) barRoot.gameObject.SetActive(show);
        barFill.localScale = new Vector3(0.8f * CurrentHP / maxHP, 0.06f, 1);
        // 슬라임이 찌그러지는 연출과 무관하게 체력바 크기 유지
        var s = transform.localScale;
        barRoot.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1);
    }
}
