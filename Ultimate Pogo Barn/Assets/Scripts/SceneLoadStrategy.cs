using System.Collections;
using UnityEngine;

// Required concept: Strategy Pattern.
// The menu depends on this abstraction, not on one specific loading method.
public abstract class SceneLoadStrategy : ScriptableObject
{
    public abstract IEnumerator LoadScene(string sceneName);
}
