var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60;
var none = new TNDA.Prototype.ArcadeInput(); var gas = new TNDA.Prototype.ArcadeInput(); gas.throttle = 1;
var use = new TNDA.Prototype.ArcadeInput(); use.action = true; use.actionPressed = true;
var roll = typeof(TNDA.Prototype.DemolitionGame).GetMethod("RollItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
System.Action skip = () => { while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none); };
System.Action<int> unprotect = n => { foreach (var c in g.cars) c.spawnProtection = 0; };
g.enabled = false;
try {
// 1. Roll odds
g.StartRun(1); skip();
g.cars[0].score = 0; g.cars[1].score = 100; g.cars[2].score = 200; g.cars[3].score = 300;
int[] last = new int[4], lead = new int[4];
for (int k = 0; k < 2000; k++) { last[(int)roll.Invoke(g, new object[]{ g.cars[0] })]++; lead[(int)roll.Invoke(g, new object[]{ g.cars[3] })]++; }
sb.Append("Roll last place: shield " + last[1] + " rocket " + last[2] + " cherry " + last[3] + " | leader: shield " + lead[1] + " rocket " + lead[2] + " cherry " + lead[3] + "\n");
// 2. Use cherry + top speed
var p1 = g.cars[0]; p1.item = 3; g.Advance(dt, use, none);
sb.Append("Use cherry: HasCherry=" + g.HasCherry(p1) + " item=" + p1.item + " status=" + g.hud.text.Split('\n')[0] + " icon=" + g.cherryViews[0].gameObject.activeSelf + "\n");
p1.position = new UnityEngine.Vector2(-3.5f, -3.5f); p1.heading = 0;
for (int f = 0; f < 150; f++) { g.Advance(dt, gas, none); foreach (var c in g.cars) if (c != p1) { c.position = new UnityEngine.Vector2(3, 1); } }
sb.Append("Cherry top speed=" + p1.speed.ToString("0.00") + " (normal max " + g.driveSpeed + ")\n");
// 3. Cherry touches AI -> AI wrecked
g.StartRun(1); skip(); unprotect(0);
p1 = g.cars[0]; var ai = g.cars[2]; p1.item = 3; g.Advance(dt, use, none);
p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 0; p1.speed = .5f; ai.position = new UnityEngine.Vector2(.55f, -1); ai.speed = 0; ai.heading = 90;
float s0 = p1.score; for (int f = 0; f < 5; f++) g.Advance(dt, none, none);
sb.Append("Cherry P1 touches AI (slow): ai alive=" + ai.alive + " p1 alive=" + p1.alive + " p1 score +" + (p1.score - s0) + "\n");
// 4. 2P: P2 rams cherry P1 -> P1 wrecked
g.StartRun(2); skip(); unprotect(0);
p1 = g.cars[0]; var p2 = g.cars[1]; p1.item = 3; g.Advance(dt, use, none);
p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 90; p1.speed = 0;
p2.position = new UnityEngine.Vector2(-.6f, -1); p2.heading = 0; p2.speed = 2.6f;
g.Advance(dt, none, gas);
sb.Append("P2 rams cherry P1: p1 alive=" + p1.alive + " p2 alive=" + p2.alive + " notice=" + g.notice.text + "\n");
// 5. Rocket kills cherry car (AI with cherry hit by P1 rocket)
g.StartRun(1); skip(); unprotect(0);
ai = g.cars[3]; ai.item = 0; typeof(TNDA.Prototype.DemolitionGame).GetField("cherrySeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, new float[]{0,0,0,8});
ai.health = 1; ai.position = new UnityEngine.Vector2(1, -1); ai.speed = 0;
g.rockets[0].Spawn(new UnityEngine.Vector2(.3f, -1), new UnityEngine.Vector2(6, 0), 0);
g.Advance(dt, none, none); g.Advance(dt, none, none);
sb.Append("Rocket hits cherry AI: ai alive=" + ai.alive + "\n");
// 6. AI cherry never harms human
g.StartRun(1); skip(); unprotect(0);
p1 = g.cars[0]; ai = g.cars[2];
typeof(TNDA.Prototype.DemolitionGame).GetField("cherrySeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, new float[]{0,0,8,0});
ai.position = new UnityEngine.Vector2(0, -1); ai.heading = 0; ai.speed = 3; p1.position = new UnityEngine.Vector2(.55f, -1); p1.speed = 0;
g.Advance(dt, none, none);
sb.Append("AI cherry touches P1: p1 alive=" + p1.alive + " ai alive=" + ai.alive + "\n");
// 7. 30s soak with AI only for exceptions
g.StartRun(1); skip();
for (int f = 0; f < 60*30; f++) g.Advance(dt, none, none);
sb.Append("30s soak ok, phase=" + g.Phase + " scores=" + string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName + ":" + c.score)));
} catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
g.enabled = true; g.PreparePreview();
return sb.ToString();
