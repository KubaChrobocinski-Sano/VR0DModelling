using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System;

[Serializable]
public class SimulationResult
{
    public float[] time;
    public float[] plv;
    public float[] pa;
    public float[] flow;
    public float[] volume;
}

public class JuliaClient : MonoBehaviour
{
    [Header("Julia Connection")]
    public string host = "127.0.0.1";
    public int port = 2000;

    [Header("Parameters")]
    public float heartRate = 70f;
    public float R1 = 0.02f;
    public float R2 = 0.5f;
    public float C = 1.5f;

    [Header("References")]
    public ShaderPlotController plotController1;
    public ShaderPlotController plotController2;
    public ShaderPlotController plotController3;

    async void Start()
    {
        await RunSimulation();
    }

    public async Task RunSimulation()
    {
        try
        {
            Debug.Log($"Connecting to Julia at {host}:{port}...");
            using (var client = new TcpClient(host, port))
            using (var stream = client.GetStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string jsonRequest = $"{{\"heartRate\":{heartRate},\"R1\":{R1},\"R2\":{R2},\"C\":{C}}}";
                await writer.WriteLineAsync(jsonRequest);
                await writer.FlushAsync();
                Debug.Log("Request sent to Julia.");

                string jsonResponse = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    Debug.LogError("No data received from Julia.");
                    return;
                }

                var result = JsonUtility.FromJson<SimulationResult>(jsonResponse);
                Debug.Log($"Received {result.time.Length} samples from Julia.");

                if (plotController1 != null)
                    plotController1.DisplaySimulation(result);
                    plotController2.DisplaySimulation(result);
                    plotController3.DisplaySimulation(result);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Julia connection failed: {e.Message}");
        }
    }
}
