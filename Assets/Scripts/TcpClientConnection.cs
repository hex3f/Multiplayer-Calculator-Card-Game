using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using static UnityEngine.PlayerLoop.EarlyUpdate;

public class TcpClientConnection : MonoBehaviour
{
    public static TcpClientConnection Instance;
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected;
    private Thread receiveThread;
    private ConnectUI connectUI;
    private StringBuilder messageBuilder = new StringBuilder();

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

    private void Start()
    {
        connectUI = FindObjectOfType<ConnectUI>();
    }

    public void ConnectToServer(string ip, int port)
    {
        try
        {
            client = new TcpClient();
            client.Connect(ip, port);
            stream = client.GetStream();
            isConnected = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            Debug.Log($"已连接到服务器 {ip}:{port}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"连接服务器失败: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        byte[] lengthBuffer = new byte[4]; // 用于读取消息长度
        byte[] messageBuffer = new byte[4096]; // 用于读取消息内容
        
        while (isConnected)
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
                Debug.Log($"收到服务器消息: {json}");

                // 在主线程中处理消息
                UnityMainThread.Execute(() => HandleMessage(json));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"接收消息失败: {e.Message}");
                break;
            }
        }

        isConnected = false;
        client.Close();
    }

    private void HandleMessage(string json)
    {
        try
        {
            // 验证JSON格式
            if (string.IsNullOrEmpty(json) || !json.StartsWith("{") || !json.EndsWith("}"))
            {
                Debug.LogError($"无效的JSON格式: {json}");
                return;
            }

            var message = JsonUtility.FromJson<NetworkMessage>(json);
            if (message == null)
            {
                Debug.LogError("消息解析失败");
                return;
            }

            Debug.Log($"处理消息: {message.type}, 玩家: {message.playerIndex}");
            
            switch (message.type)
            {
                case "GameStart":
                    // 收到游戏开始信号和初始手牌
                    if (message.initialHand != null)
                    {
                        // 同步牌堆数量
                        CardDeckManager.Instance.SyncCardCounts(
                            message.numberCardCount,
                            message.operatorCardCount,
                            message.extraOperatorCardCount,
                            message.skillCardCount
                        );
                        
                        // 使用服务器发送的初始手牌
                        ConnectUI.Instance.OnGameStart(message.initialHand);
                    }
                    else
                    {
                        Debug.LogError("未收到初始手牌数据");
                    }
                    break;
                case "DeckUpdate":
                    // 处理牌库数量更新
                    Debug.Log($"[客户端] 收到牌库数量更新: 数字牌:{message.numberCardCount} 运算符:{message.operatorCardCount} 技能牌:{message.skillCardCount}");
                    CardDeckManager.Instance.SyncCardCounts(message.numberCardCount, message.operatorCardCount, message.extraOperatorCardCount, message.skillCardCount);
                    TurnManager.Instance.UpdateCardCountDisplay(message.numberCardCount, message.operatorCardCount, message.extraOperatorCardCount, message.skillCardCount);
                    break;
                case "TargetNumber":
                case "RequestTargetNumber":
                case "Turn":
                case "Skill":
                case "DrawCard":
                case "SkipTurn":
                case "FreezeStatus":
                case "GameOver":
                    TurnManager.Instance.OnOpponentTurn(message);
                    break;
                case "DrawCardResponse":
                    // 处理抽牌响应，添加服务器发送的卡牌到手牌
                    Debug.Log($"[客户端] 收到抽牌响应，抽到{message.cardsDrawn}张牌");
                    if (message.drawnCards != null && message.drawnCards.Count > 0)
                    {
                        foreach (var card in message.drawnCards)
                        {
                            // 将卡牌添加到客户端手牌中
                            TurnManager.Instance.AddCardToHand(card);
                            Debug.Log($"添加卡牌到手牌: {card.GetDisplayText()}");
                        }
                    }
                    // 然后处理常规操作
                    TurnManager.Instance.OnOpponentTurn(message);
                    break;
                case "ScoreSync":
                    if (message.playerScores != null && message.playerScores.Length == 2)
                    {
                        int myIndex = TurnManager.Instance.playerIndex; // ← 新增方法，获取本地玩家 index
                        int opponentIndex = (myIndex + 1) % 2;

                        GameState.Instance.AddScore(myIndex, message.playerScores[myIndex]);
                        GameState.Instance.AddScore(opponentIndex, message.playerScores[opponentIndex]);

                        Debug.Log($"[客户端] ScoreSync 同步：我({myIndex})={message.playerScores[myIndex]}, 对手({opponentIndex})={message.playerScores[opponentIndex]}");

                        TurnManager.Instance.UpdateUI();
                    }
                    break;
                default:
                    Debug.LogWarning($"未知消息类型: {message.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"处理消息失败: {e.Message}");
        }
    }

    public void SendTurnData(NetworkMessage message)
    {
        if (!isConnected) return;

        try
        {
            string json = JsonUtility.ToJson(message);
            byte[] jsonData = Encoding.UTF8.GetBytes(json);
            byte[] lengthData = BitConverter.GetBytes(jsonData.Length);
            
            // 先发送长度
            stream.Write(lengthData, 0, lengthData.Length);
            // 再发送数据
            stream.Write(jsonData, 0, jsonData.Length);
            
            Debug.Log($"发送消息: {message.type}, 玩家: {message.playerIndex}");
        }
        catch (Exception e)
        {
            Debug.LogError($"发送消息失败: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        isConnected = false;
        if (client != null)
        {
            client.Close();
        }
    }
}
