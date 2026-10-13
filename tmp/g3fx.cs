var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput(); use.action = true; use.actionPressed = true;
g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
for (int f = 0; f < 60 * 4; f++) g.Advance(dt, none, none);
var p1 = g.cars[0]; p1.item = TNDA.Prototype.ArenaItem.Cherry; g.Advance(dt, use, none);
var block = new UnityEngine.MaterialPropertyBlock(); p1.body.GetPropertyBlock(block);
sb.Append("P1 cherry=" + g.HasCherry(p1) + " solid=" + block.GetFloat("_Solid") + " colour=" + block.GetColor("_SolidColor") + "\n");
var ai = g.cars[2]; ai.spawnProtection = 0; ai.shieldSeconds = 0; ai.position = new UnityEngine.Vector2(1.5f, -1.2f); ai.speed = 0;
g.rockets[0].Spawn(new UnityEngine.Vector2(.6f, -1.2f), new UnityEngine.Vector2(6, 0), 0);
for (int f = 0; f < 30 && g.rockets[0].active; f++) { ai.speed = 0; g.Advance(dt, none, none); }
sb.Append("explosion visible=" + g.explosions[0].gameObject.activeSelf + " at " + g.explosions[0].transform.position + " ai hp=" + (ai.health*100).ToString("0") + "\n");
for (int f = 0; f < 4; f++) g.Advance(dt, none, none);
sb.Append("frozen for screenshot; explosion scale=" + g.explosions[0].transform.localScale.x.ToString("0.00"));
return sb.ToString();
