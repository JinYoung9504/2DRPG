// 스켈레톤 궁수 (원거리) - 멸망한 왕국 성곽2
//  평소: 제자리 근처를 어슬렁 / 플레이어 발견: 사거리까지 다가옴, 너무 가까우면 뒤로 물러남
//  사거리 안: 활을 당김 → 화살 발사 (공격력 25, 가슴 높이로 곧게 날아감)
//   · 방어(X)로 막을 수 있음 (스턴 없음) / 숙이기(↓)나 점프로 피할 수 있음
//  공격 간격이 검사 스켈레톤보다 조금 짧음 (검사 약 2.5초 → 궁수 약 1.8초)
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class SkeletonArcherEnemy : MonoBehaviour
{
    [Header("수치")]
    public float arrowDamage = 25f;
    public int expReward = 100;

    [Header("감지 · 이동")]
    public float detectRange = 11f;
    public float heightTolerance = 2.5f;
    public float walkSpeed = 1.3f;
    public float wanderRadius = 2f;
    public float shootRange = 8f;          // 이 거리 안이면 쏨
    public float keepAwayRange = 2.5f;     // 이보다 가까우면 뒤로 물러남

    [Header("활")]
    public float drawFps = 12f;            // 활 당기는 동작 속도 (5장)
    public float releaseTime = 0.25f;      // 쏜 뒤 자세
    public float attackCooldown = 1.1f;
    public float arrowSpeed = 11f;
    public Vector2 arrowOffset = new Vector2(0.75f, 0.95f);   // 발 기준 화살이 나가는 위치 (서 있는 세리아 가슴 높이)

    public bool spriteFacesLeft = false;   // 원본 그림은 오른쪽을 봄

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; StageBounds stage;
    float homeX, wanderTarget, nextWanderTime, nextAttackTime, hurtUntil;
    bool attacking, aggro; Coroutine attackCo; string curState;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        homeX = wanderTarget = transform.position.x;
        nextWanderTime = Time.time + Random.Range(1f, 3f);
        nextAttackTime = Time.time + 0.8f;

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 0.9f;

        health.OnHurt += dir =>
        {
            aggro = true;
            if (attacking) return;                                   // 쏘는 중엔 끊기지 않음
            Vel = new Vector2(dir * 2.5f, Vel.y);
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
#if UNITY_2023_1_OR_NEWER
        stage = Object.FindFirstObjectByType<StageBounds>();
#else
        stage = Object.FindObjectOfType<StageBounds>();
#endif
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>();
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

        float moveX = 0f; bool backing = false;
        if (canSee)
        {
            float toward = Mathf.Sign(dx);
            if (dist < keepAwayRange && CanMove(-toward)) { moveX = -toward; backing = true; }   // 너무 가까우면 물러남
            else if (dist <= shootRange)
            {
                Face(toward);
                if (Time.time >= nextAttackTime) { attackCo = StartCoroutine(Shoot()); return; }
            }
            else moveX = toward;
            Face(toward);                                             // 물러날 때도 플레이어를 봄
        }
        else
        {
            aggro = false;
            if (wanderRadius > 0 && Time.time >= nextWanderTime)
            { wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius); nextWanderTime = Time.time + Random.Range(3f, 6f); }
            if (Mathf.Abs(wanderTarget - transform.position.x) > 0.15f) { moveX = Mathf.Sign(wanderTarget - transform.position.x); Face(moveX); }
        }

        float speed = !canSee ? walkSpeed * 0.5f : backing ? walkSpeed * 0.8f : walkSpeed;
        Vel = new Vector2(moveX * speed, Vel.y);
        Play(moveX != 0 ? "Walk" : "Idle");
        anim.speed = moveX != 0 && !canSee ? 0.6f : 1f;
    }

    bool CanMove(float dir)
    {
        if (stage == null) return true;
        float x = transform.position.x + dir * 0.8f;
        return x > stage.left + 0.5f && x < stage.right - 0.5f;
    }

    IEnumerator Shoot()
    {
        attacking = true;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;

        Play("AttackDraw", true);                                    // 활 당기기
        yield return new WaitForSeconds(5f / drawFps);

        Play("AttackRelease", true);                                 // 발사
        Vector3 from = transform.position + new Vector3(dir * arrowOffset.x, arrowOffset.y, 0);
        SkeletonArrow.Fire(from, dir, arrowSpeed, arrowDamage, sr.sortingOrder + 2);
        yield return new WaitForSeconds(releaseTime);

        Play("Idle", true);
        nextAttackTime = Time.time + attackCooldown;
        attacking = false; attackCo = null;
    }

    void CancelAttack()
    {
        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = null; attacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.8f, 0, 0.5f); Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1, 0.3f, 0.2f, 0.6f); Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}

// 스켈레톤 궁수의 화살: 곧게 날아가 플레이어에 맞으면 데미지, 벽·땅에 닿으면 사라짐
public class SkeletonArrow : MonoBehaviour
{
    float dir, speed, damage, life;
    SpriteRenderer sr;
    static readonly Vector2 HitSize = new Vector2(0.6f, 0.14f);

    public static void Fire(Vector3 from, float dir, float speed, float damage, int order)
    {
        var go = new GameObject("Skeleton Arrow");
        go.transform.position = from;
        var a = go.AddComponent<SkeletonArrow>();
        a.dir = dir; a.speed = speed; a.damage = damage;
        a.sr = go.AddComponent<SpriteRenderer>();
        a.sr.sprite = Resources.Load<Sprite>("Archer/Archer_Arrow");
        a.sr.flipX = dir < 0; a.sr.sortingOrder = Mathf.Max(order, 22);
    }

    void Update()
    {
        float dt = Time.deltaTime; life += dt;
        transform.position += new Vector3(dir * speed * dt, 0, 0);
        Vector2 tip = (Vector2)transform.position + new Vector2(dir * 0.3f, 0);
        foreach (var c in Physics2D.OverlapBoxAll(tip, HitSize, 0f))
        {
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;           // 몬스터는 통과
            var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp != null)
            {
                if (hp.IsDead) continue;
                var guard = hp.GetComponent<SeriaController>();
                if (guard != null && guard.TryBlock(transform.position, false)) { Destroy(gameObject); return; }   // 방어로 막음
                float before = hp.CurrentHP;
                hp.TakeDamage(damage);
                if (hp.CurrentHP < before)
                {
#if UNITY_6000_0_OR_NEWER
                    c.attachedRigidbody.linearVelocity = new Vector2(dir * 5f, 3f);
#else
                    c.attachedRigidbody.velocity = new Vector2(dir * 5f, 3f);
#endif
                }
                Destroy(gameObject); return;
            }
            if (!c.isTrigger) { Destroy(gameObject); return; }                     // 벽·땅
        }
        if (life > 2.5f) { sr.color = new Color(1, 1, 1, Mathf.Max(0, 1f - (life - 2.5f) * 4f)); if (life > 2.75f) Destroy(gameObject); }
    }
}
