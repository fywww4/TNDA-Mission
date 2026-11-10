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
        public const string P1ManualControls = P1Controls + "　↓降檔　右Shift升檔";
        public const string P2ManualControls = P2Controls + "　S降檔　左Shift升檔";
        public const string ManualTransmission = "手排";
        public const string AutomaticTransmission = "自排";
        public const string TransmissionOption = "{0}　T／手把Y切換　LB降檔 RB升檔";
        public const string PlayerGear = "P{0} {1} {2}檔";
        public const string ReverseGear = "R";
        public const string Instructions = P1Controls + "\n" + P2Controls;

        // Title screen (the demo plays behind it). START is Space or the pad's Start.
        public const string NormalRule = "1～2 人　8 台車淘汰賽";
        public const string StartPrompt = "按 START（空白鍵）開始";
        // The player-count menu: the two choices (the chosen one tinted) and how to start.
        public const string PlayerMenu = "←　{0}　{1}　→　START 開始";
        public const string OnePlayer = "1 人";
        public const string TwoPlayers = "2 人";
        public const string OnePlayerButton = "1 人";
        public const string TwoPlayerButton = "2 人";
        public const string AttractHint = "按 START 開始";
        public const string ResultsHint = "按 START 再玩　按 Esc 回標題";

        // Saved reward currency, the game's only one.
        public const string GoldEarned = "+{0} 金幣";
        public const string GoldTotal = "累計 {0} 金幣";
        public const string ResetSaveHint = "長按 Delete 3 秒清除存檔";
        public const string ResetSaveConfirm = "清除存檔？繼續按住 Delete {0} 秒";
        public const string SaveCleared = "存檔已清除，請放開 Delete";

        public const string GarageTitle = "車庫";
        public const string GarageEntry = "G／手把X 車庫";
        public const string GarageAbilityTab = "←　能力　→";
        public const string GarageShared = "P1・P2 共用能力";
        public const string GarageTopSpeed = "最高速";
        public const string GarageAcceleration = "加速";
        public const string GarageHandling = "操控";
        public const string GarageStrength = "強度";
        public static readonly string[] GarageAbilities = { GarageTopSpeed, GarageAcceleration, GarageHandling, GarageStrength };
        public const string GarageBonus = "+{0}%";
        public const string GaragePrice = "{0} 金幣";
        public const string GarageMaxed = "已滿";
        public const string GarageBought = "升級完成！";
        public const string GarageGoldShort = "金幣不足";
        public const string GarageControls = "↑↓選擇　左Ctrl／手把A 購買　Esc／手把B 返回";
        public const string GarageStep = "每級 +5%　最多 +20%";
        public const string GarageStrengthStep = "強度：減少受到的傷害";

        // Challenge mode (title list, play and result).
        public const string ModeNormal = "一般模式";
        public const string ModeChallenge = "挑戰模式";
        public const string ModeTabs = "←　{0}　{1}　→";
        public const string ChallengeRule = "只限單人";
        public const string ChallengePrompt = "↑↓選擇　按 START 開始";
        public const string ChallengeLocked = "按 START 花 {0} 金幣解鎖";
        public const string ChallengeGoldShort = "金幣不足　需 {0}，現有 {1}";
        public const string ChallengeBought = "已解鎖「{0}」";
        public const string ChallengeComingSoon = "準備中，尚未開放";
        public const string ChallengeRow = "{0} {1}　{2}";
        public const string ChallengeBestShort = "最佳 {0}";
        public const string ChallengeUnlocked = "已解鎖";
        public const string ChallengeLockedShort = "未解鎖 {0} 金幣";
        public const string ChallengeRocketOnly = "只能用火箭";
        public const string ChallengeRocketOnlyRule = "木箱只開火箭，衝撞不計分，比 120 秒最高分";
        public const string ChallengeScore90 = "90 秒最高分";
        public const string ChallengeScore90Rule = "8 台車不淘汰，比 90 秒內的總分";
        public const string ChallengeNoWreck = "不被撞毀";
        public const string ChallengeNoWreckRule = "3 台追王追你，撐過 120 秒不被撞毀";
        public const string ChallengeBoard = "挑戰　{0}";
        public const string ChallengeScoreLine = "本次成績 {0}";
        public const string ChallengeBestLine = "最佳紀錄 {0}";
        // 不被撞毀's result: the durability left on a pass; a wreck (or no pass yet) shows ChallengeNotPassed.
        public const string ChallengeDurabilityLeft = "耐久 {0}%";
        public const string ChallengeNotPassed = "未過關";
        public const string ChallengeNewBest = "新紀錄！";
        public const string ChallengeResultsHint = "按 START 重試　按 Esc 回列表";

        public const string Go = "出發！";

        // Continue after a timed elimination before the final: 10-second countdown, free, once per player a match.
        public const string ContinueTitle = "{0} 繼續？";
        public const string ContinuePress = "按 {0} 繼續（本局限 1 次）";
        public const string P1ContinueKey = "空白鍵";
        public const string P2ContinueKey = "數字鍵Enter";
        public const string Revived = "{0} 繼續！";
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
        public const string EliminationStep = "{0} 秒淘汰 {1} 台";
        public const string EliminationFinal = "{0} 秒決賽";

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
