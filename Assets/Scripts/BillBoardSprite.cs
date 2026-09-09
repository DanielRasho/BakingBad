using UnityEngine;

[ExecuteAlways]
public class BillBoardSprite : MonoBehaviour
{
    [Tooltip("If empty, defaults to Camera.main")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Lock rotation to Y-axis only (good for ground-based characters)")]
    [SerializeField] private bool yAxisOnly = true;

    private void OnEnable()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        if (yAxisOnly)
        {
            // Only rotate around Y so the sprite stays upright,
            // ignoring the camera's pitch (up/down look angle).
            Vector3 direction = targetCamera.transform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-direction);
        }
        else
        {
            // Full spherical billboard — always exactly faces the camera,
            // including up/down tilt.
            transform.rotation = targetCamera.transform.rotation;
        }
    }
}
