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
    
    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool startVisible = true;
    
    private ScriptReference currentlyInspected;
    
    void Start()
    {
        // Find components if not assigned
        if (runtimeInspector == null)
            runtimeInspector = FindObjectOfType<RuntimeInspector>();
            
        if (targetRenderer == null)
            targetRenderer = FindObjectOfType<WindFieldStreamlinesRenderer>();
            

            
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
                        "boundsBack"
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

                // Flow Direction Gradient & Streamlines Density Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "~~~~~~~~~~~~~~~~~~",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "flowDirectionGradientThreshold",
                        "streamlinesDensity"
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

                // Solid Color Section
                scriptReferences.Add(new ScriptReference 
                { 
                    sectionName = "Solid Color",
                    targetScript = targetRenderer,
                    variableNames = new string[] 
                    {
                        "solidColor",
                        "solidColorBlend"                        ,
                        "toggleTerrain"
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

            // WindStartStreamlinePoints sections removed - only showing renderer sections
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
        // F1 toggles runtime inspector visibility
        if (Input.GetKeyDown(toggleKey))
        {
            if (runtimeInspector != null)
            {
                if (runtimeInspector.gameObject.activeSelf)
                {
                    // If visible, hide it
                    runtimeInspector.gameObject.SetActive(false);
                }
                else
                {
                    // If not visible, show first section
                    InspectFirstReference();
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