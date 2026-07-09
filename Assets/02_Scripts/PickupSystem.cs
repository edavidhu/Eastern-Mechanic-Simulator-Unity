using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro; 

public class PickupSystem : MonoBehaviour
{
    [Header("Beállítások")]
    public Transform playerCamera;   
    public Transform holdPosition;   
    public float pickupRange = 3f;   

    [Header("Forgatás és Mozgatás")]
    public float rotationSensitivity = 0.5f; 
    public float panSensitivity = 0.01f;     
    public float scrollSensitivity = 0.2f;   
    public float minDistance = 1.0f;         
    public float maxDistance = 4.0f;         
    public float throwForce = 15f;           

    [Header("Súly és UI")]
    public Image crosshair;          
    public GameObject statePickup;
    public GameObject stateHolding;
    public GameObject stateTooHeavy;
    public TextMeshProUGUI promptText; 

    public FPSController fpsController; 
    public float heavyThreshold = 15f;  
    public float maxWeight = 40f;       

    [Header("Fizika Javítások")]
    public Collider playerCollider; 

    [HideInInspector] public GameObject heldObject;   
    private Rigidbody heldObjectRb;  
    private Quaternion localHoldRotation; 
    private float origLinearDamping, origAngularDamping; 
    
    private Vector3 defaultHoldLocalPos; 
    private ItemObject currentItemObj; 
    
    // --- ÚJ: Itt tároljuk a kézben lévő tárgy collidereit, hogy szellemmé tegyük őket! ---
    private Collider[] heldObjectColliders;

    [HideInInspector] public bool isRotatingObject = false;
    [HideInInspector] public bool isDraggingDoor = false;
    private CarDoor draggedDoor = null; 

    private SnapPoint currentHoveredSnapPoint;

    void Start()
    {
        if (holdPosition != null) defaultHoldLocalPos = holdPosition.localPosition;
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen)
        {
            if (crosshair != null) crosshair.color = Color.clear;
            if (currentHoveredSnapPoint != null) { currentHoveredSnapPoint.ShowHologram(false); currentHoveredSnapPoint = null; }
            return; 
        }

        UpdateUI(); 

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (heldObject == null) 
            {
                Ray ray = new Ray(playerCamera.position, playerCamera.forward);
                RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); 

                bool interactedWithDoor = false;

                foreach (var hit in hits)
                {
                    CarDoor door = hit.collider.GetComponentInParent<CarDoor>();
                    if (door != null && door.gameObject.CompareTag("AttachedPart"))
                    {
                        if (!door.isLocked)
                        {
                            door.Unlatch(); 
                            door.BeginDrag();
                            isDraggingDoor = true;
                            draggedDoor = door;
                            interactedWithDoor = true;
                            break; 
                        }
                    }
                }
                
