using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;

public class TcpHost : MonoBehaviour
{
    public static TcpHost Instance;
    private TcpListener listener;
    private List<TcpClient> clients = new List<TcpClient>();
    private bool isRunning;
    private Thread listenerThread;
    private Thread receiveThread;
    private const int maxClients = 1; // 只允许一个客户端连接
    private bool[] playerReady = new bool[2]; // 记录玩家准备状态

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartServer(string ip, int port)
    {
        try
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            listener.Start();
            isRunning = true;

            listenerThread = new Thread(ListenForClients);
            listenerThread.Start();

            // 主机（玩家0）已准备
            playerReady[0] = true;

            Debug.Log($"服务器启动在 {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"服务器启动失败: {e.Message}");
        }
    }

    private void ListenForClients()
    {
        while (isRunning)
        {
            try
            {
                if (clients.Count < maxClients)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    clients.Add(client);
                    Debug.Log("客户端已连接");

                    // 启动接收消息的线程
                    receiveThread = new Thread(() => ReceiveMessages(client));
                    receiveThread.Start();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"接受客户端连接失败: {e.Message}");
            }
        }
    }

    private void ReceiveMessages(TcpClient client)
    {
        byte[] lengthBuffer = new byte[4]; // 用于读取消息长度
        byte[] messageBuffer = new byte[4096]; // 用于读取消息内容
        NetworkStream stream = client.GetStream();

        while (isRunning && client.Connected)
        {
            try
            {
                // 读取消息长度
                int bytesRead = stream.Read(lengthBuffer, 0, 4);
                if (bytesRead == 0) continue;

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength > 4096)
                {
                    Debug.LogError($"无效的消息长度: {messageLength}");
                    continue;
                }

                // 读取消息内容
                bytesRead = stream.Read(messageBuffer, 0, messageLength);
                if (bytesRead == 0) continue;

                string json = Encoding.UTF8.GetString(messageBuffer, 0, bytesRead);
                
                try
                {
                    // 验证JSON格式
                    if (string.IsNullOrEmpty(json) || !json.StartsWith("{") || !json.EndsWith("}"))
                    {
                        Debug.LogError($"无效的JSON格式: {json}");
                        continue;
                    }

                    NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(json);
                    if (message == null)
                    {
                        Debug.LogError("消息解析失败");
                        continue;
                    }

                    Debug.Log($"收到消息: {message.type}, 玩家: {message.playerIndex}");

                    // 在主线程中处理消息
                    UnityMainThread.Execute(() =>
                    {
                        if (message.type == "PlayerReady")
                        {
                            playerReady[message.playerIndex] = true;
                            Debug.Log($"玩家 {message.playerIndex} 已准备");

                            // 检查是否所有玩家都准备好了
                            if (playerReady[0] && playerReady[1])
                            {
                                Debug.Log("所有玩家已准备，开始游戏");
                                
                                // 初始化牌堆（确保只初始化一次）
                                CardDeckManager.Instance.InitializeDeck();
                                
                                // 生成主机和客户端的初始手牌
                                List<Card> hostInitialHand = CardDeckManager.Instance.GenerateInitialHand();
                                List<Card> clientInitialHand = CardDeckManager.Instance.GenerateInitialHand();
                                
                                // 发送游戏开始信号和客户端初始手牌给客户端
                                NetworkMessage startMsg = new NetworkMessage
                                {
                                    type = "GameStart",
                                    playerIndex = 0,
                                    initialHand = clientInitialHand,
                                    numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                                    operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                                    extraOperatorCardCount = CardDeckManager.Instance.GetExtraOperatorCardCount(),
                                    skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
                                };
                                SendTurnData(startMsg);
                                
                                // 通知UI系统开始游戏
                                ConnectUI.Instance.OnGameStart(hostInitialHand);
                            }
                        }
                        else if (message.type == "RequestTargetNumber")
                        {
                            // 重新发送目标数
                            NetworkMessage targetMsg = new NetworkMessage
                            {
                                type = "TargetNumber",
                                targetNumber = TurnManager.Instance.GetTargetNumber(),
                                playerIndex = 0
                            };
                            SendTurnData(targetMsg);
                        }
                        else if (message.type == "DrawCard")
                        {
                            // 主机作为权威来源，处理客户端抽牌请求
                            Debug.Log($"[主机] 处理玩家{message.playerIndex+1}抽牌请求，数量:{message.cardsDrawn}");
                            
                            // 创建临时列表记录抽到的牌
                            List<Card> drawnCards = new List<Card>();
                            
                            // 从牌库抽取请求的牌数
                            for (int i = 0; i < message.cardsDrawn; i++)
                            {
                                Card card = CardDeckManager.Instance.DrawCard();
                                if (card != null)
                                {
                                    drawnCards.Add(card);
                                    Debug.Log($"为玩家{message.playerIndex+1}抽出卡牌: {card.GetDisplayText()}");
                                }
                            }
                            
                            // 构建包含抽出卡牌的响应消息
                            NetworkMessage drawResponse = new NetworkMessage
                            {
                                type = "DrawCardResponse",
                                playerIndex = message.playerIndex,
                                cardsDrawn = drawnCards.Count,
                                drawnCards = drawnCards,
                                numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                                operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                                skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
                            };
                            
                            // 发送抽牌结果给客户端
                            SendTurnData(drawResponse);
                            
                            // 同步一次牌库数量给所有玩家
                            NetworkMessage deckUpdateMsg = new NetworkMessage
                            {
                                type = "DeckUpdate",
                                numberCardCount = CardDeckManager.Instance.GetNumberCardCount(),
                                operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount(),
                                skillCardCount = CardDeckManager.Instance.GetSkillCardCount()
                            };
                            SendTurnData(deckUpdateMsg);
                            
                            // 处理本地的消息显示
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                        else if (message.type == "SkipTurn")
                        {
                            // 处理跳过回合消息
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                        else if (message.type == "FreezeStatus")
                        {
                            // 处理冻结状态同步
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                        else if (message.type == "ScoreSync")
                        {
                            // 处理分数同步消息
                            if (message.playerScores != null && message.playerScores.Length == 2)
                            {
                                GameState.Instance.AddScore(0, message.playerScores[0]);
                                GameState.Instance.AddScore(1, message.playerScores[1]);
                                Debug.Log($"[服务器] 收到分数同步：玩家1 {message.playerScores[0]}, 玩家2 {message.playerScores[1]}");
                                TurnManager.Instance.UpdateUI();
                            }
                        }
                        else
                        {
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"处理消息失败: {e.Message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"接收消息失败: {e.Message}");
                break;
            }
        }

        clients.Remove(client);
        client.Close();
    }

    public void SendTurnData(NetworkMessage message)
    {
        if (!isRunning || clients.Count == 0) return;

        try
        {
            // 主机作为权威来源，更新卡牌数量信息
            message.numberCardCount = CardDeckManager.Instance.GetNumberCardCount();
            message.operatorCardCount = CardDeckManager.Instance.GetOperatorCardCount();
            message.extraOperatorCardCount = CardDeckManager.Instance.GetExtraOperatorCardCount();
            message.skillCardCount = CardDeckManager.Instance.GetSkillCardCount();
            
            // 记录日志以便调试
            Debug.Log($"[主机] 发送卡牌数量 - 数字:{message.numberCardCount} 运算符:{message.operatorCardCount} 特殊运算符:{message.extraOperatorCardCount} 技能:{message.skillCardCount}");
            
            string json = JsonUtility.ToJson(message);
            byte[] jsonData = Encoding.UTF8.GetBytes(json);
            byte[] lengthData = BitConverter.GetBytes(jsonData.Length);
            
            foreach (var client in clients)
            {
                if (client.Connected)
                {
                    NetworkStream stream = client.GetStream();
                    // 先发送长度
                    stream.Write(lengthData, 0, lengthData.Length);
                    // 再发送数据
                    stream.Write(jsonData, 0, jsonData.Length);
                    Debug.Log($"发送消息: {message.type}, 目标数: {message.targetNumber}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"发送消息失败: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        isRunning = false;
        if (listener != null)
        {
            listener.Stop();
        }
        foreach (var client in clients)
        {
            client.Close();
        }
    }
}
