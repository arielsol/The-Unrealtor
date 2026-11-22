using UnityEngine;

[ExecuteInEditMode]
public class ColliderVisualizer : MonoBehaviour
{
    [Header("Display Settings")]
    public Color wireColor = Color.green;
    public Color fillColor = new Color(0f, 1f, 0f, 0.1f);
    public bool showFill = true;
    public bool showLabel = true;

    [Header("No Collider Fallback")]
    [Tooltip("When no BoxCollider exists, show a box of this size")]
    public Vector3 fallbackSize = new Vector3(2f, 2f, 2f);
    public float iconSize = 0.5f;

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        Vector3 center;
        Vector3 size;

        if (box != null)
        {
            // Has BoxCollider - use its bounds
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            center = box.center;
            size = box.size;

            if (showFill)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawCube(center, size);
            }

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(center, size);

            Gizmos.matrix = oldMatrix;
        }
        else
        {
            // No BoxCollider - draw fallback marker at position
            center = transform.position;

            if (showFill)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawCube(center, fallbackSize);
            }

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(center, fallbackSize);

            // Draw axis cross so position is easy to spot
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center - Vector3.right * iconSize, center + Vector3.right * iconSize);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(center - Vector3.up * iconSize, center + Vector3.up * iconSize);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(center - Vector3.forward * iconSize, center + Vector3.forward * iconSize);

            // Sphere at center
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(center, iconSize * 0.3f);
        }

#if UNITY_EDITOR
        if (showLabel)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, gameObject.name);
        }
#endif
    }
}
