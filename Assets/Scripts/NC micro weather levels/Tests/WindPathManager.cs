using System.Collections.Generic;
using UnityEngine;

public class WindPathManager : MonoBehaviour
{
    [Header("Prefab Setup")]
    public GameObject windPathPrefab; // Prefab with WindTrilinearInterpolator component
    public NcDataContainerImgs dataContainer;
    
    [Header("Path Generation Settings - Wall Points Only")]
    public bool generateOnStart = true;
    public bool clearExistingPaths = true;
    
    [Header("Path Settings")]
    public int maxStepsPerPath = 100;
    
    [Header("Visualization")]
    public Color pathColor = Color.green;
    
    private List<GameObject> instantiatedPaths = new List<GameObject>();
    private List<WindTrilinearInterpolator> windInterpolators = new List<WindTrilinearInterpolator>();
    
    void Start()
    {
        if (generateOnStart)
        {
            StartCoroutine(WaitForDataAndGeneratePaths());
        }
    }
    
    System.Collections.IEnumerator WaitForDataAndGeneratePaths()
    {
        // Wait for data container to be loaded
        while (dataContainer == null || !dataContainer.IsLoaded)
        {
            yield return null;
        }
        
        GenerateWindPaths();
    }
    
    [ContextMenu("Generate Wind Paths")]
    public void GenerateWindPaths()
    {
        if (dataContainer == null || !dataContainer.IsLoaded)
        {
            Debug.LogError("Data container is not loaded!");
            return;
        }
        
        if (windPathPrefab == null)
        {
            Debug.LogError("Wind path prefab is not assigned!");
            return;
        }
        
        // Clear existing paths if requested
        if (clearExistingPaths)
        {
            ClearAllPaths();
        }
        
        // Generate wall data point positions
        List<Vector3> wallPositions = GenerateWallDataPointPositions();
        
        // Create wind paths
        for (int i = 0; i < wallPositions.Count; i++)
        {
            CreateWindPath(wallPositions[i], i);
        }
        
        Debug.Log($"Generated {instantiatedPaths.Count} wind paths from wall data points");
    }
    
    List<Vector3> GenerateWallDataPointPositions()
    {
        List<Vector3> wallPositions = new List<Vector3>();
        
        if (dataContainer == null || !dataContainer.IsLoaded)
        {
            Debug.LogWarning("Data container not loaded, cannot generate wall data point positions");
            return wallPositions;
        }
        
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
        
        Debug.Log($"Found {wallPositions.Count} data points on walls out of {dataContainer.x_from_origin.Count} total data points");
        return wallPositions;
    }
    
    void CreateWindPath(Vector3 startPosition, int pathIndex)
    {
        // Instantiate the prefab
        GameObject pathInstance = Instantiate(windPathPrefab, transform);
        pathInstance.name = $"WindPath_{pathIndex}";
        
        // Get the WindTrilinearInterpolator component
        WindTrilinearInterpolator interpolator = pathInstance.GetComponent<WindTrilinearInterpolator>();
        if (interpolator == null)
        {
            Debug.LogError($"WindPath prefab must have WindTrilinearInterpolator component!");
            Destroy(pathInstance);
            return;
        }
        
        // Configure the interpolator
        interpolator.dataContainer = dataContainer;
        interpolator.startPosition = startPosition;
        interpolator.maxSteps = maxStepsPerPath;
        
        // Set path color
        interpolator.lineColor = pathColor;
        
        // Update line renderer color if it exists
        if (interpolator.lineRenderer != null)
        {
            interpolator.lineRenderer.startColor = pathColor;
            interpolator.lineRenderer.endColor = pathColor;
        }
        
        // Store references
        instantiatedPaths.Add(pathInstance);
        windInterpolators.Add(interpolator);
        
        // Force regeneration of the path
        interpolator.RegeneratePath();
        
        // Debug: Log first few path start positions
        if (pathIndex < 5)
        {
            Debug.Log($"Path {pathIndex}: Start position={startPosition}");
        }
    }
    
    [ContextMenu("Clear All Paths")]
    public void ClearAllPaths()
    {
        foreach (GameObject path in instantiatedPaths)
        {
            if (path != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(path);
                }
                else
                {
                    DestroyImmediate(path);
                }
            }
        }
        
        instantiatedPaths.Clear();
        windInterpolators.Clear();
        
        Debug.Log("Cleared all wind paths");
    }
    
    [ContextMenu("Regenerate All Paths")]
    public void RegenerateAllPaths()
    {
        foreach (WindTrilinearInterpolator interpolator in windInterpolators)
        {
            if (interpolator != null)
            {
                interpolator.RegeneratePath();
            }
        }
        
        Debug.Log("Regenerated all wind paths");
    }
    

    
    public void SetPathColor(int pathIndex, Color color)
    {
        if (pathIndex >= 0 && pathIndex < windInterpolators.Count)
        {
            WindTrilinearInterpolator interpolator = windInterpolators[pathIndex];
            if (interpolator != null)
            {
                interpolator.lineColor = color;
                if (interpolator.lineRenderer != null)
                {
                    interpolator.lineRenderer.startColor = color;
                    interpolator.lineRenderer.endColor = color;
                }
            }
        }
    }
    

    
    void OnDestroy()
    {
        ClearAllPaths();
    }
} 