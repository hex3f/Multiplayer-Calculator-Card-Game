using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // 初始化游戏
        TurnManager.Instance.Initialize(0, 2); // 假设是2人游戏，当前玩家是0号

        // 生成初始手牌
        var initialHand = CardDeckManager.Instance.GenerateInitialHand();
        
        // 开始游戏
        TurnManager.Instance.StartGame(initialHand);
    }
} 