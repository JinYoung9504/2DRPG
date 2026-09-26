// 중간 보스: 대형 마수 늑대 (시작의 마을4)
//  패턴 (서로 겹치지 않음, 한 번에 하나)
//   - 멀면  → 돌진 (멧돼지와 같은 속도) : 20 데미지, 방어로 막으면 패링 → 2초 스턴
//   - 가까우면 → 할퀴기 : 30 데미지, 방어로 막을 수 있음 (스턴은 없음)
//  공격 중에는 맞아도 멈추지 않음(슈퍼아머)
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class WolfBoss : MonoBehaviour
{
    [Header("수치")]
    public float chargeDamage = 20f;
    public float clawDamage = 30f;
    public float fireballDamage = 50f;
    public float touchDamage = 5f;          // 평소 몸에 닿았을 때 (따로 정하지 않아 임시)
    public int expReward = 100;

    [Header("패턴")]
    public float detectRange = 14f;
    public float clawRange = 4.5f;          // 이보다 가까우면 할퀴기, 멀면 돌진 (늑대 몸이 커서 넉넉하게)
    public int attacksBeforeFireball = 5;
    public bool useFireball = false;        // 파이어볼 패턴 사용 안 함 (이펙트 교체 전까지)
    public float restTime = 1.0f;           // 공격 사이 쉬는 시간
    public float walkSpeed = 1.6f;

    [Header("돌진")]
    public float chargeSpeed = 10f;         // 멧돼지와 같음
    public float chargeWindup = 0.6f, chargeMaxTime = 1.3f;

    [Header("할퀴기")]
    public float clawWindup = 0.35f;
    public Vector2 clawBoxOffset = new Vector2(2.6f, 1.8f), clawBoxSize = new Vector2(4f, 3.6f);

    [Header("파이어볼")]
    public float fireballSpeed = 3.5f;      // 천천히
    public float fireballWindup = 1.0f;
    public float fireballHeight = 2.45f;    // 땅에서 파이어볼 중심 높이 (아래쪽이 숙인 세리아 머리 위를 지나가도록)

    public bool spriteFacesLeft = false;    // 원본은 오른쪽을 봄

    enum Pat { None, Charge, Claw, Fire }
    Pat current = Pat.None;
    int count;                              // 돌진+할퀴기 횟수
    float restUntil, stunUntilLocal;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health; Color baseColor;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb; SeriaController guard;
    Coroutine co; float chargeDir; bool chargeHit;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.linearVelocity = v;
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.velocity = v;
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>(); baseColor = sr.color;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.0f;
        health.isBoss = true; if (string.IsNullOrEmpty(health.bossId)) health.bossId = "CrimsonWolf";   // 중간 보스: 리젠 없음
        restUntil = Time.time + 1.5f;

        health.OnHurt += dir => { if (current == Pat.None) anim.Play("Hit", 0, 0f); };
        health.OnDied += () => { Cancel(); anim.speed = 1f; anim.Play("Death", 0, 0f); if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward); };
        health.OnStunned += () => { Cancel(); Vel = new Vector2(0, Vel.y); anim.Play("Hit", 0, 0f); restUntil = Time.time + 2.2f; };
        health.OnRespawned += () => { Cancel(); count = 0; anim.Play("Idle", 0, 0f); restUntil = Time.time + 1.5f; };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>(); guard = go.GetComponent<SeriaController>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float d) { if (d != 0) sr.flipX = spriteFacesLeft ? d > 0 : d < 0; }

    void Cancel()
    {
        if (co != null) StopCoroutine(co);
        co = null; current = Pat.None; sr.color = baseColor;
    }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { anim.speed = 0.3f; Vel = new Vector2(0, Vel.y); return; }
        if (current != Pat.None) return;                        // 공격 중엔 다른 패턴 안 함
        anim.speed = 1f;

        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (!alive) { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); return; }
        float dx = player.position.x - transform.position.x, dist = Mathf.Abs(dx);
        if (dist > detectRange || Mathf.Abs(player.position.y - transform.position.y) > 4f) { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); return; }
        Face(Mathf.Sign(dx));

        if (Time.time < restUntil)
        {
            // 쉬는 동안: 너무 멀면 천천히 다가옴
            if (dist > clawRange + 1.5f) { Vel = new Vector2(Mathf.Sign(dx) * walkSpeed, Vel.y); anim.Play("Walk"); }
            else { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); }
            return;
        }

        if (useFireball && count >= attacksBeforeFireball) co = StartCoroutine(Fireball());
        else if (dist > clawRange) co = StartCoroutine(Charge());
        else co = StartCoroutine(Claw());
    }

    void Done(bool countIt)
    {
        if (countIt) count++;
        current = Pat.None; co = null; sr.color = baseColor;
        anim.Play("Idle", 0, 0f);
        restUntil = Time.time + restTime;
    }

    // ── 돌진 ──
    IEnumerator Charge()
    {
        current = Pat.Charge; chargeHit = false;
        Vel = new Vector2(0, Vel.y);
        chargeDir = Facing;
        DamagePopup.ShowText(transform.position + new Vector3(0, 5.3f, 0), "!", new Color(1f, 0.3f, 0.2f));
        anim.Play("ChargeReady", 0, 0f);   // 몸을 낮추고 힘을 모음
        for (float t = 0; t < chargeWindup; t += Time.deltaTime)
        {
            sr.color = Color.Lerp(baseColor, new Color(1f, 0.55f, 0.5f), Mathf.PingPong(t * 8f, 1f));
            yield return null;
        }
        sr.color = baseColor;
        anim.Play("ChargeRun", 0, 0f);
        float end = Time.time + chargeMaxTime;
        while (Time.time < end && !WallAhead(chargeDir))
        {
            Vel = new Vector2(chargeDir * chargeSpeed, Vel.y);
            // 플레이어 앞(머리가 닿는 거리)까지 오면 멈춤 → 다음은 할퀴기로 이어짐
            if (player != null && (player.position.x - transform.position.x) * chargeDir < 2.8f) break;
            if (health.IsStunned) yield break;
            yield return null;
        }
        for (float t = 0; t < 0.25f; t += Time.deltaTime) { Vel = new Vector2(Mathf.MoveTowards(Vel.x, 0, 60f * Time.deltaTime), Vel.y); yield return null; }
        Vel = new Vector2(0, Vel.y);
        Done(true);
    }

    bool WallAhead(float dir)
    {
        Vector2 o = (Vector2)transform.position + new Vector2(0, 1f);
        foreach (var h in Physics2D.RaycastAll(o, new Vector2(dir, 0), 2.6f))
        {
            if (h.collider == body || h.collider.isTrigger) continue;
            if (playerRb != null && h.collider.attachedRigidbody == playerRb) continue;
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;
            return true;
        }
        return false;
    }

    // ── 할퀴기 ──
    IEnumerator Claw()
    {
        current = Pat.Claw;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        anim.Play("Claw", 0, 0f);
        yield return new WaitForSeconds(clawWindup);
        Vector2 center = (Vector2)transform.position + new Vector2(clawBoxOffset.x * dir, clawBoxOffset.y);
        foreach (var c in Physics2D.OverlapBoxAll(center, clawBoxSize, 0f))
        {
            if (playerRb == null || c.attachedRigidbody != playerRb) continue;
            if (guard != null && guard.TryBlock(transform.position, false)) break;       // 방어 가능, 패링(스턴)은 없음
            float before = playerHealth.CurrentHP;
            playerHealth.TakeDamage(clawDamage);
            if (playerHealth.CurrentHP < before) SetVel(playerRb, new Vector2(dir * 7f, 5f));
            break;
        }
        CameraShake.Shake(0.1f, 0.1f);
        yield return new WaitForSeconds(0.55f);
        Done(true);
    }

    // ── 파이어볼 ──
    IEnumerator Fireball()
    {
        current = Pat.Fire;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        DamagePopup.ShowText(transform.position + new Vector3(0, 5.3f, 0), "!!", new Color(1f, 0.6f, 0.1f));
        anim.Play("Hit", 0, 0f); anim.speed = 0.25f;                  // 고개를 낮추고 힘을 모음
        // 입 앞에 불덩이가 점점 커짐
        var charge = new GameObject("Fire Charge").AddComponent<SpriteRenderer>();
        charge.sprite = Resources.Load<Sprite>("Wolf/Wolf_Fireball"); charge.sortingOrder = sr.sortingOrder + 2; charge.flipX = dir < 0;
        Vector3 mouth = transform.position + new Vector3(dir * 3.2f, fireballHeight, 0);
        for (float t = 0; t < fireballWindup; t += Time.deltaTime)
        {
            float k = t / fireballWindup;
            charge.transform.position = mouth; charge.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.45f, k);
            charge.color = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.PingPong(t * 6f, 1f));
            sr.color = Color.Lerp(baseColor, new Color(1f, 0.7f, 0.4f), k * 0.6f);
            if (health.IsStunned || health.IsDead) { Destroy(charge.gameObject); yield break; }
            yield return null;
        }
        Destroy(charge.gameObject);
        sr.color = baseColor;
        WolfFireball.Launch(mouth, dir, fireballSpeed, fireballDamage, transform.position.y);
        CameraShake.Shake(0.15f, 0.12f);
        yield return new WaitForSeconds(0.5f);
        count = 0;
        Done(false);
    }

    // 몸 접촉 (돌진 중이면 돌진 데미지 + 패링 판정)
    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || playerHealth == null || playerHealth.IsDead || other.attachedRigidbody != playerRb) return;
        bool charging = current == Pat.Charge;
        if (charging && chargeHit) return;
        if (guard != null && guard.TryBlock(transform.position, charging))
        {
            if (charging)
            {
                chargeHit = true;
                health.Stun(guard.parryStunTime);                         // 패링 → 2초 스턴
                Vel = new Vector2(-chargeDir * 4f, 2f);
            }
            return;
        }
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(charging ? chargeDamage : touchDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)
        {
            float d = charging ? chargeDir : Mathf.Sign(player.position.x - transform.position.x);
            if (d == 0) d = 1;
            SetVel(playerRb, charging ? new Vector2(d * 10f, 6f) : new Vector2(d * 5f, 3f));
            if (charging) chargeHit = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        float d = (GetComponent<SpriteRenderer>() != null && GetComponent<SpriteRenderer>().flipX != spriteFacesLeft) ? -1 : 1;
        Gizmos.color = Color.red; Gizmos.DrawWireCube(transform.position + new Vector3(clawBoxOffset.x * d, clawBoxOffset.y), clawBoxSize);
        Gizmos.color = new Color(1, 0.6f, 0, 0.6f); Gizmos.DrawWireSphere(transform.position, clawRange);
    }
}

