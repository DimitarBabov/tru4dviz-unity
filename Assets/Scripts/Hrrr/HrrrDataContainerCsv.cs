using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.IO;

public class HrrrDataContainerCsv : DataContainer
{
    public string resourceCsvName = "fort_worth_levels_1_to_15_unity";
    public string minmaxResourceCsvName = "fort_worth_levels_1_to_15_minmax";

    void Start()
    {
        if (lat.Count == 0 || lon.Count == 0)
        {
            LoadFromResource();
        }
        LoadMinMaxFromResource();
    }

    public void LoadFromResource()
    {
        TextAsset csvData = Resources.Load<TextAsset>(resourceCsvName);
        if (csvData == null)
        {
            Debug.LogError("CSV resource not found: " + resourceCsvName);
            return;
        }
        string[] lines = csvData.text.Split('\n');
        if (lines.Length < 2) return;
        // Find column indices
        string[] header = lines[0].Trim().Split(',');
        int idxLat = System.Array.IndexOf(header, "latitude");
        int idxLon = System.Array.IndexOf(header, "longitude");
        int idxMsl = System.Array.IndexOf(header, "gh[gpm]");
        int idxU = System.Array.IndexOf(header, "u_norm");
        int idxV = System.Array.IndexOf(header, "v_norm");
        int idxW = System.Array.IndexOf(header, "w_norm");
        int idxMag = System.Array.IndexOf(header, "mag");
        int idxMagNorm = System.Array.IndexOf(header, "mag_norm");
        float uMin = float.MaxValue, uMax = float.MinValue;
        float vMin = float.MaxValue, vMax = float.MinValue;
        float wMin = float.MaxValue, wMax = float.MinValue;
        float magMin = float.MaxValue, magMax = float.MinValue;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] row = line.Split(',');
            float fLat = float.Parse(row[idxLat], System.Globalization.CultureInfo.InvariantCulture);
            float fLon = float.Parse(row[idxLon], System.Globalization.CultureInfo.InvariantCulture);
            float fMsl = float.Parse(row[idxMsl], System.Globalization.CultureInfo.InvariantCulture);
            float fU = float.Parse(row[idxU], System.Globalization.CultureInfo.InvariantCulture);
            float fV = float.Parse(row[idxV], System.Globalization.CultureInfo.InvariantCulture);
            float fW = float.Parse(row[idxW], System.Globalization.CultureInfo.InvariantCulture);
            float fMag = float.Parse(row[idxMag], System.Globalization.CultureInfo.InvariantCulture);
            float fMagNorm = float.Parse(row[idxMagNorm], System.Globalization.CultureInfo.InvariantCulture);
            lat.Add(fLat);
            lon.Add(fLon);
            msl.Add(fMsl);
            u_norm.Add(fU);
            v_norm.Add(fV);
            w_norm.Add(fW);
            mag.Add(fMag);
            mag_norm.Add(fMagNorm);
            if (fU < uMin) uMin = fU;
            if (fU > uMax) uMax = fU;
            if (fV < vMin) vMin = fV;
            if (fV > vMax) vMax = fV;
            if (fW < wMin) wMin = fW;
            if (fW > wMax) wMax = fW;
            if (fMag < magMin) magMin = fMag;
            if (fMag > magMax) magMax = fMag;
        }
        uMinMax = new Vector2(uMin, uMax);
        vMinMax = new Vector2(vMin, vMax);
        wMinMax = new Vector2(wMin, wMax);
        magMinMax = new Vector2(magMin, magMax);
        IsLoaded = true;
    }

    public void LoadMinMaxFromResource()
    {
        TextAsset minmaxData = Resources.Load<TextAsset>(minmaxResourceCsvName);
        if (minmaxData == null)
        {
            Debug.LogError("MinMax CSV resource not found: " + minmaxResourceCsvName);
            return;
        }
        string[] lines = minmaxData.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] row = line.Split(',');
            if (row.Length < 3) continue;
            string varName = row[0];
            float min = float.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture);
            float max = float.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture);
            switch (varName)
            {
                case "u": uMinMax = new Vector2(min, max); break;
                case "v": vMinMax = new Vector2(min, max); break;
                case "w": wMinMax = new Vector2(min, max); break;
                case "mag": magMinMax = new Vector2(min, max); break;
            }
        }
        IsLoaded = true;
    }
}
