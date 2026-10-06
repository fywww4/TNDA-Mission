using UnityEngine;

namespace TNDA.Prototype
{
    public class BossClimbGame : ArcadeGame
    {
        [Header("Scene actors")]
        public Transform playerVisual;
        public SpriteRenderer boss;
        public ArcadeProjectile[] shots, hazards;
        public AudioClip shootClip;
        [Header("Tuning")]
        public float moveSpeed = 2.5f;
        public float[] floors = { -4.25f, -1.8f, .65f, 2.7f };
        public float[] ladderX = { -3, 3, -3 };

        public Vector2 PlayerPosition { get; private set; }
        public int PlayerHealth { get; private set; }
        public int BossHealth { get; private set; }
        public int BossMaxHealth { get; private set; }
        public int Kills { get; private set; }
        public float ContinueSeconds { get; private set; }
        public float Invulnerability { get; private set; }
        private Vector2 velocity, bossPosition;
        private float shotCooldown, attackClock, nextBoss, previousVerticalInput;
        private bool climbing, grounded;
        protected override bool SupportsTwoPlayers => false;

        protected override void ResetMatch()
        {
            PlayerPosition = new Vector2(-3.5f, floors[0] + .36f);
            velocity = Vector2.zero;
            PlayerHealth = 3; Kills = 0; BossHealth = BossMaxHealth = 8;
            shotCooldown = nextBoss = ContinueSeconds = previousVerticalInput = 0;
            attackClock = 1.2f; Invulnerability = 1.5f; climbing = false; grounded = true;
            playerVisual.gameObject.SetActive(true);
            bossPosition = new Vector2(0, 3.23f);
            boss.gameObject.SetActive(true);
            boss.transform.position = PixelPosition(bossPosition);
            foreach (var shot in shots) shot.Hide();
            foreach (var hazard in hazards) hazard.Hide();
            playerVisual.position = PixelPosition(PlayerPosition);
        }

        protected override void Tick(float dt, ArcadeInput input, ArcadeInput unused)
        {
            Invulnerability = Mathf.Max(0, Invulnerability - dt);
            shotCooldown = Mathf.Max(0, shotCooldown - dt);
            MovePlayer(dt, input);
            if (input.action && shotCooldown <= 0) Fire();
            if (nextBoss > 0)
            {
                nextBoss -= dt;
                if (nextBoss <= 0)
                {
                    BossHealth = BossMaxHealth = 8 + Kills * 2;
                    boss.gameObject.SetActive(true);
                    attackClock = 1;
                    Announce("BOSS " + (Kills + 1) + " // FASTER ATTACKS", Yellow);
                }
            }
            else
            {
                bossPosition = new Vector2(Mathf.Sin(Elapsed * (.8f + Kills * .12f)) * 2.6f, 3.23f);
                boss.transform.position = PixelPosition(bossPosition);
                attackClock -= dt;
                if (attackClock <= 0)
                {
                    ThrowHazard();
                    attackClock = Mathf.Max(.48f, 1.55f - Kills * .12f);
                }
            }

            foreach (var shot in shots)
            {
                if (!shot.active) continue;
                shot.position += shot.velocity * dt;
                if (shot.position.y > 4.1f) shot.Hide();
                else if (nextBoss <= 0 && Vector2.Distance(shot.position, bossPosition) < .6f)
                {
                    shot.Hide(); BossHealth--; PlayHit(.35f);
                    if (BossHealth <= 0) DefeatBoss();
                }
                if (shot.active) shot.Sync();
            }

            foreach (var hazard in hazards)
            {
                if (!hazard.active) continue;
                hazard.age += dt;
                float previousBottom = hazard.position.y - .14f;
                if (hazard.owner >= 0)
                {
                    int level = hazard.owner;
                    hazard.position.x += hazard.velocity.x * dt;
                    if (!OnPlatform(hazard.position.x, level)) { hazard.owner = -1; hazard.velocity.y = -.2f; }
                }
                else
                {
                    hazard.velocity.y -= 5.5f * dt;
                    hazard.position += hazard.velocity * dt;
                    for (int level = floors.Length - 1; level >= 0; level--)
                    {
                        if (previousBottom >= floors[level] && hazard.position.y - .14f <= floors[level]
                            && OnPlatform(hazard.position.x, level))
                        {
                            hazard.position.y = floors[level] + .14f;
                            hazard.owner = level;
                            hazard.velocity = new Vector2((level % 2 == 0 ? -1 : 1) * (1.6f + Kills * .12f), 0);
                            break;
                        }
                    }
                }
                if (Mathf.Abs(hazard.position.x) > 4.65f)
                {
                    hazard.position.x = Mathf.Clamp(hazard.position.x, -4.65f, 4.65f);
                    hazard.velocity.x *= -1;
                }
                if (Mathf.Abs(hazard.position.x - PlayerPosition.x) < .36f
                    && Mathf.Abs(hazard.position.y - PlayerPosition.y) < .46f) DamagePlayer();
                if (hazard.age > 10 || hazard.position.y < -4.7f) hazard.Hide();
                if (hazard.active) hazard.Sync();
            }
            SyncPlayer();
        }

