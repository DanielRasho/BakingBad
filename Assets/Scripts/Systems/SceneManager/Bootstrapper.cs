using System;
using UnityEngine;

public class BootStrapper : MonoBehaviour
{
    public static BootStrapper Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(Instance);
        }
        else
        {
            Instance = this;
        }
        
        DontDestroyOnLoad(gameObject);
    }
}
