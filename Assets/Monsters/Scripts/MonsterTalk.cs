// 몬스터 대화: 몬스터가 생기면 종류에 맞는 대사 파일(Resources/MonsterTalk/종류이름)을 찾아 자동으로 붙음
//  - 처음 마주쳤을 때(onSight) / 쓰러뜨렸을 때(onDefeat) 대화창 표시, 대화 중엔 게임 일시정지
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterTalk : MonoBehaviour
{
    public MonsterTalkData data;
    static readonly HashSet<string> shown = new HashSet<string>();   // 이번 게임에서 이미 나온 대사
    Transform player; EnemyHealth health; SpriteRenderer sr; string key; bool sightDone;

    public static void Attach(EnemyHealth e)
    {
        if (e == null || e.GetComponent<MonsterTalk>() != null) return;
        if (e.GetComponent<SummonedMinion>() != null) return;          // 아타칸이 부른 소환수는 대화 없음
        foreach (var mb in e.GetComponents<MonoBehaviour>())
        {
            string n = mb.GetType().Name;
            if (!n.EndsWith("Enemy") && !n.EndsWith("Boss")) continue;
            var d = Resources.Load<MonsterTalkData>("MonsterTalk/" + n);
            if (d == null) return;
            bool hasLines = (d.onSight != null && d.onSight.Length > 0) || (d.onDefeat != null && d.onDefeat.Length > 0);
            if (!hasLines) return;
            var t = e.gameObject.AddComponent<MonsterTalk>(); t.data = d; t.key = n;
            return;
        }
    }

    public static void ResetShown() => shown.Clear();

    void Start()
    {
        health = GetComponent<EnemyHealth>(); sr = GetComponent<SpriteRenderer>();
        var p = GameObject.Find("Seria"); if (p) player = p.transform;
        if (health != null) health.OnDied += () => { if (data.onDefeat != null && data.onDefeat.Length > 0) StartCoroutine(DefeatTalk()); };
    }

    bool Allowed(string kind)
    {
        if (!data.oncePerGame) return true;
        return !shown.Contains(key + kind);
    }

    void Update()
    {
        if (sightDone || data.onSight == null || data.onSight.Length == 0 || player == null || DialogueUI.IsOpen) return;
        if (health != null && health.IsDead) return;
        if (Mathf.Abs(player.position.x - transform.position.x) > data.sightRange || Mathf.Abs(player.position.y - transform.position.y) > 3f) return;
        sightDone = true;
        if (!Allowed("_sight")) return;
        shown.Add(key + "_sight");
        Show(data.onSight);
    }

    IEnumerator DefeatTalk()
    {
        if (!Allowed("_defeat")) yield break;
        shown.Add(key + "_defeat");
        yield return new WaitForSecondsRealtime(0.7f);          // 쓰러지는 모습 조금 보여준 뒤
        Show(data.onDefeat);
    }

    void Show(DialogueData.Line[] lines)
    {
        var seriaFace = Resources.Load<Sprite>("NPC/Portrait_Seria");
        var face = data.portrait != null ? data.portrait : (sr != null ? sr.sprite : null);
        var list = new DialogueUI.Entry[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            bool seria = lines[i].speaker == Speaker.Seria;
            list[i] = new DialogueUI.Entry { name = seria ? "세리아" : data.monsterName, face = lines[i].face != null ? lines[i].face : (seria ? seriaFace : face), right = !seria, text = lines[i].text };
        }
        DialogueUI.Show(list);
    }
}
