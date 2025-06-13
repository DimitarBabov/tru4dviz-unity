using System;
using UnityEngine;
using RuntimeInspectorNamespace;

[RuntimeInspectorCustomEditor(typeof(WindStartStreamlinePoints), false)]
public class WindStartStreamlinePointsEditor : IRuntimeInspectorCustomEditor
{
    public void GenerateElements(ObjectField parent)
    {
        // Point Generation Mode Section
        CreateHeaderSeparator(parent, "Point Generation Mode");
        parent.CreateDrawersForVariables("pointMode");
        
        // Wall Point Sampling Section
        CreateHeaderSeparator(parent, "Wall Point Sampling");
        parent.CreateDrawersForVariables("wallSamplingInterval");
        
        // Volume Point Sampling Section
        CreateHeaderSeparator(parent, "Volume Point Sampling");
        parent.CreateDrawersForVariables("numPointsX", "numPointsY", "numPointsZ", "irregularity", "irregularitySeed");
        
        // Random Volume Point Sampling Section
        CreateHeaderSeparator(parent, "Random Volume Point Sampling");
        parent.CreateDrawersForVariables("pointsPer100CubicMetersDensity", "randomSeedDensity", "heightDensityFalloff", "minDensityFraction");
        
        // Preferences Section
        CreateHeaderSeparator(parent, "Preferences");
        parent.CreateDrawersForVariables("saveToPreferences");
        
        // Add note about restart requirement
        CreateInfoSeparator(parent, "NOTE: Requires app restart to take effect");
        
        // Debug Section
        CreateHeaderSeparator(parent, "Debug");
        parent.CreateDrawersForVariables("showDebugInfo", "drawGizmos");
    }

    private void CreateHeaderSeparator(ObjectField parent, string headerText)
    {
        // Create a read-only text display that acts as a header separator
        var headerField = parent.CreateDrawer(typeof(string), $"", 
            () => $"=== {headerText} ===",
            (value) => { }, // Empty setter - this field is read-only
            false);
        
        // Make the header field read-only and styled differently if possible
        if (headerField != null)
        {
            headerField.gameObject.SetActive(true);
            // Try to disable the input field if it exists
            var inputField = headerField.GetComponentInChildren<UnityEngine.UI.InputField>();
            if (inputField != null)
            {
                inputField.interactable = false;
                inputField.readOnly = true;
            }
        }
    }
    
    private void CreateInfoSeparator(ObjectField parent, string infoText)
    {
        // Create a read-only text display for important information
        var infoField = parent.CreateDrawer(typeof(string), $"", 
            () => infoText,
            (value) => { }, // Empty setter - this field is read-only
            false);
        
        // Make the info field read-only and styled differently if possible
        if (infoField != null)
        {
            infoField.gameObject.SetActive(true);
            // Try to disable the input field if it exists
            var inputField = infoField.GetComponentInChildren<UnityEngine.UI.InputField>();
            if (inputField != null)
            {
                inputField.interactable = false;
                inputField.readOnly = true;
                // Try to change text color to indicate it's informational
                var text = inputField.textComponent;
                if (text != null)
                {
                    text.color = Color.yellow;
                }
            }
        }
    }

    public void Refresh()
    {
        // No special refresh logic needed
    }

    public void Cleanup()
    {
        // No cleanup needed
    }
} 