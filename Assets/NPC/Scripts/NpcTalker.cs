// NPC: 제자리에서 대기 동작, 플레이어가 1칸 앞까지 오면 머리 위 "!" + 대화창
//  대화가 끝난 뒤 다시 말을 걸려면 한 번 멀어졌다가 다가오면 됨
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class NpcTalker : MonoBehaviour
{
    public string npcName = "마을 이장";
    [Tooltip("세리아와 주고받는 대사. 비워두면 아래 Lines 를 NPC 혼자 말함")]
    public DialogueData dialogue;
    [TextArea(2, 4)] public string[] lines = { "……" };
    public Sprite portrait;
    public Sprite[] idleFrames, talkFrames;
    public float fps = 6f;
    public float talkRange = 1.3f;          // NPC 중심에서 이 거리 = 약 1칸 앞
    public bool spriteFacesLeft = false;
    public Vector2 iconOffset = new Vector2(0, 1.75f);
    [Header("둥실둥실 (요정 등)")]
    public float floatHeight = 0f;          // 땅에서 떠 있는 높이
    public float bobAmplitude = 0f;         // 위아래 흔들림 크기
    public float bobSpeed = 2f;
    Vector3 basePos;

    SpriteRenderer sr, icon; Transform player;
    bool talking, armed = true; float animT, iconT;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        basePos = transform.position;
        var p = GameObject.Find("Seria"); if (p) player = p.transform;
        var g = new GameObject("Exclaim"); g.transform.SetParent(transform, false); g.transform.localPosition = iconOffset;
        icon = g.AddComponent<SpriteRenderer>(); icon.sprite = Resources.Load<Sprite>("NPC/Icon_Exclaim"); icon.sortingOrder = sr.sortingOrder + 5; icon.enabled = false;
    }

    void Update()
    {
        // 애니메이션
        var frames = talking && talkFrames != null && talkFrames.Length > 0 ? talkFrames : idleFrames;
        if (frames != null && frames.Length > 0) { animT += Time.unscaledDeltaTime * fps; sr.sprite = frames[(int)animT % frames.Length]; }

        if (floatHeight != 0f || bobAmplitude != 0f)
            transform.position = basePos + new Vector3(0, floatHeight + Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude, 0);

        // 느낌표 톡 튀는 효과
        if (icon.enabled) { iconT += Time.unscaledDeltaTime; float s = 1f + 0.25f * Mathf.Exp(-iconT * 8f) * Mathf.Sin(iconT * 30f); icon.transform.localScale = Vector3.one * 0.55f * s; }

        if (player == null || talking) return;
        float dist = Mathf.Abs(player.position.x - transform.position.x);
        bool near = dist <= talkRange && Mathf.Abs(player.position.y - transform.position.y) < 1.5f;
        if (!near) { if (dist > talkRange + 0.8f) armed = true; return; }
        if (!armed) return;

        armed = false; talking = true; iconT = 0; icon.enabled = true;
        float d = Mathf.Sign(player.position.x - transform.position.x);
        sr.flipX = spriteFacesLeft ? d > 0 : d < 0;                          // 플레이어 쪽을 봄
        System.Action done = () => { talking = false; icon.enabled = false; };
        if (dialogue != null && dialogue.lines != null && dialogue.lines.Length > 0)
        {
            var list = new DialogueUI.Entry[dialogue.lines.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var l = dialogue.lines[i]; bool seria = l.speaker == Speaker.Seria;
                list[i] = new DialogueUI.Entry { name = seria ? dialogue.seriaName : npcName, face = l.face != null ? l.face : (seria ? dialogue.seriaPortrait : portrait), right = !seria, text = l.text };   // 세리아 왼쪽, NPC 오른쪽
            }
            DialogueUI.Show(list, done);
        }
        else DialogueUI.Show(npcName, portrait, lines, done);
    }
}
