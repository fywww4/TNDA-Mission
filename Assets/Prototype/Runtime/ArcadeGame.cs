using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TNDA.Prototype
{
    public enum ArcadePhase { Attract, Playing, Results, Countdown, Garage }
    // What the title screen offers: the normal 1P/2P elimination match, or single-player challenges.
    public enum CabinetMode { Normal, Challenge }

    public struct ArcadeInput
    {
        public float steer, throttle;
        public bool action, actionPressed;
        public bool resetSaveHeld;
        public bool shiftUpPressed, shiftDownPressed;
        // This seat's START (P1: Space or the first pad's Start; P2: Numpad Enter or the second pad's Start).
        public bool startPressed;
    }

    // The console shell around GAME3: title (demo) -> START, pick 1P/2P -> 3-2-1 countdown -> match -> results, which
    // wait for START (play again) or Esc (title); player input, the notice line and sounds. The game fills in three things: resetting the match,
    // one frame of simulation, and the HUD.
    public abstract class ArcadeGame : MonoBehaviour
    {
        public static readonly Color White = new Color32(245, 245, 245, 255);
        public static readonly Color Cyan = new Color32(41, 199, 255, 255);
        public static readonly Color Yellow = new Color32(255, 212, 72, 255);

        [Header("Arcade cabinet")]
        public Camera sceneCamera;
        public TMP_Text header, hud, footer, notice;
        public GameObject overlay;
        public TMP_Text overlayTitle, overlayBody, overlayHint;
        public UnityEngine.UI.Button onePlayerButton, twoPlayerButton;
        public AudioSource speaker;
        public string cabinetTitle;
        [TextArea] public string instructions;

        [Header("Start countdown and recorded sounds (optional)")]
        [Tooltip("0 starts play immediately; otherwise a 3-2-1-GO countdown of this many seconds runs first.")]
        public float countdownSeconds = 0;
        public TMP_Text countdownLabel;
        public AudioClip countdownClip, hitClip;

        [Header("Volume")]
        [Tooltip("Master volume for every game sound (countdown, hits, generated beeps and engine loops). Can be changed during Play Mode.")]
        [Range(0, 1)] public float masterVolume = .5f;

        public ArcadePhase Phase { get; private set; }
        public int PlayerCount { get; private set; } = 1;
        public float Elapsed { get; protected set; }
        public float CountdownLeft { get; private set; }
        // The title's player-count menu (normal mode): START opens it, left/right pick 1P or 2P, START again starts.
        public bool ChoosingPlayers { get; private set; }
        public int PlayerChoice { get; private set; } = 1;
        public const string TransmissionSaveKey = "TNDA.ManualTransmission";
        public bool ManualTransmission => PlayerPrefs.GetInt(TransmissionSaveKey, 0) == 1;
        public void SelectTransmission(bool manual)
        {
            PlayerPrefs.SetInt(TransmissionSaveKey, manual ? 1 : 0);
            PlayerPrefs.Save();
        }
        // Title screen selection. ActiveChallenge is the challenge being played or just finished (null in normal mode,
        // on the title and in the demo).
        public CabinetMode Mode { get; private set; }
        public int ChallengeCursor { get; private set; }
        public Challenge ActiveChallenge { get; private set; }
        public Challenge SelectedChallenge => Challenges.All[ChallengeCursor];
        public GarageScreen garage;
        private VehicleUpgrades upgrades;
        public VehicleUpgrades Upgrades => upgrades ?? (upgrades = new VehicleUpgrades());
        public int GarageCursor { get; private set; }
        public string GarageMessage { get; private set; } = "";
        private ChallengeRecords records;
        public ChallengeRecords Records => records ?? (records = new ChallengeRecords());
        // Saved reward gold, the game's only currency; it pays for challenge unlocks and upgrades.
        private GoldWallet wallet;
        protected GoldWallet Wallet => wallet ?? (wallet = new GoldWallet());
        public int Gold => Wallet.Total;
        private float noticeAge, goFlash;
        [NonSerialized] private AudioClip[] sounds; // generated in Awake; a reload must not turn it into an empty array

        private void Awake()
        {
            // The game never uses slow motion; guard against a time scale left over from editor tooling.
            Time.timeScale = 1;
            AudioListener.volume = masterVolume;
            MakeSounds();
            onePlayerButton.onClick.AddListener(() => StartRun(1));
            twoPlayerButton.onClick.AddListener(() => StartRun(2));
            PreparePreview();
        }

        public void PreparePreview()
        {
            PlayerCount = 1;
            Elapsed = 0;
            ActiveChallenge = null;
            ResetMatch();
            ShowAttract();
            RefreshHud();
        }

        private void Update()
        {
            AudioListener.volume = masterVolume;
            bool padBack = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            // Esc backs out from anywhere (a match in progress included); the pad's B only on menus and results.
            if (Pressed(Key.Escape) || padBack && Phase != ArcadePhase.Playing && Phase != ArcadePhase.Countdown) Back();
            else if (Phase == ArcadePhase.Attract) NavigateTitle();
            else if (Phase == ArcadePhase.Garage) NavigateGarage();
            if (Phase == ArcadePhase.Attract || Phase == ArcadePhase.Results)
            {
                bool start = Pressed(Key.Space);
                foreach (var pad in Gamepad.all) start |= pad.startButton.wasPressedThisFrame;
                if (start) PressStart();
            }
            Advance(Mathf.Min(Time.deltaTime, .05f), ReadInput(0), ReadInput(1));
        }

        // The same simulation entry point is used by live input and CLI verification.
        public void Advance(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            noticeAge = Mathf.Max(0, noticeAge - dt);
            if (noticeAge == 0) notice.text = "";
            if (goFlash > 0 && (goFlash -= dt) <= 0 && countdownLabel != null) countdownLabel.text = "";
            var startPhase = Phase;
            if (Phase == ArcadePhase.Playing) Elapsed += dt;
            else if (Phase == ArcadePhase.Countdown)
            {
                CountdownLeft -= dt;
                if (CountdownLeft <= 0)
                {
                    Phase = ArcadePhase.Playing;
                    SetCountdownLabel(CabinetText.Go);
                    goFlash = .7f;
                    Announce(CabinetText.Go, Yellow);
                }
                else SetCountdownLabel(Mathf.CeilToInt(CountdownLeft).ToString());
            }
            Tick(dt, startPhase, p1, p2);
            RefreshHud();
            if (garage != null) garage.Render(this, dt);
        }

        // START on the title or results. Title, normal mode: the first press opens the 1P/2P menu, the next starts the
        // chosen count. Title, challenge mode: starts the selected challenge, or buys it while locked. Results: the
        // same game again (same player count, or a retry of the same challenge).
        public void PressStart()
        {
            if (Phase == ArcadePhase.Results)
            {
                if (ActiveChallenge != null) TryStartChallenge(ActiveChallenge);
                else StartRun(PlayerCount);
                return;
            }
            if (Phase != ArcadePhase.Attract) return;
            if (Mode == CabinetMode.Challenge)
            {
                if (!Records.IsUnlocked(SelectedChallenge)) TryUnlockChallenge(SelectedChallenge);
                else TryStartChallenge(SelectedChallenge);
            }
            else if (!ChoosingPlayers)
            {
                ChoosingPlayers = true;
                PlayerChoice = 1;
                Sound(0);
            }
            else StartRun(PlayerChoice);
        }

        // Esc (or the pad's B): closes the player menu, leaves the garage, or ends a match or its results for the
        // title (a challenge comes back to its list, as the title keeps challenge mode selected).
        public void Back()
        {
            if (Phase == ArcadePhase.Attract) ChoosingPlayers = false;
            else if (Phase == ArcadePhase.Garage) ShowAttract();
            else PreparePreview();
        }

        public void PickPlayers(int players) => PlayerChoice = Mathf.Clamp(players, 1, 2);

        // Title screen: left/right switch between normal and challenge mode, up/down pick a challenge; while the
        // player menu is open, left/right pick 1P or 2P instead.
        private void NavigateTitle()
        {
            bool sideways = PressedAny(Key.LeftArrow, Key.RightArrow, Key.A, Key.D, Key.Tab) || DpadPressed(pad => pad.dpad.left, pad => pad.dpad.right);
            if (ChoosingPlayers)
            {
                if (sideways) PickPlayers(3 - PlayerChoice);
                return;
            }
            if (Pressed(Key.G) || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame))
            {
                OpenGarage();
                return;
            }
            if (Pressed(Key.T) || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame))
                SelectTransmission(!ManualTransmission);
            if (sideways) Mode = Mode == CabinetMode.Normal ? CabinetMode.Challenge : CabinetMode.Normal;
            if (Mode != CabinetMode.Challenge) return;
            if (PressedAny(Key.UpArrow, Key.W) || DpadPressed(pad => pad.dpad.up)) MoveChallengeCursor(-1);
            if (PressedAny(Key.DownArrow, Key.S) || DpadPressed(pad => pad.dpad.down)) MoveChallengeCursor(1);
        }

        public void SelectMode(CabinetMode mode) => Mode = mode;

        public void OpenGarage()
        {
            if (Phase != ArcadePhase.Attract || garage == null) return;
            Phase = ArcadePhase.Garage;
            GarageMessage = "";
            overlay.SetActive(false);
            RefreshHud();
            garage.Render(this, 0);
        }

        public void MoveGarageCursor(int delta)
        {
            GarageCursor = ((GarageCursor + delta) % 4 + 4) % 4;
            GarageMessage = "";
        }

        public bool TryBuySelectedUpgrade()
        {
            if (Phase != ArcadePhase.Garage) return false;
            var ability = (VehicleAbility)GarageCursor;
            bool bought = Upgrades.TryBuy(ability, Wallet);
            GarageMessage = bought ? CabinetText.GarageBought
                : Upgrades.Level(ability) == VehicleUpgrades.MaxLevel ? CabinetText.GarageMaxed : CabinetText.GarageGoldShort;
            if (bought) Sound(2);
            garage.Render(this, 0);
            return bought;
        }

        private void NavigateGarage()
        {
            if (PressedAny(Key.UpArrow, Key.W) || DpadPressed(pad => pad.dpad.up)) MoveGarageCursor(-1);
            if (PressedAny(Key.DownArrow, Key.S) || DpadPressed(pad => pad.dpad.down)) MoveGarageCursor(1);
            if (Pressed(Key.LeftCtrl) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) TryBuySelectedUpgrade();
        }

        public void MoveChallengeCursor(int delta)
        {
            int count = Challenges.All.Count;
            ChallengeCursor = ((ChallengeCursor + delta) % count + count) % count;
        }

        // A free single-player start of a challenge; false (nothing happens) while it is still locked.
        public bool TryStartChallenge(Challenge challenge)
        {
            if (challenge == null || !challenge.Playable || !Records.IsUnlocked(challenge)) return false;
            StartRun(1, challenge);
            return true;
        }

        // Pays the challenge's price in gold to open it for good; with too little gold it stays locked and says so.
        public bool TryUnlockChallenge(Challenge challenge)
        {
            if (challenge == null) return false;
            if (!challenge.Playable)
            {
                Announce(CabinetText.ChallengeComingSoon, Yellow, 2);
                return false;
            }
            if (!Records.TryUnlock(challenge, Wallet))
            {
                Announce(string.Format(CabinetText.ChallengeGoldShort, challenge.UnlockPrice, Wallet.Total), Yellow, 2);
                return false;
            }
            Sound(2);
            Announce(string.Format(CabinetText.ChallengeBought, challenge.Name), Yellow, 2);
            return true;
        }

        // Starts a normal match for 1 or 2 players, or (with a challenge) that single-player challenge.
        public void StartRun(int players = 1, Challenge challenge = null)
        {
            ActiveChallenge = challenge;
            PlayerCount = challenge != null ? 1 : Mathf.Clamp(players, 1, 2);
            Elapsed = 0;
            ChoosingPlayers = false;
            bool countdown = countdownSeconds > 0;
            Phase = countdown ? ArcadePhase.Countdown : ArcadePhase.Playing;
            if (garage != null) garage.gameObject.SetActive(false);
            if (speaker != null) speaker.Stop();
            ResetMatch();
            overlay.SetActive(false);
            if (countdown)
            {
                // The world is reset and visible but frozen until GO; Elapsed only starts counting after it.
                CountdownLeft = countdownSeconds;
                goFlash = 0;
                SetCountdownLabel(Mathf.CeilToInt(CountdownLeft).ToString());
                PlayClip(countdownClip, 2, .8f);
            }
            else
            {
                Announce(CabinetText.Go, Yellow);
                Sound(2);
            }
            RefreshHud();
        }

        private void SetCountdownLabel(string text)
        {
            if (countdownLabel != null) countdownLabel.text = text;
            else Announce(text, Yellow, 1);
        }

        public void ShowAttract()
        {
            if (Phase == ArcadePhase.Countdown && speaker != null) speaker.Stop();
            goFlash = 0;
            if (countdownLabel != null) countdownLabel.text = "";
            ActiveChallenge = null;
            ChoosingPlayers = false;
            Phase = ArcadePhase.Attract;
            if (garage != null) garage.gameObject.SetActive(false);
            ShowOverlay(cabinetTitle, instructions, CabinetText.AttractHint);
        }

        private void ShowOverlay(string title, string body, string hint)
        {
            overlay.SetActive(true);
            overlayTitle.text = title;
            overlayBody.text = body;
            overlayHint.text = hint;
            // A console game: START replays and Esc leaves, so the old mouse buttons stay hidden.
            onePlayerButton.gameObject.SetActive(false);
            twoPlayerButton.gameObject.SetActive(false);
        }

        protected void Finish(string title, string body, string hint = null)
        {
            Phase = ArcadePhase.Results;
            ShowOverlay(title, body, hint ?? CabinetText.ResultsHint);
            Sound(3);
        }

        protected void Announce(string text, Color color, float duration = 2)
        {
            notice.text = text;
            notice.color = color;
            noticeAge = duration;
        }

        // Puts the cars and arena back for a new match (or the title demo); Phase and PlayerCount are already set.
        protected abstract void ResetMatch();
        // One frame of the game, after the cabinet's own phase step (Elapsed already advanced while playing). startPhase
        // is the phase the frame began in, so the frame that turns the countdown into GO, or the results back into the
        // title, is not yet simulated as the new phase; Phase is already the new one.
        protected abstract void Tick(float dt, ArcadePhase startPhase, ArcadeInput p1, ArcadeInput p2);
        protected abstract void RefreshHud();

        private static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        private static bool PressedAny(params Key[] keys)
        {
            foreach (var key in keys) if (Pressed(key)) return true;
            return false;
        }

        private static bool DpadPressed(params System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl>[] directions)
        {
            if (Gamepad.current == null) return false;
            foreach (var direction in directions) if (direction(Gamepad.current).wasPressedThisFrame) return true;
            return false;
        }

        private static ArcadeInput ReadInput(int index)
        {
            // P1: arrows (Up = gas) + Left Ctrl item. P2: A/D/W + Numpad Enter item.
            var k = Keyboard.current;
            Key left = index == 0 ? Key.LeftArrow : Key.A;
            Key right = index == 0 ? Key.RightArrow : Key.D;
            Key gas = index == 0 ? Key.UpArrow : Key.W;
            Key button = index == 0 ? Key.LeftCtrl : Key.NumpadEnter;
            var input = new ArcadeInput();
            if (k != null)
            {
                input.steer = (k[right].isPressed ? 1 : 0) - (k[left].isPressed ? 1 : 0);
                input.throttle = k[gas].isPressed ? 1 : 0;
                input.action = k[button].isPressed;
                input.actionPressed = k[button].wasPressedThisFrame;
                input.resetSaveHeld = index == 0 && k.deleteKey.isPressed;
                input.shiftDownPressed = k[index == 0 ? Key.DownArrow : Key.S].wasPressedThisFrame;
                input.shiftUpPressed = k[index == 0 ? Key.RightShift : Key.LeftShift].wasPressedThisFrame;
                input.startPressed = k[index == 0 ? Key.Space : Key.NumpadEnter].wasPressedThisFrame;
            }
            if (Gamepad.all.Count > index)
            {
                // Stick or d-pad up is the gas; the South button uses the item.
                var pad = Gamepad.all[index];
                Vector2 stick = pad.leftStick.ReadValue();
                if (pad.dpad.ReadValue().sqrMagnitude > .01f) stick = pad.dpad.ReadValue();
                if (stick.sqrMagnitude > .03f) input.steer = stick.x;
                if (stick.y > .4f) input.throttle = 1;
                input.action |= pad.buttonSouth.isPressed;
                input.actionPressed |= pad.buttonSouth.wasPressedThisFrame;
                input.shiftDownPressed |= pad.leftShoulder.wasPressedThisFrame;
                input.shiftUpPressed |= pad.rightShoulder.wasPressedThisFrame;
                input.startPressed |= pad.startButton.wasPressedThisFrame;
            }
            return input;
        }

        protected void Sound(int kind)
        {
            if (sounds != null && speaker != null) speaker.PlayOneShot(sounds[kind], .12f);
        }

        // Plays a recorded clip when one is assigned, otherwise the matching generated sound.
        private void PlayClip(AudioClip clip, int fallback, float volume = .6f)
        {
            if (clip != null && speaker != null) speaker.PlayOneShot(clip, volume);
            else Sound(fallback);
        }

        protected void PlayHit(float volume = .6f) => PlayClip(hitClip, 1, volume);

        private void MakeSounds()
        {
            if (speaker == null) speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0;
            sounds = new AudioClip[4];
            for (int kind = 0; kind < 4; kind++)
            {
                float seconds = kind == 3 ? .35f : .09f + kind * .035f;
                var data = new float[Mathf.CeilToInt(22050 * seconds)];
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / 22050f, frequency = kind == 0 ? 880 : kind == 1 ? 110 : kind == 2 ? 660 : 440 + t * 1600;
                    float wave = Mathf.Sin(t * frequency * Mathf.PI * 2);
                    data[i] = (kind == 1 ? wave : Mathf.Sign(wave)) * (1 - i / (float)data.Length) * .4f;
                }
                sounds[kind] = AudioClip.Create("Electronic " + kind, data.Length, 1, 22050, false);
                sounds[kind].SetData(data, 0);
            }
        }

        protected virtual void OnDestroy()
        {
            if (sounds != null) foreach (var clip in sounds) Destroy(clip);
        }

        public static Vector3 PixelPosition(Vector2 position) => new Vector3(
            Mathf.Round(position.x * 32) / 32, Mathf.Round(position.y * 32) / 32, 0);
    }
}
