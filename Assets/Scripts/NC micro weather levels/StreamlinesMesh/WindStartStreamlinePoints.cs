using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum StartPointMode
{
    WallPoints,
    VolumePoints,
    RandomVolumeDensity
}

public class WindStartStreamlinePoints : MonoBehaviour
{
    [Header("Point Generation Mode")]
    [Tooltip("Choose between wall points or volume points")]
    public StartPointMode pointMode = StartPointMode.WallPoints;
    
    [Header("Wall Point Sampling")]
    [Tooltip("Sample wall points every X meters (e.g., 50 = every 50m)")]
    public float wallSamplingInterval = 50f;
    
    [Header("Volume Point Sampling")]
    [Tooltip("Number of points along X axis (longitude)")]
    public int numPointsX = 5;
    [Tooltip("Number of points along Y axis (altitude)")]
    public int numPointsY = 3;
    [Tooltip("Number of points along Z axis (latitude)")]
    public int numPointsZ = 5;
    [Tooltip("Add irregularity to grid positions (0 = regular grid, 1 = maximum jitter)")]
    [Range(0f, 1f)]
    public float irregularity = 0f;
    [Tooltip("Random seed for irregular positioning (0 = use system time)")]
    public int irregularitySeed = 0;
    
    [Header("Random Volume Point Sampling")]
    [Tooltip("Points per 100 cubic meters")]
    public float pointsPer100CubicMetersDensity = 1f;
    [Tooltip("Random seed for reproducible results (0 = use system time)")]
    public int randomSeedDensity = 0;
    [Tooltip("Height-based density falloff (higher = more points at lower altitudes)")]
    [Range(0.1f, 5f)]
    public float heightDensityFalloff = 2f;
    [Tooltip("Minimum density multiplier at maximum height (as fraction of base density)")]
    [Range(0.1f, 1f)]
    public float minDensityFraction = 0.2f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool drawGizmos = true;
    
    // Cached results
    private List<Vector3> cachedStartPoints = new List<Vector3>();
    private WindFieldStreamlinesCalculator streamlinesCalculator;
    private bool pointsGenerated = false;
    
    void Start()
    {
        // Get the streamlines calculator on the same GameObject
        streamlinesCalculator = GetComponent<WindFieldStreamlinesCalculator>();
        if (streamlinesCalculator == null)
        {
            Debug.LogError("WindStartStreamlinePoints requires WindFieldStreamlinesCalculator on the same GameObject!");
            return;
        }
        
        // Generate points when data is ready
        if (streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            GenerateStartPoints();
        }
    }
    
    void Update()
    {
        // Generate points when data becomes available
        if (!pointsGenerated && streamlinesCalculator != null && 
            streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            GenerateStartPoints();
        }
    }
    
