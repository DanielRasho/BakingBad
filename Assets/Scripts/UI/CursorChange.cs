using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class CursorChange : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Events")]
    [SerializeField] private CursorManager.CursorType cursorOnHover = CursorManager.CursorType.Pointer;

    private bool isOnHover = false;
    private CursorManager.CursorType previousCursor = CursorManager.CursorType.Default;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("UWUWUWUWU");
        previousCursor = CursorManager.Instance.GetCursorType();
        CursorManager.Instance.SetCursor(cursorOnHover);
        isOnHover = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorManager.Instance.SetCursor(previousCursor);
        isOnHover = false;
    }

    public void OnDestroy()
    {
        if (isOnHover)
        {
            CursorManager.Instance.SetCursor(previousCursor);
        }
    }
    
    public void OnDisable()
    {
        if (isOnHover)
        {
            CursorManager.Instance.SetCursor(previousCursor);
        }
    }
}
