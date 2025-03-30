using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public HandManager handManager;
    public PlayedCardsManager playedCardsManager;
    public GameObject resultTextObject;
    public Text resultText;
    public Text turnText;
    public Text player1ScoreText;
    public Text player2ScoreText;
    public Text targetNumberText;
    public Button drawCardButton;
    public Button playCardButton;
    public float resultDisplayTime = 2f;
    
    // 修改为共用结果面板
    public GameObject gameResultPanel;
    public Text gameResultText;
    
    // 添加三个Text用于显示牌库数量
    public Text numberCardCountText;  // 显示数字牌剩余数量
    public Text operatorCardCountText; // 显示运算符牌剩余数量
    public Text skillCardCountText;    // 显示技能牌剩余数量

    private Card selectedNumberCard;
    private Card selectedOperatorCard;
    private Card selectedSkillCard;
    private GameState gameState;
    private int playerIndex;
    private bool isProcessingTurn;
    private bool isPlayCard;
    private bool isFrozen;
    private NetworkMessage lastTurnData;
    private int targetNumber;
    private int currentNumber; // 当前累计数值
    private bool gameEnded = false;

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
        isPlayCard = false;
        isFrozen = false;
        lastTurnData = null;
        currentNumber = 1; // 初始值为1
        gameEnded = false;
        
        // 确保胜利和失败窗口隐藏
        if (gameResultPanel != null)
        {
            gameResultPanel.SetActive(false);
        }

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
        playedCardsManager.ClearCards(true);
        playedCardsManager.ClearCards(false);
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
            // 只要是我的回合且没有被冻结，就可以出牌（包括不出牌）
            bool canPlay = gameState.IsPlayerTurn(playerIndex) && !isFrozen && !isPlayCard;
            
            // 如果正在处理回合，禁用按钮
            if (isProcessingTurn)
            {
                canPlay = false;
            }
            
            playCardButton.interactable = canPlay;
            
            if (canPlay)
            {
                Debug.Log("出牌按钮已启用");
            }
            else
            {
                string reason = !gameState.IsPlayerTurn(playerIndex) ? "非当前回合" : 
                               (isFrozen ? "被冻结" : 
                               (isProcessingTurn ? "正在处理回合" : "未知原因"));
                Debug.Log($"出牌按钮已禁用，原因: {reason}");
            }
        }
    }

    public void OnPlayCardButtonClicked()
    {
        if (!gameState.IsPlayerTurn(playerIndex) || isProcessingTurn || isFrozen)
        {
            return;
        }

        isProcessingTurn = true;
        isPlayCard = true;

        // 如果没有选择任何牌，直接跳过回合
        //if (selectedNumberCard == null && selectedOperatorCard == null && selectedSkillCard == null)
        //{
        //    // 发送跳过回合消息
        //    NetworkMessage skipMsg = new NetworkMessage
        //    {
        //        type = "SkipTurn",
        //        playerIndex = playerIndex
        //    };

        //    if (playerIndex == 0)
        //    {
        //        TcpHost.Instance.SendTurnData(skipMsg);
        //    }
        //    else
        //    {
        //        TcpClientConnection.Instance.SendTurnData(skipMsg);
        //    }

        //    // 处理本地跳过回合
        //    int nextPlayerIndex = (playerIndex + 1) % gameState.PlayerCount;
        //    gameState.SetCurrentTurn(nextPlayerIndex);
        //    Debug.Log($"手动跳过回合，切换到玩家{nextPlayerIndex + 1}的回合");
            
        //    hasDrawnCard = false; // 重置抽牌状态
        //    UpdateUI();
        //    isProcessingTurn = false;
        //    return;
        //}

        // 如果只选择了数字牌或运算符牌，取消选择并返回
        if ((selectedNumberCard != null && selectedOperatorCard == null) || 
            (selectedNumberCard == null && selectedOperatorCard != null))
        {
            // 取消选择
            if (selectedNumberCard != null)
            {
                handManager.DeselectCard(selectedNumberCard);
                selectedNumberCard = null;
            }
            if (selectedOperatorCard != null)
            {
                handManager.DeselectCard(selectedOperatorCard);
                selectedOperatorCard = null;
            }
            isProcessingTurn = false;
            return;
        }

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

    // 检查手牌上限并处理
    private void CheckHandLimit()
    {
        int handCount = handManager.GetHand().Count;
        int handLimit = 6; // 手牌上限
        
        if (handCount > handLimit)
        {
            // 显示提示信息，要求玩家弃牌
            ShowResult($"手牌超过上限({handLimit}张)，请选择弃置卡牌");
            
            // 理论上这里应该激活弃牌选择UI
            // 简化处理：自动弃置最后抽到的牌
            int cardsToDiscard = handCount - handLimit;
            for (int i = 0; i < cardsToDiscard; i++)
            {
                List<Card> hand = handManager.GetHand();
                if (hand.Count > 0)
                {
                    Card cardToDiscard = hand[hand.Count - 1];
                    handManager.RemoveCard(cardToDiscard);
                    Debug.Log($"自动弃置卡牌: {cardToDiscard.GetDisplayText()}");
                }
            }
        }
    }

    public void OnDrawCardButtonClicked()
    {
        if (!gameState.IsPlayerTurn(playerIndex) || isFrozen || !isPlayCard)
        {
            string reason = (!gameState.IsPlayerTurn(playerIndex)).ToString() + isFrozen.ToString() + (!isPlayCard).ToString();
            Debug.Log($"无法抽牌 - 原因: {reason}");
            return;
        }

        // 检查当前手牌数量
        int currentHandCount = handManager.GetHand().Count;
        if (currentHandCount >= 6)
        {
            Debug.Log("手牌已达到上限(6张)，无法摸牌");
            return;
        }

        isProcessingTurn = true;

        // 清除所有选中状态
        selectedNumberCard = null;
        selectedOperatorCard = null;
        selectedSkillCard = null;
        handManager.DeselectCard();

        // 计算最多可以摸的牌数
        int maxCardsToDraw = Mathf.Min(2, 6 - currentHandCount);
        
        // 这里可以添加UI让玩家选择抽几张牌
        // 为简化实现，我们暂时还是默认抽最大数量的牌
        int cardsToDraw = maxCardsToDraw;
        
        // 如果是主机，处理抽牌逻辑
        if (playerIndex == 0)
        {
            // 创建临时列表记录抽到的牌
            List<Card> drawnCards = new List<Card>();
            
            // 主机直接从牌堆抽牌
            for (int i = 0; i < cardsToDraw; i++)
            {
                Card newCard = CardDeckManager.Instance.DrawCard();
                if (newCard != null)
                {
                    handManager.AddCard(newCard);
                    drawnCards.Add(newCard);
                    Debug.Log($"抽到新牌: {newCard.GetDisplayText()}");
                }
            }
            
            // 检查手牌上限
            CheckHandLimit();
            
            
            // 发送抽牌消息给客户端
            NetworkMessage drawMsg = new NetworkMessage
            {
                type = "DrawCard",
                playerIndex = playerIndex,
                cardsDrawn = drawnCards.Count,
                drawnCards = drawnCards // 使用新字段传递抽到的卡牌
            };
            
            TcpHost.Instance.SendTurnData(drawMsg);
            
            // 同步一次牌库数量
            NetworkMessage deckUpdateMsg = new NetworkMessage
            {
                type = "DeckUpdate",
                numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
            };
            TcpHost.Instance.SendTurnData(deckUpdateMsg);
            
            // 立即更新UI显示最新的牌库数量
            UpdateUI();

            // 切换到下一个玩家的回合
            int nextPlayerIndex = (playerIndex + 1) % gameState.PlayerCount;
            gameState.SetCurrentTurn(nextPlayerIndex);
            Debug.Log($"摸牌完成，切换到玩家{nextPlayerIndex + 1}的回合");
        }
        else
        {
            // 客户端发送抽牌请求给主机
            NetworkMessage drawMsg = new NetworkMessage
            {
                type = "DrawCard",
                playerIndex = playerIndex,
                cardsDrawn = cardsToDraw, // 请求抽牌数量
            };
            
            TcpClientConnection.Instance.SendTurnData(drawMsg);

            isPlayCard = true;
            
            // 显示抽牌中提示
            ShowResult("正在抽牌...");
        }

        isProcessingTurn = false;
        UpdateUI();
    }

    private void ProcessSkill()
    {
        if (selectedSkillCard == null) return;

        isProcessingTurn = true;

        Card tempSkillCard = selectedSkillCard;
        selectedSkillCard = null; // 清空选中的技能牌引用

        // 移除使用的技能卡并添加到历史区域
        handManager.RemoveCard(tempSkillCard);
        playedCardsManager.AddCard(tempSkillCard, true);

        // 立即更新UI以反映卡牌数量变化
        UpdateUI();

        NetworkMessage skillData = new NetworkMessage
        {
            type = "Skill",
            playerIndex = playerIndex,
            playedCards = new List<Card> { tempSkillCard },  // 使用playedCards列表
            skillCard = tempSkillCard  // 保持兼容性
        };

        // 发送技能数据
        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(skillData);
            
            // 如果是主机，同步一次牌库数量
            NetworkMessage deckUpdateMsg = new NetworkMessage
            {
                type = "DeckUpdate",
                numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
            };
            TcpHost.Instance.SendTurnData(deckUpdateMsg);
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
        // 获取技能卡（从playedCards或skillCard字段）
        Card skillCard = skillData.playedCards?.Count > 0 ? skillData.playedCards[0] : skillData.skillCard;
        if (skillCard == null) return;

        string skillText = $"玩家{skillData.playerIndex + 1} 使用了 {skillCard.GetSkillName()}";
        ShowResult(skillText);

        // 记录技能卡使用信息
        string cardInfo = skillData.playerIndex == playerIndex ? 
            $"我使用技能卡: [{skillCard.GetSkillName()}]" :
            $"对手使用技能卡: [{skillCard.GetSkillName()}]";
        Debug.Log(cardInfo);

        // 处理技能效果
        switch (skillCard.skillType)
        {
            case SkillType.Freeze:
                if (skillData.playerIndex == playerIndex)
                {
                    // 如果是自己使用冻结，对方应该被冻结
                    // 发送冻结状态给对方
                    NetworkMessage freezeResponse = new NetworkMessage
                    {
                        type = "FreezeStatus",
                        playerIndex = (playerIndex + 1) % 2, // 对方的索引
                        isFrozen = true
                    };
                    
                    // 发送冻结状态
                    if (playerIndex == 0)
                    {
                        TcpHost.Instance.SendTurnData(freezeResponse);
                    }
                    else
                    {
                        TcpClientConnection.Instance.SendTurnData(freezeResponse);
                    }
                    
                    Debug.Log($"我使用冻结卡，对手(玩家{(playerIndex + 1) % 2 + 1})将被冻结");
                }
                else
                {
                    // 如果是对手使用冻结，自己被冻结
                    isFrozen = true;
                    Debug.Log($"玩家{playerIndex + 1}被冻结，下回合将被跳过");
                    
                    // 同步冻结状态回应
                    NetworkMessage freezeResponse = new NetworkMessage
                    {
                        type = "FreezeStatus",
                        playerIndex = playerIndex,
                        isFrozen = true
                    };
                    
                    // 发送冻结状态确认
                    if (playerIndex == 0)
                    {
                        TcpHost.Instance.SendTurnData(freezeResponse);
                    }
                    else
                    {
                        TcpClientConnection.Instance.SendTurnData(freezeResponse);
                    }
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
            // 移除使用的技能卡已经在ProcessSkill中处理，这里不需要重复
            // 如果同时选择了数字牌和运算符牌，继续处理
            if (selectedNumberCard != null && selectedOperatorCard != null)
            {
                ProcessTurn();
                return; // 让ProcessTurn处理回合结束
            }

            // 如果只使用了技能牌
            handManager.DeselectCard();
            
            // 确保当前回合仍然是自己的
            gameState.SetCurrentTurn(playerIndex);
            
            // 无论是什么技能卡，都不切换回合（让当前玩家继续出牌）
            isProcessingTurn = false;
            UpdateUI();
            UpdatePlayCardButtonState(); // 再次更新按钮状态确保可交互
            Debug.Log("使用技能牌后继续保持回合，可以继续出牌");
            return;
        }
        else
        {
            // 如果是对手使用的技能牌，添加到对手的历史牌区域
            if (skillCard != null)
            {
                playedCardsManager.AddCard(skillCard, false);
            }
            
            // 如果是对手使用技能牌，不影响当前回合
            // 对手回合继续进行，不用做任何处理
            Debug.Log("对手使用技能牌，等待对手行动");
        }

        UpdateUI();
        isProcessingTurn = false;
        Debug.Log($"技能回合结束，当前回合：{gameState.CurrentPlayerTurn}, 当前玩家：{playerIndex}, 是否是我的回合：{gameState.IsPlayerTurn(playerIndex)}");
    }

    private void ProcessTurn()
    {
        if (selectedNumberCard == null) return;

        isProcessingTurn = true;
        List<Card> playedCards = new List<Card>();

        // 添加数字牌
        if (selectedNumberCard != null)
        {
            playedCards.Add(selectedNumberCard);
            handManager.RemoveCard(selectedNumberCard);
            playedCardsManager.AddCard(selectedNumberCard, true); // 添加到玩家区域
        }

        // 添加运算符牌
        if (selectedOperatorCard != null)
        {
            playedCards.Add(selectedOperatorCard);
            handManager.RemoveCard(selectedOperatorCard);
            playedCardsManager.AddCard(selectedOperatorCard, true); // 添加到玩家区域
        }

        // 添加技能牌
        if (selectedSkillCard != null)
        {
            playedCards.Add(selectedSkillCard);
            handManager.RemoveCard(selectedSkillCard);
            playedCardsManager.AddCard(selectedSkillCard, true); // 添加到玩家区域
        }

        // 检查当前领域效果
        GameField currentField = CardDeckManager.Instance.GetCurrentField();
        if (currentField == GameField.Square)
        {
            // 平方领域：数字自动平方
            foreach (var card in playedCards)
            {
                if (card.type == CardType.Number)
                {
                    card.numberValue = card.numberValue * card.numberValue;
                }
            }
        }
        else if (currentField == GameField.SquareRoot)
        {
            // 根号领域：数字自动取平方根
            foreach (var card in playedCards)
            {
                if (card.type == CardType.Number)
                {
                    card.numberValue = (int)Mathf.Sqrt(card.numberValue);
                }
            }
        }

        // 处理不同类型的运算符
        int result = CalculateResult(playedCards);
        gameState.AddScore(playerIndex, result);

        // 创建网络消息
        NetworkMessage message = new NetworkMessage
        {
            type = "Turn",
            playerIndex = playerIndex,
            playedCards = playedCards,
            result = result,
            currentField = currentField
        };

        // 发送消息
        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(message);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(message);
        }

        // 重置选择
        selectedNumberCard = null;
        selectedOperatorCard = null;
        selectedSkillCard = null;
        handManager.DeselectCard();
        
        // 更新UI
        UpdateUI();
        
        // 检查胜利条件
        if (result >= targetNumber)
        {
            Debug.Log($"玩家{playerIndex + 1}获胜！得分：{result}");
            
            // 发送游戏结束消息
            NetworkMessage gameOverMsg = new NetworkMessage
            {
                type = "GameOver",
                playerIndex = playerIndex,
                result = result,
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
            
            EndGame();
            return;
        }

        // 出牌后不切换回合，等待玩家摸牌
        isProcessingTurn = false;
        isPlayCard = false;
        Debug.Log("出牌完成，等待玩家摸牌");
    }
    
    private string FormatTargetNumber(int number)
    {
        // 将数字转换为字符串，确保是三位数
        string numberStr = number.ToString("D3");
        // 在每个数字之间添加空格
        return string.Join(" ", numberStr.ToCharArray());
    }

    private void UpdateUI()
    {
        // 更新回合显示
        string turnString = gameState.IsPlayerTurn(playerIndex) ? "你的回合" : "对手回合";
        if (isFrozen && gameState.IsPlayerTurn(playerIndex))
        {
            turnString += " (已冻结)";
        }
        
        if (turnText != null)
        {
            turnText.text = turnString;
        }

        // 更新目标数字显示
        if (targetNumberText != null)
        {
            targetNumberText.text = FormatTargetNumber(targetNumber);
        }

        // 更新分数显示
        if (player1ScoreText != null)
        {
            player1ScoreText.text = $"玩家1: {gameState.GetScore(0)}";
        }
        if (player2ScoreText != null)
        {
            player2ScoreText.text = $"玩家2: {gameState.GetScore(1)}";
        }

        // 更新按钮状态
        if (drawCardButton != null)
        {
            // 只要是我的回合且没有被冻结，就可以摸牌
            bool canDraw = gameState.IsPlayerTurn(playerIndex) && !isFrozen && isPlayCard;
            drawCardButton.interactable = canDraw;

            if (canDraw)
            {
                Debug.Log("摸牌按钮已启用");
            }
            else
            {
                string reason = !gameState.IsPlayerTurn(playerIndex) ? "非当前回合" : 
                               (isFrozen ? "被冻结" : 
                               (isProcessingTurn ? "正在处理回合" : "未知原因"));
                Debug.Log($"摸牌按钮已禁用，原因: {reason}");
            }
        }

        // 更新出牌按钮状态
        if (playCardButton != null)
        {
            // 只要是我的回合且没有被冻结，就可以出牌
            bool canPlay = gameState.IsPlayerTurn(playerIndex) && !isFrozen && !isProcessingTurn && !isPlayCard;
            playCardButton.interactable = canPlay;
            
            if (canPlay)
            {
                Debug.Log("出牌按钮已启用");
            }
            else
            {
                string reason = !gameState.IsPlayerTurn(playerIndex) ? "非当前回合" : 
                               (isFrozen ? "被冻结" : 
                               (isProcessingTurn ? "正在处理回合" : "未知原因"));
                Debug.Log($"出牌按钮已禁用，原因: {reason}");
            }
        }

        // 更新牌库数量显示
        UpdateCardCountTexts();
        
        Debug.Log($"界面已更新 - 当前回合:{gameState.CurrentPlayerTurn} 我的索引:{playerIndex} 是否我的回合:{gameState.IsPlayerTurn(playerIndex)} 冻结:{isFrozen}");
    }
    
    // 更新卡牌数量显示
    private void UpdateCardCountTexts()
    {
        // 只有主机才会重新计算牌库数量
        if (playerIndex == 0)
        {
            int numberCount = CardDeckManager.Instance.GetNumberCardCount();
            int operatorCount = CardDeckManager.Instance.GetOperatorCardCount();
            int skillCount = CardDeckManager.Instance.GetSkillCardCount();
            
            Debug.Log($"[主机] 更新卡牌数量显示 - 数字:{numberCount} 运算符:{operatorCount} 技能:{skillCount}");
            
            if (numberCardCountText != null)
            {
                numberCardCountText.text = $"数字牌: {numberCount}";
            }
            
            if (operatorCardCountText != null)
            {
                operatorCardCountText.text = $"运算符: {operatorCount}";
            }
            
            if (skillCardCountText != null)
            {
                skillCardCountText.text = $"技能牌: {skillCount}";
            }
        }
        // 客户端不主动更新，只依赖服务器发来的数据
    }
    
    private IEnumerator AutoSkipFrozenTurn()
    {
        if (!isFrozen || !gameState.IsPlayerTurn(playerIndex) || isProcessingTurn)
        {
            yield break; // 如果不满足条件，直接退出
        }
        
        isProcessingTurn = true;
        ShowResult($"玩家{playerIndex + 1}被冻结，跳过回合");
        
        yield return new WaitForSeconds(1.5f);
        
        // 发送跳过回合消息
        NetworkMessage skipMsg = new NetworkMessage
        {
            type = "SkipTurn",
            playerIndex = playerIndex,
            isFrozen = true
        };
        
        if (playerIndex == 0)
        {
            TcpHost.Instance.SendTurnData(skipMsg);
        }
        else
        {
            TcpClientConnection.Instance.SendTurnData(skipMsg);
        }
        
        // 重置冻结状态
        isFrozen = false;
        
        // 计算下一个玩家的索引
        int nextPlayerIndex = (playerIndex + 1) % gameState.PlayerCount;
        gameState.SetCurrentTurn(nextPlayerIndex);
        Debug.Log($"因冻结跳过回合，切换到玩家{nextPlayerIndex + 1}的回合");
        
        isPlayCard = false;
        UpdateUI();
        isProcessingTurn = false;
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
        isProcessingTurn = true;

        string msgInfo = $"处理对手消息: {message.type}, 来自玩家: {message.playerIndex}";
        Debug.Log(msgInfo);
        
        // 更新卡牌数量信息（如果有）
        if (message.numberCardCount > 0 || message.operatorCardCount > 0 || message.skillCardCount > 0)
        {
            // 记录接收到的卡牌数量信息
            Debug.Log($"[玩家{playerIndex+1}] 收到卡牌数量同步 - 数字:{message.numberCardCount} 运算符:{message.operatorCardCount} 技能:{message.skillCardCount}");
            
            // 由于服务器同步的是卡牌数量，客户端不需要自己计算，直接显示收到的数值
            if (numberCardCountText != null)
            {
                numberCardCountText.text = $"数字牌: {message.numberCardCount}";
            }
            
            if (operatorCardCountText != null)
            {
                operatorCardCountText.text = $"运算符: {message.operatorCardCount}";
            }
            
            if (skillCardCountText != null)
            {
                skillCardCountText.text = $"技能牌: {message.skillCardCount}";
            }
            
            // 更新本地CardDeckManager中的牌库计数(仅做显示用，不影响实际牌库)
            CardDeckManager.Instance.SyncCardCounts(message.numberCardCount, message.operatorCardCount, message.skillCardCount);
        }

        if (message.type == "TargetNumber")
        {
            SetTargetNumber(message.targetNumber);
        }
        else if (message.type == "GameStart")
        {
            // 已在HandleMessage里处理
        }
        else if (message.type == "Turn")
        {
            // 如果对方出牌后轮到我的回合，但我被冻结，自动跳过
            bool shouldSkipAfterProcess = message.playerIndex != playerIndex && isFrozen;
            
            // 先处理对方的出牌
            ProcessTurnResult(message);
            
            // 如果我被冻结且轮到我的回合，自动跳过
            if (shouldSkipAfterProcess && gameState.IsPlayerTurn(playerIndex))
            {
                StartCoroutine(AutoSkipFrozenTurn());
            }
        }
        else if (message.type == "Skill")
        {
            ProcessSkillResult(message);
        }
        else if (message.type == "DrawCard")
        {
            Debug.Log($"对手抽牌：{message.cardsDrawn}张");
            // 对手摸牌后，切换到我的回合
            gameState.SetCurrentTurn(playerIndex);
            isPlayCard = false;
            UpdateUI();
        }
        else if (message.type == "DrawCardResponse")
        {
            Debug.Log($"收到抽牌响应：抽到{message.cardsDrawn}张");
            // 显示抽牌结果
            if (message.cardsDrawn > 0)
            {
                ShowResult($"抽到{message.cardsDrawn}张牌");
            }
            else
            {
                ShowResult("牌库已空");
            }
            UpdateUI();
        }
        else if (message.type == "SkipTurn")
        {
            Debug.Log($"对手跳过回合(玩家{message.playerIndex + 1})");
            // 对手跳过回合，设置为我方回合
            gameState.SetCurrentTurn(playerIndex);
            isPlayCard = false;
            UpdateUI();
            
            // 如果我方被冻结，也要自动跳过回合
            if (isFrozen)
            {
                StartCoroutine(AutoSkipFrozenTurn());
            }
        }
        else if (message.type == "FreezeStatus")
        {
            // 更新冻结状态同步
            if (message.playerIndex == playerIndex)
            {
                isFrozen = message.isFrozen;
                Debug.Log($"收到冻结状态更新，我的冻结状态：{message.isFrozen}");
                
                // 如果当前是我的回合且被冻结，自动跳过
                if (gameState.IsPlayerTurn(playerIndex) && isFrozen)
                {
                    StartCoroutine(AutoSkipFrozenTurn());
                }
            }
        }
        else if (message.type == "DeckUpdate")
        {
            // 处理牌库数量更新消息
            Debug.Log($"[玩家{playerIndex+1}] 收到牌库数量更新消息: 数字:{message.numberCardCount} 运算符:{message.operatorCardCount} 技能:{message.skillCardCount}");
            UpdateCardCountDisplay(message.numberCardCount, message.operatorCardCount, message.skillCardCount);
        }
        else if (message.type == "GameOver")
        {
            // 处理游戏结束消息
            gameEnded = true;
            string winnerText = $"玩家{message.playerIndex + 1}获胜！达到目标数{targetNumber}";
            ShowResult(winnerText);
            
            // 使用相同的延迟逻辑显示失败结果
            StartCoroutine(DelayedShowDefeatPanel(message.result));
            
            // 禁用游戏按钮
            DisableGameButtons();
        }

        isProcessingTurn = false;
        
        // 确保UI状态更新
        UpdateUI();
        UpdatePlayCardButtonState();
        
        Debug.Log($"处理完毕，当前回合玩家索引：{gameState.CurrentPlayerTurn}, 我的索引：{playerIndex}, 是否是我的回合：{gameState.IsPlayerTurn(playerIndex)}, 冻结状态：{isFrozen}");
    }
    
    private IEnumerator DelayedShowDefeatPanel(int opponentScore)
    {
        // 延迟1.5秒，让玩家先看到分数和失败提示
        yield return new WaitForSeconds(1.5f);
        
        if (gameResultPanel != null && gameResultText != null)
        {
            gameResultText.text = $"你输了！\n你的分数: {gameState.GetScore(playerIndex)}\n对手分数: {opponentScore}\n目标数: {targetNumber}";
            gameResultPanel.SetActive(true);
        }
    }

    public int GetTargetNumber()
    {
        return targetNumber;
    }

    private void EndGame()
    {
        gameEnded = true;
        
        // 显示结果文本
        string winnerText = $"玩家{playerIndex + 1}获胜！达到目标数{targetNumber}";
        ShowResult(winnerText);
        
        // 发送游戏结束消息
        NetworkMessage gameOverMsg = new NetworkMessage
        {
            type = "GameOver",
            playerIndex = playerIndex,
            result = gameState.GetScore(playerIndex),
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
        
        // 设置结果面板显示胜利信息，但稍微延迟显示
        StartCoroutine(DelayedShowVictoryPanel());
        
        // 禁用游戏按钮
        DisableGameButtons();
    }
    
    private IEnumerator DelayedShowVictoryPanel()
    {
        // 延迟1.5秒，让玩家先看到分数和胜利提示
        yield return new WaitForSeconds(1.5f);
        
        if (gameResultPanel != null && gameResultText != null)
        {
            gameResultText.text = $"你赢了！\n最终分数: {gameState.GetScore(playerIndex)}\n目标数: {targetNumber}";
            gameResultPanel.SetActive(true);
        }
    }
    
    private void DisableGameButtons()
    {
        if (drawCardButton != null)
        {
            drawCardButton.interactable = false;
        }
        
        if (playCardButton != null)
        {
            playCardButton.interactable = false;
        }
    }

    private void ProcessTurnResult(NetworkMessage turnData)
    {
        if (turnData == null) return;

        // 处理对手打出的牌
        if (turnData.playedCards != null && turnData.playerIndex != playerIndex)
        {
            foreach (var card in turnData.playedCards)
            {
                playedCardsManager.AddCard(card, false); // 添加到对手区域
            }
        }

        // 保存最后一次操作，用于记录
        lastTurnData = turnData;

        // 更新当前玩家的分数
        gameState.AddScore(turnData.playerIndex, turnData.result);

        // 记录出牌信息
        string cardInfo;
        if (turnData.playerIndex == playerIndex)
        {
            cardInfo = "我出牌: ";
        }
        else
        {
            cardInfo = "对手出牌: ";
        }
        
        foreach (var card in turnData.playedCards)
        {
            cardInfo += $"[{card.GetDisplayText()}] ";
        }
        Debug.Log(cardInfo);
        
        // 记录领域效果
        if (turnData.currentField != GameField.Normal)
        {
            string fieldEffect = turnData.currentField == GameField.Square ? "平方领域" : "根号领域";
            Debug.Log($"当前领域: {fieldEffect}");
        }

        // 先更新UI，让玩家看到分数变化
        UpdateUI();
        
        // 延迟检查胜利条件
        StartCoroutine(DelayedVictoryCheckForOpponent(turnData));
    }
    
    private IEnumerator DelayedVictoryCheckForOpponent(NetworkMessage turnData)
    {
        // 延迟0.1秒让玩家看到分数变化
        yield return new WaitForSeconds(0.1f);
        
        // 检查胜利条件
        if (turnData.result >= targetNumber)
        {
            Debug.Log($"玩家{turnData.playerIndex + 1}获胜！得分：{turnData.result}");
            
            // 只有当前玩家获胜时才调用EndGame()
            if (turnData.playerIndex == playerIndex)
            {
                EndGame();
            }
            // 如果是对手获胜，等待GameOver消息处理
            else
            {
                // 显示临时提示
                ShowResult($"玩家{turnData.playerIndex + 1}获胜！达到目标数{targetNumber}");
                gameEnded = true;
                
                // 禁用游戏按钮
                DisableGameButtons();
            }
            yield break;
        }
        
        isProcessingTurn = false;
    }

    // 强制设置当前回合（仅用于调试/修复冻结问题）
    public void ForceSetCurrentTurn(int forcePlayerIndex)
    {
        gameState.SetCurrentTurn(forcePlayerIndex);
        isFrozen = false; // 确保没有冻结状态
        isProcessingTurn = false; // 确保不在处理回合中
        
        UpdateUI();
        UpdatePlayCardButtonState();
        
        Debug.Log($"已强制设置当前回合为玩家{forcePlayerIndex + 1}");
    }
    
    // 在控制台中调用此方法以修复冻结问题：TurnManager.Instance.ForceFixFreezeIssue();
    public void ForceFixFreezeIssue()
    {
        ForceSetCurrentTurn(playerIndex);
    }
    
    // 重置游戏按钮点击事件
    public void OnRestartButtonClicked()
    {
        // 隐藏结果面板
        if (gameResultPanel != null)
        {
            gameResultPanel.SetActive(false);
        }
        
        // 重置游戏状态
        gameEnded = false;
        
        // 重新初始化游戏
        Initialize(playerIndex, gameState.PlayerCount);
        
        // 生成新的初始手牌
        List<Card> initialHand = CardDeckManager.Instance.GenerateInitialHand();
        StartGame(initialHand);
    }

    // 直接更新卡牌数量显示，不重新计算
    public void UpdateCardCountDisplay(int numberCount, int operatorCount, int skillCount)
    {
        Debug.Log($"[玩家{playerIndex+1}] 直接更新卡牌数量显示 - 数字:{numberCount} 运算符:{operatorCount} 技能:{skillCount}");
        
        if (numberCardCountText != null)
        {
            numberCardCountText.text = $"数字牌: {numberCount}";
        }
        
        if (operatorCardCountText != null)
        {
            operatorCardCountText.text = $"运算符: {operatorCount}";
        }
        
        if (skillCardCountText != null)
        {
            skillCardCountText.text = $"技能牌: {skillCount}";
        }
    }

    // 添加卡牌到手牌（供客户端使用）
    public void AddCardToHand(Card card)
    {
        if (card != null)
        {
            handManager.AddCard(card);
            
            // 检查手牌上限
            CheckHandLimit();
        }
    }

    private int CalculateResult(List<Card> playedCards)
    {
        int result = gameState.GetScore(playerIndex); // 从当前玩家的分数开始计算
        
        foreach (var card in playedCards)
        {
            if (card.type == CardType.Number || card.type == CardType.Operator)
            {
                if (card.type == CardType.Operator)
                {
                    // 找到对应的数字牌
                    Card numberCard = playedCards.Find(c => c.type == CardType.Number);
                    if (numberCard != null)
                    {
                        result = card.Calculate(result, numberCard.numberValue);
                    }
                }
            }
        }
        
        return result;
    }
}