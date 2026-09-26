// 최종 보스: 아타칸 (왕좌의 간)
//  체력 3000 / 경험치 1000 / 쓰러지면 다시 나타나지 않음
//  - 세리아가 가까이 오면 맵 중앙에 마법진과 함께 등장
//  - 근접: 대검 휘두르기 — 세리아 최대 체력의 20%
//  - 원거리: 대형 검기 발사 — 세리아 최대 체력의 50%, 쏜 뒤 세리아를 향해 돌진
//  - 휘두르기/검기를 3번 쓸 때마다: 늑대 · 타락한 나무정령 · 뱀파이어 중 무작위 2종 소환
//      · 이전 소환수가 남아 있어도 계속 소환 / 소환수는 경험치 없음
//      · 아타칸이 쓰러지면 소환수도 모두 무너짐
//  - 공격은 방어(X)로 막을 수 있음 (스턴 없음), 공격 중엔 맞아도 멈추지 않음
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 아타칸이 부른 소환수 표시 (대화창·경험치 없음)
public class SummonedMinion : MonoBehaviour { }

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class AtakhanBoss : MonoBehaviour
{
    [Header("수치")]
    [Range(0f, 1f)] public float swingRatio = 0.2f;   // 대검: 세리아 최대 체력의 20%
    [Range(0f, 1f)] public float waveRatio = 0.5f;    // 검기: 세리아 최대 체력의 50%
    public int expReward = 1000;

    [Header("등장")]
    public float appearRange = 11f;        // 세리아가 이 거리 안에 오면 등장

    [Header("행동")]
    public float swingRange = 3.2f;        // 이 안이면 대검 휘두르기
    public float waveRange = 5.5f;         // 이보다 멀면 검기
    public float walkSpeed = 1.3f;
    public float restTime = 0.9f;          // 공격 사이 쉬는 시간

    [Header("대검 휘두르기")]
    public float swingWindup = 0.45f;
    public Vector2 swingBox = new Vector2(4.2f, 3.2f);
    public float swingForward = 2.0f;

    [Header("검기 + 돌진")]
    public float waveSpeed = 11f;
    public float waveHeight = 1.9f;        // 땅에서 판정 높이 (점프로 넘을 수 있음)
    public float chargeSpeed = 9f, chargeMaxTime = 1.6f;

    [Header("소환")]
    public int attacksPerSummon = 3;
    public int summonKinds = 2;            // 한 번에 소환하는 종류 수 (3종 중 무작위)
    public GameObject wolfPrefab, treePrefab, vampirePrefab;

    public bool spriteFacesLeft = false;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; Collider2D[] cols; EnemyHealth health; StageBounds stage;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    readonly List<EnemyHealth> minions = new List<EnemyHealth>();
    bool appeared, busy; int attackCount; float restUntil; Coroutine co; string cur;

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
        cols = GetComponents<Collider2D>();
        foreach (var c in cols) if (!c.isTrigger) body = c;
        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.isBoss = true; if (string.IsNullOrEmpty(health.bossId)) health.bossId = "Atakhan";
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.6f;

        health.OnHurt += dir => { if (!busy && appeared) Play("Hit", true); };
        health.OnDied += () =>
        {
            if (co != null) StopCoroutine(co); busy = false; Vel = Vector2.zero;
            anim.speed = 1f; Play("Death", true);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
            DamagePopup.ShowText(transform.position + new Vector3(0, 5f, 0), "아타칸 격파!", new Color(1f, 0.85f, 0.4f));
            CameraShake.Shake(0.5f, 0.2f);
            foreach (var m in minions) if (m != null && !m.IsDead) m.TakeDamage(999999f, transform.position.x);   // 소환수도 무너짐
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
        if (go != null)
        {
            player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
            var pc = go.GetComponent<Collider2D>();
            if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
        }
        // 등장 전: 숨어 있음
        sr.enabled = false; foreach (var c in cols) c.enabled = false; rb.simulated = false;
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float d) { if (d != 0) sr.flipX = spriteFacesLeft ? d > 0 : d < 0; }
    void Play(string s, bool restart = false) { if (!restart && cur == s) return; cur = s; anim.Play(s, 0, 0f); }
    float PlayerMax => playerHealth != null ? playerHealth.maxHP : 100f;

    void Update()
    {
        if (health.IsDead) return;
        bool ok = player != null && (playerHealth == null || !playerHealth.IsDead);

        if (!appeared)
        {
            if (ok && !busy && Mathf.Abs(player.position.x - transform.position.x) <= appearRange) co = StartCoroutine(Appear());
            return;
        }
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.speed = 0.3f; return; }
        anim.speed = 1f;
        if (busy || DialogueUI.IsOpen) return;
        if (!ok) { Vel = new Vector2(0, Vel.y); Play("Idle"); return; }

        float dx = player.position.x - transform.position.x, dist = Mathf.Abs(dx);
        Face(Mathf.Sign(dx));

        if (Time.time < restUntil)
        {
            if (dist > swingRange) { Vel = new Vector2(Mathf.Sign(dx) * walkSpeed, Vel.y); Play("Walk"); }
            else { Vel = new Vector2(0, Vel.y); Play("Idle"); }
            return;
        }

        if (attackCount >= attacksPerSummon) { attackCount = 0; co = StartCoroutine(Summon()); return; }
        if (dist <= swingRange) { co = StartCoroutine(Swing()); return; }
        if (dist >= waveRange) { co = StartCoroutine(WaveAndCharge()); return; }
        Vel = new Vector2(Mathf.Sign(dx) * walkSpeed, Vel.y); Play("Walk");
    }

    void Done(bool counts)
    {
        if (counts) attackCount++;
        busy = false; co = null;
        Play("Idle", true);
        restUntil = Time.time + restTime;
    }

    // ───────── 등장 ─────────
    IEnumerator Appear()
    {
        busy = true;
        if (player != null) Face(Mathf.Sign(player.position.x - transform.position.x));
        AtakhanFX.Summon(transform.position, 1.6f);
        CameraShake.Shake(0.6f, 0.12f);
        yield return new WaitForSeconds(0.35f);
        sr.enabled = true; sr.color = new Color(1, 1, 1, 0);
        Play("Summon", true);
        for (float t = 0; t < 0.6f; t += Time.deltaTime) { sr.color = new Color(1, 1, 1, t / 0.6f); yield return null; }
        sr.color = Color.white;
        foreach (var c in cols) c.enabled = true; rb.simulated = true;
        yield return new WaitForSeconds(0.4f);
        appeared = true;
        restUntil = Time.time + 1f;
        busy = false; co = null; Play("Idle", true);
    }

    // ───────── 대검 휘두르기 ─────────
    IEnumerator Swing()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("SwingReady", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 4.9f, 0), "!", new Color(1f, 0.3f, 0.5f));
        yield return new WaitForSeconds(swingWindup);
        Play("SwingHit", true);
        yield return new WaitForSeconds(0.12f);
        CameraShake.Shake(0.15f, 0.15f);
        Vector2 center = (Vector2)transform.position + new Vector2(dir * swingForward, swingBox.y * 0.5f + 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, swingBox, 0f))
            if (playerRb != null && c.attachedRigidbody == playerRb) { HitPlayer(PlayerMax * swingRatio, dir, transform.position); break; }
        yield return new WaitForSeconds(0.35f);
        Done(true);
    }

    // ───────── 검기 발사 → 돌진 ─────────
    IEnumerator WaveAndCharge()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("Wave", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 4.9f, 0), "!!", new Color(0.9f, 0.4f, 1f));
        yield return new WaitForSeconds(0.45f);
        AtakhanWave.Fire(transform.position + new Vector3(dir * 1.6f, 0, 0), dir, waveSpeed, waveHeight, this);
        CameraShake.Shake(0.15f, 0.1f);
        yield return new WaitForSeconds(0.35f);

        // 세리아를 향해 돌진
        Play("Run", true);
        float end = Time.time + chargeMaxTime;
        while (Time.time < end && !health.IsDead && player != null)
        {
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) < 2.4f) break;
            if (stage != null && (transform.position.x < stage.left + 1.5f && dir < 0 || transform.position.x > stage.right - 1.5f && dir > 0)) break;
            Face(Mathf.Sign(dx)); dir = Mathf.Sign(dx);
            Vel = new Vector2(dir * chargeSpeed, Vel.y);
            yield return null;
        }
        for (float t = 0; t < 0.2f; t += Time.deltaTime) { Vel = new Vector2(Mathf.MoveTowards(Vel.x, 0, 60f * Time.deltaTime), Vel.y); yield return null; }
        Vel = new Vector2(0, Vel.y);
        restUntil = 0f;
        busy = false; co = null; attackCount++;               // 돌진 직후 바로 다음 행동 (가까우면 휘두르기)
    }

    // ───────── 소환: 늑대 + 나무정령 + 뱀파이어 ─────────
    IEnumerator Summon()
    {
        busy = true; Vel = new Vector2(0, Vel.y);
        Play("Summon", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 5.2f, 0), "나의 종들이여, 일어나라!", new Color(1f, 0.35f, 0.6f));
        float x = transform.position.x;
        var spots = new[] { Clamp(x - 6f), Clamp(x + 6f), Clamp(x + (Random.value < 0.5f ? -3f : 3f)) };
        var prefabs = new[] { wolfPrefab, treePrefab, vampirePrefab };
        // 3종 중 무작위 2종
        var pick = new List<int>(); for (int i = 0; i < 3; i++) if (prefabs[i] != null) pick.Add(i);
        while (pick.Count > summonKinds) pick.RemoveAt(Random.Range(0, pick.Count));
        for (int k = 0; k < pick.Count; k++) AtakhanFX.Summon(new Vector3(spots[k], transform.position.y, 0), 1.2f);
        yield return new WaitForSeconds(0.7f);
        if (!health.IsDead)
            for (int k = 0; k < pick.Count; k++) Spawn(prefabs[pick[k]], new Vector3(spots[k], transform.position.y + 0.05f, 0));
        yield return new WaitForSeconds(0.4f);
        Done(false);
    }

    float Clamp(float x) => stage != null ? Mathf.Clamp(x, stage.left + 2.5f, stage.right - 2.5f) : x;

    void Spawn(GameObject prefab, Vector3 pos)
    {
        var g = Instantiate(prefab, pos, Quaternion.identity);
        g.name = prefab.name + " (아타칸 소환)";
        g.AddComponent<SummonedMinion>();
        var eh = g.GetComponent<EnemyHealth>();
        if (eh != null) { eh.isBoss = false; eh.bossId = ""; eh.respawnTime = 0f; minions.Add(eh); }   // 보스 기록·리젠 없음
        var w = g.GetComponent<WolfBoss>(); if (w != null) w.expReward = 0;
        var t = g.GetComponent<TreeBossEnemy>(); if (t != null) t.expReward = 0;
        var v = g.GetComponent<VampireBoss>(); if (v != null) v.expReward = 0;
        var gsr = g.GetComponent<SpriteRenderer>();
        if (gsr != null && player != null) gsr.flipX = player.position.x < pos.x;
        if (gsr != null) StartCoroutine(FadeIn(gsr));
        minions.RemoveAll(m => m == null);
    }

    IEnumerator FadeIn(SpriteRenderer s)
    {
        for (float t = 0; t < 0.5f && s != null; t += Time.deltaTime) { s.color = new Color(1f, 0.6f, 0.9f, t / 0.5f); yield return null; }
        if (s != null) s.color = Color.white;
    }

    // 공용 피격 (방어하면 막힘, 스턴 없음)
    public void HitPlayer(float dmg, float dir, Vector3 from)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(from, false)) return;
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(dmg);
        if (playerHealth.CurrentHP < before && playerRb != null) SetVel(playerRb, new Vector2((dir == 0 ? 1 : dir) * 8f, 5f));
    }
    public float WaveDamage => PlayerMax * waveRatio;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.5f, 0.6f); Gizmos.DrawWireSphere(transform.position, swingRange);
        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.5f); Gizmos.DrawWireSphere(transform.position, waveRange);
    }
}

