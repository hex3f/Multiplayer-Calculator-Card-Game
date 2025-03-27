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
    public float selectedCardOffset = 50f;

    private List<Card> hand = new List<Card>();
    private List<GameObject> cardObjects = new List<GameObject>();
    private System.Action<Card> cardClickCallback;
    private HashSet<Card> selectedCards = new HashSet<Card>();
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
        // 先复位所有卡牌
        foreach (var cardObj in cardObjects)
        {
            Vector3 pos = cardObj.transform.localPosition;
            cardObj.transform.localPosition = new Vector3(pos.x, 0, pos.z);
        }

        hand.Add(card);
        GameObject cardObjNew = Instantiate(cardPrefab, handContainer);
        cardObjects.Add(cardObjNew);

        cardObjNew.GetComponent<CardUI>().SetCard(card, OnCardClicked);

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
        // 如果卡牌已经被选中，不做任何改变
        if (selectedCards.Contains(card))
        {
            return;
        }

        // 选择新卡牌
        selectedCards.Add(card);
        int index = hand.IndexOf(card);
        if (index != -1)
        {
            // 将卡牌向前移动
            Vector3 currentPos = cardObjects[index].transform.localPosition;
            cardObjects[index].transform.localPosition = new Vector3(currentPos.x, currentPos.y + selectedCardOffset, currentPos.z);
        }
    }

    public void DeselectCard()
    {
        // 取消所有选中卡牌的选择
        foreach (var cardObj in cardObjects)
        {
            Vector3 pos = cardObj.transform.localPosition;
            cardObj.transform.localPosition = new Vector3(pos.x, 0, pos.z);
        }
        selectedCards.Clear();
        UpdateCardPositions();
    }

    public void DeselectCard(Card card)
    {
        // 取消指定卡牌的选择
        if (selectedCards.Remove(card))
        {
            int index = hand.IndexOf(card);
            if (index != -1)
            {
                Vector3 pos = cardObjects[index].transform.localPosition;
                cardObjects[index].transform.localPosition = new Vector3(pos.x, 0, pos.z);
            }
        }
        UpdateCardPositions();
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
            float targetY = selectedCards.Contains(hand[i]) ? selectedCardOffset : 0;
            Vector3 targetPosition = new Vector3(startX + i * cardSpacing, targetY, 0);
            cardObjects[i].transform.localPosition = targetPosition;
        }

        isUpdatingPositions = false;
    }

    public List<Card> GetHand()
    {
        return hand;
    }

    public List<Card> GetSelectedCards()
    {
        return new List<Card>(selectedCards);
    }

    private void ClearHand()
    {
        foreach (var cardObj in cardObjects)
        {
            Destroy(cardObj);
        }
        cardObjects.Clear();
        hand.Clear();
        selectedCards.Clear();
    }
}