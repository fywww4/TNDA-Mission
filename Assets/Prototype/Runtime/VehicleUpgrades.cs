using UnityEngine;

namespace TNDA.Prototype
{
    public enum VehicleAbility { TopSpeed, Acceleration, Handling, Strength }

    // The cabinet's shared human-car upgrades. A custom save prefix isolates test wallets from the user's save.
    public sealed class VehicleUpgrades
    {
        public const int MaxLevel = 4;
        private static readonly int[] Prices = { 50, 100, 200, 400 };
        private readonly string savePrefix;
        private readonly int[] levels = new int[4];

        public VehicleUpgrades(string savePrefix = "TNDA.Upgrades.")
        {
            this.savePrefix = savePrefix;
            for (int i = 0; i < levels.Length; i++) levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(savePrefix + (VehicleAbility)i), 0, MaxLevel);
        }

        public int Level(VehicleAbility ability) => levels[(int)ability];
        public float Bonus(VehicleAbility ability) => Level(ability) * .05f;
        public int Price(VehicleAbility ability) => Level(ability) == MaxLevel ? 0 : Prices[Level(ability)];
        public bool TryBuy(VehicleAbility ability, GoldWallet wallet)
        {
            if (Level(ability) >= MaxLevel || wallet == null || !wallet.TrySpend(Price(ability))) return false;
            levels[(int)ability]++;
            PlayerPrefs.SetInt(savePrefix + ability, Level(ability));
            PlayerPrefs.Save();
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = 0;
                PlayerPrefs.DeleteKey(savePrefix + (VehicleAbility)i);
            }
            PlayerPrefs.Save();
        }
    }
}
