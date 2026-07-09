using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ModularCarController : MonoBehaviour
{
    [Header("Láthatatlan Fizikai Kerekek")]
    public WheelCollider frontLeftW;
    public WheelCollider frontRightW;
    public WheelCollider rearLeftW;
    public WheelCollider rearRightW;

    [Header("Leszakadó 3D Kerekek")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Erő és Sebesség")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSpeedKMH = 120f;        
    public float maxReverseSpeedKMH = 30f; 
    public float accelerationFalloff = 2f; 

    [Header("Kormányzás és Drift")]
    public float lowSpeedSteerAngle = 35f;  
    public float highSpeedSteerAngle = 10f; 
    public float steerSmoothSpeed = 5f;     
    [Range(0.5f, 1f)] public float driftAssist = 0.95f; 

    [Header("Belső Tér Animációk")]
    public Transform steeringWheel;
    public float steeringWheelMultiplier = 3f; 
    public Vector3 steeringAxis = new Vector3(0, 0, 1); 

    public Transform gasPedal;
    public Transform brakePedal;
    public float pedalAngle = 15f; 
    public Vector3 pedalAxis = new Vector3(1, 0, 0); 

    [Header("Javítások")]
    [Tooltip("Pipáld be, ha a W-re hátrafelé megy!")]
    public bool isCarModelReversed = true;
    [Tooltip("Pipáld be, ha az A/D gombokra fordítva kanyarodik!")]
    public bool reverseSteering = false; 
    public bool flipRightWheels = true;

    [HideInInspector] public bool isPlayerInside = false; 

    private Rigidbody rb;
    private float currentSteerAngle = 0f;
    private Quaternion origSteerRot, origGasRot, origBrakeRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (steeringWheel != null) origSteerRot = steeringWheel.localRotation;
        if (gasPedal != null) origGasRot = gasPedal.localRotation;
        if (brakePedal != null) origBrakeRot = brakePedal.localRotation;
    }

    void FixedUpdate()
    {
        if (!isPlayerInside)
        {
            ApplyBraking(brakeForce); 
            ApplyMotorTorque(0f);
            UpdateAllWheels();
            AnimateInterior(0f, 0f, 0f);
            return;
        }

        // 1. BEOLVASÁS (A játékos szándéka: 1 = Előre, -1 = Hátra)
        float rawV = 0f; float rawH = 0f;
        if (Keyboard.current.wKey.isPressed) rawV += 1f;
        if (Keyboard.current.sKey.isPressed) rawV -= 1f;
        if (Keyboard.current.aKey.isPressed) rawH -= 1f;
        if (Keyboard.current.dKey.isPressed) rawH += 1f;

        if (reverseSteering) rawH = -rawH;

        bool isHandbraking = Keyboard.current.spaceKey.isPressed;

        // 2. IGAZI SEBESSÉG SZÁMÍTÁSA (A szélvédő irányába!)
        float currentSpeedKMH = rb.linearVelocity.magnitude * 3.6f;
        Vector3 trueForward = isCarModelReversed ? -transform.forward : transform.forward;
        float forwardSpeed = Vector3.Dot(trueForward, rb.linearVelocity);

        float currentMotorForce = 0f;
        float currentBrakeForce = 0f;

        // 3. GÁZ ÉS FÉK LOGIKA
        if (isHandbraking) 
        {
            currentBrakeForce = brakeForce; 
        }
        else if (Mathf.Abs(rawV) < 0.05f) 
        {
            currentMotorForce = 0f; 
            currentBrakeForce = 50f; // Pici motorfék (hogy lassan megálljon, ha elengeded)
        }
        else
        {
            if (rawV > 0.05f) // 'W' gomb: Előre akarunk menni
            {
                if (forwardSpeed < -1f) // De a kocsi hátrafelé gurul -> FÉK!
                {
                    currentBrakeForce = brakeForce;
                }
                else // Jó irányba megyünk -> GÁZ!
                {
                    float speedRatio = Mathf.Clamp01(currentSpeedKMH / maxSpeedKMH);
                    float dragFactor = 1f - Mathf.Pow(speedRatio, accelerationFalloff);
                    currentMotorForce = rawV * motorForce * dragFactor;
                }
            }
            else if (rawV < -0.05f) // 'S' gomb: Tolatni akarunk
            {
                if (forwardSpeed > 1f) // De a kocsi még előre gurul -> FÉK!
                {
                    currentBrakeForce = brakeForce;
                }
                else // Jó irányba megyünk -> GÁZ HÁTRAFELÉ!
                {
                    float reverseRatio = Mathf.Clamp01(currentSpeedKMH / maxReverseSpeedKMH);
                    float reverseDragFactor = 1f - Mathf.Pow(reverseRatio, accelerationFalloff);
                    currentMotorForce = rawV * motorForce * reverseDragFactor;
                }
            }
        }

        // --- A FIZIKAI TRÜKK ---
        // Ha a kasztni modellje meg van fordítva, a kerekeket negatív erővel kell hajtani, hogy "előre" menjenek!
        if (isCarModelReversed)
        {
            currentMotorForce = -currentMotorForce;
        }

        ApplyMotorTorque(currentMotorForce);
        ApplyBraking(currentBrakeForce);

        // 4. KORMÁNYZÁS ÉS DRIFT
        float speedRatioForSteer = Mathf.Clamp01(currentSpeedKMH / maxSpeedKMH);
        float dynamicMaxSteer = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedRatioForSteer);
        float targetSteerAngle = rawH * dynamicMaxSteer;
        
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steerSmoothSpeed);
        frontLeftW.steerAngle = currentSteerAngle;
        frontRightW.steerAngle = currentSteerAngle;

        if (rb.linearVelocity.magnitude > 3f)
        {
            if (Mathf.Abs(rawH) < 0.1f) rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y * 0.85f, rb.angularVelocity.z);
            else rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y * driftAssist, rb.angularVelocity.z);
        }

        UpdateAllWheels();
        // Belső tér animálása a valódi gombnyomásaink alapján!
        AnimateInterior(currentSteerAngle, (rawV > 0 ? rawV : 0), (currentBrakeForce > 50f ? 1 : 0));
    }

    private void ApplyMotorTorque(float torque)
    {
        rearLeftW.motorTorque = torque;
        rearRightW.motorTorque = torque;
    }

    private void ApplyBraking(float force)
    {
        frontLeftW.brakeTorque = force;
        frontRightW.brakeTorque = force;
        rearLeftW.brakeTorque = force;
        rearRightW.brakeTorque = force;
    }

    private void UpdateAllWheels()
    {
        UpdateWheelMesh(frontLeftW, frontLeftMesh, false);
        UpdateWheelMesh(frontRightW, frontRightMesh, flipRightWheels); 
        UpdateWheelMesh(rearLeftW, rearLeftMesh, false);
        UpdateWheelMesh(rearRightW, rearRightMesh, flipRightWheels);
    }

    private void UpdateWheelMesh(WheelCollider collider, Transform mesh, bool flip180)
    {
        if (mesh == null) return; 
        collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        if (flip180) rot = rot * Quaternion.Euler(0, 180f, 0);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    // --- EZ A FÜGGVÉNY MARADT LE AZ ELŐZŐBŐL! ---
    private void AnimateInterior(float steerAngle, float gasInput, float brakeInput)
    {
        if (steeringWheel != null)
            steeringWheel.localRotation = origSteerRot * Quaternion.AngleAxis(steerAngle * steeringWheelMultiplier, steeringAxis);

        if (gasPedal != null)
            gasPedal.localRotation = origGasRot * Quaternion.AngleAxis(gasInput * pedalAngle, pedalAxis);

        if (brakePedal != null)
            brakePedal.localRotation = origBrakeRot * Quaternion.AngleAxis(brakeInput * pedalAngle, pedalAxis);
    }
}