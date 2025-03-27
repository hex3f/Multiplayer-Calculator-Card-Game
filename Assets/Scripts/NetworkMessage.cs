using System.Collections.Generic;

[System.Serializable]
public class NetworkMessage
{
    public string type;
    public int playerIndex;
    public List<Card> playedCards;
    public Card skillCard;
    public int result;
    public int currentNumber;
    public int targetNumber;
    public GameField currentField;
    public bool isSkillSuccess;
    public int[] playerScores;
    public bool skipTurn;
    public int cardsDrawn;
    public bool isFrozen;
    public List<Card> drawnCards;
    
    // 添加卡牌数量同步字段
    public int numberCardCount;   // 数字牌剩余数量
    public int operatorCardCount; // 运算符牌剩余数量
    public int skillCardCount;    // 技能牌剩余数量
}