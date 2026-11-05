using UnityEngine;

/// <summary>
/// Marks where the player wakes up after a guard catches them (the kitchen).
/// Put it on an empty object in the kitchen, on the walkable floor.
/// </summary>
public class PlayerRespawnPoint : MonoBehaviour
{
    public static PlayerRespawnPoint Current { get; private set; }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }
}
