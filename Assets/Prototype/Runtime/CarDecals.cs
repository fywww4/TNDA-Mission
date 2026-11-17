using UnityEngine;

namespace TNDA.Prototype
{
    // Ticket 16: a pattern (外觀) drawn in white/black over the body; the seat colour underneath never changes.
    public enum CarDecal { None, TwinStripes, Flames, Lightning, Checker }

    // The cabinet's shared collection of bought patterns, and which one P1 and P2 each wear. AI cars never wear one.
    // A custom save prefix isolates test saves from the user's.
    public sealed class CarDecals
    {
        public const int Count = 4; // patterns for sale, not counting None
        private static readonly int[] Prices = { 0, 100, 200, 300, 500 };
        private readonly string savePrefix;
        private readonly bool[] owned = new bool[Count + 1];
        private readonly CarDecal[] equipped = new CarDecal[2];

        public CarDecals(string savePrefix = "TNDA.Decals.")
        {
            this.savePrefix = savePrefix;
            owned[0] = true;
            for (int i = 1; i <= Count; i++) owned[i] = PlayerPrefs.GetInt(OwnedKey((CarDecal)i)) == 1;
            for (int seat = 0; seat < equipped.Length; seat++)
            {
                int saved = PlayerPrefs.GetInt(EquipKey(seat));
                equipped[seat] = saved > 0 && saved <= Count && owned[saved] ? (CarDecal)saved : CarDecal.None;
            }
        }

        public static int Price(CarDecal decal) => Prices[(int)decal];
        public const int FramesPerSheet = 16;

        // One sheet frame of a pattern from the four sheets laid end to end (CarDecal order, 16 frames each, in
        // CarSheet's order); null for None or a missing frame.
        public static Sprite Frame(Sprite[] sheets, CarDecal decal, int frame)
        {
            int index = ((int)decal - 1) * FramesPerSheet + frame;
            return decal == CarDecal.None || sheets == null || frame < 0 || frame >= FramesPerSheet || index >= sheets.Length
                ? null : sheets[index];
        }

        public bool Owns(CarDecal decal) => owned[(int)decal];
        // seat 0 = P1, 1 = P2.
        public CarDecal Equipped(int seat) => seat >= 0 && seat < equipped.Length ? equipped[seat] : CarDecal.None;

        public bool TryBuy(CarDecal decal, GoldWallet wallet)
        {
            if (Owns(decal) || wallet == null || !wallet.TrySpend(Price(decal))) return false;
            owned[(int)decal] = true;
            PlayerPrefs.SetInt(OwnedKey(decal), 1);
            PlayerPrefs.Save();
            return true;
        }

        public bool Equip(int seat, CarDecal decal)
        {
            if (seat < 0 || seat >= equipped.Length || !Owns(decal)) return false;
            equipped[seat] = decal;
            PlayerPrefs.SetInt(EquipKey(seat), (int)decal);
            PlayerPrefs.Save();
            return true;
        }

        public void Clear()
        {
            for (int i = 1; i <= Count; i++)
            {
                owned[i] = false;
                PlayerPrefs.DeleteKey(OwnedKey((CarDecal)i));
            }
            for (int seat = 0; seat < equipped.Length; seat++)
            {
                equipped[seat] = CarDecal.None;
                PlayerPrefs.DeleteKey(EquipKey(seat));
            }
            PlayerPrefs.Save();
        }

        private string OwnedKey(CarDecal decal) => savePrefix + "Owned." + decal;
        private string EquipKey(int seat) => savePrefix + "P" + (seat + 1);
    }
}
