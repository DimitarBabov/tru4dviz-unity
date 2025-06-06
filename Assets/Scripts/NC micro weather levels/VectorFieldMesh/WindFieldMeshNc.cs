using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindFieldMeshNc : MonoBehaviour
{
    public Material windMaterial;
    public float arrowBaseToLength = 0.1f; // Base size as fraction of arrow length
    public float arrowScale = 1.5f; // Overall scale factor for arrows
    public DataContainer dataContainer;
    public CompassMarkers compassMarkers; // Reference to compass markers component
    [Tooltip("If true, all arrows will have the same length but keep their color based on magnitude")]
    public bool normalizeArrowLengths = false;
    [Tooltip("Fixed length to use when arrows are normalized")]
    public float normalizedArrowLength = 1.0f;

    public Vector3 lat_origin;
    public Vector3 lon_origin;
    public Vector3 alt_origin;

    void Start()
    {
        StartCoroutine(WaitForDataAndGenerateMesh());
    }

    IEnumerator WaitForDataAndGenerateMesh()
    {
        while (dataContainer == null || !dataContainer.IsLoaded)
        {
            yield return null;
        }
        yield return StartCoroutine(GenerateMeshFromContainer());
    }

    IEnumerator GenerateMeshFromContainer()
    {
        // Check for x_from_origin/y_from_origin
        var xField = dataContainer.GetType().GetField("x_from_origin");
        var yField = dataContainer.GetType().GetField("y_from_origin");
        if (xField == null || yField == null)
        {
            Debug.LogError("DataContainer does not have x_from_origin/y_from_origin fields!");
            yield break;
        }
        var xList = (List<float>)xField.GetValue(dataContainer);
        var yList = (List<float>)yField.GetValue(dataContainer);
        if (xList.Count == 0 || yList.Count == 0)
        {
            Debug.LogError("x_from_origin/y_from_origin are empty!");
            yield break;
        }

        // Arrow mesh template (local space, tip along +Z)
        Vector3[] arrowVerts = new Vector3[]
        {
            new Vector3(-1f, 0, 0),    // left base corner (unit size)
            new Vector3(0, 0, 1f),     // tip (unit length)
            new Vector3(1f, 0, 0),     // right base corner (unit size)
            new Vector3(0, 1f, 0)      // top base corner (unit size)
        };
        int[] arrowTris = new int[] { 0, 1, 2, 0, 3, 1, 2, 1, 3 };
        Vector2[] arrowUV = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0.5f, 1),
            new Vector2(1, 0),
            new Vector2(0.5f, 0)
        };

        // Find min/max for centering
        float minMsl = float.MaxValue, maxMsl = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < xList.Count; i++)
        {
            float x = xList[i];
            float y = yList[i];
            float msl = dataContainer.msl[i];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            if (msl < minMsl) minMsl = msl;
            if (msl > maxMsl) maxMsl = msl;
        }

        // Calculate grid cell width (uniform grid spacing)
        float gridCellWidth = xList.Count > 1 ? Mathf.Abs(xList[1] - xList[0]) : 1f;
        Debug.Log($"Grid cell width: {gridCellWidth:F3}");

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        // Initialize origins
        lat_origin = Vector3.zero;
        lon_origin = Vector3.zero;
        alt_origin = new Vector3(0, minMsl, 0);

        // Create compass markers using the separate component
        if (compassMarkers != null)
        {
            compassMarkers.CreateMarkers(minX, maxX, minY, maxY, minMsl);
        }

        // Prepare lists for combined mesh
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector2> uv2s = new List<Vector2>();
        List<Vector2> uv3s = new List<Vector2>();

        for (int i = 0; i < xList.Count; i++)
        {
            float x = xList[i];
            float y = yList[i];
            float msl = dataContainer.msl[i];
            float uNorm = dataContainer.u_norm[i];
            float vNorm = dataContainer.v_norm[i];
            float wNorm = dataContainer.w_norm[i];
            float mag = dataContainer.mag[i];
            float magNorm = dataContainer.mag_norm[i]; // Always use actual magnitude for coloring

            Vector3 tipPos = new Vector3(x, msl, y);
            
            // Get physical wind components
            Vector2 uMinMax = dataContainer.uMinMax;
            Vector2 vMinMax = dataContainer.vMinMax;
            Vector2 wMinMax = dataContainer.wMinMax;

            float uPhysical = uMinMax.x + uNorm * (uMinMax.y - uMinMax.x);
            float vPhysical = vMinMax.x + vNorm * (vMinMax.y - vMinMax.x);
            float wPhysical = wMinMax.x + wNorm * (wMinMax.y - wMinMax.x);

            // Create physical wind vector: (u, w, v) -> (X, Y, Z) in grid space
            Vector3 windVec = new Vector3(uPhysical, wPhysical, vPhysical);

            // Calculate arrow length
            float arrowLength;
            if (normalizeArrowLengths)
            {
                // Use normalized direction with fixed length, but keep original magnitude for coloring
                if (windVec.magnitude > 0.001f)
                {
                    windVec = windVec.normalized * normalizedArrowLength * gridCellWidth * arrowScale;
                }
                arrowLength = normalizedArrowLength * gridCellWidth * arrowScale;
            }
            else
            {
                // Original behavior - length proportional to magnitude
                float maxMagnitude = dataContainer.magMinMax.y;
                float lengthScale = mag / maxMagnitude;
                arrowLength = lengthScale * gridCellWidth * arrowScale;
            }

            float scaledBaseSize = arrowLength * arrowBaseToLength;

            // Calculate arrow orientation
            Vector3 windDir = windVec.normalized;
            Vector3 basePos = tipPos + windDir * arrowLength;
            
            // Create local coordinate system for the arrow
            Vector3 forward = windDir;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;

            // Build arrow vertices directly at final positions
            Vector3[] verts = new Vector3[4];
            verts[0] = basePos + right * scaledBaseSize;      // right base corner
            verts[1] = tipPos;                                // tip
            verts[2] = basePos - right * scaledBaseSize;      // left base corner
            verts[3] = basePos + up * scaledBaseSize;         // top base corner

            // Debug logging for first few arrows
            if (i < 1)
            {
                Vector3 delta = tipPos - basePos;
                Debug.Log($"Arrow {i}:");
                Debug.Log($"  Original Magnitude: {mag:F3} m/s");
                Debug.Log($"  Normalized Magnitude: {magNorm:F3}");
                Debug.Log($"  Grid Cell Width: {gridCellWidth:F3}");
                Debug.Log($"  Proportional Length: {arrowLength:F3}");
                Debug.Log($"  Final Scaled Length: {arrowLength:F3}");
                Debug.Log($"  Wind Vector (u,w,v): ({uPhysical:F3}, {wPhysical:F3}, {vPhysical:F3})");
                Debug.Log($"  Start Point (tip): {tipPos}");
                Debug.Log($"  End Point (base): {basePos}");
                Debug.Log($"  Delta: {delta}");
            }

            int vertOffset = vertices.Count;
            vertices.AddRange(verts);
            foreach (int t in arrowTris)
                triangles.Add(vertOffset + t);

            uvs.AddRange(arrowUV);

            float mslNorm = (msl - minMsl) / (maxMsl - minMsl);
            // Always use actual magnitude for UV2 (coloring)
            for (int j = 0; j < verts.Length; j++)
                uv2s.Add(new Vector2(magNorm, 0));
            for (int j = 0; j < verts.Length; j++)
                uv3s.Add(new Vector2(mslNorm, 0));

            if (i % 2000 == 0)
                yield return null;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2s);
        mesh.SetUVs(2, uv3s);
        mesh.RecalculateNormals();

        MeshFilter mf = gameObject.GetComponent<MeshFilter>();
        if (!mf) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
        if (!mr) mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = windMaterial;
    }
}
