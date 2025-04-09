using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public RectTransform handContainer;
    public float cardScale = 1.0f;
    public float selectedCardOffset = 50f;
    public float animationDuration = 0.3f;

    private List<Card> hand = new List<Card>();
    private List<GameObject> cardObjects = new List<GameObject>();
    private System.Action<Card> cardClickCallback;
    private HashSet<Card> selectedCards = new HashSet<Card>();
    private bool isUpdatingPositions = false;
    private Coroutine currentAnimation;
    public GridLayoutGroup gridLayout;
    public Vector2 cellSize;
    public Vector2 spacing;
    public RectOffset padding;

    private void Awake()
    {
        // 在Awake中初始化值
        cellSize = new Vector2(150f, 200f);
        spacing = new Vector2(-30f, 0f);
        padding = new RectOffset(50, 50, 10, 10);

        // 设置手牌容器的锚点和位置
        if (handContainer != null)
        {
            handContainer.anchorMin = new Vector2(0, 0.5f);
            handContainer.anchorMax = new Vector2(0, 0.5f);
            handContainer.pivot = new Vector2(0, 0.5f);
            handContainer.anchoredPosition = new Vector2(50f, 0f);

            // 设置 GridLayoutGroup
            GridLayoutGroup layoutGroup = handContainer.GetComponent<GridLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.childAlignment = TextAnchor.MiddleLeft;  // 设置为左对齐
                layoutGroup.padding = padding;
                layoutGroup.spacing = spacing;
                layoutGroup.cellSize = cellSize;
                layoutGroup.enabled = false;  // 禁用 GridLayoutGroup，使用我们自己的布局
            }
        }
    }

    public void ShowHand(List<Card> cards, System.Action<Card> onCardClick)
    {
        ClearHand();
        cardClickCallback = onCardClick;
        foreach (var card in cards)
        {
            AddCard(card);
        }
        UpdateCardPositions();
    }

    public void AddCard(Card card)
    {
        hand.Add(card);
        GameObject cardObjNew = Instantiate(cardPrefab, handContainer);
        cardObjects.Add(cardObjNew);

        cardObjNew.transform.localScale = Vector3.one * cardScale;

        RectTransform rectTransform = cardObjNew.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
            rectTransform.pivot = new Vector2(0, 0.5f);
            rectTransform.sizeDelta = cellSize;
        }

        cardObjNew.GetComponent<CardUI>().SetCard(card, OnCardClicked);
        UpdateCardPositions();
    }

    public void RemoveCard(Card card)
    {
        int index = hand.IndexOf(card);
        if (index != -1)
        {
            hand.RemoveAt(index);
            GameObject cardObj = cardObjects[index];
            cardObjects.RemoveAt(index);
            Destroy(cardObj);
            UpdateCardPositions();
        }
    }

    public void SelectCard(Card card)
    {
        if (selectedCards.Contains(card))
        {
            Debug.Log(11111111111111);
            return;
        }

        selectedCards.Add(card);
        UpdateCardPositions();
    }

    public void DeselectCard()
    {
        selectedCards.Clear();
        UpdateCardPositions();
    }

    public void DeselectCard(Card card)
    {
        if (selectedCards.Remove(card))
        {
            UpdateCardPositions();
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

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        List<Vector3> targetPositions = new List<Vector3>();
        List<float> targetScales = new List<float>();

        float startX = 0f;

        for (int i = 0; i < cardObjects.Count; i++)
        {
            float targetY = selectedCards.Contains(hand[i]) ? selectedCardOffset : 0;
            float posX = startX + (cellSize.x + spacing.x) * i;
            targetPositions.Add(new Vector3(posX, targetY, 0));
            targetScales.Add(cardScale);  // 始终使用固定大小
        }

        currentAnimation = StartCoroutine(AnimateAllCards(targetPositions, targetScales));
        isUpdatingPositions = false;
    }

    private IEnumerator AnimateAllCards(List<Vector3> targetPositions, List<float> targetScales)
    {
        List<Vector3> startPositions = new List<Vector3>();
        foreach (var cardObj in cardObjects)
        {
            startPositions.Add(cardObj.transform.localPosition);
            // 设置固定缩放
            cardObj.transform.localScale = Vector3.one * cardScale;
        }

        float elapsedTime = 0;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            for (int i = 0; i < cardObjects.Count; i++)
            {
                cardObjects[i].transform.localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], smoothT);
            }

            yield return null;
        }

        for (int i = 0; i < cardObjects.Count; i++)
        {
            cardObjects[i].transform.localPosition = targetPositions[i];
        }

        currentAnimation = null;
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
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }

        foreach (var cardObj in cardObjects)
        {
            Destroy(cardObj);
        }
        cardObjects.Clear();
        hand.Clear();
        selectedCards.Clear();
    }
}