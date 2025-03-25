[System.Serializable]
public class NetworkMessage
{
    public string type;
    public int playerIndex;
    public Card numberCard;
    public Card operatorCard;
    public Card skillCard;
    public int result;
    public int targetNumber;
    public int currentNumber;
    public int[] playerScores;
    public GameStatus gameStatus;
    public Card drawnCard;
    public bool skipTurn;
    public GameField currentField;
}