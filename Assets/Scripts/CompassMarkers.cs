using UnityEngine;

public class CompassMarkers : MonoBehaviour
{
    [Header("Marker Settings")]
    public float offsetPercentage = 0.05f; // Offset distance as percentage of data range
    public int fontSize = 1000;
    public string markersParentName = "Compass Markers";

    public void CreateMarkers(float minX, float maxX, float minY, float maxY, float altitude)
    {
        // Create parent object for all markers
        GameObject markersParent = new GameObject(markersParentName);
        markersParent.transform.SetParent(this.transform);

        // Calculate offset distance
        float offsetX = (maxX - minX) * offsetPercentage;
        float offsetY = (maxY - minY) * offsetPercentage;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        // Cardinal direction markers (white)
        CreateTextMarker("N", new Vector3(centerX, altitude, maxY + offsetY), "North Marker", Color.white, markersParent.transform);
        CreateTextMarker("S", new Vector3(centerX, altitude, minY - offsetY), "South Marker", Color.white, markersParent.transform);
        CreateTextMarker("W", new Vector3(minX - offsetX, altitude, centerY), "West Marker", Color.white, markersParent.transform);
        CreateTextMarker("E", new Vector3(maxX + offsetX, altitude, centerY), "East Marker", Color.white, markersParent.transform);

        Debug.Log($"Compass Markers Created:");
        Debug.Log($"  N: X={centerX}, Y={maxY}, Altitude={altitude}");
        Debug.Log($"  S: X={centerX}, Y={minY}, Altitude={altitude}");
        Debug.Log($"  W: X={minX}, Y={centerY}, Altitude={altitude}");
        Debug.Log($"  E: X={maxX}, Y={centerY}, Altitude={altitude}");
    }

    private void CreateTextMarker(string text, Vector3 position, string name, Color color, Transform parent)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.position = position;
        textObj.transform.SetParent(parent);
        // Rotate text to face upward (90 degrees around X-axis)
        textObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = fontSize;
        textMesh.color = color;
        textMesh.anchor = TextAnchor.MiddleCenter;
    }
} 