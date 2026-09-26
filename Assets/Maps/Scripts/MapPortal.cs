// 맵 끝의 이동 지점: 플레이어가 닿으면 다른 맵(씬)으로 이동
//  보스 맵: 살아 있는 보스(EnemyHealth.isBoss)가 있으면 포탈이 붉게 잠기고 나갈 수 없음 → 보스를 쓰러뜨리면 열림
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MapPortal : MonoBehaviour
{
    public string targetScene = "Map2_Boar";   // 이동할 씬 이름
    public string spawnSide = "Left";           // 도착한 맵에서 나타날 위치 (Spawn_Left / Spawn_Right)
    public bool showGlow = true;                // 은은한 빛 기둥 표시

    bool used;
    SpriteRenderer glow; float nextCheck, nextMsg; bool locked;

    // 이 맵에 아직 살아 있는 보스가 있는지
    public static bool BossAlive()
    {
#if UNITY_2023_1_OR_NEWER
        foreach (var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
#else
        foreach (var e in Object.FindObjectsOfType<EnemyHealth>())
#endif
            if (e.isBoss && !e.IsDead && e.gameObject.activeInHierarchy) return true;
        return false;
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.3f;
        bool was = locked; locked = BossAlive();
        if (glow != null)
        {
            glow.color = locked ? new Color(1f, 0.25f, 0.25f, 0.9f) : Color.white;
            if (was && !locked) DamagePopup.ShowText(transform.position + new Vector3(0, 2.6f, 0), "길이 열렸다", new Color(0.7f, 1f, 0.7f));
        }
    }

    void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (showGlow) BuildGlow();
    }

    void OnTriggerEnter2D(Collider2D other) => Touch(other);
    void OnTriggerStay2D(Collider2D other) => Touch(other);

    void Touch(Collider2D other)
    {
        if (used || other.attachedRigidbody == null || other.attachedRigidbody.name != "Seria") return;
        var hp = other.attachedRigidbody.GetComponent<PlayerHealth>();
        if (hp != null && hp.IsDead) return;
        if (BossAlive())                                           // 보스를 쓰러뜨리기 전엔 못 나감
        {
            if (Time.time >= nextMsg)
            {
                nextMsg = Time.time + 1.5f;
                DamagePopup.ShowText(other.attachedRigidbody.position + new Vector2(0, 2.2f), "보스를 쓰러뜨려야 나갈 수 있다!", new Color(1f, 0.45f, 0.4f));
            }
            var rb = other.attachedRigidbody;                       // 살짝 밀어냄
            float d = Mathf.Sign(rb.position.x - transform.position.x); if (d == 0) d = 1;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(d * 5f, 2f);
#else
            rb.velocity = new Vector2(d * 5f, 2f);
#endif
            return;
        }
        used = true;
        if (hp != null) PlayerHealth.CarryHP = hp.CurrentHP;   // 체력 유지
        SceneTransition.Go(targetScene, spawnSide);
    }

    // 맵 끝을 알려주는 반투명 빛 기둥
    void BuildGlow()
    {
        const int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float cx = 1f - Mathf.Abs(x - (w - 1) / 2f) / (w / 2f);      // 가운데가 밝게
            float cy = Mathf.Clamp01(1f - y / (float)h);                 // 아래가 밝게
            tex.SetPixel(x, y, new Color(1f, 0.95f, 0.8f, cx * cx * cy * 0.55f));
        }
        tex.Apply();
        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        var sr = g.AddComponent<SpriteRenderer>(); glow = sr;
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);   // 1 x 2 유닛
        sr.sortingOrder = -10;
        var box = GetComponent<BoxCollider2D>();
        g.transform.localPosition = new Vector3(box.offset.x, box.offset.y - box.size.y / 2f, 0);
        g.transform.localScale = new Vector3(1.2f, 1.3f, 1f);
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.4f);
        Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
    }
}
