using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OvercookedStyleCamera : MonoBehaviour
{
    public enum CameraAngle
    {
        Angle45 = 45,
        Angle60 = 60
    }

    [SerializeField] private Transform target;
    [SerializeField] private CameraAngle angle = CameraAngle.Angle45;

    private const float CameraDistance = 7f;
    private const float FieldOfView = 40f;
    private static readonly Vector3 LookAtOffset = new Vector3(0f, 0.55f, 0.4f);

    private Camera cachedCamera;

    public Transform Target
    {
        get { return target; }
        set { target = value; }
    }

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        ConfigureCamera();
    }

    private void Start()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        ConfigureCamera();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = desiredPosition;
        LookAtTarget();
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = GetDesiredPosition();
        LookAtTarget();
        ConfigureCamera();
    }

    private Vector3 GetDesiredPosition()
    {
        Quaternion rotation = Quaternion.Euler((float)angle, 0f, 0f);
        Vector3 focusPoint = target.position + LookAtOffset;
        return focusPoint - rotation * Vector3.forward * CameraDistance;
    }

    private void LookAtTarget()
    {
        Vector3 lookTarget = target.position + LookAtOffset;
        Vector3 direction = lookTarget - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private void ConfigureCamera()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        cachedCamera.orthographic = false;
        cachedCamera.usePhysicalProperties = false;
        cachedCamera.fieldOfView = FieldOfView;
        cachedCamera.nearClipPlane = 0.05f;
        cachedCamera.farClipPlane = 100f;
    }

    private void OnValidate()
    {
        cachedCamera = GetComponent<Camera>();
        ConfigureCamera();

        if (target != null && !Application.isPlaying)
        {
            SnapToTarget();
        }
    }
}
