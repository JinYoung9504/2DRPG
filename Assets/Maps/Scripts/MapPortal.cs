// 맵 끝의 이동 지점: 플레이어가 닿으면 다른 맵(씬)으로 이동
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MapPortal : MonoBehaviour
{
    public string targetScene = "Map2_Boar";   // 이동할 씬 이름
    public string spawnSide = "Left";           // 도착한 맵에서 나타날 위치 (Spawn_Left / Spawn_Right)
    public bool showGlow = true;                // 은은한 빛 기둥 표시

    bool used;

    void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (showGlow) BuildGlow();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used || other.attachedRigidbody == null || other.attachedRigidbody.name != "Seria") return;
        var hp = other.attachedRigidbody.GetComponent<PlayerHealth>();
        if (hp != null && hp.IsDead) return;
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
        var sr = g.AddComponent<SpriteRenderer>();
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
