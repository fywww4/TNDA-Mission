var game = UnityEngine.Object.FindFirstObjectByType<TNDA.Prototype.DemolitionGame>();
UnityEditor.Undo.RecordObject(game, "Update one-button driving instructions");
game.instructions = "SCORE BY SMASHING PROPS & CARS.\nLAST PLACE OUT AT 60 / 90 / 120s.\nWRECK: -250 / RESPAWN IN 3s.\nSHIELD: 6s / ROCKET: FIRE AHEAD.\nP1: LEFT/RIGHT + LEFT CTRL\nP2: A/D + NUMPAD ENTER\nHOLD TO DRIVE / RELEASE TO SLOW.\nPRESS: USE ITEM, OTHERWISE RAM.";
game.PreparePreview();
UnityEditor.EditorUtility.SetDirty(game);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(game.gameObject.scene);
UnityEditor.AssetDatabase.SaveAssets();
var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TNDA.Prototype.Editor.ArcadeFontSetup.FontPath);
var fallback = font.fallbackFontAssetTable.Single();
return new { saved = !game.gameObject.scene.isDirty, fonts = game.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.font.name).Distinct().ToArray(),
    characters = font.characterTable.Count, mode = font.atlasPopulationMode.ToString(), scale = font.faceInfo.scale,
    fallback = new { fallback.name, mode = fallback.atlasPopulationMode.ToString(), scale = fallback.faceInfo.scale, fallback.isMultiAtlasTexturesEnabled },
    instructions = game.instructions };
