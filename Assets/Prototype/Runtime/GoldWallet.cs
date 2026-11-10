using System;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // Reward currency, separate from the cabinet's inserted coins.
    public sealed class GoldWallet
    {
        private readonly string saveKey;
        public GoldWallet(string saveKey = "TNDA.Gold")
        {
            this.saveKey = saveKey;
            Total = Mathf.Max(0, PlayerPrefs.GetInt(saveKey, 0));
        }
        public int Total { get; private set; }
        public int AwardRound(params (int place, float score)[] players)
        {
            long reward = players.Sum(p => (long)RewardFor(p.place, p.score));
            int added = (int)Math.Min(reward, int.MaxValue - Total);
            Total += added;
            PlayerPrefs.SetInt(saveKey, Total);
            PlayerPrefs.Save();
            return added;
        }
        // Pays for something (a challenge unlock); false and nothing spent when the wallet holds too little.
        public bool TrySpend(int gold)
        {
            if (gold < 0 || gold > Total) return false;
            Total -= gold;
            PlayerPrefs.SetInt(saveKey, Total);
            PlayerPrefs.Save();
            return true;
        }

        public void Clear()
        {
            Total = 0;
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }

        public static int RewardFor(int place, float score)
        {
            int prize = place == 1 ? 100 : place == 2 ? 60 : place <= 4 ? 30 : 10;
            return prize + Mathf.FloorToInt(Mathf.Max(0, score) / 100);
        }
    }
}
