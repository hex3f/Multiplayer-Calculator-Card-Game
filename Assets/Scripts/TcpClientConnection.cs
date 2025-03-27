using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;

public class TcpClientConnection : MonoBehaviour
{
    public static TcpClientConnection Instance;
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected;
    private Thread receiveThread;
    private ConnectUI connectUI;

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
        byte[] buffer = new byte[1024];
        while (isConnected)
        {
            try
            {
                int len = stream.Read(buffer, 0, buffer.Length);
                if (len == 0) continue;

                string json = Encoding.UTF8.GetString(buffer, 0, len);
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
        var message = JsonUtility.FromJson<NetworkMessage>(json);
        Debug.Log($"处理消息: {message.type}, 玩家: {message.playerIndex}");
        
        switch (message.type)
        {
            case "GameStart":
                // 收到游戏开始信号，生成自己的初始手牌
                List<Card> initialHand = CardDeckManager.Instance.GenerateInitialHand();
                ConnectUI.Instance.OnGameStart(initialHand);
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
            default:
                Debug.LogWarning($"未知消息类型: {message.type}");
                break;
        }
    }

    public void SendTurnData(NetworkMessage message)
    {
        if (!isConnected) return;

        try
        {
            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
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
