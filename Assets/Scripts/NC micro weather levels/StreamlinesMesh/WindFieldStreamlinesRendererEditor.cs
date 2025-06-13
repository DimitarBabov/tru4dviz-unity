using System;
using UnityEngine;
using RuntimeInspectorNamespace;

[RuntimeInspectorCustomEditor(typeof(WindFieldStreamlinesRenderer), false)]
public class WindFieldStreamlinesRendererEditor : IRuntimeInspectorCustomEditor
{
    public void GenerateElements(ObjectField parent)
    {
        // Wind Flow Animation Section
        CreateHeaderSeparator(parent, "Flow Animation");
        parent.CreateDrawersForVariables("streamlinesWidth", "textureAnimationSpeed", "transparency", "flowTiling");
        
        // Spatial Bounds Section
        CreateHeaderSeparator(parent, "Spatial Bounds");
        parent.CreateDrawersForVariables( "maxAltitude", "minAltitude", "minLowestAltitude", "boundsLeft", "boundsRight", "boundsFront", "boundsBack","toggleTerrain");
        
        // Speed Trimming Section
        CreateHeaderSeparator(parent, "Wind Speed Range");
        parent.CreateDrawersForVariables("speedTrimLower", "speedTrimUpper");
        
        // Flow Direction Change Trimming Section
        CreateHeaderSeparator(parent, "Wind Direction Gradient");
        parent.CreateDrawersForVariables("flowDirectionGradientThreshold", "streamlinesDensity");
        
        // Global Color Mapping Section
        CreateHeaderSeparator(parent, "Global Color Mapping");
        parent.CreateDrawersForVariables("useGlobalMagnitudeRange", "globalMinWindMagnitude", "globalMaxWindMagnitude");
        
        // Gradient Colors Section
        CreateHeaderSeparator(parent, "Gradient Colors");
        parent.CreateDrawersForVariables("gradientColor0", "gradientColor1", "gradientColor2", "gradientColor3", "gradientColor4", "gradientColor5");
        
        // Solid Color Section
        CreateHeaderSeparator(parent, "Solid Color");
        parent.CreateDrawersForVariables("solidColor", "solidColorBlend", "toggleTerrain");
        
        // Preferences Section - Show both preference fields
        CreateHeaderSeparator(parent, "Preferences");
        parent.CreateDrawersForVariables("saveToPreferences", "resetPreferences");
    }

    private void CreateHeaderSeparator(ObjectField parent, string headerText)
    {
        // Create a read-only text display that acts as a header separator
        // This is a workaround since Runtime Inspector doesn't support [Header] natively
        var headerField = parent.CreateDrawer(typeof(string), $"", 
            () => headerText, // Return a separator line instead of empty string
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

    public void Refresh()
    {
        // No special refresh logic needed
    }

    public void Cleanup()
    {
        // No cleanup needed
    }
} 