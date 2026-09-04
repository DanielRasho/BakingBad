using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InputMap {
    PLAYER,
    UI,
    MAP,
    DIALOGUE
}

public class Input_Manager : MonoBehaviour
{
    public static Input_Manager Instance { get; private set; }

    private GameInputActions _actions;

    public GameInputActions Actions => _actions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _actions = new GameInputActions();
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions?.Disable();
    }

    private string GetMapName(InputMap map)
    {
        return map switch
        {
            InputMap.PLAYER => "Player",
            InputMap.UI     => "UI",
            InputMap.MAP    => "Map",
            InputMap.DIALOGUE    => "Dialogue",
            _ => throw new ArgumentOutOfRangeException(nameof(map))
        };
    }

    public void SwitchToMap(InputMap map)
    {
        string mapName = GetMapName(map);

        foreach (var actionMap in _actions.asset.actionMaps)
        {
            if (actionMap.name == mapName)
                actionMap.Enable();
            else
                actionMap.Disable();
        }
    }
}
