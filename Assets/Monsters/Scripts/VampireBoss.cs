// 멸망한 왕국 중간 보스: 뱀파이어
//  체력 350 / 공격력 50 (모든 공격) / 경험치 400 / 쓰러지면 다시 나타나지 않음(보스)
//  - 가까이(근접 거리): 광폭 베기 — "!" 후 앞으로 크게 벰 (방어로 막기 가능, 스턴 없음)
//  - 멀리(원거리):   광역기 난사 — 번갈아 사용
//      · 혈의 비 : 세리아 발밑과 좌우에 붉은 경고 → 피의 가시가 차례로 솟아오름 (움직여서 피하기)
//      · 혈의 창 : 피의 창 3발을 가슴 높이로 연속 발사 (숙이기·점프·방어로 피하기)
//  - 10초마다 날개를 펼쳐 네크로맨서 1마리 소환 (동시에 최대 maxNecromancers 마리)
//      · 소환된 네크로맨서는 원래처럼 스켈레톤 검사+궁수를 소환
//      · 이 보스가 소환한 네크로맨서·스켈레톤은 경험치를 주지 않음
//      · 뱀파이어가 쓰러지면 소환수도 모두 무너짐
//  - 공격 중에는 맞아도 멈추지 않음(슈퍼아머)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class VampireBoss : MonoBehaviour
{
    [Header("수치")]
    public float damage = 50f;
    public int expReward = 400;

    [Header("행동")]
    public float detectRange = 14f;
    public float walkSpeed = 1.2f;
    public float meleeRange = 2.4f;        // 이 안이면 근접기
    public float farRange = 4.5f;          // 이보다 멀면 광역기
    public float meleeCooldown = 1.4f;
    public float rangedCooldown = 2.2f;    // 광역기 사이 간격 (난사)

    [Header("근접: 광폭 베기")]
    public float slashWindup = 0.45f;
    public Vector2 slashBox = new Vector2(2.8f, 2.0f);
    public float slashForward = 1.4f;

    [Header("원거리: 혈의 비 (가시)")]
    public int spikeCount = 4;
    public float spikeSpacing = 1.8f;
    public float spikeWarn = 0.75f;
    public float spikeStagger = 0.18f;

    [Header("원거리: 혈의 창")]
    public int spearCount = 3;
    public float spearInterval = 0.32f;
    public float spearSpeed = 12f;
    public Vector2 spearOffset = new Vector2(1.0f, 1.0f);

    [Header("네크로맨서 소환")]
    public GameObject necromancerPrefab;
    public float summonInterval = 10f;
    public int maxNecromancers = 2;

    public bool spriteFacesLeft = false;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health; StageBounds stage;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    readonly List<EnemyHealth> necros = new List<EnemyHealth>();
    float nextMelee, nextRanged, nextSummon = -1f; bool busy, engaged; int rangedCount; Coroutine co; string cur;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.linearVelocity = v;
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.velocity = v;
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.isBoss = true; if (string.IsNullOrEmpty(health.bossId)) health.bossId = "VampireLord";
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.3f;
        nextRanged = Time.time + 1.5f;

        health.OnDied += () =>
        {
            if (co != null) StopCoroutine(co); busy = false; Vel = Vector2.zero;
            anim.speed = 1f; Play("Death", true);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
            foreach (var n in necros) if (n != null) { var ne = n.GetComponent<NecromancerEnemy>(); if (ne != null) ne.KillAllSummons(); }
            foreach (var n in necros) if (n != null && !n.IsDead) n.TakeDamage(99999f, transform.position.x);   // 소환수도 무너짐 (경험치 없음)
        };
        health.OnStunned += () => { if (co != null) StopCoroutine(co); busy = false; Play("Idle", true); };
    }

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        stage = Object.FindFirstObjectByType<StageBounds>();
#else
        stage = Object.FindObjectOfType<StageBounds>();
