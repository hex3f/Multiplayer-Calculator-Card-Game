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
        byte[] buffer = new byte[1024];
        NetworkStream stream = client.GetStream();

        while (isRunning && client.Connected)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(json);
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
                                
                                // 发送游戏开始信号给客户端
                                NetworkMessage startMsg = new NetworkMessage
                                {
                                    type = "GameStart",
                                    playerIndex = 0
                                };
                                SendTurnData(startMsg);
                                
                                // 生成初始手牌并发送给主机
                                List<Card> initialHand = CardDeckManager.Instance.GenerateInitialHand();
                                ConnectUI.Instance.OnGameStart(initialHand);
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
                            // 处理抽牌消息
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                        else if (message.type == "SkipTurn")
                        {
                            // 处理跳过回合消息
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                        else
                        {
                            TurnManager.Instance.OnOpponentTurn(message);
                        }
                    });
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
            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);
            
            foreach (var client in clients)
            {
                if (client.Connected)
                {
                    NetworkStream stream = client.GetStream();
                    stream.Write(data, 0, data.Length);
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
