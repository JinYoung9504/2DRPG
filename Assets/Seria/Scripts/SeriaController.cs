// 세리아 조작 스크립트
// ─────────────────────────────────────────────
//  ← / →  : 이동
//  Alt    : 점프 (짧게 누르면 낮게, 길게 누르면 높게)
//  Ctrl   : 기본 공격 (공격 중에 한 번 더 누르면 강공격으로 이어짐)
//  Space  : 강공격
//  Shift  : 스킬
//  H / X  : 피격 / 사망 테스트
//  ※ 모든 키는 Inspector 창에서 바꿀 수 있습니다.
// ─────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class SeriaController : MonoBehaviour
{
    [Header("키 설정")]
    public KeyCode leftKey = KeyCode.LeftArrow;
    public KeyCode rightKey = KeyCode.RightArrow;
    public KeyCode jumpKey = KeyCode.LeftAlt;
    public KeyCode jumpKey2 = KeyCode.RightAlt;
    public KeyCode attackKey = KeyCode.LeftControl;
    public KeyCode attackKey2 = KeyCode.RightControl;
    public KeyCode heavyKey = KeyCode.Space;
    public KeyCode skillKey = KeyCode.LeftShift;
    public KeyCode hitTestKey = KeyCode.H;
    public KeyCode dieTestKey = KeyCode.X;

    [Header("이동")]
    public float moveSpeed = 4f;          // 초당 이동 거리
    public float acceleration = 40f;      // 클수록 즉시 최고속도에 도달
    public float airControl = 0.7f;       // 공중에서 방향 조절 정도 (0~1)

    [Header("점프")]
    public float jumpForce = 11f;
    public float jumpCutMultiplier = 0.5f; // 점프 키를 일찍 떼면 상승 속도에 곱해짐
    public float coyoteTime = 0.1f;        // 발판에서 떨어진 직후에도 점프 허용하는 시간
    public float jumpBufferTime = 0.1f;    // 착지 직전에 누른 점프를 기억하는 시간
    public LayerMask groundLayer = ~0;

    [Header("이펙트 (비워두면 표시 안 함)")]
    public Sprite slashFx;
    public Sprite skillFx;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D col;
    bool dead;
    float coyoteCounter, jumpBufferCounter;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    // 발밑에 바닥이 있는지 확인
    bool IsGrounded()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        foreach (var h in Physics2D.RaycastAll(origin, Vector2.down, 0.2f, groundLayer))
            if (h.collider != col) return true;
        return false;
    }

    // 공격·스킬·피격 중인지 (이때는 지상에서 못 움직임)
    bool Busy()
    {
        var s = anim.GetCurrentAnimatorStateInfo(0);
        return s.IsName("Attack1") || s.IsName("Attack2") || s.IsName("Skill") || s.IsName("Hit");
    }

    // ── 입력 처리: 새 Input System / 예전 Input Manager 둘 다 지원 ──
#if ENABLE_INPUT_SYSTEM
    static Key ToKey(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.Return: return Key.Enter;
        }
        string n = k.ToString();
        if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
        return Enum.TryParse(n, out Key key) ? key : Key.None;
    }
    static UnityEngine.InputSystem.Controls.KeyControl K(KeyCode k)
    {
        var kb = Keyboard.current; var key = ToKey(k);
        return (kb == null || key == Key.None) ? null : kb[key];
    }
    static bool Down(KeyCode k) { var c = K(k); return c != null && c.wasPressedThisFrame; }
    static bool Hold(KeyCode k) { var c = K(k); return c != null && c.isPressed; }
    static bool Up(KeyCode k) { var c = K(k); return c != null && c.wasReleasedThisFrame; }
#else
    static bool Down(KeyCode k) => Input.GetKeyDown(k);
    static bool Hold(KeyCode k) => Input.GetKey(k);
    static bool Up(KeyCode k)   => Input.GetKeyUp(k);