#endif
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float d) { if (d != 0) sr.flipX = spriteFacesLeft ? d > 0 : d < 0; }
    void Play(string s, bool restart = false) { if (!restart && cur == s) return; cur = s; anim.Play(s, 0, 0f); }

    int AliveNecros() { necros.RemoveAll(n => n == null || n.IsDead); return necros.Count; }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.speed = 0.3f; return; }
        anim.speed = 1f;
        if (busy) return;

        bool ok = player != null && (playerHealth == null || !playerHealth.IsDead) && !DialogueUI.IsOpen;
        float dx = ok ? player.position.x - transform.position.x : 0f;
        float dist = Mathf.Abs(dx);
        bool see = ok && Mathf.Abs(player.position.y - transform.position.y) < 4f && (dist <= detectRange || engaged);
        if (!see) { Vel = new Vector2(0, Vel.y); Play("Idle"); return; }

        if (!engaged) { engaged = true; nextSummon = Time.time + 3f; }        // 처음 마주친 뒤 3초 후 첫 소환, 이후 10초마다
        Face(Mathf.Sign(dx));

        if (necromancerPrefab != null && Time.time >= nextSummon)
        {
            nextSummon = Time.time + summonInterval;
            if (AliveNecros() < maxNecromancers) { co = StartCoroutine(SummonNecro()); return; }
        }

        if (dist <= meleeRange)
        {
            Vel = new Vector2(0, Vel.y);
            if (Time.time >= nextMelee) { co = StartCoroutine(Slash()); return; }
            Play("Idle");
        }
        else if (dist >= farRange && Time.time >= nextRanged)
        {
            co = StartCoroutine(rangedCount++ % 2 == 0 ? BloodRain() : BloodSpears()); return;
        }
        else
        {
            Vel = new Vector2(Mathf.Sign(dx) * walkSpeed, Vel.y);
            Play("Walk");
        }
    }

    // ───────── 근접: 광폭 베기 ─────────
    IEnumerator Slash()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("MeleeReady", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 2.8f, 0), "!", new Color(1f, 0.25f, 0.25f));
        yield return new WaitForSeconds(slashWindup);
        Play("MeleeSwing", true);
        yield return new WaitForSeconds(0.08f);
        CameraShake.Shake(0.12f, 0.12f);
        Vector2 center = (Vector2)transform.position + new Vector2(dir * slashForward, slashBox.y * 0.5f + 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, slashBox, 0f))
            if (c.attachedRigidbody != null && c.attachedRigidbody == playerRb) { HitPlayer(transform.position, dir); break; }
        yield return new WaitForSeconds(0.35f);
        Play("Idle", true);
        nextMelee = Time.time + meleeCooldown;
        busy = false;
    }

    // ───────── 원거리: 혈의 비 ─────────
    IEnumerator BloodRain()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        Play("Cast", true);
        float px = player.position.x, groundY = transform.position.y;
        var xs = new List<float> { px };
        for (int i = 1; xs.Count < spikeCount; i++) { xs.Add(px + i * spikeSpacing); if (xs.Count < spikeCount) xs.Add(px - i * spikeSpacing); }
        for (int i = 0; i < xs.Count; i++)
        {
            float x = xs[i];
            if (stage != null) x = Mathf.Clamp(x, stage.left + 0.5f, stage.right - 0.5f);
            StartCoroutine(Spike(new Vector3(x, groundY, 0)));
            yield return new WaitForSeconds(spikeStagger);
        }
        yield return new WaitForSeconds(spikeWarn + 0.3f);
        Play("Idle", true);
        nextRanged = Time.time + rangedCooldown;
        busy = false;
    }

    IEnumerator Spike(Vector3 pos)
    {
        var warn = VampFX.Warn(pos);
        yield return new WaitForSeconds(spikeWarn);
        if (warn != null) Destroy(warn);
        if (health.IsDead) yield break;
        var s = VampFX.Fx("Vamp_Spikes", pos, 22);
        CameraShake.Shake(0.08f, 0.06f);
        bool hit = false;
        for (float t = 0; t < 0.45f && s != null; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 0.12f);
            s.transform.localScale = new Vector3(1f, k, 1f);                     // 땅에서 솟아오름
            if (!hit && k > 0.5f)
                foreach (var c in Physics2D.OverlapBoxAll((Vector2)pos + new Vector2(0, 1.0f), new Vector2(1.3f, 2.0f), 0f))
                    if (c.attachedRigidbody != null && c.attachedRigidbody == playerRb) { HitPlayer(pos, Mathf.Sign(player.position.x - pos.x)); hit = true; break; }
            yield return null;
        }
        if (s != null) StartCoroutine(VampFX.FadeOut(s, 0.3f));
    }

    // ───────── 원거리: 혈의 창 ─────────
    IEnumerator BloodSpears()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("Cast", true);
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < spearCount && !health.IsDead; i++)
        {
            Vector3 from = transform.position + new Vector3(dir * spearOffset.x, spearOffset.y, 0);
            VampireSpear.Fire(from, dir, spearSpeed, this);
            yield return new WaitForSeconds(spearInterval);
        }
        yield return new WaitForSeconds(0.25f);
        Play("Idle", true);
        nextRanged = Time.time + rangedCooldown;
        busy = false;
    }

    // ───────── 네크로맨서 소환 ─────────
    IEnumerator SummonNecro()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("Summon", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 3.0f, 0), "일어나라…!", new Color(1f, 0.3f, 0.35f));
        float x = transform.position.x - dir * 2.2f;                              // 보스 뒤쪽에 소환
        if (stage != null && (x < stage.left + 1f || x > stage.right - 1f)) x = transform.position.x + dir * 2.2f;
        if (stage != null) x = Mathf.Clamp(x, stage.left + 1f, stage.right - 1f);
        var pos = new Vector3(x, transform.position.y, 0);
        var vortex = VampFX.Fx("Vamp_Vortex", pos + new Vector3(0, 0.1f, 0), -2);
        StartCoroutine(VampFX.Spin(vortex, 1.3f));
        yield return new WaitForSeconds(0.6f);

        if (!health.IsDead)
        {
            var g = Instantiate(necromancerPrefab, pos, Quaternion.identity);
            g.name = "Necromancer (뱀파이어 소환)";
            var eh = g.GetComponent<EnemyHealth>(); if (eh != null) { eh.respawnTime = 0f; necros.Add(eh); }
            var n = g.GetComponent<NecromancerEnemy>();
            if (n != null) { n.expReward = 0; n.summonsGiveExp = false; }          // 이 맵 소환수는 경험치 없음
            var gsr = g.GetComponent<SpriteRenderer>(); if (gsr != null) { gsr.flipX = dir < 0; StartCoroutine(FadeIn(gsr)); }
        }
        yield return new WaitForSeconds(0.6f);
        Play("Idle", true);
        busy = false;
    }

    IEnumerator FadeIn(SpriteRenderer s)
    {
        for (float t = 0; t < 0.5f && s != null; t += Time.deltaTime) { s.color = new Color(1f, 0.5f, 0.5f, t / 0.5f); yield return null; }
        if (s != null) s.color = Color.white;
    }

    // 공용 피격 처리 (방어하면 막힘, 스턴 없음)
    public void HitPlayer(Vector3 from, float dir)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(from, false)) return;
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(damage);
        if (playerHealth.CurrentHP < before && playerRb != null) SetVel(playerRb, new Vector2((dir == 0 ? 1 : dir) * 7f, 5f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f); Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f); Gizmos.DrawWireSphere(transform.position, farRange);
    }
}

