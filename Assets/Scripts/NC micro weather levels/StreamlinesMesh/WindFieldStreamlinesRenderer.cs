using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindFieldStreamlinesRenderer : MonoBehaviour
{
    [Header("Line Renderer Mesh")]
    public Material lineMaterial;
    public Texture2D lineTexture;
    [Header("Line Width (Set in Material)")]
    [Tooltip("Line width is controlled by the shader material _LineWidth property")]
    public float lineWidth = 0.1f;
    public bool useWorldSpace = true;
    
    [Header("Path Source")]
    public WindFieldStreamlinesCalculator streamlinesCalculator;
    
    [Header("Texture Animation")]
    public float textureAnimationSpeed = 1.0f;
    public bool enableTextureAnimation = true;
    
    [Header("Flow Effect")]
    public float flowScale = 3.0f;
    public float flowStrength = 0.8f;
    public bool enableFlowAnimation = true;
    
    [Header("Visualization Trimming")]
    [Range(0, 1)]
    public float alphaCorrection = 0.75f;
    [Range(0, 1)]
    public float maxAltitude = 1.0f;
    [Range(0, 1)]
    public float minAltitude = 0.0f;
    
    [Header("Spatial Bounds")]
    public Vector3 worldBoundsMin = Vector3.zero;
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
    public bool enableSpeedTrim = false;
    [Range(0, 1)]
    public float speedTrimRange = 0.85f;
    [Range(0.01f, 0.5f)]
    [Tooltip("Width of the speed trim window (larger values = softer transition)")]
    public float speedTrimWidth = 0.1f;
    
    // Single mesh components (like WindFieldMeshNc)
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh lineMesh;
    
    // Current streamlines data
    private List<List<Vector3>> currentStreamlines = new List<List<Vector3>>();
    
    void Start()
    {
        SetupLineMesh();
        
        // Subscribe to streamlines updates if calculator is assigned
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.OnStreamlinesUpdated += UpdateStreamlines;
            
            // If calculator already has streamlines, use them
            if (streamlinesCalculator.allStreamlineWorldCoords.Count > 0)
            {
                UpdateStreamlines(streamlinesCalculator.allStreamlineWorldCoords);
            }

            // Get world bounds from calculator's data container
            if (streamlinesCalculator.dataContainer != null)
            {
                worldBoundsMin = streamlinesCalculator.dataContainer.gridMin;
                worldBoundsMax = streamlinesCalculator.dataContainer.gridMax;
                Debug.Log($"Using grid bounds: {worldBoundsMin} to {worldBoundsMax}");
            }
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (streamlinesCalculator != null)
        {
            streamlinesCalculator.OnStreamlinesUpdated -= UpdateStreamlines;
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
        
        // Prepare lists for combined mesh (like WindFieldMeshNc)
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>(); // Store line direction in normal
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector2> uv2s = new List<Vector2>(); // For magnitude normalization
        List<Vector2> uv3s = new List<Vector2>(); // For MSL normalization
        List<Color> colors = new List<Color>();
        
        // Generate mesh data for each streamline
        for (int streamlineIndex = 0; streamlineIndex < currentStreamlines.Count; streamlineIndex++)
        {
            List<Vector3> streamline = currentStreamlines[streamlineIndex];
            if (streamline.Count < 2) continue;
            
            // Get pre-calculated normalized data from calculator
            List<float> normalizedMagnitudes = new List<float>();
            List<float> normalizedMsl = new List<float>();
            float averageMagnitudeNormalization = 0f;
            float lengthMultiplier = 1f;
            float randomTextureOffset = 0f;
            
            if (streamlinesCalculator != null && 
                streamlineIndex < streamlinesCalculator.allNormalizedMagnitudes.Count &&
                streamlineIndex < streamlinesCalculator.allNormalizedMsl.Count &&
                streamlineIndex < streamlinesCalculator.allAverageMagnitudeNormalizations.Count &&
                streamlineIndex < streamlinesCalculator.allStreamlineLengthMultipliers.Count &&
                streamlineIndex < streamlinesCalculator.allRandomTextureOffsets.Count)
            {
                normalizedMagnitudes = streamlinesCalculator.allNormalizedMagnitudes[streamlineIndex];
                normalizedMsl = streamlinesCalculator.allNormalizedMsl[streamlineIndex];
                averageMagnitudeNormalization = streamlinesCalculator.allAverageMagnitudeNormalizations[streamlineIndex];
                lengthMultiplier = streamlinesCalculator.allStreamlineLengthMultipliers[streamlineIndex];
                randomTextureOffset = streamlinesCalculator.allRandomTextureOffsets[streamlineIndex];
            }
            
            // Calculate combined animation speed: magnitude * length multiplier
            float combinedAnimationSpeed = averageMagnitudeNormalization * lengthMultiplier;
            
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
                
                // Calculate UV coordinates based on distance along streamline
                float segmentU1 = cumulativeDistances[i] / totalLength;
                float segmentU2 = cumulativeDistances[i + 1] / totalLength;
                
                // Create quad vertices - shader will handle billboard positioning
                int baseIndex = vertices.Count;
                
                // Bottom left vertex (point1, side 0)
                vertices.Add(point1);
                normals.Add(direction1);
                uvs.Add(new Vector2(segmentU1, 0)); // UV.y = 0 for billboard positioning
                uv2s.Add(new Vector2(magNorm1, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
                uv3s.Add(new Vector2(mslNorm1, randomTextureOffset)); // MSL normalization
                colors.Add(Color.white);
                
                // Top left vertex (point1, side 1)
                vertices.Add(point1);
                normals.Add(direction1);
                uvs.Add(new Vector2(segmentU1, 1)); // UV.y = 1 for billboard positioning
                uv2s.Add(new Vector2(magNorm1, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
                uv3s.Add(new Vector2(mslNorm1, randomTextureOffset)); // Same MSL for both sides
                colors.Add(Color.white);
                
                // Top right vertex (point2, side 1)
                vertices.Add(point2);
                normals.Add(direction2);
                uvs.Add(new Vector2(segmentU2, 1)); // UV.y = 1 for billboard positioning
                uv2s.Add(new Vector2(magNorm2, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
                uv3s.Add(new Vector2(mslNorm2, randomTextureOffset)); // MSL for second point
                colors.Add(Color.white);
                
                // Bottom right vertex (point2, side 0)
                vertices.Add(point2);
                normals.Add(direction2);
                uvs.Add(new Vector2(segmentU2, 0)); // UV.y = 0 for billboard positioning
                uv2s.Add(new Vector2(magNorm2, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
                uv3s.Add(new Vector2(mslNorm2, randomTextureOffset)); // MSL for second point
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
            lineMesh.SetColors(colors);
            
            // Recalculate bounds
            lineMesh.RecalculateBounds();
        }
        
        Debug.Log($"Single mesh generated: {vertices.Count} vertices, {triangles.Count/3} triangles");
        
        // Update material properties
        UpdateMaterialProperties();
    }
    
    void UpdateMaterialProperties()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            Material mat = meshRenderer.material;
            
            // Update visualization trimming parameters
            mat.SetFloat("_AlfaCorrection", alphaCorrection);
            mat.SetFloat("_MaxAltitude", maxAltitude);
            mat.SetFloat("_MinAltitude", minAltitude);
            mat.SetFloat("_BoundsLeft", boundsLeft);
            mat.SetFloat("_BoundsRight", boundsRight);
            mat.SetFloat("_BoundsFront", boundsFront);
            mat.SetFloat("_BoundsBack", boundsBack);
            mat.SetFloat("_EnableSpeedTrim", enableSpeedTrim ? 1.0f : 0.0f);
            mat.SetFloat("_SpeedTrimRange", speedTrimRange);
            mat.SetFloat("_SpeedTrimWidth", speedTrimWidth);
            mat.SetVector("_WorldBoundsMin", worldBoundsMin);
            mat.SetVector("_WorldBoundsMax", worldBoundsMax);
            
            // Set line width if the material supports it
            if (mat.HasProperty("_LineWidth"))
            {
                mat.SetFloat("_LineWidth", lineWidth);
            }
            
            // Set texture if provided and material supports it
            if (mat.HasProperty("_FlowTexture") && lineTexture != null)
            {
                mat.SetTexture("_FlowTexture", lineTexture);
            }
            
            // Set texture animation speed
            if (mat.HasProperty("_TextureAnimationSpeed"))
            {
                float animSpeed = enableTextureAnimation ? textureAnimationSpeed : 0f;
                mat.SetFloat("_TextureAnimationSpeed", animSpeed);
            }
            
            // Set flow effect properties
            if (mat.HasProperty("_FlowScale"))
            {
                mat.SetFloat("_FlowScale", flowScale);
            }
            
            if (mat.HasProperty("_FlowStrength"))
            {
                mat.SetFloat("_FlowStrength", flowStrength);
            }
            
            // Set animation enable/disable
            if (mat.HasProperty("_EnableAnimation"))
            {
                float enableAnim = (enableTextureAnimation && enableFlowAnimation) ? 1.0f : 0.0f;
                mat.SetFloat("_EnableAnimation", enableAnim);
            }
        }
    }
    
    // Public methods for external control
    public void SetLineWidth(float width)
    {
        lineWidth = width;
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
        textureAnimationSpeed = speed;
        UpdateMaterialProperties();
    }
    
    public void SetTextureAnimationEnabled(bool enabled)
    {
        enableTextureAnimation = enabled;
        UpdateMaterialProperties();
    }
    
    public void SetFlowScale(float scale)
    {
        flowScale = scale;
        UpdateMaterialProperties();
    }
    
    public void SetFlowStrength(float strength)
    {
        flowStrength = strength;
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

    public void SetSpeedTrim(bool enable, float range, float width)
    {
        enableSpeedTrim = enable;
        speedTrimRange = Mathf.Clamp01(range);
        speedTrimWidth = Mathf.Clamp(width, 0.01f, 0.5f);
        UpdateMaterialProperties();
    }

    public void SetAlphaCorrection(float alpha)
    {
        alphaCorrection = Mathf.Clamp01(alpha);
        UpdateMaterialProperties();
    }

    void Update()
    {
        // Update material properties every frame to support runtime changes from inspector
        UpdateMaterialProperties();
    }
} 