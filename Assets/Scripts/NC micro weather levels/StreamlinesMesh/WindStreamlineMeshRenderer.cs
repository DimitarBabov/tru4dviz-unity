using System.Collections.Generic;
using UnityEngine;

public class WindStreamlineMeshRenderer : MonoBehaviour
{
    [Header("Line Renderer Mesh")]
    public Material lineMaterial;
    public Texture2D lineTexture;
    [Header("Line Width (Set in Material)")]
    [Tooltip("Line width is controlled by the shader material _LineWidth property")]
    public float lineWidth = 0.1f;
    public bool useWorldSpace = true;
    
    [Header("Path Source")]
    public WindStreamlineCalculator pathCalculator;
    
    [Header("Texture Animation")]
    public float textureAnimationSpeed = 1.0f;
    public bool enableTextureAnimation = true;
    
    [Header("Flow Effect")]
    public float flowScale = 3.0f;
    public float flowStrength = 0.8f;
    public bool enableFlowAnimation = true;
    
    // Custom mesh components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh lineMesh;
    
    // Current path data
    private List<Vector3> currentPath = new List<Vector3>();
    private List<float> currentNormalizedMagnitudes = new List<float>();
    private List<float> currentNormalizedMsl = new List<float>();
    
    void Start()
    {
        SetupLineMesh();
        
        // Subscribe to path updates if calculator is assigned
        if (pathCalculator != null)
        {
            pathCalculator.OnPathUpdated += UpdatePath;
            
            // If calculator already has a path, use it
            if (pathCalculator.interpolatedPathWorldCoords.Count > 0)
            {
                UpdatePath(pathCalculator.interpolatedPathWorldCoords);
            }
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (pathCalculator != null)
        {
            pathCalculator.OnPathUpdated -= UpdatePath;
        }
    }
    
    public void SetPathCalculator(WindStreamlineCalculator calculator)
    {
        // Unsubscribe from old calculator
        if (pathCalculator != null)
        {
            pathCalculator.OnPathUpdated -= UpdatePath;
        }
        
        // Subscribe to new calculator
        pathCalculator = calculator;
        if (pathCalculator != null)
        {
            pathCalculator.OnPathUpdated += UpdatePath;
            
            // Update with current path if available
            if (pathCalculator.interpolatedPathWorldCoords.Count > 0)
            {
                UpdatePath(pathCalculator.interpolatedPathWorldCoords);
            }
        }
    }
    
    public void UpdatePath(List<Vector3> pathPoints)
    {
        currentPath.Clear();
        currentPath.AddRange(pathPoints);
        
        // Get pre-calculated normalized data from calculator
        currentNormalizedMagnitudes.Clear();
        currentNormalizedMsl.Clear();
        
        if (pathCalculator != null)
        {
            currentNormalizedMagnitudes.AddRange(pathCalculator.normalizedMagnitudes);
            currentNormalizedMsl.AddRange(pathCalculator.normalizedMsl);
        }
        
        UpdateLineMesh();
    }
    
    public void SetPath(List<Vector3> pathPoints)
    {
        currentPath.Clear();
        currentPath.AddRange(pathPoints);
        
        // Clear normalized data since it's not provided
        currentNormalizedMagnitudes.Clear();
        currentNormalizedMsl.Clear();
        
        UpdateLineMesh();
    }
    
    void SetupLineMesh()
    {
        // Get or create mesh components
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
        lineMesh.name = "LineRendererMesh";
        meshFilter.mesh = lineMesh;
        
        // Setup material
        if (lineMaterial != null)
        {
            meshRenderer.material = lineMaterial;
        }
        else
        {
            // Try to find the WindPathMeshRenderer shader
            Shader windPathShader = Shader.Find("Custom/WindPathMeshRenderer");
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
        
        // Set the color and line width
        UpdateMaterialProperties();
    }
    
    void UpdateMaterialProperties()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            // Set line width if the material supports it
            if (meshRenderer.material.HasProperty("_LineWidth"))
            {
                meshRenderer.material.SetFloat("_LineWidth", lineWidth);
            }
            
            // Set texture if provided and material supports it
            if (meshRenderer.material.HasProperty("_MainTex") && lineTexture != null)
            {
                meshRenderer.material.SetTexture("_MainTex", lineTexture);
            }
            
            // Set texture animation speed
            if (meshRenderer.material.HasProperty("_TextureAnimationSpeed"))
            {
                float animSpeed = enableTextureAnimation ? textureAnimationSpeed : 0f;
                meshRenderer.material.SetFloat("_TextureAnimationSpeed", animSpeed);
            }
            
            // Set flow effect properties
            if (meshRenderer.material.HasProperty("_FlowScale"))
            {
                meshRenderer.material.SetFloat("_FlowScale", flowScale);
            }
            
            if (meshRenderer.material.HasProperty("_FlowStrength"))
            {
                meshRenderer.material.SetFloat("_FlowStrength", flowStrength);
            }
            
            // Set animation enable/disable
            if (meshRenderer.material.HasProperty("_EnableAnimation"))
            {
                float enableAnim = (enableTextureAnimation && enableFlowAnimation) ? 1.0f : 0.0f;
                meshRenderer.material.SetFloat("_EnableAnimation", enableAnim);
            }
        }
    }
    
