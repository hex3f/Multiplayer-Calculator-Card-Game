using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public TurnManager turnManager;

    private void Awake() => Instance = this;

    private void Start()
    {
        // 初始化牌堆
        CardDeckManager.Instance.InitializeDeck();
        
        // 生成初始手牌
        List<Card> hand = CardDeckManager.Instance.GenerateInitialHand();
        turnManager.StartGame(hand);
    }
}