// Measures cherry frequency (Play Mode): 6 matches of four AI cars. Counts crate pickups, cherries rolled (and how
// many while every contender was tied, e.g. all at 0), cherries eaten, and cars wrecked by a cherry touch.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
float gameOver = g.gameOverSeconds; g.gameOverSeconds = 1e6f; // no humans here, so keep GAME OVER from ending the match
int pickups = 0, cherries = 0, cherriesTied = 0, eaten = 0, cherryKills = 0, early = 0;
var sb = new System.Text.StringBuilder();
for (int m = 0; m < 6; m++)
{
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var c in g.cars) c.computer = true;
    var item = new TNDA.Prototype.ArenaItem[4]; var alive = new bool[4]; var had = new bool[4];
    for (int i = 0; i < 4; i++) { item[i] = g.cars[i].item; alive[i] = g.cars[i].alive; }
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing)
    {
        bool tied = System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(g.cars, c => !c.eliminated), c => c.score).Distinct().Count() == 1;
        if (g.Elapsed > 119.5f) g.cars[0].computer = false; // the results screen reads a human's best score
        g.Advance(dt, none, none);
        bool cherryOut = System.Linq.Enumerable.Any(g.cars, c => c.HasCherry);
        for (int i = 0; i < 4; i++)
        {
            var c = g.cars[i];
            if (item[i] == TNDA.Prototype.ArenaItem.None && c.item != TNDA.Prototype.ArenaItem.None)
            {
                pickups++;
                if (c.item == TNDA.Prototype.ArenaItem.Cherry) { cherries++; if (tied) cherriesTied++; if (g.Elapsed < 20) early++; }
            }
            if (!had[i] && c.HasCherry) eaten++;
            if (alive[i] && !c.alive && !c.eliminated && cherryOut && !c.HasCherry) cherryKills++;
            item[i] = c.item; alive[i] = c.alive; had[i] = c.HasCherry;
        }
    }
}
g.gameOverSeconds = gameOver; g.enabled = wasEnabled; g.PreparePreview();
return "pickups " + pickups + ", cherries " + cherries + " (" + (100f * cherries / pickups).ToString("0") + "%), rolled while all tied " + cherriesTied
    + ", in first 20 s " + early + ", eaten " + eaten + ", wrecks while a cherry was active " + cherryKills + " over 6 matches";
