// 몬스터 대사 (몬스터 종류마다 파일 하나, Assets/Monsters/Resources/MonsterTalk/ 안)
//  대사를 비워두면 대화창이 나오지 않음
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterTalk", menuName = "Game/몬스터 대사")]
public class MonsterTalkData : ScriptableObject
{
    [Header("몬스터 (대화창 오른쪽)")]
    public string monsterName = "몬스터";
    public Sprite portrait;

    [Header("처음 마주쳤을 때")]
    public float sightRange = 6f;
    public DialogueData.Line[] onSight = new DialogueData.Line[0];

    [Header("쓰러뜨렸을 때")]
    public DialogueData.Line[] onDefeat = new DialogueData.Line[0];

    [Header("옵션")]
    [Tooltip("켜면 같은 종류 몬스터 대사는 게임 중 한 번만 나옴 (리젠·다른 개체에서 반복 안 함)")]
    public bool oncePerGame = true;
}
