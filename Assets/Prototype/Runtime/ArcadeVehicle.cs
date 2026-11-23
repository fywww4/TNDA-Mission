using UnityEngine;

namespace TNDA.Prototype
{
    public struct VehicleLifecycleRules
    {
        public int wreckPenalty;
        public float respawnSeconds, spinStartSpeed, spinAcceleration, spinTurnsPerSecond;
        public Vector2 arenaCentre;
    }

    public class ArcadeVehicle : MonoBehaviour
    {
        public SpriteRenderer body;
        public int index;
        public Vector2 position;
        public float heading, speed, health, score, stun, cooldown;
        public bool alive, computer, eliminated;
        public float shieldSeconds, spawnProtection;
        public ArenaItem item;
        public float respawn;
        // Campaign (ticket 15), set by the match on every reset: cars on the same team (above 0) cannot hurt each other;
        // size scales the car's footprint (and its picture), armor divides the damage it takes, mass sets how far a
        // ram pushes it and how hard it pushes back. nameTag replaces the seat name (P1..P8) when set.
        public int team;
        public float size = 1, armor = 1, mass = 1;
        public string nameTag;
        // Half the gap two touching cars keep apart (0.31 for an ordinary car).
        public float Radius => .31f * size;

        public float CherrySeconds { get; private set; }
        // Spinning on an oil slick: no control, no speed, no damage.
        public float SlipSeconds { get; private set; }
        public bool IsSlipping => alive && SlipSeconds > 0;
        public bool Airborne => alive && flightLeft > 0;
        public float AirHeight => Airborne ? flightBase * (flightLeft / flightSeconds)
            + 4 * flightHeight * (flightLeft / flightSeconds) * (1 - flightLeft / flightSeconds) : 0;
        private float flightLeft, flightSeconds, flightHeight, flightBase;
        public bool HasCherry => alive && CherrySeconds > 0;
        public Vector2 SlideVelocity { get; private set; }
        public bool IsSpinningOut => spinSpeed > 0;
        public bool ReadyToRespawn => !alive && !eliminated && respawn <= 0;
        // Shown while driving or spinning off; a wreck waiting to respawn and a finished fly-out are hidden. An
        // already-hidden wreck that gets eliminated shows again while it flies out.
        public bool Visible => alive || IsSpinningOut;
        public int FinalPlace { get; internal set; }

        // Bumps into walls, props and cars, and lives started; the AI driver watches these to replan.
        public int Collisions { get; private set; }
        public int Spawns { get; private set; }
        public AiDriver Ai => ai ?? (ai = new AiDriver(this));
        [System.NonSerialized] private AiDriver ai;
        private float spinSpeed;
        private Vector2 spinDirection;
        // A side hit's turn on the spot: degrees per second, and seconds of it left.
        private float hitSpinRate, hitSpinSeconds;
        private const float ProtectionSeconds = 2;

