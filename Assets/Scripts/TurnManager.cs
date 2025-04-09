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
    public Text extraOperatorCardCountText; // 显示特殊运算符牌剩余数量
    public Text skillCardCountText;    // 显示技能牌剩余数量

    private Card selectedNumberCard;
    private Card selectedOperatorCard;
    private Card selectedExtraOperatorCard;
    private Card selectedSkillCard;
    private GameState gameState;
    private int playerIndex;
    private bool isProcessingTurn;
    private bool isPlayCard;
    private NetworkMessage lastTurnData;
    private int targetNumber;
    private int currentNumber; // 当前累计数值
    private bool gameEnded = false;
    public bool isFrozen = false; // 添加冻结状态变量

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
        selectedExtraOperatorCard = null;
        selectedSkillCard = null;
        playedCardsManager.ClearCards(true);
        playedCardsManager.ClearCards(false);
        
        // 更新牌堆数量显示
        if (playerIndex == 0) // 只有主机需要更新牌堆数量
        {
            int numberCount = CardDeckManager.Instance.GetNumberCardCount();
            int operatorCount = CardDeckManager.Instance.GetOperatorCardCount();
            int extraOperatorCount = CardDeckManager.Instance.GetExtraOperatorCardCount();
            int skillCount = CardDeckManager.Instance.GetSkillCardCount();
            
            Debug.Log($"[主机] 初始手牌后更新牌堆数量 - 数字:{numberCount} 运算符:{operatorCount} 特殊运算符:{extraOperatorCount} 技能:{skillCount}");
            
            // 发送牌堆数量更新消息
            NetworkMessage deckUpdateMsg = new NetworkMessage
            {
                type = "DeckUpdate",
                numberCardCount = numberCount,
                operatorCardCount = operatorCount,
                extraOperatorCardCount = extraOperatorCount,
                skillCardCount = skillCount
            };
            
            TcpHost.Instance.SendTurnData(deckUpdateMsg);
        }
        
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
        if (isProcessingTurn) return;

        Debug.Log($"点击卡牌: {card.GetDisplayText()}");

        // 如果卡牌已经被选中，则取消选择
        if ((card == selectedNumberCard) || (card == selectedOperatorCard) || (card == selectedExtraOperatorCard) || (card == selectedSkillCard))
        {
            Debug.Log($"取消选择卡牌: {card.GetDisplayText()}");
            switch (card.type)
            {
                case CardType.Number:
                    selectedNumberCard = null;
                    break;
                case CardType.Operator:
                    selectedOperatorCard = null;
                    break;
                case CardType.ExtraOperator:
                    selectedExtraOperatorCard = null;
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
                Debug.Log($"选择数字牌: {card.GetDisplayText()}");
                break;

            case CardType.Operator:
                // 如果已经选择了运算符牌，先取消选择
                if (selectedOperatorCard != null)
                {
                    handManager.DeselectCard(selectedOperatorCard);
                }
                selectedOperatorCard = card;
                handManager.SelectCard(card);
                Debug.Log($"选择运算符牌: {card.GetDisplayText()}");
                break;

            case CardType.ExtraOperator:
                // 如果已经选择了特殊运算符牌，先取消选择
                if (selectedExtraOperatorCard != null)
                {
                    handManager.DeselectCard(selectedExtraOperatorCard);
                }
                selectedExtraOperatorCard = card;
                handManager.SelectCard(card);
                Debug.Log($"选择特殊运算符牌: {card.GetDisplayText()}");
                break;

            case CardType.Skill:
                // 如果已经选择了技能牌，先取消选择
                if (selectedSkillCard != null)
                {
                    handManager.DeselectCard(selectedSkillCard);
                }
                selectedSkillCard = card;
                handManager.SelectCard(card);
                Debug.Log($"选择技能牌: {card.GetDisplayText()}");
                break;
        }

        // 更新出牌按钮状态
        UpdatePlayCardButtonState();
    }

    // 添加新方法：处理冻结状态下的回合切换
    private void HandleFrozenTurnSwitch()
    {
        if (isFrozen && gameState.IsPlayerTurn(playerIndex))
        {
            Debug.Log($"玩家{playerIndex + 1}被冻结，强制切换到对手回合");
            
            // 发送跳过回合消息
            NetworkMessage skipMsg = new NetworkMessage
            {
                type = "SkipTurn",
                playerIndex = playerIndex
            };

            if (playerIndex == 0)
            {
                TcpHost.Instance.SendTurnData(skipMsg);
                forzenCount++;
                if (forzenCount == 2)
                {
                    forzenCount = 0;
                    isFrozen = false;
                }
            }
            else
            {
                TcpClientConnection.Instance.SendTurnData(skipMsg);
                isFrozen = false;
            }

            // 切换到对手回合
            int nextPlayerIndex = (playerIndex + 1) % 2;
            gameState.SetCurrentTurn(nextPlayerIndex);
            isPlayCard = false;
            // 更新UI
            UpdateUI();
        }
    }

    private void UpdatePlayCardButtonState()
    {
        if (playCardButton != null)
        {
            // 检查是否被冻结，如果是则切换回合
            HandleFrozenTurnSwitch();
            
            // 只要是我的回合且没有被冻结，就可以出牌（包括不出牌）
            bool canPlay = gameState.IsPlayerTurn(playerIndex) && !isPlayCard && !isFrozen;
            
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
                               (isProcessingTurn ? "正在处理回合" : 
                               (isFrozen ? "被冻结" : "未知原因"));
                Debug.Log($"出牌按钮已禁用，原因: {reason}");
            }
        }
    }

    public void OnPlayCardButtonClicked()
    {
        if (!gameState.IsPlayerTurn(playerIndex) || isProcessingTurn)
        {
            return;
        }

        isProcessingTurn = true;
        isPlayCard = true;

        Debug.Log("点击出牌按钮");
        Debug.Log($"当前选中状态 - 数字牌: {(selectedNumberCard != null ? selectedNumberCard.GetDisplayText() : "null")}, " +
                 $"运算符牌: {(selectedOperatorCard != null ? selectedOperatorCard.GetDisplayText() : "null")}, " +
                 $"特殊运算符牌: {(selectedExtraOperatorCard != null ? selectedExtraOperatorCard.GetDisplayText() : "null")}, " +
                 $"技能牌: {(selectedSkillCard != null ? selectedSkillCard.GetDisplayText() : "null")}");

        // 如果没有选择任何牌，直接返回
        if (selectedNumberCard == null && selectedOperatorCard == null && 
            selectedExtraOperatorCard == null && selectedSkillCard == null)
        {
            Debug.Log("没有选择任何卡牌");
            isProcessingTurn = false;
            return;
        }

        // 检查出牌规则
        bool isValidPlay = false;
        
        // 规则1：技能牌
        if (selectedSkillCard != null && selectedNumberCard == null && 
            selectedOperatorCard == null && selectedExtraOperatorCard == null)
        {
            Debug.Log("规则1：单独使用技能牌");
            isValidPlay = true;
        }
        // 规则2：数字+运算
        else if (selectedNumberCard != null && selectedOperatorCard != null && 
                 selectedExtraOperatorCard == null && selectedSkillCard == null)
        {
            Debug.Log("规则2：数字+运算");
            isValidPlay = true;
        }
        // 规则3：数字+特殊运算
        else if (selectedNumberCard != null && selectedOperatorCard == null && 
                 selectedExtraOperatorCard != null && selectedSkillCard == null)
        {
            Debug.Log("规则3：数字+特殊运算");
            isValidPlay = true;
        }
        // 规则4：数字+特殊运算+运算（顺序先计算特殊运算再计算运算）
        else if (selectedNumberCard != null && selectedOperatorCard != null && 
                 selectedExtraOperatorCard != null && selectedSkillCard == null)
        {
            Debug.Log("规则4：数字+特殊运算+运算");
            isValidPlay = true;
        }
        // 规则5：数字+运算+技能
        else if (selectedNumberCard != null && selectedOperatorCard != null && 
                 selectedExtraOperatorCard == null && selectedSkillCard != null)
        {
            Debug.Log("规则5：数字+运算+技能");
            isValidPlay = true;
        }
        // 规则6：数字+特殊运算+技能
        else if (selectedNumberCard != null && selectedOperatorCard == null && 
                 selectedExtraOperatorCard != null && selectedSkillCard != null)
        {
            Debug.Log("规则6：数字+特殊运算+技能");
            isValidPlay = true;
        }

        if (!isValidPlay)
        {
            Debug.Log("出牌组合不符合规则");
            // 取消所有选择
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
            if (selectedExtraOperatorCard != null)
            {
                handManager.DeselectCard(selectedExtraOperatorCard);
                selectedExtraOperatorCard = null;
            }
            if (selectedSkillCard != null)
            {
                handManager.DeselectCard(selectedSkillCard);
                selectedSkillCard = null;
            }
            isProcessingTurn = false;
            return;
        }

        // 如果选择了技能牌，先处理技能
        if (selectedNumberCard == null && selectedSkillCard != null)
        {
            Debug.Log("开始处理技能牌");
            ProcessSkill();
            return;
        }
        
        // 否则处理数字和运算符
        Debug.Log("开始处理数字和运算符");
        ProcessTurn();
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
        // 检查是否被冻结，如果是则切换回合
        HandleFrozenTurnSwitch();
        
        // 1. 判断是否轮到自己回合 + 是否允许抽牌 + 是否被冻结
        if (!gameState.IsPlayerTurn(playerIndex) || !isPlayCard || isFrozen)
        {
            string reason = "";
            if (!gameState.IsPlayerTurn(playerIndex)) reason += "不是你的回合；";
            if (!isPlayCard) reason += "当前不能抽牌；";
            if (isFrozen) reason += "你被冻结了；";
            Debug.Log($"无法抽牌 - 原因: {reason}");
            return;
        }

        // 2. 检查手牌上限
        int currentHandCount = handManager.GetHand().Count;
        if (currentHandCount >= 6)
        {
            Debug.Log("手牌已达上限（6张），无法抽牌");
            return;
        }

        // 3. 初始化状态
        isProcessingTurn = true;
        selectedNumberCard = null;
        selectedOperatorCard = null;
        selectedExtraOperatorCard = null;
        selectedSkillCard = null;
        handManager.DeselectCard();

        // 4. 计算能抽的牌数（最多2张，且不超过上限）
        int cardsToDraw = Mathf.Min(2, 6 - currentHandCount);

        // 5. 主机逻辑：抽牌并广播
        if (playerIndex == 0)
        {
            List<Card> drawnCards = new List<Card>();

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

            // 同步抽牌信息
            TcpHost.Instance.SendTurnData(new NetworkMessage
            {
                type = "DrawCard",
                playerIndex = playerIndex,
                cardsDrawn = drawnCards.Count,
                drawnCards = drawnCards
            });

            // 同步牌堆数量
            TcpHost.Instance.SendTurnData(new NetworkMessage
            {
                type = "DeckUpdate",
                numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
            });

            // 切换回合
            int nextPlayerIndex = (playerIndex + 1) % gameState.PlayerCount;
            gameState.SetCurrentTurn(nextPlayerIndex);
            Debug.Log($"摸牌完成，切换到玩家{nextPlayerIndex + 1}的回合");
        }
        else // 6. 客户端：只发送抽牌请求
        {
            TcpClientConnection.Instance.SendTurnData(new NetworkMessage
            {
                type = "DrawCard",
                playerIndex = playerIndex,
                cardsDrawn = cardsToDraw
            });

            isPlayCard = true;
            ShowResult("正在抽牌...");
            // ⚠️ 注意：客户端应等主机回应后再将 isProcessingTurn = false
        }

        // 7. UI 更新（主机用；客户端后续主机回应时也要更新）
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
                    Debug.Log($"我被对手冻结了");
                    
                    // 如果当前是我的回合，立即跳过
                    if (gameState.IsPlayerTurn(playerIndex))
                    {
                        HandleFrozenTurnSwitch();
                    }
                }
                break;

            case SkillType.Mirror:
                // 交换双方分数
                int player0Score = gameState.GetScore(0);
                int player1Score = gameState.GetScore(1);
                gameState.AddScore(0, player1Score);
                gameState.AddScore(1, player0Score);
                
                // 发送分数同步消息
                NetworkMessage scoreSyncMsg = new NetworkMessage
                {
                    type = "ScoreSync",
                    playerIndex = playerIndex,
                    playerScores = new int[] { gameState.GetScore(0), gameState.GetScore(1) }
                };
                
                if (playerIndex == 0)
                {
                    TcpHost.Instance.SendTurnData(scoreSyncMsg);
                }
                else
                {
                    TcpClientConnection.Instance.SendTurnData(scoreSyncMsg);
                }
                
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
                // 不再调用ProcessTurn，而是直接处理数字和运算符
                List<Card> playedCards = new List<Card>();
                playedCards.Add(selectedNumberCard);
                playedCards.Add(selectedOperatorCard);
                
                int result = CalculateResult(playedCards);
                gameState.AddScore(playerIndex, result);
                
                // 创建网络消息
                NetworkMessage message = new NetworkMessage
                {
                    type = "Turn",
                    playerIndex = playerIndex,
                    playedCards = playedCards,
                    result = result,
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
                handManager.DeselectCard();
            }
            else
            {
                // 如果只使用了技能牌
                handManager.DeselectCard();
            }
            
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

        Debug.Log($"ProcessTurn开始 - selectedSkillCard: {(selectedSkillCard != null ? selectedSkillCard.GetDisplayText() : "null")}");

        // 按照特定顺序添加卡牌，确保显示顺序正确
        // 1. 先添加数字牌
        if (selectedNumberCard != null)
        {
            Debug.Log("添加数字牌");
            playedCards.Add(selectedNumberCard);
            handManager.RemoveCard(selectedNumberCard);
            playedCardsManager.AddCard(selectedNumberCard, true);
        }

        // 2. 再添加运算符牌
        if (selectedOperatorCard != null)
        {
            Debug.Log("添加运算符牌");
            playedCards.Add(selectedOperatorCard);
            handManager.RemoveCard(selectedOperatorCard);
            playedCardsManager.AddCard(selectedOperatorCard, true);
        }

        // 3. 再添加特殊运算符牌
        if (selectedExtraOperatorCard != null)
        {
            Debug.Log("添加特殊运算符牌");
            playedCards.Add(selectedExtraOperatorCard);
            handManager.RemoveCard(selectedExtraOperatorCard);
            playedCardsManager.AddCard(selectedExtraOperatorCard, true);
        }

        // 4. 最后添加技能牌
        //if (selectedSkillCard != null)
        //{
        //    Debug.Log($"添加技能牌: {selectedSkillCard.GetDisplayText()}");
        //    playedCards.Add(selectedSkillCard);
        //    handManager.RemoveCard(selectedSkillCard);
        //    playedCardsManager.AddCard(selectedSkillCard, true);
        //}
        //else
        //{
        //    Debug.Log("selectedSkillCard为null，无法添加技能牌");
        //}

        // 处理不同类型的运算符
        int result = CalculateResult(playedCards);
        
        Debug.Log($"出牌前分数 - 玩家1: {gameState.GetScore(0)}, 玩家2: {gameState.GetScore(1)}");
        
        // 先更新当前玩家的分数
        int oldScore = gameState.GetScore(playerIndex);

        // 如果是镜像技能，先不更新分数，而是直接交换
        //if (selectedSkillCard != null && selectedSkillCard.skillType == SkillType.Mirror)
        //{
        //    // 获取当前双方分数
        //    int myScore = gameState.GetScore(playerIndex);
        //    int opponentScore = gameState.GetScore((playerIndex + 1) % 2);

        //    Debug.Log($"镜像前 - 我方:{myScore}, 对方:{opponentScore}");

        //    // 先计算新的分数
        //    int myNewScore = result;

        //    // 交换分数
        //    gameState.AddScore(playerIndex, opponentScore);
        //    gameState.AddScore((playerIndex + 1) % 2, myNewScore);

        //    Debug.Log($"镜像后 - 玩家1: {gameState.GetScore(0)}, 玩家2: {gameState.GetScore(1)}");

        //    // 发送分数同步消息
        //    NetworkMessage scoreSyncMsg = new NetworkMessage
        //    {
        //        type = "ScoreSync",
        //        playerIndex = playerIndex,
        //        playerScores = new int[] {
        //            gameState.GetScore(0),
        //            gameState.GetScore(1)
        //        }
        //    };

        //    if (playerIndex == 0)
        //    {
        //        TcpHost.Instance.SendTurnData(scoreSyncMsg);
        //    }
        //    else
        //    {
        //        TcpClientConnection.Instance.SendTurnData(scoreSyncMsg);
        //    }

        //    Debug.Log($"分数互换完成：我方 {myScore} -> {gameState.GetScore(playerIndex)}, 对方 {opponentScore} -> {gameState.GetScore((playerIndex + 1) % 2)}");
        //}
        if (selectedSkillCard != null)
        {
            ProcessSkill();

            return;
        }
        else
        {
            // 如果不是镜像技能，正常更新分数
            gameState.AddScore(playerIndex, result);
            Debug.Log($"计算后分数 - 玩家{playerIndex + 1}: {result} (原分数:{oldScore})");
        }

        // 创建网络消息
        NetworkMessage message = new NetworkMessage
        {
            type = "Turn",
            playerIndex = playerIndex,
            playedCards = playedCards,
            result = result,
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
        selectedExtraOperatorCard = null;
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
        string turnString = (gameState.IsPlayerTurn(playerIndex) ? "你的回合" : "对手回合");
        
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
            // player1ScoreText 永远显示自己的分数
            player1ScoreText.text = $"{gameState.GetScore(playerIndex)}";
        }
        if (player2ScoreText != null)
        {
            // player2ScoreText 永远显示对手的分数
            player2ScoreText.text = $"{gameState.GetScore((playerIndex + 1) % 2)}";
        }

        // 更新按钮状态
        if (drawCardButton != null)
        {
            // 只要是我的回合且没有被冻结，就可以摸牌
            bool canDraw = gameState.IsPlayerTurn(playerIndex) && isPlayCard;
            drawCardButton.interactable = canDraw;

            if (canDraw)
            {
                Debug.Log("摸牌按钮已启用");
            }
            else
            {
                string reason = !gameState.IsPlayerTurn(playerIndex) ? "非当前回合" : 
                               (isProcessingTurn ? "正在处理回合" : "未知原因");
                Debug.Log($"摸牌按钮已禁用，原因: {reason}");
            }
        }

        // 更新出牌按钮状态
        if (playCardButton != null)
        {
            // 只要是我的回合且没有被冻结，就可以出牌
            bool canPlay = gameState.IsPlayerTurn(playerIndex) && !isProcessingTurn && !isPlayCard;
            playCardButton.interactable = canPlay;
            
            if (canPlay)
            {
                Debug.Log("出牌按钮已启用");
            }
            else
            {
                string reason = !gameState.IsPlayerTurn(playerIndex) ? "非当前回合" : 
                               (isProcessingTurn ? "正在处理回合" : "未知原因");
                Debug.Log($"出牌按钮已禁用，原因: {reason}");
            }
        }

        // 更新牌库数量显示
        UpdateCardCountTexts();
        
        Debug.Log($"界面已更新 - 当前回合:{gameState.CurrentPlayerTurn} 我的索引:{playerIndex} 是否我的回合:{gameState.IsPlayerTurn(playerIndex)}");
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

    int forzenCount = 0;

    public void OnOpponentTurn(NetworkMessage message)
    {
        if (message.type == "FreezeStatus")
        {
            if (message.playerIndex == playerIndex)
            {
                // 我被冻结了
                isFrozen = true;
                Debug.Log($"玩家{playerIndex + 1}被冻结");
                
                // 如果当前是我的回合，立即跳过
                if (gameState.IsPlayerTurn(playerIndex))
                {
                    HandleFrozenTurnSwitch();
                }
            }
            else
            {
                // 对手被冻结了
                Debug.Log($"对手(玩家{message.playerIndex + 1})被冻结");
            }
        }
        else if (message.type == "TargetNumber")
        {
            SetTargetNumber(message.targetNumber);
        }
        else if (message.type == "GameStart")
        {
            // 已在HandleMessage里处理
        }
        else if (message.type == "Turn")
        {
            ProcessTurnResult(message);
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
        else if (message.type == "ScoreSync")
        {
            // 更新双方分数
            if (message.playerScores != null && message.playerScores.Length == 2)
            {
                gameState.AddScore(0, message.playerScores[0]);
                gameState.AddScore(1, message.playerScores[1]);
                Debug.Log($"收到分数同步：玩家1 {message.playerScores[0]}, 玩家2 {message.playerScores[1]}");
                UpdateUI();
            }
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
            
            // 如果是客户端摸牌，切换到主机回合
            if (playerIndex == 1)
            {
                gameState.SetCurrentTurn(0); // 切换到主机回合
                isPlayCard = false;
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
        }
        else if (message.type == "DeckUpdate")
        {
            // 处理牌库数量更新消息
            Debug.Log($"[玩家{playerIndex+1}] 收到牌库数量更新消息: 数字:{message.numberCardCount} 运算符:{message.operatorCardCount} 特殊运算符:{message.extraOperatorCardCount} 技能:{message.skillCardCount}");
            UpdateCardCountDisplay(message.numberCardCount, message.operatorCardCount, message.extraOperatorCardCount, message.skillCardCount);
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
        
        Debug.Log($"处理完毕，当前回合玩家索引：{gameState.CurrentPlayerTurn}, 我的索引：{playerIndex}, 是否是我的回合：{gameState.IsPlayerTurn(playerIndex)}");
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
    public void UpdateCardCountDisplay(int numberCount, int operatorCount, int extraOperatorCount, int skillCount)
    {
        Debug.Log($"[玩家{playerIndex+1}] 直接更新卡牌数量显示 - 数字:{numberCount} 运算符:{operatorCount} 特殊运算符:{extraOperatorCount} 技能:{skillCount}");
        
        if (numberCardCountText != null)
        {
            numberCardCountText.text = $"数字牌: {numberCount}";
        }
        
        if (operatorCardCountText != null)
        {
            operatorCardCountText.text = $"运算符: {operatorCount}";
        }

        if (extraOperatorCardCountText != null)
        {
            extraOperatorCardCountText.text = $"特殊运算符: {extraOperatorCount}";
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
        int result = gameState.GetScore(playerIndex); // 当前分数

        Card numberCard = null;
        Card operatorCard = null;
        Card extraOperatorCard = null;

        // 分类卡牌
        foreach (var card in playedCards)
        {
            switch (card.type)
            {
                case CardType.Number:
                    numberCard = card;
                    break;
                case CardType.Operator:
                    operatorCard = card;
                    break;
                case CardType.ExtraOperator:
                    extraOperatorCard = card;
                    break;
            }
        }

        // 1. 先执行特殊运算符（如果有）
        if (extraOperatorCard != null)
        {
            result = extraOperatorCard.Calculate(result);
        }

        // 2. 再执行普通运算符（如果有）
        if (operatorCard != null && numberCard != null)
        {
            result = operatorCard.Calculate(result, numberCard.numberValue);
        }

        return result;
    }
}