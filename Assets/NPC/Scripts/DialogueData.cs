// 대화 데이터 (Project 창에서 선택 → Inspector 에서 대사 추가·수정·순서 변경)
//  한 줄 = 대화창 한 페이지. 말하는 사람을 NPC / 세리아 중에서 고름
//  Face 칸에 표정 그림을 넣으면 그 줄에서만 얼굴이 바뀜 (비우면 기본 얼굴)
using UnityEngine;

public enum Speaker
{
    [InspectorName("NPC")] Npc,
    [InspectorName("세리아")] Seria,
}

[CreateAssetMenu(fileName = "New Dialogue", menuName = "Game/대화 데이터")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class Line
    {
        public Speaker speaker;
        [TextArea(2, 5)] public string text;
        [Tooltip("이 줄에서만 쓸 얼굴(표정). 비워두면 기본 얼굴")] public Sprite face;
    }

    [Header("세리아 (대화창 왼쪽)")]
    public string seriaName = "세리아";
    public Sprite seriaPortrait;

    [Header("대사 (위에서부터 순서대로)")]
    public Line[] lines = new Line[0];
}
