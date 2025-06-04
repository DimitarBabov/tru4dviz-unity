using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WindStreamlineCalculator : MonoBehaviour
{
    [Header("Data Source")]
    public NcDataContainerImgs dataContainer;
    
    [Header("Debug Path")]
    public WindTrilinearInterpolator debugPath;
    
    [Header("Interpolation Settings")]
    public Vector3 startPosition = new Vector3(0f, 0f, 0f); // Normalized grid position (0-1)
    public int maxSteps = 100;
    public float stepScale = 0.1f; // Multiplier for wind vector steps (1.0 = match WindFieldMeshNc arrow length, 0.1 = 10% for detailed paths)
    
    [Header("Backward Tracing Settings")]
    [Tooltip("Enable backward tracing for streamlines that immediately exit boundaries")]
    public bool enableBackwardTracing = true;
    [Tooltip("Maximum steps to trace backward from start point")]
    public int maxBackwardSteps = 50;
    
    [Header("Texture Flow Settings")]
    public float streamlineTexReferenceLength = 100f; // Reference length for texture flow speed normalization
    public bool useRandomTextureOffset = true; // Enable random UV offset for this streamline
    [Range(0f, 10f)]
    public float randomOffsetRange = 2f; // Range for random UV offset (0-10)
    
    [Header("Grid Info")]
    public Vector3Int gridDimensions;
    public Vector3 gridMin;
    public Vector3 gridMax;
    
    [Header("Results")]
    public List<Vector3> interpolatedPath = new List<Vector3>();
    public List<Vector3> interpolatedPathWorldCoords = new List<Vector3>();
    public List<Vector3> windVectors = new List<Vector3>();
    public List<float> magnitudes = new List<float>();
    public List<float> normalizedMagnitudes = new List<float>(); // Normalized magnitude values (0-1)
    public List<float> normalizedMsl = new List<float>(); // Normalized MSL values (0-1)
    public float averageMagnitudeNormalization = 0f; // For texture animation
    public float lengthMultiplier = 1f; // For texture flow speed normalization
    public float randomTextureOffset = 0f; // Random UV offset for this streamline
    
    [Header("Normalization Data")]
    public float minMsl = float.MaxValue;
    public float maxMsl = float.MinValue;
    public float maxMagnitude = 1.0f;
    
    [Header("Line Simplification")]
    public bool enableSimplification = false;
    [Range(0.001f, 1.0f)]
    public float simplificationTolerance = 0.1f; // Distance tolerance for line simplification (in world units)
    
    private Dictionary<Vector3Int, int> gridToIndex = new Dictionary<Vector3Int, int>();
    private Vector3Int[] uniqueGridPositions;
    private float gridCellWidth; // Store grid cell width for adaptive scaling
    
    // Events for when path is updated
    public System.Action<List<Vector3>> OnPathUpdated;
    
    void Start()
    {
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
        
        // Sync debug path start position and ensure it's initialized
        SyncDebugPath();
    }
    
    void SyncDebugPath()
    {
        if (debugPath != null)
        {
            // Sync start position
            debugPath.startPosition = startPosition;
            
            // Ensure debug path is initialized if we are
            if (dataContainer != null && dataContainer.IsLoaded && gridDimensions != Vector3Int.zero)
            {
                // Check if debug path needs initialization
                if (debugPath.gridDimensions == Vector3Int.zero)
                {
                    // Trigger debug path initialization by calling its methods
                    if (debugPath.dataContainer == null)
                    {
                        debugPath.dataContainer = dataContainer;
                    }
                    
                    // Force debug path to rebuild and regenerate
                    debugPath.RegeneratePath();
                }
            }
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
        
        // Get max magnitude for normalization from data container
        if (dataContainer != null)
        {
            maxMagnitude = dataContainer.magMinMax.y;
        }
        
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
    }
    
    public void PerformInterpolation()
    {
        interpolatedPath.Clear();
        interpolatedPathWorldCoords.Clear();
        windVectors.Clear();
        magnitudes.Clear();
        normalizedMagnitudes.Clear();
        normalizedMsl.Clear();
        
        // Reset MSL bounds
        minMsl = float.MaxValue;
        maxMsl = float.MinValue;
        
        if (gridDimensions == Vector3Int.zero) return;
        
        // Convert normalized start position to grid coordinates
        Vector3 currentPos = new Vector3(
            startPosition.x * (gridDimensions.x - 1),
            startPosition.y * (gridDimensions.y - 1),
            startPosition.z * (gridDimensions.z - 1)
        );
        
        // Try forward tracing first
        List<Vector3> forwardPath = TraceStreamlineDirection(currentPos, 1.0f, maxSteps);
        
        // If forward tracing produces a very short streamline and backward tracing is enabled, try backward
        List<Vector3> backwardPath = new List<Vector3>();
        if (enableBackwardTracing && ShouldUseBackwardTracing(forwardPath, currentPos))
        {
            backwardPath = TraceStreamlineDirection(currentPos, -1.0f, maxBackwardSteps);
        }
        
        // Combine paths: backward (reversed) + forward (excluding start point to avoid duplication)
        List<Vector3> combinedPath = new List<Vector3>();
        
        // Add backward path in reverse order (so it flows toward the start point)
        for (int i = backwardPath.Count - 1; i >= 0; i--)
        {
            combinedPath.Add(backwardPath[i]);
        }
        
        // Add forward path (skip first point if we have backward path to avoid duplication)
        int forwardStartIndex = backwardPath.Count > 0 ? 1 : 0;
        for (int i = forwardStartIndex; i < forwardPath.Count; i++)
        {
            combinedPath.Add(forwardPath[i]);
        }
        
        // Use combined path for final processing
        List<Vector3> tempPath = combinedPath;
        
        if (tempPath.Count == 0) return; // No valid path found
        
        // Calculate MSL bounds and collect wind data for the combined path
        List<Vector3> tempWindVectors = new List<Vector3>();
        List<float> tempMagnitudes = new List<float>();
        List<float> tempMagNorms = new List<float>();
        
        foreach (Vector3 pos in tempPath)
        {
            Vector3 worldPos = GridToWorldPosition(pos);
            
            // Update MSL bounds
            float msl = worldPos.y; // Y coordinate is MSL in world space
            if (msl < minMsl) minMsl = msl;
            if (msl > maxMsl) maxMsl = msl;
            
            // Get wind data at this position
            Vector3 windVector = TrilinearInterpolate(pos);
            float magnitude = windVector.magnitude;
            float magNorm = GetMagNormAtPosition(pos);
            
            tempWindVectors.Add(windVector);
            tempMagnitudes.Add(magnitude);
            tempMagNorms.Add(magNorm);
        }
        
        // Avoid division by zero for MSL normalization
        if (Mathf.Approximately(minMsl, maxMsl))
        {
            maxMsl = minMsl + 1.0f;
        }
        
        // Populate final lists with normalized values
        for (int i = 0; i < tempPath.Count; i++)
        {
            interpolatedPath.Add(tempPath[i]);
            Vector3 worldPos = GridToWorldPosition(tempPath[i]);
            interpolatedPathWorldCoords.Add(worldPos);
            windVectors.Add(tempWindVectors[i]);
            magnitudes.Add(tempMagnitudes[i]);
            
            // Calculate normalized magnitude
            float magNorm = tempMagNorms[i];
            normalizedMagnitudes.Add(magNorm);
            
            // Calculate normalized MSL
            float mslNorm = (worldPos.y - minMsl) / (maxMsl - minMsl);
            normalizedMsl.Add(mslNorm);
        }
        
        // Calculate average magnitude normalization for texture animation
        if (normalizedMagnitudes.Count > 0)
        {
            float sum = 0f;
            foreach (float magNorm in normalizedMagnitudes)
            {
                sum += magNorm;
            }
            averageMagnitudeNormalization = sum / normalizedMagnitudes.Count;
        }
        else
        {
            averageMagnitudeNormalization = 0f;
        }
        
        // Calculate streamline length and texture flow speed multiplier
        float streamlineLength = 0f;
        if (interpolatedPathWorldCoords.Count > 1)
        {
            for (int i = 0; i < interpolatedPathWorldCoords.Count - 1; i++)
            {
                streamlineLength += Vector3.Distance(interpolatedPathWorldCoords[i], interpolatedPathWorldCoords[i + 1]);
            }
        }
        
        // Calculate length multiplier: if streamline is shorter than reference, increase speed
        // If streamline is longer than reference, decrease speed
        lengthMultiplier = streamlineTexReferenceLength / Mathf.Max(streamlineLength, 1f);
        
        // Generate random texture offset for this streamline
        randomTextureOffset = useRandomTextureOffset ? Random.Range(0f, randomOffsetRange) : 0f;
        
        // Apply line simplification if enabled
        if (enableSimplification && interpolatedPathWorldCoords.Count > 2)
        {
            SimplifyStreamline();
        }
        
        // Notify listeners that path has been updated
        OnPathUpdated?.Invoke(interpolatedPathWorldCoords);
    }
    
    List<Vector3> TraceStreamlineDirection(Vector3 startPos, float direction, int maxSteps)
    {
        List<Vector3> tracePath = new List<Vector3>();
        Vector3 currentPos = startPos;
        
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
            
            // Store current position
            tracePath.Add(currentPos);
            
            // Get wind vector at current position
            Vector3 windVector = TrilinearInterpolate(currentPos);
            float magnitude = windVector.magnitude;
            
            // Use wind vector as step (multiply by direction: +1 for forward, -1 for backward)
            if (magnitude > 0.001f) // Avoid division by zero
            {
                Vector3 nextPos = currentPos + (windVector * direction);
                
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
                break; // No wind, stop tracing
            }
        }
        
        return tracePath;
    }
    
    void SimplifyStreamline()
    {
        // Apply Ramer-Douglas-Peucker algorithm to reduce points while preserving shape
        List<int> keepIndices = RamerDouglasPeucker(interpolatedPathWorldCoords, 0, interpolatedPathWorldCoords.Count - 1, simplificationTolerance);
        
        // Sort indices to maintain order
        keepIndices.Sort();
        
        // Create simplified lists
        List<Vector3> simplifiedPath = new List<Vector3>();
        List<Vector3> simplifiedWorldCoords = new List<Vector3>();
        List<Vector3> simplifiedWindVectors = new List<Vector3>();
        List<float> simplifiedMagnitudes = new List<float>();
        List<float> simplifiedNormalizedMagnitudes = new List<float>();
        List<float> simplifiedNormalizedMsl = new List<float>();
        
        foreach (int index in keepIndices)
        {
            if (index < interpolatedPath.Count)
            {
                simplifiedPath.Add(interpolatedPath[index]);
                simplifiedWorldCoords.Add(interpolatedPathWorldCoords[index]);
                simplifiedWindVectors.Add(windVectors[index]);
                simplifiedMagnitudes.Add(magnitudes[index]);
                simplifiedNormalizedMagnitudes.Add(normalizedMagnitudes[index]);
                simplifiedNormalizedMsl.Add(normalizedMsl[index]);
            }
        }
        
        // Replace original lists with simplified versions
        int originalCount = interpolatedPath.Count;
        interpolatedPath = simplifiedPath;
        interpolatedPathWorldCoords = simplifiedWorldCoords;
        windVectors = simplifiedWindVectors;
        magnitudes = simplifiedMagnitudes;
        normalizedMagnitudes = simplifiedNormalizedMagnitudes;
        normalizedMsl = simplifiedNormalizedMsl;
        
        Debug.Log($"Streamline simplified: {originalCount} -> {interpolatedPath.Count} points (tolerance: {simplificationTolerance})");
    }
    
    List<int> RamerDouglasPeucker(List<Vector3> points, int startIndex, int endIndex, float tolerance)
    {
        List<int> result = new List<int>();
        
        if (endIndex <= startIndex + 1)
        {
            // Base case: only start and end points
            result.Add(startIndex);
            if (endIndex != startIndex)
                result.Add(endIndex);
            return result;
        }
        
        // Find the point with maximum distance from the line segment
        float maxDistance = 0f;
        int maxIndex = startIndex;
        
        Vector3 lineStart = points[startIndex];
        Vector3 lineEnd = points[endIndex];
        
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float distance = PointToLineDistance(points[i], lineStart, lineEnd);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }
        
        // If the maximum distance is greater than tolerance, recursively simplify
        if (maxDistance > tolerance)
        {
            // Recursively simplify the two segments
            List<int> leftResults = RamerDouglasPeucker(points, startIndex, maxIndex, tolerance);
            List<int> rightResults = RamerDouglasPeucker(points, maxIndex, endIndex, tolerance);
            
            // Combine results (remove duplicate maxIndex)
            result.AddRange(leftResults);
            for (int i = 1; i < rightResults.Count; i++) // Skip first element to avoid duplicate
            {
                result.Add(rightResults[i]);
            }
        }
        else
        {
            // All points between start and end are within tolerance, keep only endpoints
            result.Add(startIndex);
            result.Add(endIndex);
        }
        
        return result;
    }
    
    float PointToLineDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        // Calculate the distance from a point to a line segment
        Vector3 lineVector = lineEnd - lineStart;
        Vector3 pointVector = point - lineStart;
        
        // Handle degenerate case where line has zero length
        if (lineVector.sqrMagnitude < 0.0001f)
        {
            return Vector3.Distance(point, lineStart);
        }
        
        // Project point onto line
        float t = Vector3.Dot(pointVector, lineVector) / lineVector.sqrMagnitude;
        
        // Clamp t to [0,1] to stay within line segment
        t = Mathf.Clamp01(t);
        
        // Find closest point on line segment
        Vector3 closestPoint = lineStart + t * lineVector;
        
        // Return distance from point to closest point on line
        return Vector3.Distance(point, closestPoint);
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
        
        // Get wind vectors at the 8 corners
        Vector3 c000 = GetWindAtGridPoint(x0, y0, z0);
        Vector3 c001 = GetWindAtGridPoint(x0, y0, z1);
        Vector3 c010 = GetWindAtGridPoint(x0, y1, z0);
        Vector3 c011 = GetWindAtGridPoint(x0, y1, z1);
        Vector3 c100 = GetWindAtGridPoint(x1, y0, z0);
        Vector3 c101 = GetWindAtGridPoint(x1, y0, z1);
        Vector3 c110 = GetWindAtGridPoint(x1, y1, z0);
        Vector3 c111 = GetWindAtGridPoint(x1, y1, z1);
        
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
    
    [ContextMenu("Regenerate Path")]
    public void RegeneratePath()
    {
        if (dataContainer != null && dataContainer.IsLoaded)
        {
            PerformInterpolation();
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
    
    float GetMagNormAtPosition(Vector3 position)
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
        
        // Get mag_norm values at the 8 corners
        float c000 = GetMagNormAtGridPoint(x0, y0, z0);
        float c001 = GetMagNormAtGridPoint(x0, y0, z1);
        float c010 = GetMagNormAtGridPoint(x0, y1, z0);
        float c011 = GetMagNormAtGridPoint(x0, y1, z1);
        float c100 = GetMagNormAtGridPoint(x1, y0, z0);
        float c101 = GetMagNormAtGridPoint(x1, y0, z1);
        float c110 = GetMagNormAtGridPoint(x1, y1, z0);
        float c111 = GetMagNormAtGridPoint(x1, y1, z1);
        
        // Trilinear interpolation
        float c00 = Mathf.Lerp(c000, c100, fx);
        float c01 = Mathf.Lerp(c001, c101, fx);
        float c10 = Mathf.Lerp(c010, c110, fx);
        float c11 = Mathf.Lerp(c011, c111, fx);
        
        float c0 = Mathf.Lerp(c00, c10, fy);
        float c1 = Mathf.Lerp(c01, c11, fy);
        
        return Mathf.Lerp(c0, c1, fz);
    }
    
    float GetMagNormAtGridPoint(int x, int y, int z)
    {
        Vector3Int gridPos = new Vector3Int(x, y, z);
        
        if (gridToIndex.TryGetValue(gridPos, out int dataIndex))
        {
            // Return the pre-calculated mag_norm value (same as WindFieldMeshNc)
            return dataContainer.mag_norm[dataIndex];
        }
        
        // Return zero if no data at this grid point
        return 0f;
    }
    
    bool ShouldUseBackwardTracing(List<Vector3> forwardPath, Vector3 startPos)
    {
        // If forward path is very short (≤3 points), likely hit boundary quickly
        if (forwardPath.Count <= 3)
        {
            return true;
        }
        
        // Check if the streamline exits the boundary very quickly (within first few steps)
        if (forwardPath.Count <= 5)
        {
            // Calculate total distance traveled in forward direction
            float totalDistance = 0f;
            for (int i = 0; i < forwardPath.Count - 1; i++)
            {
                totalDistance += Vector3.Distance(forwardPath[i], forwardPath[i + 1]);
            }
            
            // If the streamline traveled a very short distance, it likely hit a boundary
            // Use a threshold based on grid cell size (e.g., less than 2 grid cells)
            float shortDistanceThreshold = 2.0f;
            if (totalDistance < shortDistanceThreshold)
            {
                return true;
            }
        }
        
        // Check if start position is near a boundary and wind is pointing outward
        bool nearBoundary = IsNearBoundary(startPos);
        if (nearBoundary)
        {
            Vector3 windVector = TrilinearInterpolate(startPos);
            bool windPointsOutward = DoesWindPointOutward(startPos, windVector);
            
            if (windPointsOutward)
            {
                return true;
            }
        }
        
        return false;
    }
    
    bool IsNearBoundary(Vector3 pos)
    {
        float boundaryThreshold = 1.0f; // Within 1 grid cell of boundary
        
        return (pos.x < boundaryThreshold || pos.x > gridDimensions.x - 1 - boundaryThreshold ||
                pos.y < boundaryThreshold || pos.y > gridDimensions.y - 1 - boundaryThreshold ||
                pos.z < boundaryThreshold || pos.z > gridDimensions.z - 1 - boundaryThreshold);
    }
    
    bool DoesWindPointOutward(Vector3 pos, Vector3 windVector)
    {
        float boundaryThreshold = 1.0f;
        
        // Check each boundary and see if wind points toward it
        // Left boundary (X = 0)
        if (pos.x < boundaryThreshold && windVector.x < 0) return true;
        
        // Right boundary (X = max)
        if (pos.x > gridDimensions.x - 1 - boundaryThreshold && windVector.x > 0) return true;
        
        // Bottom boundary (Y = 0)
        if (pos.y < boundaryThreshold && windVector.y < 0) return true;
        
        // Top boundary (Y = max)
        if (pos.y > gridDimensions.y - 1 - boundaryThreshold && windVector.y > 0) return true;
        
        // Front boundary (Z = 0)
        if (pos.z < boundaryThreshold && windVector.z < 0) return true;
        
        // Back boundary (Z = max)
        if (pos.z > gridDimensions.z - 1 - boundaryThreshold && windVector.z > 0) return true;
        
        return false;
    }
} 