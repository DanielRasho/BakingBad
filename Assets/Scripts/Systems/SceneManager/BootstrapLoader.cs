using UnityEngine;
using UnityEngine.SceneManagement;

public static class BootstrapLoader
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == "bootstrap")
            return;

        SceneManager.LoadScene("bootstrap", LoadSceneMode.Additive);
    }
}