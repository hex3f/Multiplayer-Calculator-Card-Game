using UnityEngine;

public enum CardType
{
    Number,
    Operator,
    Skill,
    ExtraOperator
}

public enum OperatorType
{
    Add,
    Subtract,
    Multiply,
    Divide
}

public enum SkillType
{
    Freeze,
    Mirror
}

public enum ExtraOperatorType
{
    Square,
    SquareRoot
}

[System.Serializable]
public class Card
{
    public CardType type;
    public int numberValue;
    public OperatorType operatorType;
    public SkillType skillType;
    public ExtraOperatorType extraOperatorType;

    public string GetDisplayText()
    {
        switch (type)
        {
            case CardType.Number:
                return $"数字卡 {numberValue}";
            case CardType.Operator:
                return $"运算符卡 {GetOperatorSymbol()}";
            case CardType.Skill:
                return $"技能卡 {GetSkillName()}";
            case CardType.ExtraOperator:
                return $"特殊卡 {GetExtraOperatorSymbol()}";
            default:
                return "未知卡牌";
        }
    }

    public string GetOperatorSymbol()
    {
        switch (operatorType)
        {
            case OperatorType.Add: return "+";
            case OperatorType.Subtract: return "-";
            case OperatorType.Multiply: return "×";
            case OperatorType.Divide: return "÷";
            default: return "?";
        }
    }

    public string GetSkillName()
    {
        switch (skillType)
        {
            case SkillType.Freeze: return "冻结 (跳过对手一回合)";
            case SkillType.Mirror: return "镜像 (复制对手上回合操作)";
            default: return "未知技能";
        }
    }

    public string GetExtraOperatorSymbol()
    {
        switch (extraOperatorType)
        {
            case ExtraOperatorType.Square: return "平方 (数字自动平方)";
            case ExtraOperatorType.SquareRoot: return "根号 (数字自动开方)";
            default: return "?";
        }
    }

    public int Calculate(int currentValue, int playerValue = 0)
    {
        switch (type)
        {
            case CardType.Operator:
                switch (operatorType)
                {
                    case OperatorType.Add: return currentValue + playerValue;
                    case OperatorType.Subtract: return currentValue - playerValue;
                    case OperatorType.Multiply: return currentValue * playerValue;
                    case OperatorType.Divide: return playerValue != 0 ? currentValue / playerValue : currentValue;
                    default: return currentValue;
                }
            case CardType.ExtraOperator:
                switch (extraOperatorType)
                {
                    case ExtraOperatorType.Square: return currentValue + (playerValue * playerValue);
                    case ExtraOperatorType.SquareRoot: return currentValue + (int)Mathf.Sqrt(playerValue);
                    default: return currentValue;
                }
            default:
                return currentValue;
        }
    }
}