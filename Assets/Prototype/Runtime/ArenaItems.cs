using System;
using System.Collections.Generic;
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
    }

    // The three crate items: crate draw, use effect and every name/tag/status text, in one place.
    public static class ArenaItems
    {
        private struct Definition
        {
            public string name, tag, used;
            public Func<ArcadeVehicle, IItemArena, bool> use;
        }

        private static readonly Definition[] Definitions = {
            new Definition { name = CabinetText.NoItem, tag = "" },
            new Definition { name = CabinetText.Shield, tag = CabinetText.ShieldTag, used = CabinetText.ShieldUp, use = UseShield },
            new Definition { name = CabinetText.Rocket, tag = CabinetText.RocketTag, used = CabinetText.RocketFired, use = UseRocket },
            new Definition { name = CabinetText.Cherry, tag = CabinetText.CherryTag, used = CabinetText.CherryEaten, use = UseCherry },
        };

        // An unknown stored code reads as no item, like the old integer checks.
        private static Definition Of(ArenaItem item) => (uint)item < Definitions.Length ? Definitions[(int)item] : Definitions[0];

        public static string Name(ArenaItem item) => Of(item).name;
        public static string Tag(ArenaItem item) => Of(item).tag;

        // Crates are random: shield or rocket half each; a car in last place first has cherryChance of a cherry.
        public static ArenaItem Roll(bool trailing, float cherryChance, Func<float> random)
        {
            if (trailing && random() < cherryChance) return ArenaItem.Cherry;
            return random() < .5f ? ArenaItem.Shield : ArenaItem.Rocket;
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

        private static bool UseCherry(ArcadeVehicle car, IItemArena arena)
        {
            car.GiveCherry(arena.CherrySeconds);
            return true;
        }
    }
}
