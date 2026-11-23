using TMPro;
using UnityEngine;

namespace TNDA.Prototype
{
    // Pixel cabinet view only. Purchases and their save belong to VehicleUpgrades and CarDecals; the cabinet owns navigation.
    public sealed class GarageScreen : MonoBehaviour
    {
        public TMP_Text gold, message, controls;
        public TMP_Text[] names, bonuses, prices;
        public UnityEngine.UI.Image[] rows, bars;
        public UnityEngine.UI.Image car;
        public Sprite[] carFrames;
        [Header("外觀 tab (ticket 16)")]
        public GameObject abilityPanel, decalPanel;
        public TMP_Text tab, shared, rule, note;
        public TMP_Text[] decalNames, decalPrices, decalP1, decalP2;
        public UnityEngine.UI.Image[] decalRows;
        [Tooltip("Pattern drawn over the turntable car (white/black, no palette swap).")]
        public UnityEngine.UI.Image carDecal;
        [Tooltip("Four 16-frame pattern sheets one after another (Mode7Stage.decalFrames).")]
        public Sprite[] decalFrames;
        private float spinAge;

        private static readonly Color RowIdle = new Color32(53, 73, 114, 255);

        public void Render(ArcadeGame game, float dt)
        {
            bool shown = game.Phase == ArcadePhase.Garage;
            gameObject.SetActive(shown);
            if (!shown) return;
            spinAge += dt;
            int frame = Mathf.FloorToInt(spinAge * 2) % 16;
            if (carFrames != null && carFrames.Length == 16) car.sprite = carFrames[frame];
            gold.text = string.Format(CabinetText.GoldTotal, game.Gold);
            message.text = game.GarageMessage;
            bool decalsTab = game.GarageTab == GarageTab.Decals;
            if (controls != null) controls.text = decalsTab ? CabinetText.GarageDecalControls : CabinetText.GarageControls;
            if (abilityPanel != null) abilityPanel.SetActive(!decalsTab);
            if (decalPanel != null) decalPanel.SetActive(decalsTab);
            if (tab != null) tab.text = decalsTab ? CabinetText.GarageDecalTab : CabinetText.GarageAbilityTab;
            if (shared != null) shared.text = decalsTab ? CabinetText.GarageDecalShared : CabinetText.GarageShared;
            if (rule != null) rule.text = decalsTab ? CabinetText.GarageDecalRule : CabinetText.GarageStep;
            if (note != null) note.text = decalsTab ? CabinetText.GarageDecalCherry : CabinetText.GarageStrengthStep;

            // The turntable previews the highlighted pattern on the 外觀 tab, P1's own pattern otherwise.
            var preview = decalsTab ? (CarDecal)game.GarageCursor : game.Decals.Equipped(0);
            ShowDecal(preview, frame);

            if (decalsTab) RenderDecals(game);
            else RenderAbilities(game);
        }

        private void ShowDecal(CarDecal decal, int frame)
        {
            if (carDecal == null) return;
            var sprite = CarDecals.Frame(decalFrames, decal, frame);
            carDecal.enabled = sprite != null;
            if (sprite != null) carDecal.sprite = sprite;
        }

        private void RenderAbilities(ArcadeGame game)
        {
            for (int i = 0; i < 4; i++)
            {
                var ability = (VehicleAbility)i;
                int level = game.Upgrades.Level(ability);
                bool selected = i == game.GarageCursor;
                rows[i].color = selected ? ArcadeGame.Yellow : RowIdle;
                names[i].text = CabinetText.GarageAbilities[i];
                names[i].color = selected ? ArcadeGame.Yellow : ArcadeGame.White;
                bonuses[i].text = string.Format(CabinetText.GarageBonus, level * 5);
                prices[i].text = level == VehicleUpgrades.MaxLevel ? CabinetText.GarageMaxed : string.Format(CabinetText.GaragePrice, game.Upgrades.Price(ability));
                prices[i].color = level == VehicleUpgrades.MaxLevel || game.Gold >= game.Upgrades.Price(ability) ? ArcadeGame.Yellow : ArcadeGame.White;
                for (int j = 0; j < 4; j++) bars[i * 4 + j].color = j < level ? ArcadeGame.Cyan : new Color32(44, 49, 69, 255);
            }
        }

        private void RenderDecals(ArcadeGame game)
        {
            if (decalRows == null) return;
            for (int i = 0; i < decalRows.Length; i++)
            {
                var decal = (CarDecal)i;
                bool selected = i == game.GarageCursor;
                bool owned = game.Decals.Owns(decal);
                decalRows[i].color = selected ? ArcadeGame.Yellow : RowIdle;
                decalNames[i].text = CabinetText.GarageDecalNames[i];
                decalNames[i].color = selected ? ArcadeGame.Yellow : ArcadeGame.White;
                decalPrices[i].text = decal == CarDecal.None ? "" : owned ? CabinetText.GarageOwned
                    : string.Format(CabinetText.GaragePrice, CarDecals.Price(decal));
                decalPrices[i].color = owned ? ArcadeGame.Cyan : game.Gold >= CarDecals.Price(decal) ? ArcadeGame.Yellow : ArcadeGame.White;
                // Seat colours mark who wears it.
                decalP1[i].text = game.Decals.Equipped(0) == decal ? string.Format(CabinetText.DriverName, 1) : "";
                decalP2[i].text = game.Decals.Equipped(1) == decal ? string.Format(CabinetText.DriverName, 2) : "";
            }
        }
    }
}
