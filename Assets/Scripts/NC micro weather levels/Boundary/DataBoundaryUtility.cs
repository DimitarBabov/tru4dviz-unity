using System.Collections.Generic;
using UnityEngine;

public class DataBoundaryUtility : MonoBehaviour
{
    [Header("Configuration")]
    public DataContainer dataContainer;
    public bool autoCalculateOnDataLoad = true;
    public bool showDebugInfo = true;
    
    [Header("Streamlines Renderer Reference")]
    [Tooltip("Reference to WindFieldStreamlinesRenderer to use its spatial bounds for visualization")]
    public WindFieldStreamlinesRenderer streamlinesRenderer;
    public bool useStreamlinesBounds = true;
    
    [Header("Calculated Boundaries")]
    public Vector3 dataMin;
    public Vector3 dataMax;
    public Vector3 dataCenter;
    public Vector3 dataSize;
    public bool boundariesCalculated = false;
    
    [Header("Visualization")]
    public bool showBoundaryGizmos = true;
    public Color boundaryColor = Color.cyan;
    public Color centerColor = Color.yellow;
    public float gizmoLineWidth = 2f;
    
    [Header("Runtime Visualization")]
    public bool showRuntimeBoundary = true;
    public Material boundaryLineMaterial;
    public float runtimeLineWidth = 0.02f;
    public Color runtimeBoundaryColor = Color.cyan;
    
    private GameObject boundaryContainer;
    private LineRenderer[] boundaryLines;
    
    // Track previous bounds values for dynamic updates
    private float previousBoundsLeft = -1f;
    private float previousBoundsRight = -1f;
    private float previousBoundsFront = -1f;
    private float previousBoundsBack = -1f;
    private float previousMinAltitude = -1f;
    private float previousMaxAltitude = -1f;
    private bool previousUseStreamlinesBounds = false;
    
    void Start()
    {
        if (autoCalculateOnDataLoad)
        {
            StartCoroutine(WaitForDataAndCalculateBoundaries());
        }
    }
    
    System.Collections.IEnumerator WaitForDataAndCalculateBoundaries()
    {
        // Wait for data container to be loaded
        while (dataContainer == null || !dataContainer.IsLoaded)
        {
            yield return null;
        }
        
        CalculateBoundaries();
    }
    
