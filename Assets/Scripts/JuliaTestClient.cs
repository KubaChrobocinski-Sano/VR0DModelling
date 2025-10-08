using UnityEngine;
using System.Net.Sockets;
using System.IO;
using System.Threading;

public class JuliaTestClient : MonoBehaviour
{
    public string host = "127.0.0.1";
    public int port = 2000;

    private TcpClient client;
    private StreamReader reader;
    private StreamWriter writer;

    void Start()
    {
        try
        {
            Debug.Log($"Connecting to Julia at {host}:{port} ...");
            client = new TcpClient(host, port);
            var stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);

            Debug.Log("Connected to Julia!");
            writer.WriteLine("Hello from Unity");
            writer.Flush();

            // start reading asynchronously
            new Thread(ReadLoop) { IsBackground = true }.Start();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }

    void ReadLoop()
    {
        try
        {
            while (client != null && client.Connected)
            {
                string line = reader.ReadLine();
                if (line != null)
                    Debug.Log("Received from Julia: " + line);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("ReadLoop stopped: " + ex.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (client != null)
        {
            writer?.Close();
            reader?.Close();
            client.Close();
        }
    }
}
