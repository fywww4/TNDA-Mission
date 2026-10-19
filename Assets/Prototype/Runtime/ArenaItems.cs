using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TNDA.Prototype
{
    // Serialized as the same codes the scene already stores (Oil added 2026-10-16).
    public enum ArenaItem { None = 0, Shield = 1, Rocket = 2, Cherry = 3, Oil = 4 }

    // What the item module needs from the match to apply an item.
    public interface IItemArena
    {
        float ShieldSeconds { get; }
        float CherrySeconds { get; }
        // False when every rocket is still flying.
        bool LaunchRocket(ArcadeVehicle car);
        // Drops an oil slick behind the car; false when the shared rocket/oil pool is full.
        bool DropOil(ArcadeVehicle car);
    }

    // The crate items: crate draw, use effect and every name/tag/status text, in one place.
    public static class ArenaItems
    {
        private struct Definition
        {
            public string name, used;
            public Func<ArcadeVehicle, IItemArena, bool> use;
        }

        private static readonly Definition[] Definitions = {
            new Definition { name = CabinetText.NoItem },
            new Definition { name = CabinetText.Shield, used = CabinetText.ShieldUp, use = UseShield },
            new Definition { name = CabinetText.Rocket, used = CabinetText.RocketFired, use = UseRocket },
            new Definition { name = CabinetText.Cherry, used = CabinetText.CherryEaten, use = UseCherry },
            new Definition { name = CabinetText.Oil, used = CabinetText.OilDropped, use = UseOil },
        };

        // An unknown stored code reads as no item, like the old integer checks.
        private static Definition Of(ArenaItem item) => (uint)item < Definitions.Length ? Definitions[(int)item] : Definitions[0];

        public static string Name(ArenaItem item) => Of(item).name;

        // Crates are random: shield, rocket or oil a third each; a car in last place first has cherryChance of a cherry.
        public static ArenaItem Roll(bool trailing, float cherryChance, Func<float> random)
        {
            if (trailing && random() < cherryChance) return ArenaItem.Cherry;
            float r = random();
            return r < 1 / 3f ? ArenaItem.Shield : r < 2 / 3f ? ArenaItem.Rocket : ArenaItem.Oil;
        }

        // Last place counts ties; eliminated cars are out of the standings.
        public static bool IsTrailing(ArcadeVehicle car, IReadOnlyList<ArcadeVehicle> cars) =>
            cars.Where(c => !c.eliminated).All(c => c == car || c.score >= car.score);

        // Uses the held item and reports whether it was spent. A rocket with a full pool stays held for later.
        public static bool Use(ArcadeVehicle car, IItemArena arena)
        {
            var use = Of(car.item).use;
            if (use == null || !use(car, arena)) return false;
            car.item = ArenaItem.None;
            return true;
        }

        public static string PickupMessage(ArcadeVehicle car, ArenaItem item) => string.Format(CabinetText.GotItem, car.DriverName, Name(item));

        // seconds is how long the item lasts (shield and cherry); the rocket message ignores it.
        public static string UsedMessage(ArcadeVehicle car, ArenaItem item, float seconds) =>
            string.Format(Of(item).used ?? "", car.DriverName, seconds.ToString("0"));

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
            return true;
        }

        private static bool UseRocket(ArcadeVehicle car, IItemArena arena)
        {
            if (!arena.LaunchRocket(car)) return false;
            car.cooldown = .5f;
            return true;
        }

        private static bool UseOil(ArcadeVehicle car, IItemArena arena)
        {
            if (!arena.DropOil(car)) return false;
            car.cooldown = .35f;
            return true;
        }

        private static bool UseCherry(ArcadeVehicle car, IItemArena arena)
        {
            car.GiveCherry(arena.CherrySeconds);
            return true;
        }
    }
}
