using UnityEngine;

public class DebugTerrainToggle : MonoBehaviour
{
    [Header("Terrain GameObjects")]
    [Tooltip("Google terrain GameObject")]
    public GameObject googleTerrain;
    [Tooltip("Mockup terrain GameObject")]
    public GameObject mockupTerrain;
    
    [Header("Settings")]
    [Tooltip("Input key to toggle terrains")]
    public KeyCode toggleKey = KeyCode.Mouse1; // Right mouse button
    [Tooltip("Start with Google terrain active")]
    public bool startWithGoogleTerrain = true;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private bool isGoogleTerrainActive;
    
    void Start()
    {
        InitializeTerrains();
        SetInitialState();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleTerrains();
        }
    }
    
    private void InitializeTerrains()
    {
        if (googleTerrain == null)
        {
            Debug.LogError($"[{gameObject.name}] DebugTerrainToggle: Google terrain GameObject not assigned!");
        }
        
        if (mockupTerrain == null)
        {
            Debug.LogError($"[{gameObject.name}] DebugTerrainToggle: Mockup terrain GameObject not assigned!");
        }
        
        if (googleTerrain == null || mockupTerrain == null)
        {
            enabled = false;
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Initialized with Google terrain '{googleTerrain.name}' and Mockup terrain '{mockupTerrain.name}'");
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Press {toggleKey} to toggle between terrains");
        }
    }
    
    private void SetInitialState()
    {
        if (googleTerrain == null || mockupTerrain == null) return;
        
        isGoogleTerrainActive = startWithGoogleTerrain;
        
        googleTerrain.SetActive(isGoogleTerrainActive);
        mockupTerrain.SetActive(!isGoogleTerrainActive);
        
        if (showDebugInfo)
        {
            string activeTerrain = isGoogleTerrainActive ? "Google" : "Mockup";
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Initial state set - {activeTerrain} terrain active");
        }
    }
    
    private void ToggleTerrains()
    {
        if (googleTerrain == null || mockupTerrain == null) return;
        
        isGoogleTerrainActive = !isGoogleTerrainActive;
        
        googleTerrain.SetActive(isGoogleTerrainActive);
        mockupTerrain.SetActive(!isGoogleTerrainActive);
        
        if (showDebugInfo)
        {
            string activeTerrain = isGoogleTerrainActive ? "Google" : "Mockup";
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Toggled to {activeTerrain} terrain");
        }
    }
    
    [ContextMenu("Toggle Terrains")]
    public void ToggleTerrainsManual()
    {
        ToggleTerrains();
    }
    
    [ContextMenu("Set Google Terrain Active")]
    public void SetGoogleTerrainActive()
    {
        if (googleTerrain == null || mockupTerrain == null) return;
        
        isGoogleTerrainActive = true;
        googleTerrain.SetActive(true);
        mockupTerrain.SetActive(false);
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Set Google terrain active");
        }
    }
    
    [ContextMenu("Set Mockup Terrain Active")]
    public void SetMockupTerrainActive()
    {
        if (googleTerrain == null || mockupTerrain == null) return;
        
        isGoogleTerrainActive = false;
        googleTerrain.SetActive(false);
        mockupTerrain.SetActive(true);
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DebugTerrainToggle: Set Mockup terrain active");
        }
    }
} 