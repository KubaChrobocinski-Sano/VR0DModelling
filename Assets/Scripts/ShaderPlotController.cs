using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ShaderPlotController : MonoBehaviour
{
    [Header("Shader References")]
    public Material plotMaterial;
    public RawImage plotTarget;

    public enum PlotType { LVPressure, AorticPressure, Flow, Volume }

    [Header("Plot Selection")]
    public PlotType plotToShow = PlotType.LVPressure;

    [Header("Plot Style")]
    public Color backgroundColor = Color.black;
    public Color waveformColor = Color.green;
    public Color axisColor = Color.white;
    public int xTicks = 10;
    public int yTicks = 5;
    public Font labelFont;
    public int labelFontSize = 14;
    public Color labelColor = Color.white;

    private Texture2D plotTexture;
    private const int texWidth = 1024;
    private const int texHeight = 256;

    // Store UI labels for reuse
    private readonly List<Text> xLabels = new();
    private readonly List<Text> yLabels = new();

    void Awake()
    {
        plotTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        plotTexture.wrapMode = TextureWrapMode.Clamp;
        plotMaterial.mainTexture = plotTexture;
        plotTarget.texture = plotTexture;
    }

    public void DisplaySimulation(SimulationResult sim)
    {
        float[] yData = GetDataByType(sim);
        float[] xData = sim.time;

        DrawWaveformWithAxes(xData, yData);
        UpdateLabels(xData, yData);
    }

    private float[] GetDataByType(SimulationResult sim)
    {
        return plotToShow switch
        {
            PlotType.LVPressure => sim.plv,
            PlotType.AorticPressure => sim.pa,
            PlotType.Flow => sim.flow,
            PlotType.Volume => sim.volume,
            _ => sim.plv
        };
    }

    private void DrawWaveformWithAxes(float[] x, float[] y)
    {
        plotTexture.ClearTexture(backgroundColor);

        float minY = Mathf.Min(y);
        float maxY = Mathf.Max(y);
        float rangeY = maxY - minY;
        if (rangeY < 1e-6f) rangeY = 1f;

        // --- Axis placement (closer to edges now) ---
        int marginX = Mathf.RoundToInt(texWidth * 0.05f);   // left margin (Y-axis)
        int marginY = Mathf.RoundToInt(texHeight * 0.05f);  // bottom margin (X-axis)

        int yAxisX = marginX;
        int xAxisY = marginY;

        // --- Draw axes ---
        DrawLineVertical(yAxisX, 0, texHeight - 1, axisColor);
        DrawLineHorizontal(xAxisY, 0, texWidth - 1, axisColor);

        // --- Draw ticks ---
        DrawXTicks(xAxisY, yAxisX, xTicks);
        DrawYTicks(yAxisX, xAxisY, yTicks);

        // --- Draw waveform ---
        for (int i = 0; i < texWidth; i++)
        {
            int index = Mathf.FloorToInt((float)i / texWidth * (y.Length - 1));
            float normalizedY = (y[index] - minY) / rangeY;

            // Scale waveform to fit between bottom margin and top of plot
            int yPix = Mathf.Clamp(Mathf.FloorToInt(normalizedY * (texHeight - marginY * 1.5f)) + xAxisY, 0, texHeight - 1);
            plotTexture.SetPixel(i, yPix, waveformColor);
        }

        plotTexture.Apply();
    }
    // ================= DRAW HELPERS =================
    private void DrawLineVertical(int x, int yStart, int yEnd, Color c)
    {
        for (int y = yStart; y <= yEnd; y++)
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, c);
    }

    private void DrawLineHorizontal(int y, int xStart, int xEnd, Color c)
    {
        for (int x = xStart; x <= xEnd; x++)
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, c);
    }

    private void DrawXTicks(int axisY, int axisX, int count)
{
    int usableWidth = texWidth - axisX - 10;
    for (int i = 0; i <= count; i++)
    {
        int x = axisX + Mathf.RoundToInt(i * usableWidth / (float)count);

        // Shorter tick marks centered on axis
        for (int j = -2; j <= 2; j++)
        {
            int y = axisY + j;
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, axisColor);
        }
    }
}

private void DrawYTicks(int axisX, int axisY, int count)
{
    int usableHeight = texHeight - axisY - 10;
    for (int i = 0; i <= count; i++)
    {
        int y = axisY + Mathf.RoundToInt(i * usableHeight / (float)count);

        // Shorter tick marks centered on axis
        for (int j = -2; j <= 2; j++)
        {
            int x = axisX + j;
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, axisColor);
        }
    }
}

    // ================= LABEL HANDLING =================
    private void UpdateLabels(float[] xData, float[] yData)
    {
        // Clean up old labels
        foreach (var lbl in xLabels) Destroy(lbl.gameObject);
        foreach (var lbl in yLabels) Destroy(lbl.gameObject);
        xLabels.Clear();
        yLabels.Clear();

        RectTransform plotRect = plotTarget.rectTransform;

        float minX = xData[0];
        float maxX = xData[xData.Length - 1];
        float minY = Mathf.Min(yData);
        float maxY = Mathf.Max(yData);

        // X labels
        for (int i = 0; i <= xTicks; i++)
        {
            float xVal = Mathf.Lerp(minX, maxX, i / (float)xTicks);
            Vector2 anchoredPos = new Vector2(
                Mathf.Lerp(plotRect.rect.xMin + plotRect.rect.width * 0.55f, plotRect.rect.xMax + plotRect.rect.width * 0.5f, i / (float)xTicks),
                plotRect.rect.yMin + plotRect.rect.height * 0.35f
            );

            CreateLabel(xVal.ToString("0.00"), anchoredPos, plotRect, xLabels, TextAnchor.UpperCenter);
        }

        // Y labels
        for (int i = 0; i <= yTicks; i++)
        {
            float yVal = Mathf.Lerp(minY, maxY, i / (float)yTicks);
            Vector2 anchoredPos = new Vector2(
                plotRect.rect.xMin + plotRect.rect.width * 0.4f,
                Mathf.Lerp(plotRect.rect.yMin + plotRect.rect.height * 0.55f, plotRect.rect.yMax + plotRect.rect.height * 0.45f, i / (float)yTicks)
            );

            CreateLabel(yVal.ToString("0.00"), anchoredPos, plotRect, yLabels, TextAnchor.MiddleRight);
        }
    }

    private void CreateLabel(string text, Vector2 anchoredPos, RectTransform parentRect, List<Text> list, TextAnchor anchor)
    {
        GameObject go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(plotTarget.transform, false);

        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = labelFont;
        t.fontSize = labelFontSize;
        t.color = labelColor;
        t.alignment = anchor;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        list.Add(t);
    }
}

public static class Texture2DExtensions
{
    public static void ClearTexture(this Texture2D tex, Color c)
    {
        var fillColorArray = tex.GetPixels();
        for (int i = 0; i < fillColorArray.Length; ++i)
            fillColorArray[i] = c;
        tex.SetPixels(fillColorArray);
    }
}
