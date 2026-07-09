using UnityEngine;

public class CarDoor : MonoBehaviour
{
    [Header("Zsanér Szögek (TE IRÁNYÍTASZ!)")]
    [Tooltip("Általában 0. Ide SOHA nem mehet befelé az ajtó!")]
    public float closedAngle = 0f;  
    [Tooltip("Ide írd a nyitott szöget. (Lehet 70, de akár -70 is)")]
    public float openedAngle = 70f;     

    [Header("Ajtó Állapot")]
    public bool isLocked = false;     
    public bool isLatched = true;     
    public Vector3 hingeAxis = new Vector3(0, 1, 0); 
    public bool isVerticalOpen = false; 

    [Header("Gázteleszkóp (Motorháztető/Csomagtartó)")]
    public bool hasGasStrut = false; 
    [Tooltip("Ha true (motorháztető/csomagtartó), csatolt állapotban gravitáció ki van kapcsolva. Levéve/letörve automatikusan visszakapcsol.")]
    public bool weightlessWhenAttached = false;

    [Header("Egér Húzás és Fizika")]
    public float doorSensitivity = 1f; 
    public float breakForce = 30000f; 
    public float maxMouseTorque = 6f;
    public float maxDoorAngularSpeed = 8f;

    [Header("Stabilitás")]
    public float latchedSpring = 2600f;
    public float latchedDamper = 420f;
    public float gasStrutLatchedSpring = 2200f;
    public float gasStrutLatchedDamper = 360f;
    [Tooltip("Nyitva elengedve mekkora erővel tartja a helyet a gázteleszkóp.")]
    public float gasStrutOpenSpring = 500f;
    public float gasStrutOpenDamper = 20f;
    [Tooltip("Hány fokon belül záródjon be automatikusan ha elengedik.")]
    public float autoCloseDegrees = 8f;
    public bool snapToClosedPose = true;

    private HingeJoint hinge;
    private FixedJoint fixedHinge;  // Bereteszelt weightless ajtókhoz (lag-mentes rögzítés)
    private Rigidbody rb;
    private SnapPoint mySnapPoint; 
    private bool isBeingDragged = false;
    
    private float lastUnlatchTime = 0f;

