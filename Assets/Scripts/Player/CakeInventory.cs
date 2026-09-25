using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public class CakeInventoryItem
{
    public string orderId;
    public string orderTitle;
    public string cellNumber;
    public int payoutValue;
    public Sprite icon;
}

// Holds the finished cakes the player is carrying. Slots are selected with 1-N or the mouse wheel,
// and the selected cake is the one offered when talking to a prisoner.
[DisallowMultipleComponent]
public class CakeInventory : MonoBehaviour
{
    [SerializeField] private int slotCount = 5;

    private CakeInventoryItem[] slots;
    private int selectedIndex;
    private PrisonCookPlayerController player;

    public event Action Changed;

    public int SlotCount
    {
        get { EnsureSlots(); return slots.Length; }
    }

    public int SelectedIndex
    {
        get { return selectedIndex; }
    }

    public CakeInventoryItem SelectedItem
    {
        get { return GetItem(selectedIndex); }
    }

    public bool IsFull
    {
        get { return FindFreeSlot() < 0; }
    }

    private void Awake()
    {
        EnsureSlots();
        player = GetComponent<PrisonCookPlayerController>();
    }

    private void Update()
    {
        if (player != null && player.InputLocked)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < SlotCount && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Select(i);
                    return;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
            {
                Select((selectedIndex - 1 + SlotCount) % SlotCount);
            }
            else if (scroll < -0.01f)
            {
                Select((selectedIndex + 1) % SlotCount);
            }
        }
#endif
    }

    public CakeInventoryItem GetItem(int index)
    {
        EnsureSlots();
        return index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public void Select(int index)
    {
        EnsureSlots();
        int clamped = Mathf.Clamp(index, 0, slots.Length - 1);
        if (clamped == selectedIndex)
        {
            return;
        }

        selectedIndex = clamped;
        NotifyChanged();
    }

    public bool TryAdd(CakeInventoryItem item)
    {
        int freeSlot = FindFreeSlot();
        if (item == null || freeSlot < 0)
        {
            return false;
        }

        slots[freeSlot] = item;
        // Selecting the new cake means the next delivery offers it without extra input.
        selectedIndex = freeSlot;
        NotifyChanged();
        return true;
    }

    public CakeInventoryItem RemoveSelected()
    {
        CakeInventoryItem item = SelectedItem;
        if (item == null)
        {
            return null;
        }

        slots[selectedIndex] = null;
        NotifyChanged();
        return item;
    }

    private int FindFreeSlot()
    {
        EnsureSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != Mathf.Max(1, slotCount))
        {
            slots = new CakeInventoryItem[Mathf.Max(1, slotCount)];
            selectedIndex = 0;
        }
    }

    private void NotifyChanged()
    {
        if (Changed != null)
        {
            Changed.Invoke();
        }
    }
}
