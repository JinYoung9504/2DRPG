// 독버섯 AI
//  평소: 통통 튀며 어슬렁 / 발견: 통통 튀며 다가옴 (몸통 박치기 5)
//  가까우면(독 범위) 멈춰서 독가스 분사 → 맞으면 5초간 초당 5 데미지 (독)
//  몸통 박치기는 방어로 막으면 패링(스턴), 독가스는 방어로 막으면 BLOCK
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class MushroomEnemy : MonoBehaviour
{
    [Header("수치")]
    public float bodyDamage = 5f;          // 몸통 박치기
    public float poisonDps = 5f;           // 독: 초당 데미지
    public float poisonDuration = 5f;      // 독: 지속 시간
    public int expReward = 15;

    [Header("행동")]
    public float detectRange = 6f;
    public float gasRange = 2.6f;          // 이 안이면 독가스
    public float gasCooldown = 3.5f;
    public float moveSpeed = 1.3f;
    public float hopForce = 4f;            // 통통 튀는 높이
    public float wanderRadius = 1.5f;
    public bool spriteFacesLeft = false;   // 원본은 오른쪽(살짝 정면)을 봄

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float homeX, wanderTarget, nextWanderTime, nextGasTime, nextHopTime, stunUntil;
    bool gassing, wasGrounded = true;

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
        homeX = wanderTarget = transform.position.x;
        nextGasTime = Time.time + 1f;

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 0.6f;
        health.OnHurt += dir => { if (!gassing) { stunUntil = Time.time + 0.3f; Vel = new Vector2(dir * 3f, 2.5f); anim.Play("Hit", 0, 0f); } };
        health.OnDied += () =>
        {
            StopAllCoroutines(); gassing = false; anim.speed = 1f; anim.Play("Death", 0, 0f);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
        health.OnStunned += () => { StopAllCoroutines(); gassing = false; };
        health.OnRespawned += () => { gassing = false; anim.Play("Idle", 0, 0f); wanderTarget = homeX; };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float d) { if (d != 0) sr.flipX = spriteFacesLeft ? d > 0 : d < 0; }

    bool Grounded()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.1f;
        foreach (var h in Physics2D.RaycastAll(o, Vector2.down, 0.2f))
            if (h.collider != body && !h.collider.isTrigger && h.collider.attachedRigidbody != playerRb) return true;
        return false;
    }

    void Update()
    {
        if (health.IsDead) return;
        anim.speed = health.IsStunned ? 0.3f : 1f;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); return; }
        if (gassing || Time.time < stunUntil) return;

        bool grounded = Grounded();
        if (grounded && !wasGrounded) anim.Play("Land", 0, 0f);
        wasGrounded = grounded;

        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        float dx = alive ? player.position.x - transform.position.x : 0f;
        float dy = alive ? player.position.y - transform.position.y : 99f;
        bool see = alive && Mathf.Abs(dy) < 2.5f && Mathf.Abs(dx) <= detectRange;

        float move = 0f;
        if (see)
        {
            Face(Mathf.Sign(dx));
            if (Mathf.Abs(dx) <= gasRange && Time.time >= nextGasTime && grounded) { StartCoroutine(Gas()); return; }
            move = Mathf.Sign(dx);
        }
        else
        {
            if (Time.time >= nextWanderTime) { wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius); nextWanderTime = Time.time + Random.Range(3f, 6f); }
            if (Mathf.Abs(wanderTarget - transform.position.x) > 0.15f) { move = Mathf.Sign(wanderTarget - transform.position.x); Face(move); }
        }

        // 통통 튀며 이동
        if (move != 0 && grounded && Time.time >= nextHopTime)
        {
            Vel = new Vector2(move * moveSpeed * (see ? 1.4f : 1f), hopForce);
            nextHopTime = Time.time + (see ? 0.45f : 0.8f);
            anim.Play("Jump", 0, 0f);
        }
        else if (grounded && Time.time > nextHopTime - 0.2f) Vel = new Vector2(Mathf.MoveTowards(Vel.x, 0, 10f * Time.deltaTime), Vel.y);
        if (move == 0 && grounded && !anim.GetCurrentAnimatorStateInfo(0).IsName("Land")) anim.Play("Idle");
    }

    IEnumerator Gas()
    {
        gassing = true;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        anim.Play("Attack", 0, 0f);
        yield return new WaitForSeconds(0.45f);                 // 부풀어 오르는 준비 동작
        PoisonCloud.Spawn(transform.position + new Vector3(dir * 0.5f, 0, 0), dir, this);
        yield return new WaitForSeconds(0.7f);
        anim.Play("Idle", 0, 0f);
        nextGasTime = Time.time + gasCooldown;
        gassing = false;
    }

    // 독가스가 플레이어에 닿았을 때 (PoisonCloud 가 호출)
    public void GasHit()
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(transform.position, false)) return;
        playerHealth.ApplyPoison(poisonDps, poisonDuration);
    }

    // 몸통 박치기
    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || playerHealth == null || playerHealth.IsDead || other.attachedRigidbody != playerRb) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(transform.position, true))
        {
            health.Stun(guard.parryStunTime);
            Vel = new Vector2(Mathf.Sign(transform.position.x - player.position.x) * 4f, 3f);
            return;
        }
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(bodyDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)
            SetVel(playerRb, new Vector2(Mathf.Sign(player.position.x - transform.position.x) * 5f, 3f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.8f, 0, 0.5f); Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.6f); Gizmos.DrawWireSphere(transform.position, gasRange);
    }
}

// 독가스 구름: 앞으로 퍼지며 커졌다가 사라짐. 닿은 플레이어에게 독 (구름 1개당 1번)
public class PoisonCloud : MonoBehaviour
{
    MushroomEnemy owner; float dir; bool hit;
    const float Life = 1.4f;

    public static void Spawn(Vector3 pos, float dir, MushroomEnemy owner)
    {
        var g = new GameObject("Poison Cloud"); g.transform.position = pos;
        var c = g.AddComponent<PoisonCloud>(); c.owner = owner; c.dir = dir;
    }

    IEnumerator Start()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>(); sr.sortingOrder = 22;
        sr.sprite = Resources.Load<Sprite>("Mushroom/Mush_GasCloud");
        sr.flipX = dir < 0;
        Vector3 start = transform.position;
        for (float t = 0; t < Life; t += Time.deltaTime)
        {
            float k = t / Life;
            float grow = Mathf.Lerp(0.3f, 1.25f, Mathf.Sqrt(Mathf.Min(1f, k * 2f)));
            transform.localScale = new Vector3(grow, grow, 1);
            transform.position = start + new Vector3(dir * 1.3f * Mathf.Sqrt(k), 0.05f + 0.25f * k, 0);
            float wob = 1f + 0.05f * Mathf.Sin(t * 12f);
            transform.localScale = new Vector3(grow * wob, grow / wob, 1);
            sr.color = new Color(1, 1, 1, k < 0.15f ? k / 0.15f : k > 0.65f ? (1 - k) / 0.35f : 1f);
            if (!hit && k < 0.8f && owner != null && sr.sprite != null)
            {
                var b = sr.bounds;
                foreach (var c in Physics2D.OverlapBoxAll(b.center, b.size * 0.75f, 0f))
                    if (c.attachedRigidbody != null && c.attachedRigidbody.GetComponent<PlayerHealth>() != null) { hit = true; owner.GasHit(); break; }
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
