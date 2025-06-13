using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindFieldStreamlinesRenderer : MonoBehaviour
{
    private const string PREFS_PREFIX = "WindFieldStreamlines_";

    [Header("Line Width (Set in Material)")]
    [Tooltip("Line width is controlled by the shader material _LineWidth property")]
    [Range(0.1f, 10.0f)]
    public float streamlinesWidth = 0.1f;
    
    [Header("Wind Flow Animation")]
    public bool enableFlowAnimation = true;
    [Range(0.0f, 10.0f)]
    public float textureAnimationSpeed = 1.0f;
    public bool enableTextureAnimation = true;
    [Range(0.01f, 2.0f)]
    public float flowTiling = 1.0f;
    
    [Header("Terrain Toggle")]
    [Tooltip("True = Show Terrain1, False = Show Terrain2")]
    public bool toggleTerrain = true;
    public GameObject terrain1;
    public GameObject terrain2;
    
    // Private field to track previous terrain toggle state
    private bool previousToggleTerrain = true;
    
    // Private fields to track previous preference checkbox states
    private bool previousSaveToPreferences = false;
    private bool previousResetPreferences = false;
    
    [Header("Internal References")]
    [System.NonSerialized]
    public bool useWorldSpace = true;
    
    // Path Source - now automatically found as component
    [System.NonSerialized]
    private WindFieldStreamlinesCalculator streamlinesCalculator;
    
    
   
    
    // Hidden streamline length info - still accessible via code but not in inspector
    [System.NonSerialized]
    private float minStreamlineLength = 0f;
    [System.NonSerialized]
    private float maxStreamlineLength = 0f;
    
    [Header("Visualization Param Trimming")]
    [Range(0, 1)]
    public float transparency = 0.75f;
    [Range(0, 1)]
    public float maxAltitude = 1.0f;
    [Range(0, 1)]
    public float minAltitude = 0.0f;
    [Range(0, 1)]
    [Tooltip("Minimum Lowest Altitude - hide streamlines whose lowest point is above this threshold")]
    public float minLowestAltitude = 0.0f;
   
    [Header("Spatial Bounds")]
     [System.NonSerialized]
    public Vector3 worldBoundsMin = Vector3.zero;
    [System.NonSerialized]
    public Vector3 worldBoundsMax = Vector3.one;
    [Range(0, 1)]   
    public float boundsLeft = 0.0f;
    [Range(0, 1)]
    public float boundsRight = 1.0f;
    [Range(0, 1)]
    public float boundsFront = 0.0f;
    [Range(0, 1)]
    public float boundsBack = 1.0f;

    [Header("Speed Trimming")]
    [Range(0, 1)]
    [Tooltip("Speed Lower Trim - hide streamlines with speed below this threshold (0=show all low speeds)")]
    public float speedTrimLower = 0.0f;
    [Range(0, 1)]
    [Tooltip("Speed Upper Trim - hide streamlines with speed above this threshold (1=show all high speeds)")]
    public float speedTrimUpper = 1.0f;
    
    [Header("Flow Direction Change Trimming")]
    [Range(0, 0.15f)]
    [Tooltip("Flow Direction Change Threshold (0=show all, 1=show only most curved sections)")]
    public float flowDirectionGradientThreshold = 0.0f;
    

    [Header("Global Color Mapping")]
     [Tooltip("Use global magnitude range instead of data-specific range for color mapping")]
    public bool useGlobalMagnitudeRange = false;
    [Tooltip("Global minimum wind magnitude for color mapping (values below this will be clamped to 0)")]
    public float globalMinWindMagnitude = 0f;
    [Tooltip("Global maximum wind magnitude for color mapping (values above this will be clamped to 1)")]
    public float globalMaxWindMagnitude = 20f; // Default to 20 m/s, adjust as needed
   

    [Header("Gradient Colors")]
    [Tooltip("Gradient color for lowest wind speeds (0-20% range)")]
    public Color gradientColor0 = new Color(0.0f, 0.0f, 1.0f, 1.0f); // Blue
    [Tooltip("Gradient color for low wind speeds (20-40% range)")]
    public Color gradientColor1 = new Color(0.0f, 1.0f, 1.0f, 1.0f); // Cyan
    [Tooltip("Gradient color for medium wind speeds (40-60% range)")]
    public Color gradientColor2 = new Color(0.0f, 1.0f, 0.0f, 1.0f); // Green
    [Tooltip("Gradient color for high wind speeds (60-80% range)")]
    public Color gradientColor3 = new Color(1.0f, 1.0f, 0.0f, 1.0f); // Yellow
    [Tooltip("Gradient color for very high wind speeds (80-100% range)")]
    public Color gradientColor4 = new Color(1.0f, 0.5f, 0.0f, 1.0f); // Orange
    [Tooltip("Gradient color for maximum wind speeds (100% range)")]
    public Color gradientColor5 = new Color(1.0f, 0.0f, 0.0f, 1.0f); // Red

    [Header("Solid Color")]
    [Tooltip("Solid color to blend with gradient colors")]
    public Color solidColor = new Color(1.0f, 1.0f, 1.0f, 1.0f); // White
    [Range(0, 1)]
    [Tooltip("Blend between gradient colors (0) and solid color (1)")]
    public float solidColorBlend = 0.0f;

    [Header("Material Settings")]
    public Material lineMaterial;   
    public Texture2D lineTexture;
    
    [Header("Compass")]
    public CompassMarkers compassMarkers; // Reference to compass markers component
    // Single mesh components (like WindFieldMeshNc)
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh lineMesh;
    
    // Current streamlines data
    private List<List<Vector3>> currentStreamlines = new List<List<Vector3>>();
    
    [Header("Preferences")]
    [Tooltip("Set to true to save current settings to preferences")]
    public bool saveToPreferences = false;
    [Tooltip("Set to true to reset all settings to Unity inspector defaults and clear saved preferences")]
    public bool resetPreferences = false;
    
    
    void Awake()
    {
        // Automatically get the streamlines calculator component from the same GameObject
        streamlinesCalculator = GetComponent<WindFieldStreamlinesCalculator>();
        if (streamlinesCalculator == null)
        {
            Debug.LogWarning($"WindFieldStreamlinesCalculator component not found on {gameObject.name}. Please add it to the same GameObject.");
        }
    }
    
    void Start()
    {
        // Save initial Unity inspector values with index 0
        SaveToPreferences(0);
        
        LoadFromPreferences();
        SetupLineMesh();
        
        // Apply initial terrain toggle state
        ApplyTerrainToggle();
        previousToggleTerrain = toggleTerrain;
        
        // Initialize previous preference states
        previousSaveToPreferences = saveToPreferences;
        previousResetPreferences = resetPreferences;
        
        // Simple check - if calculator and data are ready, proceed immediately
        CheckAndSetup();
    }
    
    private bool isSetupComplete = false;
    
    void CheckAndSetup()
    {
        if (!isSetupComplete && streamlinesCalculator != null && streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            streamlinesCalculator.OnStreamlinesUpdated += UpdateStreamlines;
            
            // Get world bounds from calculator's data container
            worldBoundsMin = streamlinesCalculator.dataContainer.gridMin;
            worldBoundsMax = streamlinesCalculator.dataContainer.gridMax;
            Debug.Log($"Using grid bounds: {worldBoundsMin} to {worldBoundsMax}");
            
            // Create compass markers using the separate component
            if (compassMarkers != null)
            {
                compassMarkers.CreateMarkers(worldBoundsMin.x, worldBoundsMax.x, worldBoundsMin.z, worldBoundsMax.z, worldBoundsMin.y);
            }
            
            // If calculator already has streamlines, use them
            if (streamlinesCalculator.allStreamlineWorldCoords.Count > 0)
            {
                UpdateStreamlines(streamlinesCalculator.allStreamlineWorldCoords);
            }
            
            isSetupComplete = true;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.OnStreamlinesUpdated -= UpdateStreamlines;
            if (saveToPreferences)
            {
                SaveToPreferences();
            }
        }
    }
    
    public void SetStreamlinesCalculator(WindFieldStreamlinesCalculator calculator)
    {
        // Unsubscribe from old calculator
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.OnStreamlinesUpdated -= UpdateStreamlines;
        }
        
        // Subscribe to new calculator
        streamlinesCalculator = calculator;
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.OnStreamlinesUpdated += UpdateStreamlines;
            
            // Update with current streamlines if available
            if (streamlinesCalculator.allStreamlineWorldCoords.Count > 0)
            {
                UpdateStreamlines(streamlinesCalculator.allStreamlineWorldCoords);
            }
        }
    }
    
    public void UpdateStreamlines(List<List<Vector3>> streamlines)
    {
        currentStreamlines.Clear();
        currentStreamlines.AddRange(streamlines);
        
        // Use coroutine for mesh generation (like WindFieldMeshNc)
        StartCoroutine(GenerateStreamlinesMesh());
    }
    
    void SetupLineMesh()
    {
        // Get or create mesh components (like WindFieldMeshNc)
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        // Create the mesh
        lineMesh = new Mesh();
        lineMesh.name = "StreamlinesLineMesh";
        meshFilter.mesh = lineMesh;
        
        // Setup material
        if (lineMaterial != null)
        {
            meshRenderer.material = lineMaterial;
        }
        else
        {
            // Try to find the WindStreamlineTexture shader
            Shader windPathShader = Shader.Find("Custom/WindStreamlineTexture");
            if (windPathShader != null)
            {
                meshRenderer.material = new Material(windPathShader);
            }
            else
            {
                // Fallback to unlit material
                meshRenderer.material = new Material(Shader.Find("Unlit/Color"));
            }
        }
        
        // Set initial material properties
        UpdateMaterialProperties();
    }
    
    IEnumerator GenerateStreamlinesMesh()
    {
        if (currentStreamlines.Count == 0)
        {
            // Clear mesh if no streamlines
            if (lineMesh != null)
            {
                lineMesh.Clear();
            }
            yield break;
        }
        
        // Calculate total vertices needed
        int totalVertices = 0;
        foreach (var streamline in currentStreamlines)
        {
            if (streamline.Count >= 2)
            {
                totalVertices += (streamline.Count - 1) * 4; // 4 vertices per segment
            }
        }
        
        Debug.Log($"Generating single mesh for {currentStreamlines.Count} streamlines with {totalVertices} total vertices");
        
        // Get min/max streamline lengths from calculator
        if (streamlinesCalculator != null)
        {
            minStreamlineLength = streamlinesCalculator.minStreamlineLength;
            maxStreamlineLength = streamlinesCalculator.maxStreamlineLength;
        }
        else
        {
            minStreamlineLength = 0f;
            maxStreamlineLength = 1f; // Avoid division by zero
        }
        
        Debug.Log($"Using streamline lengths from calculator - Min: {minStreamlineLength:F2}, Max: {maxStreamlineLength:F2}");
        
        // Prepare lists for combined mesh (like WindFieldMeshNc)
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>(); // Store line direction in normal
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector2> uv2s = new List<Vector2>(); // For magnitude normalization
        List<Vector2> uv3s = new List<Vector2>(); // For MSL normalization
        List<Vector2> uv4s = new List<Vector2>(); // For direction changes
        List<Vector2> uv5s = new List<Vector2>(); // UV5 for random texture offset
        List<Color> colors = new List<Color>();
        
        // Generate mesh data for each streamline
        for (int streamlineIndex = 0; streamlineIndex < currentStreamlines.Count; streamlineIndex++)
        {
            List<Vector3> streamline = currentStreamlines[streamlineIndex];
            if (streamline.Count < 2) continue;
            
            // Get pre-calculated normalized data from calculator
            List<float> normalizedMagnitudes = new List<float>();
            List<float> normalizedMsl = new List<float>();
            List<float> directionChanges = new List<float>();
            float averageMagnitudeNormalization = 0f;
            float lengthMultiplier = 1f;
            float randomTextureOffset = 0f;
            float lowestAltitude = 0f;
            
            if (streamlinesCalculator != null && 
                streamlineIndex < streamlinesCalculator.allNormalizedMagnitudes.Count &&
                streamlineIndex < streamlinesCalculator.allNormalizedMsl.Count &&
                streamlineIndex < streamlinesCalculator.allDirectionChanges.Count &&
                streamlineIndex < streamlinesCalculator.allAverageMagnitudeNormalizations.Count &&
                streamlineIndex < streamlinesCalculator.allStreamlineLengthMultipliers.Count &&
                streamlineIndex < streamlinesCalculator.allRandomTextureOffsets.Count &&
                streamlineIndex < streamlinesCalculator.allLowestAltitudes.Count)
            {
                normalizedMagnitudes = streamlinesCalculator.allNormalizedMagnitudes[streamlineIndex];
                normalizedMsl = streamlinesCalculator.allNormalizedMsl[streamlineIndex];
                directionChanges = streamlinesCalculator.allDirectionChanges[streamlineIndex];
                averageMagnitudeNormalization = streamlinesCalculator.allAverageMagnitudeNormalizations[streamlineIndex];
                lengthMultiplier = streamlinesCalculator.allStreamlineLengthMultipliers[streamlineIndex];
                randomTextureOffset = streamlinesCalculator.allRandomTextureOffsets[streamlineIndex];
                lowestAltitude = streamlinesCalculator.allLowestAltitudes[streamlineIndex];
            }
            
            // Normalize lowest altitude to 0-1 range using world bounds
            float normalizedLowestAltitude = 0f;
            if (worldBoundsMax.y > worldBoundsMin.y)
            {
                normalizedLowestAltitude = (lowestAltitude - worldBoundsMin.y) / (worldBoundsMax.y - worldBoundsMin.y);
                normalizedLowestAltitude = Mathf.Clamp01(normalizedLowestAltitude);
            }
            
            // Pre-calculate smoothed directions for this streamline
            List<Vector3> smoothedDirections = new List<Vector3>();
            
            // Calculate total streamline length and cumulative distances
            float totalLength = 0f;
            List<float> cumulativeDistances = new List<float>();
            cumulativeDistances.Add(0f); // First point starts at 0
            
            for (int i = 0; i < streamline.Count - 1; i++)
            {
                float segmentLength = Vector3.Distance(streamline[i], streamline[i + 1]);
                totalLength += segmentLength;
                cumulativeDistances.Add(totalLength);
            }
            
            // Calculate normalized average wind speed (0-1 based on average magnitude for this streamline)
            float normalizedAverageWindSpeed = averageMagnitudeNormalization; // This is already 0-1 normalized
            
            // Calculate directions (same as before)
            for (int i = 0; i < streamline.Count; i++)
            {
                Vector3 direction;
                if (i == 0)
                {
                    direction = (streamline[1] - streamline[0]).normalized;
                }
                else if (i == streamline.Count - 1)
                {
                    direction = (streamline[i] - streamline[i - 1]).normalized;
                }
                else
                {
                    direction = ((streamline[i + 1] - streamline[i]).normalized +
                               (streamline[i] - streamline[i - 1]).normalized).normalized;
                }
                smoothedDirections.Add(direction);
            }
            
            // Create quads between each pair of points
            for (int i = 0; i < streamline.Count - 1; i++)
            {
                Vector3 point1 = streamline[i];
                Vector3 point2 = streamline[i + 1];
                
                // Use smoothed directions instead of segment direction
                Vector3 direction1 = smoothedDirections[i];
                Vector3 direction2 = smoothedDirections[i + 1];
                
                // Get pre-calculated normalized data
                float magNorm1 = 0f, magNorm2 = 0f;
                float mslNorm1 = 0f, mslNorm2 = 0f;
                float dirChange1 = 0f, dirChange2 = 0f;
                
                if (i < normalizedMagnitudes.Count)
                {
                    magNorm1 = normalizedMagnitudes[i];
                }
                if (i + 1 < normalizedMagnitudes.Count)
                {
                    magNorm2 = normalizedMagnitudes[i + 1];
                }
                
                if (i < normalizedMsl.Count)
                {
                    mslNorm1 = normalizedMsl[i];
                }
                if (i + 1 < normalizedMsl.Count)
                {
                    mslNorm2 = normalizedMsl[i + 1];
                }
                
                if (i < directionChanges.Count)
                {
                    dirChange1 = directionChanges[i];
                }
                if (i + 1 < directionChanges.Count)
                {
                    dirChange2 = directionChanges[i + 1];
                }
                
                // Calculate UV coordinates based on distance along streamline
                float segmentU1 = totalLength > 0 ? cumulativeDistances[i] / totalLength : 0f;
                float segmentU2 = totalLength > 0 ? cumulativeDistances[i + 1] / totalLength : 0f;
                
                // Create quad vertices - shader will handle billboard positioning
                int baseIndex = vertices.Count;
                
                // Bottom left vertex (point1, side 0)
                vertices.Add(point1);
                normals.Add(direction1);
                uvs.Add(new Vector2(segmentU1, 0)); // UV.y = 0 for billboard positioning
                uv2s.Add(new Vector2(magNorm1, normalizedAverageWindSpeed)); // UV2.x = magnitude, UV2.y = normalized average wind speed
                uv3s.Add(new Vector2(mslNorm1, cumulativeDistances[i])); // UV3.x = MSL normalization, UV3.y = actual cumulative distance
                uv4s.Add(new Vector2(dirChange1, normalizedLowestAltitude)); // UV4.x = direction change, UV4.y = normalized lowest altitude
                uv5s.Add(new Vector2(randomTextureOffset, normalizedLowestAltitude)); // UV5 for random texture offset
                colors.Add(Color.white);
                
                // Top left vertex (point1, side 1)
                vertices.Add(point1);
                normals.Add(direction1);
                uvs.Add(new Vector2(segmentU1, 1)); // UV.y = 1 for billboard positioning
                uv2s.Add(new Vector2(magNorm1, normalizedAverageWindSpeed)); // UV2.x = magnitude, UV2.y = normalized average wind speed
                uv3s.Add(new Vector2(mslNorm1, cumulativeDistances[i])); // UV3.x = MSL normalization, UV3.y = actual cumulative distance
                uv4s.Add(new Vector2(dirChange1, normalizedLowestAltitude)); // UV4.x = direction change, UV4.y = normalized lowest altitude
                uv5s.Add(new Vector2(randomTextureOffset, normalizedLowestAltitude)); // UV5 for random texture offset
                colors.Add(Color.white);
                
                // Top right vertex (point2, side 1)
                vertices.Add(point2);
                normals.Add(direction2);
                uvs.Add(new Vector2(segmentU2, 1)); // UV.y = 1 for billboard positioning
                uv2s.Add(new Vector2(magNorm2, normalizedAverageWindSpeed)); // UV2.x = magnitude, UV2.y = normalized average wind speed
                uv3s.Add(new Vector2(mslNorm2, cumulativeDistances[i + 1])); // UV3.x = MSL normalization, UV3.y = actual cumulative distance
                uv4s.Add(new Vector2(dirChange2, normalizedLowestAltitude)); // UV4.x = direction change, UV4.y = normalized lowest altitude
                uv5s.Add(new Vector2(randomTextureOffset, normalizedLowestAltitude)); // UV5 for random texture offset
                colors.Add(Color.white);
                
                // Bottom right vertex (point2, side 0)
                vertices.Add(point2);
                normals.Add(direction2);
                uvs.Add(new Vector2(segmentU2, 0)); // UV.y = 0 for billboard positioning
                uv2s.Add(new Vector2(magNorm2, normalizedAverageWindSpeed)); // UV2.x = magnitude, UV2.y = normalized average wind speed
                uv3s.Add(new Vector2(mslNorm2, cumulativeDistances[i + 1])); // UV3.x = MSL normalization, UV3.y = actual cumulative distance
                uv4s.Add(new Vector2(dirChange2, normalizedLowestAltitude)); // UV4.x = direction change, UV4.y = normalized lowest altitude
                uv5s.Add(new Vector2(randomTextureOffset, normalizedLowestAltitude)); // UV5 for random texture offset
                colors.Add(Color.white);
                
                // Add triangles (two triangles per quad)
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
            
            // Yield every 100 streamlines to prevent frame drops (like WindFieldMeshNc)
            if (streamlineIndex % 100 == 0)
                yield return null;
        }
        
        // Apply mesh data (like WindFieldMeshNc)
        lineMesh.Clear();
        if (vertices.Count > 0)
        {
            // Use 32-bit indices for large meshes (like WindFieldMeshNc)
            lineMesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            
            lineMesh.SetVertices(vertices);
            lineMesh.SetNormals(normals);
            lineMesh.SetTriangles(triangles, 0);
            lineMesh.SetUVs(0, uvs);
            lineMesh.SetUVs(1, uv2s); // UV2 for magnitude normalization
            lineMesh.SetUVs(2, uv3s); // UV3 for MSL normalization
            lineMesh.SetUVs(3, uv4s); // UV4 for direction changes
            lineMesh.SetUVs(4, uv5s); // UV5 for random texture offset
            lineMesh.SetColors(colors);
            
            // Recalculate bounds
            lineMesh.RecalculateBounds();
        }
        
        Debug.Log($"Single mesh generated: {vertices.Count} vertices, {triangles.Count/3} triangles");
        
        // Update material properties
        UpdateMaterialProperties();
    }
    
    public void UpdateMaterialProperties()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            Material mat = meshRenderer.material;
            
            // Update visualization trimming parameters
            mat.SetFloat("_AlfaCorrection", transparency);
            mat.SetFloat("_MaxAltitude", maxAltitude);
            mat.SetFloat("_MinAltitude", minAltitude);
            mat.SetFloat("_MinLowestAltitude", minLowestAltitude);
            mat.SetFloat("_BoundsLeft", boundsLeft);
            mat.SetFloat("_BoundsRight", boundsRight);
            mat.SetFloat("_BoundsFront", boundsFront);
            mat.SetFloat("_BoundsBack", boundsBack);
            mat.SetFloat("_SpeedTrimLower", speedTrimLower);
            mat.SetFloat("_SpeedTrimUpper", speedTrimUpper);
            mat.SetFloat("_FlowDirectionChangeThreshold", flowDirectionGradientThreshold);
            mat.SetVector("_WorldBoundsMin", worldBoundsMin);
            mat.SetVector("_WorldBoundsMax", worldBoundsMax);
            
            // Set line width if the material supports it
            if (mat.HasProperty("_LineWidth"))
            {
                mat.SetFloat("_LineWidth", streamlinesWidth);
            }
            
            // Set texture if provided and material supports it
            if (mat.HasProperty("_FlowTexture") && lineTexture != null)
            {
                mat.SetTexture("_FlowTexture", lineTexture);
            }
            
            // Set texture animation speed
            if (mat.HasProperty("_TextureAnimationSpeed"))
            {
                float animSpeed = enableTextureAnimation ? -textureAnimationSpeed : 0f;
                mat.SetFloat("_TextureAnimationSpeed", animSpeed);
            }
            
            // Set flow tiling property
            if (mat.HasProperty("_FlowTiling"))
            {
                mat.SetFloat("_FlowTiling", flowTiling);
            }
            
            // Set animation enable/disable
            if (mat.HasProperty("_EnableAnimation"))
            {
                float enableAnim = (enableTextureAnimation && enableFlowAnimation) ? 1.0f : 0.0f;
                mat.SetFloat("_EnableAnimation", enableAnim);
            }
            
            // Set global magnitude range for color mapping
            if (mat.HasProperty("_GlobalMinWindMagnitude")) mat.SetFloat("_GlobalMinWindMagnitude", globalMinWindMagnitude);
            if (mat.HasProperty("_GlobalMaxWindMagnitude")) mat.SetFloat("_GlobalMaxWindMagnitude", globalMaxWindMagnitude);
            if (mat.HasProperty("_UseGlobalMagnitudeRange")) mat.SetFloat("_UseGlobalMagnitudeRange", useGlobalMagnitudeRange ? 1.0f : 0.0f);
            
            // Set data magnitude range for shader remapping
            if (streamlinesCalculator != null && streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
            {
                Vector2 dataMinMax = streamlinesCalculator.dataContainer.magMinMax;
                if (mat.HasProperty("_DataMinWindMagnitude")) mat.SetFloat("_DataMinWindMagnitude", dataMinMax.x);
                if (mat.HasProperty("_DataMaxWindMagnitude")) mat.SetFloat("_DataMaxWindMagnitude", dataMinMax.y);
            }
            
            // Set gradient colors
            if (mat.HasProperty("_Color0")) mat.SetColor("_Color0", gradientColor0);
            if (mat.HasProperty("_Color1")) mat.SetColor("_Color1", gradientColor1);
            if (mat.HasProperty("_Color2")) mat.SetColor("_Color2", gradientColor2);
            if (mat.HasProperty("_Color3")) mat.SetColor("_Color3", gradientColor3);
            if (mat.HasProperty("_Color4")) mat.SetColor("_Color4", gradientColor4);
            if (mat.HasProperty("_Color5")) mat.SetColor("_Color5", gradientColor5);
            
            // Set solid color properties
            if (mat.HasProperty("_SolidColor")) mat.SetColor("_SolidColor", solidColor);
            if (mat.HasProperty("_SolidColorBlend")) mat.SetFloat("_SolidColorBlend", solidColorBlend);
        }
    }
    
    // Public methods for external control
    public void SetLineWidth(float width)
    {
        streamlinesWidth = width;
        UpdateMaterialProperties();
    }
    
    public void SetLineMaterial(Material material)
    {
        lineMaterial = material;
        if (meshRenderer != null)
        {
            meshRenderer.material = lineMaterial;
            UpdateMaterialProperties();
        }
    }
    
    public void ClearStreamlines()
    {
        currentStreamlines.Clear();
        if (lineMesh != null)
        {
            lineMesh.Clear();
        }
    }
    
    public bool HasStreamlines()
    {
        return currentStreamlines.Count > 0;
    }
    
    public int GetStreamlineCount()
    {
        return currentStreamlines.Count;
    }
    
    public int GetTotalVertexCount()
    {
        return lineMesh != null ? lineMesh.vertexCount : 0;
    }
    
    // Public method to force mesh update
    public void ForceUpdateMesh()
    {
        StartCoroutine(GenerateStreamlinesMesh());
    }
    
    [ContextMenu("Debug Mesh Info")]
    public void DebugMeshInfo()
    {
        if (lineMesh != null)
        {
            Debug.Log($"Single Mesh Info:");
            Debug.Log($"  Vertices: {lineMesh.vertexCount}");
            Debug.Log($"  Triangles: {lineMesh.triangles.Length / 3}");
            Debug.Log($"  Index Format: {lineMesh.indexFormat}");
            Debug.Log($"  Streamlines: {currentStreamlines.Count}");
        }
    }
    
    public void SetLineTexture(Texture2D texture)
    {
        lineTexture = texture;
        UpdateMaterialProperties();
    }
    
    public void SetTextureAnimationSpeed(float speed)
    {
        // Negative speed to reverse the animation
        textureAnimationSpeed = -speed;
        UpdateMaterialProperties();
    }
    
    public void SetTextureAnimationEnabled(bool enabled)
    {
        enableTextureAnimation = enabled;
        UpdateMaterialProperties();
    }
    
    public void SetFlowTiling(float tiling)
    {
        flowTiling = tiling;
        UpdateMaterialProperties();
    }
    
    public void SetFlowAnimationEnabled(bool enabled)
    {
        enableFlowAnimation = enabled;
        UpdateMaterialProperties();
    }

    // Runtime parameter update methods
    public void SetAltitudeBounds(float min, float max)
    {
        minAltitude = Mathf.Clamp01(min);
        maxAltitude = Mathf.Clamp01(max);
        UpdateMaterialProperties();
    }

    public void SetSpatialBounds(float left, float right, float front, float back)
    {
        boundsLeft = Mathf.Clamp01(left);
        boundsRight = Mathf.Clamp01(right);
        boundsFront = Mathf.Clamp01(front);
        boundsBack = Mathf.Clamp01(back);
        UpdateMaterialProperties();
    }

    public void SetSpeedTrim(float lower, float upper)
    {
        speedTrimLower = Mathf.Clamp01(lower);
        speedTrimUpper = Mathf.Clamp01(upper);
        UpdateMaterialProperties();
    }

    public void SetFlowDirectionChangeTrim(float threshold)
    {
        flowDirectionGradientThreshold = Mathf.Clamp01(threshold);
        UpdateMaterialProperties();
    }

    public void SetAlphaCorrection(float alpha)
    {
        transparency = Mathf.Clamp01(alpha);
        UpdateMaterialProperties();
    }

    public void SetMinLowestAltitude(float minLowest)
    {
        minLowestAltitude = Mathf.Clamp01(minLowest);
        UpdateMaterialProperties();
    }

    // Gradient color setter methods
    public void SetGradientColor0(Color color)
    {
        gradientColor0 = color;
        UpdateMaterialProperties();
    }

    public void SetGradientColor1(Color color)
    {
        gradientColor1 = color;
        UpdateMaterialProperties();
    }

    public void SetGradientColor2(Color color)
    {
        gradientColor2 = color;
        UpdateMaterialProperties();
    }

    public void SetGradientColor3(Color color)
    {
        gradientColor3 = color;
        UpdateMaterialProperties();
    }

    public void SetGradientColor4(Color color)
    {
        gradientColor4 = color;
        UpdateMaterialProperties();
    }

    public void SetGradientColor5(Color color)
    {
        gradientColor5 = color;
        UpdateMaterialProperties();
    }

    public void SetAllGradientColors(Color color0, Color color1, Color color2, Color color3, Color color4, Color color5)
    {
        gradientColor0 = color0;
        gradientColor1 = color1;
        gradientColor2 = color2;
        gradientColor3 = color3;
        gradientColor4 = color4;
        gradientColor5 = color5;
        UpdateMaterialProperties();
    }

    // Solid color setter methods
    public void SetSolidColor(Color color)
    {
        solidColor = color;
        UpdateMaterialProperties();
    }

    public void SetSolidColorBlend(float blend)
    {
        solidColorBlend = Mathf.Clamp01(blend);
        UpdateMaterialProperties();
    }

    // Global magnitude range control methods
    public void SetGlobalMagnitudeRange(float minMagnitude, float maxMagnitude)
    {
        globalMinWindMagnitude = minMagnitude;
        globalMaxWindMagnitude = maxMagnitude;
        UpdateMaterialProperties();
    }

    public void SetGlobalMinWindMagnitude(float minMagnitude)
    {
        globalMinWindMagnitude = minMagnitude;
        UpdateMaterialProperties();
    }

    public void SetGlobalMaxWindMagnitude(float maxMagnitude)
    {
        globalMaxWindMagnitude = maxMagnitude;
        UpdateMaterialProperties();
    }

    public void SetUseGlobalMagnitudeRange(bool useGlobal)
    {
        useGlobalMagnitudeRange = useGlobal;
        UpdateMaterialProperties();
    }

    // Terrain toggle methods
    public void SetActiveTerrain(bool useTerrain1)
    {
        toggleTerrain = useTerrain1;
        ApplyTerrainToggle();
    }

    public void SwitchTerrain()
    {
        toggleTerrain = !toggleTerrain;
        ApplyTerrainToggle();
    }

    public void ShowTerrain1()
    {
        SetActiveTerrain(true);
    }

    public void ShowTerrain2()
    {
        SetActiveTerrain(false);
    }

    private void ApplyTerrainToggle()
    {
        if (terrain1 != null)
        {
            terrain1.SetActive(toggleTerrain);
        }
        
        if (terrain2 != null)
        {
            terrain2.SetActive(!toggleTerrain);
        }
        
        string activeTerrain = toggleTerrain ? "Terrain1" : "Terrain2";
        Debug.Log($"Switched to: {activeTerrain}");
    }

    [ContextMenu("Toggle Terrain")]
    public void ContextMenuToggleTerrain()
    {
        SwitchTerrain();
    }

    [ContextMenu("Set Global Range From Current Data")]
    public void SetGlobalRangeFromCurrentData()
    {
        if (streamlinesCalculator != null && streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            Vector2 dataMinMax = streamlinesCalculator.dataContainer.magMinMax;
            globalMinWindMagnitude = dataMinMax.x;
            globalMaxWindMagnitude = dataMinMax.y;
            UpdateMaterialProperties();
            Debug.Log($"Set global magnitude range from current data: {globalMinWindMagnitude:F2} to {globalMaxWindMagnitude:F2} m/s");
        }
        else
        {
            Debug.LogWarning("Cannot set global range: streamlines calculator or data container not available");
        }
    }

    [ContextMenu("Debug Global Color Mapping")]
    public void DebugGlobalColorMapping()
    {
        Debug.Log($"=== Global Color Mapping Debug ===");
        Debug.Log($"useGlobalMagnitudeRange: {useGlobalMagnitudeRange}");
        Debug.Log($"globalMinWindMagnitude: {globalMinWindMagnitude:F2}");
        Debug.Log($"globalMaxWindMagnitude: {globalMaxWindMagnitude:F2}");
        
        if (meshRenderer != null && meshRenderer.material != null)
        {
            Material mat = meshRenderer.material;
            Debug.Log($"Material: {mat.name}");
            Debug.Log($"Has _GlobalMinWindMagnitude: {mat.HasProperty("_GlobalMinWindMagnitude")}");
            Debug.Log($"Has _GlobalMaxWindMagnitude: {mat.HasProperty("_GlobalMaxWindMagnitude")}");
            Debug.Log($"Has _UseGlobalMagnitudeRange: {mat.HasProperty("_UseGlobalMagnitudeRange")}");
            Debug.Log($"Has _DataMinWindMagnitude: {mat.HasProperty("_DataMinWindMagnitude")}");
            Debug.Log($"Has _DataMaxWindMagnitude: {mat.HasProperty("_DataMaxWindMagnitude")}");
            
            if (mat.HasProperty("_GlobalMinWindMagnitude"))
                Debug.Log($"_GlobalMinWindMagnitude value: {mat.GetFloat("_GlobalMinWindMagnitude"):F2}");
            if (mat.HasProperty("_GlobalMaxWindMagnitude"))
                Debug.Log($"_GlobalMaxWindMagnitude value: {mat.GetFloat("_GlobalMaxWindMagnitude"):F2}");
            if (mat.HasProperty("_UseGlobalMagnitudeRange"))
                Debug.Log($"_UseGlobalMagnitudeRange value: {mat.GetFloat("_UseGlobalMagnitudeRange")}");
            if (mat.HasProperty("_DataMinWindMagnitude"))
                Debug.Log($"_DataMinWindMagnitude value: {mat.GetFloat("_DataMinWindMagnitude"):F2}");
            if (mat.HasProperty("_DataMaxWindMagnitude"))
                Debug.Log($"_DataMaxWindMagnitude value: {mat.GetFloat("_DataMaxWindMagnitude"):F2}");
        }
        else
        {
            Debug.LogWarning("No material found for debugging");
        }
        
        if (streamlinesCalculator != null && streamlinesCalculator.dataContainer != null && streamlinesCalculator.dataContainer.IsLoaded)
        {
            Vector2 dataMinMax = streamlinesCalculator.dataContainer.magMinMax;
            Debug.Log($"Data magnitude range: {dataMinMax.x:F2} to {dataMinMax.y:F2} m/s");
        }
    }

    void Update()
    {
        // Simple check every frame until setup is complete
        CheckAndSetup();
        
        // Check for terrain toggle changes
        if (toggleTerrain != previousToggleTerrain)
        {
            ApplyTerrainToggle();
            previousToggleTerrain = toggleTerrain;
        }
        
        // Handle save preferences - only when checkbox changes from false to true
        if (saveToPreferences && !previousSaveToPreferences)
        {
            SaveToPreferences();
        }
        previousSaveToPreferences = saveToPreferences;
        
        // Handle reset preferences - only when checkbox changes from false to true
        if (resetPreferences && !previousResetPreferences)
        {
            ResetToDefaults();
        }
        previousResetPreferences = resetPreferences;
        
        // Update material properties every frame to support runtime changes from inspector
        UpdateMaterialProperties();
    }
    void OnDisable()
    {
        if (saveToPreferences)
        {
            SaveToPreferences();
        }
    }
    private void SaveToPreferences()
    {
        SaveToPreferences(1); // Default to index 1 for regular saves
    }
    
    private void SaveToPreferences(int index)
    {
        string suffix = "_" + index;
        
        // Save all exposed parameters
        PlayerPrefs.SetInt(PREFS_PREFIX + "enableFlowAnimation" + suffix, enableFlowAnimation ? 1 : 0);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "streamlinesWidth" + suffix, streamlinesWidth);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "textureAnimationSpeed" + suffix, textureAnimationSpeed);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "transparency" + suffix, transparency);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "flowTiling" + suffix, flowTiling);
        
        // Save terrain toggle
        PlayerPrefs.SetInt(PREFS_PREFIX + "toggleTerrain" + suffix, toggleTerrain ? 1 : 0);
        
        // Save preference checkboxes
        PlayerPrefs.SetInt(PREFS_PREFIX + "saveToPreferences" + suffix, saveToPreferences ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "resetPreferences" + suffix, resetPreferences ? 1 : 0);
        
        PlayerPrefs.SetFloat(PREFS_PREFIX + "maxAltitude" + suffix, maxAltitude);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "minAltitude" + suffix, minAltitude);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "minLowestAltitude" + suffix, minLowestAltitude);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "boundsLeft" + suffix, boundsLeft);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "boundsRight" + suffix, boundsRight);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "boundsFront" + suffix, boundsFront);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "boundsBack" + suffix, boundsBack);
        
        PlayerPrefs.SetFloat(PREFS_PREFIX + "speedTrimLower" + suffix, speedTrimLower);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "speedTrimUpper" + suffix, speedTrimUpper);
        
        PlayerPrefs.SetFloat(PREFS_PREFIX + "flowDirectionGradientThreshold" + suffix, flowDirectionGradientThreshold);
        
        PlayerPrefs.SetInt(PREFS_PREFIX + "useGlobalMagnitudeRange" + suffix, useGlobalMagnitudeRange ? 1 : 0);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "globalMinWindMagnitude" + suffix, globalMinWindMagnitude);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "globalMaxWindMagnitude" + suffix, globalMaxWindMagnitude);
        
        // Save gradient colors
        SaveColor(PREFS_PREFIX + "gradientColor0" + suffix, gradientColor0);
        SaveColor(PREFS_PREFIX + "gradientColor1" + suffix, gradientColor1);
        SaveColor(PREFS_PREFIX + "gradientColor2" + suffix, gradientColor2);
        SaveColor(PREFS_PREFIX + "gradientColor3" + suffix, gradientColor3);
        SaveColor(PREFS_PREFIX + "gradientColor4" + suffix, gradientColor4);
        SaveColor(PREFS_PREFIX + "gradientColor5" + suffix, gradientColor5);
        
        // Save solid color properties
        SaveColor(PREFS_PREFIX + "solidColor" + suffix, solidColor);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "solidColorBlend" + suffix, solidColorBlend);
        
        PlayerPrefs.Save();
        
        if (index == 0)
        {
            Debug.Log("WindFieldStreamlinesRenderer initial Unity inspector values saved!");
        }
        else
        {
            Debug.Log("WindFieldStreamlinesRenderer settings saved to preferences!");
        }
    }
    
    [ContextMenu("Load from Preferences")]
    public void LoadFromPreferences()
    {
        LoadFromPreferences(1); // Default to index 1 for regular loads
    }
    
    public void LoadFromPreferences(int index)
    {
        string suffix = "_" + index;
        
        // Load all exposed parameters
        enableFlowAnimation = PlayerPrefs.GetInt(PREFS_PREFIX + "enableFlowAnimation" + suffix, enableFlowAnimation ? 1 : 0) == 1;
        streamlinesWidth = PlayerPrefs.GetFloat(PREFS_PREFIX + "streamlinesWidth" + suffix, streamlinesWidth);
        textureAnimationSpeed = PlayerPrefs.GetFloat(PREFS_PREFIX + "textureAnimationSpeed" + suffix, textureAnimationSpeed);
        transparency = PlayerPrefs.GetFloat(PREFS_PREFIX + "transparency" + suffix, transparency);
        flowTiling = PlayerPrefs.GetFloat(PREFS_PREFIX + "flowTiling" + suffix, flowTiling);
        
        // Load terrain toggle
        toggleTerrain = PlayerPrefs.GetInt(PREFS_PREFIX + "toggleTerrain" + suffix, toggleTerrain ? 1 : 0) == 1;
        
        // Load preference checkboxes
        saveToPreferences = PlayerPrefs.GetInt(PREFS_PREFIX + "saveToPreferences" + suffix, saveToPreferences ? 1 : 0) == 1;
        resetPreferences = PlayerPrefs.GetInt(PREFS_PREFIX + "resetPreferences" + suffix, resetPreferences ? 1 : 0) == 1;
        
        maxAltitude = PlayerPrefs.GetFloat(PREFS_PREFIX + "maxAltitude" + suffix, maxAltitude);
        minAltitude = PlayerPrefs.GetFloat(PREFS_PREFIX + "minAltitude" + suffix, minAltitude);
        minLowestAltitude = PlayerPrefs.GetFloat(PREFS_PREFIX + "minLowestAltitude" + suffix, minLowestAltitude);
        boundsLeft = PlayerPrefs.GetFloat(PREFS_PREFIX + "boundsLeft" + suffix, boundsLeft);
        boundsRight = PlayerPrefs.GetFloat(PREFS_PREFIX + "boundsRight" + suffix, boundsRight);
        boundsFront = PlayerPrefs.GetFloat(PREFS_PREFIX + "boundsFront" + suffix, boundsFront);
        boundsBack = PlayerPrefs.GetFloat(PREFS_PREFIX + "boundsBack" + suffix, boundsBack);
        
        speedTrimLower = PlayerPrefs.GetFloat(PREFS_PREFIX + "speedTrimLower" + suffix, speedTrimLower);
        speedTrimUpper = PlayerPrefs.GetFloat(PREFS_PREFIX + "speedTrimUpper" + suffix, speedTrimUpper);
        
        flowDirectionGradientThreshold = PlayerPrefs.GetFloat(PREFS_PREFIX + "flowDirectionGradientThreshold" + suffix, flowDirectionGradientThreshold);
        
        useGlobalMagnitudeRange = PlayerPrefs.GetInt(PREFS_PREFIX + "useGlobalMagnitudeRange" + suffix, useGlobalMagnitudeRange ? 1 : 0) == 1;
        globalMinWindMagnitude = PlayerPrefs.GetFloat(PREFS_PREFIX + "globalMinWindMagnitude" + suffix, globalMinWindMagnitude);
        globalMaxWindMagnitude = PlayerPrefs.GetFloat(PREFS_PREFIX + "globalMaxWindMagnitude" + suffix, globalMaxWindMagnitude);
        
        // Load gradient colors
        gradientColor0 = LoadColor(PREFS_PREFIX + "gradientColor0" + suffix, gradientColor0);
        gradientColor1 = LoadColor(PREFS_PREFIX + "gradientColor1" + suffix, gradientColor1);
        gradientColor2 = LoadColor(PREFS_PREFIX + "gradientColor2" + suffix, gradientColor2);
        gradientColor3 = LoadColor(PREFS_PREFIX + "gradientColor3" + suffix, gradientColor3);
        gradientColor4 = LoadColor(PREFS_PREFIX + "gradientColor4" + suffix, gradientColor4);
        gradientColor5 = LoadColor(PREFS_PREFIX + "gradientColor5" + suffix, gradientColor5);
        
        // Load solid color properties
        solidColor = LoadColor(PREFS_PREFIX + "solidColor" + suffix, solidColor);
        solidColorBlend = PlayerPrefs.GetFloat(PREFS_PREFIX + "solidColorBlend" + suffix, solidColorBlend);
        
        if (index == 0)
        {
            Debug.Log("WindFieldStreamlinesRenderer Unity inspector defaults loaded!");
        }
        else
        {
            Debug.Log("WindFieldStreamlinesRenderer settings loaded from preferences!");
        }
    }
    
    [ContextMenu("Reset to Defaults")]
    public void ResetToDefaults()
    {
        LoadFromPreferences(0); // Load from the initial Unity inspector values (index 0)
        
        // Apply terrain toggle
        ApplyTerrainToggle();
        previousToggleTerrain = toggleTerrain;
        
        // Update material properties
        UpdateMaterialProperties();
        
        Debug.Log("WindFieldStreamlinesRenderer settings reset to Unity inspector defaults!");
    }
    
    private void SaveColor(string key, Color color)
    {
        PlayerPrefs.SetFloat(key + "_r", color.r);
        PlayerPrefs.SetFloat(key + "_g", color.g);
        PlayerPrefs.SetFloat(key + "_b", color.b);
        PlayerPrefs.SetFloat(key + "_a", color.a);
    }
    
    private Color LoadColor(string key, Color defaultColor)
    {
        return new Color(
            PlayerPrefs.GetFloat(key + "_r", defaultColor.r),
            PlayerPrefs.GetFloat(key + "_g", defaultColor.g),
            PlayerPrefs.GetFloat(key + "_b", defaultColor.b),
            PlayerPrefs.GetFloat(key + "_a", defaultColor.a)
        );
    }
} 