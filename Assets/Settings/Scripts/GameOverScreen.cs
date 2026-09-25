// 세리아가 쓰러지면: 화면이 어두워지며 GAME OVER → 시작 화면으로
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    static bool running;

    public static void Show()
    {
        if (running) return;
        running = true;
        new GameObject("Game Over").AddComponent<GameOverScreen>();
    }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;
        var c = gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 900;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        var dim = UIHelper.Stretch("Dim", transform).gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0);
        var t = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>(); t.SetParent(transform, false);
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.55f); t.sizeDelta = new Vector2(1300, 260);
        var img = t.gameObject.AddComponent<Image>(); img.sprite = Resources.Load<Sprite>("Settings/GameOver"); img.color = new Color(1, 1, 1, 0);

        yield return new WaitForSecondsRealtime(1.0f);                          // 쓰러지는 동작 보여주기
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 1.2f)
        {
            dim.color = new Color(0, 0, 0, 0.75f * k);
            img.color = new Color(1, 1, 1, k);
            t.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, 1 - (1 - k) * (1 - k));
            yield return null;
        }
        yield return new WaitForSecondsRealtime(2.0f);
        SceneTransition.Go("Title", null);
        yield return new WaitForSecondsRealtime(0.5f);                          // 화면이 어두워진 뒤 제거
        running = false;
        Destroy(gameObject);
    }
}
