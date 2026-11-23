using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(menuName = "Ultimate Pogo Barn/Scene Loading/Async Scene Load")]
public class AsyncSceneLoadStrategy : SceneLoadStrategy
{
    public override IEnumerator LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene '" + sceneName + "' is not in Build Settings.");
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError("Could not start loading scene: " + sceneName);
            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }
}