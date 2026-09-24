// 저장 / 불러오기 (JSON 파일, PC·모바일 공용 저장 위치 사용)
using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[Serializable]
public class SaveData
{
    public string scene;        // 저장한 맵(씬) 이름
    public float x, y;          // 캐릭터 위치
    public float hp;            // 체력
    public bool facingLeft;     // 바라보는 방향
    public int level = 1;       // 레벨
    public int exp;             // 경험치
    public string savedAt;      // 저장 시각
}

public static class SaveSystem
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool HasSave => File.Exists(FilePath);

    public static bool Save()
    {
        var player = GameObject.Find("Seria");
        if (player == null) { Debug.LogWarning("[Save] Seria 를 찾을 수 없습니다."); return false; }
        var hp = player.GetComponent<PlayerHealth>();
        if (hp != null && hp.IsDead) return false;

        var data = new SaveData
        {
            scene = SceneManager.GetActiveScene().name,
            x = player.transform.position.x,
            y = player.transform.position.y,
            hp = hp != null ? hp.CurrentHP : 100f,
            facingLeft = FacingLeft(player),
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            level = PlayerLevel.Instance != null ? PlayerLevel.Instance.level : 1,
            exp = PlayerLevel.Instance != null ? PlayerLevel.Instance.exp : 0,
        };
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[Save] 저장 완료: {FilePath}");
            return true;
        }
        catch (Exception e) { Debug.LogError("[Save] 저장 실패: " + e.Message); return false; }
    }

    static bool FacingLeft(GameObject p)
    {
        var sr = p.GetComponent<SpriteRenderer>();
        return sr != null && sr.flipX;
    }

    public static SaveData Load()
    {
        if (!HasSave) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)); }
        catch (Exception e) { Debug.LogError("[Save] 불러오기 실패: " + e.Message); return null; }
    }

    public static void Delete() { if (HasSave) File.Delete(FilePath); }
}

public static class UIHelper
{
    // 버튼 클릭을 받으려면 씬에 EventSystem 이 필요
    public static void EnsureEventSystem()
    {
        #if UNITY_2022_2_OR_NEWER
        var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
        var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
        if (EventSystem.current != null || existing != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }
}
