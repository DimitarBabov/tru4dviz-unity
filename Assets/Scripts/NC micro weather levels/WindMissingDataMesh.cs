using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WindMissingDataMesh : MonoBehaviour
{
    [Header("Data Source")]
    public NcDataContainerImgs ncDataContainer;
    
    [Header("Mesh Generation")]
    public Color solidColor = Color.white;
    
    [Header("Level Coloring")]
    [Tooltip("Coloring mode for different levels")]
    public ColoringMode coloringMode = ColoringMode.Alternating;
    
    [Header("Alternating Colors")]
    [Tooltip("First color for alternating levels")]
    public Color alternatingColor1 = Color.red;
    [Tooltip("Second color for alternating levels")]
    public Color alternatingColor2 = Color.blue;
    
    [Header("Multiple Level Colors")]
    [Tooltip("Colors for each level - will cycle through if there are more levels than colors")]
    public Color[] levelColors = new Color[] 
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        Color.white,
        Color.gray
    };
    
    [Header("Height Gradient")]
    [Tooltip("Bottom color for height gradient")]
    public Color gradientBottomColor = Color.blue;
    [Tooltip("Top color for height gradient")]
    public Color gradientTopColor = Color.red;
    
    [Header("Vertical Stacking")]
    public float layerHeight = 10.0f; // Height spacing between each image layer
    
    [Header("Top Surface")]
    public bool generateTopSurface = true;
    public GameObject topSurfacePrefab; // Optional prefab for top surface, if null will create automatically
    
    [Header("Loading Status")]
    [Tooltip("Shows current loading status")]
    public string loadingStatus = "Waiting for data...";
    
    [Header("Debug")]
    public bool showDebugInfo = true;

    private float quadSize = 1.0f; // Will be set from grid info

    void Start()
    {
        if (ncDataContainer == null)
        {
            Debug.LogError("NcDataContainerImgs reference not assigned!");
            loadingStatus = "Error: No data container assigned";
            return;
        }

        // Start the coroutine to wait for data loading
        StartCoroutine(WaitForDataAndGenerateMesh());
    }

    IEnumerator WaitForDataAndGenerateMesh()
    {
        loadingStatus = "Waiting for data container to load...";
        
        // Wait for the data container to finish loading
        while (!ncDataContainer.IsLoaded)
        {
            // Update status from data container if available
            if (!string.IsNullOrEmpty(ncDataContainer.loadingStatus))
            {
                loadingStatus = $"Data loading: {ncDataContainer.loadingStatus} ({ncDataContainer.loadingProgress:P0})";
            }
            
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }

        loadingStatus = "Data loaded, validating...";
        yield return null;

        if (ncDataContainer.levelImages == null || ncDataContainer.levelImages.Count == 0)
        {
            Debug.LogError("No textures found in NcDataContainerImgs.levelImages!");
            loadingStatus = "Error: No images found";
            yield break;
        }

        loadingStatus = "Generating mesh...";
        yield return null;

        // Get quad size from grid cell width
        quadSize = ncDataContainer.gridCellSize.x;
        
        // Use grid cell height for layer spacing if available
        if (ncDataContainer.gridCellSize.y > 0)
        {
            layerHeight = ncDataContainer.gridCellSize.y;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"Using quad size from grid info: {quadSize:F3}");
            Debug.Log($"Using layer height from grid info: {layerHeight:F3}");
            Debug.Log($"Grid bounds: {ncDataContainer.gridMin} to {ncDataContainer.gridMax}");
            Debug.Log($"Grid cell size: {ncDataContainer.gridCellSize}");
            Debug.Log($"Processing {ncDataContainer.levelImages.Count} images from NcDataContainerImgs");
        }

        // Generate the mesh (this might be heavy, so we could yield periodically if needed)
        GenerateMultiImageMesh();
        
        loadingStatus = "Mesh generation complete!";
    }

    void GenerateMultiImageMesh()
    {
        var sourceImages = ncDataContainer.levelImages;
        
        // Combined mesh data for all images
        List<Vector3> allVertices = new List<Vector3>();
        List<int> allTriangles = new List<int>();
        List<Vector2> allUvs = new List<Vector2>();
        List<Color> allColors = new List<Color>(); // Add vertex colors
        Dictionary<Vector3, int> globalVertexMap = new Dictionary<Vector3, int>();

        int totalOriginalEdges = 0;
        int totalOptimizedSegments = 0;

        // Combined top surface data for all images
        List<Vector3> allTopSurfaceVertices = new List<Vector3>();
        List<int> allTopSurfaceTriangles = new List<int>();
        List<Vector2> allTopSurfaceUvs = new List<Vector2>();
        List<Color> allTopSurfaceColors = new List<Color>(); // Add vertex colors for top surface

        // Calculate total height range for gradient coloring
        float minHeight = ncDataContainer.gridMin.y;
        float maxHeight = ncDataContainer.gridMin.y + (sourceImages.Count - 1) * layerHeight + quadSize;

        for (int imageIndex = 0; imageIndex < sourceImages.Count; imageIndex++)
        {
            Texture2D sourceImage = sourceImages[imageIndex];
            
            if (sourceImage == null)
            {
                Debug.LogWarning($"Source image at index {imageIndex} is null, skipping...");
                continue;
            }

            // Calculate vertical offset for this image layer
            Vector3 imageOffset = new Vector3(0, imageIndex * layerHeight, 0);

            if (showDebugInfo)
            {
                Debug.Log($"Processing image {imageIndex + 1}/{sourceImages.Count}: {sourceImage.name} at height {imageOffset.y}");
            }

            // Generate mesh for this image
            GenerateOptimizedMeshForImage(sourceImage, imageOffset, allVertices, allTriangles, allUvs, allColors, globalVertexMap, ref totalOriginalEdges, ref totalOptimizedSegments, imageIndex, minHeight, maxHeight);

            // Generate top surface data for this image if enabled
            if (generateTopSurface)
            {
                GenerateTopSurfaceForImage(sourceImage, imageOffset, allTopSurfaceVertices, allTopSurfaceTriangles, allTopSurfaceUvs, allTopSurfaceColors, imageIndex, minHeight, maxHeight);
            }
        }

        if (showDebugInfo)
        {
            float optimizationRatio = totalOriginalEdges > 0 ? (float)totalOptimizedSegments / totalOriginalEdges : 0f;
            Debug.Log($"Multi-image wall optimization: {totalOptimizedSegments} segments created from {totalOriginalEdges} potential individual edges");
            Debug.Log($"Overall optimization ratio: {optimizationRatio:F3} ({(1f - optimizationRatio) * 100:F1}% reduction in wall segment count)");
            Debug.Log($"Final combined mesh: {allVertices.Count} vertices, {allTriangles.Count / 3} triangles covering {sourceImages.Count} images");
        }

        // Create the combined mesh
        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.vertices = allVertices.ToArray();
        combinedMesh.triangles = allTriangles.ToArray();
        combinedMesh.uv = allUvs.ToArray();
        combinedMesh.colors = allColors.ToArray(); // Add vertex colors to mesh
        combinedMesh.RecalculateNormals();
        combinedMesh.RecalculateBounds();
        combinedMesh.name = $"MultiImageMesh_{sourceImages.Count}Images";

        // Create a child GameObject for the wall mesh instead of using this GameObject
        CreateWallMeshChild(combinedMesh);

        // Generate combined top surface if enabled
        if (generateTopSurface && allTopSurfaceVertices.Count > 0)
        {
            GenerateCombinedTopSurface(allTopSurfaceVertices, allTopSurfaceTriangles, allTopSurfaceUvs, allTopSurfaceColors);
        }
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.SaveAssets();
        #endif
    }

    void CreateWallMeshChild(Mesh wallMesh)
    {
        // Create child GameObject for wall mesh
        GameObject wallMeshObj = new GameObject(gameObject.name + "_WallMesh");
        wallMeshObj.transform.SetParent(transform);
        wallMeshObj.transform.localPosition = Vector3.zero;
        wallMeshObj.transform.localRotation = Quaternion.identity;
        wallMeshObj.transform.localScale = Vector3.one;
        
        // Add required components
        MeshFilter wallMeshFilter = wallMeshObj.AddComponent<MeshFilter>();
        MeshRenderer wallMeshRenderer = wallMeshObj.AddComponent<MeshRenderer>();
        
        // Assign the mesh
        wallMeshFilter.mesh = wallMesh;
        
        // Copy material from the original component
        MeshRenderer originalRenderer = GetComponent<MeshRenderer>();
        if (originalRenderer != null && originalRenderer.material != null)
        {
            wallMeshRenderer.material = originalRenderer.material;
        }

        if (showDebugInfo)
        {
            Debug.Log($"Wall mesh child created: '{wallMeshObj.name}' with {wallMesh.vertexCount} vertices, {wallMesh.triangles.Length / 3} triangles");
        }
    }

    void GenerateOptimizedMeshForImage(Texture2D sourceImage, Vector3 imageOffset, List<Vector3> allVertices, List<int> allTriangles, List<Vector2> allUvs, List<Color> allColors, Dictionary<Vector3, int> globalVertexMap, ref int totalOriginalEdges, ref int totalOptimizedSegments, int imageIndex, float minHeight, float maxHeight)
    {
        int width = sourceImage.width;
        int height = sourceImage.height;

        // First pass: identify all solid pixels
        HashSet<Vector2Int> solidPixels = new HashSet<Vector2Int>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Read pixels from right to left (mirrored)
                Color pixel = sourceImage.GetPixel(width - 1 - x, y);
                if (pixel.r == 1f && pixel.g == 1f && pixel.b == 1f && pixel.a == 1f)
                {
                    solidPixels.Add(new Vector2Int(x, y));
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"Found {solidPixels.Count} solid pixels to process for layer at height {imageOffset.y}");
        }

        // Second pass: generate optimized wall segments
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>(); // Add color list for this image
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();

        int originalEdgeCount = 0;
        int optimizedSegmentCount = 0;

        // Calculate color for this level
        Color levelColor;
        if (coloringMode == ColoringMode.HeightGradient)
        {
            float heightRatio = (imageOffset.y + ncDataContainer.gridMin.y - minHeight) / (maxHeight - minHeight);
            levelColor = Color.Lerp(gradientBottomColor, gradientTopColor, heightRatio);
        }
        else if (coloringMode == ColoringMode.Alternating)
        {
            levelColor = (imageIndex % 2 == 0) ? alternatingColor1 : alternatingColor2;
        }
        else
        {
            // Use discrete level colors, cycling through the array
            levelColor = levelColors[imageIndex % levelColors.Length];
        }

        // Optimize each direction separately
        optimizedSegmentCount += CreateOptimizedWallSegments(solidPixels, vertices, triangles, uvs, colors, vertexMap, WallDirection.Bottom, width, height, imageOffset, ref originalEdgeCount, levelColor);
        optimizedSegmentCount += CreateOptimizedWallSegments(solidPixels, vertices, triangles, uvs, colors, vertexMap, WallDirection.Top, width, height, imageOffset, ref originalEdgeCount, levelColor);
        optimizedSegmentCount += CreateOptimizedWallSegments(solidPixels, vertices, triangles, uvs, colors, vertexMap, WallDirection.Left, width, height, imageOffset, ref originalEdgeCount, levelColor);
        optimizedSegmentCount += CreateOptimizedWallSegments(solidPixels, vertices, triangles, uvs, colors, vertexMap, WallDirection.Right, width, height, imageOffset, ref originalEdgeCount, levelColor);

        // Adjust triangle indices for the global mesh
        int vertexOffset = allVertices.Count;
        for (int i = 0; i < triangles.Count; i++)
        {
            triangles[i] += vertexOffset;
        }

        // Add vertices, triangles, uvs, and colors to the global mesh
        allVertices.AddRange(vertices);
        allTriangles.AddRange(triangles);
        allUvs.AddRange(uvs);
        allColors.AddRange(colors);

        totalOriginalEdges += originalEdgeCount;
        totalOptimizedSegments += optimizedSegmentCount;
    }

    enum WallDirection
    {
        Bottom,
        Top,
        Left,
        Right
    }

    int CreateOptimizedWallSegments(HashSet<Vector2Int> solidPixels, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, Dictionary<Vector3, int> vertexMap, WallDirection direction, int width, int height, Vector3 imageOffset, ref int originalEdgeCount, Color levelColor)
    {
        // Find all boundary pixels for this direction
        HashSet<Vector2Int> boundaryPixels = new HashSet<Vector2Int>();
        
        foreach (Vector2Int pixel in solidPixels)
        {
            Vector2Int adjacentPixel = Vector2Int.zero;
            
            switch (direction)
            {
                case WallDirection.Bottom:
                    adjacentPixel = new Vector2Int(pixel.x, pixel.y - 1);
                    break;
                case WallDirection.Top:
                    adjacentPixel = new Vector2Int(pixel.x, pixel.y + 1);
                    break;
                case WallDirection.Left:
                    adjacentPixel = new Vector2Int(pixel.x - 1, pixel.y);
                    break;
                case WallDirection.Right:
                    adjacentPixel = new Vector2Int(pixel.x + 1, pixel.y);
                    break;
            }
            
            if (!solidPixels.Contains(adjacentPixel))
            {
                boundaryPixels.Add(pixel);
                originalEdgeCount++;
            }
        }

        if (boundaryPixels.Count == 0)
            return 0;

        // Track processed pixels to avoid duplicates
        HashSet<Vector2Int> processedPixels = new HashSet<Vector2Int>();
        int segmentsCreated = 0;

        foreach (Vector2Int pixel in boundaryPixels)
        {
            if (processedPixels.Contains(pixel))
                continue;

            // Find the longest continuous segment in the appropriate direction
            List<Vector2Int> segment = FindLongestWallSegment(boundaryPixels, processedPixels, pixel, direction, width, height);
            
            if (segment.Count > 0)
            {
                CreateWallSegmentQuad(segment, direction, vertices, triangles, uvs, colors, vertexMap, imageOffset, levelColor);
                segmentsCreated++;
            }
        }

        return segmentsCreated;
    }

    List<Vector2Int> FindLongestWallSegment(HashSet<Vector2Int> boundaryPixels, HashSet<Vector2Int> processedPixels, Vector2Int start, WallDirection direction, int width, int height)
    {
        List<Vector2Int> segment = new List<Vector2Int>();
        Vector2Int current = start;
        Vector2Int stepDirection = Vector2Int.zero;
        
        // Determine step direction based on wall direction
        switch (direction)
        {
            case WallDirection.Bottom:
            case WallDirection.Top:
                stepDirection = Vector2Int.right; // Extend horizontally
                break;
            case WallDirection.Left:
            case WallDirection.Right:
                stepDirection = Vector2Int.up; // Extend vertically
                break;
        }

        // Extend in both directions from start point
        
        // First, extend in positive direction
        current = start;
        while (boundaryPixels.Contains(current) && !processedPixels.Contains(current))
        {
            segment.Add(current);
            processedPixels.Add(current);
            current += stepDirection;
        }

        // Then, extend in negative direction from start
        current = start - stepDirection;
        List<Vector2Int> negativeSegment = new List<Vector2Int>();
        while (boundaryPixels.Contains(current) && !processedPixels.Contains(current))
        {
            negativeSegment.Add(current);
            processedPixels.Add(current);
            current -= stepDirection;
        }

        // Combine segments (negative segment is added in reverse order to maintain continuity)
        negativeSegment.Reverse();
        negativeSegment.AddRange(segment);
        
        return negativeSegment;
    }

    void CreateWallSegmentQuad(List<Vector2Int> segment, WallDirection direction, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, Dictionary<Vector3, int> vertexMap, Vector3 imageOffset, Color levelColor)
    {
        if (segment.Count == 0)
            return;

        Vector2Int segmentStart = segment[0];
        Vector2Int segmentEnd = segment[segment.Count - 1];
        
        Vector3 start = Vector3.zero;
        Vector3 end = Vector3.zero;
        
        // Get grid offset for proper world positioning
        Vector3 gridOffset = ncDataContainer.gridMin;
        
        // Calculate start and end positions based on direction, then add proper offsets
        switch (direction)
        {
            case WallDirection.Bottom:
                start = new Vector3(segmentStart.x * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, segmentStart.y * quadSize + gridOffset.z);
                end = new Vector3((segmentEnd.x + 1) * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, segmentStart.y * quadSize + gridOffset.z);
                break;
            case WallDirection.Top:
                start = new Vector3((segmentEnd.x + 1) * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, (segmentStart.y + 1) * quadSize + gridOffset.z);
                end = new Vector3(segmentStart.x * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, (segmentStart.y + 1) * quadSize + gridOffset.z);
                break;
            case WallDirection.Left:
                start = new Vector3(segmentStart.x * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, (segmentEnd.y + 1) * quadSize + gridOffset.z);
                end = new Vector3(segmentStart.x * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, segmentStart.y * quadSize + gridOffset.z);
                break;
            case WallDirection.Right:
                start = new Vector3((segmentStart.x + 1) * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, segmentStart.y * quadSize + gridOffset.z);
                end = new Vector3((segmentStart.x + 1) * quadSize + gridOffset.x, gridOffset.y + imageOffset.y, (segmentEnd.y + 1) * quadSize + gridOffset.z);
                break;
        }

        // Create the wall quad
        CreateEdgeQuadOptimized(start, end, vertices, triangles, uvs, colors, vertexMap, segment.Count, levelColor);
    }

    void CreateEdgeQuadOptimized(Vector3 start, Vector3 end, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, Dictionary<Vector3, int> vertexMap, int segmentLength, Color levelColor)
    {
        // Create a flat quad along the boundary edge
        Vector3 height = Vector3.up * quadSize;

        // Four corners of a flat quad
        Vector3[] edgeVertices = new Vector3[]
        {
            start,                    // 0 - bottom start
            end,                      // 1 - bottom end
            start + height,           // 2 - top start
            end + height              // 3 - top end
        };

        // Add vertices (reuse existing ones if possible to ensure connectivity)
        int[] vertexIndices = new int[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 vertex = edgeVertices[i];
            // Round vertex position to ensure proper sharing at corners
            vertex = new Vector3(
                Mathf.Round(vertex.x / 0.001f) * 0.001f,
                Mathf.Round(vertex.y / 0.001f) * 0.001f,
                Mathf.Round(vertex.z / 0.001f) * 0.001f
            );
            
            if (vertexMap.ContainsKey(vertex))
            {
                vertexIndices[i] = vertexMap[vertex];
            }
            else
            {
                vertexIndices[i] = vertices.Count;
                vertexMap[vertex] = vertices.Count;
                vertices.Add(vertex);
                // Create tiling UVs based on world position to show individual quads
                Vector2 uv = new Vector2(
                    vertex.x / quadSize, // Tile based on world X position
                    vertex.z / quadSize  // Tile based on world Z position
                );
                uvs.Add(uv);
                colors.Add(levelColor); // Add vertex color
            }
        }

        // Create two triangles for the flat quad
        triangles.AddRange(new int[] { vertexIndices[0], vertexIndices[2], vertexIndices[1] });
        triangles.AddRange(new int[] { vertexIndices[1], vertexIndices[2], vertexIndices[3] });
    }

    void GenerateTopSurfaceForImage(Texture2D sourceImage, Vector3 imageOffset, List<Vector3> allTopSurfaceVertices, List<int> allTopSurfaceTriangles, List<Vector2> allTopSurfaceUvs, List<Color> allTopSurfaceColors, int imageIndex, float minHeight, float maxHeight)
    {
        int width = sourceImage.width;
        int height = sourceImage.height;

        // Find all solid pixels
        HashSet<Vector2Int> solidPixels = new HashSet<Vector2Int>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Read pixels from right to left (mirrored)
                Color pixel = sourceImage.GetPixel(width - 1 - x, y);
                if (pixel.r == 1f && pixel.g == 1f && pixel.b == 1f && pixel.a == 1f)
                {
                    solidPixels.Add(new Vector2Int(x, y));
                }
            }
        }

        if (solidPixels.Count == 0)
        {
            if (showDebugInfo)
                Debug.Log($"No solid pixels found for top surface generation at height {imageOffset.y}");
            return;
        }

        // Generate optimized rectangular patches instead of individual quads
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>(); // Add color list for top surface
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();

        float topHeight = quadSize + ncDataContainer.gridMin.y; // Height from grid base plus quad height
        
        // Calculate color for this level
        Color levelColor;
        if (coloringMode == ColoringMode.HeightGradient)
        {
            float heightRatio = (topHeight + imageOffset.y - minHeight) / (maxHeight - minHeight);
            levelColor = Color.Lerp(gradientBottomColor, gradientTopColor, heightRatio);
        }
        else if (coloringMode == ColoringMode.Alternating)
        {
            levelColor = (imageIndex % 2 == 0) ? alternatingColor1 : alternatingColor2;
        }
        else
        {
            // Use discrete level colors, cycling through the array
            levelColor = levelColors[imageIndex % levelColors.Length];
        }
        
        // Track processed pixels to avoid duplicates
        HashSet<Vector2Int> processedPixels = new HashSet<Vector2Int>();
        int patchesCreated = 0;
        int totalPixelsCovered = 0;

        foreach (Vector2Int pixel in solidPixels)
        {
            if (processedPixels.Contains(pixel))
                continue;

            // Find the largest rectangular patch starting from this pixel
            Vector2Int patchStart = pixel;
            Vector2Int patchEnd = FindLargestRectangularPatch(solidPixels, processedPixels, patchStart, width, height);
            
            // Calculate patch dimensions
            int patchWidth = patchEnd.x - patchStart.x + 1;
            int patchHeight = patchEnd.y - patchStart.y + 1;
            int patchPixels = patchWidth * patchHeight;
            
            // Create a single large quad for this rectangular patch with proper positioning
            Vector3 gridOffset = ncDataContainer.gridMin;
            Vector3 bottomLeft = new Vector3(patchStart.x * quadSize + gridOffset.x, topHeight + imageOffset.y, patchStart.y * quadSize + gridOffset.z);
            Vector3 bottomRight = new Vector3((patchEnd.x + 1) * quadSize + gridOffset.x, topHeight + imageOffset.y, patchStart.y * quadSize + gridOffset.z);
            Vector3 topLeft = new Vector3(patchStart.x * quadSize + gridOffset.x, topHeight + imageOffset.y, (patchEnd.y + 1) * quadSize + gridOffset.z);
            Vector3 topRight = new Vector3((patchEnd.x + 1) * quadSize + gridOffset.x, topHeight + imageOffset.y, (patchEnd.y + 1) * quadSize + gridOffset.z);

            // Add vertices with proper sharing
            int[] vertexIndices = new int[4];
            Vector3[] patchVertices = { bottomLeft, bottomRight, topLeft, topRight };
            
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = patchVertices[i];
                // Round vertex position to ensure proper sharing
                vertex = new Vector3(
                    Mathf.Round(vertex.x / 0.001f) * 0.001f,
                    Mathf.Round(vertex.y / 0.001f) * 0.001f,
                    Mathf.Round(vertex.z / 0.001f) * 0.001f
                );
                
                if (vertexMap.ContainsKey(vertex))
                {
                    vertexIndices[i] = vertexMap[vertex];
                }
                else
                {
                    vertexIndices[i] = vertices.Count;
                    vertexMap[vertex] = vertices.Count;
                    vertices.Add(vertex);
                    // Create tiling UVs based on world position to show individual quads
                    Vector2 uv = new Vector2(
                        vertex.x / quadSize, // Tile based on world X position
                        vertex.z / quadSize  // Tile based on world Z position
                    );
                    uvs.Add(uv);
                    colors.Add(levelColor); // Add vertex color
                }
            }

            // Create two triangles for the patch quad (facing up)
            triangles.AddRange(new int[] { 
                vertexIndices[0], 
                vertexIndices[2], 
                vertexIndices[1] 
            });
            triangles.AddRange(new int[] { 
                vertexIndices[1], 
                vertexIndices[2], 
                vertexIndices[3] 
            });
            
            patchesCreated++;
            totalPixelsCovered += patchPixels;
        }

        if (showDebugInfo)
        {
            float optimizationRatio = solidPixels.Count > 0 ? (float)patchesCreated / solidPixels.Count : 0f;
            Debug.Log($"Top surface optimized at height {imageOffset.y}: {patchesCreated} patches covering {totalPixelsCovered} pixels (was {solidPixels.Count} individual quads)");
            Debug.Log($"Optimization ratio: {optimizationRatio:F3} ({(1f - optimizationRatio) * 100:F1}% reduction in quad count)");
            Debug.Log($"Layer top surface: {vertices.Count} vertices, {triangles.Count / 3} triangles");
        }

        // Adjust triangle indices for the global mesh
        int vertexOffset = allTopSurfaceVertices.Count;
        for (int i = 0; i < triangles.Count; i++)
        {
            triangles[i] += vertexOffset;
        }

        // Add to the combined top surface data
        allTopSurfaceVertices.AddRange(vertices);
        allTopSurfaceTriangles.AddRange(triangles);
        allTopSurfaceUvs.AddRange(uvs);
        allTopSurfaceColors.AddRange(colors);
    }

    Vector2Int FindLargestRectangularPatch(HashSet<Vector2Int> solidPixels, HashSet<Vector2Int> processedPixels, Vector2Int start, int imageWidth, int imageHeight)
    {
        // Find the largest rectangular patch starting from 'start' position
        int maxX = start.x;
        int maxY = start.y;
        
        // First, extend as far right as possible
        while (maxX + 1 < imageWidth && solidPixels.Contains(new Vector2Int(maxX + 1, start.y)) && !processedPixels.Contains(new Vector2Int(maxX + 1, start.y)))
        {
            maxX++;
        }
        
        // Then, try to extend down while maintaining the width
        bool canExtendDown = true;
        while (canExtendDown && maxY + 1 < imageHeight)
        {
            // Check if entire row below is solid and unprocessed
            for (int x = start.x; x <= maxX; x++)
            {
                Vector2Int checkPixel = new Vector2Int(x, maxY + 1);
                if (!solidPixels.Contains(checkPixel) || processedPixels.Contains(checkPixel))
                {
                    canExtendDown = false;
                    break;
                }
            }
            
            if (canExtendDown)
            {
                maxY++;
            }
        }
        
        // Mark all pixels in this patch as processed
        for (int y = start.y; y <= maxY; y++)
        {
            for (int x = start.x; x <= maxX; x++)
            {
                processedPixels.Add(new Vector2Int(x, y));
            }
        }
        
        return new Vector2Int(maxX, maxY);
    }

    void GenerateCombinedTopSurface(List<Vector3> topSurfaceVertices, List<int> topSurfaceTriangles, List<Vector2> topSurfaceUvs, List<Color> topSurfaceColors)
    {
        if (topSurfaceVertices.Count == 0)
        {
            if (showDebugInfo)
                Debug.Log("No top surface data found for any images");
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"Creating combined top surface with {topSurfaceVertices.Count} vertices, {topSurfaceTriangles.Count / 3} triangles");
        }

        // Create the combined top surface mesh
        Mesh combinedTopSurfaceMesh = new Mesh();
        combinedTopSurfaceMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedTopSurfaceMesh.vertices = topSurfaceVertices.ToArray();
        combinedTopSurfaceMesh.triangles = topSurfaceTriangles.ToArray();
        combinedTopSurfaceMesh.uv = topSurfaceUvs.ToArray();
        combinedTopSurfaceMesh.colors = topSurfaceColors.ToArray();
        combinedTopSurfaceMesh.RecalculateNormals();
        combinedTopSurfaceMesh.RecalculateBounds();
        combinedTopSurfaceMesh.name = $"MultiImageTopSurface_{ncDataContainer.levelImages.Count}Images";

        // Create a new GameObject for the combined top surface
        GameObject topSurfaceObj;
        if (topSurfacePrefab != null)
        {
            topSurfaceObj = Instantiate(topSurfacePrefab, transform);
        }
        else
        {
            topSurfaceObj = new GameObject(gameObject.name + "_TopSurface");
            topSurfaceObj.transform.SetParent(transform);
            topSurfaceObj.transform.localPosition = Vector3.zero;
            topSurfaceObj.transform.localRotation = Quaternion.identity;
            topSurfaceObj.transform.localScale = Vector3.one;
            
            topSurfaceObj.AddComponent<MeshFilter>();
            topSurfaceObj.AddComponent<MeshRenderer>();
            
            // Copy material from the boundary walls
            MeshRenderer wallRenderer = GetComponent<MeshRenderer>();
            MeshRenderer topRenderer = topSurfaceObj.GetComponent<MeshRenderer>();
            if (wallRenderer != null && wallRenderer.material != null)
            {
                topRenderer.material = wallRenderer.material;
            }
        }
        
        // Assign the mesh
        topSurfaceObj.GetComponent<MeshFilter>().mesh = combinedTopSurfaceMesh;

        if (showDebugInfo)
        {
            Debug.Log($"Combined top surface created: {topSurfaceVertices.Count} vertices, {topSurfaceTriangles.Count / 3} triangles covering {ncDataContainer.levelImages.Count} images");
        }
    }

    public enum ColoringMode
    {
        Alternating,
        MultipleLevelColors,
        HeightGradient
    }
}


