// 보스 체력바: 보스(늑대 · 타락한 나무정령 · 뱀파이어 · 아타칸)와 마주치면 화면 위쪽에 크게 표시
//  - 이름 + 체력 숫자 + 깎인 만큼 천천히 줄어드는 흰 잔상
//  - 보스 머리 위의 작은 체력바는 숨김
//  - 아타칸이 부른 소환수(보스가 아님)는 표시하지 않음
using UnityEngine;
using UnityEngine.UI;

public class BossHPBar : MonoBehaviour
{
    public float showRange = 16f;           // 세리아와 이 거리 안이면 표시
    GameObject root; Image fill, trail; Text nameText, hpText; CanvasGroup group;
    EnemyHealth boss; float trailAmount = 1f, nextFind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var g = new GameObject("Boss HP Bar"); DontDestroyOnLoad(g); g.AddComponent<BossHPBar>();
    }

    static string NameOf(EnemyHealth e)
    {
        if (e.GetComponent<AtakhanBoss>() != null) return "최종 보스  아타칸";
        if (e.GetComponent<VampireBoss>() != null) return "뱀파이어";
        if (e.GetComponent<TreeBossEnemy>() != null) return "타락한 거대 나무정령";
        if (e.GetComponent<WolfBoss>() != null) return "대형 마수 늑대";
        return string.IsNullOrEmpty(e.bossId) ? e.name : e.bossId;
    }

    void Build()
    {
        var c = gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 120;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        root = new GameObject("Bar", typeof(RectTransform)); root.transform.SetParent(transform, false);
        var rt = (RectTransform)root.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -40); rt.sizeDelta = new Vector2(1000, 90);
        group = root.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;

        var font = DialogueUI.KoreanFont();
        nameText = Txt(rt, "Name", new Vector2(0, -2), new Vector2(1000, 40), 32, new Color(1f, 0.85f, 0.6f), font, TextAnchor.MiddleCenter);
        var frame = Img(rt, "Frame", new Vector2(0, -46), new Vector2(1000, 36), new Color(0.85f, 0.7f, 0.45f));
        var back = Img(frame, "Back", Vector2.zero, new Vector2(992, 28), new Color(0.08f, 0.04f, 0.06f));
        back.pivot = new Vector2(0.5f, 0.5f); back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
        trail = Filled(back, "Trail", new Color(1f, 0.95f, 0.85f, 0.85f));
        fill = Filled(back, "Fill", new Color(0.78f, 0.1f, 0.2f));
        hpText = Txt(back, "HP", Vector2.zero, new Vector2(992, 28), 20, Color.white, font, TextAnchor.MiddleCenter);
        ((RectTransform)hpText.transform).anchorMin = ((RectTransform)hpText.transform).anchorMax = ((RectTransform)hpText.transform).pivot = new Vector2(0.5f, 0.5f);
        root.SetActive(false);
    }

    static RectTransform Img(RectTransform p, string n, Vector2 pos, Vector2 size, Color c)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(p, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return rt;
    }
    static Image Filled(RectTransform p, string n, Color c)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(p, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(2, 2); rt.offsetMax = new Vector2(-2, -2);
        var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.Apply();
        i.sprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        i.type = Image.Type.Filled; i.fillMethod = Image.FillMethod.Horizontal; i.fillOrigin = 0; i.fillAmount = 1f;
        return i;
    }
    static Text Txt(RectTransform p, string n, Vector2 pos, Vector2 size, int fs, Color c, Font f, TextAnchor a)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(p, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        var t = rt.gameObject.AddComponent<Text>(); t.font = f; t.fontSize = fs; t.color = c; t.alignment = a; t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
        var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.85f); o.effectDistance = new Vector2(2, -2);
        return t;
    }

    void Start() { Build(); }

    void Update()
    {
        if (root == null) return;
        var seria = GameObject.Find("Seria");
        if (Time.unscaledTime >= nextFind || boss == null || boss.IsDead)
        {
            nextFind = Time.unscaledTime + 0.3f;
            EnemyHealth best = null; float bd = float.MaxValue;
#if UNITY_2023_1_OR_NEWER
            foreach (var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
#else
            foreach (var e in FindObjectsOfType<EnemyHealth>())
#endif
            {
                if (!e.isBoss || e.IsDead || !e.gameObject.activeInHierarchy) continue;
                var r = e.GetComponent<SpriteRenderer>(); if (r != null && !r.enabled) continue;    // 아직 등장 전 (아타칸)
                HideSmallBar(e);
                float d = seria != null ? Mathf.Abs(seria.transform.position.x - e.transform.position.x) : 0f;
                if (d < bd) { bd = d; best = e; }
            }
            if (best != boss) { boss = best; if (boss != null) trailAmount = boss.CurrentHP / Mathf.Max(1f, boss.maxHP); }
            if (boss != null && seria != null && bd > showRange && boss.CurrentHP >= boss.maxHP) boss = null;   // 아직 멀리 있고 싸우기 전
        }

        bool show = boss != null && !boss.IsDead;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;
        float k = Mathf.Clamp01(boss.CurrentHP / Mathf.Max(1f, boss.maxHP));
        fill.fillAmount = k;
        trailAmount = trailAmount > k ? Mathf.MoveTowards(trailAmount, k, Time.unscaledDeltaTime * 0.35f) : k;
        trail.fillAmount = trailAmount;
        nameText.text = NameOf(boss);
        hpText.text = Mathf.CeilToInt(boss.CurrentHP) + " / " + Mathf.CeilToInt(boss.maxHP);
    }

    static void HideSmallBar(EnemyHealth e)
    {
        var bar = e.transform.Find("HP Bar");
        if (bar != null && bar.localScale != Vector3.zero) bar.localScale = Vector3.zero;
    }
}
