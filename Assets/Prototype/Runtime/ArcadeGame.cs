using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TNDA.Prototype
{
    public enum ArcadePhase { Attract, Playing, Results, Countdown }

    public struct ArcadeInput
    {
        public float steer, throttle;
        public bool action, actionPressed;
    }

    // The cabinet around GAME3: title (attract) -> 3-2-1 countdown -> match -> results -> back to title, coin and
    // restart keys, player input, the notice line and sounds. The game fills in three things: resetting the match,
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
        public float presentationSeconds = 0;

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
        // Coins inserted and not yet spent. A 1P game costs one coin, a 2P game two, a continue one.
        public int Credits { get; private set; }
        public const int MaxCredits = 9;
        private float resultAge, noticeAge, goFlash;
        [NonSerialized] private AudioClip[] sounds; // generated in Awake; a reload must not turn it into an empty array

        private void Awake()
        {
            // The game never uses slow motion; guard against a time scale left over from editor tooling.
            Time.timeScale = 1;
            AudioListener.volume = masterVolume;
            MakeSounds();
            onePlayerButton.onClick.AddListener(() => TryStartRun(1));
            twoPlayerButton.onClick.AddListener(() => TryStartRun(2));
            PreparePreview();
        }

        public void PreparePreview()
        {
            PlayerCount = 1;
            Elapsed = 0;
            ResetMatch();
            ShowAttract();
            RefreshHud();
        }

        private void Update()
        {
            AudioListener.volume = masterVolume;
            if (Pressed(Key.Escape)) ShowAttract();
            // 5 is the coin slot (as in arcade emulators); the gamepad's Select inserts a coin too.
            if (Pressed(Key.Digit5) || Pressed(Key.Numpad5) || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame))
                InsertCoin();
            if (Phase == ArcadePhase.Attract || Phase == ArcadePhase.Results)
            {
                if (Pressed(Key.Digit2) || Pressed(Key.NumpadEnter)) TryStartRun(2);
                else if (Pressed(Key.Digit1) || Pressed(Key.LeftCtrl) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))
                    TryStartRun(1);
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
            else if (Phase == ArcadePhase.Results && presentationSeconds > 0)
            {
                resultAge += dt;
                overlayHint.text = string.Format(CabinetText.ReturnToTitle,
                    Mathf.CeilToInt(Mathf.Max(0, presentationSeconds - resultAge)));
                if (resultAge >= presentationSeconds) PreparePreview();
            }
            Tick(dt, startPhase, p1, p2);
            RefreshHud();
        }

        public void InsertCoin()
        {
            Credits = Mathf.Min(Credits + 1, MaxCredits);
            Sound(0);
            Announce(string.Format(CabinetText.CoinInserted, Credits), Yellow, 1.5f);
        }

        // Spends the coins for a continue or a new game; false (nothing spent) when too few are in.
        protected bool SpendCredits(int coins)
        {
            if (Credits < coins) return false;
            Credits -= coins;
            return true;
        }

        // A paid start: one coin per player. Without enough coins nothing happens and the prompt keeps asking.
        public bool TryStartRun(int players)
        {
            players = Mathf.Clamp(players, 1, 2);
            if (!SpendCredits(players))
            {
                Announce(CabinetText.InsertCoinFirst, Yellow, 1.5f);
                return false;
            }
            StartRun(players);
            return true;
        }

        // Starts without coins; only CLI checks use it (every player start goes through TryStartRun).
        public void StartRun(int players = 1)
        {
            PlayerCount = Mathf.Clamp(players, 1, 2);
            Elapsed = resultAge = 0;
            bool countdown = countdownSeconds > 0;
            Phase = countdown ? ArcadePhase.Countdown : ArcadePhase.Playing;
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
            Phase = ArcadePhase.Attract;
            ShowOverlay(cabinetTitle, instructions, CabinetText.AttractHint);
        }

        private void ShowOverlay(string title, string body, string hint)
        {
            overlay.SetActive(true);
            overlayTitle.text = title;
            overlayBody.text = body;
            overlayHint.text = hint;
            onePlayerButton.gameObject.SetActive(true);
            twoPlayerButton.gameObject.SetActive(true);
        }

        protected void Finish(string title, string body)
        {
            Phase = ArcadePhase.Results;
            resultAge = 0;
            ShowOverlay(title, body, CabinetText.ResultsHint);
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
