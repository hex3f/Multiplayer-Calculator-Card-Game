using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ConnectUI : MonoBehaviour
{
    public static ConnectUI Instance;
    public InputField ipInput;
    public InputField portInput;
    public Button hostButton;
    public Button clientButton;
    public GameObject connectPanel;
    public GameObject gamePanel;
    public Text waitingText;
    public Text statusText;
    public Text turnText;
    public Text player1ScoreText;
    public Text player2ScoreText;
    public Text targetNumberText;
    public GameObject resultTextObject;
    public Text resultText;
    public Button drawCardButton;
    public HandManager handManager;
    public GameObject cardPrefab;
    public Transform handContainer;

    private void Awake()
    {
        Instance = this;
        connectPanel.SetActive(true);
        gamePanel.SetActive(false);
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        waitingText.gameObject.SetActive(false);
    }

    private void Start()
    {
        // 初始化UI组件
        InitializeUI();

        // 初始化网络连接
        InitializeNetwork();
    }

    private void InitializeUI()
    {
        // 初始化手牌管理器
        if (handManager != null)
        {
            // 确保有卡牌预制体
            if (cardPrefab == null)
            {
                Debug.LogError("卡牌预制体未设置！");
                return;
            }

            // 确保有手牌容器
            if (handContainer == null)
            {
                Debug.LogError("手牌容器未设置！");
                return;
            }

            handManager.cardPrefab = cardPrefab;
            handManager.handContainer = handContainer;
        }

        // 初始化回合管理器
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.InitializeUI(
                turnText,
                targetNumberText,
                resultText,
                drawCardButton,
                player1ScoreText,
                player2ScoreText
            );
        }
    }

    private void InitializeNetwork()
    {
        // 设置默认IP和端口
        ipInput.text = "127.0.0.1";
        portInput.text = "8888";
    }

    private void OnHostButtonClicked()
    {
        string ip = ipInput.text;
        int port = int.Parse(portInput.text);
        
        // 启动服务器
        TcpHost.Instance.StartServer(ip, port);
        
        // 初始化游戏（主机是玩家0）
        TurnManager.Instance.Initialize(0, 2);
        
        // 显示等待文本
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        waitingText.text = "等待对手连接...";
        waitingText.gameObject.SetActive(true);
    }

    private void OnClientButtonClicked()
    {
        string ip = ipInput.text;
        int port = int.Parse(portInput.text);
        
        // 连接到服务器
        TcpClientConnection.Instance.ConnectToServer(ip, port);
        
        // 初始化游戏（客户端是玩家1）
        TurnManager.Instance.Initialize(1, 2);
        
        // 显示等待文本
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        waitingText.text = "正在连接到主机...";
        waitingText.gameObject.SetActive(true);

        // 发送准备消息给主机
        NetworkMessage readyMsg = new NetworkMessage
        {
            type = "PlayerReady",
            playerIndex = 1
        };
        TcpClientConnection.Instance.SendTurnData(readyMsg);
    }

    public void OnGameStart(List<Card> initialHand)
    {
        // 初始化牌堆
        CardDeckManager.Instance.InitializeDeck();
        
        // 开始游戏
        TurnManager.Instance.StartGame(initialHand);
        
        // 初始化UI
        TurnManager.Instance.InitializeUI(
            turnText: turnText,
            player1ScoreText: player1ScoreText,
            player2ScoreText: player2ScoreText,
            targetNumberText: targetNumberText,
            resultText: resultText,
            drawCardButton: drawCardButton
        );
        
        // 隐藏连接面板，显示游戏面板
        connectPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
}