        private void MovePlayer(float dt, ArcadeInput input)
        {
            bool nearLadder = false;
            for (int i = 0; i < ladderX.Length; i++)
            {
                if (Mathf.Abs(PlayerPosition.x - ladderX[i]) < .38f
                    && PlayerPosition.y >= floors[i] + .18f && PlayerPosition.y <= floors[i + 1] + .48f)
                {
                    nearLadder = true;
                    if (Mathf.Abs(input.throttle) > .1f) climbing = true;
                }
            }
            if (!nearLadder) climbing = false;
            Vector2 previous = PlayerPosition;
            PlayerPosition += Vector2.right * input.steer * moveSpeed * dt;
            if (climbing)
            {
                velocity.y = 0;
                PlayerPosition += Vector2.up * input.throttle * 2.3f * dt;
            }
            else
            {
                bool jump = input.jumpPressed || input.throttle > .5f && previousVerticalInput <= .5f;
                if (jump && grounded) { velocity.y = 4.8f; grounded = false; Sound(2); }
                velocity.y -= 11 * dt;
                PlayerPosition += Vector2.up * velocity.y * dt;
            }
            grounded = false;
            if (!climbing && velocity.y <= 0)
            {
                for (int level = floors.Length - 1; level >= 0; level--)
                    if (previous.y - .36f >= floors[level] - .02f && PlayerPosition.y - .36f <= floors[level]
                        && OnPlatform(PlayerPosition.x, level))
                    {
                        PlayerPosition = new Vector2(PlayerPosition.x, floors[level] + .36f);
                        velocity.y = 0; grounded = true; break;
                    }
            }
            PlayerPosition = new Vector2(Mathf.Clamp(PlayerPosition.x, -4.6f, 4.6f), Mathf.Clamp(PlayerPosition.y, floors[0] + .36f, 3.65f));
            previousVerticalInput = input.throttle;
        }

        private bool OnPlatform(float x, int level)
        {
            if (level == 0) return Mathf.Abs(x) <= 4.7f;
            return level % 2 == 1 ? x >= -4.7f && x <= 3.8f : x >= -3.8f && x <= 4.7f;
        }

        public void Fire()
        {
            if (Phase != ArcadePhase.Playing) return;
            foreach (var shot in shots) if (!shot.active)
            {
                shot.Spawn(PlayerPosition + Vector2.up * .4f, Vector2.up * 6.8f);
                shotCooldown = .22f; PlayClip(shootClip, 0, .45f); break;
            }
        }

        private void ThrowHazard()
        {
            foreach (var hazard in hazards) if (!hazard.active)
            {
                float horizontal = Mathf.Clamp((PlayerPosition.x - bossPosition.x) * .5f, -2.3f, 2.3f);
                hazard.Spawn(bossPosition + Vector2.down * .4f, new Vector2(horizontal, -.3f));
                break;
            }
        }

        public void DefeatBoss()
        {
            Kills++; BossHealth = 0;
            boss.gameObject.SetActive(false);
            foreach (var hazard in hazards) hazard.Hide();
            foreach (var shot in shots) shot.Hide();
            nextBoss = 1.2f;
            Announce("BOSS DOWN!  NEXT WAVE", Yellow); Sound(3);
        }

        public void DamagePlayer()
        {
            if (Phase != ArcadePhase.Playing || Invulnerability > 0) return;
            PlayerHealth--; Invulnerability = 1.3f; PlayHit(.8f);
            Announce("HIT!  KEEP MOVING", Yellow);
            if (PlayerHealth <= 0)
            {
                Phase = ArcadePhase.Continue; ContinueSeconds = 10;
                ShowOverlay("CONTINUE?", "BOSSES CLEARED " + Kills + "\nBOSS HP " + BossHealth + " / " + BossMaxHealth
                    + "\n\nKEEP YOUR PROGRESS. REVIVE HERE.", "C / SPACE INSERT COIN");
                playerVisual.gameObject.SetActive(false);
            }
        }

        public override void ContinueRun()
        {
            if (Phase != ArcadePhase.Continue) return;
            PlayerHealth = 3; Invulnerability = 2; Phase = ArcadePhase.Playing;
            overlay.SetActive(false); playerVisual.gameObject.SetActive(true);
            Announce("REVIVED // 2s INVINCIBLE", Cyan); Sound(2);
        }

        protected override void TickIdle(float dt)
        {
            if (Phase != ArcadePhase.Continue) return;
            ContinueSeconds = Mathf.Max(0, ContinueSeconds - dt);
            overlayHint.text = "C / SPACE REVIVE  //  " + Mathf.CeilToInt(ContinueSeconds) + "s";
            if (ContinueSeconds <= 0)
            {
                int best = Mathf.Max(Kills, PlayerPrefs.GetInt("BossBest", 0));
                PlayerPrefs.SetInt("BossBest", best);
                Finish("GAME OVER", "BOSSES CLEARED " + Kills + "\nBEST " + best + "\n\nTRY ONE MORE COIN.");
            }
        }

        private void SyncPlayer()
        {
            playerVisual.position = PixelPosition(PlayerPosition);
            playerVisual.gameObject.SetActive(PlayerHealth > 0 && (Phase != ArcadePhase.Playing
                || Invulnerability <= 0 || ((int)(Invulnerability * 10) % 2 == 0)));
        }

        protected override void RefreshHud()
        {
            if (Phase == ArcadePhase.Attract)
                overlayHint.text = "C / SPACE INSERT COIN  //  1 SINGLE PLAYER";
            header.text = "01 / BOSS CLIMB";
            hud.text = "HP " + new string('|', Mathf.Max(0, PlayerHealth)) + new string('.', 3 - Mathf.Clamp(PlayerHealth, 0, 3))
                + "    KILLS " + Kills.ToString("00") + "    BOSS " + BossHealth + "/" + BossMaxHealth;
            footer.text = "WASD MOVE  W JUMP/CLIMB  SPACE FIRE";
        }
    }
}
