// 검은 화면 오프닝: 대사가 한 줄씩 타자 치듯 나타남
//  클릭 · 터치 · Enter · Space · Ctrl : 글자 바로 전부 표시 → 한 번 더 누르면 다음 줄
//  오른쪽 위 "건너뛰기" 또는 Esc : 오프닝 전체 건너뛰기
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PrologueScreen : MonoBehaviour
{
    PrologueData data; Action onDone;
    Text nameText, bodyText, nextMark; CanvasGroup group, textGroup;
    bool typing, advance, skip;

    public static void Play(Action onDone)
    {
        var data = Resources.Load<PrologueData>("Story/Prologue");
        if (data == null) { data = ScriptableObject.CreateInstance<PrologueData>(); }
        if (data.lines == null || data.lines.Length == 0) { onDone?.Invoke(); return; }
        var go = new GameObject("Prologue");
        var p = go.AddComponent<PrologueScreen>(); p.data = data; p.onDone = onDone;
    }

    void Start() { Build(); StartCoroutine(Run()); }

    void Build()
    {
        var cgo = new GameObject("Prologue Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        group = cgo.AddComponent<CanvasGroup>(); group.alpha = 0f;

        // 검은 바탕 (전체가 클릭 영역)
        var bg = UIHelper.Stretch("Black", cgo.transform).gameObject.AddComponent<Image>(); bg.color = Color.black;
        var click = bg.gameObject.AddComponent<Button>(); click.transition = Selectable.Transition.None;
        click.onClick.AddListener(() => advance = true);

        var tg = new GameObject("Text", typeof(RectTransform)); tg.transform.SetParent(cgo.transform, false);
        var trt = (RectTransform)tg.transform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.sizeDelta = new Vector2(1500, 500);
        textGroup = tg.AddComponent<CanvasGroup>(); textGroup.blocksRaycasts = false;

        nameText = Txt(trt, "Name", new Vector2(0, 110), new Vector2(1500, 60), 40, new Color(1f, 0.82f, 0.55f));
        bodyText = Txt(trt, "Body", new Vector2(0, -10), new Vector2(1500, 260), 44, new Color(0.93f, 0.93f, 0.95f));
        bodyText.lineSpacing = 1.25f;
        nextMark = Txt(trt, "Next", new Vector2(0, -190), new Vector2(100, 50), 30, new Color(1f, 0.8f, 0.5f)); nextMark.text = "▼";

        // 건너뛰기
        var sk = new GameObject("Skip", typeof(RectTransform)); sk.transform.SetParent(cgo.transform, false);
        var srt = (RectTransform)sk.transform; srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 1);
        srt.anchoredPosition = new Vector2(-40, -30); srt.sizeDelta = new Vector2(220, 64);
        var simg = sk.AddComponent<Image>(); simg.color = new Color(1, 1, 1, 0.08f);
        var sb = sk.AddComponent<Button>(); sb.onClick.AddListener(() => skip = true);
        var st = Txt(srt, "Label", Vector2.zero, srt.sizeDelta, 30, new Color(1, 1, 1, 0.75f)); st.text = "건너뛰기 ▶▶";
        st.rectTransform.anchorMin = st.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }

    Text Txt(RectTransform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color c)
    {
        var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        var t = g.AddComponent<Text>(); t.font = DialogueUI.KoreanFont(); t.fontSize = fontSize; t.color = c;
        t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = g.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(2, -2);
        return t;
    }

    void Update()
    {
        if (SeriaController.KeyDown(KeyCode.Return) || SeriaController.KeyDown(KeyCode.Space) ||
            SeriaController.KeyDown(KeyCode.LeftControl) || SeriaController.KeyDown(KeyCode.KeypadEnter)) advance = true;
        if (SeriaController.KeyDown(KeyCode.Escape)) skip = true;
        if (nextMark != null) nextMark.color = new Color(1f, 0.8f, 0.5f, typing ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
    }

    IEnumerator Run()
    {
        // 화면 전체가 검게 덮임
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { group.alpha = t; yield return null; }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(0.4f);

        foreach (var line in data.lines)
        {
            if (skip) break;
            bool hasName = !string.IsNullOrEmpty(line.speaker);
            nameText.text = hasName ? line.speaker : "";
            bodyText.fontStyle = hasName ? FontStyle.Normal : FontStyle.Italic;
            bodyText.text = ""; advance = false;
            // 줄이 바뀔 때 살짝 페이드
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.3f) { textGroup.alpha = t; yield return null; }
            textGroup.alpha = 1f;

            string s = line.text ?? "";
            typing = true;
            float cps = Mathf.Max(1f, data.charsPerSecond), shown = 0f;
            while (shown < s.Length && !advance && !skip)
            {
                shown += Time.unscaledDeltaTime * cps;
                bodyText.text = s.Substring(0, Mathf.Min(s.Length, (int)shown));
                yield return null;
            }
            bodyText.text = s; typing = false; advance = false;
            while (!advance && !skip) yield return null;              // 다음 줄 기다림
            advance = false;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.25f) { textGroup.alpha = t; yield return null; }
            textGroup.alpha = 0f;
        }

        nameText.text = bodyText.text = "";
        onDone?.Invoke();                                              // 맵 이동 시작 (화면은 검은 채로 유지)
        yield return new WaitForSecondsRealtime(1.5f);
        Destroy(gameObject);
    }
}
