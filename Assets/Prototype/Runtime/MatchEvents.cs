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

    // Cues the rules still choose themselves until ticket 07 turns them into events like the ones above.
    public sealed class NoticeCue : MatchEvent
    {
        public readonly string Text;
        public readonly Color Color;
        public readonly float Seconds;
        public NoticeCue(string text, Color color, float seconds) { Text = text; Color = color; Seconds = seconds; }
    }

    // One of the cabinet's generated tones (ArcadeGame.Sound).
    public sealed class ToneCue : MatchEvent
    {
        public readonly int Kind;
        public ToneCue(int kind) { Kind = kind; }
    }

    // The recorded hit sound at a volume (ArcadeGame.PlayHit).
    public sealed class HitCue : MatchEvent
    {
        public readonly float Volume;
        public HitCue(float volume) { Volume = volume; }
    }
}
