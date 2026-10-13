using TMPro;
using UnityEngine;

namespace TNDA.Prototype
{
    public class ArcadeVehicle : MonoBehaviour
    {
        public SpriteRenderer body;
        public TMP_Text tagLabel;
        public int index;
        public Vector2 position;
        public float heading, speed, health, score, stun, boost, cooldown;
        public bool alive, computer, eliminated;
        public float shieldSeconds, spawnProtection;
        public int item;
        public float progress, bestProgress, lastAngle, respawn;

        public string DriverName => index == 0 ? "P1" : index == 1 && !computer ? "P2" : "電腦" + index;
        public Vector2 Forward => new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));

        public void ResetVehicle(Vector2 start, float angle, int playerCount)
        {
            position = start;
            heading = angle;
            speed = score = stun = boost = cooldown = progress = bestProgress = respawn = 0;
            item = 0;
            eliminated = false;
            shieldSeconds = spawnProtection = 0;
            health = 1;
            alive = true;
            computer = index >= playerCount;
            gameObject.SetActive(true);
            body.color = index == 0 ? ArcadeGame.Cyan : index == 1 && !computer ? ArcadeGame.Yellow : ArcadeGame.White;
            Sync();
        }

        public void Drive(float dt, ArcadeInput input, float maxSpeed, float acceleration = 3.2f, float idleThrottle = .45f)
        {
            stun = Mathf.Max(0, stun - dt);
            boost = Mathf.Max(0, boost - dt);
            cooldown = Mathf.Max(0, cooldown - dt);
            if (stun > 0)
            {
                speed = Mathf.MoveTowards(speed, 0, dt * 7);
                heading -= input.steer * 155 * dt;
                return;
            }
            float target = input.throttle > .1f ? maxSpeed * Mathf.Clamp01(input.throttle)
                : input.throttle < -.1f ? maxSpeed * Mathf.Clamp(input.throttle, -1, 0) * .4f : maxSpeed * idleThrottle;
            if (boost > 0) target = maxSpeed * 1.65f;
            speed = Mathf.MoveTowards(speed, target, dt * acceleration);
            heading -= input.steer * 155 * dt * Mathf.Clamp(Mathf.Abs(speed), .9f, 1.4f) * (speed < 0 ? -1 : 1);
            position += Forward * speed * dt;
        }

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
