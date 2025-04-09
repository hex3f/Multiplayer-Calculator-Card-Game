using System.Collections.Generic;

[System.Serializable]
public class NetworkMessage
{
    public string type;
    public int playerIndex;
    public int targetNumber;
    public List<Card> playedCards;
    public List<Card> drawnCards;
    public int cardsDrawn;
    public int numberCardCount;
    public int operatorCardCount;
    public int extraOperatorCardCount;
    public int skillCardCount;
    public int playerScore;
    public int opponentScore;
    public bool isFrozen;
    public List<Card> initialHand;
    public Card skillCard;
    public int result;
    public bool isSkillSuccess;
    public int[] playerScores;
    public bool skipTurn;
}