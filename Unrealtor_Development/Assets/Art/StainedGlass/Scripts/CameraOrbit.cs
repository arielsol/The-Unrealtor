using UnityEngine;

/// <summary>
/// Simple camera orbit script for the stained glass showcase.
/// Slowly rotates the camera around the center to view all panels.
/// Attach to the Main Camera in the Circular Gallery scene.
/// </summary>
public class CameraOrbit : MonoBehaviour
{
    [Header("Orbit Settings")]
    public float orbitSpeed = 8f;
    public float orbitRadius = 3f;
    public float bobAmount = 0.3f;
    public float bobSpeed = 0.5f;
    public Vector3 lookTarget = Vector3.zero;

    private float currentAngle = 0f;
    private float baseY;

    void Start()
    {
        baseY = transform.position.y;
        if (lookTarget == Vector3.zero)
            lookTarget = new Vector3(0, 2, 0);
    }

    void Update()
    {
        currentAngle += orbitSpeed * Time.deltaTime;

        float rad = currentAngle * Mathf.Deg2Rad;
        float x = Mathf.Sin(rad) * orbitRadius;
        float z = Mathf.Cos(rad) * orbitRadius;
        float y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        transform.position = new Vector3(x, y, z);
        transform.LookAt(lookTarget);
    }
}
