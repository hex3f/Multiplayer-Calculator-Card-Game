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
    Divide,
}

public enum ExtraOperatorType
{
    Square,
    SquareRoot
}

public enum SkillType
{
    Freeze,
    Mirror
}


[System.Serializable]
public class Card
{
    public CardType type;
    public int numberValue;
    public OperatorType operatorType;
    public ExtraOperatorType extraOperatorType;
    public SkillType skillType;

    public string GetDisplayText()
    {
        switch (type)
        {
            case CardType.Number:
                return $"数字卡 {numberValue}";
            case CardType.Operator:
                return $"运算符卡 {GetOperatorSymbol()}";
            case CardType.ExtraOperator:
                return $"特殊运算符卡 {GetExtraOperatorSymbol()}";
            case CardType.Skill:
                return $"技能卡 {GetSkillName()}";
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

    public string GetExtraOperatorSymbol()
    {
        switch (extraOperatorType)
        {
            case ExtraOperatorType.Square: return "Square";
            case ExtraOperatorType.SquareRoot: return "SquareRoot";
            default: return "?";
        }
    }

    public string GetSkillName()
    {
        switch (skillType)
        {
            case SkillType.Freeze: return "冻结";
            case SkillType.Mirror: return "镜像";
            default: return "未知技能";
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
                    case ExtraOperatorType.Square:
                        return currentValue * currentValue;
                    case ExtraOperatorType.SquareRoot:
                        return (int)Mathf.Sqrt(currentValue);  // 强制转换为 int（因为 Sqrt 返回 float）
                    default:
                        return currentValue;
                }
            default:
                return currentValue;
        }
    }

}