    [ContextMenu("Regenerate Start Points")]
    public void GenerateStartPoints()
    {
        if (streamlinesCalculator == null || streamlinesCalculator.dataContainer == null || 
            !streamlinesCalculator.dataContainer.IsLoaded)
        {
            Debug.LogWarning("Cannot generate start points: data not ready");
            return;
        }
        
        cachedStartPoints.Clear();
        pointsGenerated = false; // Reset flag to ensure fresh generation
        
        if (pointMode == StartPointMode.WallPoints)
        {
            cachedStartPoints = GenerateGridSampledWallPoints();
        }
        else if (pointMode == StartPointMode.VolumePoints)
        {
            cachedStartPoints = GenerateVolumePoints();
        }
        else if (pointMode == StartPointMode.RandomVolumeDensity)
        {
            cachedStartPoints = GenerateRandomVolumeDensityPoints();
        }
        
        pointsGenerated = true;
        
        if (showDebugInfo)
        {
            string modeDescription = "";
            if (pointMode == StartPointMode.WallPoints)
            {
                modeDescription = "Grid Sampled Wall Points";
            }
            else if (pointMode == StartPointMode.VolumePoints)
            {
                modeDescription = "Volume Points";
            }
            else if (pointMode == StartPointMode.RandomVolumeDensity)
            {
                modeDescription = "Random Volume Points";
            }
            
            Debug.Log($"Generated {cachedStartPoints.Count} start points using {modeDescription}");
        }
        
        // Trigger streamline regeneration so mesh updates
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.GenerateStreamlines();
        }
    }
    
    public List<Vector3> GetStartPoints()
    {
        if (!pointsGenerated)
        {
            GenerateStartPoints();
        }
        return new List<Vector3>(cachedStartPoints);
    }
    
    List<Vector3> GenerateGridSampledWallPoints()
    {
        var dataContainer = streamlinesCalculator.dataContainer;
        
        // Find the bounds of the data
        float minX = dataContainer.x_from_origin.Min();
        float maxX = dataContainer.x_from_origin.Max();
        float minY = dataContainer.y_from_origin.Min();
        float maxY = dataContainer.y_from_origin.Max();
        float minZ = dataContainer.msl.Min();
        float maxZ = dataContainer.msl.Max();
        
        Vector3 worldSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        
        List<Vector3> sampledWallPoints = new List<Vector3>();
        
        if (showDebugInfo)
        {
            Debug.Log($"World size: {worldSize}, Sampling: every {wallSamplingInterval}m");
        }
        
        // Generate wall points at regular intervals
        // We'll generate points on all 6 faces of the bounding box
        
        // Calculate number of steps for each dimension
        int stepsX = Mathf.RoundToInt(worldSize.x / wallSamplingInterval) + 1;
        int stepsY = Mathf.RoundToInt(worldSize.y / wallSamplingInterval) + 1;
        int stepsZ = Mathf.RoundToInt(worldSize.z / wallSamplingInterval) + 1;
        
        // Face 1 & 2: X = 0 and X = 1 (left and right walls)
        for (int y = 0; y < stepsY; y++)
        {
            for (int z = 0; z < stepsZ; z++)
            {
                float normY = (float)y / (stepsY - 1);
                float normZ = (float)z / (stepsZ - 1);
                
                sampledWallPoints.Add(new Vector3(0f, normY, normZ)); // Left wall
                sampledWallPoints.Add(new Vector3(1f, normY, normZ)); // Right wall
            }
        }
        
        // Face 3 & 4: Y = 0 and Y = 1 (bottom and top walls)
        for (int x = 0; x < stepsX; x++)
        {
            for (int z = 0; z < stepsZ; z++)
            {
                float normX = (float)x / (stepsX - 1);
                float normZ = (float)z / (stepsZ - 1);
                
                sampledWallPoints.Add(new Vector3(normX, 0f, normZ)); // Bottom wall
                sampledWallPoints.Add(new Vector3(normX, 1f, normZ)); // Top wall
            }
        }
        
        // Face 5 & 6: Z = 0 and Z = 1 (front and back walls)
        for (int x = 0; x < stepsX; x++)
        {
            for (int y = 0; y < stepsY; y++)
            {
                float normX = (float)x / (stepsX - 1);
                float normY = (float)y / (stepsY - 1);
                
                sampledWallPoints.Add(new Vector3(normX, normY, 0f)); // Front wall
                sampledWallPoints.Add(new Vector3(normX, normY, 1f)); // Back wall
            }
        }
        
        // Remove duplicates (corner and edge points will be duplicated)
        List<Vector3> uniquePoints = new List<Vector3>();
        float tolerance = 0.001f;
        
        foreach (Vector3 point in sampledWallPoints)
        {
            bool isDuplicate = false;
            foreach (Vector3 existing in uniquePoints)
            {
                if (Vector3.Distance(point, existing) < tolerance)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                uniquePoints.Add(point);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"Generated {sampledWallPoints.Count} wall points, {uniquePoints.Count} unique points after deduplication");
        }
        
        return uniquePoints;
    }
    
    List<Vector3> GenerateVolumePoints()
    {
        List<Vector3> volumePoints = new List<Vector3>();
        
        // Set random seed for irregular positioning if needed
        if (irregularity > 0f)
        {
            if (irregularitySeed != 0)
            {
                Random.InitState(irregularitySeed);
            }
            else
            {
                Random.InitState((int)System.DateTime.Now.Ticks);
            }
        }
        
        // Calculate grid spacing for jitter bounds
        float gridSpacingX = numPointsX > 1 ? 1f / (numPointsX - 1) : 1f;
        float gridSpacingY = numPointsY > 1 ? 1f / (numPointsY - 1) : 1f;
        float gridSpacingZ = numPointsZ > 1 ? 1f / (numPointsZ - 1) : 1f;
        
        // Maximum jitter is half the grid spacing to avoid overlap
        float maxJitterX = gridSpacingX * 0.5f * irregularity;
        float maxJitterY = gridSpacingY * 0.5f * irregularity;
        float maxJitterZ = gridSpacingZ * 0.5f * irregularity;
        
        // Generate regularly spaced points with optional jitter
        for (int x = 0; x < numPointsX; x++)
        {
            for (int y = 0; y < numPointsY; y++)
            {
                for (int z = 0; z < numPointsZ; z++)
                {
                    // Calculate base normalized position (0-1) for each dimension
                    float normX = numPointsX > 1 ? (float)x / (numPointsX - 1) : 0.5f;
                    float normY = numPointsY > 1 ? (float)y / (numPointsY - 1) : 0.5f;
                    float normZ = numPointsZ > 1 ? (float)z / (numPointsZ - 1) : 0.5f;
                    
                    // Add random jitter if irregularity is enabled
                    if (irregularity > 0f)
                    {
                        float jitterX = Random.Range(-maxJitterX, maxJitterX);
                        float jitterY = Random.Range(-maxJitterY, maxJitterY);
                        float jitterZ = Random.Range(-maxJitterZ, maxJitterZ);
                        
                        normX = Mathf.Clamp01(normX + jitterX);
                        normY = Mathf.Clamp01(normY + jitterY);
                        normZ = Mathf.Clamp01(normZ + jitterZ);
                    }
                    
                    volumePoints.Add(new Vector3(normX, normY, normZ));
                }
            }
        }
        
        if (showDebugInfo)
        {
            var dataContainer = streamlinesCalculator.dataContainer;
            float minX = dataContainer.x_from_origin.Min();
            float maxX = dataContainer.x_from_origin.Max();
            float minY = dataContainer.y_from_origin.Min();
            float maxY = dataContainer.y_from_origin.Max();
            float minZ = dataContainer.msl.Min();
            float maxZ = dataContainer.msl.Max();
            
            Vector3 worldSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            float spacingX = worldSize.x / (numPointsX > 1 ? numPointsX - 1 : 1);
            float spacingY = worldSize.y / (numPointsY > 1 ? numPointsY - 1 : 1);
            float spacingZ = worldSize.z / (numPointsZ > 1 ? numPointsZ - 1 : 1);
            
            Debug.Log($"Volume grid: {numPointsX}x{numPointsY}x{numPointsZ} = {volumePoints.Count} points");
            Debug.Log($"Point spacing: X={spacingX:F1}m, Y={spacingY:F1}m, Z={spacingZ:F1}m");
            
            if (irregularity > 0f)
            {
                float maxJitterXWorld = spacingX * 0.5f * irregularity;
                float maxJitterYWorld = spacingY * 0.5f * irregularity;
                float maxJitterZWorld = spacingZ * 0.5f * irregularity;
                Debug.Log($"Irregularity: {irregularity:F2} with jitter up to X=±{maxJitterXWorld:F1}m, Y=±{maxJitterYWorld:F1}m, Z=±{maxJitterZWorld:F1}m");
                Debug.Log($"Irregularity seed used: {(irregularitySeed != 0 ? irregularitySeed.ToString() : "system time")}");
            }
        }
        
        return volumePoints;
    }
    
    List<Vector3> GenerateRandomVolumeDensityPoints()
    {
        List<Vector3> randomPoints = new List<Vector3>();
        
        // Calculate number of points based on density
        var dataContainer = streamlinesCalculator.dataContainer;
        float minX = dataContainer.x_from_origin.Min();
        float maxX = dataContainer.x_from_origin.Max();
        float minY = dataContainer.y_from_origin.Min();
        float maxY = dataContainer.y_from_origin.Max();
        float minZ = dataContainer.msl.Min();
        float maxZ = dataContainer.msl.Max();
        
        Vector3 worldSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        float volumeCubicMeters = worldSize.x * worldSize.y * worldSize.z;
        float volumePer100CubicMeters = volumeCubicMeters / 100f;
        
        // Calculate base number of points (we'll generate more and use rejection sampling)
        int baseNumPoints = Mathf.RoundToInt(volumePer100CubicMeters * pointsPer100CubicMetersDensity);
        // Increase the number of generated points to account for rejection sampling
        int targetNumPoints = Mathf.RoundToInt(baseNumPoints / minDensityFraction);
        targetNumPoints = Mathf.Max(1, targetNumPoints);
        
        if (showDebugInfo)
        {
            Debug.Log($"=== Random Volume Density Calculation ===");
            Debug.Log($"World size: {worldSize}");
            Debug.Log($"Total volume: {volumeCubicMeters:F0} cubic meters");
            Debug.Log($"Volume per 100m³: {volumePer100CubicMeters:F2}");
            Debug.Log($"Base density: {pointsPer100CubicMetersDensity} points/100m³");
            Debug.Log($"Height falloff: {heightDensityFalloff}");
            Debug.Log($"Min density fraction: {minDensityFraction}");
            Debug.Log($"Base points: {baseNumPoints}");
            Debug.Log($"Target points before rejection: {targetNumPoints}");
        }
        
        // Set random seed for reproducible results
        if (randomSeedDensity != 0)
        {
            Random.InitState(randomSeedDensity);
            if (showDebugInfo)
            {
                Debug.Log($"Using fixed random seed: {randomSeedDensity}");
            }
        }
        else
        {
            int dynamicSeed = (int)(System.DateTime.Now.Ticks % int.MaxValue) + UnityEngine.Random.Range(0, 10000);
            Random.InitState(dynamicSeed);
            if (showDebugInfo)
            {
                Debug.Log($"Using dynamic random seed: {dynamicSeed}");
            }
        }
        
        // Generate points using rejection sampling based on height
        int acceptedPoints = 0;
        int maxAttempts = targetNumPoints * 10; // Limit total attempts to prevent infinite loops
        int attempts = 0;
        
        while (acceptedPoints < baseNumPoints && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random normalized coordinates (0-1)
            float normX = Random.Range(0f, 1f);
            float normY = Random.Range(0f, 1f);
            float normZ = Random.Range(0f, 1f);
            
            // Calculate probability of accepting this point based on height
            // Use exponential falloff: p = minDensity + (1-minDensity) * e^(-falloff * height)
            float heightFactor = normZ; // Z is our height in normalized coordinates
            float acceptanceProbability = minDensityFraction + (1f - minDensityFraction) * Mathf.Exp(-heightDensityFalloff * heightFactor);
            
            // Randomly accept or reject the point based on the probability
            if (Random.value < acceptanceProbability)
            {
                randomPoints.Add(new Vector3(normX, normY, normZ));
                acceptedPoints++;
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"=== Generated {randomPoints.Count} points after {attempts} attempts ===");
            Debug.Log($"Acceptance rate: {(randomPoints.Count / (float)attempts):P1}");
            if (randomPoints.Count > 0)
            {
                // Sort points by height (Z) for better debugging visualization
                var heightGroups = randomPoints.GroupBy(p => Mathf.Floor(p.z * 10) / 10f)
                                            .OrderBy(g => g.Key)
                                            .ToDictionary(g => g.Key, g => g.Count());
                Debug.Log("Height distribution (normalized height : count):");
                foreach (var group in heightGroups)
                {
                    Debug.Log($"  {group.Key:F1} : {group.Value}");
                }
            }
        }
        
        return randomPoints;
    }
    
    void OnDrawGizmos()
    {
        if (!drawGizmos || cachedStartPoints.Count == 0 || streamlinesCalculator == null) return;
        
        // Draw start points
        Gizmos.color = Color.cyan;
        foreach (Vector3 point in cachedStartPoints)
        {
            // Convert normalized position to world position using the calculator's method
            Vector3 worldPos = ConvertToWorldPosition(point);
            Gizmos.DrawSphere(worldPos, 10f); // Size based on dataset resolution
        }
        
        // Draw bounds
        if (streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = (streamlinesCalculator.gridMin + streamlinesCalculator.gridMax) * 0.5f;
            Vector3 size = streamlinesCalculator.gridMax - streamlinesCalculator.gridMin;
            Gizmos.DrawWireCube(center, size);
        }
    }
    
    Vector3 ConvertToWorldPosition(Vector3 normalizedPos)
    {
        if (streamlinesCalculator == null) return normalizedPos;
        
        // Use the same conversion as the streamlines calculator
        Vector3 gridPos = new Vector3(
            normalizedPos.x * (streamlinesCalculator.gridDimensions.x - 1),
            normalizedPos.y * (streamlinesCalculator.gridDimensions.y - 1),
            normalizedPos.z * (streamlinesCalculator.gridDimensions.z - 1)
        );
        
        return streamlinesCalculator.GridToWorldPosition(gridPos);
    }
    
    [ContextMenu("Debug Info")]
    public void PrintDebugInfo()
    {
        if (streamlinesCalculator == null || streamlinesCalculator.dataContainer == null) return;
        
        var dataContainer = streamlinesCalculator.dataContainer;
        float minX = dataContainer.x_from_origin.Min();
        float maxX = dataContainer.x_from_origin.Max();
        float minY = dataContainer.y_from_origin.Min();
        float maxY = dataContainer.y_from_origin.Max();
        float minZ = dataContainer.msl.Min();
        float maxZ = dataContainer.msl.Max();
        
        Vector3 worldSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        
        Debug.Log($"Data bounds: X[{minX:F1}, {maxX:F1}] Y[{minY:F1}, {maxY:F1}] Z[{minZ:F1}, {maxZ:F1}]");
        Debug.Log($"World size: {worldSize}");
        Debug.Log($"Wall sampling interval: {wallSamplingInterval}m");
        Debug.Log($"Current cached points: {cachedStartPoints.Count}");
    }
} 