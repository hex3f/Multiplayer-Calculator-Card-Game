using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Sprite numberSprite;
    public Sprite skillSprite;
    public Sprite operatorSprite;

    public Text cardText;
    public Image cardBackground;

    public Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    public Color normalColor = new Color(1f, 1f, 1f, 1f);

    private Card card;
    private System.Action<Card> onClick;

    public void SetCard(Card card, System.Action<Card> onClick)
    {
        this.card = card;
        this.onClick = onClick;

        if (card.type == CardType.Number)
        {
            cardText.text = card.numberValue.ToString();
            cardBackground.sprite = numberSprite;
        }
        else if (card.type == CardType.Skill)
        {
            cardText.text = card.GetSkillName();
            cardBackground.sprite = skillSprite;
        }
        else if (card.type == CardType.Operator || card.type == CardType.ExtraOperator)
        {
            cardText.text = card.GetOperatorSymbol();
            cardBackground.sprite = operatorSprite;
        }

        // Add click handler
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnCardClick);
        }
    }

    private void OnCardClick()
    {
        onClick?.Invoke(card);
    }

    public void SetSelected(bool selected)
    {
        if (cardBackground != null)
        {
            cardBackground.color = selected ? selectedColor : normalColor;
        }
    }
}