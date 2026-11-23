using System;
using System.IO;
using UnityEngine;

// Required concept: ScriptableObject data with JSON dual serialization.
// The asset is the editor-authored version; JSON is an external saved copy.
[CreateAssetMenu(menuName = "Ultimate Pogo Barn/Map Scene Data")]
public class MapSceneData : ScriptableObject
{
    public string displayName;
    public string sceneName;

    [Serializable]
    private class MapSceneJson
    {
        public string displayName;
        public string sceneName;
    }

    public string ExportJson()
    {
        MapSceneJson data = new MapSceneJson
        {
            displayName = displayName,
            sceneName = sceneName
        };

        string path = Path.Combine(
            Application.persistentDataPath,
            name + "_MapSceneData.json");

        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        Debug.Log("Map scene data exported to: " + path);
        return path;
    }

    public void ImportJson(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Debug.LogError("Map scene JSON file was not found: " + path);
            return;
        }

        MapSceneJson data =
            JsonUtility.FromJson<MapSceneJson>(File.ReadAllText(path));

        if (data == null || string.IsNullOrWhiteSpace(data.sceneName))
        {
            Debug.LogError("Map scene JSON is invalid or has no sceneName.");
            return;
        }

        displayName = data.displayName;
        sceneName = data.sceneName;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
