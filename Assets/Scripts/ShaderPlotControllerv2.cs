using UnityEngine;
using UnityEngine.UI;
/*
public class ShaderPlotControllerv2 : MonoBehaviour
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

    private Texture2D plotTexture;
    private const int texWidth = 1024;
    private const int texHeight = 256;

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

        // --- Draw axes ---
        int xAxisY = Mathf.RoundToInt(texHeight * 0.1f); // Small bottom margin
        int yAxisX = Mathf.RoundToInt(texWidth * 0.1f);  // Small left margin

        DrawLineVertical(yAxisX, 0, texHeight - 1, axisColor);
        DrawLineHorizontal(xAxisY, 0, texWidth - 1, axisColor);

        // --- Draw ticks on axes ---
        DrawXTicks(xAxisY, yAxisX, xTicks);
        DrawYTicks(yAxisX, xAxisY, yTicks);

        // --- Draw waveform ---
        for (int i = 0; i < texWidth; i++)
        {
            int index = Mathf.FloorToInt((float)i / texWidth * (y.Length - 1));
            float normalizedY = (y[index] - minY) / rangeY;
            int yPix = Mathf.Clamp(Mathf.FloorToInt(normalizedY * (texHeight * 0.8f)) + xAxisY, 0, texHeight - 1);
            plotTexture.SetPixel(i, yPix, waveformColor);
        }

        plotTexture.Apply();
    }

    // Draw a vertical line
    private void DrawLineVertical(int x, int yStart, int yEnd, Color c)
    {
        for (int y = yStart; y <= yEnd; y++)
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, c);
    }

    // Draw a horizontal line
    private void DrawLineHorizontal(int y, int xStart, int xEnd, Color c)
    {
        for (int x = xStart; x <= xEnd; x++)
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, c);
    }

    private void DrawXTicks(int axisY, int axisX, int count)
    {
        int usableWidth = texWidth - axisX - 10;
        for (int i = 1; i <= count; i++)
        {
            int x = axisX + Mathf.RoundToInt(i * usableWidth / (float)count);
            for (int j = -3; j <= 3; j++)
                if (axisY + j >= 0 && axisY + j < texHeight)
                    plotTexture.SetPixel(x, axisY + j, axisColor);
        }
    }

    private void DrawYTicks(int axisX, int axisY, int count)
    {
        int usableHeight = texHeight - axisY - 10;
        for (int i = 1; i <= count; i++)
        {
            int y = axisY + Mathf.RoundToInt(i * usableHeight / (float)count);
            for (int j = -3; j <= 3; j++)
                if (axisX + j >= 0 && axisX + j < texWidth)
                    plotTexture.SetPixel(axisX + j, y, axisColor);
        }
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
*/