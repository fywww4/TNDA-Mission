using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TNDA.Prototype
{
    // Story: a campaign stage's opening dialogue, page by page, before its countdown (ticket 15).
    public enum ArcadePhase { Attract, Playing, Results, Countdown, Garage, Story }
    public enum GarageTab { Abilities, Decals }
    // What the title screen offers: the normal 1P/2P elimination match, single-player challenges, or the campaign.
    public enum CabinetMode { Normal, Challenge, Campaign }

    public struct ArcadeInput
    {
        public float steer, throttle;
        public bool brake;
        public bool action, actionPressed;
        public bool resetSaveHeld;
        // This seat's START (P1: Space/Enter or pad 1; P2: Numpad Enter or pad 2).
        public bool startPressed;
        // Hold SELECT to see this seat's standings: Tab / Numpad 0, or its pad's Back/View.
        public bool selectHeld;
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
        // Title screen selection. ActiveChallenge is the challenge being played or just finished (null in normal mode,
        // on the title and in the demo).
        public CabinetMode Mode { get; private set; }
        public int ChallengeCursor { get; private set; }
        public Challenge ActiveChallenge { get; private set; }
        public Challenge SelectedChallenge => Challenges.All[ChallengeCursor];
        // Campaign (ticket 15): the stage picked on the title, the stage being told, played or just finished (null
        // otherwise), the story page showing, and whether the last finished stage was cleared.
        public int StageCursor { get; private set; }
        public CampaignStage SelectedStage => Campaign.Stages[StageCursor];
        public CampaignStage ActiveStage { get; private set; }
        public int StoryPage { get; private set; }
        public bool StageCleared { get; private set; }
        private CampaignProgress progress;
        public CampaignProgress Progress => progress ?? (progress = new CampaignProgress());
        public GarageScreen garage;
        private VehicleUpgrades upgrades;
        public VehicleUpgrades Upgrades => upgrades ?? (upgrades = new VehicleUpgrades());
        private CarDecals decals;
        public CarDecals Decals => decals ?? (decals = new CarDecals());
        public GarageTab GarageTab { get; private set; }
        // Abilities: the ability row. Decals: the CarDecal row (0 = no pattern, then the four for sale).
        public int GarageCursor { get; private set; }
        private int GarageRows => GarageTab == GarageTab.Abilities ? 4 : CarDecals.Count + 1;
        public string GarageMessage { get; private set; } = "";
        private ChallengeRecords records;
        public ChallengeRecords Records => records ?? (records = new ChallengeRecords());
        // Saved reward gold, the game's only currency; it pays for challenge unlocks and upgrades.
        private GoldWallet wallet;
        protected GoldWallet Wallet => wallet ?? (wallet = new GoldWallet());
        public int Gold => Wallet.Total;
        private float noticeAge, goFlash;
        // Cheat menu (~): opens on any screen while the game runs and pauses everything until it closes.
        public bool CheatMenuOpen { get; private set; }
        private GameObject cheatPanel;
        private TMP_Text cheatText;
        private string cheatStatus = "";
        [NonSerialized] private AudioClip[] sounds; // generated in Awake; a reload must not turn it into an empty array

        private void Awake()
        {
            // The game never uses slow motion; guard against a time scale left over from editor tooling.
            Time.timeScale = 1;
            Cheats.Invincible = false;
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
            ActiveStage = null;
            ResetMatch();
            ShowAttract();
            RefreshHud();
        }

        private void Update()
        {
            AudioListener.volume = masterVolume;
            if (Pressed(Key.Quote)) SetDeveloperMode(!DeveloperMode);
            if (DeveloperMode && developerText != null) developerText.text = DeveloperReport();
            if (Pressed(Key.Backquote)) SetCheatMenu(!CheatMenuOpen);
            else if (CheatMenuOpen)
            {
                if (Pressed(Key.Escape)) SetCheatMenu(false);
                else CheatMenuInput();
            }
            if (CheatMenuOpen) return;
            bool menu = Phase == ArcadePhase.Attract || Phase == ArcadePhase.Garage || Phase == ArcadePhase.Results
                || Phase == ArcadePhase.Story;
            bool back = Pressed(Key.Z) || PadPressed(p => p.buttonSouth.wasPressedThisFrame);
            if (Pressed(Key.Escape) || (menu && back)) Back();
            else
            {
                var phase = Phase;
                if (Phase == ArcadePhase.Attract) NavigateTitle();
                else if (Phase == ArcadePhase.Garage) NavigateGarage();
                if (Phase == phase && (Phase == ArcadePhase.Attract || Phase == ArcadePhase.Results || Phase == ArcadePhase.Story)
                    && (PressedAny(Key.Space, Key.Enter, Key.NumpadEnter)
                        || PadPressed(p => p.startButton.wasPressedThisFrame) || ConfirmPressed())) PressStart();
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
            if (Phase == ArcadePhase.Story)
            {
                NextStoryPage();
                return;
            }
            if (Phase == ArcadePhase.Results)
            {
                if (ActiveStage != null)
                {
                    // Cleared: on to the next stage's story (the last stage plays again). Failed: straight back in.
                    var next = StageCleared ? Campaign.After(ActiveStage) : null;
                    if (next != null) BeginStory(next);
                    else StartRun(1, stage: ActiveStage);
                }
                else if (ActiveChallenge != null) TryStartChallenge(ActiveChallenge);
                else StartRun(PlayerCount);
                return;
            }
            if (Phase != ArcadePhase.Attract) return;
            if (Mode == CabinetMode.Campaign)
            {
                if (Progress.IsUnlocked(SelectedStage)) BeginStory(SelectedStage);
                else Announce(CabinetText.CampaignLocked, Yellow, 2);
            }
            else if (Mode == CabinetMode.Challenge)
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

        // Esc (or the layout's menu-back button): closes the player menu, leaves the garage, or ends a match or its results for the
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
            if (PressedAny(Key.G, Key.RightShift, Key.Numpad0) || PadPressed(p => p.selectButton.wasPressedThisFrame))
            {
                OpenGarage();
                return;
            }
            bool left = PressedAny(Key.LeftArrow, Key.Numpad4) || DirectionPressed(pad => pad.dpad.left);
            bool right = PressedAny(Key.RightArrow, Key.Numpad6, Key.Tab) || DirectionPressed(pad => pad.dpad.right);
            if (ChoosingPlayers)
            {
                if (left || right) PickPlayers(3 - PlayerChoice);
                return;
            }
            if (left || right) MoveMode(right ? 1 : -1);
            int vertical = PressedAny(Key.UpArrow, Key.Numpad8) || DirectionPressed(pad => pad.dpad.up) ? -1
                : PressedAny(Key.DownArrow, Key.Numpad2) || DirectionPressed(pad => pad.dpad.down) ? 1 : 0;
            if (vertical == 0) return;
            if (Mode == CabinetMode.Challenge) MoveChallengeCursor(vertical);
            else if (Mode == CabinetMode.Campaign) MoveStageCursor(vertical);
        }

        public void SelectMode(CabinetMode mode) => Mode = mode;

        // Left/right on the title: 一般 -> 挑戰 -> 關卡 and round again.
        public void MoveMode(int delta)
        {
            const int count = 3;
            Mode = (CabinetMode)((((int)Mode + delta) % count + count) % count);
        }

        public void MoveStageCursor(int delta)
        {
            int count = Campaign.Stages.Count;
            StageCursor = ((StageCursor + delta) % count + count) % count;
        }

        // A campaign stage's opening story: the stage's field is set up behind the dialogue, START turns the page and
        // after the last page the stage starts. Esc goes back to the title.
        public void BeginStory(CampaignStage stage)
        {
            if (stage == null) return;
            ActiveStage = stage;
            ActiveChallenge = null;
            PlayerCount = 1;
            Elapsed = 0;
            ChoosingPlayers = false;
            StoryPage = 0;
            Phase = ArcadePhase.Story;
            if (garage != null) garage.gameObject.SetActive(false);
            ResetMatch();
            if (stage.Story == null || stage.Story.Count == 0) { StartRun(1, stage: stage); return; }
            ShowStoryPage();
            Sound(0);
        }

        public void NextStoryPage()
        {
            if (Phase != ArcadePhase.Story || ActiveStage == null) return;
            if (++StoryPage >= ActiveStage.Story.Count) { StartRun(1, stage: ActiveStage); return; }
            ShowStoryPage();
            Sound(0);
        }

        private void ShowStoryPage()
        {
            string page = ActiveStage.Story[StoryPage];
            int bar = page.IndexOf('｜');
            string body = bar < 0 ? page : string.Format(CabinetText.StoryPage, page.Substring(0, bar), page.Substring(bar + 1));
            ShowOverlay(string.Format(CabinetText.StageTitle, ActiveStage.Number, ActiveStage.Name), body, CabinetText.StoryHint);
        }

        // The finished stage's result panel; a clear is saved and opens the next stage.
        protected void FinishStage(bool cleared, string body)
        {
            StageCleared = cleared;
            if (cleared) Progress.MarkCleared(ActiveStage);
            bool last = Campaign.After(ActiveStage) == null;
            string title = cleared && last ? CabinetText.CampaignComplete
                : string.Format(cleared ? CabinetText.StageClear : CabinetText.StageFailed, ActiveStage.Number);
            Finish(title, body, !cleared ? CabinetText.StageRetryHint : last ? CabinetText.StageReplayHint : CabinetText.StageNextHint);
        }

        public void OpenGarage()
        {
            if (Phase != ArcadePhase.Attract || garage == null) return;
            Phase = ArcadePhase.Garage;
            GarageTab = GarageTab.Abilities;
            GarageCursor = 0;
            GarageMessage = "";
            overlay.SetActive(false);
            RefreshHud();
            garage.Render(this, 0);
        }

        public void MoveGarageCursor(int delta)
        {
            GarageCursor = ((GarageCursor + delta) % GarageRows + GarageRows) % GarageRows;
            GarageMessage = "";
        }

        // Left/right in the garage: 能力 <-> 外觀.
        public void SwitchGarageTab()
        {
            GarageTab = GarageTab == GarageTab.Abilities ? GarageTab.Decals : GarageTab.Abilities;
            GarageCursor = 0;
            GarageMessage = "";
        }

        // 外觀 tab, for P1 (seat 0) or P2 (seat 1): buys the selected pattern into the shared collection if needed, then
        // puts it on that seat's car. The "no pattern" row takes it off.
        public bool UseSelectedDecal(int seat)
        {
            if (Phase != ArcadePhase.Garage || GarageTab != GarageTab.Decals) return false;
            var decal = (CarDecal)GarageCursor;
            bool bought = false;
            if (!Decals.Owns(decal))
            {
                bought = Decals.TryBuy(decal, Wallet);
                if (!bought)
                {
                    GarageMessage = CabinetText.GarageGoldShort;
                    garage?.Render(this, 0);
                    return false;
                }
            }
            Decals.Equip(seat, decal);
            GarageMessage = string.Format(bought ? CabinetText.GarageDecalBought : CabinetText.GarageDecalEquipped,
                seat + 1, CabinetText.GarageDecalNames[(int)decal]);
            Sound(bought ? 2 : 0);
            garage?.Render(this, 0);
            return true;
        }

        public bool TryBuySelectedUpgrade()
        {
            if (Phase != ArcadePhase.Garage || GarageTab != GarageTab.Abilities) return false;
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
            if (PressedAny(Key.UpArrow, Key.Numpad8) || DirectionPressed(pad => pad.dpad.up)) MoveGarageCursor(-1);
            if (PressedAny(Key.DownArrow, Key.Numpad2) || DirectionPressed(pad => pad.dpad.down)) MoveGarageCursor(1);
            if (PressedAny(Key.LeftArrow, Key.RightArrow, Key.Numpad4, Key.Numpad6) || DirectionPressed(pad => pad.dpad.left, pad => pad.dpad.right))
                SwitchGarageTab();
            if (GarageTab == GarageTab.Abilities)
            {
                if (ConfirmPressed()) TryBuySelectedUpgrade();
            }
            else
                // Each seat equips its own car: P1 on its item key or pad 1, P2 on Numpad Enter or pad 2.
                for (int seat = 0; seat < 2; seat++)
                    if (SeatConfirmPressed(seat)) UseSelectedDecal(seat);
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
        public void StartRun(int players = 1, Challenge challenge = null, CampaignStage stage = null)
        {
            ActiveChallenge = challenge;
            ActiveStage = stage;
            StageCleared = false;
            PlayerCount = challenge != null || stage != null ? 1 : Mathf.Clamp(players, 1, 2);
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
            ActiveStage = null;
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

        public void SetCheatMenu(bool open)
        {
            CheatMenuOpen = open;
            cheatStatus = "";
            if (open && cheatPanel == null) BuildCheatPanel();
            if (cheatPanel != null)
            {
                cheatPanel.SetActive(open);
                cheatPanel.transform.SetAsLastSibling();
            }
            if (open) RenderCheatMenu();
            Sound(0);
        }

        private void CheatMenuInput()
        {
            for (int i = 1; i <= 5; i++)
                if (Pressed(Key.Digit1 + i - 1) || Pressed(Key.Numpad1 + i - 1)) UseCheat(i);
        }

        // 1 gold, 2 every campaign stage cleared, 3 score, 4 enemies wiped out, 5 invincibility on/off.
        public void UseCheat(int number)
        {
            bool done = true;
            switch (number)
            {
                case 1:
                    Wallet.Add(Cheats.Gold);
                    cheatStatus = string.Format(CabinetText.CheatGoldDone, Gold);
                    break;
                case 2:
                    foreach (var stage in Campaign.Stages) Progress.MarkCleared(stage);
                    cheatStatus = CabinetText.CheatStagesDone;
                    break;
                case 3:
                    done = CheatScore(Cheats.Score);
                    cheatStatus = done ? CabinetText.CheatScoreDone : CabinetText.CheatNeedsMatch;
                    break;
                case 4:
                    done = CheatKnockOutEnemies();
                    cheatStatus = done ? CabinetText.CheatWipeDone : CabinetText.CheatNeedsMatch;
                    break;
                case 5:
                    Cheats.Invincible = !Cheats.Invincible;
                    cheatStatus = string.Format(CabinetText.CheatInvincibleDone, Cheats.Invincible ? CabinetText.CheatOn : CabinetText.CheatOff);
                    break;
                default: return;
            }
            Sound(done ? 2 : 1);
            RefreshHud();
            RenderCheatMenu();
        }

        // The match-only cheats; false when no match is being played (title, garage, story, results, the demo).
        protected virtual bool CheatScore(int points) => false;
        protected virtual bool CheatKnockOutEnemies() => false;

        private void BuildCheatPanel()
        {
            var canvas = overlayBody.canvas.rootCanvas;
            cheatPanel = new GameObject("Cheat menu", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            var rect = (RectTransform)cheatPanel.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300, 150);
            cheatPanel.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            var outline = cheatPanel.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Yellow;
            outline.effectDistance = new Vector2(2, -2);
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            cheatText = textObject.GetComponent<TextMeshProUGUI>();
            cheatText.font = overlayBody.font;
            cheatText.fontSharedMaterial = overlayBody.fontSharedMaterial;
            cheatText.fontSize = 11;
            cheatText.lineSpacing = 20;
            cheatText.color = White;
            cheatText.alignment = TextAlignmentOptions.TopLeft;
            cheatText.richText = true;
            cheatText.raycastTarget = false;
        }

        // Developer mode (' on any screen): a plain text panel, top left, with each car's health and the crate odds.
        // The game keeps running underneath.
        public bool DeveloperMode { get; private set; }
        private GameObject developerPanel;
        private TMP_Text developerText;

        public void SetDeveloperMode(bool on)
        {
            DeveloperMode = on;
            if (on && developerPanel == null) BuildDeveloperPanel();
            if (developerPanel == null) return;
            developerPanel.SetActive(on);
            developerPanel.transform.SetAsLastSibling();
            if (on) developerText.text = DeveloperReport();
        }

        // What the developer panel shows; the match adapter fills it in.
        protected virtual string DeveloperReport() => CabinetText.DevTitle + "\n" + CabinetText.DevNoMatch;

        private void BuildDeveloperPanel()
        {
            var canvas = overlayBody.canvas.rootCanvas;
            developerPanel = new GameObject("Developer mode", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)developerPanel.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(4, -4);
            rect.sizeDelta = new Vector2(504, 280);
            var image = developerPanel.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 4);
            textRect.offsetMax = new Vector2(-6, -4);
            developerText = textObject.GetComponent<TextMeshProUGUI>();
            developerText.font = overlayBody.font;
            developerText.fontSharedMaterial = overlayBody.fontSharedMaterial;
            developerText.fontSize = 11;
            developerText.color = White;
            developerText.alignment = TextAlignmentOptions.TopLeft;
            developerText.textWrappingMode = TextWrappingModes.NoWrap;
            developerText.overflowMode = TextOverflowModes.Truncate;
            developerText.richText = false;
            developerText.raycastTarget = false;
        }

        private void RenderCheatMenu()
        {
            if (cheatText == null) return;
            string yellow = "#" + ColorUtility.ToHtmlStringRGB(Yellow);
            cheatText.text = string.Join("\n",
                "<color=" + yellow + ">" + CabinetText.CheatTitle + "</color>",
                string.Format(CabinetText.CheatGold, Gold),
                CabinetText.CheatStages,
                CabinetText.CheatScore,
                CabinetText.CheatWipe,
                string.Format(CabinetText.CheatInvincible, Cheats.Invincible ? CabinetText.CheatOn : CabinetText.CheatOff),
                "<color=" + yellow + ">" + cheatStatus + "</color>");
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

        private static bool DirectionPressed(params Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl>[] directions)
        {
            return PadPressed(pad =>
            {
                foreach (var direction in directions)
                {
                    var button = direction(pad);
                    var stick = pad.leftStick[button.name] as UnityEngine.InputSystem.Controls.ButtonControl;
                    if (button.wasPressedThisFrame || (stick != null && stick.wasPressedThisFrame))
                        return true;
                }
                return false;
            });
        }

        private static bool PadPressed(Func<Gamepad, bool> pressed)
        {
            foreach (var pad in Gamepad.all) if (pressed(pad)) return true;
            return false;
        }

        private bool ConfirmPressed() => Pressed(Key.X) || PadPressed(p => p.buttonEast.wasPressedThisFrame);

        private bool SeatConfirmPressed(int seat)
        {
            if (seat == 0 ? Pressed(Key.X) : Pressed(Key.NumpadEnter)) return true;
            return Gamepad.all.Count > seat && Gamepad.all[seat].buttonEast.wasPressedThisFrame;
        }

        // The live input boundary: each seat combines its keyboard keys with its own gamepad.
        public static ArcadeInput ReadInput(int index)
        {
            // One control layout (SFC style): P1 Z gas, X item, A brake; P2 stays on the numpad. Pads: South gas,
            // East item, West brake.
            var k = Keyboard.current;
            Key left = index == 0 ? Key.LeftArrow : Key.Numpad4;
            Key right = index == 0 ? Key.RightArrow : Key.Numpad6;
            Key gas = index == 0 ? Key.Z : Key.Numpad8;
            Key button = index == 0 ? Key.X : Key.NumpadEnter;
            Key brake = index == 0 ? Key.A : Key.Numpad2;
            var input = new ArcadeInput();
            if (k != null)
            {
                input.steer = (k[right].isPressed ? 1 : 0) - (k[left].isPressed ? 1 : 0);
                input.throttle = k[gas].isPressed ? 1 : 0;
                input.brake = k[brake].isPressed;
                input.action = k[button].isPressed;
                input.actionPressed = k[button].wasPressedThisFrame;
                input.resetSaveHeld = index == 0 && k.deleteKey.isPressed;
                input.startPressed = index == 0 ? k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame
                    : k.numpadEnterKey.wasPressedThisFrame;
                input.selectHeld = k[index == 0 ? Key.Tab : Key.Numpad0].isPressed;
            }
            if (Gamepad.all.Count > index)
            {
                var pad = Gamepad.all[index];
                Vector2 stick = pad.leftStick.ReadValue();
                if (pad.dpad.ReadValue().sqrMagnitude > .01f) stick = pad.dpad.ReadValue();
                if (stick.sqrMagnitude > .03f) input.steer = stick.x;
                if (pad.buttonSouth.isPressed) input.throttle = 1;
                input.brake |= pad.buttonWest.isPressed;
                var item = pad.buttonEast;
                input.action |= item.isPressed;
                input.actionPressed |= item.wasPressedThisFrame;
                input.startPressed |= pad.startButton.wasPressedThisFrame;
                input.selectHeld |= pad.selectButton.isPressed;
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