// 파이어볼: 천천히 일직선. 판정은 "숙인 높이 위 ~ 아주 높은 곳"까지라 숙이기로만 피할 수 있음 (방어 불가)
public class WolfFireball : MonoBehaviour
{
    float dir, speed, damage, groundY, traveled; bool done;
    SpriteRenderer sr;

    public static void Launch(Vector3 pos, float dir, float speed, float damage, float groundY)
    {
        var g = new GameObject("Wolf Fireball"); g.transform.position = pos;
        var f = g.AddComponent<WolfFireball>(); f.dir = dir; f.speed = speed; f.damage = damage; f.groundY = groundY;
    }

    void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>(); sr.sortingOrder = 30;
        sr.sprite = Resources.Load<Sprite>("Wolf/Wolf_Fireball"); sr.flipX = dir < 0;
    }

    void Update()
    {
        if (done) return;
        float step = speed * Time.deltaTime;
        transform.position += new Vector3(dir * step, 0, 0); traveled += step;
        transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 20f));   // 일렁임
        if (traveled > 22f) { Destroy(gameObject); return; }

        // 판정: 불덩이 머리 부분 x, 높이는 땅+0.7 ~ 땅+8 (숙이면 몸 높이 0.55 → 피함)
        float headX = transform.position.x + dir * 2.4f;           // 불덩이 머리(앞쪽 둥근 부분)
        Vector2 center = new Vector2(headX, groundY + 0.75f + 4f);
        foreach (var c in Physics2D.OverlapBoxAll(center, new Vector2(2.6f, 8f), 0f))
        {
            var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp == null || hp.IsDead) continue;
            float before = hp.CurrentHP;
            hp.TakeDamage(damage);
            if (hp.CurrentHP < before)
            {
#if UNITY_6000_0_OR_NEWER
                c.attachedRigidbody.linearVelocity = new Vector2(dir * 9f, 6f);
#else
                c.attachedRigidbody.velocity = new Vector2(dir * 9f, 6f);
#endif
                CameraShake.Shake(0.2f, 0.2f);
                StartCoroutine(Burst());
            }
            return;
        }
    }

    IEnumerator Burst()
    {
        done = true;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            transform.localScale = Vector3.one * (1f + t * 3f);
            sr.color = new Color(1, 1, 1, 1f - t / 0.25f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
