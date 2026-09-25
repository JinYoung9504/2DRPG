// 나무 정령 (고정형)
//  제자리에서 움직이지 않음 (플레이어 쪽으로 몸만 돌림)
//  공격 범위 안에 들어오면: 준비 동작("!") → 땅을 따라 긴 가지를 뻗어 휘두름 → 끝에서 땅이 터짐 → 가지를 거둠
//   · 가지는 땅 높이로 뻗으므로 점프로 피할 수 있음 / 방어로 막을 수 있음(BLOCK)
//  몸에 닿으면 접촉 데미지 (방어로 막으면 패링 → 스턴)
//  공격 중에는 맞아도 멈추지 않음(슈퍼아머)
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class TreeEntEnemy : MonoBehaviour
{
    [Header("수치 (임시)")]
    public float branchDamage = 15f;
    public float touchDamage = 3f;
    public int expReward = 40;

    [Header("공격")]
    public float detectRange = 9f;       // 이 안에 들어오면 플레이어 쪽을 바라봄
    public float attackRange = 7f;       // 이 안이면 가지 공격
    public float attackCooldown = 3f;
    public float branchLength = 7f;      // 가지가 뻗는 최대 길이
    public float branchHeight = 0.9f;    // 가지 판정 높이 (땅에서부터) → 점프로 피할 수 있음
    public float extendTime = 0.35f, holdTime = 0.25f, retractTime = 0.3f;

    public bool spriteFacesLeft = false; // 원본 그림은 오른쪽을 봄

    Animator anim; SpriteRenderer sr; EnemyHealth health; Collider2D[] cols;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float nextAttackTime; bool attacking; Coroutine attackCo;
    SpriteRenderer branch;

    void Awake()
    {
        anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>(); cols = GetComponents<Collider2D>();
        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.1f;
        nextAttackTime = Time.time + 1.5f;

        health.OnHurt += dir => { if (!attacking) { anim.Play("Hit", 0, 0f); } };
        health.OnDied += () =>
        {
            CancelAttack();
            anim.Play("Death", 0, 0f);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
        health.OnStunned += () => { CancelAttack(); anim.Play("Idle", 0, 0f); nextAttackTime = Time.time + 2.5f; };
        health.OnRespawned += () => { anim.Play("Idle", 0, 0f); nextAttackTime = Time.time + 1.5f; };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null) foreach (var c in cols) if (!c.isTrigger) Physics2D.IgnoreCollision(pc, c);
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float dir) { if (dir != 0) sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0; }

    void Update()
    {
        if (health.IsDead) return;
        anim.speed = health.IsStunned ? 0.3f : 1f;
        if (health.IsStunned || attacking) return;

        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (!alive) return;
        float dx = player.position.x - transform.position.x, dy = player.position.y - transform.position.y;
        if (Mathf.Abs(dy) > 3f || Mathf.Abs(dx) > detectRange) return;
        Face(Mathf.Sign(dx));
        if (Mathf.Abs(dx) <= attackRange && Time.time >= nextAttackTime) attackCo = StartCoroutine(Attack());
    }

    IEnumerator Attack()
    {
        attacking = true;
        float dir = Facing;
        // 1) 준비
        DamagePopup.ShowText(transform.position + new Vector3(0, 3.3f, 0), "!", new Color(0.6f, 1f, 0.4f));
        anim.Play("AttackStart", 0, 0f);
        yield return new WaitForSeconds(0.6f);

        // 2) 가지 뻗기 (땅을 따라)
        EnsureBranch();
        Vector3 root = transform.position + new Vector3(dir * 0.9f, 0.35f, 0);
        branch.transform.position = root;
        branch.flipX = dir < 0;
        branch.enabled = true;
        float baseLen = branch.sprite != null ? branch.sprite.bounds.size.x : 4f;
        bool hitDone = false;
        float Len(float k) => Mathf.Max(0.01f, branchLength * k);
        for (float t = 0; t < extendTime; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / extendTime) * (1f - t / extendTime);   // 빠르게 뻗다가 감속
            SetBranch(root, dir, Len(k), baseLen);
            if (!hitDone) hitDone = HitCheck(root, dir, Len(k));
            yield return null;
        }
        SetBranch(root, dir, branchLength, baseLen);
        if (!hitDone) hitDone = HitCheck(root, dir, branchLength);

        // 3) 끝에서 땅이 터짐
        EntImpact.Play(root + new Vector3(dir * branchLength, -0.35f, 0));
        CameraShake.Shake(0.15f, 0.12f);
        for (float t = 0; t < holdTime; t += Time.deltaTime)
        {
            if (!hitDone) hitDone = HitCheck(root, dir, branchLength);
            yield return null;
        }

        // 4) 거두기
        for (float t = 0; t < retractTime; t += Time.deltaTime) { SetBranch(root, dir, Len(1f - t / retractTime), baseLen); yield return null; }
        branch.enabled = false;

        // 5) 마무리 동작
        anim.Play("AttackEnd", 0, 0f);
        yield return new WaitForSeconds(0.6f);
        anim.Play("Idle", 0, 0f);
        nextAttackTime = Time.time + attackCooldown;
        attacking = false; attackCo = null;
    }

    void CancelAttack()
    {
        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = null; attacking = false;
        if (branch != null) branch.enabled = false;
    }

    void EnsureBranch()
    {
        if (branch != null) return;
        var g = new GameObject("Branch");
        branch = g.AddComponent<SpriteRenderer>();
        branch.sprite = Resources.Load<Sprite>("Ent/Ent_Branch");
        branch.sortingOrder = sr.sortingOrder + 1;
    }

    // 가지 길이 조절 (피벗이 뿌리 쪽이라 가로로 늘어남)
    void SetBranch(Vector3 root, float dir, float len, float baseLen)
    {
        branch.transform.position = root;
        branch.transform.localScale = new Vector3(len / baseLen, 1f, 1f);
        branch.transform.rotation = Quaternion.identity;
        // 왼쪽을 볼 때는 flipX 로 뿌리 기준 왼쪽으로 뻗음
    }

    // 뿌리 ~ 가지 끝 사이, 땅에서 branchHeight 높이까지가 판정
    bool HitCheck(Vector3 root, float dir, float len)
    {
        if (playerHealth == null || playerHealth.IsDead) return false;
        Vector2 center = new Vector2(root.x + dir * len / 2f, transform.position.y + branchHeight / 2f);
        foreach (var c in Physics2D.OverlapBoxAll(center, new Vector2(len, branchHeight), 0f))
        {
            if (c.attachedRigidbody != playerRb || playerRb == null) continue;
            var guard = player.GetComponent<SeriaController>();
            if (guard != null && guard.TryBlock(transform.position, false)) return true;       // 방어로 막음
            float before = playerHealth.CurrentHP;
            playerHealth.TakeDamage(branchDamage);
            if (playerHealth.CurrentHP < before)
            {
#if UNITY_6000_0_OR_NEWER
                playerRb.linearVelocity = new Vector2(dir * 7f, 6f);
#else
                playerRb.velocity = new Vector2(dir * 7f, 6f);
#endif
            }
            return true;
        }
        return false;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || playerHealth == null || playerHealth.IsDead || other.attachedRigidbody != playerRb) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(transform.position, true)) { health.Stun(guard.parryStunTime); return; }   // 패링
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(touchDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)
        {
            float d = Mathf.Sign(player.position.x - transform.position.x); if (d == 0) d = 1;
#if UNITY_6000_0_OR_NEWER
            playerRb.linearVelocity = new Vector2(d * 6f, 4f);
#else
            playerRb.velocity = new Vector2(d * 6f, 4f);
#endif
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.5f); Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

// 가지 끝 땅 충격 이펙트
public class EntImpact : MonoBehaviour
{
    public static void Play(Vector3 pos) { var g = new GameObject("Ent Impact"); g.transform.position = pos; g.AddComponent<EntImpact>(); }
    IEnumerator Start()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>(); sr.sortingOrder = 23;
        var f = Resources.LoadAll<Sprite>("Ent/Ent_Impact");
        System.Array.Sort(f, (a, b) => Idx(a.name).CompareTo(Idx(b.name)));
        foreach (var s in f) { sr.sprite = s; yield return new WaitForSeconds(1f / 16f); }
        Destroy(gameObject);
    }
    static int Idx(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }
}
