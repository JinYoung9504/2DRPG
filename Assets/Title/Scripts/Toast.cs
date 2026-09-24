// 화면 아래쪽에 잠깐 떴다가 사라지는 알림 (한글이 깨지지 않도록 이미지로 표시)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Toast : MonoBehaviour
{
    static Toast inst;
    Image img; Coroutine running;

    public static void Show(Sprite sprite, float duration = 1.4f)
    {
        if (sprite == null) return;
        if (inst == null)
        {
            var go = new GameObject("Toast", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 500;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
            inst = go.AddComponent<Toast>();
            var rt = new GameObject("Image", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(go.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.72f); rt.pivot = new Vector2(0.5f, 0.5f);   // 화면 위쪽 가운데
            inst.img = rt.gameObject.AddComponent<Image>(); inst.img.raycastTarget = false;
        }
        inst.img.sprite = sprite; inst.img.SetNativeSize();
        if (inst.running != null) inst.StopCoroutine(inst.running);
        inst.running = inst.StartCoroutine(inst.Run(duration));
    }

    IEnumerator Run(float duration)
    {
        var rt = (RectTransform)img.transform;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.15f)
        { img.color = new Color(1, 1, 1, t); rt.anchoredPosition = new Vector2(0, -20 * (1 - t)); yield return null; }
        img.color = Color.white; rt.anchoredPosition = Vector2.zero;
        yield return new WaitForSecondsRealtime(duration);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.3f) { img.color = new Color(1, 1, 1, t); yield return null; }
        img.color = new Color(1, 1, 1, 0);
    }
}
