using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class WindFieldStreamlinesCalculator : MonoBehaviour
{
    [Header("Data Source")]
    public NcDataContainerImgs dataContainer;
    
    [Header("UI Status")]
    public TextMeshProUGUI statusText;
    
    [Header("Streamline Generation Settings")]
    public bool generateOnStart = true;
    [Tooltip("Use WindStartStreamlinePoints component if available (recommended)")]
    public bool useStartPointGenerator = true; // Use WindStartStreamlinePoints component if available
    [Tooltip("Fallback method when WindStartStreamlinePoints is not available")]
    public bool fallbackUseWallPointsOnly = true; // Fallback for when start point generator is not available
    [Range(1, 100000)]
    public int maxStreamlines = 1000;
    
    [Header("Backward Tracing Settings")]
    [Tooltip("Enable backward tracing for streamlines that immediately exit boundaries")]
    public bool enableBackwardTracing = true;
    [Tooltip("Maximum steps to trace backward from start point")]
    public int maxBackwardSteps = 50;
    
    [Header("Interpolation Settings")]
    public int maxStepsPerStreamline = 100;
    public float stepScale = 0.1f; // Multiplier for wind vector steps
    
    [Header("Texture Flow Settings")]
    public float streamlineTexReferenceLength = 100f; // Reference length for texture flow speed normalization
    public bool useRandomTextureOffsets = true; // Enable random UV offsets per streamline
    [Range(0f, 10f)]
    public float randomOffsetRange = 2f; // Range for random UV offsets (0-10)
    

    
    [Header("Grid Info")]
    public Vector3Int gridDimensions;
    public Vector3 gridMin;
    public Vector3 gridMax;
    
    [Header("Results")]
    public List<List<Vector3>> allStreamlinePaths = new List<List<Vector3>>();
    public List<List<Vector3>> allStreamlineWorldCoords = new List<List<Vector3>>();
    public List<List<Vector3>> allWindVectors = new List<List<Vector3>>();
    public List<List<float>> allMagnitudes = new List<List<float>>();
    public List<List<float>> allNormalizedMagnitudes = new List<List<float>>();
    public List<List<float>> allNormalizedMsl = new List<List<float>>();
    public List<List<float>> allDirectionChanges = new List<List<float>>(); // Angle changes between sequential segments (in radians)
    public List<float> allAverageMagnitudeNormalizations = new List<float>(); // For texture animation per streamline
    public List<float> allStreamlineLengthMultipliers = new List<float>(); // For texture flow speed normalization
    public List<float> allRandomTextureOffsets = new List<float>(); // Random UV offsets per streamline
    public List<float> allAverageCurvatures = new List<float>(); // Average curvature per streamline for threshold filtering
    public List<float> allLowestAltitudes = new List<float>(); // Lowest altitude (Y coordinate) per streamline for trimming
    
    [Header("Normalization Data")]
    private float globalMinMsl = float.MaxValue;
    private float globalMaxMsl = float.MinValue;
    public float maxMagnitude = 1.0f;
    
    [Header("Streamline Length Info")]
    [Tooltip("Minimum streamline length in the current dataset (read-only)")]
    public float minStreamlineLength = 0f;
    [Tooltip("Maximum streamline length in the current dataset (read-only)")]
    public float maxStreamlineLength = 0f;
    
    private Dictionary<Vector3Int, int> gridToIndex = new Dictionary<Vector3Int, int>();
    private Vector3Int[] uniqueGridPositions;
    private float gridCellWidth;
    
    [Header("Simplification Settings")]
    [Tooltip("Enable streamline simplification to reduce number of points")]
    public bool enableSimplification = true;
    [Tooltip("Tolerance in world units (higher values = fewer points, more deviation)")]
    [Range(0.1f, 100f)]
    public float simplificationTolerance = 5f;
    
    [Header("Curvature Threshold Filtering")]
    [Tooltip("Enable threshold-based filtering to remove straight streamlines")]
    public bool enableCurvatureThresholdFiltering = false;
    [Tooltip("Curvature threshold - streamlines below this get filtered")]
    [Range(0f, 0.5f)]
    public float curvatureThreshold = 0.1f;
    [Tooltip("Probability to drop streamlines below threshold (0=never drop, 1=always drop)")]
    [Range(0f, 1f)]
    public float dropProbability = 0.7f;
    
    // Events for when streamlines are updated
    public System.Action<List<List<Vector3>>> OnStreamlinesUpdated;
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
    
    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        UpdateStatus("");
        statusText.gameObject.SetActive(false);
    }
    
    void Start()
    {
        UpdateStatus("Initializing...");
        // Simple check - if data is ready, proceed immediately
        CheckAndGenerate();
    }
    
    void Update()
    {
        // Simple check every frame until data is ready
        CheckAndGenerate();
    }
    
    void CheckAndGenerate()
    {
        if (generateOnStart && dataContainer != null && dataContainer.IsLoaded && gridDimensions == Vector3Int.zero)
        {
            UpdateStatus("Building grid structure...");
            BuildGridStructure();
            GenerateStreamlines();
        }
        else if (generateOnStart && (dataContainer == null || !dataContainer.IsLoaded))
        {
            UpdateStatus("Waiting for data container to load...");
        }
    }
    
    void BuildGridStructure()
    {
        if (dataContainer.x_from_origin.Count == 0) 
        {
            Debug.LogWarning("No x_from_origin data available for grid structure");
            return;
        }
        
        Debug.Log($"Building grid structure with {dataContainer.x_from_origin.Count} data points");
        
        // Find unique positions for each dimension (same as WindStreamlineCalculator)
        var uniqueX = dataContainer.x_from_origin.Distinct().OrderBy(x => x).ToList();
        var uniqueY = dataContainer.y_from_origin.Distinct().OrderBy(y => y).ToList();
        var uniqueZ = dataContainer.msl.Distinct().OrderBy(z => z).ToList();
        
        Debug.Log($"Unique values - X: {uniqueX.Count}, Y: {uniqueY.Count}, Z: {uniqueZ.Count}");
        Debug.Log($"X range: {uniqueX.Min():F2} to {uniqueX.Max():F2}");
        Debug.Log($"Y range: {uniqueY.Min():F2} to {uniqueY.Max():F2}");
        Debug.Log($"Z range: {uniqueZ.Min():F2} to {uniqueZ.Max():F2}");
        
        gridDimensions = new Vector3Int(uniqueX.Count, uniqueY.Count, uniqueZ.Count);
        // Use same coordinate system as DataBoundaryUtility: (X, MSL, Y)
        gridMin = new Vector3(uniqueX.Min(), uniqueZ.Min(), uniqueY.Min());
        gridMax = new Vector3(uniqueX.Max(), uniqueZ.Max(), uniqueY.Max());
        
        Debug.Log($"Grid dimensions: {gridDimensions}");
        Debug.Log($"Grid bounds: {gridMin} to {gridMax}");
        
        // Calculate grid cell width (same as WindStreamlineCalculator)
        gridCellWidth = uniqueX.Count > 1 ? Mathf.Abs(uniqueX[1] - uniqueX[0]) : 1f;
        
        Debug.Log($"Grid cell width: {gridCellWidth}");
        
        // Get max magnitude for normalization from data container
        if (dataContainer != null)
        {
            maxMagnitude = dataContainer.magMinMax.y;
            Debug.Log($"Max magnitude: {maxMagnitude}");
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
        
        Debug.Log($"Built grid structure with {gridToIndex.Count} unique grid positions from {dataContainer.x_from_origin.Count} data points");
    }
    
    [ContextMenu("Generate Streamlines")]
    public void GenerateStreamlines()
    {
        Debug.Log("=== GenerateStreamlines called ===");
        UpdateStatus("Starting streamline generation...");
        
        if (dataContainer == null || !dataContainer.IsLoaded)
        {
            Debug.LogError("Data container is not loaded!");
            UpdateStatus("Error: Data container not loaded!");
            return;
        }
        
        Debug.Log($"Data container loaded with {dataContainer.x_from_origin.Count} data points");
        
        if (gridDimensions == Vector3Int.zero)
        {
            Debug.Log("Grid dimensions zero, building grid structure...");
            BuildGridStructure();
        }
        
        Debug.Log($"Grid dimensions: {gridDimensions}");
        
        // Debug magnitude range info
        if (dataContainer != null)
        {
            Vector2 dataMinMax = dataContainer.magMinMax;
            Debug.Log($"Magnitude range - Data: {dataMinMax.x:F2} to {dataMinMax.y:F2} m/s");
            // Note: Global magnitude range for color mapping is now handled by WindFieldStreamlinesRenderer
        }
        
        // Clear existing data
        ClearAllStreamlines();
        UpdateStatus("Clearing existing streamlines...");
        
        // Generate starting positions
        UpdateStatus("Generating start positions...");
        List<Vector3> startPositions = GenerateStartingPositions();
        
        Debug.Log($"Generated {startPositions.Count} start positions");
        
        // Limit number of streamlines
        if (startPositions.Count > maxStreamlines)
        {
            startPositions = startPositions.Take(maxStreamlines).ToList();
            Debug.Log($"Limited to {startPositions.Count} streamlines");
        }
        
        Debug.Log($"Generating {startPositions.Count} streamlines...");
        UpdateStatus($"Generating {startPositions.Count} streamlines...");
        
        // Generate streamlines for each start position (same as multiple WindStreamlineCalculator instances)
        for (int i = 0; i < startPositions.Count; i++)
        {
            GenerateSingleStreamline(startPositions[i]);
            
            // Progress update for large numbers
            if (i % 100 == 0 && i > 0)
            {
                UpdateStatus($"Generated {i}/{startPositions.Count} streamlines...");
            }
        }
        
        Debug.Log($"=== Generated {allStreamlinePaths.Count} streamlines with total {GetTotalPoints()} points ===");
        
        // Calculate min/max streamline lengths
        CalculateStreamlineLengthRange();
        
        // Apply curvature threshold filtering if enabled
        if (enableCurvatureThresholdFiltering)
        {
            ApplyCurvatureThresholdFiltering();
            Debug.Log($"After curvature threshold filtering: {allStreamlinePaths.Count} streamlines remaining");
            
            // Recalculate min/max after filtering
            CalculateStreamlineLengthRange();
        }
        
        UpdateStatus($"Complete! Generated {allStreamlinePaths.Count} streamlines with {GetTotalPoints()} points");
        
        // Clear status message after a brief delay
        StartCoroutine(ClearStatusAfterDelay());
        
        // Notify listeners
        OnStreamlinesUpdated?.Invoke(allStreamlineWorldCoords);
        
        Debug.Log($"OnStreamlinesUpdated event fired with {allStreamlineWorldCoords.Count} streamlines");
    }
    
    void GenerateSingleStreamline(Vector3 startPosition)
    {
        // This method works exactly like WindStreamlineCalculator.PerformInterpolation()
        List<Vector3> path = new List<Vector3>();
        List<Vector3> worldCoords = new List<Vector3>();
        List<Vector3> windVectors = new List<Vector3>();
        List<float> magnitudes = new List<float>();
        List<float> normalizedMagnitudes = new List<float>();
        List<float> normalizedMsl = new List<float>();
        List<float> directionChanges = new List<float>();
        
        // Get global MSL bounds from data container
        float globalMinMsl = dataContainer.gridMin.y;
        float globalMaxMsl = dataContainer.gridMax.y;
        
        // Convert normalized start position to grid coordinates
        Vector3 currentPos = new Vector3(
            startPosition.x * (gridDimensions.x - 1),
            startPosition.y * (gridDimensions.y - 1),
            startPosition.z * (gridDimensions.z - 1)
        );
        
        // Try forward tracing first
        List<Vector3> forwardPath = TraceStreamlineDirection(currentPos, 1.0f, maxStepsPerStreamline);
        
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
        
        // Convert to world coordinates first
        List<Vector3> worldPath = new List<Vector3>();
        foreach (Vector3 gridPos in combinedPath)
        {
            worldPath.Add(GridToWorldPosition(gridPos));
        }
        
        // Apply simplification in world space if enabled
        if (enableSimplification && worldPath.Count > 2)
        {
            worldPath = SimplifyLine(worldPath, simplificationTolerance);
        }
        
        // Now process the simplified world path
        foreach (Vector3 worldPos in worldPath)
        {
            // Convert back to grid space for wind calculations
            Vector3 gridPos = WorldToGridPosition(worldPos);
            
            // Calculate normalized MSL using global bounds
            float mslNorm = (worldPos.y - globalMinMsl) / (globalMaxMsl - globalMinMsl);
            mslNorm = Mathf.Clamp01(mslNorm); // Ensure it's in 0-1 range
            
            // Get wind data at this position
            Vector3 windVector = TrilinearInterpolate(gridPos);
            float magnitude = windVector.magnitude;
            float magNorm = GetMagNormAtPosition(gridPos);
            
            path.Add(gridPos);
            worldCoords.Add(worldPos);
            windVectors.Add(windVector);
            magnitudes.Add(magnitude);
            normalizedMagnitudes.Add(magNorm);
            normalizedMsl.Add(mslNorm);
        }
        
        // Calculate direction changes between sequential line segments
        CalculateDirectionChanges(worldCoords, directionChanges);
        
        // Calculate average curvature for this streamline
        float averageCurvature = CalculateAverageCurvature(directionChanges);
        
        // Calculate average magnitude normalization for texture animation
        float averageMagnitudeNormalization = 0f;
        if (normalizedMagnitudes.Count > 0)
        {
            float sum = 0f;
            foreach (float magNorm in normalizedMagnitudes)
            {
                sum += magNorm;
            }
            averageMagnitudeNormalization = sum / normalizedMagnitudes.Count;
        }
        
        // Calculate streamline length and texture flow speed multiplier
        float streamlineLength = 0f;
        if (worldCoords.Count > 1)
        {
            for (int i = 0; i < worldCoords.Count - 1; i++)
            {
                streamlineLength += Vector3.Distance(worldCoords[i], worldCoords[i + 1]);
            }
        }
        
        // Calculate length multiplier: if streamline is shorter than reference, increase speed
        // If streamline is longer than reference, decrease speed
        float lengthMultiplier = streamlineTexReferenceLength / Mathf.Max(streamlineLength, 1f);
        
        // Generate random texture offset for this streamline
        float randomTextureOffset = useRandomTextureOffsets ? Random.Range(0f, randomOffsetRange) : 0f;
        
        // Calculate lowest altitude for this streamline
        float lowestAltitude = float.MaxValue;
        foreach (Vector3 worldPos in worldCoords)
        {
            if (worldPos.y < lowestAltitude)
            {
                lowestAltitude = worldPos.y;
            }
        }
        
        // Only add streamlines with at least 2 points
        if (path.Count >= 2)
        {
            allStreamlinePaths.Add(path);
            allStreamlineWorldCoords.Add(worldCoords);
            allWindVectors.Add(windVectors);
            allMagnitudes.Add(magnitudes);
            allNormalizedMagnitudes.Add(normalizedMagnitudes);
            allNormalizedMsl.Add(normalizedMsl);
            allDirectionChanges.Add(directionChanges);
            allAverageCurvatures.Add(averageCurvature);
            allAverageMagnitudeNormalizations.Add(averageMagnitudeNormalization);
            allStreamlineLengthMultipliers.Add(lengthMultiplier);
            allRandomTextureOffsets.Add(randomTextureOffset);
            allLowestAltitudes.Add(lowestAltitude);
        }
    }
    
    List<Vector3> TraceStreamlineDirection(Vector3 startPos, float direction, int maxSteps)
    {
        List<Vector3> tracePath = new List<Vector3>();
        Vector3 currentPos = startPos;
        
        for (int step = 0; step < maxSteps; step++)
        {
            // Check bounds strictly - no extension beyond data boundaries
            if (currentPos.x < 0 || currentPos.x > gridDimensions.x - 1 ||
                currentPos.y < 0 || currentPos.y > gridDimensions.y - 1 ||
                currentPos.z < 0 || currentPos.z > gridDimensions.z - 1)
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
                
                // Check if next step would go outside strict boundaries
                if (nextPos.x < 0 || nextPos.x > gridDimensions.x - 1 ||
                    nextPos.y < 0 || nextPos.y > gridDimensions.y - 1 ||
                    nextPos.z < 0 || nextPos.z > gridDimensions.z - 1)
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
    
    List<Vector3> GenerateStartingPositions()
    {
        // Check if we should use the WindStartStreamlinePoints component
        if (useStartPointGenerator)
        {
            WindStartStreamlinePoints startPointGenerator = GetComponent<WindStartStreamlinePoints>();
            if (startPointGenerator != null)
            {
                List<Vector3> generatedPoints = startPointGenerator.GetStartPoints();
                if (generatedPoints.Count > 0)
                {
                    Debug.Log($"✓ Using WindStartStreamlinePoints: {generatedPoints.Count} points generated with Grid Sampled Wall Points");
                    return generatedPoints;
                }
                else
                {
                    Debug.LogWarning("⚠ WindStartStreamlinePoints generated 0 points, falling back to default method");
                }
            }
            else
            {
                Debug.LogWarning("⚠ useStartPointGenerator is enabled but no WindStartStreamlinePoints component found on this GameObject. Add the component or disable useStartPointGenerator.");
            }
        }
        else
        {
            Debug.Log("ℹ useStartPointGenerator is disabled, using fallback method");
        }
        
        // Fall back to original methods
        string fallbackMethod = fallbackUseWallPointsOnly ? "Wall Points" : "Grid Points";
        Debug.Log($"ℹ Using fallback method: {fallbackMethod}");
        return fallbackUseWallPointsOnly ? GenerateWallStartPositions() : GenerateGridStartPositions();
    }
    
    List<Vector3> GenerateWallStartPositions()
    {
        List<Vector3> wallPositions = new List<Vector3>();
        
        // Find the bounds of the data
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        
        for (int i = 0; i < dataContainer.x_from_origin.Count; i++)
        {
            float x = dataContainer.x_from_origin[i];
            float y = dataContainer.y_from_origin[i];
            float z = dataContainer.msl[i];
            
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }
        
        // Convert data points to normalized positions and check if they're on walls
        for (int i = 0; i < dataContainer.x_from_origin.Count; i++)
        {
            float x = dataContainer.x_from_origin[i];
            float y = dataContainer.y_from_origin[i];
            float z = dataContainer.msl[i];
            
            // Normalize to 0-1 range
            float normX = (x - minX) / (maxX - minX);
            float normY = (y - minY) / (maxY - minY);
            float normZ = (z - minZ) / (maxZ - minZ);
            
            // Check if point is on any of the 6 walls (with small tolerance)
            float tolerance = 0.001f;
            bool isOnWall = false;
            Vector3 wallPosition = Vector3.zero;
            
            // Wall 1: X = 0 (left wall)
            if (Mathf.Abs(normX - 0f) < tolerance)
            {
                wallPosition = new Vector3(0f, normY, normZ);
                isOnWall = true;
            }
            // Wall 2: X = 1 (right wall)
            else if (Mathf.Abs(normX - 1f) < tolerance)
            {
                wallPosition = new Vector3(1f, normY, normZ);
                isOnWall = true;
            }
            // Wall 3: Y = 0 (bottom wall)
            else if (Mathf.Abs(normY - 0f) < tolerance)
            {
                wallPosition = new Vector3(normX, 0f, normZ);
                isOnWall = true;
            }
            // Wall 4: Y = 1 (top wall)
            else if (Mathf.Abs(normY - 1f) < tolerance)
            {
                wallPosition = new Vector3(normX, 1f, normZ);
                isOnWall = true;
            }
            // Wall 5: Z = 0 (front wall)
            else if (Mathf.Abs(normZ - 0f) < tolerance)
            {
                wallPosition = new Vector3(normX, normY, 0f);
                isOnWall = true;
            }
            // Wall 6: Z = 1 (back wall)
            else if (Mathf.Abs(normZ - 1f) < tolerance)
            {
                wallPosition = new Vector3(normX, normY, 1f);
                isOnWall = true;
            }
            
            if (isOnWall)
            {
                wallPositions.Add(wallPosition);
            }
        }
        
        Debug.Log($"Found {wallPositions.Count} wall start positions");
        return wallPositions;
    }
    
    List<Vector3> GenerateGridStartPositions()
    {
        List<Vector3> gridPositions = new List<Vector3>();
        
        // Generate regular grid of start positions
        int gridRes = Mathf.CeilToInt(Mathf.Pow(maxStreamlines, 1f/3f)); // Cube root for 3D grid
        
        for (int x = 0; x < gridRes; x++)
        {
            for (int y = 0; y < gridRes; y++)
            {
                for (int z = 0; z < gridRes; z++)
                {
                    Vector3 pos = new Vector3(
                        (float)x / (gridRes - 1),
                        (float)y / (gridRes - 1),
                        (float)z / (gridRes - 1)
                    );
                    gridPositions.Add(pos);
                }
            }
        }
        
        Debug.Log($"Generated {gridPositions.Count} grid start positions");
        return gridPositions;
    }
    
    Vector3 TrilinearInterpolate(Vector3 position)
    {
        // Same implementation as WindStreamlineCalculator
        Vector3 clampedPos = new Vector3(
            Mathf.Clamp(position.x, 0f, gridDimensions.x - 1f),
            Mathf.Clamp(position.y, 0f, gridDimensions.y - 1f),
            Mathf.Clamp(position.z, 0f, gridDimensions.z - 1f)
        );
        
        int x0 = Mathf.FloorToInt(clampedPos.x);
        int y0 = Mathf.FloorToInt(clampedPos.y);
        int z0 = Mathf.FloorToInt(clampedPos.z);
        int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
        int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
        int z1 = Mathf.Min(z0 + 1, gridDimensions.z - 1);
        
        float fx = clampedPos.x - x0;
        float fy = clampedPos.y - y0;
        float fz = clampedPos.z - z0;
        
        Vector3 c000 = GetWindAtGridPoint(x0, y0, z0);
        Vector3 c001 = GetWindAtGridPoint(x0, y0, z1);
        Vector3 c010 = GetWindAtGridPoint(x0, y1, z0);
        Vector3 c011 = GetWindAtGridPoint(x0, y1, z1);
        Vector3 c100 = GetWindAtGridPoint(x1, y0, z0);
        Vector3 c101 = GetWindAtGridPoint(x1, y0, z1);
        Vector3 c110 = GetWindAtGridPoint(x1, y1, z0);
        Vector3 c111 = GetWindAtGridPoint(x1, y1, z1);
        
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
        // Same implementation as WindStreamlineCalculator
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
        
        return Vector3.zero;
    }
    
    float GetMagNormAtPosition(Vector3 position)
    {
        // Same implementation as WindStreamlineCalculator
        Vector3 clampedPos = new Vector3(
            Mathf.Clamp(position.x, 0f, gridDimensions.x - 1f),
            Mathf.Clamp(position.y, 0f, gridDimensions.y - 1f),
            Mathf.Clamp(position.z, 0f, gridDimensions.z - 1f)
        );
        
        int x0 = Mathf.FloorToInt(clampedPos.x);
        int y0 = Mathf.FloorToInt(clampedPos.y);
        int z0 = Mathf.FloorToInt(clampedPos.z);
        int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
        int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
        int z1 = Mathf.Min(z0 + 1, gridDimensions.z - 1);
        
        float fx = clampedPos.x - x0;
        float fy = clampedPos.y - y0;
        float fz = clampedPos.z - z0;
        
        float c000 = GetMagNormAtGridPoint(x0, y0, z0);
        float c001 = GetMagNormAtGridPoint(x0, y0, z1);
        float c010 = GetMagNormAtGridPoint(x0, y1, z0);
        float c011 = GetMagNormAtGridPoint(x0, y1, z1);
        float c100 = GetMagNormAtGridPoint(x1, y0, z0);
        float c101 = GetMagNormAtGridPoint(x1, y0, z1);
        float c110 = GetMagNormAtGridPoint(x1, y1, z0);
        float c111 = GetMagNormAtGridPoint(x1, y1, z1);
        
        float c00 = Mathf.Lerp(c000, c100, fx);
        float c01 = Mathf.Lerp(c001, c101, fx);
        float c10 = Mathf.Lerp(c010, c110, fx);
        float c11 = Mathf.Lerp(c011, c111, fx);
        
        float c0 = Mathf.Lerp(c00, c10, fy);
        float c1 = Mathf.Lerp(c01, c11, fy);
        
        float interpolatedMagNorm = Mathf.Lerp(c0, c1, fz);
        
        // Note: Global magnitude range remapping is now handled by WindFieldStreamlinesRenderer
        return interpolatedMagNorm;
    }
    
    float GetMagNormAtGridPoint(int x, int y, int z)
    {
        // Same implementation as WindStreamlineCalculator
        Vector3Int gridPos = new Vector3Int(x, y, z);
        
        if (gridToIndex.TryGetValue(gridPos, out int dataIndex))
        {
            return dataContainer.mag_norm[dataIndex];
        }
        
        return 0f;
    }
    
    public Vector3 GridToWorldPosition(Vector3 gridPos)
    {
        // Same implementation as WindStreamlineCalculator
        if (gridDimensions == Vector3Int.zero) return gridPos;
        
        Vector3 normalizedPos = new Vector3(
            gridPos.x / (gridDimensions.x - 1),
            gridPos.y / (gridDimensions.y - 1),
            gridPos.z / (gridDimensions.z - 1)
        );
        
        return new Vector3(
            Mathf.LerpUnclamped(gridMin.x, gridMax.x, normalizedPos.x),
            Mathf.LerpUnclamped(gridMin.y, gridMax.y, normalizedPos.z),
            Mathf.LerpUnclamped(gridMin.z, gridMax.z, normalizedPos.y)
        );
    }
    
    void CalculateStreamlineLengthRange()
    {
        if (allStreamlineWorldCoords.Count == 0)
        {
            minStreamlineLength = 0f;
            maxStreamlineLength = 0f;
            return;
        }
        
        List<float> streamlineLengths = new List<float>();
        
        // Calculate length for each streamline
        foreach (var streamline in allStreamlineWorldCoords)
        {
            float length = 0f;
            for (int i = 0; i < streamline.Count - 1; i++)
            {
                length += Vector3.Distance(streamline[i], streamline[i + 1]);
            }
            streamlineLengths.Add(length);
        }
        
        // Find min/max
        if (streamlineLengths.Count > 0)
        {
            minStreamlineLength = Mathf.Min(streamlineLengths.ToArray());
            maxStreamlineLength = Mathf.Max(streamlineLengths.ToArray());
        }
        else
        {
            minStreamlineLength = 0f;
            maxStreamlineLength = 1f; // Avoid division by zero
        }
        
        Debug.Log($"Streamline lengths - Min: {minStreamlineLength:F2}, Max: {maxStreamlineLength:F2}");
    }
    
    public void ClearAllStreamlines()
    {
        allStreamlinePaths.Clear();
        allStreamlineWorldCoords.Clear();
        allWindVectors.Clear();
        allMagnitudes.Clear();
        allNormalizedMagnitudes.Clear();
        allNormalizedMsl.Clear();
        allDirectionChanges.Clear();
        allAverageCurvatures.Clear();
        allAverageMagnitudeNormalizations.Clear();
        allStreamlineLengthMultipliers.Clear();
        allRandomTextureOffsets.Clear();
        allLowestAltitudes.Clear();
        
        globalMinMsl = float.MaxValue;
        globalMaxMsl = float.MinValue;
        minStreamlineLength = 0f;
        maxStreamlineLength = 0f;
    }
    
    public int GetTotalPoints()
    {
        int total = 0;
        foreach (var path in allStreamlinePaths)
        {
            total += path.Count;
        }
        return total;
    }
    
    public int GetStreamlineCount()
    {
        return allStreamlinePaths.Count;
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
    
    void CalculateDirectionChanges(List<Vector3> worldCoords, List<float> directionChanges)
    {
        directionChanges.Clear();
        
        if (worldCoords.Count < 3)
        {
            // Not enough points to calculate direction changes
            for (int i = 0; i < worldCoords.Count; i++)
            {
                directionChanges.Add(0f);
            }
            return;
        }
        
        // First point has no previous segment, so direction change is 0
        directionChanges.Add(0f);
        
        // Calculate direction change for each point (except first and last)
        for (int i = 1; i < worldCoords.Count - 1; i++)
        {
            Vector3 prevSegment = (worldCoords[i] - worldCoords[i - 1]).normalized;
            Vector3 nextSegment = (worldCoords[i + 1] - worldCoords[i]).normalized;
            
            // Calculate angle between the two segments
            float dotProduct = Vector3.Dot(prevSegment, nextSegment);
            // Clamp to avoid floating point errors
            dotProduct = Mathf.Clamp(dotProduct, -1f, 1f);
            
            // Get the angle in radians and take absolute value
            float angleRadians = Mathf.Abs(Mathf.Acos(dotProduct));
            
            // Normalize to 0-1 range (π radians = 180° = maximum change = 1.0)
            float normalizedAngle = angleRadians / Mathf.PI;
            
            directionChanges.Add(normalizedAngle);
        }
        
        // Last point has no next segment, so direction change is 0
        directionChanges.Add(0f);
    }
    
    float CalculateAverageCurvature(List<float> directionChanges)
    {
        if (directionChanges.Count == 0) return 0f;
        
        float totalCurvature = 0f;
        int validAngles = 0;
        
        // Sum all direction changes (excluding first and last points which are always 0)
        for (int i = 1; i < directionChanges.Count - 1; i++)
        {
            totalCurvature += directionChanges[i];
            validAngles++;
        }
        
        // Return average curvature (0 if no valid angles)
        return validAngles > 0 ? totalCurvature / validAngles : 0f;
    }
    
    void ApplyCurvatureThresholdFiltering()
    {
        if (allAverageCurvatures.Count == 0) return;
        
        // Create lists to store filtered streamlines
        List<List<Vector3>> filteredPaths = new List<List<Vector3>>();
        List<List<Vector3>> filteredWorldCoords = new List<List<Vector3>>();
        List<List<Vector3>> filteredWindVectors = new List<List<Vector3>>();
        List<List<float>> filteredMagnitudes = new List<List<float>>();
        List<List<float>> filteredNormalizedMagnitudes = new List<List<float>>();
        List<List<float>> filteredNormalizedMsl = new List<List<float>>();
        List<List<float>> filteredDirectionChanges = new List<List<float>>();
        List<float> filteredAverageCurvatures = new List<float>();
        List<float> filteredAverageMagnitudeNormalizations = new List<float>();
        List<float> filteredStreamlineLengthMultipliers = new List<float>();
        List<float> filteredRandomTextureOffsets = new List<float>();
        List<float> filteredLowestAltitudes = new List<float>();
        
        int originalCount = allStreamlinePaths.Count;
        int keptCount = 0;
        int droppedBelowThreshold = 0;
        
        // Filter each streamline based on its average curvature
        for (int i = 0; i < allStreamlinePaths.Count; i++)
        {
            float avgCurvature = allAverageCurvatures[i];
            bool keepStreamline = true;
            
            // Check if average curvature is below threshold
            if (avgCurvature < curvatureThreshold)
            {
                // Roll for drop probability
                if (Random.Range(0f, 1f) < dropProbability)
                {
                    keepStreamline = false;
                    droppedBelowThreshold++;
                }
            }
            
            if (keepStreamline)
            {
                // Keep this streamline
                filteredPaths.Add(allStreamlinePaths[i]);
                filteredWorldCoords.Add(allStreamlineWorldCoords[i]);
                filteredWindVectors.Add(allWindVectors[i]);
                filteredMagnitudes.Add(allMagnitudes[i]);
                filteredNormalizedMagnitudes.Add(allNormalizedMagnitudes[i]);
                filteredNormalizedMsl.Add(allNormalizedMsl[i]);
                filteredDirectionChanges.Add(allDirectionChanges[i]);
                filteredAverageCurvatures.Add(allAverageCurvatures[i]);
                filteredAverageMagnitudeNormalizations.Add(allAverageMagnitudeNormalizations[i]);
                filteredStreamlineLengthMultipliers.Add(allStreamlineLengthMultipliers[i]);
                filteredRandomTextureOffsets.Add(allRandomTextureOffsets[i]);
                filteredLowestAltitudes.Add(allLowestAltitudes[i]);
                keptCount++;
            }
        }
        
        // Replace original lists with filtered ones
        allStreamlinePaths = filteredPaths;
        allStreamlineWorldCoords = filteredWorldCoords;
        allWindVectors = filteredWindVectors;
        allMagnitudes = filteredMagnitudes;
        allNormalizedMagnitudes = filteredNormalizedMagnitudes;
        allNormalizedMsl = filteredNormalizedMsl;
        allDirectionChanges = filteredDirectionChanges;
        allAverageCurvatures = filteredAverageCurvatures;
        allAverageMagnitudeNormalizations = filteredAverageMagnitudeNormalizations;
        allStreamlineLengthMultipliers = filteredStreamlineLengthMultipliers;
        allRandomTextureOffsets = filteredRandomTextureOffsets;
        allLowestAltitudes = filteredLowestAltitudes;
        
        float keepPercentage = originalCount > 0 ? (float)keptCount / originalCount * 100f : 0f;
        Debug.Log($"Curvature threshold filtering: kept {keptCount}/{originalCount} streamlines ({keepPercentage:F1}%)");
        Debug.Log($"Dropped {droppedBelowThreshold} streamlines below threshold {curvatureThreshold:F3} with {dropProbability:F1} probability");
    }
    
    // Note: Global magnitude range functions moved to WindFieldStreamlinesRenderer
    
    List<Vector3> SimplifyLine(List<Vector3> points, float tolerance)
    {
        if (points.Count <= 2) return points;

        List<bool> keepPoint = new List<bool>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            keepPoint.Add(false);
        }
        
        // Always keep first and last points
        keepPoint[0] = true;
        keepPoint[points.Count - 1] = true;

        // Start the recursive simplification
        SimplifySection(points, 0, points.Count - 1, tolerance, keepPoint);

        // Build the simplified line
        List<Vector3> simplified = new List<Vector3>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keepPoint[i])
            {
                simplified.Add(points[i]);
            }
        }

        return simplified;
    }

    void SimplifySection(List<Vector3> points, int start, int end, float tolerance, List<bool> keepPoint)
    {
        if (end <= start + 1) return;

        float maxDistSq = 0;
        int furthestIndex = start;

        Vector3 lineStart = points[start];
        Vector3 lineEnd = points[end];

        // Find the point furthest from the line segment
        for (int i = start + 1; i < end; i++)
        {
            float distSq = PointLineDistanceSq(points[i], lineStart, lineEnd);
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
                furthestIndex = i;
            }
        }

        // If the furthest point is beyond our tolerance, keep it and recursively simplify the subsections
        if (maxDistSq > tolerance * tolerance)
        {
            keepPoint[furthestIndex] = true;
            SimplifySection(points, start, furthestIndex, tolerance, keepPoint);
            SimplifySection(points, furthestIndex, end, tolerance, keepPoint);
        }
    }

    float PointLineDistanceSq(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        float lineLengthSq = (lineEnd - lineStart).sqrMagnitude;
        if (lineLengthSq == 0) return (point - lineStart).sqrMagnitude;

        // Calculate the projection of point onto the line
        float t = Vector3.Dot(point - lineStart, lineEnd - lineStart) / lineLengthSq;
        t = Mathf.Clamp01(t);

        Vector3 projection = lineStart + t * (lineEnd - lineStart);
        return (point - projection).sqrMagnitude;
    }

    [ContextMenu("Debug Simplification Stats")]
    public void DebugSimplificationStats()
    {
        if (allStreamlinePaths.Count == 0)
        {
            Debug.Log("No streamlines to analyze");
            return;
        }

        int totalOriginalPoints = 0;
        List<List<Vector3>> simplifiedPaths = new List<List<Vector3>>();
        int totalSimplifiedPoints = 0;
        float totalOriginalLength = 0f;
        float totalSimplifiedLength = 0f;

        foreach (var path in allStreamlineWorldCoords)
        {
            totalOriginalPoints += path.Count;
            
            // Calculate original length
            float originalLength = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                originalLength += Vector3.Distance(path[i], path[i + 1]);
            }
            totalOriginalLength += originalLength;
            
            // Simplify and calculate new length
            var simplified = SimplifyLine(path, simplificationTolerance);
            simplifiedPaths.Add(simplified);
            totalSimplifiedPoints += simplified.Count;
            
            float simplifiedLength = 0f;
            for (int i = 0; i < simplified.Count - 1; i++)
            {
                simplifiedLength += Vector3.Distance(simplified[i], simplified[i + 1]);
            }
            totalSimplifiedLength += simplifiedLength;
        }

        float reductionPercent = 100f * (1f - (float)totalSimplifiedPoints / totalOriginalPoints);
        float lengthDifferencePercent = 100f * (1f - totalSimplifiedLength / totalOriginalLength);
        
        Debug.Log($"=== Streamline Simplification Stats ===");
        Debug.Log($"Original points: {totalOriginalPoints}");
        Debug.Log($"Simplified points: {totalSimplifiedPoints}");
        Debug.Log($"Point reduction: {reductionPercent:F1}%");
        Debug.Log($"Original total length: {totalOriginalLength:F1} world units");
        Debug.Log($"Simplified total length: {totalSimplifiedLength:F1} world units");
        Debug.Log($"Length difference: {lengthDifferencePercent:F1}%");
        Debug.Log($"Average points per streamline: {totalOriginalPoints / allStreamlinePaths.Count:F1} → {totalSimplifiedPoints / allStreamlinePaths.Count:F1}");
        Debug.Log($"Tolerance: {simplificationTolerance} world units");
    }
    
    [ContextMenu("Debug Direction Changes")]
    public void DebugDirectionChanges()
    {
        if (allDirectionChanges.Count == 0)
        {
            Debug.Log("No direction change data available");
            return;
        }
        
        float totalDirectionChanges = 0f;
        float maxDirectionChange = 0f;
        int totalPoints = 0;
        int sharpTurns = 0; // Count turns > 45 degrees
        
        foreach (var directionChangeList in allDirectionChanges)
        {
            foreach (float change in directionChangeList)
            {
                totalDirectionChanges += change;
                totalPoints++;
                
                if (change > maxDirectionChange)
                {
                    maxDirectionChange = change;
                }
                
                // Count sharp turns (> 45 degrees = 0.25 in normalized range)
                if (change > 0.25f)
                {
                    sharpTurns++;
                }
            }
        }
        
        float averageDirectionChange = totalPoints > 0 ? totalDirectionChanges / totalPoints : 0f;
        
        Debug.Log($"=== Direction Change Stats ===");
        Debug.Log($"Total streamlines: {allDirectionChanges.Count}");
        Debug.Log($"Total points: {totalPoints}");
        Debug.Log($"Average direction change: {averageDirectionChange * 180f:F2}° ({averageDirectionChange:F4} normalized)");
        Debug.Log($"Maximum direction change: {maxDirectionChange * 180f:F2}° ({maxDirectionChange:F4} normalized)");
        Debug.Log($"Sharp turns (>45°): {sharpTurns} ({100f * sharpTurns / totalPoints:F1}% of points)");
    }

    // Add this new helper method to convert world coordinates back to grid coordinates
    private Vector3 WorldToGridPosition(Vector3 worldPos)
    {
        Vector3 normalizedPos = new Vector3(
            (worldPos.x - gridMin.x) / (gridMax.x - gridMin.x),
            (worldPos.z - gridMin.z) / (gridMax.z - gridMin.z),
            (worldPos.y - gridMin.y) / (gridMax.y - gridMin.y)
        );
        
        return new Vector3(
            normalizedPos.x * (gridDimensions.x - 1),
            normalizedPos.y * (gridDimensions.y - 1),
            normalizedPos.z * (gridDimensions.z - 1)
        );
    }
} 