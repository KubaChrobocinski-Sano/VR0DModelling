using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;

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
    public float[] time;
    public float[] plv;
    public float[] pa;
    public float[] flow;
    public float[] volume;

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

    [Header("UI References")]
    [Tooltip("A prefab containing a Slider, a Text component named 'Label', and a Text component named 'ValueDisplay'.")]
    public GameObject parameterUIPrefab;
    [Tooltip("The parent object (e.g., a Panel) where the UI sliders will be created.")]
    public Transform parametersUIParent;
    [Tooltip("The button that will trigger the simulation.")]
    public Button runSimulationButton;


    [Header("Model Information (Read-Only)")]
    [SerializeField] private string modelName;
    [SerializeField] private List<string> availableOutputs;

    [Header("Plotter References")]
    public ShaderPlotController[] plotControllers;

    // --- Private Fields ---
    private List<ModelParameter> parameters = new List<ModelParameter>();
    private Dictionary<string, Slider> parameterSliders = new Dictionary<string, Slider>();
    private Dictionary<string, TMP_Text> parameterValueTexts = new Dictionary<string, TMP_Text>();
    private bool isSimulationRunning = false;

    async void Start()
    {
        // On start, fetch model metadata, create the UI, and run the initial simulation.
        await GetModelMetadata();
        CreateParameterUI();

        // Add a listener to the button to run the simulation on click.
        runSimulationButton?.onClick.AddListener(() => _ = RunSimulation());

        // Run the first simulation with default values.
        await RunSimulation();
    }

    /// <summary>
    /// Creates the UI sliders and text fields based on parameters from the Julia server.
    /// </summary>
    private void CreateParameterUI()
    {
        // Clear any old UI elements
        foreach (Transform child in parametersUIParent)
        {
            Destroy(child.gameObject);
        }
        parameterSliders.Clear();
        parameterValueTexts.Clear();

        if (parameterUIPrefab == null)
        {
            Debug.LogError("Parameter UI Prefab is not assigned in the Inspector!");
            return;
        }

        // Create a new UI element for each parameter
        foreach (var param in parameters)
        {
            GameObject uiInstance = Instantiate(parameterUIPrefab, parametersUIParent);

            // Find the components within the instantiated prefab
            Slider slider = uiInstance.GetComponentInChildren<Slider>();
            // Find the Text components by name for reliability
            TMP_Text label = uiInstance.transform.Find("Label")?.GetComponent<TMP_Text>();
            TMP_Text valueText = uiInstance.transform.Find("ValueDisplay")?.GetComponent<TMP_Text>();

            if (label != null) label.text = param.name;


            if (slider != null && valueText != null)
            {
                // Set slider range (e.g., 0 to double the initial value) and current value.
                slider.minValue = 0;
                slider.maxValue = (float)param.value * 2f;
                slider.value = (float)param.value;

                // Set value display text
                valueText.text = param.value.ToString("F2");

                // Store references
                parameterSliders.Add(param.name, slider);
                parameterValueTexts.Add(param.name, valueText);

                // Add a listener to update the text when the slider moves
                string currentParamName = param.name;
                slider.onValueChanged.AddListener((value) => OnSliderChanged(currentParamName, value));
            }
            else
            {
                Debug.LogWarning($"Prefab for '{param.name}' is missing a Slider, a Text named 'Label', or a Text named 'ValueDisplay'.", uiInstance);
            }
        }
    }

    private void OnSliderChanged(string paramName, float value)
    {
        if (parameterValueTexts.TryGetValue(paramName, out TMP_Text valueText))
        {
            valueText.text = value.ToString("F2");
        }
    }

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
                string jsonRequest = "{\"command\":\"get_metadata\"}";
                await writer.WriteLineAsync(jsonRequest);
                await writer.FlushAsync();

                string jsonResponse = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    Debug.LogError("No metadata received from Julia.");
                    return;
                }

                var metadata = JsonUtility.FromJson<ModelMetadata>(jsonResponse);
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
    /// Reads values from the UI, sends them to Julia, and updates the plots.
    /// </summary>
    public async Task RunSimulation()
    {
        if (isSimulationRunning)
        {
            Debug.LogWarning("Simulation is already running.");
            return;
        }
        isSimulationRunning = true;

        // Update parameter list from UI slider values before sending
        foreach (var param in parameters)
        {
            if (parameterSliders.ContainsKey(param.name))
            {
                param.value = parameterSliders[param.name].value;
            }
        }

        try
        {
            Debug.Log($"Connecting to Julia at {host}:{port}...");
            using (var client = new TcpClient(host, port))
            using (var stream = client.GetStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var paramJsonParts = parameters.Select(p => $"\"{p.name}\":{p.value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
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
}

