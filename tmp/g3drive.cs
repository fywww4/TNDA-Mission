var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60;
var none = new TNDA.Prototype.ArcadeInput(); var gas = new TNDA.Prototype.ArcadeInput(); gas.throttle = 1;
g.enabled = false; // drive the simulation manually for exact timing
g.StartRun(1);
while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
var car = g.cars[0];
for (int f = 1; f <= 150; f++) { g.Advance(dt, gas, none); if (f % 30 == 0) sb.Append("hold " + (f/60f).ToString("0.0") + "s speed=" + car.speed.ToString("0.00") + "\n"); }
for (int f = 1; f <= 90; f++) { g.Advance(dt, none, none); if (f % 30 == 0) sb.Append("release " + (f/60f).ToString("0.0") + "s speed=" + car.speed.ToString("0.00") + "\n"); }
var press = new TNDA.Prototype.ArcadeInput(); press.action = true; press.actionPressed = true;
g.Advance(dt, press, none);
sb.Append("Ctrl without item: shield=" + car.shieldSeconds + " item=" + car.item + "\n");
car.item = TNDA.Prototype.ArenaItem.Shield; g.Advance(dt, press, none);
sb.Append("Ctrl with shield: item=" + car.item + " shield=" + car.shieldSeconds.ToString("0.0") + " notice=" + g.notice.text + "\n");
sb.Append("hud=" + g.hud.text.Replace("\n"," | ") + "\nboard=" + g.arenaBoard.text + "\nfooter=" + g.footer.text.Replace("\n"," | "));
g.enabled = true;
return sb.ToString();
