using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public Transform handContainer;
    public float cardSpacing = 100f;
    public float cardScale = 1f;
    public float selectedCardScale = 1.2f;
    public float cardMoveSpeed = 10f;

    private List<Card> hand = new List<Card>();
    private List<GameObject> cardObjects = new List<GameObject>();
    private System.Action<Card> cardClickCallback;
    private Card selectedCard;
    private bool isUpdatingPositions = false;

    public void ShowHand(List<Card> cards, System.Action<Card> onCardClick)
    {
        // 清空现有手牌
        ClearHand();

        // 保存回调
        cardClickCallback = onCardClick;

        // 添加新卡牌
        foreach (var card in cards)
        {
            AddCard(card);
        }

        // 更新卡牌位置
        UpdateCardPositions();
    }

    public void AddCard(Card card)
    {
        hand.Add(card);
        GameObject cardObj = Instantiate(cardPrefab, handContainer);
        cardObjects.Add(cardObj);

        // 设置卡牌文本
        Text cardText = cardObj.GetComponentInChildren<Text>();
        if (cardText != null)
        {
            cardText.text = card.GetDisplayText();
        }

        // 添加点击事件
        Button button = cardObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnCardClicked(card));
        }

        // 更新卡牌位置
        UpdateCardPositions();
    }

    public void RemoveCard(Card card)
    {
        int index = hand.IndexOf(card);
        if (index != -1)
        {
            hand.RemoveAt(index);
            Destroy(cardObjects[index]);
            cardObjects.RemoveAt(index);
            UpdateCardPositions();
        }
    }

    public void SelectCard(Card card)
    {
        // 如果选择了同一张卡牌，不做任何改变
        if (selectedCard == card)
        {
            return;
        }

        // 取消之前选中卡牌的选择
        if (selectedCard != null)
        {
            int prevIndex = hand.IndexOf(selectedCard);
            if (prevIndex != -1)
            {
                cardObjects[prevIndex].transform.localScale = Vector3.one * cardScale;
            }
        }

        // 选择新卡牌
        selectedCard = card;
        int index = hand.IndexOf(card);
        if (index != -1)
        {
            cardObjects[index].transform.localScale = Vector3.one * selectedCardScale;
        }
    }

    public void DeselectCard()
    {
        if (selectedCard != null)
        {
            int index = hand.IndexOf(selectedCard);
            if (index != -1)
            {
                cardObjects[index].transform.localScale = Vector3.one * cardScale;
            }
            selectedCard = null;
        }
    }

    private void OnCardClicked(Card card)
    {
        cardClickCallback?.Invoke(card);
    }

    private void UpdateCardPositions()
    {
        if (isUpdatingPositions) return;
        isUpdatingPositions = true;

        float startX = -(hand.Count - 1) * cardSpacing / 2;
        for (int i = 0; i < cardObjects.Count; i++)
        {
            Vector3 targetPosition = new Vector3(startX + i * cardSpacing, 0, 0);
            cardObjects[i].transform.localPosition = targetPosition;
        }

        isUpdatingPositions = false;
    }

    public List<Card> GetHand()
    {
        return hand;
    }

    private void ClearHand()
    {
        foreach (var cardObj in cardObjects)
        {
            Destroy(cardObj);
        }
        cardObjects.Clear();
        hand.Clear();
        selectedCard = null;
    }
}