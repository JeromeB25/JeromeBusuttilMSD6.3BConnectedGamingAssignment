using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Monitors and logs network latency between server and clients.
/// Sends ping messages at regular intervals and measures round-trip time.
/// </summary>
public class NetworkLatencyMonitor : NetworkBehaviour
{
    // Singleton instance
    public static NetworkLatencyMonitor Instance { get; private set; }
    
    [Header("Ping Settings")]
    [Tooltip("Time in seconds between ping measurements")]
    [SerializeField] private float pingInterval = 2.0f;
    
    [Tooltip("Number of ping samples to keep for averaging")]
    [SerializeField] private int pingSampleSize = 10;
    
    [Tooltip("Whether to log every ping measurement to the console")]
    [SerializeField] private bool logEveryPing = true;
    
    [Tooltip("Whether to log average ping periodically")]
    [SerializeField] private bool logAveragePing = true;
    
    [Tooltip("Interval in seconds for logging average ping")]
    [SerializeField] private float averageLogInterval = 10.0f;
    
    // Dictionary to track ping times for each client
    private Dictionary<ulong, Queue<float>> clientPingTimes = new Dictionary<ulong, Queue<float>>();
    
    // Dictionary to track when pings were sent to calculate round-trip time
    private Dictionary<int, Stopwatch> pingStopwatches = new Dictionary<int, Stopwatch>();
    
    // Counter for ping message IDs
    private int pingIdCounter = 0;
    
    // Timer for the next ping
    private float nextPingTime = 0f;
    
    // Timer for logging average ping
    private float nextAverageLogTime = 0f;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Initialize timer
        nextPingTime = Time.time + pingInterval;
        nextAverageLogTime = Time.time + averageLogInterval;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize ping tracking for ourselves
        if (!clientPingTimes.ContainsKey(NetworkManager.LocalClientId))
        {
            clientPingTimes[NetworkManager.LocalClientId] = new Queue<float>();
        }
        
        // Log that we've started monitoring
        if (IsServer)
        {
            Debug.Log("[NetworkLatencyMonitor] Started monitoring latency as server");
            
            // Subscribe to client connect/disconnect events
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.Log("[NetworkLatencyMonitor] Started monitoring latency as client");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe from events
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void Update()
    {
        if (!NetworkManager.IsConnectedClient)
            return;
            
        // Time to send a new ping
        if (Time.time >= nextPingTime)
        {
            SendPing();
            nextPingTime = Time.time + pingInterval;
        }
        
        // Time to log average ping
        if (logAveragePing && Time.time >= nextAverageLogTime)
        {
            LogAveragePing();
            nextAverageLogTime = Time.time + averageLogInterval;
        }
    }

    private void SendPing()
    {
        if (IsServer)
        {
            // Server sends ping to all clients
            SendPingToClientsClientRpc(pingIdCounter);
        }
        else
        {
            // Client sends ping to server
            SendPingToServerServerRpc(pingIdCounter);
        }
        
        // Start measuring time
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        pingStopwatches[pingIdCounter] = stopwatch;
        
        // Increment ping ID
        pingIdCounter++;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendPingToServerServerRpc(int pingId, ServerRpcParams rpcParams = default)
    {
        // Get the client who sent this ping
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Server received ping from client, send pong back
        SendPongToClientClientRpc(pingId, clientId);
    }

    [ClientRpc]
    private void SendPingToClientsClientRpc(int pingId)
    {
        if (!IsServer)
        {
            // Client received ping from server, send pong back
            SendPongToServerServerRpc(pingId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendPongToServerServerRpc(int pingId, ServerRpcParams rpcParams = default)
    {
        // Get the client who sent this pong
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Calculate round-trip time
        if (pingStopwatches.TryGetValue(pingId, out Stopwatch stopwatch))
        {
            stopwatch.Stop();
            float pingMs = stopwatch.ElapsedMilliseconds;
            pingStopwatches.Remove(pingId);
            
            // Record ping time for this client
            RecordPingTime(clientId, pingMs);
            
            // Log ping time
            if (logEveryPing)
            {
                Debug.Log($"[NetworkLatencyMonitor] Client {clientId} ping: {pingMs:F1}ms");
            }
        }
    }

    [ClientRpc]
    private void SendPongToClientClientRpc(int pingId, ulong targetClientId)
    {
        // Only process if we're the target client
        if (NetworkManager.LocalClientId == targetClientId)
        {
            // Calculate round-trip time
            if (pingStopwatches.TryGetValue(pingId, out Stopwatch stopwatch))
            {
                stopwatch.Stop();
                float pingMs = stopwatch.ElapsedMilliseconds;
                pingStopwatches.Remove(pingId);
                
                // Record ping time for ourselves
                RecordPingTime(NetworkManager.LocalClientId, pingMs);
                
                // Log ping time
                if (logEveryPing)
                {
                    Debug.Log($"[NetworkLatencyMonitor] Your ping to server: {pingMs:F1}ms");
                }
            }
        }
    }

    private void RecordPingTime(ulong clientId, float pingMs)
    {
        // Ensure the client has a queue
        if (!clientPingTimes.ContainsKey(clientId))
        {
            clientPingTimes[clientId] = new Queue<float>();
        }
        
        // Add the ping time
        Queue<float> pingQueue = clientPingTimes[clientId];
        pingQueue.Enqueue(pingMs);
        
        // Keep only the latest samples
        while (pingQueue.Count > pingSampleSize)
        {
            pingQueue.Dequeue();
        }
    }

    private void LogAveragePing()
    {
        foreach (var kvp in clientPingTimes)
        {
            ulong clientId = kvp.Key;
            Queue<float> pingQueue = kvp.Value;
            
            if (pingQueue.Count > 0)
            {
                // Calculate average ping
                float sum = 0;
                foreach (float ping in pingQueue)
                {
                    sum += ping;
                }
                float avgPing = sum / pingQueue.Count;
                
                // Format message based on whether this is us or another client
                string message;
                if (clientId == NetworkManager.LocalClientId)
                {
                    message = $"[NetworkLatencyMonitor] Your average ping: {avgPing:F1}ms (over {pingQueue.Count} samples)";
                }
                else
                {
                    message = $"[NetworkLatencyMonitor] Client {clientId} average ping: {avgPing:F1}ms (over {pingQueue.Count} samples)";
                }
                
                Debug.Log(message);
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkLatencyMonitor] Client {clientId} connected, starting to monitor latency");
        
        // Initialize ping tracking for this client
        if (!clientPingTimes.ContainsKey(clientId))
        {
            clientPingTimes[clientId] = new Queue<float>();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkLatencyMonitor] Client {clientId} disconnected, stopping latency monitoring");
        
        // Remove ping tracking for this client
        clientPingTimes.Remove(clientId);
    }

    /// <summary>
    /// Returns the current average ping for a specific client
    /// </summary>
    public float GetAveragePing(ulong clientId)
    {
        if (clientPingTimes.TryGetValue(clientId, out Queue<float> pingQueue) && pingQueue.Count > 0)
        {
            float sum = 0;
            foreach (float ping in pingQueue)
            {
                sum += ping;
            }
            return sum / pingQueue.Count;
        }
        return 0f;
    }

    /// <summary>
    /// Returns the current local client's average ping to the server
    /// </summary>
    public float GetLocalAveragePing()
    {
        return GetAveragePing(NetworkManager.LocalClientId);
    }
}