using UnityEngine;

public class TestController : MonoBehaviour
{
    [Header("Components")]
    public TestDataGenerator dataGenerator;
    public GridStructurePerformanceTester performanceTester;
    
    [Header("Test Configuration")]
    public bool runTestOnStart = true;
    public bool testMultipleSizes = false;
    
    void Start()
    {
        // Find components if not assigned
        if (dataGenerator == null)
            dataGenerator = FindObjectOfType<TestDataGenerator>();
        if (performanceTester == null)
            performanceTester = FindObjectOfType<GridStructurePerformanceTester>();
            
        if (runTestOnStart)
        {
            Invoke(nameof(RunTests), 0.5f); // Small delay to ensure everything is initialized
        }
    }
    
    [ContextMenu("Run All Tests")]
    public void RunTests()
    {
        Debug.Log("=== Starting Grid Structure Performance Investigation ===");
        
        if (dataGenerator == null || performanceTester == null)
        {
            Debug.LogError("Missing components! Ensure TestDataGenerator and GridStructurePerformanceTester are in the scene.");
            return;
        }
        
        // Test with default 10x10x10
        Debug.Log("Analyzing 10x10x10 grid (1,000 points) performance...");
        dataGenerator.GenerateTestData();
        performanceTester.RunPerformanceTest();
        
        if (testMultipleSizes)
        {
            // Test with larger sizes to see scaling behavior
            TestDifferentSizes();
        }
        
        Debug.Log("=== Performance Investigation Complete ===");
    }
    
    void TestDifferentSizes()
    {
        int[] testSizes = { 20, 30, 50 }; // 20x20x10, 30x30x10, 50x50x10
        
        foreach (int size in testSizes)
        {
            Debug.Log($"\n--- Analyzing {size}x{size}x10 grid ({size * size * 10} points) ---");
            
            // Store original values
            int originalX = dataGenerator.gridSizeX;
            int originalY = dataGenerator.gridSizeY;
            
            // Set new size
            dataGenerator.gridSizeX = size;
            dataGenerator.gridSizeY = size;
            dataGenerator.gridSizeZ = 10; // Keep Z constant
            
            // Run analysis
            dataGenerator.GenerateTestData();
            performanceTester.RunPerformanceTest();
            
            // Restore original values
            dataGenerator.gridSizeX = originalX;
            dataGenerator.gridSizeY = originalY;
        }
        
        // Restore original data
        dataGenerator.GenerateTestData();
    }
    
    void Update()
    {
        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.T))
        {
            RunTests();
        }
        
        if (Input.GetKeyDown(KeyCode.L))
        {
            performanceTester.TestWithLargerDataset();
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            // Toggle detailed logs
            performanceTester.showDetailedLogs = !performanceTester.showDetailedLogs;
            Debug.Log($"Detailed logs: {(performanceTester.showDetailedLogs ? "ON" : "OFF")}");
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            // Toggle per-iteration logs
            performanceTester.showPerIterationLogs = !performanceTester.showPerIterationLogs;
            Debug.Log($"Per-iteration logs: {(performanceTester.showPerIterationLogs ? "ON" : "OFF")}");
        }
    }
} 