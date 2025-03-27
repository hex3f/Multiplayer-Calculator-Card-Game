using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public HandManager handManager;
    public GameObject resultTextObject;
    public Text resultText;
    public Text turnText;
    public Text player1ScoreText;
    public Text player2ScoreText;
    public Text targetNumberText;
    public Button drawCardButton;
    public Button playCardButton;
    public float resultDisplayTime = 2f;

    private Card selectedNumberCard;
    private Card selectedOperatorCard;
    private Card selectedSkillCard;
    private GameState gameState;
    private int playerIndex;
    private bool isProcessingTurn;
    private bool isFrozen;
    private NetworkMessage lastTurnData;
    private int targetNumber;
    private int currentNumber; // 当前累计数值
    private bool gameEnded = false;
    private bool hasDrawnCard = false;

    private void Awake() => Instance = this;

    public void InitializeUI(Text turnText, Text targetNumberText, Text resultText, Button drawCardButton, Button playCardButton, Text player1ScoreText, Text player2ScoreText)
    {
        this.turnText = turnText;
        this.targetNumberText = targetNumberText;
        this.resultText = resultText;
        this.drawCardButton = drawCardButton;
        this.playCardButton = playCardButton;
        this.player1ScoreText = player1ScoreText;
        this.player2ScoreText = player2ScoreText;

        if (drawCardButton != null)
        {
            drawCardButton.onClick.AddListener(OnDrawCardButtonClicked);
        }

        if (playCardButton != null)
        {
            playCardButton.onClick.AddListener(OnPlayCardButtonClicked);
        }
    }

    public void Initialize(int playerIndex, int playerCount)
    {
        this.playerIndex = playerIndex;
        gameState = new GameState(playerCount);
        isProcessingTurn = false;
        isFrozen = false;
        lastTurnData = null;
        currentNumber = 1; // 初始值为1
        gameEnded = false;
        hasDrawnCard = false;

        if (playerIndex == 0) // 主机生成目标数
        {
            targetNumber = CardDeckManager.Instance.GenerateTargetNumber();
            UpdateUI(); // 立即更新UI
            // 发送目标数给客户端
            NetworkMessage targetMsg = new NetworkMessage
            {
                type = "TargetNumber",
                targetNumber = targetNumber,
                playerIndex = playerIndex
            };
            TcpHost.Instance.SendTurnData(targetMsg);
        }
        else
        {
            // 客户端请求目标数
            NetworkMessage requestMsg = new NetworkMessage
            {
                type = "RequestTargetNumber",
                playerIndex = playerIndex
            };
            TcpClientConnection.Instance.SendTurnData(requestMsg);
        }
    }

    public void SetTargetNumber(int number)
    {
        targetNumber = number;
        UpdateUI();
        Debug.Log($"设置目标数: {number}, 玩家: {playerIndex}");
    }

    public void StartGame(List<Card> initialHand)
    {
        gameState.StartGame();
        handManager.ShowHand(initialHand, OnCardClicked);
        selectedNumberCard = null;
        selectedOperatorCard = null;
        selectedSkillCard = null;
        UpdateUI();
    }

    // 检查手牌中是否有数字牌，如果没有则抽牌
    // 这段代码是临时功能，可以随时删除
    private void CheckAndDrawNumberCard()
    {
        bool hasNumberCard = false;
        foreach (var card in handManager.GetHand())
        {
            if (card.type == CardType.Number)
            {
                hasNumberCard = true;
                break;
            }
        }

        // 如果没有数字牌，尝试抽牌
        if (!hasNumberCard)
        {
            Debug.Log("当前手牌中没有数字牌，尝试抽牌...");
            Card newCard = CardDeckManager.Instance.DrawCard();
            if (newCard != null)
            {
                handManager.AddCard(newCard);
                Debug.Log($"抽到新牌: {newCard.GetDisplayText()}");
                
                // 如果抽到的不是数字牌，再次检查
                if (newCard.type != CardType.Number)
                {
                    Debug.Log("抽到的不是数字牌，跳过回合");
                    // 发送跳过回合消息
                    NetworkMessage skipMsg = new NetworkMessage
                    {
                        type = "SkipTurn",
                        playerIndex = playerIndex
                    };

                    if (playerIndex == 0)
                    {
                        TcpHost.Instance.SendTurnData(skipMsg);
                    }
                    else
                    {
                        TcpClientConnection.Instance.SendTurnData(skipMsg);
                    }
                }
            }
        }
    }

    private void OnCardClicked(Card card)
    {
        if (isProcessingTurn || isFrozen) return;

        // 如果卡牌已经被选中，则取消选择
        if ((card == selectedNumberCard) || (card == selectedOperatorCard) || (card == selectedSkillCard))
        {
            switch (card.type)
            {
                case CardType.Number:
                    selectedNumberCard = null;
                    break;
                case CardType.Operator:
                    selectedOperatorCard = null;
                    break;
                case CardType.Skill:
                    selectedSkillCard = null;
                    break;
            }
            handManager.DeselectCard(card);
            UpdatePlayCardButtonState();
            return;
        }

        // 选择新卡牌
        switch (card.type)
        {
            case CardType.Number:
                // 如果已经选择了数字牌，先取消选择
                if (selectedNumberCard != null)
                {
                    handManager.DeselectCard(selectedNumberCard);
                }
                selectedNumberCard = card;
                handManager.SelectCard(card);
                break;

            case CardType.Operator:
                // 如果已经选择了运算符牌，先取消选择
                if (selectedOperatorCard != null)
                {
                    handManager.DeselectCard(selectedOperatorCard);
                }
                selectedOperatorCard = card;
                handManager.SelectCard(card);
                break;

            case CardType.Skill:
                // 如果已经选择了技能牌，先取消选择
                if (selectedSkillCard != null)
                {
                    handManager.DeselectCard(selectedSkillCard);
                }
                selectedSkillCard = card;
                handManager.SelectCard(card);
                break;
        }

        // 更新出牌按钮状态
        UpdatePlayCardButtonState();
    }

    private void UpdatePlayCardButtonState()
    {
        if (playCardButton != null)
        {
            // 可以单独出技能牌，或者数字牌+运算符牌（可以带技能牌）
            bool canPlay = (selectedSkillCard != null) || (selectedNumberCard != null && selectedOperatorCard != null);
            playCardButton.interactable = canPlay && gameState.IsPlayerTurn(playerIndex) && !isProcessingTurn && !isFrozen;
        }
    }

    public void OnPlayCardButtonClicked()
    {
        // 如果选择了技能牌，先处理技能
        if (selectedSkillCard != null)
        {
            ProcessSkill();
            return;
        }
        
        // 否则处理数字和运算符
        if (selectedNumberCard != null && selectedOperatorCard != null)
        {
            ProcessTurn();
        }
    }

    public void OnDrawCardButtonClicked()
    {
        if (!gameState.IsPlayerTurn(playerIndex) || isProcessingTurn || isFrozen || hasDrawnCard)
        {
            string reason = isFrozen ? "被冻结" : (!gameState.IsPlayerTurn(playerIndex) ? "不是当前回合" : (hasDrawnCard ? "已经摸过牌" : "正在处理回合"));
            Debug.Log($"无法抽牌 - 原因: {reason}");
            return;
        }

        isProcessingTurn = true;

        // 清除所有选中状态
        selectedNumberCard = null;
        selectedOperatorCard = null;
        selectedSkillCard = null;
        handManager.DeselectCard();

        // 抽两张新牌
        for (int i = 0; i < 2; i++)
        {
            Card newCard = CardDeckManager.Instance.DrawCard();
            if (newCard != null)
            {
                handManager.AddCard(newCard);
                Debug.Log($"抽到新牌: {newCard.GetDisplayText()}");
            }
        }
        
        hasDrawnCard = true; // 标记已经摸过牌
        
        // 发送抽牌消息给对手
        NetworkMessage drawMsg = new NetworkMessage
        {
            type = "DrawCard",
            playerIndex = playerIndex
        };

        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(drawMsg);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(drawMsg);
        }

        isProcessingTurn = false;
        UpdatePlayCardButtonState(); // 更新出牌按钮状态
    }

    private void ProcessSkill()
    {
        if (selectedSkillCard == null) return;

        isProcessingTurn = true;

        NetworkMessage skillData = new NetworkMessage
        {
            type = "Skill",
            playerIndex = playerIndex,
            skillCard = selectedSkillCard
        };

        // 发送技能数据
        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(skillData);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(skillData);
        }

        // 处理本地技能效果
        ProcessSkillResult(skillData);
    }

    private void ProcessSkillResult(NetworkMessage skillData)
    {
        string skillText = $"玩家{skillData.playerIndex + 1} 使用了 {skillData.skillCard.GetSkillName()}";
        ShowResult(skillText);

        // 记录技能卡使用信息
        string cardInfo = skillData.playerIndex == playerIndex ? 
            $"我使用技能卡: [{skillData.skillCard.GetSkillName()}]" :
            $"对手使用技能卡: [{skillData.skillCard.GetSkillName()}]";
        Debug.Log(cardInfo);

        // 处理技能效果
        switch (skillData.skillCard.skillType)
        {
            case SkillType.Freeze:
                if (skillData.playerIndex != playerIndex)
                {
                    // 如果是对手使用冻结，标记自己被冻结
                    isFrozen = true;
                    Debug.Log($"玩家{playerIndex + 1}被冻结，下回合将被跳过");
                }
                break;

            case SkillType.Mirror:
                // 交换双方分数
                int player0Score = gameState.GetScore(0);
                int player1Score = gameState.GetScore(1);
                gameState.AddScore(0, player1Score);
                gameState.AddScore(1, player0Score);
                Debug.Log($"分数互换：玩家1 {player0Score} <-> 玩家2 {player1Score}");
                break;
        }

        // 如果是自己使用的技能牌
        if (skillData.playerIndex == playerIndex)
        {
            // 移除使用的技能卡
            if (selectedSkillCard != null)
            {
                handManager.RemoveCard(selectedSkillCard);
                selectedSkillCard = null;
            }

            // 如果同时选择了数字牌和运算符牌，继续处理
            if (selectedNumberCard != null && selectedOperatorCard != null)
            {
                ProcessTurn();
                return; // 让ProcessTurn处理回合结束
            }

            // 如果只使用了技能牌
            handManager.DeselectCard();
            hasDrawnCard = false;
            
            // 如果是冻结卡，不切换回合（让当前玩家继续出牌）
            if (skillData.skillCard.skillType != SkillType.Freeze)
            {
                gameState.NextTurn();
            }
        }
        else
        {
            // 如果是对手的回合，且不是冻结卡，才切换回合
            if (skillData.skillCard.skillType != SkillType.Freeze)
            {
                gameState.SetCurrentTurn((skillData.playerIndex + 1) % gameState.PlayerCount);
            }
        }

        UpdateUI();
        isProcessingTurn = false;
        Debug.Log($"技能回合结束，当前回合：{gameState.CurrentPlayerTurn}, 当前玩家：{playerIndex}, 是否是我的回合：{gameState.IsPlayerTurn(playerIndex)}");
    }

    private void ProcessTurn()
    {
        if (selectedNumberCard == null || selectedOperatorCard == null) return;

        isProcessingTurn = true;
        int playerValue = selectedNumberCard.numberValue;
        int result;

        // 检查当前领域效果
        GameField currentField = CardDeckManager.Instance.GetCurrentField();
        if (currentField == GameField.Square)
        {
            // 平方领域：数字自动平方
            playerValue = playerValue * playerValue;
        }
        else if (currentField == GameField.SquareRoot)
        {
            // 根号领域：数字自动取平方根
            playerValue = (int)Mathf.Sqrt(playerValue);
        }

        // 获取当前玩家的分数
        int currentScore = gameState.GetScore(playerIndex);

        // 处理不同类型的运算符
        if (selectedOperatorCard.type == CardType.ExtraOperator)
        {
            result = selectedOperatorCard.Calculate(currentScore);
        }
        else
        {
            result = selectedOperatorCard.Calculate(currentScore, playerValue);
        }

        // 创建回合数据
        NetworkMessage turnData = new NetworkMessage
        {
            type = "Turn",
            playerIndex = playerIndex,
            numberCard = selectedNumberCard,
            operatorCard = selectedOperatorCard,
            result = result,
            currentNumber = result,
            currentField = currentField
        };

        // 发送回合数据
        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(turnData);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(turnData);
        }

        // 处理本地回合结果
        ProcessTurnResult(turnData);

        // 检查是否达到目标数
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (gameEnded) return;

        if (currentNumber == targetNumber)
        {
            gameEnded = true;
            string winnerText = $"玩家{gameState.CurrentPlayerTurn + 1}获胜！达到目标数{targetNumber}";
            ShowResult(winnerText);

            // 发送游戏结束消息
            NetworkMessage gameOverMsg = new NetworkMessage
            {
                type = "GameOver",
                playerIndex = gameState.CurrentPlayerTurn,
                playerScores = new int[] { gameState.GetScore(0), gameState.GetScore(1) }
            };

            if (playerIndex == 0)
            {
                TcpHost.Instance.SendTurnData(gameOverMsg);
            }
            else
            {
                TcpClientConnection.Instance.SendTurnData(gameOverMsg);
            }
        }
    }

    private void ProcessTurnResult(NetworkMessage turnData)
    {
        // 保存最后一次操作，用于记录
        lastTurnData = turnData;

        // 更新当前数值
        currentNumber = turnData.currentNumber;

        // 记录出牌信息
        string cardInfo = "";
        if (turnData.playerIndex == playerIndex)
        {
            cardInfo = "我出牌: ";
            if (selectedSkillCard != null)
            {
                cardInfo += $"[技能卡: {selectedSkillCard.GetSkillName()}] ";
            }
            if (selectedNumberCard != null && selectedOperatorCard != null)
            {
                cardInfo += $"[数字卡: {selectedNumberCard.numberValue}] ";
                cardInfo += $"[运算符卡: {selectedOperatorCard.GetOperatorSymbol()}]";
            }
            
            // 记录计算过程
            int currentScore = gameState.GetScore(playerIndex);
            int result = turnData.result;
            string calculation = "";
            
            // 检查领域效果
            GameField currentField = CardDeckManager.Instance.GetCurrentField();
            if (currentField == GameField.Square)
            {
                calculation = $"领域效果(平方): {selectedNumberCard.numberValue} -> {selectedNumberCard.numberValue * selectedNumberCard.numberValue}\n";
            }
            else if (currentField == GameField.SquareRoot)
            {
                calculation = $"领域效果(根号): {selectedNumberCard.numberValue} -> {(int)Mathf.Sqrt(selectedNumberCard.numberValue)}\n";
            }
            
            // 记录计算过程
            if (selectedNumberCard != null && selectedOperatorCard != null)
            {
                if (selectedOperatorCard.type == CardType.Operator && 
                    (selectedOperatorCard.operatorType == OperatorType.Square || 
                     selectedOperatorCard.operatorType == OperatorType.SquareRoot))
                {
                    calculation += $"计算过程: {currentScore} {selectedOperatorCard.GetOperatorSymbol()} = {result}";
                }
                else
                {
                    calculation += $"计算过程: {currentScore} {selectedOperatorCard.GetOperatorSymbol()} {selectedNumberCard.numberValue} = {result}";
                }
            }
            
            Debug.Log(cardInfo + "\n" + calculation);
        }
        else
        {
            cardInfo = $"对手出牌: ";
            if (turnData.skillCard != null)
            {
                cardInfo += $"[技能卡: {turnData.skillCard.GetSkillName()}] ";
            }
            if (turnData.numberCard != null && turnData.operatorCard != null)
            {
                cardInfo += $"[数字卡: {turnData.numberCard.numberValue}] ";
                cardInfo += $"[运算符卡: {turnData.operatorCard.GetOperatorSymbol()}]";
            }
            
            // 记录对手的计算过程
            int opponentScore = gameState.GetScore(turnData.playerIndex);
            string calculation = "";
            
            // 检查领域效果
            if (turnData.currentField == GameField.Square)
            {
                calculation = $"领域效果(平方): {turnData.numberCard.numberValue} -> {turnData.numberCard.numberValue * turnData.numberCard.numberValue}\n";
            }
            else if (turnData.currentField == GameField.SquareRoot)
            {
                calculation = $"领域效果(根号): {turnData.numberCard.numberValue} -> {(int)Mathf.Sqrt(turnData.numberCard.numberValue)}\n";
            }
            
            // 记录计算过程
            if (turnData.numberCard != null && turnData.operatorCard != null)
            {
                if (turnData.operatorCard.type == CardType.Operator && 
                    (turnData.operatorCard.operatorType == OperatorType.Square || 
                     turnData.operatorCard.operatorType == OperatorType.SquareRoot))
                {
                    calculation += $"计算过程: {opponentScore} {turnData.operatorCard.GetOperatorSymbol()} = {turnData.result}";
                }
                else
                {
                    calculation += $"计算过程: {opponentScore} {turnData.operatorCard.GetOperatorSymbol()} {turnData.numberCard.numberValue} = {turnData.result}";
                }
            }
            
            Debug.Log(cardInfo + "\n" + calculation);
        }

        string resultText;
        if (turnData.operatorCard != null && turnData.operatorCard.type == CardType.Operator && 
            (turnData.operatorCard.operatorType == OperatorType.Square || 
             turnData.operatorCard.operatorType == OperatorType.SquareRoot))
        {
            resultText = $"玩家{turnData.playerIndex + 1}: {turnData.operatorCard.GetOperatorSymbol()}{gameState.GetScore(turnData.playerIndex)} = {turnData.result}";
        }
        else if (turnData.numberCard != null && turnData.operatorCard != null)
        {
            resultText = $"玩家{turnData.playerIndex + 1}: {gameState.GetScore(turnData.playerIndex)} {turnData.operatorCard.GetOperatorSymbol()} {turnData.numberCard.numberValue} = {turnData.result}";
        }
        else
        {
            resultText = $"玩家{turnData.playerIndex + 1} 使用了技能";
        }
        ShowResult(resultText);

        // 只有在自己的回合才移除卡牌
        if (turnData.playerIndex == playerIndex)
        {
            // 移除使用的卡牌
            if (selectedNumberCard != null)
            {
                handManager.RemoveCard(selectedNumberCard);
            }
            if (selectedOperatorCard != null)
            {
                handManager.RemoveCard(selectedOperatorCard);
            }
            if (selectedSkillCard != null)
            {
                handManager.RemoveCard(selectedSkillCard);
            }

            // 清除选择
            selectedNumberCard = null;
            selectedOperatorCard = null;
            selectedSkillCard = null;
            handManager.DeselectCard();

            // 重置摸牌状态
            hasDrawnCard = false;

            // 只在本地玩家回合结束时切换回合
            gameState.NextTurn();
            CardDeckManager.Instance.NextRound();

            // 检查游戏是否结束
            if (CardDeckManager.Instance.IsGameOver())
            {
                EndGame();
            }
        }
        else
        {
            // 如果是对手的回合，更新当前回合为对手的下一个回合
            gameState.SetCurrentTurn((turnData.playerIndex + 1) % gameState.PlayerCount);
        }

        // 更新分数
        gameState.AddScore(turnData.playerIndex, turnData.result);

        // 检查是否达到目标数
        CheckWinCondition();

        // 如果是被冻结的玩家的回合开始，解除冻结状态
        if (turnData.playerIndex != playerIndex && isFrozen)
        {
            isFrozen = false;
            Debug.Log($"玩家{playerIndex + 1}的冻结状态已解除");
        }

        UpdateUI();

        isProcessingTurn = false;
        Debug.Log($"回合结束，当前回合：{gameState.CurrentPlayerTurn}, 当前玩家：{playerIndex}, 是否是我的回合：{gameState.IsPlayerTurn(playerIndex)}");
    }

    private void DrawNewCards()
    {
        // 抽两张新牌
        for (int i = 0; i < 2; i++)
        {
            Card newCard = CardDeckManager.Instance.DrawCard();
            if (newCard != null)
            {
                handManager.AddCard(newCard);
            }
        }
    }

    private void UpdateUI()
    {
        string status = isFrozen ? " (已冻结)" : "";
        string fieldStatus = "";
        GameField currentField = CardDeckManager.Instance.GetCurrentField();
        switch (currentField)
        {
            case GameField.Square:
                fieldStatus = " (平方领域)";
                break;
            case GameField.SquareRoot:
                fieldStatus = " (根号领域)";
                break;
        }
        turnText.text = $"当前回合: 玩家{gameState.CurrentPlayerTurn + 1}{status}{fieldStatus}";
        
        // 更新分数显示
        player1ScoreText.text = $"{gameState.GetScore(playerIndex)}";
        player2ScoreText.text = $"{gameState.GetScore((playerIndex + 1) % 2)}";
        
        targetNumberText.text = $"{targetNumber}";
        if (CardDeckManager.Instance.GetCurrentRound() > 0)
        {
            targetNumberText.text += $"\n回合: {CardDeckManager.Instance.GetCurrentRound()}/10";
        }

        if (drawCardButton != null)
        {
            drawCardButton.interactable = gameState.IsPlayerTurn(playerIndex) && !isProcessingTurn && !isFrozen && !hasDrawnCard;
        }

        UpdatePlayCardButtonState();
    }

    private void ShowResult(string text)
    {
        resultText.text = text;
        resultTextObject.SetActive(true);
        StartCoroutine(HideResultAfterDelay());
    }

    private IEnumerator HideResultAfterDelay()
    {
        yield return new WaitForSeconds(resultDisplayTime);
        resultTextObject.SetActive(false);
    }

    public void OnOpponentTurn(NetworkMessage message)
    {
        if (message.type == "GameOver")
        {
            gameEnded = true;
            string winnerText = $"玩家{message.playerIndex + 1}获胜！达到目标数{targetNumber}";
            ShowResult(winnerText);
            return;
        }

        if (message.type == "Skill")
        {
            ProcessSkillResult(message);
        }
        else if (message.type == "TargetNumber")
        {
            Debug.Log($"收到目标数: {message.targetNumber}, 玩家: {playerIndex}");
            SetTargetNumber(message.targetNumber);
        }
        else if (message.type == "RequestTargetNumber")
        {
            // 主机收到请求后发送目标数
            if (playerIndex == 0)
            {
                Debug.Log($"收到目标数请求，发送目标数: {targetNumber}");
                NetworkMessage targetMsg = new NetworkMessage
                {
                    type = "TargetNumber",
                    targetNumber = targetNumber,
                    playerIndex = playerIndex
                };
                TcpHost.Instance.SendTurnData(targetMsg);
            }
        }
        else
        {
            ProcessTurnResult(message);
        }
    }

    public int GetTargetNumber()
    {
        return targetNumber;
    }

    private void EndGame()
    {
        gameEnded = true;
        int player1Score = gameState.GetScore(0);
        int player2Score = gameState.GetScore(1);
        int player1Diff = Mathf.Abs(targetNumber - player1Score);
        int player2Diff = Mathf.Abs(targetNumber - player2Score);

        string winnerText;
        if (player1Diff < player2Diff)
        {
            winnerText = $"玩家1获胜！\n玩家1: {player1Score} (差值: {player1Diff})\n玩家2: {player2Score} (差值: {player2Diff})";
        }
        else if (player2Diff < player1Diff)
        {
            winnerText = $"玩家2获胜！\n玩家1: {player1Score} (差值: {player1Diff})\n玩家2: {player2Score} (差值: {player2Diff})";
        }
        else
        {
            winnerText = $"平局！\n玩家1: {player1Score} (差值: {player1Diff})\n玩家2: {player2Score} (差值: {player2Diff})";
        }

        ShowResult(winnerText);

        // 发送游戏结束消息
        NetworkMessage gameOverMsg = new NetworkMessage
        {
            type = "GameOver",
            playerIndex = player1Diff < player2Diff ? 0 : (player2Diff < player1Diff ? 1 : -1),
            playerScores = new int[] { player1Score, player2Score }
        };

        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(gameOverMsg);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(gameOverMsg);
        }
    }
}