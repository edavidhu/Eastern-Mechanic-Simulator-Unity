using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InventorySlot : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int slotIndex;

    // --- ÚJ: Saját Dupla-Klikk időzítő! ---
    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.3f; // 0.3 másodpercen belüli kattintás számít duplának!

    public void OnPointerDown(PointerEventData eventData)
    {
        if (InventoryManager.Instance == null) return;

        // BAL KLIKK
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // Kiszámoljuk, mennyi idő telt el az előző kattintás óta
            float timeSinceLastClick = Time.unscaledTime - lastClickTime;
            lastClickTime = Time.unscaledTime; // Lementjük a mostani kattintás idejét!

            // SAJÁT DUPLA KLIKK ÉRZÉKELÉSE
            if (timeSinceLastClick <= doubleClickThreshold) 
            {
                Debug.Log("<color=yellow>-> Saját Dupla klikk érzékelve!</color>");
                InventoryManager.Instance.DoubleClickSlot(slotIndex);
            }
            else if (Keyboard.current.shiftKey.isPressed) 
                InventoryManager.Instance.ShiftClickSlot(slotIndex);
            else if (Keyboard.current.ctrlKey.isPressed)
                InventoryManager.Instance.CtrlClickSlot(slotIndex);
            else 
                InventoryManager.Instance.LeftClickSlot(slotIndex);
        }
        // JOBB KLIKK
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            InventoryManager.Instance.RightClickSlot(slotIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.hoveredSlot = slotIndex;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.hoveredSlot == slotIndex)
            InventoryManager.Instance.hoveredSlot = -1;
    }
}