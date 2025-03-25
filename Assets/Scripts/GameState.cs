using UnityEngine;
using System.Collections.Generic;

public enum GameStatus
{
    WaitingForPlayers,
    InProgress,
    GameOver
}

public class GameState : MonoBehaviour
{
    public static GameState Instance;
    private int currentPlayerTurn = 0;
    private int playerCount = 2;
    private int[] playerScores = new int[2];
    private bool isGameStarted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 初始化玩家分数为1
            for (int i = 0; i < playerScores.Length; i++)
            {
                playerScores[i] = 1;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameStatus Status { get; private set; }
    public int CurrentPlayerTurn => currentPlayerTurn;
    public int PlayerCount => playerCount;
    public int[] PlayerScores => playerScores;

    public GameState(int playerCount)
    {
        Status = GameStatus.WaitingForPlayers;
        this.playerCount = playerCount;
        playerScores = new int[playerCount];
        currentPlayerTurn = 0;
    }

    public void StartGame()
    {
        isGameStarted = true;
        currentPlayerTurn = 0;
        // 重置玩家分数为1
        for (int i = 0; i < playerScores.Length; i++)
        {
            playerScores[i] = 1;
        }
    }

    public void NextTurn()
    {
        currentPlayerTurn = (currentPlayerTurn + 1) % playerCount;
    }

    public void SetCurrentTurn(int turn)
    {
        currentPlayerTurn = turn;
    }

    public bool IsPlayerTurn(int playerIndex)
    {
        return currentPlayerTurn == playerIndex;
    }

    public void AddScore(int playerIndex, int value)
    {
        if (playerIndex >= 0 && playerIndex < playerScores.Length)
        {
            playerScores[playerIndex] = value;
        }
    }

    public void EndGame()
    {
        Status = GameStatus.GameOver;
    }

    public int GetScore(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerScores.Length)
        {
            return playerScores[playerIndex];
        }
        return 0;
    }

    public bool IsGameStarted => isGameStarted;
} 