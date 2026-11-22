using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    public Transform[] floaters;

    public float underWaterDrag = 3;
    public float underWaterAngularDrag = 1;
    public float airDrag = 3;
    public float airAngularDrag = 1;
    public float floatingPower = 500f;

    Rigidbody m_RigidBody;
    bool underwater;
    int floatersUnderwater;

    [Header("Reference to water plane")]
    public Transform waterPlane;
    public Material waterMaterial;

    [Header("Match Your Shader Settings")]
    public float timeDivider = 30f;
    public Vector2 uvTiling = Vector2.one;
    public float noiseScale = 10f;  // Should match shader's Gradient Noise scale

    private float shaderDisplacement;
    private float customTime = 0f;

    private Bounds cachedBounds;
    private MeshFilter cachedMeshFilter;

    void Start()
    {
        m_RigidBody = GetComponent<Rigidbody>();

        Renderer renderer = waterPlane.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            waterMaterial = renderer.material;

            if (renderer.material.HasProperty("_Displacement"))
            {
                shaderDisplacement = renderer.material.GetFloat("_Displacement");
                Debug.Log($"Using shader displacement value: {shaderDisplacement}");
            }
        }

        // Check mesh UV mapping
        MeshFilter meshFilter = waterPlane.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.sharedMesh;
        Bounds bounds = mesh.bounds;

        Vector2[] uvs = mesh.uv;

        Debug.Log($"First UV: {uvs[0]}");
        Debug.Log($"Last UV: {uvs[uvs.Length - 1]}");
        Debug.Log($"Center UV (approx): {uvs[uvs.Length / 2]}");

        Debug.Log($"Mesh bounds: min={bounds.min}, max={bounds.max}, center={bounds.center}");
        Debug.Log($"Mesh size: {bounds.size}");

        int minXminZ = 0, maxXminZ = 0, minXmaxZ = 0, maxXmaxZ = 0;
        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // Bottom-left corner (min X, min Z)
            if (v.x <= vertices[minXminZ].x && v.z <= vertices[minXminZ].z)
                minXminZ = i;

            // Bottom-right corner (max X, min Z)
            if (v.x >= vertices[maxXminZ].x && v.z <= vertices[maxXminZ].z)
                maxXminZ = i;

            // Top-left corner (min X, max Z)
            if (v.x <= vertices[minXmaxZ].x && v.z >= vertices[minXmaxZ].z)
                minXmaxZ = i;

            // Top-right corner (max X, max Z)
            if (v.x >= vertices[maxXmaxZ].x && v.z >= vertices[maxXmaxZ].z)
                maxXmaxZ = i;
        }

        Debug.Log($"=== CORNER UVs ===");
        Debug.Log($"Bottom-Left corner UV: {uvs[minXminZ]} (should be ~0,0)");
        Debug.Log($"Bottom-Right corner UV: {uvs[maxXminZ]} (should be ~1,0)");
        Debug.Log($"Top-Left corner UV: {uvs[minXmaxZ]} (should be ~0,1)");
        Debug.Log($"Top-Right corner UV: {uvs[maxXmaxZ]} (should be ~1,1)");


        // Test a known position
        Vector3 testWorldPos = waterPlane.position; // Center of plane
        Vector3 testObjPos = waterPlane.InverseTransformPoint(testWorldPos);

        Vector2 calculatedUV = new Vector2(
            (testObjPos.x - bounds.min.x) / bounds.size.x,
            (testObjPos.z - bounds.min.z) / bounds.size.z
        );

        Debug.Log($"Center of plane:");
        Debug.Log($"  Object space: {testObjPos}");
        Debug.Log($"  Calculated UV: {calculatedUV}");
        Debug.Log($"  Expected: (0.5, 0.5) if bounds centered at origin");
        Debug.Log($"Water plane vertex count: {mesh.vertexCount}");
        Debug.Log($"Water plane triangle count: {mesh.triangles.Length / 3}");


        cachedMeshFilter = waterPlane.GetComponent<MeshFilter>();
        cachedBounds = cachedMeshFilter.sharedMesh.bounds;

    }

    Vector2 GetMinUV(Vector2[] uvs)
    {
        Vector2 min = uvs[0];
        foreach (var uv in uvs) { min.x = Mathf.Min(min.x, uv.x); min.y = Mathf.Min(min.y, uv.y); }
        return min;
    }

    Vector2 GetMaxUV(Vector2[] uvs)
    {
        Vector2 max = uvs[0];
        foreach (var uv in uvs) { max.x = Mathf.Max(max.x, uv.x); max.y = Mathf.Max(max.y, uv.y); }
        return max;
    }

    void FixedUpdate()
    {


        floatersUnderwater = 0;


        for (int i = 0; i < floaters.Length; i++)
        {
            float currentWaterHeight = GetWaterHeightAtPosition(floaters[i].position);
            float difference = floaters[i].position.y - currentWaterHeight;

            // Debug when things seem off
            if (Time.time > 20f && i == 0) // After 20 seconds, check first floater
            {
                Debug.Log($"Time: {Time.time:F2}, Floater Y: {floaters[i].position.y:F3}, Water Height: {currentWaterHeight:F3}, Diff: {difference:F3}");
            }

            if (difference < 0)
            {
                float force = floatingPower * Mathf.Abs(difference);
                m_RigidBody.AddForceAtPosition(Vector3.up * force, floaters[i].position, ForceMode.Force);
                floatersUnderwater += 1;

                if (!underwater)
                {
                    underwater = true;
                    SwitchState(underwater);
                }
            }
        }

        if (underwater && floatersUnderwater == 0)
        {
            underwater = false;
            SwitchState(underwater);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            //Vector4 timeParams = Shader.GetGlobalVector("_TimeParameters");
            float currentTime = Mathf.Repeat(Time.time * 0.02f, 100f);

            // Center of plane
            Vector3 centerWorld = waterPlane.position;
            Vector3 objectPos = waterPlane.InverseTransformPoint(centerWorld);
            MeshFilter meshFilter = waterPlane.GetComponent<MeshFilter>();
            Bounds bounds = meshFilter.sharedMesh.bounds;

            Vector2 uv = new Vector2(
                ((objectPos.x - bounds.min.x) / bounds.size.x),
                ((objectPos.z - bounds.min.z) / bounds.size.z)
            );

            Debug.Log($"UV before time: {uv}");
            uv += Vector2.one * currentTime;
            Debug.Log($"UV after time: {uv}");

            float noise = UnityGradientNoise(uv, noiseScale);
            Debug.Log($"Noise: {noise:F4}");
            Debug.Log($"Is this still 0.5? If yes, there's a problem.");
        }
    }

    void SwitchState(bool isUnderWater)
    {
        if (isUnderWater)
        {
            m_RigidBody.linearDamping = underWaterDrag;
            m_RigidBody.angularDamping = underWaterAngularDrag;
        }
        else
        {
            m_RigidBody.linearDamping = airDrag;
            m_RigidBody.angularDamping = airAngularDrag;
        }
    }

    float GetWaterHeightAtPosition(Vector3 worldPosition)
    {
        Vector3 objectPos = waterPlane.InverseTransformPoint(worldPosition);

        // Map to UV0 space (no flip)
        Vector2 uv = new Vector2(
            (objectPos.x - cachedBounds.min.x) / cachedBounds.size.x,
            (objectPos.z - cachedBounds.min.z) / cachedBounds.size.z
        );

        // Match shader: Time / 30, not Time * 0.02
        float timeOffset = Time.time / 30f;
        uv += Vector2.one * timeOffset;

        // Gradient noise with scale 10
        float noise = UnityGradientNoise(uv, 10f);

        float heightOffset = noise * shaderDisplacement;

        return waterPlane.position.y + heightOffset;
    }

    // Unity's gradient noise implementation
    float Fmod(float a, float b)
    {
        return a - b * Mathf.Floor(a / b);
    }

    Vector2 GradientNoiseDir(Vector2 p)
    {
        // Use proper fmod to match HLSL exactly
        p.x = Fmod(p.x, 289f);
        p.y = Fmod(p.y, 289f);

        float x = Fmod((34f * p.x + 1f) * p.x, 289f) + p.y;
        x = Fmod((34f * x + 1f) * x, 289f);
        x = Fmod(x / 41f, 1f) * 2f - 1f;

        return new Vector2(x - Mathf.Floor(x + 0.5f), Mathf.Abs(x) - 0.5f).normalized;
    }

    float GradientNoise(Vector2 p)
    {
        Vector2 ip = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
        Vector2 fp = new Vector2(p.x - ip.x, p.y - ip.y);

        float d00 = Vector2.Dot(GradientNoiseDir(ip), fp);
        float d01 = Vector2.Dot(GradientNoiseDir(ip + new Vector2(0, 1)), fp - new Vector2(0, 1));
        float d10 = Vector2.Dot(GradientNoiseDir(ip + new Vector2(1, 0)), fp - new Vector2(1, 0));
        float d11 = Vector2.Dot(GradientNoiseDir(ip + new Vector2(1, 1)), fp - new Vector2(1, 1));

        fp.x = fp.x * fp.x * fp.x * (fp.x * (fp.x * 6f - 15f) + 10f);
        fp.y = fp.y * fp.y * fp.y * (fp.y * (fp.y * 6f - 15f) + 10f);

        return Mathf.Lerp(Mathf.Lerp(d00, d01, fp.y), Mathf.Lerp(d10, d11, fp.y), fp.x);
    }

    float UnityGradientNoise(Vector2 UV, float Scale)
    {
        return GradientNoise(UV * Scale) + 0.5f;
    }

    void OnDrawGizmos()
    {
        if (waterPlane == null || !Application.isPlaying) return;
        MeshFilter meshFilter = waterPlane.GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // Only draw every Nth vertex (e.g., every 10th vertex)
        int step = 10; // Adjust this - lower = more gizmos, higher = fewer

        Gizmos.color = Color.cyan;
        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 worldPos = waterPlane.TransformPoint(vertices[i]);
            float waveHeight = GetWaterHeightAtPosition(worldPos);

            Gizmos.DrawWireSphere(new Vector3(worldPos.x, waveHeight, worldPos.z), 0.15f);
        }

        // Draw floater gizmos
        foreach (var floater in floaters)
        {
            if (floater != null)
            {
                // Red: floater's actual position
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(floater.position, 0.3f);

                // Blue: calculated wave height at this position
                float waveHeight = GetWaterHeightAtPosition(floater.position);
                Vector3 wavePosition = new Vector3(floater.position.x, waveHeight, floater.position.z);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(wavePosition, 0.3f);
            }
        }
    }
}