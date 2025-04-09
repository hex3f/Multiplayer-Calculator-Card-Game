using System.Collections.Generic;
using UnityEngine;

public class CardDeckManager : MonoBehaviour
{
    public static CardDeckManager Instance;

    private List<Card> deck = new List<Card>();
    private List<Card> discardPile = new List<Card>();
    private int currentRound = 0;
    private const int MAX_ROUNDS = 5;
    public int MinNumber;
    public int MaxNumber;

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

        // 添加基础数字牌 (1-12，每个数字3张)
        for (int i = 1; i <= 12; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                deck.Add(new Card { type = CardType.Number, numberValue = i });
            }
        }

        // 添加运算符牌
        for (int i = 0; i < 9; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Add });
        for (int i = 0; i < 9; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Subtract });
        for (int i = 0; i < 4; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Multiply });
        for (int i = 0; i < 2; i++) deck.Add(new Card { type = CardType.Operator, operatorType = OperatorType.Divide });

        // 添加特殊运算符牌
        for (int i = 0; i < 2; i++) deck.Add(new Card { type = CardType.ExtraOperator, extraOperatorType = ExtraOperatorType.Square });
        for (int i = 0; i < 2; i++) deck.Add(new Card { type = CardType.ExtraOperator, extraOperatorType = ExtraOperatorType.SquareRoot });

        // 添加技能牌
        for (int i = 0; i < 2; i++) deck.Add(new Card { type = CardType.Skill, skillType = SkillType.Freeze });
        for (int i = 0; i < 2; i++) deck.Add(new Card { type = CardType.Skill, skillType = SkillType.Mirror });

        ShuffleDeck();
        
        // 打印初始牌库数量
        Debug.Log($"初始化牌库完成，总牌数: {deck.Count}, 数字牌:{GetNumberCardCount()}, 运算符:{GetOperatorCardCount()}, 技能牌:{GetSkillCardCount()}");
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
        
        // 抽取2张数字牌
        for (int i = 0; i < 2; i++)
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
                Debug.Log($"抽到数字牌: {card.GetDisplayText()}, 牌堆剩余数字牌: {GetNumberCardCount()}");
            }
        }

        // 抽取2张运算牌
        for (int i = 0; i < 2; i++)
        {
            Card operatorCard = DrawCard();
            while (operatorCard != null && operatorCard.type != CardType.Operator)
            {
                // 如果不是运算牌，放回牌堆底部
                deck.Add(operatorCard);
                operatorCard = DrawCard();
            }
            if (operatorCard != null)
            {
                hand.Add(operatorCard);
                Debug.Log($"抽到运算符牌: {operatorCard.GetDisplayText()}, 牌堆剩余运算符牌: {GetOperatorCardCount()}");
            }
        }

        // 抽取1张特殊运算牌
        for (int i = 0; i < 1; i++)
        {
            Card extraOperatorCard = DrawCard();
            while (extraOperatorCard != null && extraOperatorCard.type != CardType.ExtraOperator)
            {
                // 如果不是运算牌，放回牌堆底部
                deck.Add(extraOperatorCard);
                extraOperatorCard = DrawCard();
            }
            if (extraOperatorCard != null)
            {
                hand.Add(extraOperatorCard);
                Debug.Log($"抽到特殊运算符牌: {extraOperatorCard.GetDisplayText()}, 牌堆剩余特殊运算符牌: {GetExtraOperatorCardCount()}");
            }
        }

        // 抽取1张技能牌
        Card skillCard = DrawCard();
        while (skillCard != null && skillCard.type != CardType.Skill)
        {
            // 如果不是技能牌，放回牌堆底部
            deck.Add(skillCard);
            skillCard = DrawCard();
        }
        if (skillCard != null)
        {
            hand.Add(skillCard);
            Debug.Log($"抽到技能牌: {skillCard.GetDisplayText()}, 牌堆剩余技能牌: {GetSkillCardCount()}");
        }

        Debug.Log($"生成初始手牌完成，手牌数量: {hand.Count}张");
        Debug.Log($"牌堆剩余: 总牌数:{deck.Count}张, 数字牌:{GetNumberCardCount()}, 运算符:{GetOperatorCardCount()}, 特殊运算符:{GetExtraOperatorCardCount()}, 技能牌:{GetSkillCardCount()}");

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
        return Random.Range(MinNumber, MaxNumber);
    }

    public int GetCurrentRound()
    {
        return currentRound;
    }

    public bool IsGameOver()
    {
        return currentRound >= MAX_ROUNDS;
    }

    // 优化获取各类型牌的剩余数量方法，使用缓存提高性能
    public int GetNumberCardCount()
    {
        return CountCardsByType(CardType.Number);
    }
    
    public int GetOperatorCardCount()
    {
        return CountCardsByType(CardType.Operator);
    }
    public int GetExtraOperatorCardCount()
    {
        return CountCardsByType(CardType.ExtraOperator);
    }

    public int GetSkillCardCount()
    {
        return CountCardsByType(CardType.Skill);
    }
    
    private int CountCardsByType(CardType type)
    {
        int count = 0;
        foreach (var card in deck)
        {
            if (card.type == type)
            {
                count++;
            }
        }
        return count;
    }
    
    // 获取所有卡牌类型的数量信息
    public Dictionary<CardType, int> GetAllCardCounts()
    {
        Dictionary<CardType, int> counts = new Dictionary<CardType, int>();
        
        counts[CardType.Number] = GetNumberCardCount();
        counts[CardType.Operator] = GetOperatorCardCount();
        counts[CardType.ExtraOperator] = GetExtraOperatorCardCount();
        counts[CardType.Skill] = GetSkillCardCount();
        
        return counts;
    }

    // 用于网络同步，更新牌库数量（仅客户端使用）
    public void SyncCardCounts(int numberCount, int operatorCount, int extraOperatorCount, int skillCount)
    {
        // 这个方法用于客户端接收服务器同步的牌库数量
        Debug.Log($"同步牌库数量: 数字:{numberCount} 运算符:{operatorCount} 特殊运算符:{extraOperatorCount} 技能:{skillCount}");
        
        // 更新UI显示
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UpdateCardCountDisplay(numberCount, operatorCount, extraOperatorCount, skillCount);
        }
    }

    // 获取牌库剩余总数
    public int GetDeckCount()
    {
        return deck.Count;
    }
}