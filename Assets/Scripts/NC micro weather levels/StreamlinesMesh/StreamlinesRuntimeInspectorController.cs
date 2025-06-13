using UnityEngine;
using RuntimeInspectorNamespace;
using System.Collections.Generic;

[System.Serializable]
public class ScriptReference
{
    public string sectionName;
    public Component targetScript;
    public string[] variableNames;
    public bool isExpanded = true;
}

public class StreamlinesRuntimeInspectorController : MonoBehaviour
{
    [Header("Runtime Inspector")]
    public RuntimeInspector runtimeInspector;
    
    [Header("Script References")]
    public List<ScriptReference> scriptReferences = new List<ScriptReference>();
    
    [Header("Default References")]
    public WindFieldStreamlinesRenderer targetRenderer;
    public WindStartStreamlinePoints targetStartPoints;
    
    [Header("Controls")]
    public KeyCode rendererToggleKey = KeyCode.F1;
    public KeyCode startPointsToggleKey = KeyCode.F2;
    public bool startVisible = true;
    
    private ScriptReference currentlyInspected;
    
    void Start()
    {
        // Find components if not assigned
        if (runtimeInspector == null)
            runtimeInspector = FindObjectOfType<RuntimeInspector>();
            
        if (targetRenderer == null)
            targetRenderer = FindObjectOfType<WindFieldStreamlinesRenderer>();
            
        if (targetStartPoints == null)
            targetStartPoints = FindObjectOfType<WindStartStreamlinePoints>();
            
        // Add default references if not already present
        if (scriptReferences.Count == 0)
        {
            // Add WindFieldStreamlinesRenderer sections
            if (targetRenderer != null)
            {
                // Flow Animation Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Flow Animation",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {                       
                        "streamlinesWidth", 
                        "textureAnimationSpeed", 
                        "transparency", 
                        "flowTiling"
                    }
                });

                // Spatial Bounds Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Spatial Bounds",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "maxAltitude",
                        "minAltitude",
                        "minLowestAltitude",
                        "boundsLeft",
                        "boundsRight",
                        "boundsFront",
                        "boundsBack",
                        "toggleTerrain"
                    }
                });

                // Wind Speed Range Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Wind Speed Range",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "speedTrimLower",
                        "speedTrimUpper"
                    }
                });

                // Flow Direction Gradient Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Flow Direction Gradient",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "flowDirectionGradientThreshold"
                    }
                });

                // Global Color Mapping Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Global Color Mapping",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "useGlobalMagnitudeRange",
                        "globalMinWindMagnitude",
                        "globalMaxWindMagnitude"
                    }
                });

                // Gradient Colors Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Gradient Colors",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "gradientColor0",
                        "gradientColor1",
                        "gradientColor2",
                        "gradientColor3",
                        "gradientColor4",
                        "gradientColor5"
                    }
                });

                // Preferences Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Preferences",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "saveToPreferences",
                        "resetPreferences"
                    }
                });
            }

            // Add WindStartStreamlinePoints sections
            if (targetStartPoints != null)
            {
                // Point Generation Mode Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Point Generation Mode",
                    targetScript = targetStartPoints,
                    variableNames = new string[] 
                    {
                        "pointMode"
                    }
                });

                // Wall Point Sampling Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Wall Point Sampling",
                    targetScript = targetStartPoints,
                    variableNames = new string[] 
                    {
                        "wallSamplingInterval"
                    }
                });

                // Volume Point Sampling Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Volume Point Sampling",
                    targetScript = targetStartPoints,
                    variableNames = new string[] 
                    {
                        "numPointsX",
                        "numPointsY",
                        "numPointsZ",
                        "irregularity",
                        "irregularitySeed"
                    }
                });

                // Random Volume Point Sampling Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Random Volume Point Sampling",
                    targetScript = targetStartPoints,
                    variableNames = new string[] 
                    {
                        "pointsPer100CubicMetersDensity",
                        "randomSeedDensity",
                        "heightDensityFalloff",
                        "minDensityFraction"
                    }
                });

                // Debug Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Debug",
                    targetScript = targetStartPoints,
                    variableNames = new string[] 
                    {
                        "showDebugInfo",
                        "drawGizmos"
                    }
                });
            }
        }
        
        // Set up the runtime inspector
        if (runtimeInspector != null && scriptReferences.Count > 0)
        {
            InspectFirstReference();
            runtimeInspector.gameObject.SetActive(startVisible);
        }
        else
        {
            Debug.LogError("RuntimeInspector or no script references found!");
        }
    }
    
    void Update()
    {
        // Toggle visibility and inspect renderer with F1
        if (Input.GetKeyDown(rendererToggleKey))
        {
            if (runtimeInspector != null)
            {
                if (runtimeInspector.gameObject.activeSelf)
                {
                    runtimeInspector.gameObject.SetActive(false);
                }
                else
                {
                    InspectFirstReference();
                    runtimeInspector.gameObject.SetActive(true);
                }
            }
        }
        
        // Toggle visibility and inspect start points with F2
        if (Input.GetKeyDown(startPointsToggleKey))
        {
            if (runtimeInspector != null)
            {
                if (runtimeInspector.gameObject.activeSelf)
                {
                    runtimeInspector.gameObject.SetActive(false);
                }
                else
                {
                    InspectNextReference();
                    runtimeInspector.gameObject.SetActive(true);
                }
            }
        }
    }
    
    public void InspectFirstReference()
    {
        if (runtimeInspector != null && scriptReferences.Count > 0)
        {
            var reference = scriptReferences[0];
            if (reference.targetScript != null)
            {
                currentlyInspected = reference;
                runtimeInspector.Inspect(reference.targetScript);
                Debug.Log($"Runtime Inspector: Now inspecting {reference.sectionName}");
            }
        }
    }
    
    public void InspectNextReference()
    {
        if (runtimeInspector != null && scriptReferences.Count > 0)
        {
            int currentIndex = currentlyInspected != null ? 
                scriptReferences.IndexOf(currentlyInspected) : -1;
            int nextIndex = (currentIndex + 1) % scriptReferences.Count;
            
            var reference = scriptReferences[nextIndex];
            if (reference.targetScript != null)
            {
                currentlyInspected = reference;
                runtimeInspector.Inspect(reference.targetScript);
                Debug.Log($"Runtime Inspector: Now inspecting {reference.sectionName}");
            }
        }
    }
    
    public void InspectReference(int index)
    {
        if (runtimeInspector != null && index >= 0 && index < scriptReferences.Count)
        {
            var reference = scriptReferences[index];
            if (reference.targetScript != null)
            {
                currentlyInspected = reference;
                runtimeInspector.Inspect(reference.targetScript);
                Debug.Log($"Runtime Inspector: Now inspecting {reference.sectionName}");
            }
        }
    }
    
    public void ClearInspector()
    {
        if (runtimeInspector != null)
        {
            currentlyInspected = null;
            runtimeInspector.Inspect((Object)null);
        }
    }
} 