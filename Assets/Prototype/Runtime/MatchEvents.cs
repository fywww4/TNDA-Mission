using UnityEngine;

namespace TNDA.Prototype
{
    // What happened in the match this frame. DemolitionMatch only records events; DemolitionGame (the scene adapter)
    // turns them into notices, sounds and effects, and tests read them directly.
    public abstract class MatchEvent { }

    // Car drove through a wooden fence and scores Points (any car, like a prop); the adapter scatters wood pieces there.
    public sealed class FenceSmashed : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly ArenaObstacle Obstacle;
        public readonly int Points;
        public FenceSmashed(ArcadeVehicle car, ArenaObstacle obstacle, int points = 0) { Car = car; Obstacle = obstacle; Points = points; }
    }

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

    // How two cars met (ticket 18). Front: nose into a car that is not side-on or running ahead; Rear: from behind;
    // Side: into its flank (the victim spins); Scrape: side by side heading the same way (both pushed apart, sparks).
    public enum ImpactKind { Front, Rear, Side, Scrape }

    // Attacker drove into Victim. Impact is the attacker's speed into the victim (for a scrape, the faster car is the
    // attacker and Impact the two cars' relative speed); Multiplier is 2 for a side hit, 1.5 from behind, 1 head-on and less for a scrape.
    // VictimHurt: the victim lost durability and is still driving. VictimWrecked: this ram wrecked the victim.
    public sealed class CarRammed : MatchEvent
    {
        public readonly ArcadeVehicle Attacker, Victim;
        public readonly float Impact, Multiplier;
        public readonly bool VictimHurt, VictimWrecked;
        public readonly ImpactKind Kind;
        public readonly Vector2 Point;
        public CarRammed(ArcadeVehicle attacker, ArcadeVehicle victim, float impact, float multiplier, bool victimHurt,
            ImpactKind kind, bool victimWrecked)
        {
            Attacker = attacker; Victim = victim; Impact = impact; Multiplier = multiplier; VictimHurt = victimHurt;
            Kind = kind; VictimWrecked = victimWrecked;
            Point = (attacker.position + victim.position) / 2;
        }
    }

    // A player wrecked a car by driving into it, or rammed it at prop-smashing speed: pieces of Victim scatter across
    // the floor (DemolitionMatch.Parts). Computer rammers and shielded victims shed nothing.
    public sealed class DebrisSplashed : MatchEvent
    {
        public readonly ArcadeVehicle Rammer, Victim;
        public DebrisSplashed(ArcadeVehicle rammer, ArcadeVehicle victim) { Rammer = rammer; Victim = victim; }
    }

    // A cherry car touched another car (or rammed it while the other held the cherry). Wrecked is false when the harm
    // rules protected the victim.
    public sealed class CherryStrike : MatchEvent
    {
        public readonly ArcadeVehicle Attacker, Victim;
        public readonly bool Wrecked;
        public CherryStrike(ArcadeVehicle attacker, ArcadeVehicle victim, bool wrecked) { Attacker = attacker; Victim = victim; Wrecked = wrecked; }
    }
    // Ticket 21: Car drove into a spike ball (or the ball rolled into it). Hurt: it lost durability (no shield or
    // protection); Wrecked: that finished it.
    public sealed class SpikeBallHit : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly float Impact;
        public readonly bool Hurt, Wrecked;
        public readonly Vector2 Point;
        public SpikeBallHit(ArcadeVehicle car, float impact, bool hurt, bool wrecked, Vector2 point)
        { Car = car; Impact = impact; Hurt = hurt; Wrecked = wrecked; Point = point; }
    }

    // Ticket 21: a spike ball Launcher knocked flying reached Victim; Wrecked unless something protected it.
    public sealed class SpikeBallStruck : MatchEvent
    {
        public readonly ArcadeVehicle Launcher, Victim;
        public readonly bool Wrecked;
        public readonly Vector2 Point;
        public SpikeBallStruck(ArcadeVehicle launcher, ArcadeVehicle victim, bool wrecked, Vector2 point)
        { Launcher = launcher; Victim = victim; Wrecked = wrecked; Point = point; }
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

    // Every score change (props, rams, rocket hits, wreck bonus and penalty), at the car's position when it happened.
    // Reported after the event that caused it.
    public sealed class ScoreChanged : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly int Amount;
        public readonly Vector2 Point;
        public ScoreChanged(ArcadeVehicle car, int amount, Vector2 point) { Car = car; Amount = amount; Point = point; }
    }

    // A paid continue brought an eliminated car back, tied with the lowest surviving score.
    public sealed class VehicleRevived : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public VehicleRevived(ArcadeVehicle car) { Car = car; }
    }

    // A player's car pulled in a scrap part (Kind is its Debris.png cell) and scores for it.
    public sealed class PartCollected : MatchEvent
    {
        public readonly ArcadeVehicle Car;
        public readonly int Kind;
        public PartCollected(ArcadeVehicle car, int kind) { Car = car; Kind = kind; }
    }

    // Car drove onto Dropper's oil slick and spun out (a player's view is smeared with oil, Mode7Debris).
    public sealed class CarSlipped : MatchEvent
    {
        public readonly ArcadeVehicle Car, Dropper;
        public CarSlipped(ArcadeVehicle car, ArcadeVehicle dropper) { Car = car; Dropper = dropper; }
    }

    public sealed class MatchEnded : MatchEvent
    {
        public readonly ArcadeVehicle Winner;
        public MatchEnded(ArcadeVehicle winner) { Winner = winner; }
    }
}