    void UpdateLineMesh()
    {
        if (lineMesh == null || currentPath.Count < 2) 
        {
            if (lineMesh != null)
            {
                lineMesh.Clear();
            }
            return;
        }
        
        // Generate mesh data for shader-based billboard line
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>(); // Store line direction in normal
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector2> uv2s = new List<Vector2>(); // For magnitude normalization
        List<Vector2> uv3s = new List<Vector2>(); // For MSL normalization
        List<Color> colors = new List<Color>();
        
        // Pre-calculate smoothed directions for each point to avoid notches
        List<Vector3> smoothedDirections = new List<Vector3>();
        
        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 direction = Vector3.zero;
            
            if (i == 0)
            {
                // First point: use direction to next point
                direction = (currentPath[i + 1] - currentPath[i]).normalized;
            }
            else if (i == currentPath.Count - 1)
            {
                // Last point: use direction from previous point
                direction = (currentPath[i] - currentPath[i - 1]).normalized;
            }
            else
            {
                // Middle points: average the incoming and outgoing directions
                Vector3 incoming = (currentPath[i] - currentPath[i - 1]).normalized;
                Vector3 outgoing = (currentPath[i + 1] - currentPath[i]).normalized;
                direction = (incoming + outgoing).normalized;
            }
            
            smoothedDirections.Add(direction);
        }
        
        // Create quads between each pair of points
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 point1 = currentPath[i];
            Vector3 point2 = currentPath[i + 1];
            
            // Use smoothed directions instead of segment direction
            Vector3 direction1 = smoothedDirections[i];
            Vector3 direction2 = smoothedDirections[i + 1];
            
            // Get pre-calculated normalized data
            float magNorm1 = 0f, magNorm2 = 0f;
            float mslNorm1 = 0f, mslNorm2 = 0f;
            
            if (i < currentNormalizedMagnitudes.Count)
            {
                magNorm1 = currentNormalizedMagnitudes[i];
            }
            if (i + 1 < currentNormalizedMagnitudes.Count)
            {
                magNorm2 = currentNormalizedMagnitudes[i + 1];
            }
            
            if (i < currentNormalizedMsl.Count)
            {
                mslNorm1 = currentNormalizedMsl[i];
            }
            if (i + 1 < currentNormalizedMsl.Count)
            {
                mslNorm2 = currentNormalizedMsl[i + 1];
            }
            
            // Calculate UV coordinates properly
            // UV: For billboard positioning and texture animation
            // UV2: For magnitude-based color interpolation (same as WindFieldMeshNc)
            // UV3: For MSL normalization (same as WindFieldMeshNc)
            
