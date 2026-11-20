using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Dual serialization for premade maps:
// edit/store the layout as a ScriptableObject asset, or export/import it as JSON.
// This does not create a player-facing custom-map editor.
[Serializable]
public class UPBPlacedTrackPiece
{
    public string prefabKey;
    public Vector3 position;
    public Vector3 eulerAngles;
    public Vector3 scale = Vector3.one;
}

[CreateAssetMenu(menuName = "Ultimate Pogo Barn/Premade Track Layout")]
public class UPBPremadeTrackLayout : ScriptableObject
{
    public List<UPBPlacedTrackPiece> pieces = new List<UPBPlacedTrackPiece>();

    [Serializable]
    private class LayoutFile
    {
        public List<UPBPlacedTrackPiece> pieces;
    }

    public void ExportJson(string filePath)
    {
        LayoutFile data = new LayoutFile { pieces = pieces };
        File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
    }

    public void ImportJson(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Track layout file not found.", filePath);

        LayoutFile data = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(filePath));
        pieces = data != null && data.pieces != null
            ? data.pieces
            : new List<UPBPlacedTrackPiece>();
    }
}
