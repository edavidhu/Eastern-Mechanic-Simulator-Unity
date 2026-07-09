using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Mozgás és Sebesség")]
    public float walkSpeed = 5.0f;
    public float sprintSpeed = 8.0f; 
    public float crouchSpeed = 2.5f; // ÚJ: Guggolós sebesség
    public float jumpHeight = 1.5f;
    public float gravity = -15.0f; 

    [Header("Guggolás")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;

    [Header("Kamera")]
    public Transform playerCamera;
    public float mouseSensitivity = 0.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private float originalWalkSpeed;
    private float originalSprintSpeed;
    private float originalCrouchSpeed;
    
    private PickupSystem pickupSystem; 

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        controller = GetComponent<CharacterController>();
        pickupSystem = GetComponent<PickupSystem>(); 
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        originalWalkSpeed = walkSpeed;
        originalSprintSpeed = sprintSpeed;
        originalCrouchSpeed = crouchSpeed;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        HandleLook();
        HandleMovement();
    }

private void HandleLook()
    {
        // VÉDELMEK: Ha forgatunk valamit, ha nyitva a táska, VAGY ha ajtót húzunk -> Ne forogjon a fejünk!
        if (pickupSystem != null && pickupSystem.isRotatingObject) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) return;
        if (pickupSystem != null && pickupSystem.isDraggingDoor) return;

        // Egér mozgásának beolvasása (Ez volt kétszer neked!)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // Fel/le nézés
        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Jobbra/balra nézés
        transform.Rotate(Vector3.up * mouseDelta.x);
    }

    private void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        // --- ÚJ: GUGGOLÁS LOGIKA (C betű vagy Bal CTRL) ---
        bool isCrouching = Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
        
        // Magasság állítása folyamatosan (puhán)
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * 10f);

        // Kamera magasságának igazítása a testhez
        Vector3 camPos = playerCamera.localPosition;
        camPos.y = (controller.height / 2f) - 0.2f; // Mindig a feje búbjánál legyen a kamera
        playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, camPos, Time.deltaTime * 10f);

        // Mozgás beolvasása
        float x = 0f; float z = 0f;
        if (Keyboard.current.wKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed) z -= 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;

        Vector3 move = transform.right * x + transform.forward * z;
        
        // Sebesség kiválasztása (Guggol, Fut, vagy Sétál?)
        float currentSpeed = walkSpeed;
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (Keyboard.current.leftShiftKey.isPressed) currentSpeed = sprintSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);

        // Ugrás (Space) - Csak ha nem guggolunk!
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // A súly-büntetést ráhúztuk a guggolásra is!
    public void ApplyWeightPenalty(float mass)
    {
        if (mass <= 0) 
        { 
            walkSpeed = originalWalkSpeed; 
            sprintSpeed = originalSprintSpeed; 
            crouchSpeed = originalCrouchSpeed;
            return; 
        }

        float penalty = Mathf.Clamp(mass / 50f, 0f, 0.7f); 
        walkSpeed = originalWalkSpeed * (1f - penalty);
        sprintSpeed = originalSprintSpeed * (1f - penalty);
        crouchSpeed = originalCrouchSpeed * (1f - penalty);
    }

     private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Ha nincs rajta Rigidbody, vagy le van fagyasztva (Kinematic), nem lökdöshetjük
        if (body == null || body.isKinematic) return;

        // Nem akarjuk a padlót lefelé tolni
        if (hit.moveDirection.y < -0.3f) return;

        // Kiszámoljuk a lökés erejét a sétálásunk iránya alapján
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        
        // Adunk neki egy szép löketet!
        body.linearVelocity += pushDir * 2f * Time.deltaTime;
    }
}