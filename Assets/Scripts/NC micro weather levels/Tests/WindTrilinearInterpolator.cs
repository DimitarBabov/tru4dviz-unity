using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WindTrilinearInterpolator : MonoBehaviour
{
    [Header("Data Source")]
    public NcDataContainerImgs dataContainer;
    
    [Header("Interpolation Settings")]
    public Vector3 startPosition = new Vector3(0f, 0f, 0f); // Normalized grid position (0-1)
    public int maxSteps = 100;
    public float stepScale = 0.1f; // Multiplier for wind vector steps (1.0 = match WindFieldMeshNc arrow length, 0.1 = 10% for detailed paths)
    
    [Header("Grid Info")]
    public Vector3Int gridDimensions;
    public Vector3 gridMin;
    public Vector3 gridMax;
    
    [Header("Results")]
    public List<Vector3> interpolatedPath = new List<Vector3>();
    public List<Vector3> interpolatedPathWorldCoords = new List<Vector3>();
    public List<Vector3> windVectors = new List<Vector3>();
    public List<float> magnitudes = new List<float>();
    
    [Header("Visualization")]
    public LineRenderer lineRenderer;
    public Material lineMaterial;
    public float lineWidth = 0.1f;
    public Color lineColor = Color.green;
    public bool showStartCube = true;
    public bool showEndCube = true;
    public float cubeSize = 10f;
    
    [Header("Surrounding Points Visualization")]
    public bool showSurroundingPoints = true;
    public float cubeLength = 2.0f;
    public float cubeWidth = 0.1f;
    public Color surroundingPointColor = Color.cyan;
    
    private Dictionary<Vector3Int, int> gridToIndex = new Dictionary<Vector3Int, int>();
    private Vector3Int[] uniqueGridPositions;
    private GameObject startCube;
    private GameObject endCube;
    private float gridCellWidth; // Store grid cell width for adaptive scaling
    private GameObject surroundingPointsParent;
    private HashSet<Vector3Int> usedGridPoints = new HashSet<Vector3Int>();
    
    void Start()
    {
        SetupLineRenderer();
        
        if (dataContainer != null && dataContainer.IsLoaded)
        {
            BuildGridStructure();
            PerformInterpolation();
        }
        else
        {
            Debug.LogWarning("Data container not loaded yet. Will try again in Update.");
        }
    }
    
    void Update()
    {
        if (dataContainer != null && dataContainer.IsLoaded && gridDimensions == Vector3Int.zero)
        {
            BuildGridStructure();
            PerformInterpolation();
        }
    }
    
    void BuildGridStructure()
    {
        if (dataContainer.x_from_origin.Count == 0) return;
        
        // Find unique positions for each dimension
        var uniqueX = dataContainer.x_from_origin.Distinct().OrderBy(x => x).ToList();
        var uniqueY = dataContainer.y_from_origin.Distinct().OrderBy(y => y).ToList();
        var uniqueZ = dataContainer.msl.Distinct().OrderBy(z => z).ToList();
        
        gridDimensions = new Vector3Int(uniqueX.Count, uniqueY.Count, uniqueZ.Count);
        // Use same coordinate system as DataBoundaryUtility: (X, MSL, Y)
        gridMin = new Vector3(uniqueX.Min(), uniqueZ.Min(), uniqueY.Min());
        gridMax = new Vector3(uniqueX.Max(), uniqueZ.Max(), uniqueY.Max());
        
        // Calculate grid cell width (same as WindFieldMeshNc)
        gridCellWidth = uniqueX.Count > 1 ? Mathf.Abs(uniqueX[1] - uniqueX[0]) : 1f;
        
        //Debug.Log($"Grid dimensions: {gridDimensions}");
       // Debug.Log($"Grid bounds: {gridMin} to {gridMax}");
        //Debug.Log($"Grid cell width: {gridCellWidth:F3}");
        
        // Build mapping from grid coordinates to data indices
        gridToIndex.Clear();
        uniqueGridPositions = new Vector3Int[dataContainer.x_from_origin.Count];
        
        for (int i = 0; i < dataContainer.x_from_origin.Count; i++)
        {
            int xIdx = uniqueX.FindIndex(x => Mathf.Approximately(x, dataContainer.x_from_origin[i]));
            int yIdx = uniqueY.FindIndex(y => Mathf.Approximately(y, dataContainer.y_from_origin[i]));
            int zIdx = uniqueZ.FindIndex(z => Mathf.Approximately(z, dataContainer.msl[i]));
            
            Vector3Int gridPos = new Vector3Int(xIdx, yIdx, zIdx);
            uniqueGridPositions[i] = gridPos;
            
            if (!gridToIndex.ContainsKey(gridPos))
            {
                gridToIndex[gridPos] = i;
            }
        }
        
        //Debug.Log($"Built grid structure with {gridToIndex.Count} unique positions");
    }
    
    void PerformInterpolation()
    {
        interpolatedPath.Clear();
        interpolatedPathWorldCoords.Clear();
        windVectors.Clear();
        magnitudes.Clear();
        usedGridPoints.Clear();
        
        if (gridDimensions == Vector3Int.zero) return;
        
        // Convert normalized start position to grid coordinates
        Vector3 currentPos = new Vector3(
            startPosition.x * (gridDimensions.x - 1),
            startPosition.y * (gridDimensions.y - 1),
            startPosition.z * (gridDimensions.z - 1)
        );
        
        for (int step = 0; step < maxSteps; step++)
        {
            // Check bounds with tolerance - allow small overshoot to show exit direction
            float boundaryTolerance = 0.3f;
            if (currentPos.x < -boundaryTolerance || currentPos.x > gridDimensions.x - 1 + boundaryTolerance ||
                currentPos.y < -boundaryTolerance || currentPos.y > gridDimensions.y - 1 + boundaryTolerance ||
                currentPos.z < -boundaryTolerance || currentPos.z > gridDimensions.z - 1 + boundaryTolerance)
            {
                break;
            }
            
            // Store current position first
            interpolatedPath.Add(currentPos);
            interpolatedPathWorldCoords.Add(GridToWorldPosition(currentPos));
            
            // Perform trilinear interpolation
            Vector3 windVector = TrilinearInterpolate(currentPos);
            float magnitude = windVector.magnitude;
            
            // Store wind data
            windVectors.Add(windVector);
            magnitudes.Add(magnitude);
            
            // Use wind vector directly as step (already represents direction and magnitude)
            if (magnitude > 0.001f) // Avoid division by zero
            {
                Vector3 nextPos = currentPos + windVector;
                
                // Check if next step would go outside boundary with tolerance
                if (nextPos.x < -boundaryTolerance || nextPos.x > gridDimensions.x - 1 + boundaryTolerance ||
                    nextPos.y < -boundaryTolerance || nextPos.y > gridDimensions.y - 1 + boundaryTolerance ||
                    nextPos.z < -boundaryTolerance || nextPos.z > gridDimensions.z - 1 + boundaryTolerance)
                {
                    break;
                }
                
                currentPos = nextPos;
            }
            else
            {
                break;
            }
        }
        
        UpdateLineRenderer();
        UpdateStartCube();
        UpdateEndCube();
        UpdateSurroundingPoints();
    }
    


    
    Vector3 TrilinearInterpolate(Vector3 position)
    {
        // Clamp position to valid grid bounds for interpolation
        Vector3 clampedPos = new Vector3(
            Mathf.Clamp(position.x, 0f, gridDimensions.x - 1f),
            Mathf.Clamp(position.y, 0f, gridDimensions.y - 1f),
            Mathf.Clamp(position.z, 0f, gridDimensions.z - 1f)
        );
        
        // Get the 8 surrounding grid points
        int x0 = Mathf.FloorToInt(clampedPos.x);
        int y0 = Mathf.FloorToInt(clampedPos.y);
        int z0 = Mathf.FloorToInt(clampedPos.z);
        int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
        int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
        int z1 = Mathf.Min(z0 + 1, gridDimensions.z - 1);
        
        // Get fractional parts
        float fx = clampedPos.x - x0;
        float fy = clampedPos.y - y0;
        float fz = clampedPos.z - z0;
        
        // Get wind vectors at the 8 corners and track used grid points
        Vector3 c000 = GetWindAtGridPoint(x0, y0, z0);
        Vector3 c001 = GetWindAtGridPoint(x0, y0, z1);
        Vector3 c010 = GetWindAtGridPoint(x0, y1, z0);
        Vector3 c011 = GetWindAtGridPoint(x0, y1, z1);
        Vector3 c100 = GetWindAtGridPoint(x1, y0, z0);
        Vector3 c101 = GetWindAtGridPoint(x1, y0, z1);
        Vector3 c110 = GetWindAtGridPoint(x1, y1, z0);
        Vector3 c111 = GetWindAtGridPoint(x1, y1, z1);
        
        // Track all 8 corner points used in this interpolation
        usedGridPoints.Add(new Vector3Int(x0, y0, z0));
        usedGridPoints.Add(new Vector3Int(x0, y0, z1));
        usedGridPoints.Add(new Vector3Int(x0, y1, z0));
        usedGridPoints.Add(new Vector3Int(x0, y1, z1));
        usedGridPoints.Add(new Vector3Int(x1, y0, z0));
        usedGridPoints.Add(new Vector3Int(x1, y0, z1));
        usedGridPoints.Add(new Vector3Int(x1, y1, z0));
        usedGridPoints.Add(new Vector3Int(x1, y1, z1));
        
        // Trilinear interpolation
        Vector3 c00 = Vector3.Lerp(c000, c100, fx);
        Vector3 c01 = Vector3.Lerp(c001, c101, fx);
        Vector3 c10 = Vector3.Lerp(c010, c110, fx);
        Vector3 c11 = Vector3.Lerp(c011, c111, fx);
        
        Vector3 c0 = Vector3.Lerp(c00, c10, fy);
        Vector3 c1 = Vector3.Lerp(c01, c11, fy);
        
        return Vector3.Lerp(c0, c1, fz);
    }
    
    public Vector3 GetWindAtGridPoint(int x, int y, int z)
    {
        Vector3Int gridPos = new Vector3Int(x, y, z);
        
        if (gridToIndex.TryGetValue(gridPos, out int dataIndex))
        {
            // Get normalized values and denormalize them back to physical values
            float uNorm = dataContainer.u_norm[dataIndex];
            float vNorm = dataContainer.v_norm[dataIndex];
            float wNorm = dataContainer.w_norm[dataIndex];
            
            // Denormalize to get actual physical wind components
            Vector2 uMinMax = dataContainer.uMinMax;
            Vector2 vMinMax = dataContainer.vMinMax;
            Vector2 wMinMax = dataContainer.wMinMax;
            
            float uPhysical = uMinMax.x + uNorm * (uMinMax.y - uMinMax.x);
            float vPhysical = vMinMax.x + vNorm * (vMinMax.y - vMinMax.x);
            float wPhysical = wMinMax.x + wNorm * (wMinMax.y - wMinMax.x);
            
            // Create physical wind vector: (u, w, v) -> (X, Y, Z) in grid space
            Vector3 physicalWind = new Vector3(uPhysical, wPhysical, vPhysical);
            
            // Get actual magnitude for validation (should match calculated magnitude)
            float magnitude = dataContainer.mag[dataIndex];
            float calculatedMagnitude = physicalWind.magnitude;
            
            // Apply coordinate transformation to match world space: (X, Y, Z) -> (X, Z, Y)
            Vector3 gridSpaceDirection = new Vector3(physicalWind.x, physicalWind.z, physicalWind.y);
            
            // Apply stepScale directly to the physical wind vector
            return gridSpaceDirection * stepScale;
        }
        
        // Return zero vector if no data at this grid point
        return Vector3.zero;
    }
    
    void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }
        
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
    }
    
    void UpdateLineRenderer()
    {
        if (lineRenderer == null) return;
        
        lineRenderer.positionCount = interpolatedPathWorldCoords.Count;
        for (int i = 0; i < interpolatedPathWorldCoords.Count; i++)
        {
            lineRenderer.SetPosition(i, interpolatedPathWorldCoords[i]);
        }
    }
    
    void UpdateStartCube()
    {
        if (!showStartCube || interpolatedPathWorldCoords.Count < 2)
        {
            if (startCube != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(startCube);
                }
                else
                {
                    DestroyImmediate(startCube);
                }
                startCube = null;
            }
            return;
        }
        
        // Create start arrow if it doesn't exist
        if (startCube == null)
        {
            startCube = new GameObject("StartPositionArrow");
            startCube.transform.SetParent(transform);
            
            // Add mesh components
            MeshFilter meshFilter = startCube.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = startCube.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.SetFloat("_Mode", 3); // Set to Transparent mode
            meshRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            meshRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            meshRenderer.material.SetInt("_ZWrite", 0);
            meshRenderer.material.DisableKeyword("_ALPHATEST_ON");
            meshRenderer.material.EnableKeyword("_ALPHABLEND_ON");
            meshRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            meshRenderer.material.renderQueue = 3000;
            Color blueColor = Color.blue;
            blueColor.a = 0.25f; // Set opacity to 25%
            meshRenderer.material.color = blueColor;
        }
        
        // Base center at first position, tip at second position
        Vector3 baseCenter = interpolatedPathWorldCoords[0];
        Vector3 tipPos = interpolatedPathWorldCoords[1];
        Vector3 arrowDirection = (tipPos - baseCenter).normalized;
        float arrowLength = Vector3.Distance(baseCenter, tipPos);
        
        // Create arrow mesh with actual size
        Mesh arrowMesh = CreateArrowMeshWithSize(arrowLength);
        startCube.GetComponent<MeshFilter>().mesh = arrowMesh;
        
        // Position so tip is at first position (translate back by scaled arrow length)
        Vector3 arrowBasePosition = baseCenter - arrowDirection * (arrowLength * 0.5f);
        startCube.transform.position = arrowBasePosition;
        if (arrowDirection != Vector3.zero)
        {
            startCube.transform.rotation = Quaternion.LookRotation(arrowDirection);
        }
        startCube.transform.localScale = Vector3.one;
    }
    
    void UpdateEndCube()
    {
        if (!showEndCube || interpolatedPathWorldCoords.Count < 2)
        {
            if (endCube != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(endCube);
                }
                else
                {
                    DestroyImmediate(endCube);
                }
                endCube = null;
            }
            return;
        }
        
        // Create end arrow if it doesn't exist
        if (endCube == null)
        {
            endCube = new GameObject("EndPositionArrow");
            endCube.transform.SetParent(transform);
            
            // Add mesh components
            MeshFilter meshFilter = endCube.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = endCube.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.SetFloat("_Mode", 3); // Set to Transparent mode
            meshRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            meshRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            meshRenderer.material.SetInt("_ZWrite", 0);
            meshRenderer.material.DisableKeyword("_ALPHATEST_ON");
            meshRenderer.material.EnableKeyword("_ALPHABLEND_ON");
            meshRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            meshRenderer.material.renderQueue = 3000;
            Color redColor = Color.red;
            redColor.a = 0.25f; // Set opacity to 25%
            meshRenderer.material.color = redColor;
        }
        
        // Base center at second-to-last position, tip at last position
        int lastIndex = interpolatedPathWorldCoords.Count - 1;
        Vector3 baseCenter = interpolatedPathWorldCoords[lastIndex - 1];
        Vector3 tipPos = interpolatedPathWorldCoords[lastIndex];
        Vector3 arrowDirection = (tipPos - baseCenter).normalized;
        float arrowLength = Vector3.Distance(baseCenter, tipPos);
        
        // Create arrow mesh with actual size
        Mesh arrowMesh = CreateArrowMeshWithSize(arrowLength);
        endCube.GetComponent<MeshFilter>().mesh = arrowMesh;
        
        // Position so base center is at last position (translate forward by scaled arrow length)
        Vector3 arrowBasePosition = tipPos - arrowDirection * (arrowLength * 0.5f);
        endCube.transform.position = arrowBasePosition;
        if (arrowDirection != Vector3.zero)
        {
            endCube.transform.rotation = Quaternion.LookRotation(arrowDirection);
        }
        endCube.transform.localScale = Vector3.one;
    }
    
    void UpdateSurroundingPoints()
    {
        // Clear existing surrounding points
        if (surroundingPointsParent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(surroundingPointsParent);
            }
            else
            {
                DestroyImmediate(surroundingPointsParent);
            }
        }
        
        if (!showSurroundingPoints || usedGridPoints.Count == 0)
        {
            return;
        }
        
        // Create parent object for surrounding points
        surroundingPointsParent = new GameObject("SurroundingPoints");
        surroundingPointsParent.transform.SetParent(transform);
        
        Debug.Log($"Creating {usedGridPoints.Count} surrounding point wind direction cubes");
        
        // Create an elongated cube for each unique grid point used in interpolation
        int cubeIndex = 0;
        foreach (Vector3Int gridPoint in usedGridPoints)
        {
            // Convert grid position to world position
            Vector3 worldPos = GridToWorldPosition(new Vector3(gridPoint.x, gridPoint.y, gridPoint.z));
            
            // Get wind vector at this grid point
            Vector3 windVector = GetWindAtGridPoint(gridPoint.x, gridPoint.y, gridPoint.z);
            
            if (windVector.magnitude > 0.001f) // Only create cube if there's wind data
            {
                // Create cube
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"GridPoint_{gridPoint.x}_{gridPoint.y}_{gridPoint.z}";
                cube.transform.SetParent(surroundingPointsParent.transform);
                cube.transform.position = worldPos;
                
                // Get the raw wind components directly from data (same as WindFieldMeshNc)
                Vector3Int gridPos = new Vector3Int(gridPoint.x, gridPoint.y, gridPoint.z);
                if (gridToIndex.TryGetValue(gridPos, out int dataIndex))
                {
                    float uNorm = dataContainer.u_norm[dataIndex];
                    float vNorm = dataContainer.v_norm[dataIndex];
                    float wNorm = dataContainer.w_norm[dataIndex];
                    
                    // Use exact same wind vector calculation as WindFieldMeshNc
                    Vector3 windVecRaw = new Vector3(uNorm, wNorm, vNorm);
                    
                    // Set rotation to align with wind direction (same as WindFieldMeshNc)
                    if (windVecRaw.magnitude > 0.001f)
                    {
                        cube.transform.rotation = Quaternion.LookRotation(windVecRaw.normalized);
                    }
                }
                
                // Set scale: elongated in Z direction (forward), thin in X and Y
                cube.transform.localScale = new Vector3(cubeWidth, cubeWidth, cubeLength);
                
                // Set material and color
                Renderer cubeRenderer = cube.GetComponent<Renderer>();
                cubeRenderer.material = new Material(Shader.Find("Standard"));
                cubeRenderer.material.color = surroundingPointColor;
                
                cubeIndex++;
            }
        }
        
        Debug.Log($"Created {cubeIndex} surrounding point wind direction cubes at grid positions used in trilinear interpolation");
    }
    
    [ContextMenu("Regenerate Path")]
    public void RegeneratePath()
    {
        if (dataContainer != null && dataContainer.IsLoaded)
        {
            PerformInterpolation();
        }
    }
    

    
    Mesh CreateArrowMeshWithSize(float arrowLength)
    {
        // Scale down the entire arrow by half
        float scaledLength = arrowLength * 0.5f;
        
        // Calculate base size proportional to scaled arrow length
        float baseSize = scaledLength * 0.1f; // Base is 10% of scaled arrow length
        
        // Arrow mesh template (local space, tip along +Z) - similar to WindFieldMeshNc
        Vector3[] arrowVerts = new Vector3[]
        {
            new Vector3(-baseSize, 0, 0),        // left base corner
            new Vector3(0, 0, scaledLength),     // tip (pointing forward)
            new Vector3(baseSize, 0, 0),         // right base corner
            new Vector3(0, baseSize, 0)          // top base corner
        };
        
        int[] arrowTris = new int[] { 
            0, 1, 2,  // bottom triangle
            0, 3, 1,  // left triangle
            2, 1, 3   // right triangle
        };
        
        Vector2[] arrowUV = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0.5f, 1),
            new Vector2(1, 0),
            new Vector2(0.5f, 0)
        };
        
        Mesh mesh = new Mesh();
        mesh.vertices = arrowVerts;
        mesh.triangles = arrowTris;
        mesh.uv = arrowUV;
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    void OnDestroy()
    {
        if (startCube != null)
        {
            if (Application.isPlaying)
            {
                Destroy(startCube);
            }
            else
            {
                DestroyImmediate(startCube);
            }
        }
        
        if (endCube != null)
        {
            if (Application.isPlaying)
            {
                Destroy(endCube);
            }
            else
            {
                DestroyImmediate(endCube);
            }
        }
        
        if (surroundingPointsParent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(surroundingPointsParent);
            }
            else
            {
                DestroyImmediate(surroundingPointsParent);
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (interpolatedPath.Count == 0) return;
        
        // Draw the interpolated path
        Gizmos.color = Color.green;
        for (int i = 0; i < interpolatedPath.Count - 1; i++)
        {
            Vector3 worldPos1 = GridToWorldPosition(interpolatedPath[i]);
            Vector3 worldPos2 = GridToWorldPosition(interpolatedPath[i + 1]);
            Gizmos.DrawLine(worldPos1, worldPos2);
        }
        
        // Draw wind vectors at each point
        Gizmos.color = Color.red;
        for (int i = 0; i < interpolatedPath.Count; i++)
        {
            Vector3 worldPos = GridToWorldPosition(interpolatedPath[i]);
            Vector3 windDir = windVectors[i].normalized;
            float magnitude = magnitudes[i];
            
            // Scale the arrow based on magnitude
            float arrowLength = Mathf.Clamp(magnitude * 0.1f, 0.1f, 2.0f);
            Gizmos.DrawRay(worldPos, windDir * arrowLength);
            
            // Draw a small sphere at each point
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(worldPos, 0.05f);
            Gizmos.color = Color.red;
        }
        
        // Draw start position
        if (interpolatedPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Vector3 startWorldPos = GridToWorldPosition(interpolatedPath[0]);
            Gizmos.DrawSphere(startWorldPos, 0.2f);
        }
    }
    
    public Vector3 GridToWorldPosition(Vector3 gridPos)
    {
        // Convert grid coordinates back to world coordinates
        if (gridDimensions == Vector3Int.zero) return gridPos;
        
        Vector3 normalizedPos = new Vector3(
            gridPos.x / (gridDimensions.x - 1),
            gridPos.y / (gridDimensions.y - 1),
            gridPos.z / (gridDimensions.z - 1)
        );
        
        // Convert grid coordinates to world coordinates using same system as DataBoundaryUtility
        // Grid: (X, Y, Z) -> World: (X, MSL, Y)
        // Use LerpUnclamped to allow extrapolation beyond boundaries
        return new Vector3(
            Mathf.LerpUnclamped(gridMin.x, gridMax.x, normalizedPos.x),  // X from x_from_origin
            Mathf.LerpUnclamped(gridMin.y, gridMax.y, normalizedPos.z),  // MSL from msl (Z grid -> Y world)
            Mathf.LerpUnclamped(gridMin.z, gridMax.z, normalizedPos.y)   // Y from y_from_origin (Y grid -> Z world)
        );
    }
} 