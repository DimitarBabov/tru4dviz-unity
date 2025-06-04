using System.Collections.Generic;
using UnityEngine;

public class DataContainer : MonoBehaviour
{
    public List<float> lat = new List<float>();
    public List<float> lon = new List<float>();
    public List<float> msl = new List<float>();
    public List<float> u_norm = new List<float>();
    public List<float> v_norm = new List<float>();
    public List<float> w_norm = new List<float>();
    public List<float> mag = new List<float>();
    public List<float> mag_norm = new List<float>();

    public Vector2 uMinMax;
    public Vector2 vMinMax;
    public Vector2 wMinMax;
    public Vector2 magMinMax;

    public Vector2 latMinMax;
    public Vector2 lonMinMax;

    // Images for each level - stores the texture data for visualization
    public List<Texture2D> levelImages = new List<Texture2D>();

    // Grid cell dimensions: x = width, y = length, z = height
    public Vector3 gridCellDimensions;

    public bool IsLoaded { get; protected set; } = false;

    void Awake()
    {
        if (lat.Count > 0)
            latMinMax = new Vector2(Mathf.Min(lat.ToArray()), Mathf.Max(lat.ToArray()));
        if (lon.Count > 0)
            lonMinMax = new Vector2(Mathf.Min(lon.ToArray()), Mathf.Max(lon.ToArray()));
    }
} 