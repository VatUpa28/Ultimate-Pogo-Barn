using System.Collections;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("Map Scene Assets")]
    public MapSceneData map1;
    public MapSceneData map2;
    public MapSceneData map3;

    [Header("Loading")]
    public SceneLoadStrategy loadStrategy;
    [Min(0f)] public float transitionDelay = 0.15f;

    [Header("Runtime State")]
    public MenuLoadState loadState = MenuLoadState.Ready;

    public void LoadMap1() { BeginMapLoad(map1); }
    public void LoadMap2() { BeginMapLoad(map2); }
    public void LoadMap3() { BeginMapLoad(map3); }

    private void BeginMapLoad(MapSceneData mapData)
    {
        if (loadState == MenuLoadState.Loading)
            return;

        if (mapData == null)
        {
            Debug.LogError("A map scene data asset has not been assigned.");
            return;
        }

        if (loadStrategy == null)
        {
            Debug.LogError("Assign a scene loading strategy in the Inspector.");
            return;
        }

        if (string.IsNullOrWhiteSpace(mapData.sceneName))
        {
            Debug.LogError("The selected map has an empty sceneName.");
            return;
        }

        StartCoroutine(LoadMapRoutine(mapData));
    }

    // Required concept: coroutine.
    // This gives the menu a short transition delay, then delegates the load strategy.
    private IEnumerator LoadMapRoutine(MapSceneData mapData)
    {
        loadState = MenuLoadState.Loading;

        if (transitionDelay > 0f)
            yield return new WaitForSeconds(transitionDelay);

        yield return StartCoroutine(loadStrategy.LoadScene(mapData.sceneName));

        // If the scene could not load, allow the user to try again.
        loadState = MenuLoadState.Ready;
    }
}
