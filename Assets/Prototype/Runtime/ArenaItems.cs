using System;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // Serialized as the same 0-3 codes the scene already stores.
    public enum ArenaItem { None = 0, Shield = 1, Rocket = 2, Cherry = 3 }

    // What the item module needs from the match to apply an item.
    public interface IItemArena
    {
        float ShieldSeconds { get; }
        float CherrySeconds { get; }
        // False when every rocket is still flying.
        bool LaunchRocket(ArcadeVehicle car);
        void ItemFeedback(string message, Color color, float seconds);
    }

    // The three crate items: crate draw, use effect and every name/tag/status text, in one place.
    public static class ArenaItems
    {
        private struct Definition
        {
            public string name, tag;
            public Func<ArcadeVehicle, IItemArena, bool> use;
        }

        private static readonly Definition[] Definitions = {
            new Definition { name = CabinetText.NoItem, tag = "" },
            new Definition { name = CabinetText.Shield, tag = CabinetText.ShieldTag, use = UseShield },
            new Definition { name = CabinetText.Rocket, tag = CabinetText.RocketTag, use = UseRocket },
            new Definition { name = CabinetText.Cherry, tag = CabinetText.CherryTag, use = UseCherry },
        };

        public static string Name(ArenaItem item) => Definitions[(int)item].name;
        public static string Tag(ArenaItem item) => Definitions[(int)item].tag;

        // Crates are random: shield or rocket half each; a car in last place first has cherryChance of a cherry.
        public static ArenaItem Roll(bool trailing, float cherryChance, Func<float> random)
        {
            if (trailing && random() < cherryChance) return ArenaItem.Cherry;
            return random() < .5f ? ArenaItem.Shield : ArenaItem.Rocket;
        }

        // Last place counts ties; eliminated cars are out of the standings.
        public static bool IsTrailing(ArcadeVehicle car, ArcadeVehicle[] cars) =>
            cars.Where(c => !c.eliminated).All(c => c == car || c.score >= car.score);

        // Uses the held item and reports whether it was spent. A rocket with a full pool stays held for later.
        public static bool Use(ArcadeVehicle car, IItemArena arena)
        {
            var use = Definitions[(int)car.item].use;
            if (use == null || !use(car, arena)) return false;
            car.item = ArenaItem.None;
            return true;
        }

        public static string Pickup(ArcadeVehicle car) => string.Format(CabinetText.GotItem, car.DriverName, Name(car.item));

        public static string Status(ArcadeVehicle car)
        {
            if (car.HasCherry) return string.Format(CabinetText.StatusCherry, Mathf.CeilToInt(car.CherrySeconds));
            if (car.shieldSeconds > 0) return string.Format(CabinetText.StatusShield, Mathf.CeilToInt(car.shieldSeconds));
            return car.item != ArenaItem.None ? Name(car.item) : null;
        }

        public static string ActionStatus(ArcadeVehicle car) =>
            car.HasCherry ? string.Format(CabinetText.ActionCherry, Mathf.CeilToInt(car.CherrySeconds)) : Name(car.item);

        private static bool UseShield(ArcadeVehicle car, IItemArena arena)
        {
            car.shieldSeconds = arena.ShieldSeconds;
            car.cooldown = .35f;
            arena.ItemFeedback(string.Format(CabinetText.ShieldUp, car.DriverName, arena.ShieldSeconds.ToString("0")), ArcadeGame.Cyan, 2);
            return true;
        }

        private static bool UseRocket(ArcadeVehicle car, IItemArena arena)
        {
            if (!arena.LaunchRocket(car))
            {
                if (!car.computer) arena.ItemFeedback(CabinetText.RocketBusy, ArcadeGame.Yellow, 1);
                return false;
            }
            car.cooldown = .5f;
            arena.ItemFeedback(string.Format(CabinetText.RocketFired, car.DriverName), ArcadeGame.Yellow, 1);
            return true;
        }

        private static bool UseCherry(ArcadeVehicle car, IItemArena arena)
        {
            car.GiveCherry(arena.CherrySeconds);
            arena.ItemFeedback(string.Format(CabinetText.CherryEaten, car.DriverName, arena.CherrySeconds.ToString("0")), car.body.color, 2.5f);
            return true;
        }
    }
}