// 피의 창: 곧게 날아가 세리아에 맞으면 데미지
public class VampireSpear : MonoBehaviour
{
    float dir, speed, life; VampireBoss owner; SpriteRenderer sr;

    public static void Fire(Vector3 from, float dir, float speed, VampireBoss owner)
    {
        var g = new GameObject("Blood Spear"); g.transform.position = from;
        var s = g.AddComponent<VampireSpear>(); s.dir = dir; s.speed = speed; s.owner = owner;
        s.sr = g.AddComponent<SpriteRenderer>(); s.sr.sprite = Resources.Load<Sprite>("Vampire/Vamp_Spear");
        s.sr.flipX = dir < 0; s.sr.sortingOrder = 22;
    }

    void Update()
    {
        float dt = Time.deltaTime; life += dt;
        transform.position += new Vector3(dir * speed * dt, 0, 0);
        Vector2 tip = (Vector2)transform.position + new Vector2(dir * 1.3f, 0);
        foreach (var c in Physics2D.OverlapBoxAll(tip, new Vector2(1.2f, 0.25f), 0f))
        {
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp != null) { if (hp.IsDead) continue; if (owner != null) owner.HitPlayer(transform.position, dir); Destroy(gameObject); return; }
        }
        if (life > 2.2f) { sr.color = new Color(1, 1, 1, Mathf.Max(0, 1f - (life - 2.2f) * 4f)); if (life > 2.45f) Destroy(gameObject); }
    }
}

// 이펙트 도우미
public static class VampFX
{
    static Sprite warnSprite;

    public static SpriteRenderer Fx(string name, Vector3 pos, int order)
    {
        var g = new GameObject(name); g.transform.position = pos;
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = Resources.Load<Sprite>("Vampire/" + name); r.sortingOrder = order;
        return r;
    }

    // 붉은 경고 표시 (땅에 납작한 빛)
    public static GameObject Warn(Vector3 pos)
    {
        if (warnSprite == null)
        {
            const int w = 64, h = 32; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f + 0.5f) / (w / 2f), dy = (y - h / 2f + 0.5f) / (h / 2f); float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d); a = a * a + (d > 0.75f && d < 0.95f ? 0.7f : 0f);
                tex.SetPixel(x, y, new Color(1f, 0.15f, 0.2f, Mathf.Clamp01(a)));
            }
            tex.Apply(); warnSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
        var g = new GameObject("Blood Warning"); g.transform.position = pos + new Vector3(0, 0.08f, 0);
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = warnSprite; r.sortingOrder = 19;
        g.transform.localScale = new Vector3(0.7f, 0.45f, 1);
        g.AddComponent<WarnBlink>();
        return g;
    }

    public static IEnumerator FadeOut(SpriteRenderer r, float time)
    {
        for (float t = 0; t < time && r != null; t += Time.deltaTime) { r.color = new Color(1, 1, 1, 1 - t / time); yield return null; }
        if (r != null) Object.Destroy(r.gameObject);
    }

    public static IEnumerator Spin(SpriteRenderer r, float time)
    {
        for (float t = 0; t < time && r != null; t += Time.deltaTime)
        {
            float open = Mathf.Clamp01(t / 0.3f), k = t / time;
            r.transform.localScale = new Vector3(1.1f * open, 0.45f * open, 1);
            r.transform.localRotation = Quaternion.Euler(0, 0, -t * 40f);
            r.color = new Color(1, 1, 1, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            yield return null;
        }
        if (r != null) Object.Destroy(r.gameObject);
    }
}

public class WarnBlink : MonoBehaviour
{
    SpriteRenderer r; float t;
    void Start() { r = GetComponent<SpriteRenderer>(); }
    void Update() { t += Time.deltaTime; r.color = new Color(1, 1, 1, Mathf.Min(1f, t * 4f) * (0.7f + 0.3f * Mathf.Sin(t * 18f))); }
}
