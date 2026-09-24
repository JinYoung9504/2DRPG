// 맵(씬) 이동: 화면을 어둡게 → 다음 씬 로드 → 지정한 위치에 플레이어 배치 → 다시 밝게
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public static bool Busy => inst != null && inst.busy;

    static SceneTransition inst;
    Image fade; bool busy;

    // spawnSide: "Left"/"Right" → Spawn_Left/Spawn_Right 위치에 등장, null → 씬에 놓인 위치 그대로
    public static void Go(string sceneName, string spawnSide) => Load(sceneName, () => PlacePlayer(spawnSide));

    // 저장된 좌표로 이동 (이어하기)
    public static void GoToPosition(string sceneName, Vector3 pos, bool facingLeft) => Load(sceneName, () => PlaceAt(pos, facingLeft));

    static void Load(string sceneName, System.Action onLoaded)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneTransition] '{sceneName}' 씬을 찾을 수 없습니다. File > Build Profiles(Build Settings)의 씬 목록에 추가되어 있는지 확인하세요.");
            return;
        }
        Ensure();
        if (inst.busy) return;
        inst.StartCoroutine(inst.Run(sceneName, onLoaded));
    }

    static void Ensure()
    {
        if (inst != null) return;
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<SceneTransition>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var img = new GameObject("Fade", typeof(RectTransform)).GetComponent<RectTransform>();
        img.SetParent(go.transform, false);
        img.anchorMin = Vector2.zero; img.anchorMax = Vector2.one; img.offsetMin = img.offsetMax = Vector2.zero;
        inst.fade = img.gameObject.AddComponent<Image>();
        inst.fade.color = new Color(0, 0, 0, 0); inst.fade.raycastTarget = false;
    }

    IEnumerator Run(string sceneName, System.Action onLoaded)
    {
        busy = true;
        yield return Fade(0f, 1f, 0.35f);
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        onLoaded?.Invoke();
        yield return null;
        yield return Fade(1f, 0f, 0.35f);
        busy = false;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0; t < time; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        fade.color = new Color(0, 0, 0, to);
    }

    static void PlaceAt(Vector3 pos, bool facingLeft)
    {
        var player = GameObject.Find("Seria");
        if (player != null)
        {
            player.transform.position = pos;
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = facingLeft;
        }
        SnapCamera();
    }

    static void SnapCamera()
    {
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (cam != null) cam.SnapToTarget();
    }

    static void PlacePlayer(string side)
    {
        if (string.IsNullOrEmpty(side)) { SnapCamera(); return; }
        var player = GameObject.Find("Seria");
        var spawn = GameObject.Find("Spawn_" + side);
        if (player != null && spawn != null)
        {
            player.transform.position = spawn.transform.position;
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = side == "Right";     // 오른쪽에서 들어오면 왼쪽을 봄
        }
        SnapCamera();
    }
}
