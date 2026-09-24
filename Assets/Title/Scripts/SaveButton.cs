// 인게임 우측 상단의 저장 버튼 (플로피 디스크 아이콘)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SaveButton : MonoBehaviour
{
    public Sprite icon;
    public Sprite toastSaved;
    public float size = 64f;

    RectTransform btn;

    void Start()
    {
        UIHelper.EnsureEventSystem();
        var cgo = new GameObject("Save Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var c = cgo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 110;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;

        btn = new GameObject("Btn Save", typeof(RectTransform)).GetComponent<RectTransform>();
        btn.SetParent(cgo.transform, false);
        btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(1, 1);
        btn.anchoredPosition = new Vector2(-24, -24); btn.sizeDelta = new Vector2(size, size);
        var img = btn.gameObject.AddComponent<Image>(); img.sprite = icon; img.color = new Color(1, 1, 1, 0.85f);

        // 누름 효과용 덮개
        var over = UIHelper.Stretch("Press", btn).gameObject.AddComponent<Image>();
        over.sprite = icon; over.color = new Color(1, 1, 1, 0);
        var fx = over.gameObject.AddComponent<UIButtonFx>();
        fx.hoverColor = new Color(1, 1, 1, 0.25f); fx.pressColor = new Color(0, 0, 0, 0.3f);
        fx.onClick = DoSave;
    }

    void DoSave()
    {
        if (SceneTransition.Busy) return;
        if (SaveSystem.Save())
        {
            Toast.Show(toastSaved);
            StopAllCoroutines(); StartCoroutine(Pop());
        }
    }

    IEnumerator Pop()
    {
        for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            btn.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / 0.25f * Mathf.PI));
            yield return null;
        }
        btn.localScale = Vector3.one;
    }
}
