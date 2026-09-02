using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class AdditiveSceneLoader : MonoBehaviour
{
    [SerializeField] private string[] additiveSceneNames;

    private readonly List<AsyncOperation> pendingLoads = new List<AsyncOperation>();

    private void Awake()
    {
        LoadConfiguredScenes();
    }

    private void LoadConfiguredScenes()
    {
        if (additiveSceneNames == null)
        {
            return;
        }

        for (int i = 0; i < additiveSceneNames.Length; i++)
        {
            string sceneName = additiveSceneNames[i];
            if (string.IsNullOrWhiteSpace(sceneName) || IsSceneLoaded(sceneName))
            {
                continue;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOperation != null)
            {
                pendingLoads.Add(loadOperation);
            }
        }
    }

    private static bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.isLoaded && loadedScene.name == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}