    // Weightless ajtók (motorháztető/csomagtartó) belső állapota
    private Rigidbody connectedChassis;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) 
        {
            rb.maxDepenetrationVelocity = 2f; 
            rb.maxAngularVelocity = maxDoorAngularSpeed;
        }
    }

    public void AttachToChassis(Rigidbody chassisRb, SnapPoint sp)
    {
        mySnapPoint = sp;
        connectedChassis = chassisRb;
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null || chassisRb == null) return;

        if (hinge != null) Destroy(hinge);

        rb.isKinematic = false;
        rb.useGravity = !weightlessWhenAttached;
        rb.linearVelocity = chassisRb.linearVelocity;
        rb.angularVelocity = chassisRb.angularVelocity;

        hinge = gameObject.AddComponent<HingeJoint>();
        hinge.connectedBody = chassisRb;
        hinge.enableCollision = false;

        Vector3 hingeWorldPos = (sp != null) ? sp.transform.position : transform.position;

        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = transform.InverseTransformPoint(hingeWorldPos);
        hinge.axis = hingeAxis.sqrMagnitude > 0.0001f ? hingeAxis.normalized : Vector3.up;
        hinge.connectedAnchor = chassisRb.transform.InverseTransformPoint(hingeWorldPos);

        hinge.breakForce = breakForce;
        hinge.breakTorque = breakForce;

        // Bezárt pozíció tárolása a karosszériához képest — MINDEN ajtótípusnál kell a snap-hez
        closedLocalPosition = chassisRb.transform.InverseTransformPoint(transform.position);
        closedLocalRotation = Quaternion.Inverse(chassisRb.transform.rotation) * transform.rotation;

        ForceCloseDoor();
    }

    public void DetachFromChassis()
    {
        if (hinge != null) { Destroy(hinge); hinge = null; }
        if (fixedHinge != null) { Destroy(fixedHinge); fixedHinge = null; }
        mySnapPoint = null;
        connectedChassis = null;
        if (weightlessWhenAttached && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void Unlatch()
    {
        if (!isLatched) return;
        if (fixedHinge == null && hinge == null) return; // Az ajtó már le van szakadva

        isLatched = false;
        lastUnlatchTime = Time.time;

        if (rb != null)
        {
            // FixedJoint eltávolítása — minden ajtótípusnál egységesen
            if (fixedHinge != null) { Destroy(fixedHinge); fixedHinge = null; }
            rb.isKinematic = false;
            rb.useGravity = !weightlessWhenAttached;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CreateHinge();
        }

        if (hinge == null) return; // CreateHinge sem sikerült (nincs chassis)

        hinge.useLimits = true;
        JointLimits limits = hinge.limits;
        limits.min = Mathf.Min(closedAngle, openedAngle);
        limits.max = Mathf.Max(closedAngle, openedAngle);
        limits.bounciness = 0f;
        limits.contactDistance = 0f; // 0 = pontosan a szögnél aktivál, nem előtte
        hinge.limits = limits;

        if (hasGasStrut) SetDoorSpring(hinge.angle, gasStrutOpenSpring, gasStrutOpenDamper);
        else hinge.useSpring = false;
    }

    public void ReleaseDoor()
    {
        if (hinge != null && !isLatched && mySnapPoint != null)
        {
            float closeDelta = Mathf.Abs(Mathf.DeltaAngle(hinge.angle, closedAngle));

            if (closeDelta < 25f)
            {
                ForceCloseDoor();
            }
            else if (hasGasStrut)
            {
                SetDoorSpring(hinge.angle, gasStrutOpenSpring, gasStrutOpenDamper); 
            }
        }

        isBeingDragged = false;
    }

    void FixedUpdate()
    {
        if (rb != null && !rb.isKinematic && HasInvalidPhysicsState())
        {
            RecoverFromInvalidState();
            return;
        }

        if (hinge != null && mySnapPoint != null)
        {
            float closeDelta = Mathf.Abs(Mathf.DeltaAngle(hinge.angle, closedAngle));

            if (!isLatched && Time.time - lastUnlatchTime > 0.3f && closeDelta <= autoCloseDegrees)
            {
                ForceCloseDoor();
            }
        }

        if (rb != null)
        {
            rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, maxDoorAngularSpeed);
        }
    }

    public void ForceCloseDoor()
    {
        isLatched = true; 
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Minden ajtó: snapelés a bezárt pozícióba mielőtt a FixedJoint rögzít
            // (FixedJoint az aktuális pozíciót láncolja le, nem a célpozíciót!)
            if (connectedChassis != null)
            {
                Vector3 worldPos = connectedChassis.transform.TransformPoint(closedLocalPosition);
                Quaternion worldRot = connectedChassis.transform.rotation * closedLocalRotation;
                rb.position = worldPos;
                rb.rotation = worldRot;
                transform.SetPositionAndRotation(worldPos, worldRot);
            }

            if (weightlessWhenAttached)
            {
                // FixedJoint rögzítés: az ajtó nem kinematikus, de a FixedJoint
                // mereven tartja a karosszériához — nulla lag, nulla instabilitás.
                if (hinge != null) { Destroy(hinge); hinge = null; }
                rb.isKinematic = false;
                rb.useGravity = false;
                // Ha nincs FixedJoint (pl. recovery hívta), létrehozzuk
                if (fixedHinge == null) CreateFixedHinge();
            }
            else
            {
                // Normál ajtók: FixedJoint rögzítés lezárt állapotban — nulla oszcilláció
                if (hinge != null) { Destroy(hinge); hinge = null; }
                if (fixedHinge == null) CreateFixedHinge();
            }
        }

        Debug.Log($"<color=cyan>Ajtó BEVÁGVA! ({gameObject.name})</color>");
    }

    public void MoveDoor(float mouseDeltaX, float mouseDeltaY)
    {
        if (isLocked || hinge == null || isLatched) return;

        hinge.useSpring = false; 
        float input = isVerticalOpen ? mouseDeltaY : mouseDeltaX;
        Vector3 axis = hingeAxis.sqrMagnitude > 0.0001f ? hingeAxis.normalized : Vector3.up;

        if (hasGasStrut)
        {
            float torque = Mathf.Clamp(input * doorSensitivity, -maxMouseTorque, maxMouseTorque);
            rb.AddRelativeTorque(axis * torque, ForceMode.Acceleration);
        }
        else
        {
            rb.AddRelativeTorque(axis * input * doorSensitivity, ForceMode.VelocityChange);
        }
    }

    public void BeginDrag()
    {
        isBeingDragged = true;
    }

    public void EndDrag()
    {
        isBeingDragged = false;
    }

    void OnJointBreak(float force)
    {
        // Unity már megsemmisítette a törött joint component-et, nullázzuk a referenciákat
        hinge = null;
        fixedHinge = null;
        connectedChassis = null;
        if (weightlessWhenAttached && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        if (mySnapPoint != null)
        {
            mySnapPoint.ForceDetach();
            mySnapPoint = null;
        }
    }

    // FixedJoint létrehozása (weightless ajtó bereteszelt állapotában)
    private void CreateFixedHinge()
    {
        if (connectedChassis == null) return;
        if (fixedHinge != null) Destroy(fixedHinge);

        fixedHinge = gameObject.AddComponent<FixedJoint>();
        fixedHinge.connectedBody = connectedChassis;
        fixedHinge.enableCollision = false;
        // Soha nem törik le: csak HingeJoint törhet (nyitott ajtó ütésnél)
        fixedHinge.breakForce = Mathf.Infinity;
        fixedHinge.breakTorque = Mathf.Infinity;
    }

    // HingeJoint létrehozása (weightless ajtó nyitásakor újra szükséges)
    private void CreateHinge()
    {
        if (connectedChassis == null) return;
        if (hinge != null) Destroy(hinge);

        hinge = gameObject.AddComponent<HingeJoint>();
        hinge.connectedBody = connectedChassis;
        hinge.enableCollision = false;

        Vector3 hingeWorldPos = (mySnapPoint != null) ? mySnapPoint.transform.position : transform.position;

        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = transform.InverseTransformPoint(hingeWorldPos);
        hinge.axis = hingeAxis.sqrMagnitude > 0.0001f ? hingeAxis.normalized : Vector3.up;
        hinge.connectedAnchor = connectedChassis.transform.InverseTransformPoint(hingeWorldPos);

        hinge.breakForce = breakForce;
        hinge.breakTorque = breakForce;
    }

    private void SetDoorSpring(float targetPos, float springForce, float damperForce)
    {
        if (hinge != null)
        {
            hinge.useSpring = true;
            JointSpring doorSpring = new JointSpring();
            doorSpring.spring = springForce;  
            doorSpring.damper = damperForce;  
            doorSpring.targetPosition = targetPos; 
            hinge.spring = doorSpring;
        }
    }

    private bool HasInvalidPhysicsState()
    {
        if (!IsFiniteVector3(transform.position)) return true;
        if (!IsFiniteVector3(rb.position)) return true;
        if (!IsFiniteVector3(rb.linearVelocity)) return true;
        if (!IsFiniteVector3(rb.angularVelocity)) return true;
        return false;
    }

    private void RecoverFromInvalidState()
    {
        if (rb == null) return;

        // Csak akkor teleportálunk, ha a pozíció is érvénytelen.
        // Sebesség-NaN esetén elég nullázni, nem kell elmozgatni az ajtót.
        if (mySnapPoint != null &&
            (!IsFiniteVector3(transform.position) || !IsFiniteVector3(rb.position)))
        {
            rb.position = mySnapPoint.transform.position;
            rb.rotation = mySnapPoint.transform.rotation;
            Physics.SyncTransforms();
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.maxAngularVelocity = maxDoorAngularSpeed;

        ForceCloseDoor();
    }

    private bool IsFiniteVector3(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}