using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneGameManager : MonoBehaviour
{
    #region Singleton
    public static SceneGameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    [SerializeField] private LoadingOverlay loadingOverlay;
    [SerializeField] private float minimumLoadTime = 1.5f;
    // For preventing to load the same scene twice.
    private Dictionary<string, string> loadedSceneBySlot = new();
    private bool isBusy = false;


    private void Start()
    {
        var active = SceneManager.GetActiveScene();
        loadedSceneBySlot[active.name] = active.name;
    }

    // API
    public SceneTransitionPlan NewTransition()
    {
        return new SceneTransitionPlan();
    }
    
    private Coroutine ExecutePlan(SceneTransitionPlan plan)
    {
        if (isBusy)
        {
            Debug.LogWarning("Scene change already in progress.");
            return null;
        }

        isBusy = true;
        return StartCoroutine(ChangeSceneRoutine(plan));
    }

    private IEnumerator ChangeSceneRoutine(SceneTransitionPlan plan)
    {
        if (plan.Overlay)
        {
            yield return loadingOverlay.FadeIn();
            yield return new WaitForSeconds(4f);
        }

        foreach (var key in plan.ScenesToUnload)
        {
            yield return UnloadSceneRoutine(key);
        }
        if (plan.ClearUnusedAssets) yield return CleanupUnusedAssetsRoutine();

        foreach (var kvp in plan.ScenesToLoad)
        {
            if (loadedSceneBySlot.ContainsKey(kvp.Key))
            {
                yield return UnloadSceneRoutine(kvp.Key);
            }

            yield return LoadAdditiveRoutine(kvp.Key, kvp.Value, plan.ActiveSceneName == kvp.Value);
        }

        if (plan.Overlay)
        {
            yield return loadingOverlay.FadeOut();
        }
        isBusy = false;
    }

    private IEnumerator LoadAdditiveRoutine(string slotKey, string sceneName, bool setActive)
    {
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if(loadOp == null) yield break;
        while (loadOp.progress < 0.9f)
        {
            yield return null;
        }

        loadOp.allowSceneActivation = true;
        while (!loadOp.isDone)
        {
            yield return null;
        }
        if (setActive)
        {
            Scene newScene = SceneManager.GetSceneByName(sceneName);
            if (newScene.IsValid() && newScene.isLoaded)
            {
                SceneManager.SetActiveScene(newScene);
            }
        }
        loadedSceneBySlot[slotKey] = sceneName;
    }

    private IEnumerator UnloadSceneRoutine(string slotKey)
    {
        if (!loadedSceneBySlot.TryGetValue(slotKey, out string sceneName)) yield break;
        if (string.IsNullOrEmpty(sceneName)) yield break;
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(slotKey);
        if (unloadOp != null)
        {
            while (!unloadOp.isDone)
            {
                yield return null;
            }
        }
        loadedSceneBySlot.Remove(slotKey);
    }

    private IEnumerator CleanupUnusedAssetsRoutine()
    {
        AsyncOperation loadOp = Resources.UnloadUnusedAssets();
        while (!loadOp.isDone)
        {
            yield return null;
        }
    }
    
    // Transition Plan Class
    public class SceneTransitionPlan
    {
        public Dictionary<string, string> ScenesToLoad { get; } = new();
        public List<string> ScenesToUnload { get; } = new();
        public string ActiveSceneName { get; private set; } = "";
        public bool ClearUnusedAssets { get; private set; } = false;
        public bool Overlay { get; private set; } = false;
        public SceneTransitionPlan Load(string slotKey, string sceneName, bool setActive = false)
        {
            ScenesToLoad[slotKey] = sceneName;
            if (setActive) ActiveSceneName = sceneName;
            return this;
        }
        public SceneTransitionPlan Unload(string slotKey)
        {
            ScenesToUnload.Add(slotKey);
            return this;
        }

        public SceneTransitionPlan WithOverlay()
        {
            Overlay = true;
            return this;
        }

        public SceneTransitionPlan WithClearUnusedAssets()
        {
            ClearUnusedAssets = true;
            return this;
        }

        public Coroutine Perform()
        {
            return SceneGameManager.Instance.ExecutePlan(this);
        }
    }

    public void InsertSlot(string key, string value)
    {
        loadedSceneBySlot[key] = value;
    }
}
