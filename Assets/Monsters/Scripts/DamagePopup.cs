// 데미지 숫자: 위로 떠오르며 사라짐
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    float life = 0.8f, t; TextMesh tm; Color c; Vector3 vel;

    static Font font;
    static Font GetFont()
    {
        if (font == null)
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return font;
    }

    public static void Show(Vector3 pos, float amount, Color? color = null)
        => ShowText(pos, Mathf.RoundToInt(amount).ToString(), color ?? new Color(1f, 0.9f, 0.3f));

    public static void ShowText(Vector3 pos, string text, Color color)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.font = GetFont(); tm.text = text;
        tm.fontSize = 64; tm.characterSize = 0.06f; tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>(); mr.material = tm.font.material; mr.sortingOrder = 200;
        go.AddComponent<DamagePopup>();
    }

    void Awake() { tm = GetComponent<TextMesh>(); c = tm.color; vel = new Vector3(0, 1.6f, 0); }

    void Update()
    {
        t += Time.deltaTime;
        transform.position += vel * Time.deltaTime;
        vel *= 0.92f;
        float k = t / life;
        tm.color = new Color(c.r, c.g, c.b, 1f - k * k);
        transform.localScale = Vector3.one * (k < 0.15f ? 1f + (0.15f - k) * 3f : 1f);   // 처음에 톡 튀게
        if (t >= life) Destroy(gameObject);
    }
}
