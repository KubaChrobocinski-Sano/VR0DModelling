using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class ShaderPlotController : MonoBehaviour
{
    [Header("Shader References")]
    public Material plotMaterial;
    public RawImage plotTarget;

    [Header("Plot Selection")]
    [Tooltip("The name of the output variable to plot (e.g., 'pa', 'volume'). Case-insensitive.")]
    public string plotToShow = "pa"; // Default to aortic pressure

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

    private readonly List<Text> xLabels = new();
    private readonly List<Text> yLabels = new();

    void Awake()
    {
        plotTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        plotTexture.wrapMode = TextureWrapMode.Clamp;
        if (plotMaterial != null)
        {
            // Create a unique instance of the material for this plotter
            plotMaterial = new Material(plotMaterial);
            plotMaterial.mainTexture = plotTexture;
        }
        if (plotTarget != null)
        {
            plotTarget.material = plotMaterial;
            plotTarget.texture = plotTexture;
        }
    }

    public void DisplaySimulation(SimulationResult sim)
    {
        if (sim == null || sim.time == null || sim.time.Length == 0) return;

        // Get data based on the public string 'plotToShow'
        float[] yData = sim.GetOutput(plotToShow);
        float[] xData = sim.time;

        if (yData == null || yData.Length == 0)
        {
            Debug.LogWarning($"Output variable '{plotToShow}' not found in simulation results.", this);
            plotTexture.ClearTexture(backgroundColor); // Clear the plot if data is not found
            plotTexture.Apply();
            return;
        }

        DrawWaveformWithAxes(xData, yData);
        UpdateLabels(xData, yData);
    }

    private void DrawWaveformWithAxes(float[] x, float[] y)
    {
        plotTexture.ClearTexture(backgroundColor);

        float minY = y.Min();
        float maxY = y.Max();
        float rangeY = maxY - minY;
        if (Mathf.Approximately(rangeY, 0)) rangeY = 1f;

        int marginX = Mathf.RoundToInt(texWidth * 0.05f);
        int marginY = Mathf.RoundToInt(texHeight * 0.1f);

        // Draw waveform
        Vector2 prevPixel = Vector2.zero;
        for (int i = 0; i < texWidth; i++)
        {
            int index = Mathf.FloorToInt((float)i / (texWidth - 1) * (y.Length - 1));
            float normalizedY = (y[index] - minY) / rangeY;
            int yPix = marginY + Mathf.FloorToInt(normalizedY * (texHeight - marginY * 2));

            Vector2 currentPixel = new Vector2(i, yPix);

            if (i > 0)
            {
                DrawLine(prevPixel, currentPixel, waveformColor);
            }
            prevPixel = currentPixel;
        }

        DrawAxes(marginX, marginY);

        plotTexture.Apply();
    }

    private void DrawAxes(int marginX, int marginY)
    {
        // Y-Axis
        DrawLine(new Vector2(marginX, marginY), new Vector2(marginX, texHeight - marginY), axisColor);
        // X-Axis
        DrawLine(new Vector2(marginX, marginY), new Vector2(texWidth - marginX, marginY), axisColor);

        // Ticks
        for (int i = 0; i <= xTicks; i++)
        {
            float xPos = marginX + (float)i / xTicks * (texWidth - marginX * 2);
            DrawLine(new Vector2(xPos, marginY - 5), new Vector2(xPos, marginY + 5), axisColor);
        }
        for (int i = 0; i <= yTicks; i++)
        {
            float yPos = marginY + (float)i / yTicks * (texHeight - marginY * 2);
            DrawLine(new Vector2(marginX - 5, yPos), new Vector2(marginX + 5, yPos), axisColor);
        }
    }

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        int x = (int)from.x;
        int y = (int)from.y;
        int x2 = (int)to.x;
        int y2 = (int)to.y;

        int w = x2 - x;
        int h = y2 - y;
        int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
        if (w < 0) dx1 = -1; else if (w > 0) dx1 = 1;
        if (h < 0) dy1 = -1; else if (h > 0) dy1 = 1;
        if (w < 0) dx2 = -1; else if (w > 0) dx2 = 1;
        int longest = Mathf.Abs(w);
        int shortest = Mathf.Abs(h);
        if (!(longest > shortest))
        {
            longest = Mathf.Abs(h);
            shortest = Mathf.Abs(w);
            if (h < 0) dy2 = -1; else if (h > 0) dy2 = 1;
            dx2 = 0;
        }
        int numerator = longest >> 1;
        for (int i = 0; i <= longest; i++)
        {
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                plotTexture.SetPixel(x, y, color);
            numerator += shortest;
            if (!(numerator < longest))
            {
                numerator -= longest;
                x += dx1;
                y += dy1;
            }
            else
            {
                x += dx2;
                y += dy2;
            }
        }
    }


    private void UpdateLabels(float[] xData, float[] yData)
    {
        foreach (var lbl in xLabels) if (lbl) Destroy(lbl.gameObject);
        foreach (var lbl in yLabels) if (lbl) Destroy(lbl.gameObject);
        xLabels.Clear();
        yLabels.Clear();

        RectTransform plotRect = plotTarget.rectTransform;
        float minX = xData.Min();
        float maxX = xData.Max();
        float minY = yData.Min();
        float maxY = yData.Max();

        for (int i = 0; i <= xTicks; i++)
        {
            float xVal = Mathf.Lerp(minX, maxX, (float)i / xTicks);
            float xPos = Mathf.Lerp(-plotRect.rect.width / 2f, plotRect.rect.width / 2f, (float)i / xTicks) + 40f;
            CreateLabel(xVal.ToString("F2"), new Vector2(xPos, -plotRect.rect.height / 2f - 15f), xLabels);
        }

        for (int i = 0; i <= yTicks; i++)
        {
            float yVal = Mathf.Lerp(minY, maxY, (float)i / yTicks);
            float yPos = Mathf.Lerp(-plotRect.rect.height / 2f, plotRect.rect.height / 2f, (float)i / yTicks);
            CreateLabel(yVal.ToString("F2"), new Vector2(-plotRect.rect.width / 2f - 40f, yPos), yLabels, TextAnchor.MiddleRight);
        }
    }

    private void CreateLabel(string text, Vector2 anchoredPos, List<Text> list, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(plotTarget.transform, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = labelFont ? labelFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = labelFontSize;
        t.color = labelColor;
        t.alignment = anchor;
        RectTransform rt = t.rectTransform;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(80, 20);
        list.Add(t);
    }
}

public static class Texture2DExtensions
{
    public static void ClearTexture(this Texture2D tex, Color c)
    {
        if (tex == null) return;
        var fillColorArray = new Color[tex.width * tex.height];
        for (int i = 0; i < fillColorArray.Length; ++i)
            fillColorArray[i] = c;
        tex.SetPixels(fillColorArray);
    }
}
