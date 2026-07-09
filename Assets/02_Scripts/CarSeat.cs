using UnityEngine;
using UnityEngine.InputSystem;

public class CarSeat : MonoBehaviour
{
    [Header("Beállítások")]
    public ModularCarController carController;
    public Transform driverCameraPosition; 
    public Transform exitPosition;         
    public float mouseSensitivity = 0.2f;

    private GameObject playerObj;
    private CharacterController playerCharCtrl;
    private FPSController playerFpsCtrl;
    
    // ÚJ: Lementjük a PickupSystem-et, hogy tudjunk vele kommunikálni!
    private PickupSystem pickupSystem; 
    
    private Transform mainCamera;
    private Transform originalPlayerParent;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;

    private float xRotation = 0f;
    private float yRotation = 0f;

    private Collider seatCollider; 

    void Awake()
    {
        seatCollider = GetComponent<Collider>();
    }

    public void EnterCar(GameObject player)
    {
        if (carController.isPlayerInside) return;

        playerObj = player;
        playerCharCtrl = player.GetComponent<CharacterController>();
        playerFpsCtrl = player.GetComponent<FPSController>();
        
        // Lementjük a lézer rendszerét
        pickupSystem = player.GetComponent<PickupSystem>(); 
        
        mainCamera = Camera.main.transform;

        originalPlayerParent = player.transform.parent;
        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;

        if (playerCharCtrl != null) playerCharCtrl.enabled = false;
        if (playerFpsCtrl != null) playerFpsCtrl.enabled = false;

        player.transform.parent = driverCameraPosition;
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;

        mainCamera.localPosition = Vector3.zero;
        xRotation = 0f; yRotation = 0f;
        mainCamera.localRotation = Quaternion.identity;

        carController.isPlayerInside = true;
        if (seatCollider != null) seatCollider.enabled = false;
    }

    void Update()
    {
        if (!carController.isPlayerInside) return;

        // --- ÚJ VÉDELEM: HA AJTÓT HÚZUNK VAGY TÁSKÁZUNK, NE MOZOGJON A KAMERA! ---
        bool isDragging = (pickupSystem != null && pickupSystem.isDraggingDoor);
        bool isInventory = (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen);

        // Csak akkor nézelődhetünk, ha nem csinálunk semmi mást
        if (!isInventory && !isDragging)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            xRotation -= mouseDelta.y;
            yRotation += mouseDelta.x;
            xRotation = Mathf.Clamp(xRotation, -60f, 60f); 
            yRotation = Mathf.Clamp(yRotation, -110f, 110f); 
            mainCamera.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }

        // KISZÁLLÁS (F betű)
        // Biztosíték: Csak akkor szállhatunk ki, ha nincs a kezünkben semmi!
        if (Keyboard.current.fKey.wasPressedThisFrame && (pickupSystem == null || pickupSystem.heldObject == null))
        {
            carController.isPlayerInside = false;

            playerObj.transform.parent = originalPlayerParent;
            playerObj.transform.position = exitPosition.position;
            playerObj.transform.rotation = exitPosition.rotation;

            mainCamera.localPosition = originalCameraLocalPos;
            mainCamera.localRotation = Quaternion.identity;

            if (playerCharCtrl != null) playerCharCtrl.enabled = true;
            if (playerFpsCtrl != null) playerFpsCtrl.enabled = true;

            if (seatCollider != null) seatCollider.enabled = true;
        }
    }
}