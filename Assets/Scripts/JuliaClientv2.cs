using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class SimulationResult
{
    public float[] time;
    public float[] plv;     // Left ventricular pressure
    public float[] pa;      // Aortic pressure
    public float[] flow;    // Flow
    public float[] volume;  // LV volume
}

public class JuliaClientv2 : MonoBehaviour
{
    [Header("Julia Connection")]
    public string host = "127.0.0.1";
    public int port = 2000;

    [Header("Model Parameters")]
    [Range(30, 120)] public float heartRate = 70f;
    [Range(0.01f, 0.2f)] public float R1 = 0.02f;
    [Range(0.1f, 2f)] public float R2 = 0.5f;
    [Range(0.1f, 3f)] public float C = 1.5f;

    [Header("Plot References")]
    public ShaderPlotController plotController1;
    public ShaderPlotController plotController2;
    public ShaderPlotController plotController3;

    private bool isRunning = false;

    async void Start()
    {
        await RunSimulation();
    }

    public async Task RunSimulation()
    {
        if (isRunning) return;
        isRunning = true;

        try
        {
            Debug.Log($"Connecting to Julia at {host}:{port}...");
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                using (var stream = client.GetStream())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // Build request JSON
                    string jsonRequest = $"{{\"heartRate\":{heartRate},\"R1\":{R1},\"R2\":{R2},\"C\":{C}}}";
                    await writer.WriteLineAsync(jsonRequest);
                    await writer.FlushAsync();
                    Debug.Log("Request sent to Julia.");

                    // Wait for full JSON response
                    string jsonResponse = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(jsonResponse))
                    {
                        Debug.LogError("No data received from Julia.");
                        isRunning = false;
                        return;
                    }

                    // Parse JSON into SimulationResult
                    SimulationResult result = JsonUtility.FromJson<SimulationResult>(jsonResponse);
                    if (result == null || result.time == null)
                    {
                        Debug.LogError(" Invalid data format from Julia.");
                        isRunning = false;
                        return;
                    }

                    Debug.Log($"Received {result.time.Length} samples from Julia.");

                    // Push data to plots
                    if (plotController1 != null) plotController1.DisplaySimulation(result);
                    if (plotController2 != null) plotController2.DisplaySimulation(result);
                    if (plotController3 != null) plotController3.DisplaySimulation(result);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Julia connection failed: {e.Message}");
        }

        isRunning = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Automatically rerun simulation when parameters change in play mode
        if (Application.isPlaying)
            _ = RunSimulation();
    }
#endif
}
