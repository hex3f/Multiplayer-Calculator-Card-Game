using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class PlayedCardsManager : MonoBehaviour
{
    [Header("玩家区域设置")]
    public RectTransform playerNumberCardsContainer;
    public RectTransform playerOperatorCardsContainer;
    public RectTransform playerSkillCardsContainer;

    [Header("对手区域设置")]
    public RectTransform opponentNumberCardsContainer;
    public RectTransform opponentOperatorCardsContainer;
    public RectTransform opponentSkillCardsContainer;

    [Header("卡牌预制体")]
    public GameObject cardPrefab;

    [Header("卡牌设置")]
    public float cardScale = 0.8f;

    private Dictionary<RectTransform, List<GameObject>> containerCards = new Dictionary<RectTransform, List<GameObject>>();

    private void Awake()
    {
        // 初始化容器列表
        InitializeContainer(playerNumberCardsContainer);
        InitializeContainer(playerOperatorCardsContainer);
        InitializeContainer(playerSkillCardsContainer);
        InitializeContainer(opponentNumberCardsContainer);
        InitializeContainer(opponentOperatorCardsContainer);
        InitializeContainer(opponentSkillCardsContainer);
    }

    private void InitializeContainer(RectTransform container)
    {
        if (container != null)
        {
            containerCards[container] = new List<GameObject>();
        }
    }

    public void AddCard(Card card, bool isPlayer)
    {
        RectTransform targetContainer = GetTargetContainer(card, isPlayer);
        if (targetContainer == null) return;

        GameObject cardObj = Instantiate(cardPrefab, targetContainer);
        cardObj.transform.localScale = Vector3.one * cardScale;  // 设置卡牌缩放

        // 设置卡牌UI
        CardUI cardUI = cardObj.GetComponent<CardUI>();
        cardUI.SetCard(card, null); // 不需要点击回调

        // 添加到容器列表
        containerCards[targetContainer].Add(cardObj);
    }

    private RectTransform GetTargetContainer(Card card, bool isPlayer)
    {
        if (isPlayer)
        {
            switch (card.type)
            {
                case CardType.Number:
                    return playerNumberCardsContainer;
                case CardType.Operator:
                    return playerOperatorCardsContainer;
                case CardType.Skill:
                    return playerSkillCardsContainer;
                default:
                    return null;
            }
        }
        else
        {
            switch (card.type)
            {
                case CardType.Number:
                    return opponentNumberCardsContainer;
                case CardType.Operator:
                    return opponentOperatorCardsContainer;
                case CardType.Skill:
                    return opponentSkillCardsContainer;
                default:
                    return null;
            }
        }
    }

    public void ClearCards(bool isPlayer)
    {
        if (isPlayer)
        {
            ClearContainer(playerNumberCardsContainer);
            ClearContainer(playerOperatorCardsContainer);
            ClearContainer(playerSkillCardsContainer);
        }
        else
        {
            ClearContainer(opponentNumberCardsContainer);
            ClearContainer(opponentOperatorCardsContainer);
            ClearContainer(opponentSkillCardsContainer);
        }
    }

    private void ClearContainer(RectTransform container)
    {
        if (container != null && containerCards.ContainsKey(container))
        {
            foreach (var cardObj in containerCards[container])
            {
                Destroy(cardObj);
            }
            containerCards[container].Clear();
        }
    }
} 