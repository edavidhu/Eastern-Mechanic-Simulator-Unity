using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("A Nagy Táska")]
    public GameObject inventoryPanel;
    public bool isInventoryOpen = false;

    [Header("Adatok és UI")]
    public ItemData[] slots = new ItemData[35]; 
    public int[] slotAmounts = new int[35]; 
    
    public Image[] slotIcons = new Image[35];   
    public Image[] slotBackgrounds = new Image[35]; 
    public TextMeshProUGUI[] slotAmountTexts = new TextMeshProUGUI[35]; 

    [Header("Kijelölés és Eldobás")]
    public int selectedSlot = 0;        
    public Transform dropSpawnPoint;    

    [Header("Egéren lévő Tárgy")]
    public ItemData mouseItem;          
    public int mouseItemAmount = 0;     
    public Image mouseItemIcon;         
    public TextMeshProUGUI mouseAmountText; 
    
    [HideInInspector] public int hoveredSlot = -1; 
    private int originalSlot = -1;                 

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (mouseItemIcon != null) mouseItemIcon.color = Color.clear;
        if (mouseAmountText != null) mouseAmountText.enabled = false;

        InventorySlot[] allSlotScripts = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] != null)
            {
                InventorySlot slotScript = slotBackgrounds[i].GetComponent<InventorySlot>();
                if (slotScript != null) slotScript.slotIndex = i;
            }
        }
        UpdateHotbarUI();
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame)
            ToggleInventory();

        if (mouseItem != null && mouseItemIcon != null && isInventoryOpen)
        {
            mouseItemIcon.transform.position = Mouse.current.position.ReadValue();
        }

        PickupSystem ps = FindAnyObjectByType<PickupSystem>();
        if (ps != null && ps.heldObject != null) return;

        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll > 0.1f) { selectedSlot--; if (selectedSlot < 0) selectedSlot = 6; UpdateHotbarUI(); }
        else if (scroll < -0.1f) { selectedSlot++; if (selectedSlot > 6) selectedSlot = 0; UpdateHotbarUI(); }

        // Q betűs eldobás (csak ha nincs nyitva a táska)
        if (Keyboard.current.qKey.wasPressedThisFrame && !isInventoryOpen && mouseItem == null) 
            DropSelectedItem();
    }

    // --- 1. A SIMA BAL KLIKK LOGIKA (Felvétel / Lerakás / Csere) ---
    public void LeftClickSlot(int index)
    {
        if (!isInventoryOpen) return;

        // A) Üres az egerünk -> Felvesszük a zsebből a tárgyat
        if (mouseItem == null)
        {
            if (slots[index] != null)
            {
                originalSlot = index; 
                mouseItem = slots[index];
                mouseItemAmount = slotAmounts[index];
                
                slots[index] = null; 
                slotAmounts[index] = 0;
            }
        }
        // B) Van valami az egerünkön -> Lerakjuk a zsebbe
        else
        {
            // Összeolvadás (Merge) - ha ugyanaz a tárgy
            if (slots[index] == mouseItem && slotAmounts[index] < mouseItem.maxStack)
            {
                int spaceLeft = mouseItem.maxStack - slotAmounts[index];
                if (mouseItemAmount <= spaceLeft)
                {
                    slotAmounts[index] += mouseItemAmount;
                    ClearMouseItem();
                }
                else
                {
                    slotAmounts[index] = mouseItem.maxStack;
                    mouseItemAmount -= spaceLeft;
                }
            }
            // Sima lerakás (vagy Helycsere, ha van ott más)
            else
            {
                ItemData tempItem = slots[index];
                int tempAmount = slotAmounts[index];

                slots[index] = mouseItem;
                slotAmounts[index] = mouseItemAmount;

                if (tempItem != null)
                {
                    mouseItem = tempItem;
                    mouseItemAmount = tempAmount;
                }
                else ClearMouseItem();
            }
        }

        UpdateHotbarUI();
    }

    // --- 2. JOBB KLIKK LOGIKA (Felezés / 1 db Letétele) ---
    public void RightClickSlot(int index)
    {
        if (!isInventoryOpen) return;

        // Üres az egér, zsebben van valami -> Felezés
        if (mouseItem == null && slots[index] != null)
        {
            int totalAmount = slotAmounts[index];
            if (totalAmount == 1)
            {
                LeftClickSlot(index); // Ha csak 1 van, simán felvesszük
                return;
            }

            int half = totalAmount / 2;
            mouseItem = slots[index];
            mouseItemAmount = half;
            slotAmounts[index] -= half;
        }
        // Egeren van valami, zseb üres vagy ugyanaz -> 1 db lerakása
        else if (mouseItem != null)
        {
            if (slots[index] == null)
            {
                slots[index] = mouseItem;
                slotAmounts[index] = 1;
                mouseItemAmount--;
                if (mouseItemAmount <= 0) ClearMouseItem();
            }
            else if (slots[index] == mouseItem && slotAmounts[index] < mouseItem.maxStack)
            {
                slotAmounts[index]++;
                mouseItemAmount--;
                if (mouseItemAmount <= 0) ClearMouseItem();
            }
        }

        UpdateHotbarUI();
    }

    // --- 3. DUPLA KLIKK (Mindent felszív az egérre) ---