        // P1 to P8 by seat in the arcade, for players and computer cars alike.
        public string DriverName => string.IsNullOrEmpty(nameTag) ? string.Format(CabinetText.DriverName, index + 1) : nameTag;
        public Vector2 Forward => new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));

        public void ResetVehicle(Vector2 start, float angle, int playerCount)
        {
            eliminated = false;
            score = 0;
            computer = index >= playerCount;
            Respawn(start, angle);
        }

        public void Respawn(Vector2 start, float angle)
        {
            if (eliminated) return;
            ClearTransientState();
            position = start;
            heading = angle;
            health = 1;
            alive = true;
            Spawns++;
            spawnProtection = ProtectionSeconds;
        }

        private void ClearTransientState()
        {
            speed = stun = cooldown = respawn = shieldSeconds = spawnProtection = CherrySeconds = SlipSeconds = spinSpeed = 0;
            hitSpinRate = hitSpinSeconds = 0;
            flightLeft = flightSeconds = flightHeight = flightBase = 0;
            item = ArenaItem.None;
            FinalPlace = 0;
            SlideVelocity = spinDirection = Vector2.zero;
        }

        // Damage and both exit paths own their cleanup; callers only handle match-wide effects.
        public bool TakeDamage(float damage, VehicleLifecycleRules rules)
        {
            if (!alive || eliminated || damage <= 0 || shieldSeconds > 0 || spawnProtection > 0) return false;
            health = Mathf.Max(0, health - damage);
            if (health <= 0)
            {
                score = Mathf.Max(0, score - rules.wreckPenalty);
                ClearTransientState();
                alive = false;
                respawn = rules.respawnSeconds;
                StartSpinOut(rules);
            }
            return true;
        }

        public void Eliminate(int place, VehicleLifecycleRules rules)
        {
            if (eliminated) return;
            ClearTransientState();
            FinalPlace = place;
            eliminated = true;
            alive = false;
            StartSpinOut(rules);
        }

        // A paid continue: an eliminated car is back in the match with at least floorScore points; the caller respawns it.
        public void Rejoin(float floorScore)
        {
            if (!eliminated) return;
            eliminated = false;
            FinalPlace = 0;
            score = Mathf.Max(score, floorScore);
        }

        public void GiveCherry(float seconds)
        {
            if (!alive || eliminated) return;
            CherrySeconds = Mathf.Max(0, seconds);
            item = ArenaItem.None;
            cooldown = .35f;
        }

        public void AdvanceTimers(float dt)
        {
            if (eliminated) return;
            if (!alive) { respawn -= dt; return; }
            shieldSeconds = Mathf.Max(0, shieldSeconds - dt);
            spawnProtection = Mathf.Max(0, spawnProtection - dt);
            CherrySeconds = Mathf.Max(0, CherrySeconds - dt);
            flightLeft = Mathf.Max(0, flightLeft - dt);
        }

        public void Launch(float seconds, float height, float takeoffHeight = 0)
        {
            if (!alive || Airborne || seconds <= 0) return;
            flightLeft = flightSeconds = seconds;
            flightHeight = height;
            flightBase = takeoffHeight;
        }

        public void RecordCollision() => Collisions++;

        public void Slip(float seconds)
        {
            if (!alive) return;
            SlipSeconds = seconds;
            speed = 0;
        }

        // Knocked side-on: turns `degrees` on the spot (negative turns clockwise) over `seconds`, stunned meanwhile.
        public void SpinFromHit(float degrees, float seconds)
        {
            if (!alive || seconds <= 0) return;
            hitSpinRate = degrees / seconds;
            hitSpinSeconds = seconds;
            stun = Mathf.Max(stun, seconds);
        }

        public void ApplySlide(Vector2 impulse)
        {
            if (alive) SlideVelocity += impulse;
        }

        public void AdvanceSlide(float dt, float friction)
        {
            if (!alive) return;
            position += SlideVelocity * dt;
            SlideVelocity *= Mathf.Exp(-friction * dt);
        }

        public void ReboundSlide(bool hitX, bool hitY)
        {
            SlideVelocity = new Vector2(SlideVelocity.x * (hitX ? -.3f : 1), SlideVelocity.y * (hitY ? -.3f : 1));
        }

        private void StartSpinOut(VehicleLifecycleRules rules)
        {
            Vector2 away = position - rules.arenaCentre;
            if (away.sqrMagnitude < .01f) away = Forward;
            spinDirection = away.normalized;
            spinSpeed = rules.spinStartSpeed;
        }

        public void AdvanceSpinOut(float dt, VehicleLifecycleRules rules, Vector2 viewCentre, float viewHalfSize)
        {
            if (!IsSpinningOut) return;
            spinSpeed += rules.spinAcceleration * dt;
            position += spinDirection * spinSpeed * dt;
            heading += rules.spinTurnsPerSecond * 360 * dt;
            if (Mathf.Abs(position.x - viewCentre.x) > viewHalfSize || Mathf.Abs(position.y - viewCentre.y) > viewHalfSize)
                spinSpeed = 0;
        }

        public void Drive(float dt, ArcadeInput input, float maxSpeed, float acceleration, float turnScale = 1)
        {
            stun = Mathf.Max(0, stun - dt);
            cooldown = Mathf.Max(0, cooldown - dt);
            if (SlipSeconds > 0)
            {
                // One full turn over the default 0.8 s slip, standing still and ignoring the controls.
                SlipSeconds = Mathf.Max(0, SlipSeconds - dt);
                speed = 0;
                heading += 450 * dt;
                return;
            }
            if (hitSpinSeconds > 0)
            {
                float step = Mathf.Min(dt, hitSpinSeconds);
                heading += hitSpinRate * step;
                hitSpinSeconds -= step;
            }
            if (stun > 0)
            {
                speed = Mathf.MoveTowards(speed, 0, dt * 7);
                heading -= input.steer * 155 * turnScale * dt;
                return;
            }
            // No gas means coasting to a stop.
            float target = input.throttle > .1f ? maxSpeed * Mathf.Clamp01(input.throttle) : 0;
            if (input.brake)
            {
                // Brakes to zero first; continuing to hold then backs up at 1/2.8 of top speed.
                if (speed <= 0) target = -maxSpeed / 2.8f;
                else { target = 0; acceleration = 8; }
            }
            speed = Mathf.MoveTowards(speed, target, dt * acceleration);
            heading -= input.steer * 155 * turnScale * dt * Mathf.Clamp(Mathf.Abs(speed), .9f, 1.4f) * (speed < 0 ? -1 : 1);
            position += Forward * speed * dt;
        }

        // Mirrors the car onto its sprite (the name tag is a screen-space label the scene adapter places); only the
        // scene adapter calls this.
        public void Sync()
        {
            transform.position = ArcadeGame.PixelPosition(position);
            body.transform.localRotation = Quaternion.Euler(0, 0, heading - 90);
        }
    }
}
