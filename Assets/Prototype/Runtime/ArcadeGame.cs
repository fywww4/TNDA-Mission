using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype
{
    public enum ArcadePhase { Attract, Playing, Continue, Results, Countdown }

    public struct ArcadeInput
    {
        public float steer, throttle;
        public bool action, actionPressed, jumpPressed;
    }

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

        public ArcadePhase Phase { get; protected set; }
        public int PlayerCount { get; private set; } = 1;
        public float Elapsed { get; protected set; }
        public float CountdownLeft { get; private set; }
        protected float resultAge, noticeAge;
        private float goFlash;
        private AudioClip[] sounds;
        protected virtual bool SupportsTwoPlayers => true;

        protected virtual void Awake()
        {
            MakeSounds();
            onePlayerButton.onClick.AddListener(() => MenuAction(1));
            twoPlayerButton.onClick.AddListener(() => MenuAction(2));
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
            if (Pressed(Key.F1)) { LoadCabinet("GAME1"); return; }
            if (Pressed(Key.F2)) { LoadCabinet("GAME2"); return; }
            if (Pressed(Key.F3)) { LoadCabinet("GAME3"); return; }
            if (Pressed(Key.R)) StartRun(PlayerCount);
            if (Pressed(Key.Escape)) ShowAttract();
            bool coin = Pressed(Key.C) || Pressed(Key.Space) || Pressed(Key.Enter)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (Phase == ArcadePhase.Continue && coin) ContinueRun();
            else if (Phase == ArcadePhase.Attract || Phase == ArcadePhase.Results)
            {
                if (Pressed(Key.Digit2) && SupportsTwoPlayers) StartRun(2);
                else if (coin || Pressed(Key.Digit1)) StartRun(1);
            }
            Advance(Mathf.Min(Time.deltaTime, .05f), ReadInput(0), ReadInput(1));
        }

        // The same simulation entry point is used by live input and CLI verification.
        public void Advance(float dt, ArcadeInput p1, ArcadeInput p2)
        {
            noticeAge = Mathf.Max(0, noticeAge - dt);
            if (noticeAge == 0) notice.text = "";
            if (goFlash > 0 && (goFlash -= dt) <= 0 && countdownLabel != null) countdownLabel.text = "";
            if (Phase == ArcadePhase.Playing)
            {
                Elapsed += dt;
                Tick(dt, p1, p2);
            }
            else if (Phase == ArcadePhase.Countdown)
            {
                CountdownLeft -= dt;
                if (CountdownLeft <= 0)
                {
                    Phase = ArcadePhase.Playing;
                    SetCountdownLabel("GO!");
                    goFlash = .7f;
                    Announce("GO!", Yellow);
                }
                else SetCountdownLabel(Mathf.CeilToInt(CountdownLeft).ToString());
            }
            else
            {
                TickIdle(dt);
                if (Phase == ArcadePhase.Results && presentationSeconds > 0)
                {
                    resultAge += dt;
                    overlayHint.text = "RETURN TO TITLE IN " + Mathf.CeilToInt(Mathf.Max(0, presentationSeconds - resultAge))
                        + "s   //   R RESTART";
                    if (resultAge >= presentationSeconds) PreparePreview();
                }
            }
            AfterTick(dt);
            RefreshHud();
        }

        public void StartRun(int players = 1)
        {
            PlayerCount = SupportsTwoPlayers ? Mathf.Clamp(players, 1, 2) : 1;
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
                Announce("GO!", Yellow);
                Sound(2);
            }
            RefreshHud();
        }

        private void SetCountdownLabel(string text)
        {
            if (countdownLabel != null) countdownLabel.text = text;
            else Announce(text, Yellow, 1);
        }

        private void MenuAction(int players)
        {
            if (Phase == ArcadePhase.Continue) ContinueRun();
            else StartRun(players);
        }

        public void ShowAttract()
        {
            if (Phase == ArcadePhase.Countdown && speaker != null) speaker.Stop();
            goFlash = 0;
            if (countdownLabel != null) countdownLabel.text = "";
            Phase = ArcadePhase.Attract;
            ShowOverlay(cabinetTitle, instructions, "C / SPACE INSERT COIN  //  F1 F2 F3 SWITCH GAME");
        }

        protected void ShowOverlay(string title, string body, string hint)
        {
            overlay.SetActive(true);
            overlayTitle.text = title;
            overlayBody.text = body;
            overlayHint.text = hint;
            onePlayerButton.gameObject.SetActive(true);
            twoPlayerButton.gameObject.SetActive(SupportsTwoPlayers && Phase != ArcadePhase.Continue);
        }

        protected void Finish(string title, string body)
        {
            Phase = ArcadePhase.Results;
            resultAge = 0;
            ShowOverlay(title, body, "R RESTART  //  C INSERT COIN");
            Sound(3);
        }

        protected void Announce(string text, Color color, float duration = 2)
        {
            notice.text = text;
            notice.color = color;
            noticeAge = duration;
        }

        protected abstract void ResetMatch();
        protected abstract void Tick(float dt, ArcadeInput p1, ArcadeInput p2);
        protected abstract void RefreshHud();
        protected virtual void TickIdle(float dt) { }
        // Runs every frame in every phase, after the phase logic (e.g. for looping engine audio).
        protected virtual void AfterTick(float dt) { }
        public virtual void ContinueRun() { StartRun(PlayerCount); }

        private static void LoadCabinet(string scene)
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(scene);
        }

        private static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        private static ArcadeInput ReadInput(int index)
        {
            var k = Keyboard.current;
            Key left = index == 0 ? Key.A : Key.LeftArrow;
            Key right = index == 0 ? Key.D : Key.RightArrow;
            Key up = index == 0 ? Key.W : Key.UpArrow;
            Key down = index == 0 ? Key.S : Key.DownArrow;
            Key button = index == 0 ? Key.Space : Key.RightCtrl;
            var input = new ArcadeInput();
            if (k != null)
            {
                input.steer = (k[right].isPressed ? 1 : 0) - (k[left].isPressed ? 1 : 0);
                input.throttle = (k[up].isPressed ? 1 : 0) - (k[down].isPressed ? 1 : 0);
                input.jumpPressed = k[up].wasPressedThisFrame;
                input.action = k[button].isPressed;
                input.actionPressed = k[button].wasPressedThisFrame;
            }
            if (Gamepad.all.Count > index)
            {
                var pad = Gamepad.all[index];
                Vector2 stick = pad.leftStick.ReadValue();
                if (pad.dpad.ReadValue().sqrMagnitude > .01f) stick = pad.dpad.ReadValue();
                if (stick.sqrMagnitude > .03f) { input.steer = stick.x; input.throttle = stick.y; }
                input.action |= pad.buttonSouth.isPressed;
                input.actionPressed |= pad.buttonSouth.wasPressedThisFrame;
                input.jumpPressed |= pad.dpad.up.wasPressedThisFrame;
            }
            return input;
        }

        protected void Sound(int kind)
        {
            if (sounds != null && speaker != null) speaker.PlayOneShot(sounds[kind], .12f);
        }

        // Plays a recorded clip when one is assigned, otherwise the matching generated sound.
        protected void PlayClip(AudioClip clip, int fallback, float volume = .6f)
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

        protected static string Bar(float fraction, int width = 10)
        {
            int fill = Mathf.Clamp(Mathf.CeilToInt(fraction * width), 0, width);
            return new string('|', fill) + new string('.', width - fill);
        }

        public static Vector3 PixelPosition(Vector2 position) => new Vector3(
            Mathf.Round(position.x * 32) / 32, Mathf.Round(position.y * 32) / 32, 0);
    }
}
