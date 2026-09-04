using UnityEngine;
using System;


public class CursorManager : MonoBehaviour
{
    [Header("Cursor Types")]
    [SerializeField] private CursorData defaultCursor;
    [SerializeField] private CursorData pointerCursor;

    public static CursorManager Instance { get; private set; }

    public enum CursorType
    {
        Default,
        Pointer
    }

    [Serializable]
    public class CursorData
    {
        public Texture2D texture;
        public Vector2 hotspot = Vector2.zero;
        public CursorMode cursorMode = CursorMode.Auto;
    }

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetCursor(CursorType.Default);
    }

    /// <summary>
    /// Changes the cursor based on the enum type.
    /// </summary>
    public void SetCursor(CursorType type)
    {
        CursorData cursorData = GetCursorData(type);

        if (cursorData == null || cursorData.texture == null)
        {
            Debug.LogWarning($"Cursor '{type}' is missing a texture.");
            return;
        }

        Cursor.SetCursor(
            cursorData.texture,
            cursorData.hotspot,
            cursorData.cursorMode
        );
    }

    private CursorData GetCursorData(CursorType type)
    {
        return type switch
        {
            CursorType.Default => defaultCursor,
            CursorType.Pointer => pointerCursor,
            _ => null
        };
    }
}
