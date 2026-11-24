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
        public const string P1Controls = "P1　Z油門　X道具　A煞車　←→轉向";
        public const string P2Controls = "P2　數字8油門　2煞車　4 6轉向　Enter道具";
        // Each player's speedometer: arena units per second × SpeedToKmh, rounded (reversing reads positive too).
        public const string PlayerSpeed = "P{0} {1,3} km/h";
        public const float SpeedToKmh = 40;
        public const string Instructions = P1Controls + "\n" + P2Controls;

        // Title screen (the demo plays behind it). START is Space or the pad's Start.
        public const string NormalRule = "1～2 人　8 台車淘汰賽";
        public const string StartPrompt = "按 START（Enter／空白鍵）開始";
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
        public const string GarageEntry = "SELECT（右Shift／Back）車庫　G捷徑";
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
        public const string GarageControls = "←→分頁　↑↓選擇　X／手把右鍵 購買　Z／手把下鍵 返回";
        public const string GarageStep = "每級 +5%　最多 +20%";
        public const string GarageStrengthStep = "強度：減少受到的傷害";
        // Ticket 16: the 外觀 tab. Patterns are drawn over the seat colour; P1 and P2 each wear their own.
        public const string GarageDecalTab = "←　外觀　→";
        public const string GarageDecalShared = "收藏共用　各自裝備";
        public const string GarageDecalNone = "無圖案";
        public static readonly string[] GarageDecalNames = { GarageDecalNone, "雙條紋", "火焰", "閃電", "棋盤格" };
        public const string GarageOwned = "已擁有";
        public const string GarageDecalBought = "購買完成！P{0} 裝備{1}";
        public const string GarageDecalEquipped = "P{0} 裝備：{1}";
        public const string GarageDecalRule = "圖案畫在座位色上　AI 不裝";
        public const string GarageDecalCherry = "吃櫻桃時暫時換成櫻桃條紋";
        public const string GarageDecalControls = "←→分頁　↑↓選擇　P1：X／手把1右鍵　P2：數字Enter／手把2右鍵";

        // Challenge mode (title list, play and result).
        public const string ModeNormal = "一般模式";
        public const string ModeChallenge = "挑戰模式";
        public const string ModeCampaign = "關卡模式";
        public const string ModeTabs = "←　{0}　{1}　→";

        // Campaign mode (ticket 15): title list, story, play and result. Story pages are "speaker｜line" placeholders
        // until the user supplies the script.
        public const string CampaignRule = "只限單人　過關後開放下一關";
        public const string CampaignPrompt = "↑↓選擇　按 START 開始";
        public const string CampaignLocked = "先通過上一關才能挑戰";
        public const string CampaignRow = "{0} 第{1}關 {2}　{3}";
        public const string CampaignCleared = "已過關";
        public const string CampaignOpen = "挑戰中";
        public const string CampaignLockedShort = "未開放";
        public const string StageTitle = "第{0}關 {1}";
        public const string StoryHint = "按 START 繼續　按 Esc 返回";
        public const string StoryPage = "{0}：\n\n{1}";
        public const string Stage1Name = "新手教學";
        public const string Stage1Goal = "在訓練場學會操作，再把 3 台碰碰車撞出局";
        public static readonly string[] Stage1Story = {
            "教練｜歡迎來到廢車場訓練場，菜鳥。",
            "教練｜這裡是黑盒子，沒有觀眾，撞爛了也沒人笑你。",
            "教練｜照我說的一步一步做，最後跟 3 台碰碰車打一場。",
        };
        public const string Stage2Name = "金字塔淘汰賽";
        public const string Stage2Goal = "8 台車淘汰賽，拿到冠軍";
        public static readonly string[] Stage2Story = {
            "主持人｜各位觀眾！今天有新面孔上場！",
            "主持人｜8 台車、3 次淘汰，只有冠軍能留下來！",
            "教練｜記住訓練場學到的，去拿冠軍。",
        };
        public const string Stage3Name = "魔王戰";
        public const string Stage3Goal = "和 2 台友軍一起撞毀魔王";
        public static readonly string[] Stage3Story = {
            "魔王｜就是你這小子在我的廢車場出風頭？",
            "魔王｜我和我的手下會把你壓成一塊鐵餅。",
            "教練｜別怕，我派了 2 台車幫你。撞毀魔王就贏了！",
        };
        public const string AllyName = "友軍";
        public const string BossName = "魔王";
        public const string MinionName = "嘍囉";
        public const string StageBoard = "第{0}關　{1}";
        public const string TutorialBoard = "教學 {0}／{1}　{2}";
        public static readonly string[] TutorialSteps = {
            "按住油門鍵往前開",
            "邊開邊按←→，轉一整圈",
            "按住煞車鍵，停下後繼續按住倒車",
            "開去撞一台車",
            "開過道具箱撿道具",
            "按道具鍵使用道具",
        };
        public const string TutorialStepDone = "完成！";
        public const string TutorialFight = "開打！把 3 台車都撞出局！";
        public const string TutorialFightBoard = "撞出局！剩 {0} 台　{1:00}秒";
        public const string BossBoard = "魔王 耐久 {0}%　剩 {1:000} 秒";
        public const string KnockedOut = "{0} 出局！";
        public const string StageClear = "第{0}關 過關！";
        public const string StageFailed = "第{0}關 失敗";
        public const string CampaignComplete = "全部關卡完成！";
        public const string StageNextHint = "按 START 下一關　按 Esc 回標題";
        public const string StageRetryHint = "按 START 重試　按 Esc 回標題";
        public const string StageReplayHint = "按 START 再玩一次　按 Esc 回標題";
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
        public const string RacePlace = "{0}<size=11>/{1}</size>";
        public const string RaceEliminatedPlace = "淘汰<size=11> 第{0}名</size>";
        public const string RaceScore = "P{0}　分數 {1:00000}";
        public const string RaceTime = "P{0}　時間 {1:000}　撞毀 {2}";
        public const string P1StandingsHint = "按住Tab／SELECT 看排行";
        public const string P2StandingsHint = "按住數字0／SELECT 看排行";
        public const string RaceStanding = "{0} {1} {2:00000} 耐久{3}";
        public const string RaceStandingEliminated = "{0}　淘汰";

        // Cheat menu (~ key, any screen while the game runs; the game pauses while it is open).
        public const string CheatTitle = "作弊選單　　（~／Esc 關閉）";
        public const string CheatGold = "1　金幣 +1000（現有 {0}）";
        public const string CheatStages = "2　全關卡破關";
        public const string CheatScore = "3　分數 +1000";
        public const string CheatWipe = "4　消滅所有敵軍";
        public const string CheatInvincible = "5　無敵模式：{0}";
        public const string CheatOn = "開", CheatOff = "關";
        public const string CheatGoldDone = "金幣 +1000，現有 {0}";
        public const string CheatStagesDone = "全部關卡已破關";
        public const string CheatScoreDone = "P1 分數 +1000";
        public const string CheatWipeDone = "敵軍已全部消滅";
        public const string CheatInvincibleDone = "無敵模式{0}";
        public const string CheatNeedsMatch = "比賽中才能使用";

        // Developer mode (' toggles it): plain text, no icons. 2026-11-23 user request.
        public const string DevTitle = "開發者模式　（' 關閉）";
        public const string DevNoMatch = "沒有比賽資料";
        public const string DevCar = "{0}　血量 {1:0}%　裝甲 ×{2:0.0#}　{3}　道具：{4}";
        public const string DevDrops = "　木箱爆率　{0}";
        public const string DevDrop = "{0} {1:0}%";
        public const string DevDriving = "行駛中", DevWrecked = "撞毀，{0:0.0} 秒後重生", DevOut = "已淘汰";
        public const string DevNone = "無";
        public const string DevFixedCrates = "固定道具木箱不在此列；火箭限定時全為火箭";

        // Every string above (templates included), for building the pixel font's character set.
        public static IEnumerable<string> All => typeof(CabinetText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(f => f.GetValue(null) is string[] list ? list : new[] { f.GetValue(null) as string })
            .Where(s => s != null);
    }
}
