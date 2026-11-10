// Ticket 12: pre-rasterise the new challenge text into the Cubic 11 pixel font (edit mode).
var font = TNDA.Prototype.Editor.ArcadeFontSetup.GetOrCreate(null);
string need = "按花金幣解鎖不足需現有已「」耐久%未過關準備中尚開放";
return new { glyphs = font.characterTable.Count, missing = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(need, c => !font.HasCharacter(c)))) };
