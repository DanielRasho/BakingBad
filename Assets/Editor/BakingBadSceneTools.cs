using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BakingBadSceneTools
{
    private const string MainMapPath = "Assets/Scenes/MainMap.unity";
    private const string KitchenCellPath = "Assets/Scenes/KitchenCell.unity";
    private const string TargetPath = "Walls/Kitchen/KitchenCellJail";
    private const string GeneratedRootName = "KitchenCellGameplay";

    private static readonly HashSet<string> ExcludedSourceRoots = new HashSet<string>
    {
        "Main Camera",
        "Directional Light"
    };

    [MenuItem("Baking Bad/Copy KitchenCell Into MainMap")]
    public static void CopyKitchenCellIntoMainMap()
    {
        Scene mainMapScene = OpenOrGetScene(MainMapPath, OpenSceneMode.Single);
        Transform target = FindScenePath(mainMapScene, TargetPath);
        if (target == null)
        {
            throw new System.InvalidOperationException("Could not find target path in MainMap: " + TargetPath);
        }

        RemoveExistingGeneratedRoot(target);

        Scene kitchenCellScene = EditorSceneManager.OpenScene(KitchenCellPath, OpenSceneMode.Additive);
        GameObject tempSourceRoot = BuildTemporarySourceRoot(kitchenCellScene);
        GameObject copiedRoot = Object.Instantiate(tempSourceRoot);
        copiedRoot.name = GeneratedRootName;
        SceneManager.MoveGameObjectToScene(copiedRoot, mainMapScene);
        copiedRoot.transform.SetParent(target, false);
        copiedRoot.transform.localPosition = Vector3.zero;
        copiedRoot.transform.localRotation = Quaternion.identity;
        copiedRoot.transform.localScale = Vector3.one;

        RestoreTemporarySourceRoot(tempSourceRoot);
        Object.DestroyImmediate(tempSourceRoot);

        EditorSceneManager.CloseScene(kitchenCellScene, true);
        EditorSceneManager.MarkSceneDirty(mainMapScene);
        EditorSceneManager.SaveScene(mainMapScene);
        AssetDatabase.Refresh();

        Debug.Log("Copied KitchenCell gameplay objects into MainMap at " + TargetPath + "/" + GeneratedRootName + ".");
    }

    private static Scene OpenOrGetScene(string scenePath, OpenSceneMode mode)
    {
        Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            return loadedScene;
        }

        return EditorSceneManager.OpenScene(scenePath, mode);
    }

    private static Transform FindScenePath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        Transform current = null;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == parts[0])
            {
                current = roots[i].transform;
                break;
            }
        }

        for (int i = 1; current != null && i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
        }

        return current;
    }

    private static void RemoveExistingGeneratedRoot(Transform target)
    {
        Transform existing = target.Find(GeneratedRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static GameObject BuildTemporarySourceRoot(Scene sourceScene)
    {
        GameObject tempRoot = new GameObject("__KitchenCellCopySource");
        SceneManager.MoveGameObjectToScene(tempRoot, sourceScene);

        GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
        List<Transform> rootsToCopy = new List<Transform>();
        for (int i = 0; i < sourceRoots.Length; i++)
        {
            GameObject sourceRoot = sourceRoots[i];
            if (sourceRoot == tempRoot || ExcludedSourceRoots.Contains(sourceRoot.name))
            {
                continue;
            }

            rootsToCopy.Add(sourceRoot.transform);
        }

        for (int i = 0; i < rootsToCopy.Count; i++)
        {
            rootsToCopy[i].SetParent(tempRoot.transform, true);
        }

        return tempRoot;
    }

    private static void RestoreTemporarySourceRoot(GameObject tempRoot)
    {
        while (tempRoot.transform.childCount > 0)
        {
            Transform child = tempRoot.transform.GetChild(0);
            child.SetParent(null, true);
        }
    }
}
