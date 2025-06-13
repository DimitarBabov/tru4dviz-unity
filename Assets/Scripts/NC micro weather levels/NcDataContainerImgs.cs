using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class NcDataContainerImgs : DataContainer
{
    public string imgResourceFolder = "img_encoded_nc_levels";
    // Set subFolder to one of: "high_levels_img_encoded", "mid_levels_img_encoded", "low_levels_img_encoded"
    public string subFolder = "mid_levels_img_encoded";
    public int minLevel = 0;
    public int maxLevel = 2;

    public float lat_origin;
    public float lon_origin;
    public List<float> x_from_origin = new List<float>();
    public List<float> y_from_origin = new List<float>();

    [Header("Grid Info")]
    public Vector3Int gridDimensions;
    public Vector3 gridMin;
    public Vector3 gridMax;
    public Vector3 gridCellSize;
    
    [Header("UI Status")]
    public TextMeshProUGUI statusText;
    
    [Header("Wind Statistics")]
    [Tooltip("Average U (eastward) wind component in m/s")]
    public float averageU = 0f;
    [Tooltip("Average V (northward) wind component in m/s")]
    public float averageV = 0f;
    [Tooltip("Average W (vertical) wind component in m/s")]
    public float averageW = 0f;
    [Tooltip("Average total wind magnitude in m/s")]
    public float averageMagnitude = 0f;
    
    [Header("Loading Progress")]
    [Tooltip("Shows current loading progress (0-1)")]
    [Range(0f, 1f)]
    public float loadingProgress = 0f;
    [Tooltip("Current loading status")]
    public string loadingStatus = "Not Started";
    
    [Header("Performance Settings")]
    [Tooltip("Maximum time per frame in milliseconds before yielding (lower = more responsive, higher = faster loading)")]
    [Range(5f, 50f)]
    public float maxFrameTimeMs = 16f; // Default to ~60fps budget
    
    private Dictionary<Vector3Int, int> gridToIndex = new Dictionary<Vector3Int, int>();
    private Vector3Int[] uniqueGridPositions;
    private float gridCellWidth;

    private void UpdateStatus(string message)
    {
        loadingStatus = message;
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    void Start()
    {
        UpdateStatus("Initializing data loading...");
        StartCoroutine(LoadFromImagesAndJsonCoroutine());
    }

    [System.Serializable]
    public class LevelMeta
    {
        public int level_index;
        public float u_min, u_max, v_min, v_max, w_min, w_max;
        public float alt_min, alt_max;
        public float altitude;
        public float min_lat, max_lat, min_lon, max_lon;
        public float min_x_from_origin, max_x_from_origin, min_y_from_origin, max_y_from_origin;
        public int num_lat, num_lon;
        public float lat_origin;
        public float lon_origin;
    }

    public IEnumerator LoadFromImagesAndJsonCoroutine()
    {
        loadingProgress = 0f;
        UpdateStatus("Starting data load...");
        yield return null; // Allow UI to update
        
        System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();
        frameTimer.Start();
        
        lat.Clear(); lon.Clear(); msl.Clear();
        u_norm.Clear(); v_norm.Clear(); w_norm.Clear();
        mag.Clear(); mag_norm.Clear();
        x_from_origin.Clear(); y_from_origin.Clear();
        levelImages.Clear();

        float uGlobalMin = float.MaxValue, uGlobalMax = float.MinValue;
        float vGlobalMin = float.MaxValue, vGlobalMax = float.MinValue;
        float wGlobalMin = float.MaxValue, wGlobalMax = float.MinValue;
        float magGlobalMin = float.MaxValue, magGlobalMax = float.MinValue;

        // Track magnitude per level for averaging
        Dictionary<int, List<float>> magnitudesByLevel = new Dictionary<int, List<float>>();

        // Determine prefix based on subFolder
        string prefix = "mid_level";
        if (subFolder.Contains("high")) prefix = "high_level";
        else if (subFolder.Contains("low")) prefix = "low_level";

        UpdateStatus("Loading images for missing data mesh...");
        loadingProgress = 0.1f;
        
        // Check if we need to yield based on frame time
        if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
        {
            yield return null;
            frameTimer.Restart();
        }

        // First pass: Load ALL available images for missing data mesh (regardless of minLevel/maxLevel)
        List<int> allAvailableLevels = new List<int>();
        for (int level = 0; level <= 50; level++) // Check a wide range of possible levels
        {
            string imgPath = subFolder + "/" + prefix + level + "_img";
            Texture2D tex = Resources.Load<Texture2D>(imgPath);
            if (tex != null)
            {
                allAvailableLevels.Add(level);
                levelImages.Add(tex);
                Debug.Log($"Loaded image for level {level} (missing data mesh)");
            }
            
            // Yield only if we've spent too much time in this frame
            if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
            {
                yield return null;
                frameTimer.Restart();
            }
        }
        
        Debug.Log($"Loaded {levelImages.Count} images for missing data mesh from levels: [{string.Join(", ", allAvailableLevels)}]");

        UpdateStatus("Processing wind data...");
        loadingProgress = 0.3f;
        
        if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
        {
            yield return null;
            frameTimer.Restart();
        }

        // Second pass: Process wind data ONLY for specified level range (minLevel to maxLevel)
        bool originSet = false;
        List<float> tempU = new List<float>();
        List<float> tempV = new List<float>();
        List<float> tempW = new List<float>();
        List<float> tempMag = new List<float>();
        
        Debug.Log($"Processing wind data for levels {minLevel} to {maxLevel}");
        
        int totalLevels = maxLevel - minLevel + 1;
        int processedLevels = 0;
        
        for (int level = minLevel; level <= maxLevel; level++)
        {
            Debug.Log($"Processing wind data for level {level}");
            UpdateStatus($"Processing level {level}...");
            loadingProgress = 0.3f + (processedLevels / (float)totalLevels) * 0.5f;
            
            if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
            {
                yield return null;
                frameTimer.Restart();
            }
            
            string metaPath = subFolder + "/" + prefix + level + "_meta";
            TextAsset metaJson = Resources.Load<TextAsset>(metaPath);
            if (metaJson == null) 
            {
                Debug.LogWarning($"No metadata found for level {level}, skipping wind data processing");
                processedLevels++;
                continue;
            }
            
            LevelMeta meta = JsonUtility.FromJson<LevelMeta>(metaJson.text);
            if (!originSet && meta != null)
            {
                lat_origin = meta.lat_origin;
                lon_origin = meta.lon_origin;
                originSet = true;
                Debug.Log($"Origin set from level {level}: lat={lat_origin}, lon={lon_origin}");
            }
            
            // Initialize magnitude list for this level
            if (!magnitudesByLevel.ContainsKey(level))
            {
                magnitudesByLevel[level] = new List<float>();
            }
            
            string imgPath = subFolder + "/" + prefix + level + "_img";
            Texture2D tex = Resources.Load<Texture2D>(imgPath);
            if (tex == null) 
            {
                Debug.LogWarning($"No image found for level {level}, skipping wind data processing");
                processedLevels++;
                continue;
            }
            
            Debug.Log($"Processing level {level}: {meta.num_lat}x{meta.num_lon} image");
            
            Color32[] pixels = tex.GetPixels32();
            int numLat = meta.num_lat;
            int numLon = meta.num_lon;
            float dx = (meta.max_x_from_origin - meta.min_x_from_origin) / (numLon - 1);
            float dy = (meta.max_y_from_origin - meta.min_y_from_origin) / (numLat - 1);
            
            int validPixels = 0;
            int skippedPixels = 0;
            
            for (int y = 0; y < numLat; y++)
            {
                for (int x = 0; x < numLon; x++)
                {
                    int idx = y * numLon + x;
                    if (idx >= pixels.Length) continue;
                    Color32 c = pixels[idx];
                    if (c.r == 255 && c.g == 255 && c.b == 255 && c.a == 255) 
                    {
                        // Skip missing data points (white pixels)
                        skippedPixels++;
                        continue;
                    }
                    
                    validPixels++;
                    
                    // Decode using the per-level metadata ranges (this gives us the original values)
                    float uVal = meta.u_min + (c.r / 255f) * (meta.u_max - meta.u_min);
                    float vVal = meta.v_min + (c.g / 255f) * (meta.v_max - meta.v_min);
                    float wVal = meta.w_min + (c.b / 255f) * (meta.w_max - meta.w_min);
                    float altNorm = Mathf.Clamp01((c.a - 1f) / 253f);
                    float altVal = meta.alt_min + altNorm * (meta.alt_max - meta.alt_min);
                    
                    // Track global ranges for final normalization
                    if (uVal < uGlobalMin) uGlobalMin = uVal;
                    if (uVal > uGlobalMax) uGlobalMax = uVal;
                    if (vVal < vGlobalMin) vGlobalMin = vVal;
                    if (vVal > vGlobalMax) vGlobalMax = vVal;
                    if (wVal < wGlobalMin) wGlobalMin = wVal;
                    if (wVal > wGlobalMax) wGlobalMax = wVal;
                    if (Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal) < magGlobalMin) magGlobalMin = Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal);
                    if (Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal) > magGlobalMax) magGlobalMax = Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal);
                    
                    // Store the original values
                    tempU.Add(uVal);
                    tempV.Add(vVal);
                    tempW.Add(wVal);
                    tempMag.Add(Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal));
                    
                    // Store magnitude for this level
                    magnitudesByLevel[level].Add(Mathf.Sqrt(uVal * uVal + vVal * vVal + wVal * wVal));
                    
                    // Store spatial and altitude data
                    float x_from = meta.min_x_from_origin + x * dx;
                    float y_from = meta.max_y_from_origin - y * dy;
                    x_from_origin.Add(x_from);
                    y_from_origin.Add(y_from);
                    msl.Add(altVal);
                }
                
                // Yield only if we've spent too much time in this frame (check every row)
                if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
                {
                    yield return null;
                    frameTimer.Restart();
                }
            }
            
            Debug.Log($"Level {level} processed: {validPixels} valid pixels, {skippedPixels} skipped (missing data)");
            processedLevels++;
        }

        UpdateStatus("Normalizing data...");
        loadingProgress = 0.8f;
        
        if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
        {
            yield return null;
            frameTimer.Restart();
        }
        
        // Now normalize for visualization (these will be used for direction and coloring)
        for (int i = 0; i < tempU.Count; i++)
        {
            // Safe normalization - handle zero ranges to avoid NaN
            float uRange = uGlobalMax - uGlobalMin;
            float vRange = vGlobalMax - vGlobalMin;
            float wRange = wGlobalMax - wGlobalMin;
            float magRange = magGlobalMax - magGlobalMin;
            
            u_norm.Add(uRange > 0 ? (tempU[i] - uGlobalMin) / uRange : 0.5f);
            v_norm.Add(vRange > 0 ? (tempV[i] - vGlobalMin) / vRange : 0.5f);
            w_norm.Add(wRange > 0 ? (tempW[i] - wGlobalMin) / wRange : 0.0f);
            mag.Add(tempMag[i]); // Store original magnitude values
            mag_norm.Add(magRange > 0 ? (tempMag[i] - magGlobalMin) / magRange : 0.5f);
            
            // Yield only if we've spent too much time in this frame (check every 10000 iterations)
            if (i % 10000 == 0 && frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
            {
                yield return null;
                frameTimer.Restart();
            }
        }
        
        uMinMax = new Vector2(uGlobalMin, uGlobalMax);
        vMinMax = new Vector2(vGlobalMin, vGlobalMax);
        wMinMax = new Vector2(wGlobalMin, wGlobalMax);
        magMinMax = new Vector2(magGlobalMin, magGlobalMax);
        
        Debug.Log($"Decoded {tempU.Count} wind vectors");
        Debug.Log($"U range: {uGlobalMin:F3} to {uGlobalMax:F3} m/s");
        Debug.Log($"V range: {vGlobalMin:F3} to {vGlobalMax:F3} m/s");
        Debug.Log($"W range: {wGlobalMin:F3} to {wGlobalMax:F3} m/s");
        Debug.Log($"Magnitude range: {magGlobalMin:F3} to {magGlobalMax:F3} m/s");
        
        // Calculate average wind component values for inspector display
        if (tempU.Count > 0)
        {
            float sumU = 0f, sumV = 0f, sumW = 0f, sumMag = 0f;
            for (int i = 0; i < tempU.Count; i++)
            {
                sumU += tempU[i];
                sumV += tempV[i];
                sumW += tempW[i];
                sumMag += tempMag[i];
            }
            
            averageU = sumU / tempU.Count;
            averageV = sumV / tempV.Count;
            averageW = sumW / tempW.Count;
            averageMagnitude = sumMag / tempMag.Count;
            
            Debug.Log($"Average wind components: U={averageU:F3}, V={averageV:F3}, W={averageW:F3}, Mag={averageMagnitude:F3} m/s");
        }
        else
        {
            averageU = averageV = averageW = averageMagnitude = 0f;
        }
        
        // Print average magnitude per level
        Debug.Log("Average magnitude per level:");
        foreach (var kvp in magnitudesByLevel)
        {
            int level = kvp.Key;
            List<float> magnitudes = kvp.Value;
            if (magnitudes.Count > 0)
            {
                float average = magnitudes.Sum() / magnitudes.Count;
                Debug.Log($"  Level {level}: {average:F3} m/s (from {magnitudes.Count} data points)");
            }
        }

        UpdateStatus("Building grid structure...");
        loadingProgress = 0.9f;
        
        if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
        {
            yield return null;
            frameTimer.Restart();
        }
        
        // Build grid structure using metadata
        yield return StartCoroutine(BuildGridStructureCoroutine());
        
        IsLoaded = true;
        
        UpdateStatus("Data loading complete! Generating visualizations...");
        loadingProgress = 1f;
        yield return null;
        
        // Clear status message after a brief delay
        yield return new WaitForSeconds(2f);
        UpdateStatus("");
        statusText.gameObject.SetActive(false);
        
        Debug.Log("Data loading completed successfully!");
    }
    
    public void LoadFromImagesAndJson()
    {
        // Keep the old method for backward compatibility, but use coroutine
        StartCoroutine(LoadFromImagesAndJsonCoroutine());
    }
    
    IEnumerator BuildGridStructureCoroutine()
    {
        System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();
        frameTimer.Start();
        
        if (levelImages.Count == 0) yield break;
        
        // Use metadata from first level to get grid structure
        string prefix = "mid_level";
        if (subFolder.Contains("high")) prefix = "high_level";
        else if (subFolder.Contains("low")) prefix = "low_level";
        
        // Get metadata from first available level
        LevelMeta firstMeta = null;
        for (int level = minLevel; level <= maxLevel; level++)
        {
            string metaPath = subFolder + "/" + prefix + level + "_meta";
            TextAsset metaJson = Resources.Load<TextAsset>(metaPath);
            if (metaJson != null)
            {
                firstMeta = JsonUtility.FromJson<LevelMeta>(metaJson.text);
                break;
            }
        }
        
        if (firstMeta == null) yield break;
        
        // Collect altitude ranges across all levels
        float globalAltMin = float.MaxValue;
        float globalAltMax = float.MinValue;
        float minLevelAltitude = 0f;
        float maxLevelAltitude = 0f;
        
        for (int level = minLevel; level <= maxLevel; level++)
        {
            string metaPath = subFolder + "/" + prefix + level + "_meta";
            TextAsset metaJson = Resources.Load<TextAsset>(metaPath);
            if (metaJson != null)
            {
                LevelMeta meta = JsonUtility.FromJson<LevelMeta>(metaJson.text);
                if (meta.alt_min < globalAltMin) globalAltMin = meta.alt_min;
                if (meta.alt_max > globalAltMax) globalAltMax = meta.alt_max;
                
                // Get specific altitude values for min and max levels
                if (level == minLevel)
                {
                    minLevelAltitude = meta.altitude;
                }
                if (level == maxLevel)
                {
                    maxLevelAltitude = meta.altitude;
                }
            }
            
            // Yield only if we've spent too much time in this frame
            if (frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
            {
                yield return null;
                frameTimer.Restart();
            }
        }
        
        // Set grid bounds: [min_x_from_origin, alt_min, min_y_from_origin] (Unity: X, Y=altitude, Z)
        gridMin = new Vector3(firstMeta.min_x_from_origin, globalAltMin, firstMeta.min_y_from_origin);
        gridMax = new Vector3(firstMeta.max_x_from_origin, globalAltMax, firstMeta.max_y_from_origin);
        
        // Calculate actual number of unique altitude levels from the loaded data
        var uniqueAltitudes = msl.Distinct().ToList();
        int actualNumLevels = uniqueAltitudes.Count;
        
        // Set grid dimensions as point counts (X=longitude, Y=altitude, Z=latitude)
        gridDimensions = new Vector3Int(firstMeta.num_lon, actualNumLevels, firstMeta.num_lat);
        
        // Calculate grid cell size as spacing: (max - min) / (num_points - 1)
        // For Y (altitude), use actual altitude difference between loaded levels
        gridCellSize = new Vector3(
            (gridMax.x - gridMin.x) / (gridDimensions.x - 1),
            (maxLevelAltitude - minLevelAltitude) / (gridDimensions.y - 1),
            (gridMax.z - gridMin.z) / (gridDimensions.z - 1)
        );
        
        // Calculate grid cell width from X spacing
        gridCellWidth = gridCellSize.x;
        
        // Build mapping from grid coordinates to data indices
        yield return StartCoroutine(BuildGridToIndexMappingCoroutine());
        
        Debug.Log($"Grid bounds: {gridMin} to {gridMax}");
        Debug.Log($"Grid dimensions: {gridDimensions}");
        Debug.Log($"Grid cell size: {gridCellSize}");
        Debug.Log($"Grid cell width: {gridCellWidth:F3}");
    }
    
    void BuildGridStructure()
    {
        // Keep the old method for backward compatibility, but use coroutine
        StartCoroutine(BuildGridStructureCoroutine());
    }
    
    IEnumerator BuildGridToIndexMappingCoroutine()
    {
        System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();
        frameTimer.Start();
        
        gridToIndex.Clear();
        uniqueGridPositions = new Vector3Int[x_from_origin.Count];
        
        // Get sorted unique values for mapping
        var uniqueX = x_from_origin.Distinct().OrderBy(x => x).ToList();
        var uniqueY = y_from_origin.Distinct().OrderBy(y => y).ToList();
        var uniqueZ = msl.Distinct().OrderBy(z => z).ToList();
        
        for (int i = 0; i < x_from_origin.Count; i++)
        {
            int xIdx = uniqueX.FindIndex(x => Mathf.Approximately(x, x_from_origin[i]));
            int yIdx = uniqueY.FindIndex(y => Mathf.Approximately(y, y_from_origin[i]));
            int zIdx = uniqueZ.FindIndex(z => Mathf.Approximately(z, msl[i]));
           
            Vector3Int gridPos = new Vector3Int(xIdx, zIdx, yIdx);
            uniqueGridPositions[i] = gridPos;
            
            if (!gridToIndex.ContainsKey(gridPos))
            {
                gridToIndex[gridPos] = i;
            }
            
            // Yield only if we've spent too much time in this frame (check every 25000 iterations)
            if (i % 25000 == 0 && frameTimer.ElapsedMilliseconds > maxFrameTimeMs)
            {
                yield return null;
                frameTimer.Restart();
            }
        }
        
        Debug.Log($"Grid structure built: {gridDimensions} dimensions, cell width: {gridCellWidth:F3}");
        Debug.Log($"Grid bounds: {gridMin} to {gridMax}");
    }
    
    void BuildGridToIndexMapping()
    {
        // Keep the old method for backward compatibility, but use coroutine
        StartCoroutine(BuildGridToIndexMappingCoroutine());
    }


} 