#endif
    bool Pressed(KeyCode a, KeyCode b = KeyCode.None) => Down(a) || (b != KeyCode.None && Down(b));
    bool Held(KeyCode a, KeyCode b = KeyCode.None) => Hold(a) || (b != KeyCode.None && Hold(b));
    bool Released(KeyCode a, KeyCode b = KeyCode.None) => Up(a) || (b != KeyCode.None && Up(b));

    void Start()
    {
        // 흔한 설정 문제를 Console 에 알려줌
        if (rb.bodyType != RigidbodyType2D.Dynamic)
            Debug.LogWarning("[Seria] Rigidbody 2D 의 Body Type 이 Dynamic 이 아닙니다. Dynamic 으로 바꾸세요.");
        if (anim.runtimeAnimatorController == null)
            Debug.LogWarning("[Seria] Animator 에 Controller 가 없습니다. Tools > Seria > 애니메이션 만들기 를 다시 실행하세요.");
        if (rb.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionX))
            Debug.LogWarning("[Seria] Rigidbody 2D 의 Freeze Position X 가 켜져 있습니다. 꺼주세요.");
    }

    [Header("디버그")]
    public bool showDebug = true;   // 화면 왼쪽 위에 상태 표시
    float dbgX; bool dbgGround, dbgBusy;
    void OnGUI()
    {
        if (!showDebug) return;
        GUI.Label(new Rect(10, 10, 600, 120),
            $"Input X: {dbgX}   Grounded: {dbgGround}   Busy: {dbgBusy}\n" +
            $"Velocity: {Vel}   State: {(anim.runtimeAnimatorController ? CurrentState() : "NO CONTROLLER")}");
    }
    string CurrentState()
    {
        foreach (var n in new[] { "Idle", "Walk", "Jump", "Fall", "Attack1", "Attack2", "Skill", "Hit", "Die" })
            if (anim.GetCurrentAnimatorStateInfo(0).IsName(n)) return n;
        return "?";
    }

    void Update()
    {
        if (dead) return;

        bool grounded = IsGrounded();
        bool busy = Busy();

        // ── 좌우 입력 ──
        float x = 0;
        if (Held(leftKey)) x -= 1;
        if (Held(rightKey)) x += 1;
        if (busy && grounded) x = 0;                 // 지상 공격 중엔 제자리
        if (x != 0 && !busy) sr.flipX = x < 0;       // 원본 그림은 오른쪽을 봄

        // ── 점프 타이밍 계산 ──
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        jumpBufferCounter = Pressed(jumpKey, jumpKey2) ? jumpBufferTime : jumpBufferCounter - Time.deltaTime;

        Vector2 v = Vel;

        // ── 이동 (가속도 적용) ──
        float control = grounded ? 1f : airControl;
        v.x = Mathf.MoveTowards(v.x, x * moveSpeed, acceleration * control * Time.deltaTime);

        // ── 점프 ──
        if (jumpBufferCounter > 0 && coyoteCounter > 0 && !busy)
        {
            v.y = jumpForce;
            jumpBufferCounter = 0;
            coyoteCounter = 0;
        }
        // 점프 키를 일찍 떼면 낮게 점프
        if (Released(jumpKey, jumpKey2) && v.y > 0)
            v.y *= jumpCutMultiplier;

        Vel = v;

        // ── 공격 ──
        if (Pressed(attackKey, attackKey2)) anim.SetTrigger("Attack");
        if (Pressed(heavyKey)) { anim.SetTrigger("Heavy"); StartCoroutine(Fx(slashFx, 0.25f, new Vector2(0.6f, 0.7f), 0.25f)); }
        if (Pressed(skillKey)) { anim.SetTrigger("Skill"); StartCoroutine(Fx(skillFx, 0.5f, new Vector2(1.0f, 0.7f), 0.4f)); }

        // ── 테스트용 ──
        if (Pressed(hitTestKey)) anim.SetTrigger("Hit");
        if (Pressed(dieTestKey)) { anim.SetTrigger("Die"); dead = true; Vel = Vector2.zero; }

        dbgX = x; dbgGround = grounded; dbgBusy = busy;

        // ── 애니메이터에 상태 전달 ──
        anim.SetFloat("Speed", Mathf.Abs(x));
        anim.SetFloat("VelY", Vel.y);
        anim.SetBool("Grounded", grounded);
    }

    // 간단한 이펙트: delay 후 캐릭터 앞에 스프라이트를 띄우고 서서히 사라지게
    IEnumerator Fx(Sprite sprite, float delay, Vector2 offset, float life)
    {
        if (sprite == null) yield break;
        yield return new WaitForSeconds(delay);
        float dir = sr.flipX ? -1 : 1;
        var go = new GameObject("FX");
        go.transform.position = transform.position + new Vector3(offset.x * dir, offset.y, 0);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.flipX = sr.flipX; r.sortingOrder = sr.sortingOrder + 1;
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            r.color = new Color(1, 1, 1, 1 - t / life);
            yield return null;
        }
        Destroy(go);
    }
}