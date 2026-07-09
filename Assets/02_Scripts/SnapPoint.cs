using UnityEngine;
using System.Collections;

public class SnapPoint : MonoBehaviour
{
    [Header("Beállítások")]
    public ItemData acceptedPart;    
    public GameObject ghostHologram; 
    public Transform partParent;     

    [Header("Állapot")]
    public bool isInstalled = true;  
    public GameObject installedPart; 

    private BoxCollider myCollider; 

    void Start()
    {
        myCollider = GetComponent<BoxCollider>();

        if (ghostHologram != null) ghostHologram.SetActive(false);

        if (isInstalled && installedPart != null)
        {
            installedPart.tag = "AttachedPart";
            if (myCollider != null) myCollider.enabled = false;
            
            SetLayerRecursively(installedPart, LayerMask.NameToLayer("CarParts"));
            
            // --- ÚJ BIZTOSÍTÉK: Felszerelt állapotban Szellemmé tesszük, hogy ne zavarja a WheelCollidert! ---
            SetCollidersTrigger(installedPart, true);

            ItemObject io = installedPart.GetComponent<ItemObject>();
            if (io != null) io.currentSnapPoint = this;

            installedPart.transform.parent = partParent;
            installedPart.transform.position = transform.position;
            installedPart.transform.rotation = transform.rotation;
            
            CarDoor doorScript = installedPart.GetComponent<CarDoor>();
            if (doorScript != null)
            {
                Rigidbody chassisRb = partParent.GetComponentInParent<Rigidbody>();
                if (chassisRb != null) doorScript.AttachToChassis(chassisRb, this); 
            }
            else
            {
                Rigidbody rb = installedPart.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true; 
                    rb.interpolation = RigidbodyInterpolation.None;
                }
            }
            Physics.SyncTransforms(); 
        }
    }

    public void ShowHologram(bool show)
    {
        if (ghostHologram != null)
        {
            if (show && isInstalled) return; 
            ghostHologram.SetActive(show);
        }
    }

    public void InstallPart(GameObject partInHand)
    {
        isInstalled = true;
        ShowHologram(false); 
        if (myCollider != null) myCollider.enabled = false;

        installedPart = partInHand;
        
        ItemObject io = installedPart.GetComponent<ItemObject>();
        if (io != null) io.currentSnapPoint = this;

        installedPart.transform.parent = partParent;
        installedPart.transform.position = transform.position;
        installedPart.transform.rotation = transform.rotation;

        Physics.SyncTransforms();

        installedPart.tag = "AttachedPart"; 
        
        SetLayerRecursively(installedPart, LayerMask.NameToLayer("CarParts"));
        
        // --- ÚJ BIZTOSÍTÉK: Bepattintáskor Szellemmé válik ---
        SetCollidersTrigger(installedPart, true);

        CarDoor doorScript = installedPart.GetComponent<CarDoor>();
        if (doorScript != null)
        {
            Rigidbody chassisRb = partParent.GetComponentInParent<Rigidbody>();
            if (chassisRb != null) doorScript.AttachToChassis(chassisRb, this); 
        }
        else
        {
            Rigidbody rb = installedPart.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }
    }

    public GameObject UninstallPart()
    {
        if (isInstalled && installedPart != null)
        {
            isInstalled = false;
            if (myCollider != null) myCollider.enabled = true;

            GameObject partToReturn = installedPart; 
            
            ItemObject io = partToReturn.GetComponent<ItemObject>();
            if (io != null) io.currentSnapPoint = null;

            partToReturn.tag = "Pickupable"; 
            
            SetLayerRecursively(partToReturn, LayerMask.NameToLayer("Default"));
            
            // --- ÚJ BIZTOSÍTÉK: Leszedéskor visszanyeri a fizikai szilárdságát ---
            SetCollidersTrigger(partToReturn, false);
            
            CarDoor doorScript = partToReturn.GetComponent<CarDoor>();
            if (doorScript != null) doorScript.DetachFromChassis();

            Rigidbody rb = partToReturn.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                rb.isKinematic = false; 
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            
            partToReturn.transform.parent = null;
            installedPart = null;

            return partToReturn; 
        }
        return null;
    }

    public void ForceDetach()
    {
        isInstalled = false;
        
        if (myCollider != null) myCollider.enabled = false;
        Invoke("EnableCollider", 2f); 

        if (installedPart != null)
        {
            GameObject flyingPart = installedPart; 
            flyingPart.tag = "Pickupable"; 
            
            // --- ÚJ BIZTOSÍTÉK: Leszakadáskor is azonnal szilárd lesz ---
            SetCollidersTrigger(flyingPart, false);

            ItemObject io = flyingPart.GetComponent<ItemObject>();
            if (io != null) io.currentSnapPoint = null;

            Rigidbody rb = flyingPart.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate; 
                rb.WakeUp(); 

                Vector3 pushDir = (flyingPart.transform.position - partParent.position).normalized;
                pushDir.y = 0.5f; 
                
                rb.AddForce(pushDir * 8f, ForceMode.Impulse);
                rb.AddTorque(new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f)), ForceMode.Impulse);
            }

            flyingPart.transform.parent = null;
            installedPart = null;

            StartCoroutine(DelayedLayerRestore(flyingPart, 1.5f));
        }
    }

    private IEnumerator DelayedLayerRestore(GameObject part, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (part != null)
        {
            SetLayerRecursively(part, LayerMask.NameToLayer("Default"));
        }
    }

    private void EnableCollider()
    {
        if (!isInstalled && myCollider != null)
        {
            myCollider.enabled = true;
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    // --- ÚJ FUNKCIÓ: A Colliderek "Szellemesítése" ---
    private void SetCollidersTrigger(GameObject obj, bool isTrigger)
    {
        if (obj == null) return;
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.isTrigger = isTrigger;
        }
    }
}