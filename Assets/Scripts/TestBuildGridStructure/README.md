# Grid Structure Performance Analysis

This folder contains scripts to investigate and analyze the performance bottlenecks in the grid structure building process of the wind visualization system.

## Files

- **TestDataGenerator.cs** - Generates test data similar to the wind field data (10x10x10 by default)
- **GridStructurePerformanceTester.cs** - Analyzes performance breakdown of the original grid building method
- **TestController.cs** - Orchestrates the tests and provides easy controls
- **README.md** - This documentation

## Setup Instructions

1. Create an empty GameObject in your test scene
2. Add all three scripts to the GameObject:
   - TestDataGenerator
   - GridStructurePerformanceTester  
   - TestController
3. The TestController will automatically find and link the other components

## How to Run Analysis

### Automatic Testing
- The analysis will run automatically when you play the scene (if `runTestOnStart` is enabled)

### Manual Testing
- **Right-click** on TestController → "Run All Tests"
- **Right-click** on GridStructurePerformanceTester → "Run Performance Test"
- **Right-click** on TestDataGenerator → "Generate Test Data"

### Keyboard Shortcuts (during play)
- **T** - Run all tests
- **L** - Test with larger dataset (50x50x10)
- **D** - Toggle detailed logs
- **I** - Toggle per-iteration logs (shows first 10 iterations)

## What It Analyzes

### Performance Breakdown
The tester breaks down the original grid building method into distinct steps:

1. **Distinct().OrderBy() Operations**
   ```csharp
   var uniqueX = x_from_origin.Distinct().OrderBy(x => x).ToList();
   var uniqueY = y_from_origin.Distinct().OrderBy(y => y).ToList();
   var uniqueZ = msl.Distinct().OrderBy(z => z).ToList();
   ```

2. **FindIndex Operations** (Expected bottleneck)
   ```csharp
   int xIdx = uniqueX.FindIndex(x => Mathf.Approximately(x, x_from_origin[i]));
   int yIdx = uniqueY.FindIndex(y => Mathf.Approximately(y, y_from_origin[i]));
   int zIdx = uniqueZ.FindIndex(z => Mathf.Approximately(z, msl[i]));
   ```

3. **Dictionary Operations**
   ```csharp
   if (!gridToIndex.ContainsKey(gridPos))
       gridToIndex[gridPos] = i;
   ```

## Expected Analysis Results

For a 10x10x10 grid (1,000 points), you should see:
- **Distinct().OrderBy()**: ~0-1ms (minimal impact)
- **FindIndex operations**: ~1-10ms (major bottleneck)
- **Dictionary operations**: ~0-1ms (minimal impact)

The FindIndex operations should consume 80-95% of the total time.

## Performance Metrics Provided

- **Total execution time** for each step
- **Percentage breakdown** of where time is spent
- **Computational complexity analysis**:
  - Total FindIndex calls (data_points × 3)
  - Average unique list size
  - Theoretical operations count
  - Operations per millisecond

## Configuration Options

### TestDataGenerator
- `gridSizeX/Y/Z` - Size of the test grid
- `minX/maxX` etc. - Spatial bounds of the test data
- `showDebugInfo` - Enable detailed logging

### GridStructurePerformanceTester  
- `showDetailedLogs` - Show detailed performance information
- `showPerIterationLogs` - Show first 10 iterations of FindIndex operations

### TestController
- `runTestOnStart` - Auto-run analysis when scene starts
- `testMultipleSizes` - Test with multiple grid sizes (20x20, 30x30, 50x50)

## Understanding the Bottleneck

The analysis will confirm that **FindIndex operations** are the primary performance bottleneck:

- **FindIndex** performs a linear search through the entire list (O(n))
- Called **3 times per data point** (X, Y, Z coordinates)
- With 1,000 data points and ~10 unique values per dimension:
  - Total FindIndex calls: 3,000
  - Average operations per call: 5 (linear search average)
  - Total operations: ~15,000

For larger datasets, this scales quadratically and becomes the dominant performance cost.

## Scaling Analysis

Use the "Test Multiple Sizes" option to see how performance degrades:
- 10×10×10 (1,000 points): Baseline
- 20×20×10 (4,000 points): ~4× slower
- 30×30×10 (9,000 points): ~9× slower  
- 50×50×10 (25,000 points): ~25× slower

This quadratic scaling confirms the O(n²) complexity of the FindIndex approach. 