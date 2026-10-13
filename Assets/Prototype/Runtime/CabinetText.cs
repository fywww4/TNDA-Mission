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
        // In-match footer: P1's controls plus the restart key.
        public const string P1Footer = P1Controls + "　R重玩";

        // Attract mode, like an arcade cabinet waiting for coins.
        public const string CoinRule = "每位玩家 1 枚硬幣";
        public const string CoinPrompt = "按 1 單人　按 2 雙人";
        public const string OnePlayerButton = "1P／3 台電腦";
        public const string TwoPlayerButton = "2P／2 台電腦";
        public const string AttractHint = "按 1／2 選人數　C／空白鍵 投幣";
        public const string ResultsHint = "R 重玩　C 投幣";
        public const string ReturnToTitle = "{0} 秒後回到標題　R 重玩";

        public const string Go = "出發！";
        public static readonly string[] StartMessages = { "活下來！", "盡量取得最高分！" };
        public const string ComputerName = "電腦{0}";

        public const string NoItem = "無道具";
        public const string Shield = "防護罩";
        public const string Rocket = "火箭";
        public const string Cherry = "櫻桃";
        public const string ShieldTag = " [盾]";
        public const string RocketTag = " [箭]";
        public const string CherryTag = " [櫻]";

        // Feedback line announcements.
        public const string Smashed = "{0} 撞碎 +{1}";
        public const string GotItem = "{0} 拿到{1}，按道具鍵使用";
        public const string ShieldUp = "{0} 防護罩 {1} 秒";
        public const string RocketBusy = "火箭還在飛，道具先保留";
        public const string RocketFired = "{0} 發射火箭！";
        public const string CherryEaten = "{0} 吃下櫻桃！{1} 秒內碰到就撞毀";
        public const string CherryWreck = "{0} 一碰撞毀 {1}！";
        public const string Rammed = "{0} 撞擊 x{1}";
        public const string ShieldBlocked = "{0} 防護罩擋下！";
        public const string Wrecked = "{0} 被撞毀 -{1}，{2} 秒後重生";
        public const string WreckBonus = "{0} 撞毀獎勵 +{1}";
        public const string Eliminated = "最後一名 {0} 淘汰！";

        // Results panel.
        public const string Winner = "{0} 獲勝！";
        public const string Standing = "{0}. {1}  {2}";
        public const string ResultsSummary = "最高分 {0}　撞毀 {1} 次";
        public const string EliminationRule = "60／90／120 秒各淘汰最後一名";

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
