using UnityEngine;

public class SimplePickupDrop : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float reachDistance = 3f;           // How far you can pick things up
    public Transform holdParent;               // Empty GameObject in front of camera
    public LayerMask pickableLayerMask;        // Only raycast against these layers for pickup

    private GameObject heldObject = null;
    private Rigidbody heldRigidbody = null;

    // For hover logging
    private GameObject lastHovered = null;

    void Update()
    {
        HandleHoverLogging();

        if (Input.GetMouseButtonDown(1))
        {
            if (heldObject == null)
                TryPickup();
            else
                Drop();
        }

        // Smoothly follow the holdParent if holding something
        if (heldObject)
        {
            heldObject.transform.position = Vector3.Lerp(
                heldObject.transform.position,
                holdParent.position,
                Time.deltaTime * 10f
            );
            heldObject.transform.rotation = Quaternion.Lerp(
                heldObject.transform.rotation,
                holdParent.rotation,
                Time.deltaTime * 10f
            );
        }
    }

    void HandleHoverLogging()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // We'll use reachDistance here too, but no layer mask so you see everything
        bool didHit = Physics.Raycast(ray, out hit, reachDistance);

        if (didHit)
        {
            GameObject current = hit.collider.gameObject;
            if (current != lastHovered)
            {
                string tagInfo = current.tag;
                string layerInfo = LayerMask.LayerToName(current.layer);
                float dist = hit.distance;
                Debug.Log($"[Hover] Now pointing at: {current.name} | Tag: {tagInfo} | Layer: {layerInfo} | Distance: {dist:F2}m");
                lastHovered = current;
            }
        }
        else if (lastHovered != null)
        {
            Debug.Log($"[Hover] No longer pointing at: {lastHovered.name}");
            lastHovered = null;
        }
    }

    void TryPickup()
    {
        Debug.Log("[Pickup] Attempting raycast");
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, reachDistance, pickableLayerMask))
        {
            Debug.Log($"[Pickup] Ray hit: {hit.collider.name} (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            var candidate = hit.collider.gameObject;

            if (candidate.CompareTag("Pickable"))
            {
                Debug.Log($"[Pickup] Tag check passed on {candidate.name}");
                heldRigidbody = candidate.GetComponentInParent<Rigidbody>();
                heldObject = heldRigidbody.gameObject;

                if (heldRigidbody)
                {
                    Debug.Log($"[Pickup] Disabling physics on {heldObject.name}");
                    heldRigidbody.isKinematic = true;
                    heldRigidbody.detectCollisions = false;
                }

                heldObject.transform.SetParent(holdParent);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;

                Debug.Log($"[Pickup] {heldObject.name} picked up");
            }
            else
            {
                Debug.Log($"[Pickup] {candidate.name} does not have tag 'Pickable'");
            }
        }
        else
        {
            Debug.Log("[Pickup] Nothing hit within reach");
        }
    }

    void Drop()
    {
        Debug.Log($"[Drop] Dropping {heldObject.name}");
        heldObject.transform.SetParent(null);

        heldObject.transform.localScale = Vector3.one;

        if (heldRigidbody)
        {
            Debug.Log($"[Drop] Re-enabling physics on {heldObject.name}");
            heldRigidbody.isKinematic = false;
            heldRigidbody.detectCollisions = true;
            heldRigidbody.AddForce(
                Camera.main.transform.forward * 2f,
                ForceMode.VelocityChange
            );
        }

        heldObject = null;
        heldRigidbody = null;
        Debug.Log("[Drop] Drop complete");
    }
}
