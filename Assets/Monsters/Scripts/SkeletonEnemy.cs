// 스켈레톤 병사 (검사 타입) - 멸망한 왕국 성곽1
//  평소: 제자리 근처를 어슬렁 / 플레이어 발견: 걸어서 다가옴
//  칼 닿는 거리: 멈춰서 칼을 치켜듦("!") → 앞으로 베기 (공격력 40)
//   · 방어(X)로 막을 수 있음 (칼 공격이라 패링 스턴은 없음)
//   · 뒤로 빠지거나 점프로 피할 수 있음
//  몸에 닿는 것만으로는 데미지 없음
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class SkeletonEnemy : MonoBehaviour
{
    [Header("수치")]
    public float slashDamage = 40f;
    public int expReward = 50;

    [Header("감지 · 이동")]
    public float detectRange = 8f;
    public float heightTolerance = 2.5f;
    public float walkSpeed = 1.4f;
    public float wanderRadius = 2f;

    [Header("베기")]
    public float attackRange = 1.7f;       // 이 거리 안이면 베기 시작
    public float windupTime = 0.5f;        // 칼을 치켜드는 시간 (피할 여유)
    public float swingFps = 14f;
    public int hitFrame = 1;               // 베기 동작 중 몇 번째 장면에서 판정 (0부터)
    public Vector2 hitBoxSize = new Vector2(2.2f, 1.7f);
    public float hitBoxForward = 1.0f;     // 몸 중심에서 앞쪽으로
    public float attackCooldown = 1.6f;

    public bool spriteFacesLeft = false;   // 원본 그림은 오른쪽을 봄

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float homeX, wanderTarget, nextWanderTime, nextAttackTime, hurtUntil;
    bool attacking, aggro; Coroutine attackCo; string curState;

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
        nextWanderTime = Time.time + Random.Range(1f, 3f);

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.2f;

        health.OnHurt += dir =>
        {
            aggro = true;
            if (attacking) return;                                   // 베는 중엔 끊기지 않음
            Vel = new Vector2(dir * 2.5f, Vel.y);                    // 살짝 밀림
            hurtUntil = Time.time + 0.3f;
            Play("Hit", true);
        };
        health.OnDied += () =>
        {
            CancelAttack(); Vel = Vector2.zero;
            Play("Death", true);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
        health.OnStunned += () => { CancelAttack(); Play("Idle", true); nextAttackTime = Time.time + 2f; };
        health.OnRespawned += () => { aggro = false; attacking = false; wanderTarget = homeX; Play("Idle", true); nextAttackTime = Time.time + 1f; };
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
    void Face(float dir) { if (dir != 0) sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0; }

    void Play(string state, bool restart = false)
    {
        if (!restart && curState == state) return;
        curState = state; anim.Play(state, 0, 0f);
    }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.speed = 0.3f; return; }
        anim.speed = 1f;
        if (attacking || Time.time < hurtUntil) return;

        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        float dx = alive ? player.position.x - transform.position.x : 0f;
        float dy = alive ? player.position.y - transform.position.y : 0f;
        float dist = Mathf.Abs(dx);
        bool canSee = alive && Mathf.Abs(dy) <= heightTolerance && (dist <= detectRange || (aggro && dist <= detectRange * 1.5f));

        float moveX = 0f;
        if (canSee)
        {
            Face(Mathf.Sign(dx));
            if (dist <= attackRange)
            {
                if (Time.time >= nextAttackTime) { attackCo = StartCoroutine(Attack()); return; }
            }
            else moveX = Mathf.Sign(dx);
        }
        else
        {
            aggro = false;
            if (wanderRadius > 0 && Time.time >= nextWanderTime)
            { wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius); nextWanderTime = Time.time + Random.Range(3f, 6f); }
            if (Mathf.Abs(wanderTarget - transform.position.x) > 0.15f) { moveX = Mathf.Sign(wanderTarget - transform.position.x); Face(moveX); }
        }

        float speed = canSee ? walkSpeed : walkSpeed * 0.5f;
        Vel = new Vector2(moveX * speed, Vel.y);
        Play(moveX != 0 ? "Walk" : "Idle");
        anim.speed = moveX != 0 && !canSee ? 0.6f : 1f;
    }

    IEnumerator Attack()
    {
        attacking = true;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;

        // 1) 칼을 치켜듦
        Play("AttackReady", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 2.0f, 0), "!", new Color(1f, 0.35f, 0.3f));
        yield return new WaitForSeconds(windupTime);

        // 2) 베기
        Play("AttackSwing", true);
        yield return new WaitForSeconds(hitFrame / swingFps);
        HitCheck(dir);
        yield return new WaitForSeconds(Mathf.Max(0.1f, (4 - hitFrame) / swingFps + 0.15f));

        Play("Idle", true);
        nextAttackTime = Time.time + attackCooldown;
        attacking = false; attackCo = null;
    }

    void CancelAttack()
    {
        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = null; attacking = false;
    }

    void HitCheck(float dir)
    {
        if (playerHealth == null || playerHealth.IsDead || playerRb == null) return;
        Vector2 center = (Vector2)transform.position + new Vector2(dir * hitBoxForward, hitBoxSize.y * 0.5f + 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, hitBoxSize, 0f))
        {
            if (c.attachedRigidbody != playerRb) continue;
            var guard = player.GetComponent<SeriaController>();
            if (guard != null && guard.TryBlock(transform.position, false)) return;   // 방어로 막음 (스턴 없음)
            float before = playerHealth.CurrentHP;
            playerHealth.TakeDamage(slashDamage);
            if (playerHealth.CurrentHP < before) SetVel(playerRb, new Vector2(dir * 6f, 4f));
            CameraShake.Shake(0.12f, 0.1f);
            return;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.8f, 0, 0.5f); Gizmos.DrawWireSphere(transform.position, detectRange);
        float d = GetComponent<SpriteRenderer>() != null && GetComponent<SpriteRenderer>().flipX != spriteFacesLeft ? -1f : 1f;
        Gizmos.color = new Color(1, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(transform.position + new Vector3(d * hitBoxForward, hitBoxSize.y * 0.5f + 0.1f, 0), hitBoxSize);
    }
}
