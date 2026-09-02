using UnityEngine;

[DisallowMultipleComponent]
public class CakePickup : MonoBehaviour
{
    [SerializeField] private string orderId;
    [SerializeField] private string orderTitle;
    [SerializeField] private int payoutValue;
    [SerializeField] private bool isHeld;

    private Collider cachedCollider;

    public string OrderId
    {
        get { return orderId; }
    }

    public string OrderTitle
    {
        get { return orderTitle; }
    }

    public int PayoutValue
    {
        get { return payoutValue; }
    }

    public bool IsHeld
    {
        get { return isHeld; }
    }

    private void Awake()
    {
        EnsurePickupCollider();
    }

    public void Initialize(string nextOrderId, string nextOrderTitle, int nextPayoutValue)
    {
        orderId = nextOrderId;
        orderTitle = nextOrderTitle;
        payoutValue = nextPayoutValue;
    }

    public void AttachTo(Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        isHeld = true;

        if (cachedCollider == null)
        {
            EnsurePickupCollider();
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }
    }

    public void DropAt(Vector3 worldPosition, Quaternion worldRotation)
    {
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        isHeld = false;

        if (cachedCollider == null)
        {
            EnsurePickupCollider();
        }

        if (cachedCollider != null)
        {
            cachedCollider.isTrigger = true;
            cachedCollider.enabled = true;
        }
    }

    private void EnsurePickupCollider()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.55f;
            sphereCollider.center = new Vector3(0f, 0.25f, 0f);
            cachedCollider = sphereCollider;
        }

        cachedCollider.isTrigger = true;
    }
}
