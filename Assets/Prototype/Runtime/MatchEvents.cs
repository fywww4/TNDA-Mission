using UnityEngine;

namespace TNDA.Prototype
{
    // What happened in the match this frame. DemolitionMatch only records events; DemolitionGame (the scene adapter)
    // turns them into notices, sounds and effects, and tests read them directly.
    public abstract class MatchEvent { }

    // A rocket struck a car (even one whose shield blocked it), at the car's position.
    public sealed class RocketHit : MatchEvent
    {
        public readonly ArcadeVehicle Attacker, Victim;
        public readonly Vector2 Point;
        public RocketHit(ArcadeVehicle attacker, ArcadeVehicle victim, Vector2 point) { Attacker = attacker; Victim = victim; Point = point; }
    }

    // A car rammed a prop to pieces, or its rocket did, scoring the prop's Points.
    public sealed class PropSmashed : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly int Points;
        public readonly bool ByRocket;
        public PropSmashed(ArcadeVehicle car, int points, bool byRocket) { Car = car; Points = points; ByRocket = byRocket; }
    }

    // A car drove over a crate and now holds Item.
    public sealed class ItemPicked : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly ArenaItem Item;
        public ItemPicked(ArcadeVehicle car, ArenaItem item) { Car = car; Item = item; }
    }

    // A car spent its held Item.
    public sealed class ItemUsed : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly ArenaItem Item;
        public ItemUsed(ArcadeVehicle car, ArenaItem item) { Car = car; Item = item; }
    }

    // A car pressed for its rocket while both rockets were still flying, so it keeps the rocket for later.
    public sealed class RocketsBusy : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public RocketsBusy(ArcadeVehicle car) { Car = car; }
    }

    // Attacker drove into Victim (Impact is the closing speed, Multiplier 2 for a side hit, 1.5 from behind).
    // VictimHurt: the victim lost durability and is still driving.
    public sealed class CarRammed : MatchEvent
    {
        public readonly ArcadeVehicle Attacker, Victim;
        public readonly float Impact, Multiplier;
        public readonly bool VictimHurt;
        public CarRammed(ArcadeVehicle attacker, ArcadeVehicle victim, float impact, float multiplier, bool victimHurt)
        {
            Attacker = attacker; Victim = victim; Impact = impact; Multiplier = multiplier; VictimHurt = victimHurt;
        }
    }

    // A cherry car touched another car (or rammed it while the other held the cherry). Wrecked is false when the harm
    // rules protected the victim.
    public sealed class CherryStrike : MatchEvent
    {
        public readonly ArcadeVehicle Attacker, Victim;
        public readonly bool Wrecked;
        public CherryStrike(ArcadeVehicle attacker, ArcadeVehicle victim, bool wrecked) { Attacker = attacker; Victim = victim; Wrecked = wrecked; }
    }

    // A car hit the arena wall fast enough to be heard.
    public sealed class WallHit : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public WallHit(ArcadeVehicle car) { Car = car; }
    }

    public sealed class ShieldBlocked : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public ShieldBlocked(ArcadeVehicle car) { Car = car; }
    }

    // Durability ran out: the car spins off, loses Deducted points and respawns later.
    public sealed class VehicleWrecked : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly int Deducted;
        public VehicleWrecked(ArcadeVehicle car, int deducted) { Car = car; Deducted = deducted; }
    }

    public sealed class WreckBonus : MatchEvent
    {
        public readonly ArcadeVehicle Attacker;
        public readonly int Points;
        public WreckBonus(ArcadeVehicle attacker, int points) { Attacker = attacker; Points = points; }
    }

    // A timed (or demo) elimination: the car is out for good, finishing in Place.
    public sealed class VehicleEliminated : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly int Place;
        public VehicleEliminated(ArcadeVehicle car, int place) { Car = car; Place = place; }
    }

    public sealed class MatchEnded : MatchEvent
    {
        public readonly ArcadeVehicle Winner;
        public MatchEnded(ArcadeVehicle winner) { Winner = winner; }
    }
}
