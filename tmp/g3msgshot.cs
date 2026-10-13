var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput(); g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
for (int f = 0; f < 60 * 3; f++) g.Advance(dt, none, none);
return "frozen at t=" + g.Elapsed.ToString("0.00") + " label=" + g.countdownLabel.text;
