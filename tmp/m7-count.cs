var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder();
foreach (var p in g.props) sb.Append("prop " + p.view.name + " " + p.point + (p.broken ? " broken" : "") + " | ");
foreach (var p in g.pickups) sb.Append("crate " + p.point + (p.available ? "" : " taken") + " | ");
foreach (var c in g.cars) sb.Append(c.DriverName + " " + c.position + " score " + c.score + " | ");
return sb.ToString();