    public void CalculateBoundaries()
    {
        if (dataContainer == null || !dataContainer.IsLoaded)
        {
            Debug.LogError("DataContainer is not loaded!");
            return;
        }
        
        var boundaries = GetDataBoundaries();
        if (!boundaries.HasValue)
        {
            Debug.LogError("Could not determine data boundaries!");
            boundariesCalculated = false;
            return;
        }
        
        dataMin = boundaries.Value.min;
        dataMax = boundaries.Value.max;
        dataCenter = (dataMin + dataMax) * 0.5f;
        dataSize = dataMax - dataMin;
        boundariesCalculated = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"Data boundaries calculated:");
            Debug.Log($"  Min: {dataMin}");
            Debug.Log($"  Max: {dataMax}");
            Debug.Log($"  Center: {dataCenter}");
            Debug.Log($"  Size: {dataSize}");
            Debug.Log($"  Volume: {dataSize.x * dataSize.y * dataSize.z:F3} cubic units");
        }
        
        // Create runtime boundary visualization
        if (showRuntimeBoundary)
        {
            CreateRuntimeBoundaryVisualization();
        }
    }
    
    private (Vector3 min, Vector3 max)? GetDataBoundaries()
    {
        // Check for x_from_origin and y_from_origin fields (for NcDataContainerImgs)
        var xField = dataContainer.GetType().GetField("x_from_origin");
        var yField = dataContainer.GetType().GetField("y_from_origin");
        
        if (xField != null && yField != null)
        {
            var xList = (List<float>)xField.GetValue(dataContainer);
            var yList = (List<float>)yField.GetValue(dataContainer);
            
            if (xList.Count == 0 || yList.Count == 0)
            {
                Debug.LogError("x_from_origin/y_from_origin are empty!");
                return null;
            }
            
            // Find min/max values
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minMsl = float.MaxValue, maxMsl = float.MinValue;
            
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
            
            Vector3 min = new Vector3(minX, minMsl, minY);
            Vector3 max = new Vector3(maxX, maxMsl, maxY);
            
            return (min, max);
        }
        else
        {
            // Fallback to lat/lon/msl if x_from_origin/y_from_origin not available
            if (dataContainer.lat.Count == 0)
            {
                Debug.LogError("No spatial data available in DataContainer!");
                return null;
            }
            
            float minLat = float.MaxValue, maxLat = float.MinValue;
            float minLon = float.MaxValue, maxLon = float.MinValue;
            float minMsl = float.MaxValue, maxMsl = float.MinValue;
            
            for (int i = 0; i < dataContainer.lat.Count; i++)
            {
                float lat = dataContainer.lat[i];
                float lon = dataContainer.lon[i];
                float msl = dataContainer.msl[i];
                
                if (lat < minLat) minLat = lat;
                if (lat > maxLat) maxLat = lat;
                if (lon < minLon) minLon = lon;
                if (lon > maxLon) maxLon = lon;
                if (msl < minMsl) minMsl = msl;
                if (msl > maxMsl) maxMsl = msl;
            }
            
            Vector3 min = new Vector3(minLon, minMsl, minLat);
            Vector3 max = new Vector3(maxLon, maxMsl, maxLat);
            
            return (min, max);
        }
    }
    
    // Public utility methods for other scripts to use
    public Vector3 GetRandomPositionInBounds()
    {
        if (!boundariesCalculated)
        {
            Debug.LogError("Boundaries not calculated yet!");
            return Vector3.zero;
        }
        
        float randomX = Random.Range(dataMin.x, dataMax.x);
        float randomY = Random.Range(dataMin.y, dataMax.y);
        float randomZ = Random.Range(dataMin.z, dataMax.z);
        
        return new Vector3(randomX, randomY, randomZ);
    }
    
    public Vector3 GetRandomPositionInBounds(Vector3 margin)
    {
        if (!boundariesCalculated)
        {
            Debug.LogError("Boundaries not calculated yet!");
            return Vector3.zero;
        }
        
        Vector3 adjustedMin = dataMin + margin;
        Vector3 adjustedMax = dataMax - margin;
        
        float randomX = Random.Range(adjustedMin.x, adjustedMax.x);
        float randomY = Random.Range(adjustedMin.y, adjustedMax.y);
        float randomZ = Random.Range(adjustedMin.z, adjustedMax.z);
        
        return new Vector3(randomX, randomY, randomZ);
    }
    
    public bool IsPositionInBounds(Vector3 position)
    {
        if (!boundariesCalculated)
        {
            return false;
        }
        
        return position.x >= dataMin.x && position.x <= dataMax.x &&
               position.y >= dataMin.y && position.y <= dataMax.y &&
               position.z >= dataMin.z && position.z <= dataMax.z;
    }
    
    public bool IsPositionInBounds(Vector3 position, Vector3 margin)
    {
        if (!boundariesCalculated)
        {
            return false;
        }
        
        Vector3 adjustedMin = dataMin + margin;
        Vector3 adjustedMax = dataMax - margin;
        
        return position.x >= adjustedMin.x && position.x <= adjustedMax.x &&
               position.y >= adjustedMin.y && position.y <= adjustedMax.y &&
               position.z >= adjustedMin.z && position.z <= adjustedMax.z;
    }
    
    public Vector3 ClampPositionToBounds(Vector3 position)
    {
        if (!boundariesCalculated)
        {
            return position;
        }
        
        return new Vector3(
            Mathf.Clamp(position.x, dataMin.x, dataMax.x),
            Mathf.Clamp(position.y, dataMin.y, dataMax.y),
            Mathf.Clamp(position.z, dataMin.z, dataMax.z)
        );
    }
    
    public Vector3 GetNormalizedPosition(Vector3 worldPosition)
    {
        if (!boundariesCalculated)
        {
            return Vector3.zero;
        }
        
        return new Vector3(
            (worldPosition.x - dataMin.x) / dataSize.x,
            (worldPosition.y - dataMin.y) / dataSize.y,
            (worldPosition.z - dataMin.z) / dataSize.z
        );
    }
    
    public Vector3 GetWorldPositionFromNormalized(Vector3 normalizedPosition)
    {
        if (!boundariesCalculated)
        {
            return Vector3.zero;
        }
        
        return new Vector3(
            dataMin.x + normalizedPosition.x * dataSize.x,
            dataMin.y + normalizedPosition.y * dataSize.y,
            dataMin.z + normalizedPosition.z * dataSize.z
        );
    }
    
    // Context menu methods for testing
    [ContextMenu("Recalculate Boundaries")]
    public void RecalculateBoundaries()
    {
        CalculateBoundaries();
    }
    
    [ContextMenu("Log Boundary Info")]
    public void LogBoundaryInfo()
    {
        if (boundariesCalculated)
        {
            Debug.Log($"Boundary Info:");
            Debug.Log($"  Min: {dataMin}");
            Debug.Log($"  Max: {dataMax}");
            Debug.Log($"  Center: {dataCenter}");
            Debug.Log($"  Size: {dataSize}");
            Debug.Log($"  Volume: {dataSize.x * dataSize.y * dataSize.z:F3} cubic units");
        }
        else
        {
            Debug.Log("Boundaries not calculated yet!");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showBoundaryGizmos || !boundariesCalculated) return;
        
        Vector3 minCorner = dataMin;
        Vector3 maxCorner = dataMax;
        Vector3 center = dataCenter;
        Vector3 size = dataSize;
        
        // Apply streamlines renderer spatial bounds if available and enabled
        if (useStreamlinesBounds && streamlinesRenderer != null)
        {
            // Calculate the actual bounds based on streamlines renderer spatial bounds
            Vector3 dataRange = dataMax - dataMin;
            
            // Apply horizontal bounds (left/right affects X, front/back affects Z)
            float leftBound = dataMin.x + streamlinesRenderer.boundsLeft * dataRange.x;
            float rightBound = dataMin.x + streamlinesRenderer.boundsRight * dataRange.x;
            float frontBound = dataMin.z + streamlinesRenderer.boundsFront * dataRange.z;
            float backBound = dataMin.z + streamlinesRenderer.boundsBack * dataRange.z;
            
            // Apply vertical bounds (min/max altitude affects Y)
            float bottomBound = dataMin.y + streamlinesRenderer.minAltitude * dataRange.y;
            float topBound = dataMin.y + streamlinesRenderer.maxAltitude * dataRange.y;
            
            // Update bounds for gizmo drawing
            minCorner = new Vector3(leftBound, bottomBound, frontBound);
            maxCorner = new Vector3(rightBound, topBound, backBound);
            center = (minCorner + maxCorner) * 0.5f;
            size = maxCorner - minCorner;
        }
        
        // Draw the boundary box
        Gizmos.color = boundaryColor;
        DrawWireCube(center, size);
        
        // Draw center point
        Gizmos.color = centerColor;
        Gizmos.DrawSphere(center, Mathf.Min(size.x, size.y, size.z) * 0.02f);
        
        // Draw corner points
        Gizmos.color = Color.red;
        float cornerSize = Mathf.Min(size.x, size.y, size.z) * 0.01f;
        
        // 8 corners of the bounding box
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(minCorner.x, minCorner.y, minCorner.z), // min corner
            new Vector3(maxCorner.x, minCorner.y, minCorner.z),
            new Vector3(minCorner.x, maxCorner.y, minCorner.z),
            new Vector3(maxCorner.x, maxCorner.y, minCorner.z),
            new Vector3(minCorner.x, minCorner.y, maxCorner.z),
            new Vector3(maxCorner.x, minCorner.y, maxCorner.z),
            new Vector3(minCorner.x, maxCorner.y, maxCorner.z),
            new Vector3(maxCorner.x, maxCorner.y, maxCorner.z)  // max corner
        };
        
        foreach (Vector3 corner in corners)
        {
            Gizmos.DrawSphere(corner, cornerSize);
        }
        
        // Draw coordinate axes from center
        Gizmos.color = Color.red;   // X-axis
        Gizmos.DrawRay(center, Vector3.right * size.x * 0.1f);
        Gizmos.color = Color.green; // Y-axis
        Gizmos.DrawRay(center, Vector3.up * size.y * 0.1f);
        Gizmos.color = Color.blue;  // Z-axis
        Gizmos.DrawRay(center, Vector3.forward * size.z * 0.1f);
    }
    
    void DrawWireCube(Vector3 center, Vector3 size)
    {
        Vector3 halfSize = size * 0.5f;
        
        // Bottom face
        Vector3 p1 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 p2 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 p3 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        Vector3 p4 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        
        // Top face
        Vector3 p5 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        Vector3 p6 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        Vector3 p7 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        Vector3 p8 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
        
        // Draw bottom face
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
        
        // Draw top face
        Gizmos.DrawLine(p5, p6);
        Gizmos.DrawLine(p6, p7);
        Gizmos.DrawLine(p7, p8);
        Gizmos.DrawLine(p8, p5);
        
        // Draw vertical edges
        Gizmos.DrawLine(p1, p5);
        Gizmos.DrawLine(p2, p6);
        Gizmos.DrawLine(p3, p7);
        Gizmos.DrawLine(p4, p8);
    }
    
    void CreateRuntimeBoundaryVisualization()
    {
        // Clean up existing boundary visualization
        DestroyRuntimeBoundaryVisualization();
        
        // Create container for boundary lines
        boundaryContainer = new GameObject("DataBoundaryVisualization");
        boundaryContainer.transform.SetParent(transform);
        
        // Create 12 line renderers for the 12 edges of a cube
        boundaryLines = new LineRenderer[12];
        
        Vector3[] corners = GetBoundaryCorners();
        
        // Define the 12 edges of a cube (pairs of corner indices)
        int[,] edges = new int[12, 2]
        {
            // Bottom face edges
            {0, 1}, {1, 2}, {2, 3}, {3, 0},
            // Top face edges  
            {4, 5}, {5, 6}, {6, 7}, {7, 4},
            // Vertical edges
            {0, 4}, {1, 5}, {2, 6}, {3, 7}
        };
        
        for (int i = 0; i < 12; i++)
        {
            GameObject lineObj = new GameObject($"BoundaryEdge_{i}");
            lineObj.transform.SetParent(boundaryContainer.transform);
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = boundaryLineMaterial;
            lr.startColor = runtimeBoundaryColor;
            lr.endColor = runtimeBoundaryColor;
            lr.startWidth = runtimeLineWidth;
            lr.endWidth = runtimeLineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            
            // Set the two points for this edge
            Vector3 start = corners[edges[i, 0]];
            Vector3 end = corners[edges[i, 1]];
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            
            boundaryLines[i] = lr;
        }
        
        Debug.Log("Runtime boundary visualization created");
    }
    
    void DestroyRuntimeBoundaryVisualization()
    {
        if (boundaryContainer != null)
        {
            if (Application.isPlaying)
            {
                Destroy(boundaryContainer);
            }
            else
            {
                DestroyImmediate(boundaryContainer);
            }
            boundaryContainer = null;
            boundaryLines = null;
        }
    }
    
    Vector3[] GetBoundaryCorners()
    {
        Vector3 minCorner = dataMin;
        Vector3 maxCorner = dataMax;
        
        // Apply streamlines renderer spatial bounds if available and enabled
        if (useStreamlinesBounds && streamlinesRenderer != null)
        {
            // Calculate the actual bounds based on streamlines renderer spatial bounds
            Vector3 dataRange = dataMax - dataMin;
            
            // Apply horizontal bounds (left/right affects X, front/back affects Z)
            float leftBound = dataMin.x + streamlinesRenderer.boundsLeft * dataRange.x;
            float rightBound = dataMin.x + streamlinesRenderer.boundsRight * dataRange.x;
            float frontBound = dataMin.z + streamlinesRenderer.boundsFront * dataRange.z;
            float backBound = dataMin.z + streamlinesRenderer.boundsBack * dataRange.z;
            
            // Apply vertical bounds (min/max altitude affects Y)
            float bottomBound = dataMin.y + streamlinesRenderer.minAltitude * dataRange.y;
            float topBound = dataMin.y + streamlinesRenderer.maxAltitude * dataRange.y;
            
            // Update min/max corners with streamlines bounds
            minCorner = new Vector3(leftBound, bottomBound, frontBound);
            maxCorner = new Vector3(rightBound, topBound, backBound);
            
            if (showDebugInfo)
            {
                Debug.Log($"Using streamlines spatial bounds:");
                Debug.Log($"  Left: {streamlinesRenderer.boundsLeft:F3} -> X: {leftBound:F3}");
                Debug.Log($"  Right: {streamlinesRenderer.boundsRight:F3} -> X: {rightBound:F3}");
                Debug.Log($"  Front: {streamlinesRenderer.boundsFront:F3} -> Z: {frontBound:F3}");
                Debug.Log($"  Back: {streamlinesRenderer.boundsBack:F3} -> Z: {backBound:F3}");
                Debug.Log($"  Min Altitude: {streamlinesRenderer.minAltitude:F3} -> Y: {bottomBound:F3}");
                Debug.Log($"  Max Altitude: {streamlinesRenderer.maxAltitude:F3} -> Y: {topBound:F3}");
            }
        }
        
        return new Vector3[8]
        {
            new Vector3(minCorner.x, minCorner.y, minCorner.z), // 0: min corner
            new Vector3(maxCorner.x, minCorner.y, minCorner.z), // 1
            new Vector3(maxCorner.x, minCorner.y, maxCorner.z), // 2
            new Vector3(minCorner.x, minCorner.y, maxCorner.z), // 3
            new Vector3(minCorner.x, maxCorner.y, minCorner.z), // 4
            new Vector3(maxCorner.x, maxCorner.y, minCorner.z), // 5
            new Vector3(maxCorner.x, maxCorner.y, maxCorner.z), // 6
            new Vector3(minCorner.x, maxCorner.y, maxCorner.z)  // 7: max corner
        };
    }
    
    public void UpdateRuntimeBoundaryVisualization()
    {
        if (showRuntimeBoundary && boundariesCalculated)
        {
            CreateRuntimeBoundaryVisualization();
        }
        else
        {
            DestroyRuntimeBoundaryVisualization();
        }
    }
    
    void OnDestroy()
    {
        DestroyRuntimeBoundaryVisualization();
    }
    
    void Update()
    {
        // Monitor streamlines renderer bounds for dynamic updates
        if (useStreamlinesBounds && streamlinesRenderer != null && boundariesCalculated)
        {
            bool boundsChanged = false;
            
            // Check if any bounds have changed
            if (previousBoundsLeft != streamlinesRenderer.boundsLeft ||
                previousBoundsRight != streamlinesRenderer.boundsRight ||
                previousBoundsFront != streamlinesRenderer.boundsFront ||
                previousBoundsBack != streamlinesRenderer.boundsBack ||
                previousMinAltitude != streamlinesRenderer.minAltitude ||
                previousMaxAltitude != streamlinesRenderer.maxAltitude ||
                previousUseStreamlinesBounds != useStreamlinesBounds)
            {
                boundsChanged = true;
            }
            
            // Update boundary visualization if bounds changed
            if (boundsChanged)
            {
                // Store current values
                previousBoundsLeft = streamlinesRenderer.boundsLeft;
                previousBoundsRight = streamlinesRenderer.boundsRight;
                previousBoundsFront = streamlinesRenderer.boundsFront;
                previousBoundsBack = streamlinesRenderer.boundsBack;
                previousMinAltitude = streamlinesRenderer.minAltitude;
                previousMaxAltitude = streamlinesRenderer.maxAltitude;
                previousUseStreamlinesBounds = useStreamlinesBounds;
                
                // Refresh visualization
                if (showRuntimeBoundary)
                {
                    CreateRuntimeBoundaryVisualization();
                }
                
                if (showDebugInfo)
                {
                    Debug.Log("Boundary visualization updated due to streamlines bounds change");
                }
            }
        }
        else if (!useStreamlinesBounds && previousUseStreamlinesBounds)
        {
            // Switched from using streamlines bounds to not using them
            previousUseStreamlinesBounds = useStreamlinesBounds;
            if (showRuntimeBoundary && boundariesCalculated)
            {
                CreateRuntimeBoundaryVisualization();
            }
        }
    }
    
    [ContextMenu("Toggle Runtime Boundary")]
    public void ToggleRuntimeBoundary()
    {
        showRuntimeBoundary = !showRuntimeBoundary;
        UpdateRuntimeBoundaryVisualization();
    }
    
    [ContextMenu("Toggle Streamlines Bounds Usage")]
    public void ToggleStreamlinesBoundsUsage()
    {
        useStreamlinesBounds = !useStreamlinesBounds;
        UpdateRuntimeBoundaryVisualization();
        Debug.Log($"Using streamlines bounds: {useStreamlinesBounds}");
    }
    
    [ContextMenu("Update Boundary Visualization")]
    public void UpdateBoundaryVisualizationFromStreamlines()
    {
        if (showRuntimeBoundary && boundariesCalculated)
        {
            CreateRuntimeBoundaryVisualization();
        }
    }
    
    // Public method for external scripts to trigger boundary update
    public void RefreshBoundaryVisualization()
    {
        UpdateBoundaryVisualizationFromStreamlines();
    }
} 