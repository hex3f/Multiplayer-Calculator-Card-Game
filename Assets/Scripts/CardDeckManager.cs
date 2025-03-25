using System.Collections.Generic;
using UnityEngine;

public class CardDeckManager : MonoBehaviour
{
    public static CardDeckManager Instance;

    private List<Card> deck = new List<Card>();
    private List<Card> discardPile = new List<Card>();
    private int currentRound = 0;
    private const int MAX_ROUNDS = 10;
    private GameField currentField = GameField.Normal;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeDeck()
    {
        deck.Clear();
        discardPile.Clear();
        currentRound = 0;
        currentField = GameField.Normal;

        // 添加基础数字牌 (1-12，每个数字3张)
        for (int i = 1; i <= 12; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                deck.Add(new Card { type = CardType.Number, numberValue = i });
            }
        }

        // 添加运算符牌
        for (int i = 0; i < 8; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Add });
        for (int i = 0; i < 6; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Subtract });
        for (int i = 0; i < 8; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Multiply });
        for (int i = 0; i < 4; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Divide });

        // 添加技能牌
        for (int i = 0; i < 3; i++) deck.Add(new Card { type = CardType.Skill, skillType = SkillType.Freeze });
        for (int i = 0; i < 3; i++) deck.Add(new Card { type = CardType.Skill, skillType = SkillType.Mirror });

        // 添加特殊牌
        for (int i = 0; i < 3; i++) deck.Add(new Card { type = CardType.ExtraOperator, extraOperatorType = ExtraOperatorType.Square });
        for (int i = 0; i < 3; i++) deck.Add(new Card { type = CardType.ExtraOperator, extraOperatorType = ExtraOperatorType.SquareRoot });

        ShuffleDeck();
    }

    private void ShuffleDeck()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Card temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    public List<Card> GenerateInitialHand()
    {
        // 确保牌堆已初始化
        if (deck.Count == 0)
        {
            InitializeDeck();
        }

        List<Card> hand = new List<Card>();
        
        // 抽取3张数字牌
        for (int i = 0; i < 3; i++)
        {
            Card card = DrawCard();
            while (card != null && card.type != CardType.Number)
            {
                // 如果不是数字牌，放回牌堆底部
                deck.Add(card);
                card = DrawCard();
            }
            if (card != null)
            {
                hand.Add(card);
            }
        }

        // 抽取2张运算牌
        for (int i = 0; i < 2; i++)
        {
            Card card = DrawCard();
            while (card != null && card.type != CardType.Operator)
            {
                // 如果不是运算牌，放回牌堆底部
                deck.Add(card);
                card = DrawCard();
            }
            if (card != null)
            {
                hand.Add(card);
            }
        }

        // 抽取1张特殊牌（技能牌或特殊运算符牌）
        Card specialCard = DrawCard();
        while (specialCard != null && specialCard.type != CardType.Skill && specialCard.type != CardType.ExtraOperator)
        {
            // 如果不是特殊牌，放回牌堆底部
            deck.Add(specialCard);
            specialCard = DrawCard();
        }
        if (specialCard != null)
        {
            hand.Add(specialCard);
        }

        return hand;
    }

    public Card DrawCard()
    {
        if (deck.Count == 0)
        {
            if (discardPile.Count == 0) return null;
            deck.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }

        Card card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    public void DiscardCard(Card card)
    {
        discardPile.Add(card);
    }

    public int GenerateTargetNumber()
    {
        return Random.Range(1, 100);
    }

    public void NextRound()
    {
        currentRound++;
        if (currentRound == 5)
        {
            // 第5回合随机选择一个特殊领域
            currentField = (GameField)Random.Range(1, 4);
        }
        else if (currentRound > 5)
        {
            currentField = GameField.Normal;
        }
    }

    public GameField GetCurrentField()
    {
        return currentField;
    }

    public int GetCurrentRound()
    {
        return currentRound;
    }

    public bool IsGameOver()
    {
        return currentRound >= MAX_ROUNDS;
    }
}

public enum GameField
{
    Normal,
    Square,
    SquareRoot
}