                if (!interactedWithDoor) TryPickupObject();
            }
            else 
            {
                if (currentHoveredSnapPoint != null && !currentHoveredSnapPoint.isInstalled)
                {
                    ItemObject io = heldObject.GetComponent<ItemObject>();
                    if (IsPartAcceptedForSnapPoint(io, currentHoveredSnapPoint))
                    {
                        GameObject objToInstall = heldObject; 
                        DropObject(); 
                        currentHoveredSnapPoint.InstallPart(objToInstall);
                        return; 
                    }
                }
                DropObject();
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (isDraggingDoor && draggedDoor != null)
            {
                draggedDoor.EndDrag();
                draggedDoor.ReleaseDoor();
            }
            isDraggingDoor = false;
            draggedDoor = null;
        }

        if (isDraggingDoor && draggedDoor != null)
        {
            if (draggedDoor.isLatched)
            {
                draggedDoor.EndDrag();
                isDraggingDoor = false;
                draggedDoor = null;
            }
            else
            {
                float mouseX = Mouse.current.delta.x.ReadValue();
                float mouseY = Mouse.current.delta.y.ReadValue(); 

                if (fpsController != null && !fpsController.enabled) 
                {
                    mouseX = -mouseX; 
                    mouseY = -mouseY; 
                }
                
                draggedDoor.MoveDoor(mouseX, mouseY);
            }
        }

        if (Keyboard.current.fKey.wasPressedThisFrame && heldObject == null && fpsController.enabled)
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                ItemObject io = hit.collider.GetComponentInParent<ItemObject>();
                if (io != null && io.gameObject.CompareTag("AttachedPart") && io.currentSnapPoint != null)
                {
                    GameObject uninstalledPart = io.currentSnapPoint.UninstallPart();
                    if (uninstalledPart != null) EquipObjectFromHotbar(uninstalledPart); 
                    break; 
                }
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            
            bool enteredCar = false;

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("CarSeat"))
                {
                    CarSeat seat = hit.collider.GetComponent<CarSeat>();
                    if (seat != null) 
                    { 
                        if (heldObject != null) DropObject(); 
                        seat.EnterCar(fpsController.gameObject); 
                        enteredCar = true; 
                        break; 
                    }
                }
            }

            if (!enteredCar) TryStoreObjectInInventory();
        }

        if (heldObject != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame && !Mouse.current.middleButton.isPressed) ThrowObject();
            if (Mouse.current.middleButton.isPressed) { isRotatingObject = true; if (Mouse.current.rightButton.isPressed) PanTarget(); else RotateTarget(); }
            else isRotatingObject = false;
            AdjustHoldDistance();
        }
        else isRotatingObject = false;
    }

    void FixedUpdate()
    {
        if (heldObjectRb != null && !heldObjectRb.isKinematic)
        {
            Vector3 offsetPos = Vector3.zero;
            Quaternion offsetRot = Quaternion.identity;

            if (currentItemObj != null)
            {
                offsetPos = currentItemObj.holdPositionOffset;
                offsetRot = Quaternion.Euler(currentItemObj.holdRotationOffset);
            }

            Vector3 targetPos = holdPosition.position - (heldObject.transform.rotation * offsetPos);
            Vector3 moveDir = targetPos - heldObject.transform.position;
            
            heldObjectRb.linearVelocity = moveDir * 10f; 

            Quaternion targetRotation = playerCamera.rotation * localHoldRotation * offsetRot;
            heldObjectRb.MoveRotation(Quaternion.Slerp(heldObject.transform.rotation, targetRotation, Time.fixedDeltaTime * 15f));
        }
    }

    public void EquipObjectFromHotbar(GameObject newObj)
    {
        if (heldObject != null) return; 

        Rigidbody rb = newObj.GetComponent<Rigidbody>();
        if (rb == null) return;

        Joint[] joints = newObj.GetComponents<Joint>();
        foreach (Joint j in joints) Destroy(j);

        heldObject = newObj;
        heldObjectRb = rb;
        currentItemObj = heldObject.GetComponent<ItemObject>();

        // AZONNALI TELEPORT (Hogy ne húzza át a kocsin)
        Vector3 offsetPos = currentItemObj != null ? currentItemObj.holdPositionOffset : Vector3.zero;
        heldObject.transform.position = holdPosition.position - (playerCamera.rotation * offsetPos);

        origLinearDamping = heldObjectRb.linearDamping;
        origAngularDamping = heldObjectRb.angularDamping;

        heldObjectRb.isKinematic = false; 
        heldObjectRb.useGravity = false;
        heldObjectRb.linearDamping = 5f;  
        heldObjectRb.angularDamping = 5f;

        holdPosition.localPosition = defaultHoldLocalPos;
        localHoldRotation = Quaternion.identity; 

        if (fpsController != null) fpsController.ApplyWeightPenalty(rb.mass);

        // --- A TE ÖTLETED: SZELLEM MÓD BEKAPCSOLÁSA ---
        heldObjectColliders = heldObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in heldObjectColliders)
        {
            if (col != playerCollider) col.isTrigger = true; // Átmegy mindenen!
        }
    }

    private void TryPickupObject()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            ItemObject io = hit.collider.GetComponentInParent<ItemObject>();
            if (io != null && io.gameObject.CompareTag("Pickupable"))
            {
                Rigidbody rb = io.GetComponent<Rigidbody>();
                if (rb == null || rb.mass > maxWeight) return; 

                Joint[] joints = io.gameObject.GetComponents<Joint>();
                foreach (Joint j in joints) Destroy(j);

                heldObject = io.gameObject;
                heldObjectRb = rb;
                currentItemObj = io;

                // AZONNALI TELEPORT
                Vector3 offsetPos = currentItemObj != null ? currentItemObj.holdPositionOffset : Vector3.zero;
                heldObject.transform.position = holdPosition.position - (playerCamera.rotation * offsetPos);

                origLinearDamping = heldObjectRb.linearDamping;
                origAngularDamping = heldObjectRb.angularDamping;

                heldObjectRb.isKinematic = false;
                heldObjectRb.useGravity = false;
                heldObjectRb.linearDamping = 5f;  
                heldObjectRb.angularDamping = 5f;

                holdPosition.localPosition = defaultHoldLocalPos;
                localHoldRotation = Quaternion.identity;
                
                if (fpsController != null && rb != null) fpsController.ApplyWeightPenalty(rb.mass);

                // --- A TE ÖTLETED: SZELLEM MÓD BEKAPCSOLÁSA ---
                heldObjectColliders = heldObject.GetComponentsInChildren<Collider>();
                foreach (Collider col in heldObjectColliders)
                {
                    if (col != playerCollider) col.isTrigger = true; // Átmegy mindenen!
                }
                
                break;
            }
        }
    }

    private void DropObject()
    {
        // --- A TE ÖTLETED: SZELLEM MÓD KIKAPCSOLÁSA (Újra szilárd lesz) ---
        if (heldObjectColliders != null)
        {
            foreach (Collider col in heldObjectColliders)
            {
                if (col != null && col != playerCollider) col.isTrigger = false;
            }
            heldObjectColliders = null;
        }

        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.useGravity = true;
            heldObjectRb.linearDamping = origLinearDamping;
            heldObjectRb.angularDamping = origAngularDamping;
        }

        heldObject = null;
        heldObjectRb = null;
        currentItemObj = null;

        if (fpsController != null) fpsController.ApplyWeightPenalty(0f);
    }

    private void ThrowObject()
    {
        Rigidbody rbToThrow = heldObjectRb;
        DropObject(); // Itt már visszanyeri a szilárdságát
        if (rbToThrow != null) rbToThrow.AddForce(playerCamera.forward * throwForce, ForceMode.Impulse); // Aztán repül
    }

    private void TryStoreObjectInInventory()
    {
        GameObject targetObj = null;

        if (heldObject != null) targetObj = heldObject;
        else
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                ItemObject io = hit.collider.GetComponentInParent<ItemObject>();
                if (io != null && io.gameObject.CompareTag("Pickupable"))
                {
                    targetObj = io.gameObject;
                    break;
                }
            }
        }

        if (targetObj != null)
        {
            ItemObject itemObj = targetObj.GetComponent<ItemObject>();
            if (itemObj != null && itemObj.itemData != null)
            {
                bool storedSuccessfully = InventoryManager.Instance.AddItem(itemObj.itemData);
                if (storedSuccessfully)
                {
                    if (heldObject == targetObj) DropObject(); 
                    Destroy(targetObj); 
                }
            }
        }
    }

    private void RotateTarget()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * rotationSensitivity;
        localHoldRotation = Quaternion.AngleAxis(-mouseDelta.x, Vector3.up) * localHoldRotation;
        localHoldRotation = Quaternion.AngleAxis(mouseDelta.y, Vector3.right) * localHoldRotation;
    }

    private void PanTarget()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * panSensitivity;
        Vector3 localPos = holdPosition.localPosition;
        
        localPos.x += mouseDelta.x;
        localPos.y += mouseDelta.y;

        localPos.x = Mathf.Clamp(localPos.x, -2f, 2f);
        localPos.y = Mathf.Clamp(localPos.y, -2f, 2f);

        holdPosition.localPosition = localPos;
    }

    private void AdjustHoldDistance()
    {
        float scroll = Mouse.current.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) > 0.1f) 
        {
            float direction = Mathf.Sign(scroll); 
            Vector3 localPos = holdPosition.localPosition;
            localPos.z += direction * scrollSensitivity;
            localPos.z = Mathf.Clamp(localPos.z, minDistance, maxDistance);
            holdPosition.localPosition = localPos;
        }
    }

    private void UpdateUI()
    {
        if (crosshair == null) return;
        
        if (statePickup != null) statePickup.SetActive(false);
        if (stateHolding != null) stateHolding.SetActive(false);
        if (stateTooHeavy != null) stateTooHeavy.SetActive(false);
        if (promptText != null) promptText.text = "";

        if (currentHoveredSnapPoint != null) 
        {
            currentHoveredSnapPoint.ShowHologram(false);
            currentHoveredSnapPoint = null;
        }

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        string finalPrompt = "";
        Color finalCrossColor = Color.white;
        bool colorSet = false;
        bool foundSolidObj = false;

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("CarSeat"))
            {
                finalPrompt += "[E] Beszállás\n";
                if (!colorSet) { finalCrossColor = Color.green; colorSet = true; }
            }

            if (foundSolidObj) continue; 

            ItemObject attachedIo = hit.collider.GetComponentInParent<ItemObject>();
            if (attachedIo != null && heldObject == null && !isDraggingDoor && attachedIo.gameObject.CompareTag("AttachedPart"))
            {
                CarDoor door = attachedIo.GetComponent<CarDoor>();
                if (door != null)
                {
                    if (door.isLocked) { finalPrompt += "<color=red>Zárva</color>\n[F] Leszerelés\n"; if (!colorSet) { finalCrossColor = Color.red; colorSet = true; } }
                    else
                    {
                        if (door.isLatched) finalPrompt += "[Bal Klikk] Kinyitás\n[F] Leszerelés\n";
                        else finalPrompt += "[Bal Klikk Húzva] Ajtó Mozgatása\n[F] Leszerelés\n";
                        if (!colorSet) { finalCrossColor = new Color(0.2f, 0.6f, 1f); colorSet = true; }
                    }
                }
                else
                {
                    finalPrompt += "[F] Leszerelés\n";
                    if (!colorSet) { finalCrossColor = new Color(0.2f, 0.6f, 1f); colorSet = true; } 
                }
                foundSolidObj = true;
            }

            else if (hit.collider.CompareTag("SnapPoint"))
            {
                SnapPoint sp = hit.collider.GetComponent<SnapPoint>();
                if (sp != null)
                {
                    currentHoveredSnapPoint = sp;
                    if (!sp.isInstalled && heldObject != null)
                    {
                        ItemObject io = heldObject.GetComponent<ItemObject>();
                        if (IsPartAcceptedForSnapPoint(io, sp))
                        {
                            sp.ShowHologram(true); 
                            finalPrompt += "[Bal Klikk] Bepattintás\n";
                            if (!colorSet) { finalCrossColor = Color.green; colorSet = true; }
                            foundSolidObj = true;
                        }
                        else
                        {
                            finalPrompt += "<color=red>Nem ide való alkatrész</color>\n";
                            if (!colorSet) { finalCrossColor = Color.red; colorSet = true; }
                        }
                    }
                }
            }

            else if (heldObject == null)
            {
                ItemObject ioUI = hit.collider.GetComponentInParent<ItemObject>();
                if (ioUI != null && ioUI.gameObject.CompareTag("Pickupable"))
                {
                    Rigidbody rb = ioUI.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        if (rb.mass > maxWeight) { finalPrompt += "<color=red>Túl nehéz!</color>\n"; if (!colorSet) { finalCrossColor = Color.red; colorSet = true; } }
                        else if (rb.mass > heavyThreshold) { finalPrompt += "[Bal Klikk] Nehéz Felvétel\n"; if (!colorSet) { finalCrossColor = Color.yellow; colorSet = true; } }
                        else { finalPrompt += "[Bal Klikk] Felvétel\n"; if (!colorSet) { finalCrossColor = Color.green; colorSet = true; } }
                        foundSolidObj = true;
                    }
                }
            }
        }

        if (heldObject != null)
        {
            crosshair.color = new Color(1, 1, 1, 0.5f); 
            if (stateHolding != null) stateHolding.SetActive(true); 
        }
        else
        {
            crosshair.color = finalCrossColor;
            if (isDraggingDoor) crosshair.color = Color.clear; 
            else if (promptText != null) promptText.text = finalPrompt;
            
            if (finalPrompt != "" && statePickup != null) statePickup.SetActive(true); 
        }
    }

    private bool IsPartAcceptedForSnapPoint(ItemObject io, SnapPoint sp)
    {
        if (io == null || sp == null || io.itemData == null) return false;
        if (sp.acceptedPart == io.itemData) return true;

        if (sp.acceptedPart != null)
        {
            if (!string.IsNullOrWhiteSpace(sp.acceptedPart.itemName) &&
                !string.IsNullOrWhiteSpace(io.itemData.itemName) &&
                string.Equals(sp.acceptedPart.itemName.Trim(), io.itemData.itemName.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        return io.GetComponent<CarDoor>() != null;
    }
}