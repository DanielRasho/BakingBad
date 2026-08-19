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
        cachedCollider = GetComponent<Collider>();
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
            cachedCollider = GetComponent<Collider>();
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
            cachedCollider = GetComponent<Collider>();
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
        }
    }
}
