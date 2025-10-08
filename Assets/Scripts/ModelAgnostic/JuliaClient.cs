using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

// --- Data Structures for Communication ---

[Serializable]
public class ModelParameter
{
    public string name;
    public double value;
}

[Serializable]
public class ModelMetadata
{
    public string modelName;
    public List<ModelParameter> parameters;
    public List<string> outputs;
}

[Serializable]
public class SimulationResult
{
    // Using lists of keys and values for JSON deserialization,
    // as Unity's JsonUtility doesn't support dictionaries directly.
    public float[] time;
    public float[] plv;
    public float[] pa;
    public float[] flow;
    public float[] volume;

    // Helper to get data by name, making it agnostic.
    public float[] GetOutput(string name)
    {
        return name.ToLower() switch
        {
            "plv" => plv,
            "pa" => pa,
            "flow" => flow,
            "volume" => volume,
            "time" => time,
            _ => null,
        };
    }
}


public class JuliaClient : MonoBehaviour
{
    [Header("Julia Connection")]
    public string host = "127.0.0.1";
    public int port = 2000;

    [Header("Model Information (Read-Only)")]
    [SerializeField] private string modelName;
    [SerializeField] private List<string> availableOutputs;

    [Header("Model Parameters (Editable)")]
    public List<ModelParameter> parameters = new List<ModelParameter>();

    [Header("Plotter References")]
    public ShaderPlotController[] plotControllers;

    private bool isSimulationRunning = false;

    async void Start()
    {
        // On start, fetch the model structure from Julia
        await GetModelMetadata();
        // Then run the initial simulation
        await RunSimulation();
    }

    /// <summary>
    /// Connects to the Julia server to get the model's name, parameters, and outputs.
    /// </summary>
    public async Task GetModelMetadata()
    {
        Debug.Log("Requesting model metadata from Julia...");
        try
        {
            using (var client = new TcpClient(host, port))
            using (var stream = client.GetStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // Request metadata
                string jsonRequest = "{\"command\":\"get_metadata\"}";
                await writer.WriteLineAsync(jsonRequest);
                await writer.FlushAsync();

                // Read and parse response
                string jsonResponse = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    Debug.LogError("No metadata received from Julia.");
                    return;
                }

                var metadata = JsonUtility.FromJson<ModelMetadata>(jsonResponse);

                // Update Inspector fields
                modelName = metadata.modelName;
                parameters = metadata.parameters;
                availableOutputs = metadata.outputs;

                Debug.Log($"Successfully loaded model: {modelName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to get metadata: {e.Message}");
        }
    }

    /// <summary>
    /// Runs the simulation by sending the current parameter values to Julia.
    /// </summary>
    [ContextMenu("Run Simulation with Current Parameters")]
    public async Task RunSimulation()
    {
        if (isSimulationRunning)
        {
            Debug.LogWarning("Simulation is already running.");
            return;
        }

        isSimulationRunning = true;
        try
        {
            Debug.Log($"Connecting to Julia at {host}:{port}...");
            using (var client = new TcpClient(host, port))
            using (var stream = client.GetStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // --- FIX: Manually construct the parameters JSON to avoid Newtonsoft dependency ---
                var paramJsonParts = parameters.Select(p => $"\"{p.name}\":{p.value}");
                string paramsJson = "{" + string.Join(",", paramJsonParts) + "}";
                string jsonRequest = $"{{\"command\":\"run_simulation\",\"parameters\":{paramsJson}}}";

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

                // Update all assigned plot controllers
                foreach (var plot in plotControllers)
                {
                    if (plot != null)
                        plot.DisplaySimulation(result);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Julia connection failed: {e.Message}");
        }
        finally
        {
            isSimulationRunning = false;
        }
    }

    // This method is called by Unity when a value is changed in the Inspector.
    private async void OnValidate()
    {
        // To avoid spamming requests while typing, you might want to add a delay
        // or a button, but for simplicity, this will re-run on any change.
        if (Application.isPlaying && parameters.Count > 0)
        {
            await RunSimulation();
        }
    }
}