            // Use path calculator's average magnitude normalization and length multiplier for consistent animation
            float pathAvgMagNorm = 0f;
            float pathLengthMultiplier = 1f;
            float pathRandomOffset = 0f;
            if (pathCalculator != null)
            {
                pathAvgMagNorm = pathCalculator.averageMagnitudeNormalization;
                pathLengthMultiplier = pathCalculator.lengthMultiplier;
                pathRandomOffset = pathCalculator.randomTextureOffset;
            }
            
            // Calculate combined animation speed: magnitude * length multiplier
            float combinedAnimationSpeed = pathAvgMagNorm * pathLengthMultiplier;
            
            // Simple UV coordinates for billboard positioning and texture animation
            float segmentU1 = (float)i / (currentPath.Count - 1); // Start of segment (0 to 1 along the path)
            float segmentU2 = (float)(i + 1) / (currentPath.Count - 1); // End of segment
            
            // Create quad vertices - shader will handle billboard positioning
            int baseIndex = vertices.Count;
            
            // Bottom left vertex (point1, side 0)
            vertices.Add(point1);
            normals.Add(direction1);
            uvs.Add(new Vector2(segmentU1, 0)); // UV.y = 0 for billboard positioning
            uv2s.Add(new Vector2(magNorm1, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
            uv3s.Add(new Vector2(mslNorm1, pathRandomOffset)); // MSL normalization (same as WindFieldMeshNc)
            colors.Add(Color.white);
            
            // Top left vertex (point1, side 1)
            vertices.Add(point1);
            normals.Add(direction1);
            uvs.Add(new Vector2(segmentU1, 1)); // UV.y = 1 for billboard positioning
            uv2s.Add(new Vector2(magNorm1, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
            uv3s.Add(new Vector2(mslNorm1, pathRandomOffset)); // Same MSL for both sides
            colors.Add(Color.white);
            
            // Top right vertex (point2, side 1)
            vertices.Add(point2);
            normals.Add(direction2);
            uvs.Add(new Vector2(segmentU2, 1)); // UV.y = 1 for billboard positioning
            uv2s.Add(new Vector2(magNorm2, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
            uv3s.Add(new Vector2(mslNorm2, pathRandomOffset)); // MSL for second point
            colors.Add(Color.white);
            
            // Bottom right vertex (point2, side 0)
            vertices.Add(point2);
            normals.Add(direction2);
            uvs.Add(new Vector2(segmentU2, 0)); // UV.y = 0 for billboard positioning
            uv2s.Add(new Vector2(magNorm2, combinedAnimationSpeed)); // UV2.x = magnitude, UV2.y = combined animation speed
            uv3s.Add(new Vector2(mslNorm2, pathRandomOffset)); // MSL for second point
            colors.Add(Color.white);
            
            // Add triangles (two triangles per quad)
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }
        
        // Apply mesh data
        lineMesh.Clear();
        lineMesh.vertices = vertices.ToArray();
        lineMesh.normals = normals.ToArray();
        lineMesh.triangles = triangles.ToArray();
        lineMesh.uv = uvs.ToArray();
        lineMesh.SetUVs(1, uv2s); // UV2 for magnitude normalization
        lineMesh.SetUVs(2, uv3s); // UV3 for MSL normalization
        lineMesh.colors = colors.ToArray();
        
        // Recalculate bounds
        lineMesh.RecalculateBounds();
        
        // Update material properties
        UpdateMaterialProperties();
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
    
    public void ClearPath()
    {
        currentPath.Clear();
        currentNormalizedMagnitudes.Clear();
        currentNormalizedMsl.Clear();
        if (lineMesh != null)
        {
            lineMesh.Clear();
        }
    }
    
    public bool HasPath()
    {
        return currentPath.Count > 1;
    }
    
    public int GetPathPointCount()
    {
        return currentPath.Count;
    }
    
    // Public method to force mesh update
    public void ForceUpdateMesh()
    {
        UpdateLineMesh();
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
} 