// --- 3. DUPLA KLIKK (Mindent felszív az egérre) ---
    // --- 3. DUPLA KLIKK (Mindent felszív az egérre) ---
    public void DoubleClickSlot(int index)
    {
        Debug.Log($"<color=yellow>--- DOUBLE CLICK INDUL (Zseb: {index}) ---</color>");
        
        if (!isInventoryOpen) return;

        ItemData targetItem = null;

        // Az 1. kattintás már lefutott, így a tárgy vagy a zsebben, vagy az egeren van!
        if (mouseItem != null) 
        {
            targetItem = mouseItem;
            Debug.Log($"<color=orange>1. Kattintás miatt már az egeren van: {targetItem.itemName}</color>");
            
            if (slots[index] == targetItem)
            {
                mouseItemAmount += slotAmounts[index];
                slots[index] = null;
                slotAmounts[index] = 0;
            }
        }
        else if (slots[index] != null) 
        {
            targetItem = slots[index];
            mouseItem = targetItem;
            mouseItemAmount = slotAmounts[index];
            slots[index] = null;
            slotAmounts[index] = 0;
            Debug.Log($"<color=orange>A zsebből vettük fel: {targetItem.itemName}</color>");
        }

        if (targetItem == null) 
        {
            Debug.Log("<color=red>HIBA: Nincs mit felszívni (Egér és Zseb is üres)!</color>");
            return;
        }

        mouseItemIcon.sprite = mouseItem.icon;
        mouseItemIcon.color = Color.white;
        mouseItemIcon.enabled = true;

        int gatheredCount = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == targetItem)
            {
                int spaceLeft = targetItem.maxStack - mouseItemAmount;
                if (spaceLeft <= 0) break; 

                int amountToTake = Mathf.Min(spaceLeft, slotAmounts[i]);
                mouseItemAmount += amountToTake;
                slotAmounts[i] -= amountToTake;
                gatheredCount += amountToTake;

                if (slotAmounts[i] <= 0) slots[i] = null;
            }
        }

        Debug.Log($"<color=green>SIKER: Felszívtunk még {gatheredCount} darabot! Összesen az egeren: {mouseItemAmount}</color>");
        UpdateHotbarUI();
    }

    // --- 4. SHIFT + KLIKK (Gyorspakolás) ---
    public void ShiftClickSlot(int index)
    {
        if (!isInventoryOpen || slots[index] == null || mouseItem != null) return;

        ItemData itemToMove = slots[index];
        int amountToMove = slotAmounts[index];
        int startIdx = (index <= 6) ? 7 : 0;
        int endIdx = (index <= 6) ? 35 : 7;

        for (int i = startIdx; i < endIdx; i++)
        {
            if (slots[i] == itemToMove && slotAmounts[i] < itemToMove.maxStack)
            {
                int spaceLeft = itemToMove.maxStack - slotAmounts[i];
                if (amountToMove <= spaceLeft) { slotAmounts[i] += amountToMove; slots[index] = null; slotAmounts[index] = 0; UpdateHotbarUI(); return; }
                else { slotAmounts[i] = itemToMove.maxStack; amountToMove -= spaceLeft; }
            }
        }

        for (int i = startIdx; i < endIdx; i++)
        {
            if (slots[i] == null) { slots[i] = itemToMove; slotAmounts[i] = amountToMove; slots[index] = null; slotAmounts[index] = 0; UpdateHotbarUI(); return; }
        }
        slotAmounts[index] = amountToMove; 
        UpdateHotbarUI();
    }

    // --- 5. CTRL + KLIKK (1 darab felvétele) ---
    public void CtrlClickSlot(int index)
    {
        if (!isInventoryOpen || slots[index] == null) return;

        if (mouseItem == null)
        {
            originalSlot = index;
            mouseItem = slots[index];
            mouseItemAmount = 1;
            slotAmounts[index]--;
            if (slotAmounts[index] <= 0) slots[index] = null;
        }
        else if (mouseItem == slots[index] && mouseItemAmount < mouseItem.maxStack)
        {
            mouseItemAmount++;
            slotAmounts[index]--;
            if (slotAmounts[index] <= 0) slots[index] = null;
        }
        UpdateHotbarUI();
    }

    private void ClearMouseItem()
    {
        mouseItem = null;
        mouseItemAmount = 0;
        if (mouseItemIcon != null) { mouseItemIcon.sprite = null; mouseItemIcon.color = Color.clear; }
        if (mouseAmountText != null) mouseAmountText.enabled = false;
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(isInventoryOpen);

        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (mouseItem != null)
            {
                if (slots[originalSlot] == null || (slots[originalSlot] == mouseItem && slotAmounts[originalSlot] + mouseItemAmount <= mouseItem.maxStack))
                {
                    slots[originalSlot] = mouseItem;
                    slotAmounts[originalSlot] += mouseItemAmount;
                }
                else if (!AddItem(mouseItem, mouseItemAmount))
                {
                    if (mouseItem.dropPrefab != null && dropSpawnPoint != null)
                        for (int i = 0; i < mouseItemAmount; i++) Instantiate(mouseItem.dropPrefab, dropSpawnPoint.position + dropSpawnPoint.forward * 1.5f + (Vector3.up * i * 0.5f), dropSpawnPoint.rotation);
                }
                ClearMouseItem();   
                UpdateHotbarUI();   
            }
        }
    }

    public bool AddItem(ItemData itemToAdd, int amount = 1)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == itemToAdd && slotAmounts[i] < itemToAdd.maxStack)
            {
                slotAmounts[i] += amount; UpdateHotbarUI(); return true; 
            }
        }
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) { slots[i] = itemToAdd; slotAmounts[i] = amount; UpdateHotbarUI(); return true; }
        }
        return false; 
    }

    private void DropSelectedItem()
    {
        if (slots[selectedSlot] != null)
        {
            PickupSystem ps = FindAnyObjectByType<PickupSystem>();
            if (ps != null && ps.heldObject != null) return; 

            ItemData itemToDrop = slots[selectedSlot];
            if (itemToDrop.dropPrefab != null && ps != null)
            {
                GameObject spawnedItem = Instantiate(itemToDrop.dropPrefab, ps.holdPosition.position, ps.playerCamera.rotation);
                ps.EquipObjectFromHotbar(spawnedItem);
            }
            slotAmounts[selectedSlot]--; 
            if (slotAmounts[selectedSlot] <= 0) slots[selectedSlot] = null; 
            UpdateHotbarUI();
        }
    }

    private void UpdateHotbarUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slotIcons[i].sprite = slots[i].icon;
                slotIcons[i].color = new Color(1f, 1f, 1f, 1f); 
                slotIcons[i].enabled = true;
                if (slotAmountTexts.Length > i && slotAmountTexts[i] != null)
                {
                    if (slotAmounts[i] > 1) { slotAmountTexts[i].text = slotAmounts[i].ToString(); slotAmountTexts[i].enabled = true; }
                    else slotAmountTexts[i].enabled = false;
                }
            }
            else
            {
                slotIcons[i].sprite = null;
                slotIcons[i].enabled = false;
                if (slotAmountTexts.Length > i && slotAmountTexts[i] != null) slotAmountTexts[i].enabled = false;
            }

            if (slotBackgrounds.Length > i && slotBackgrounds[i] != null)
            {
                slotBackgrounds[i].color = new Color(0.2f, 0.2f, 0.2f, 0.7f); 
                Outline outline = slotBackgrounds[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == selectedSlot && i <= 6);
            }
        }

        if (mouseItemIcon != null)
        {
            if (mouseItem != null)
            {
                mouseItemIcon.sprite = mouseItem.icon;
                mouseItemIcon.color = Color.white;
            }
            else
            {
                mouseItemIcon.sprite = null;
                mouseItemIcon.color = Color.clear;
            }
        }

        if (mouseAmountText != null)
        {
            if (mouseItem != null && mouseItemAmount > 1) { mouseAmountText.text = mouseItemAmount.ToString(); mouseAmountText.enabled = true; }
            else mouseAmountText.enabled = false;
        }
    }
}