using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 4) Dual serialization: ScriptableObject asset in-editor, JSON for export/import.
[Serializable]
public class UPBPlacedPiece
{
    public string prefabKey;
    public Vector3 position;
    public Vector3 eulerAngles;
    public Vector3 scale = Vector3.one;
}
[CreateAssetMenu(menuName = "Ultimate Pogo Barn/Map Layout")]
public class UPBMapLayout : ScriptableObject
{
    public List<UPBPlacedPiece> pieces = new List<UPBPlacedPiece>();
    [Serializable] private class LayoutFile { public List<UPBPlacedPiece> pieces; }

    public void ExportJson(string path)
    {
        var data = new LayoutFile { pieces = pieces };
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }
    public void ImportJson(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var data = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
        pieces = data != null && data.pieces != null ? data.pieces : new List<UPBPlacedPiece>();
    }
}