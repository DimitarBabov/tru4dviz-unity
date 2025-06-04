using UnityEngine;
using UnityEngine.UI;

public class WindVisualizationToggle : MonoBehaviour
{
    [Header("Visualization GameObjects")]
    public GameObject windPathManagerObject;    // GameObject with WindPathManager component
    public GameObject windFieldMeshObject;      // GameObject with WindFieldMeshNc component
    
  
    
    [Header("Toggle Settings")]
    public bool showPathsWhenToggleOn = true;   // What to show when toggle is ON
    
    void Start()
    {
    
        
        // Validate references
        if (windPathManagerObject == null)
        {
            Debug.LogWarning("WindPathManager GameObject is not assigned!");
        }
        
        if (windFieldMeshObject == null)
        {
            Debug.LogWarning("WindFieldMesh GameObject is not assigned!");
        }
    }
    
    public void OnToggleChanged(bool isOn)
    {
        if (showPathsWhenToggleOn)
        {
            // Toggle ON = Show Paths, Toggle OFF = Show Field Mesh
            SetWindPathsActive(isOn);
            SetWindFieldMeshActive(!isOn);
        }
        else
        {
            // Toggle ON = Show Field Mesh, Toggle OFF = Show Paths
            SetWindPathsActive(!isOn);
            SetWindFieldMeshActive(isOn);
        }
        
        Debug.Log($"Wind Visualization Toggle: Paths={windPathManagerObject?.activeInHierarchy}, Mesh={windFieldMeshObject?.activeInHierarchy}");
    }
    
    void SetWindPathsActive(bool active)
    {
        if (windPathManagerObject != null)
        {
            windPathManagerObject.SetActive(active);
        }
    }
    
    void SetWindFieldMeshActive(bool active)
    {
        if (windFieldMeshObject != null)
        {
            windFieldMeshObject.SetActive(active);
        }
    }
    
  
    
} 