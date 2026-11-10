using TMPro;
using UnityEngine;

namespace TNDA.Prototype
{
    // Pixel cabinet view only. Purchases and their save belong to VehicleUpgrades; the cabinet owns navigation.
    public sealed class GarageScreen : MonoBehaviour
    {
        public TMP_Text gold, message;
        public TMP_Text[] names, bonuses, prices;
        public UnityEngine.UI.Image[] rows, bars;
        public UnityEngine.UI.Image car;
        public Sprite[] carFrames;
        private float spinAge;

        public void Render(ArcadeGame game, float dt)
        {
            bool shown = game.Phase == ArcadePhase.Garage;
            gameObject.SetActive(shown);
            if (!shown) return;
            spinAge += dt;
            if (carFrames != null && carFrames.Length == 16) car.sprite = carFrames[Mathf.FloorToInt(spinAge * 2) % 16];
            gold.text = string.Format(CabinetText.GoldTotal, game.Gold);
            message.text = game.GarageMessage;
            for (int i = 0; i < 4; i++)
            {
                var ability = (VehicleAbility)i;
                int level = game.Upgrades.Level(ability);
                bool selected = i == game.GarageCursor;
                rows[i].color = selected ? ArcadeGame.Yellow : new Color32(53, 73, 114, 255);
                names[i].text = CabinetText.GarageAbilities[i];
                names[i].color = selected ? ArcadeGame.Yellow : ArcadeGame.White;
                bonuses[i].text = string.Format(CabinetText.GarageBonus, level * 5);
                prices[i].text = level == VehicleUpgrades.MaxLevel ? CabinetText.GarageMaxed : string.Format(CabinetText.GaragePrice, game.Upgrades.Price(ability));
                prices[i].color = level == VehicleUpgrades.MaxLevel || game.Gold >= game.Upgrades.Price(ability) ? ArcadeGame.Yellow : ArcadeGame.White;
                for (int j = 0; j < 4; j++) bars[i * 4 + j].color = j < level ? ArcadeGame.Cyan : new Color32(44, 49, 69, 255);
            }
        }
    }
}
