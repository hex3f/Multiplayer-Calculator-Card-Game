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
}