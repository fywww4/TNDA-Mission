var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var r = g.crownView;
var s = r.sprite;
var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(s));
var block = new UnityEngine.MaterialPropertyBlock(); r.GetPropertyBlock(block);
return new { scene = g.gameObject.scene.path, dirty = g.gameObject.scene.isDirty, height = g.crownHeight,
    path = UnityEditor.AssetDatabase.GetAssetPath(s), rect = s.rect.ToString(), ppu = s.pixelsPerUnit,
    tex = s.texture.name, texSize = s.texture.width + "x" + s.texture.height,
    filter = s.texture.filterMode.ToString(), compression = importer.textureCompression.ToString(),
    pivot = s.pivot.ToString(), scale = r.transform.lossyScale.ToString(), rotation = r.transform.eulerAngles.ToString(),
    tint = r.color.ToString(), material = r.sharedMaterial.name, shader = r.sharedMaterial.shader.name,
    hasBlock = !block.isEmpty, drawMode = r.drawMode.ToString(), size = r.size.ToString(),
    position = r.transform.position.ToString(), order = r.sortingOrder,
    labels = System.Linq.Enumerable.Select(g.carLabels, t => new { name = t.name, pos = t.transform.position.ToString() }).ToArray() };
