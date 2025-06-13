using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class GridStructurePerformanceTester : MonoBehaviour
{
    [Header("References")]
    public TestDataGenerator dataGenerator;
    
    [Header("Grid Structure Results")]
    public Vector3Int gridDimensions;
    public Vector3 gridMin;
    public Vector3 gridMax;
    public Vector3 gridCellSize;
    
    [Header("Performance Results")]
    public long gridBuildingTimeMs = 0;
    public long distinctOrderByTimeMs = 0;
    public long findIndexTimeMs = 0;
    public long dictionaryOpsTimeMs = 0;
    public long directCalculationTimeMs = 0;
    
    [Header("Validation Results")]
    public bool resultsMatch = false;
    public int mismatchCount = 0;
    
    [Header("Debug")]
    public bool showDetailedLogs = true;
    public bool showPerIterationLogs = false;
    
    // Grid mapping data (same as original)
    private Dictionary<Vector3Int, int> gridToIndex = new Dictionary<Vector3Int, int>();
    private Vector3Int[] uniqueGridPositions;
    private float gridCellWidth;
    
    void Start()
    {
        if (dataGenerator == null)
            dataGenerator = GetComponent<TestDataGenerator>();
    }
    
    [ContextMenu("Run Performance Test")]
    public void RunPerformanceTest()
    {
        if (dataGenerator == null || dataGenerator.x_from_origin.Count == 0)
        {
            Debug.LogError("No test data available! Generate test data first.");
            return;
        }
        
        Debug.Log("=== Grid Structure Performance Analysis ===");
        Debug.Log($"Testing with {dataGenerator.totalDataPoints} data points");
        
        // Test original method with detailed timing
        TestOriginalMethodDetailed();
        
        // Test direct calculation method and compare results
        TestDirectCalculationAndCompare();
        
        Debug.Log("=== Performance Analysis Complete ===");
    }
    
    void TestOriginalMethodDetailed()
    {
        Debug.Log("Analyzing ORIGINAL method performance breakdown...");
        
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        Stopwatch stepStopwatch = new Stopwatch();
        
        // Clear previous results
        gridToIndex.Clear();
        uniqueGridPositions = new Vector3Int[dataGenerator.x_from_origin.Count];
        
        // STEP 1: Distinct().OrderBy() operations
        stepStopwatch.Start();
        var uniqueX = dataGenerator.x_from_origin.Distinct().OrderBy(x => x).ToList();
        var uniqueY = dataGenerator.y_from_origin.Distinct().OrderBy(y => y).ToList();
        var uniqueZ = dataGenerator.msl.Distinct().OrderBy(z => z).ToList();
        stepStopwatch.Stop();
        distinctOrderByTimeMs = stepStopwatch.ElapsedMilliseconds;
        
        if (showDetailedLogs)
        {
            Debug.Log($"Distinct().OrderBy() operations: {distinctOrderByTimeMs} ms");
            Debug.Log($"Unique counts - X: {uniqueX.Count}, Y: {uniqueY.Count}, Z: {uniqueZ.Count}");
        }
        
        // STEP 2: FindIndex operations
        stepStopwatch.Restart();
        for (int i = 0; i < dataGenerator.x_from_origin.Count; i++)
        {
            int xIdx = uniqueX.FindIndex(x => Mathf.Approximately(x, dataGenerator.x_from_origin[i]));
            int yIdx = uniqueY.FindIndex(y => Mathf.Approximately(y, dataGenerator.y_from_origin[i]));
            int zIdx = uniqueZ.FindIndex(z => Mathf.Approximately(z, dataGenerator.msl[i]));
            
            if (showPerIterationLogs && i < 10) // Show first 10 iterations
            {
               
                Debug.Log($"Point {i}: xIdx={xIdx}, yIdx={yIdx}, zIdx={zIdx}");
            }
            
            Vector3Int gridPos = new Vector3Int(xIdx, zIdx, yIdx);
            uniqueGridPositions[i] = gridPos;
        }
        stepStopwatch.Stop();
        findIndexTimeMs = stepStopwatch.ElapsedMilliseconds;
        
        // STEP 3: Dictionary operations
        stepStopwatch.Restart();
        for (int i = 0; i < uniqueGridPositions.Length; i++)
        {
            Vector3Int gridPos = uniqueGridPositions[i];
            if (!gridToIndex.ContainsKey(gridPos))
            {
                gridToIndex[gridPos] = i;
            }
        }
        stepStopwatch.Stop();
        dictionaryOpsTimeMs = stepStopwatch.ElapsedMilliseconds;
        
        totalStopwatch.Stop();
        gridBuildingTimeMs = totalStopwatch.ElapsedMilliseconds;
        
        // Store grid info
        gridDimensions = new Vector3Int(uniqueX.Count, uniqueZ.Count, uniqueY.Count);
        gridMin = new Vector3(uniqueX.Min(), uniqueZ.Min(), uniqueY.Min());
        gridMax = new Vector3(uniqueX.Max(), uniqueZ.Max(), uniqueY.Max());
        gridCellWidth = uniqueX.Count > 1 ? Mathf.Abs(uniqueX[1] - uniqueX[0]) : 1f;
        
        // Performance breakdown
        Debug.Log($"=== PERFORMANCE BREAKDOWN ===");
        Debug.Log($"Total grid building time: {gridBuildingTimeMs} ms");
        Debug.Log($"1. Distinct().OrderBy(): {distinctOrderByTimeMs} ms ({(float)distinctOrderByTimeMs/gridBuildingTimeMs*100:F1}%)");
        Debug.Log($"2. FindIndex operations: {findIndexTimeMs} ms ({(float)findIndexTimeMs/gridBuildingTimeMs*100:F1}%)");
        Debug.Log($"3. Dictionary operations: {dictionaryOpsTimeMs} ms ({(float)dictionaryOpsTimeMs/gridBuildingTimeMs*100:F1}%)");
        Debug.Log($"Grid dimensions: {gridDimensions}");
        Debug.Log($"Grid bounds: {gridMin} to {gridMax}");
        Debug.Log($"Built {gridToIndex.Count} unique grid positions from {dataGenerator.x_from_origin.Count} data points");
        
        // Calculate theoretical FindIndex operations
        int totalFindIndexCalls = dataGenerator.x_from_origin.Count * 3; // X, Y, Z for each point
        int avgUniqueSize = (uniqueX.Count + uniqueY.Count + uniqueZ.Count) / 3;
        long theoreticalOperations = totalFindIndexCalls * avgUniqueSize / 2; // Average case for linear search
        
        Debug.Log($"=== COMPUTATIONAL COMPLEXITY ===");
        Debug.Log($"Total FindIndex calls: {totalFindIndexCalls}");
        Debug.Log($"Average unique list size: {avgUniqueSize}");
        Debug.Log($"Theoretical operations (avg case): {theoreticalOperations:N0}");
        Debug.Log($"Operations per millisecond: {(theoreticalOperations / (findIndexTimeMs + 1)):N0}"); // +1 to avoid division by zero
    }
    
    void TestDirectCalculationAndCompare()
    {
        Debug.Log("Testing DIRECT CALCULATION method and comparing results...");
        
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        // Get grid parameters from data generator
        float minX = dataGenerator.minX;
        float maxX = dataGenerator.maxX;
        float minY = dataGenerator.minY;
        float maxY = dataGenerator.maxY;
        float minZ = dataGenerator.minZ;
        float maxZ = dataGenerator.maxZ;
        
        int gridSizeX = dataGenerator.gridSizeX;
        int gridSizeY = dataGenerator.gridSizeY;
        int gridSizeZ = dataGenerator.gridSizeZ;
        
        // Calculate step sizes
        float stepX = (maxX - minX) / (gridSizeX - 1);
        float stepY = (maxY - minY) / (gridSizeY - 1);
        float stepZ = (maxZ - minZ) / (gridSizeZ - 1);
        
        // Direct calculation approach
        Vector3Int[] directGridPositions = new Vector3Int[dataGenerator.x_from_origin.Count];
        
        for (int i = 0; i < dataGenerator.x_from_origin.Count; i++)
        {
            // Direct calculation - O(1) per point
            int xIdx = Mathf.RoundToInt((dataGenerator.x_from_origin[i] - minX) / stepX);
            int yIdx = Mathf.RoundToInt((dataGenerator.y_from_origin[i] - minY) / stepY);
            int zIdx = Mathf.RoundToInt((dataGenerator.msl[i] - minZ) / stepZ);
            
            // Same coordinate order as original: (xIdx, zIdx, yIdx)
            Vector3Int gridPos = new Vector3Int(xIdx, zIdx, yIdx);
            directGridPositions[i] = gridPos;
        }
        
        stopwatch.Stop();
        directCalculationTimeMs = stopwatch.ElapsedMilliseconds;
        
        // Compare results
        mismatchCount = 0;
        for (int i = 0; i < uniqueGridPositions.Length; i++)
        {
            if (uniqueGridPositions[i] != directGridPositions[i])
            {
                mismatchCount++;
                if (mismatchCount <= 5) // Show first 5 mismatches
                {
                    Debug.LogWarning($"Mismatch at point {i}: FindIndex={uniqueGridPositions[i]}, Direct={directGridPositions[i]}");
                    Debug.LogWarning($"  Data point: ({dataGenerator.x_from_origin[i]:F3}, {dataGenerator.y_from_origin[i]:F3}, {dataGenerator.msl[i]:F3})");
                }
            }
        }
        
        resultsMatch = (mismatchCount == 0);
        
        // Results
        Debug.Log($"=== DIRECT CALCULATION RESULTS ===");
        Debug.Log($"Direct calculation time: {directCalculationTimeMs} ms");
        Debug.Log($"Performance improvement: {(float)findIndexTimeMs / (directCalculationTimeMs + 1):F1}x faster");
        Debug.Log($"Results match: {resultsMatch}");
        if (!resultsMatch)
        {
            Debug.LogWarning($"Found {mismatchCount} mismatches out of {uniqueGridPositions.Length} points");
            Debug.LogWarning("This suggests the data may not be perfectly regular or there are floating-point precision issues");
        }
        else
        {
            Debug.Log("✓ Perfect match! This confirms the data is a regular grid structure.");
            Debug.Log("✓ Direct calculation can safely replace FindIndex operations for massive performance gains.");
        }
    }
    
    [ContextMenu("Test With Larger Dataset")]
    public void TestWithLargerDataset()
    {
        // Temporarily increase grid size for more realistic performance testing
        int originalX = dataGenerator.gridSizeX;
        int originalY = dataGenerator.gridSizeY;
        int originalZ = dataGenerator.gridSizeZ;
        
        // Test with 50x50x10 = 25,000 points
        dataGenerator.gridSizeX = 50;
        dataGenerator.gridSizeY = 50;
        dataGenerator.gridSizeZ = 10;
        
        dataGenerator.GenerateTestData();
        RunPerformanceTest();
        
        // Restore original size
        dataGenerator.gridSizeX = originalX;
        dataGenerator.gridSizeY = originalY;
        dataGenerator.gridSizeZ = originalZ;
        dataGenerator.GenerateTestData();
    }
} 