// 대형 검기: 땅을 따라 날아감 (점프로 넘거나 방어로 막기)
public class AtakhanWave : MonoBehaviour
{
    float dir, speed, height, life; AtakhanBoss owner; SpriteRenderer sr; bool hit;

    public static void Fire(Vector3 ground, float dir, float speed, float height, AtakhanBoss owner)
    {
        var g = new GameObject("Atakhan Sword Wave"); g.transform.position = ground + new Vector3(0, height * 0.55f, 0);
        var w = g.AddComponent<AtakhanWave>(); w.dir = dir; w.speed = speed; w.height = height; w.owner = owner;
        w.sr = g.AddComponent<SpriteRenderer>(); w.sr.sprite = Resources.Load<Sprite>("Atakhan/Atk_WaveFx");
        w.sr.flipX = dir < 0; w.sr.sortingOrder = 22;
    }

    void Update()
    {
        float dt = Time.deltaTime; life += dt;
        transform.position += new Vector3(dir * speed * dt, 0, 0);
        float s = 1f + 0.06f * Mathf.Sin(life * 25f);
        transform.localScale = new Vector3(Mathf.Min(1f, life * 6f) * s, s, 1);
        if (!hit && owner != null)
        {
            Vector2 center = (Vector2)transform.position + new Vector2(dir * 0.6f, -height * 0.55f + height * 0.5f);
            foreach (var c in Physics2D.OverlapBoxAll(center, new Vector2(1.6f, height), 0f))
            {
                var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
                if (hp == null || hp.IsDead) continue;
                hit = true; owner.HitPlayer(owner.WaveDamage, dir, transform.position);
                break;
            }
        }
        if (life > 2.6f) { sr.color = new Color(1, 1, 1, Mathf.Max(0, 1f - (life - 2.6f) * 3f)); if (life > 2.95f) Destroy(gameObject); }
    }
}

// 소환 마법진 이펙트 (Resources/Atakhan/Atk_SummonFx, 6장)
public static class AtakhanFX
{
    public static void Summon(Vector3 ground, float scale)
    {
        var g = new GameObject("Atakhan Summon Fx"); g.transform.position = ground; g.transform.localScale = Vector3.one * scale;
        g.AddComponent<AtakhanSummonFx>();
    }
}

public class AtakhanSummonFx : MonoBehaviour
{
    IEnumerator Start()
    {
        var r = gameObject.AddComponent<SpriteRenderer>(); r.sortingOrder = 21;
        var f = Resources.LoadAll<Sprite>("Atakhan/Atk_SummonFx");
        System.Array.Sort(f, (a, b) => Idx(a.name).CompareTo(Idx(b.name)));
        foreach (var s in f) { r.sprite = s; yield return new WaitForSeconds(0.08f); }
        for (float t = 0; t < 0.4f; t += Time.deltaTime) { r.color = new Color(1, 1, 1, 1f - t / 0.4f); yield return null; }
        Destroy(gameObject);
    }
    static int Idx(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }
}
