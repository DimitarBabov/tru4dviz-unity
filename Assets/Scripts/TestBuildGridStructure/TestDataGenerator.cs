using System.Collections.Generic;
using UnityEngine;

public class TestDataGenerator : MonoBehaviour
{
    [Header("Test Data Configuration")]
    public int gridSizeX = 10;
    public int gridSizeY = 10;
    public int gridSizeZ = 10;
    
    [Header("Spatial Bounds")]
    public float minX = -500f;
    public float maxX = 500f;
    public float minY = -300f;
    public float maxY = 300f;
    public float minZ = 100f;  // altitude
    public float maxZ = 1000f; // altitude
    
    [Header("Generated Data")]
    public List<float> x_from_origin = new List<float>();
    public List<float> y_from_origin = new List<float>();
    public List<float> msl = new List<float>(); // altitude data
    
    [Header("Debug Info")]
    public bool showDebugInfo = true;
    public int totalDataPoints = 0;
    
    [Header("All Data Points")]
    [SerializeField] private List<Vector3> allDataPoints = new List<Vector3>();
    
    void Start()
    {
        GenerateTestData();
    }
    
    [ContextMenu("Generate Test Data")]
    public void GenerateTestData()
    {
        x_from_origin.Clear();
        y_from_origin.Clear();
        msl.Clear();
        
        // Calculate step sizes
        float stepX = (maxX - minX) / (gridSizeX - 1);
        float stepY = (maxY - minY) / (gridSizeY - 1);
        float stepZ = (maxZ - minZ) / (gridSizeZ - 1);
        
        if (showDebugInfo)
        {
            Debug.Log($"Generating {gridSizeX}x{gridSizeY}x{gridSizeZ} test data");
            Debug.Log($"Step sizes - X: {stepX:F3}, Y: {stepY:F3}, Z: {stepZ:F3}");
        }
        
        // Generate grid data points
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    float xVal = minX + x * stepX;
                    float yVal = minY + y * stepY;
                    float zVal = minZ + z * stepZ;
                    
                    x_from_origin.Add(xVal);
                    y_from_origin.Add(yVal);
                    msl.Add(zVal);
                }
            }
        }
        
        totalDataPoints = x_from_origin.Count;
        
        // Update all data points for inspector viewing
        UpdateAllDataPoints();
        
        if (showDebugInfo)
        {
            Debug.Log($"Generated {totalDataPoints} data points");
            Debug.Log($"X range: {x_from_origin[0]:F3} to {x_from_origin[x_from_origin.Count-1]:F3}");
            Debug.Log($"Y range: {y_from_origin[0]:F3} to {y_from_origin[y_from_origin.Count-1]:F3}");
            Debug.Log($"Z range: {msl[0]:F3} to {msl[msl.Count-1]:F3}");
            
            // Show unique counts
            var uniqueX = new HashSet<float>(x_from_origin);
            var uniqueY = new HashSet<float>(y_from_origin);
            var uniqueZ = new HashSet<float>(msl);
            
            Debug.Log($"Unique values - X: {uniqueX.Count}, Y: {uniqueY.Count}, Z: {uniqueZ.Count}");
        }
    }
    
    private void UpdateAllDataPoints()
    {
        allDataPoints.Clear();
        
        // Show all data points as Vector3 (X, Y, Z) for inspector viewing
        for (int i = 0; i < x_from_origin.Count; i++)
        {
            Vector3 dataPoint = new Vector3(x_from_origin[i], y_from_origin[i], msl[i]);
            allDataPoints.Add(dataPoint);
        }
    }
} 