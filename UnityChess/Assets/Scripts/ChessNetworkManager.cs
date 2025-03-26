using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityChess;

/// <summary>
/// Manages network connections for the Chess game.
/// Handles hosting, joining, leaving and rejoining sessions.
/// </summary>
public class ChessNetworkManager : MonoBehaviourSingleton<ChessNetworkManager>
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button rejoinButton;
    
    [Header("Network Settings")]
    [SerializeField] private ushort port = 7777;
    
    // Store previous connection info
    private bool wasHost = false;
    
    // Track player sides
    private Dictionary<ulong, Side> playerSides = new Dictionary<ulong, Side>();

    private void Start()
    {
        Debug.Log("ChessNetworkManager starting...");
        
        // Setup buttons
        if (hostButton != null) hostButton.onClick.AddListener(StartHost);
        if (clientButton != null) clientButton.onClick.AddListener(StartClient);
        if (leaveButton != null) leaveButton.onClick.AddListener(LeaveGame);
        if (rejoinButton != null) rejoinButton.onClick.AddListener(RejoinGame);
        
        // Set initial button states
        if (leaveButton != null) leaveButton.interactable = false;
        if (rejoinButton != null) rejoinButton.interactable = false;
        
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
        else
        {
            Debug.LogWarning("NetworkManager singleton is not available yet");
        }
    }

    private void OnDestroy()
    {
        // Remove event listeners
        if (hostButton != null) hostButton.onClick.RemoveListener(StartHost);
        if (clientButton != null) clientButton.onClick.RemoveListener(StartClient);
        if (leaveButton != null) leaveButton.onClick.RemoveListener(LeaveGame);
        if (rejoinButton != null) rejoinButton.onClick.RemoveListener(RejoinGame);
        
        // Unsubscribe from network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
    }

    public void StartHost()
    {
        Debug.Log("Starting as host...");
        
        // Configure transport
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        }
        
        // Start host
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("Host started successfully");
            wasHost = true;
            
            // Update UI buttons
            if (hostButton != null) hostButton.interactable = false;
            if (clientButton != null) clientButton.interactable = false;
            if (leaveButton != null) leaveButton.interactable = true;
            
            // Assign White to host
            playerSides[NetworkManager.Singleton.LocalClientId] = Side.White;
        }
        else
        {
            Debug.LogError("Failed to start host");
        }
    }

    public void StartClient()
    {
        Debug.Log("Starting as client...");
        
        // Configure transport
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        }
        
        // Start client
        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log("Client started successfully");
            wasHost = false;
            
            // Update UI buttons
            if (hostButton != null) hostButton.interactable = false;
            if (clientButton != null) clientButton.interactable = false;
            if (leaveButton != null) leaveButton.interactable = true;
        }
        else
        {
            Debug.LogError("Failed to start client");
        }
    }

    public void LeaveGame()
    {
        Debug.Log("Leaving game...");
        
        // Shutdown network connection
        NetworkManager.Singleton.Shutdown();
        
        // Update UI buttons
        if (hostButton != null) hostButton.interactable = true;
        if (clientButton != null) clientButton.interactable = true;
        if (leaveButton != null) leaveButton.interactable = false;
        if (rejoinButton != null) rejoinButton.interactable = true;
    }

    public void RejoinGame()
    {
        Debug.Log("Rejoining game...");
        
        if (wasHost)
        {
            StartHost();
        }
        else
        {
            StartClient();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        
        // If this is a new client connecting to the server, assign them Black
        if (NetworkManager.Singleton.IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            playerSides[clientId] = Side.Black;
            Debug.Log($"Assigned Black to client {clientId}");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        
        // Remove from side tracking
        if (playerSides.ContainsKey(clientId))
        {
            playerSides.Remove(clientId);
        }
    }

    private void OnServerStarted()
    {
        Debug.Log("Server started");
    }

    // Get the side assigned to a specific client ID
    public Side GetPlayerSide(ulong clientId)
    {
        if (playerSides.TryGetValue(clientId, out Side side))
        {
            return side;
        }
        
        // Default to White for host, Black for client
        return NetworkManager.Singleton.IsHost ? Side.White : Side.Black;
    }
    
    // Get the local player's side
    public Side GetLocalPlayerSide()
    {
        return GetPlayerSide(NetworkManager.Singleton.LocalClientId);
    }
}