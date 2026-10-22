using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TNDA.Prototype
{
    // Every piece of text GAME3 shows on screen, defined once. The editor scene patch and the runtime HUD
    // both read it, and the pixel font pre-rasterises the characters found here.
    // Entries with {0}/{1}/{2} are string.Format templates filled in at runtime.
    public static class CabinetText
    {
        public const string Title = "廢車場大亂鬥";
        // Simplified rules (user request): only the two players' controls.
        public const string P1Controls = "P1　↑油門　←→轉向　左Ctrl道具";
        public const string P2Controls = "P2　W油門　AD轉向　數字鍵Enter道具";
        public const string Instructions = P1Controls + "\n" + P2Controls;

        // Attract mode, like an arcade cabinet waiting for coins.
        public const string CoinRule = "單人 1 枚　雙人 2 枚";
        // The attract prompt follows the coins inserted.
        public const string CoinPrompt = "按 5 投幣";
        public const string CoinPromptOne = "已投 {0} 枚　按 1 單人";
        public const string CoinPromptTwo = "已投 {0} 枚　按 1 單人　按 2 雙人";
        public const string CoinInserted = "已投 {0} 枚";
        public const string InsertCoinFirst = "請先按 5 投幣";
        public const string OnePlayerButton = "1P／3 台電腦";
        public const string TwoPlayerButton = "2P／2 台電腦";
        public const string AttractHint = "按 5 投幣　按 1／2 開始";
        public const string ResultsHint = "按 5 投幣　按 1／2 再玩";
        public const string ReturnToTitle = "{0} 秒後回標題　按 5 投幣再玩";

        public const string Go = "出發！";

        // Continue after a timed elimination (4th or 3rd place): 10-second countdown, one coin.
        public const string ContinueTitle = "{0} 投幣繼續？";
        public const string ContinueNeedCoin = "按 5 投幣，再按 {0}";
        public const string ContinuePress = "按 {0} 繼續（已投 {1} 枚）";
        public const string P1ContinueKey = "左Ctrl";
        public const string P2ContinueKey = "數字鍵Enter";
        public const string Revived = "{0} 投幣復活！";
        public const string GameOver = "GAME OVER";
        // Victory picture (2026-10-19), shown when a player wins, before the results.
        public const string VictorySurvived = "你活下來了";
        public const string VictoryScore = "最終得分 {0}";
        public static readonly string[] StartMessages = { "活下來！", "盡量取得最高分！" };
        // Driver names by seat (2026-10-21, replacing Player1/Player2/Panda/SAN/Zhima): P1 to P4.
        public const string DriverName = "P{0}";

        public const string NoItem = "無道具";
        public const string Shield = "防護罩";
        public const string Rocket = "火箭";
        public const string Cherry = "櫻桃";
        public const string Oil = "油";

        // Score pop-ups over the car; the feedback line only carries rule reminders.
        public const string ScoreGain = "+{0}";
        public const string ScoreLoss = "-{0}";

        // Feedback line announcements.
        public const string GotItem = "{0} 拿到{1}，按道具鍵使用";
        public const string ShieldUp = "{0} 防護罩 {1} 秒";
        public const string RocketBusy = "火箭還在飛，道具先保留";
        public const string RocketFired = "{0} 發射火箭！";
        public const string OilDropped = "{0} 丟下油！";
        public const string CherryEaten = "{0} 吃下櫻桃！{1} 秒內碰到就撞毀";
        public const string ShieldBlocked = "{0} 防護罩擋下！";
        public const string Eliminated = "最後一名 {0} 淘汰！";

        // Results panel.
        public const string Winner = "{0} 獲勝！";
        public const string Standing = "{0}. {1}  {2}";
        public const string ResultsSummary = "最高分 {0}　撞毀 {1} 次";
        public const string EliminationRule = "45／80／120 秒各淘汰最後一名";

        // Leaderboard, player status and timer.
        public const string StatusEliminated = "淘汰 第{0}名";
        public const string StatusRespawn = "{0}秒後重生";
        public const string StatusCherry = "櫻桃 {0}";
        public const string StatusShield = "防護罩 {0}";
        public const string StatusHealth = "耐久 {0}";
        public const string ActionEliminated = "已淘汰";
        public const string ActionRespawning = "重生中";
        public const string ActionCherry = "櫻桃 {0} 秒";
        public const string PlayerStatus = "{0} {1}";
        public const string TimeAndWrecks = "時間 {0}　撞毀 {1}";
        public const string Spectating = "觀戰中";
        public const string Champion = "冠軍 {0}";
        public const string NextElimination = "{0:00}秒後淘汰／剩{1}車";

        // Every string above (templates included), for building the pixel font's character set.
        public static IEnumerable<string> All => typeof(CabinetText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(f => f.GetValue(null) is string[] list ? list : new[] { f.GetValue(null) as string })
            .Where(s => s != null);
    }
}
