using UnityEngine;

public class DebugTempRotateTranslate : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("WindMissingDataMesh component to rotate (if null, will search on this GameObject)")]
    public WindMissingDataMesh targetMesh;
    
    [Header("Rotation Settings")]
    [Tooltip("Rotation to apply after mesh initialization")]
    public Vector3 rotationAngles = new Vector3(0, 180, 0);
    [Tooltip("Apply rotation in local space (true) or world space (false)")]
    public bool useLocalSpace = true;
    
    [Header("Translation Settings")]
    [Tooltip("Translation to apply after mesh initialization")]
    public Vector3 translationOffset = new Vector3(0, -30, 0);
    [Tooltip("Apply translation in local space (true) or world space (false)")]
    public bool useLocalSpaceForTranslation = true;
    
    [Header("Timing")]
    [Tooltip("Delay in seconds before applying transformations")]
    public float delaySeconds = 0.1f;
    [Tooltip("Interval in seconds to check for mesh initialization")]
    public float checkInterval = 0.1f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private bool transformationsApplied = false;
    private bool meshInitialized = false;
    
    void Start()
    {
        InitializeTarget();
        if (targetMesh != null)
        {
            InvokeRepeating(nameof(CheckMeshInitialization), checkInterval, checkInterval);
        }
    }
    
    private void InitializeTarget()
    {
        if (targetMesh == null)
        {
            targetMesh = GetComponent<WindMissingDataMesh>();
            if (targetMesh == null)
            {
                targetMesh = GetComponentInChildren<WindMissingDataMesh>();
            }
        }
        
        if (targetMesh == null)
        {
            Debug.LogError($"[{gameObject.name}] DebugTempRotateTranslate: No WindMissingDataMesh found!");
            enabled = false;
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTempRotateTranslate: Found target mesh on '{targetMesh.gameObject.name}'");
            Debug.Log($"[{gameObject.name}] DebugTempRotateTranslate: Will apply rotation {rotationAngles} and translation {translationOffset} after {delaySeconds}s delay");
        }
    }
    
    void CheckMeshInitialization()
    {
        if (transformationsApplied)
        {
            CancelInvoke(nameof(CheckMeshInitialization));
            return;
        }
        
        MeshFilter[] childMeshFilters = targetMesh.GetComponentsInChildren<MeshFilter>();
        bool hasValidMeshes = false;
        int totalVertices = 0;
        
        foreach (MeshFilter childMeshFilter in childMeshFilters)
        {
            if (childMeshFilter.gameObject != targetMesh.gameObject && 
                childMeshFilter.mesh != null && 
                childMeshFilter.mesh.vertexCount > 0)
            {
                hasValidMeshes = true;
                totalVertices += childMeshFilter.mesh.vertexCount;
            }
        }
        
        if (hasValidMeshes && !meshInitialized)
        {
            meshInitialized = true;
            if (showDebugInfo)
            {
                Debug.Log($"[{gameObject.name}] DebugTempRotateTranslate: Child meshes initialized with {totalVertices} total vertices across {childMeshFilters.Length} child objects");
            }
            
            Invoke(nameof(ApplyTransformations), delaySeconds);
        }
    }
    
    void ApplyTransformations()
    {
        if (transformationsApplied || targetMesh == null) return;
        
        Transform targetTransform = targetMesh.transform;
        
        // Apply rotation
        if (useLocalSpace)
        {
            targetTransform.localRotation = Quaternion.Euler(rotationAngles);
        }
        else
        {
            targetTransform.rotation = Quaternion.Euler(rotationAngles);
        }
        
        // Apply translation
        if (useLocalSpaceForTranslation)
        {
            targetTransform.localPosition += translationOffset;
        }
        else
        {
            targetTransform.position += translationOffset;
        }
        
        transformationsApplied = true;
        CancelInvoke(nameof(CheckMeshInitialization));
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTempRotateTranslate: Applied transformations to '{targetTransform.name}':\n" +
                     $"  Rotation: {rotationAngles} ({(useLocalSpace ? "local" : "world")} space)\n" +
                     $"  Translation: {translationOffset} ({(useLocalSpaceForTranslation ? "local" : "world")} space)");
        }
    }
    
    [ContextMenu("Apply Transformations Now")]
    public void ApplyTransformationsNow()
    {
        if (targetMesh == null)
        {
            Debug.LogError($"[{gameObject.name}] DebugTempRotateTranslate: No target mesh assigned!");
            return;
        }
        
        ApplyTransformations();
    }
    
    [ContextMenu("Reset Transformations")]
    public void ResetTransformations()
    {
        if (targetMesh == null)
        {
            Debug.LogError($"[{gameObject.name}] DebugTempRotateTranslate: No target mesh assigned!");
            return;
        }
        
        Transform targetTransform = targetMesh.transform;
        
        if (useLocalSpace)
        {
            targetTransform.localRotation = Quaternion.identity;
        }
        else
        {
            targetTransform.rotation = Quaternion.identity;
        }
        
        if (useLocalSpaceForTranslation)
        {
            targetTransform.localPosition -= translationOffset;
        }
        else
        {
            targetTransform.position -= translationOffset;
        }
        
        transformationsApplied = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTempRotateTranslate: Reset transformations for '{targetTransform.name}'");
        }
    }
    
    void OnDisable()
    {
        CancelInvoke();
    }
} 