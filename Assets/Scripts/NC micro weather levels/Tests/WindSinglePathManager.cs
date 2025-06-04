using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WindSinglePathManager : MonoBehaviour
{
    [Header("Prefab Setup")]
    public GameObject windPathPrefab; // Same prefab as WindPathManager
    public NcDataContainerImgs dataContainer;
    
    [Header("Path Generation Settings")]
    public bool generateOnStart = true;
    public bool clearExistingPath = true;
    
    [Header("Path Settings")]
    public int maxStepsPerPath = 100;
    public Color pathColor = Color.green;
    

    
    private GameObject pathInstance;
    private WindTrilinearInterpolator windInterpolator;
    
    void Start()
    {
        if (generateOnStart)
        {
            StartCoroutine(WaitForDataAndGeneratePath());
        }
    }
    
    System.Collections.IEnumerator WaitForDataAndGeneratePath()
    {
        // Wait for data container to be loaded
        while (dataContainer == null || !dataContainer.IsLoaded)
        {
            yield return null;
        }
        
        GenerateRandomWindPath();
    }
    
    [ContextMenu("Generate Random Wind Path")]
    public void GenerateRandomWindPath()
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
        
        // Clear existing path if requested
        if (clearExistingPath)
        {
            ClearPath();
        }
        
        // Generate wall data point positions (same as WindPathManager)
        List<Vector3> wallPositions = GenerateWallDataPointPositions();
        
        if (wallPositions.Count == 0)
        {
            Debug.LogError("No wall positions found!");
            return;
        }
        
        // Select a single random wall position
        int randomIndex = Random.Range(0, wallPositions.Count);
        Vector3 randomStartPosition = wallPositions[randomIndex];
        
        // Create the wind path
        CreateWindPath(randomStartPosition);
        
        Debug.Log($"Generated random wind path starting at: {randomStartPosition}");
    }
    
    List<Vector3> GenerateWallDataPointPositions()
    {
        List<Vector3> wallPositions = new List<Vector3>();
        
        if (dataContainer == null || !dataContainer.IsLoaded)
        {
            Debug.LogWarning("Data container not loaded, cannot generate wall data point positions");
            return wallPositions;
        }
        
        // Find the bounds of the data (same logic as WindPathManager)
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
        
        Debug.Log($"Found {wallPositions.Count} wall data points");
        return wallPositions;
    }
    
    void CreateWindPath(Vector3 startPosition)
    {
        // Instantiate the prefab (same as WindPathManager)
        pathInstance = Instantiate(windPathPrefab, transform);
        pathInstance.name = "SingleWindPath";
        
        // Get the WindTrilinearInterpolator component
        windInterpolator = pathInstance.GetComponent<WindTrilinearInterpolator>();
        if (windInterpolator == null)
        {
            Debug.LogError($"WindPath prefab must have WindTrilinearInterpolator component!");
            Destroy(pathInstance);
            return;
        }
        
        // Configure the interpolator (same as WindPathManager)
        windInterpolator.dataContainer = dataContainer;
        windInterpolator.startPosition = startPosition;
        windInterpolator.maxSteps = maxStepsPerPath;
        
        // Debug: Check stepScale - if it's too small, paths won't move
        Debug.Log($"WindInterpolator stepScale: {windInterpolator.stepScale}");
        if (windInterpolator.stepScale < 0.001f)
        {
            Debug.LogWarning("stepScale is very small, increasing to 0.1");
            windInterpolator.stepScale = 0.1f;
        }
        
        // Set path color
        windInterpolator.lineColor = pathColor;
        
        // Update line renderer color if it exists
        if (windInterpolator.lineRenderer != null)
        {
            windInterpolator.lineRenderer.startColor = pathColor;
            windInterpolator.lineRenderer.endColor = pathColor;
        }
        
        // Force regeneration of the path
        windInterpolator.RegeneratePath();
        
        Debug.Log($"Created wind path with start position: {startPosition}");
        Debug.Log($"Path generated {windInterpolator.interpolatedPath.Count} points");
        
        // Debug: Check if data container is properly loaded
        if (windInterpolator.dataContainer == null)
        {
            Debug.LogError("WindInterpolator dataContainer is null!");
        }
        else if (!windInterpolator.dataContainer.IsLoaded)
        {
            Debug.LogError("WindInterpolator dataContainer is not loaded!");
        }
        else
        {
            Debug.Log($"DataContainer loaded with {windInterpolator.dataContainer.x_from_origin.Count} data points");
        }
    }
    
    
    [ContextMenu("Clear Path")]
    public void ClearPath()
    {
        if (pathInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(pathInstance);
            }
            else
            {
                DestroyImmediate(pathInstance);
            }
            pathInstance = null;
            windInterpolator = null;
        }
        

        
        Debug.Log("Cleared wind path");
    }
    
    [ContextMenu("Regenerate Path")]
    public void RegeneratePath()
    {
        if (windInterpolator != null)
        {
            windInterpolator.RegeneratePath();
        }
    }
    
    void OnDestroy()
    {
        ClearPath();
    }
} 