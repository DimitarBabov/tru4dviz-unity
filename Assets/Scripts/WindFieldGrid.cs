using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindFieldGrid : MonoBehaviour
{
    public DataContainer dataContainer;
    public float meshScale = 0.01f; // Scale down everything 100x by default
    public Color gridColor = Color.white;
    public float lineWidth = 0.01f;
    public Material lineMaterial;

    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

    void Start()
    {
        StartCoroutine(WaitForDataAndDrawGrid());
    }

    IEnumerator WaitForDataAndDrawGrid()
    {
        while (dataContainer == null || !dataContainer.IsLoaded)
        {
            yield return null;
        }
        DrawGrid();
    }

    void DrawGrid()
    {
        var xField = dataContainer.GetType().GetField("x_from_origin");
        var yField = dataContainer.GetType().GetField("y_from_origin");
        if (xField == null || yField == null)
        {
            Debug.LogError("DataContainer does not have x_from_origin/y_from_origin fields!");
            return;
        }
        var xList = (List<float>)xField.GetValue(dataContainer);
        var yList = (List<float>)yField.GetValue(dataContainer);
        int numPoints = xList.Count;
        if (numPoints == 0)
        {
            Debug.LogError("x_from_origin/y_from_origin are empty!");
            return;
        }
        // Find unique x, y, and msl values
        HashSet<float> uniqueX = new HashSet<float>(xList);
        HashSet<float> uniqueY = new HashSet<float>(yList);
        HashSet<float> uniqueMsl = new HashSet<float>(dataContainer.msl);
        List<float> sortedX = new List<float>(uniqueX);
        List<float> sortedY = new List<float>(uniqueY);
        List<float> sortedMsl = new List<float>(uniqueMsl);
        sortedX.Sort();
        sortedY.Sort();
        sortedMsl.Sort();
        float minY = float.MaxValue, maxY = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (float y in yList) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        foreach (float x in xList) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
        // For each unique msl (altitude), draw a grid at that height
        foreach (float msl in sortedMsl)
        {
            // For each unique x, draw a vertical line from minY to maxY at this msl
            foreach (float x in sortedX)
            {
                LineRenderer lr = CreateLineRenderer();
                Vector3[] linePoints = new Vector3[2];
                linePoints[0] = new Vector3(x, msl, minY) * meshScale;
                linePoints[1] = new Vector3(x, msl, maxY) * meshScale;
                lr.positionCount = 2;
                lr.SetPositions(linePoints);
                lineRenderers.Add(lr);
            }
            // For each unique y, draw a horizontal line from minX to maxX at this msl
            foreach (float y in sortedY)
            {
                LineRenderer lr = CreateLineRenderer();
                Vector3[] linePoints = new Vector3[2];
                linePoints[0] = new Vector3(minX, msl, y) * meshScale;
                linePoints[1] = new Vector3(maxX, msl, y) * meshScale;
                lr.positionCount = 2;
                lr.SetPositions(linePoints);
                lineRenderers.Add(lr);
            }
        }
    }

    LineRenderer CreateLineRenderer()
    {
        GameObject go = new GameObject("GridLine");
        go.transform.parent = this.transform;
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = gridColor;
        lr.endColor = gridColor;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = false;
        return lr;
    }
} 