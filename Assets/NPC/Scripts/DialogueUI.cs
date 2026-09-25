// 대화창: 왼쪽 얼굴 이미지 + 이름표 + 대사 (한 글자씩 출력)
//  넘기기: Space / Enter / Ctrl / Z / 마우스 클릭 / 화면 터치
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DialogueUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static DialogueUI inst;

    Text nameText, bodyText; Image portrait; GameObject root; RectTransform nextMark;
    string[] lines; int index; bool typing; Action onClose; Coroutine typeCo;
    public float charsPerSecond = 35f;

    public static void Show(string speaker, Sprite face, string[] dialogLines, Action onClosed = null)
    {
        if (dialogLines == null || dialogLines.Length == 0) return;
        if (inst == null) { var go = new GameObject("Dialogue UI"); inst = go.AddComponent<DialogueUI>(); inst.Build(); }
        inst.Open(speaker, face, dialogLines, onClosed);
    }

    // 한글 폰트: 운영체제 폰트 사용 (없으면 기본 폰트)
    static Font korean;
    public static Font KoreanFont()
    {
        if (korean != null) return korean;
        try { korean = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Droid Sans Fallback" }, 32); } catch { }
#if UNITY_2022_2_OR_NEWER
        if (korean == null) korean = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        if (korean == null) korean = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return korean;
    }

    RectTransform Rt(string n, Transform p, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(p, false); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        return rt;
    }

    Text Txt(RectTransform rt, int size, TextAnchor align, Color c)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = KoreanFont(); t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.15f;
        var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    void Build()
    {
        DontDestroyOnLoad(gameObject);
        UIHelper.EnsureEventSystem();
        var c = gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 300;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        var r = (RectTransform)root.transform; r.SetParent(transform, false);
        r.anchorMin = new Vector2(0.5f, 0); r.anchorMax = new Vector2(0.5f, 0); r.pivot = new Vector2(0.5f, 0);
        r.anchoredPosition = new Vector2(0, 40); r.sizeDelta = new Vector2(1400, 300);

        var frame = Rt("Frame", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        frame.sprite = Resources.Load<Sprite>("NPC/Dialog_Frame");
        var click = frame.gameObject.AddComponent<UIButtonFx>();            // 화면 터치·클릭으로 넘기기
        click.hoverColor = click.pressColor = Color.white; click.onClick = Advance;

        portrait = Rt("Portrait", r, new Vector2(0, 0), new Vector2(0, 1), new Vector2(18, 18), new Vector2(330, 60)).gameObject.AddComponent<Image>();
        portrait.preserveAspect = true; portrait.raycastTarget = false;

        var plate = Rt("NamePlate", r, new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -32), new Vector2(660, 38)).gameObject.AddComponent<Image>();
        plate.sprite = Resources.Load<Sprite>("NPC/Dialog_NamePlate"); plate.raycastTarget = false;
        nameText = Txt(Rt("Name", plate.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), 34, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.78f));

        bodyText = Txt(Rt("Body", r, Vector2.zero, Vector2.one, new Vector2(370, 40), new Vector2(-60, -60)), 38, TextAnchor.UpperLeft, Color.white);

        nextMark = Rt("Next", r, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 24), new Vector2(-40, 54));
        var nt = Txt(nextMark, 30, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.45f)); nt.font = KoreanFont(); nt.text = "▼";
        root.SetActive(false);
    }

    void Open(string speaker, Sprite face, string[] l, Action closed)
    {
        lines = l; index = 0; onClose = closed;
        nameText.text = speaker; portrait.sprite = face; portrait.enabled = face != null;
        root.SetActive(true); IsOpen = true;
        ShowLine();
    }

    void ShowLine()
    {
        if (typeCo != null) StopCoroutine(typeCo);
        typeCo = StartCoroutine(Type(lines[index]));
    }

    IEnumerator Type(string s)
    {
        typing = true; bodyText.text = "";
        for (int i = 1; i <= s.Length; i++) { bodyText.text = s.Substring(0, i); yield return new WaitForSecondsRealtime(1f / charsPerSecond); }
        typing = false;
    }

    void Advance()
    {
        if (!IsOpen) return;
        if (typing) { StopCoroutine(typeCo); bodyText.text = lines[index]; typing = false; return; }   // 출력 중이면 한 번에 표시
        index++;
        if (index >= lines.Length) { Close(); return; }
        ShowLine();
    }

    void Close()
    {
        root.SetActive(false); IsOpen = false;
        var cb = onClose; onClose = null; cb?.Invoke();
    }

    int openedFrame;
    void Update()
    {
        if (!IsOpen) { openedFrame = Time.frameCount; return; }
        if (Time.frameCount - openedFrame < 2) return;                         // 열린 직후 같은 키 입력 무시
        nextMark.gameObject.SetActive(!typing && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f);
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool next = k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame || k.rightCtrlKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame);
#else
        bool next = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Z);
#endif
        if (next) Advance();
    }

    void OnDestroy() { if (inst == this) { IsOpen = false; inst = null; } }
}
