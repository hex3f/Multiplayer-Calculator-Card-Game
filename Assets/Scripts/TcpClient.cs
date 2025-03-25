using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;

public class GameClient
{
    public int playerIndex { get; set; }
    private NetworkStream stream;
    public System.Net.Sockets.TcpClient client { get; private set; }

    public GameClient(NetworkStream stream, System.Net.Sockets.TcpClient client)
    {
        this.stream = stream;
        this.client = client;
    }

    public void SendMessage(NetworkMessage message)
    {
        try
        {
            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"发送消息失败: {e.Message}");
        }
    }

    public void Close()
    {
        stream?.Close();
        client?.Close();
    }
} 