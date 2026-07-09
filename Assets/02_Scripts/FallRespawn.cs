using UnityEngine;

public class FallRespawn : MonoBehaviour
{
    private Rigidbody rb;

    void Start() { rb = GetComponent<Rigidbody>(); }

    void Update()
    {
        if (transform.position.y < -10f)
        {
            // Fixen a pálya közepére, 5 méter magasra dobjuk vissza!
            transform.position = new Vector3(0, 5f, 0); 
            
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}