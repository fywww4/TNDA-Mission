using TMPro;
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
        public TMP_Text tagLabel;
        public int index;
        public Vector2 position;
        public float heading, speed, health, score, stun, cooldown;
        public bool alive, computer, eliminated;
        public float shieldSeconds, spawnProtection;
        public ArenaItem item;
        public float respawn;

        public float CherrySeconds { get; private set; }
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
        private const float ProtectionSeconds = 2;

        public string DriverName => index == 0 ? "P1" : index == 1 && !computer ? "P2" : string.Format(CabinetText.ComputerName, index);
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
            speed = stun = cooldown = respawn = shieldSeconds = spawnProtection = CherrySeconds = spinSpeed = 0;
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
        }

        public void RecordCollision() => Collisions++;

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

        public void Drive(float dt, ArcadeInput input, float maxSpeed, float acceleration)
        {
            stun = Mathf.Max(0, stun - dt);
            cooldown = Mathf.Max(0, cooldown - dt);
            if (stun > 0)
            {
                speed = Mathf.MoveTowards(speed, 0, dt * 7);
                heading -= input.steer * 155 * dt;
                return;
            }
            // No gas means coasting to a stop: there is no reverse and no automatic forward drive.
            float target = input.throttle > .1f ? maxSpeed * Mathf.Clamp01(input.throttle) : 0;
            speed = Mathf.MoveTowards(speed, target, dt * acceleration);
            heading -= input.steer * 155 * dt * Mathf.Clamp(Mathf.Abs(speed), .9f, 1.4f) * (speed < 0 ? -1 : 1);
            position += Forward * speed * dt;
        }

        // Mirrors the car onto its sprite and name tag; only the scene adapter calls this.
        public void Sync()
        {
            transform.position = ArcadeGame.PixelPosition(position);
            body.transform.localRotation = Quaternion.Euler(0, 0, heading - 90);
            if (tagLabel != null)
            {
                tagLabel.text = DriverName;
                tagLabel.color = body.color;
                tagLabel.transform.position = ArcadeGame.PixelPosition(position + Vector2.up * .52f);
            }
        }
    }
}
