using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityChess;

/// <summary>
/// Manages the network functionality for the chess game, handling connections, 
/// game sessions, and move synchronization.
/// </summary>
public class ChessNetworkManager : NetworkBehaviour
{
    // Singleton instance
    public static ChessNetworkManager Instance { get; private set; }
    
    // Network events
    public delegate void ChessMoveEvent(Square from, Square to, Transform pieceTransform, Transform squareTransform, Piece promotionPiece);
    public static event ChessMoveEvent OnChessMove;
    
    // Events for UI to subscribe to
    public delegate void ConnectionStatusEvent(string message, NetworkConnectionState state);
    public static event ConnectionStatusEvent OnConnectionStatusChanged;
    
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed,
        HostWaiting
    }

    // Port for the game server
    [SerializeField] private ushort networkPort = 7777;

    // Dictionary to store connected players and their assigned sides
    private Dictionary<ulong, Side> playerSides = new Dictionary<ulong, Side>();

    // Game state
    private bool isMultiplayerGameActive = false;

    // Events
    public event Action<ulong> OnPlayerJoined;
    public event Action<ulong> OnPlayerLeft;
    public event Action OnConnectionFailed;
    public event Action OnGameStarted;

    private void Awake()
    {
        // Ensure singleton behavior
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        // Set up network event handlers
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        // Subscribe to the GameManager's MoveExecutedEvent
        GameManager.MoveExecutedEvent += OnMoveExecuted;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }

        GameManager.MoveExecutedEvent -= OnMoveExecuted;
    }

    /// <summary>
    /// Creates a new game as the host
    /// </summary>
    public void CreateGame()
    {
        try
        {
            Debug.Log("Starting host...");

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null! Make sure NetworkManager exists in the scene.");
                NotifyConnectionStatus("NetworkManager not found", NetworkConnectionState.Failed);
                return;
            }

            // Configure transport
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = "127.0.0.1";
                transport.ConnectionData.Port = networkPort;
            }

            bool success = NetworkManager.Singleton.StartHost();
            Debug.Log("StartHost result: " + success);

            if (success)
            {
                playerSides[NetworkManager.Singleton.LocalClientId] = Side.White;
                NotifyConnectionStatus("Hosting game. Waiting for opponent to join...", NetworkConnectionState.HostWaiting);
            }
            else
            {
                NotifyConnectionStatus("Failed to start host", NetworkConnectionState.Failed);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception in CreateGame: {e.Message}\n{e.StackTrace}");
            NotifyConnectionStatus($"Error: {e.Message}", NetworkConnectionState.Failed);
        }
    }

    /// <summary>
    /// Joins an existing game as a client
    /// </summary>
    public void JoinGame(string ipAddress)
    {
        Debug.Log("JoinGame method called");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null when trying to join game");
            NotifyConnectionStatus("Error: Network manager not found", NetworkConnectionState.Failed);
            return;
        }

        try
        {
            NotifyConnectionStatus("Connecting to host...", NetworkConnectionState.Connecting);

            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("Unity Transport component not found on NetworkManager");
                NotifyConnectionStatus("Error: Transport component not found", NetworkConnectionState.Failed);
                return;
            }

            // Validate IP address
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = "127.0.0.1"; // Default to localhost
            }

            // Configure the transport
            transport.ConnectionData.Address = ipAddress;
            transport.ConnectionData.Port = networkPort;

            Debug.Log($"Connecting to: {ipAddress}:{networkPort}");

            // Start as client
            if (NetworkManager.Singleton.StartClient())
            {
                NotifyConnectionStatus($"Connecting to {ipAddress}...", NetworkConnectionState.Connecting);
            }
            else
            {
                NotifyConnectionStatus("Failed to connect to host", NetworkConnectionState.Failed);
                OnConnectionFailed?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join game: {e.Message}\n{e.StackTrace}");
            NotifyConnectionStatus($"Failed to join game: {e.Message}", NetworkConnectionState.Failed);
            OnConnectionFailed?.Invoke();
        }
    }

    /// <summary>
    /// Disconnects from the current game session
    /// </summary>
    public void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            // Shut down the network connection
            NetworkManager.Singleton.Shutdown();

            // Clear player dictionary
            playerSides.Clear();
            isMultiplayerGameActive = false;

            NotifyConnectionStatus("Disconnected", NetworkConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Notifies UI about connection status changes
    /// </summary>
    private void NotifyConnectionStatus(string message, NetworkConnectionState state)
    {
        Debug.Log($"Connection status: {message} (State: {state})");
        OnConnectionStatusChanged?.Invoke(message, state);
    }

    /// <summary>
    /// Called when a client connects to the network
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");

        // If the connecting player is not the host, assign them to the black side
        if (clientId != NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost)
        {
            // Add client to players dictionary as Black
            playerSides[clientId] = Side.Black;

            // Inform the game that a player has joined
            NotifyConnectionStatus("Opponent connected. Game starting.", NetworkConnectionState.Connected);
            OnPlayerJoined?.Invoke(clientId);

            // Tell the client they are the black side
            AssignPlayerSideClientRpc(clientId, (int)Side.Black);

            // Start the game when both players are connected
            StartGameClientRpc();
        }
        else if (NetworkManager.Singleton.IsClient && clientId == NetworkManager.Singleton.LocalClientId)
        {
            NotifyConnectionStatus("Connected to host.", NetworkConnectionState.Connected);
        }
    }

    /// <summary>
    /// Called when a client disconnects from the network
    /// </summary>
    private void OnClientDisconnect(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");

        if (playerSides.ContainsKey(clientId))
        {
            playerSides.Remove(clientId);
            OnPlayerLeft?.Invoke(clientId);
        }

        // If we're the host and a client disconnected
        if (NetworkManager.Singleton.IsHost && clientId != NetworkManager.Singleton.LocalClientId)
        {
            NotifyConnectionStatus("Opponent disconnected. Waiting for new opponent...", NetworkConnectionState.HostWaiting);
        }
        // If we're disconnecting as a client
        else if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            NotifyConnectionStatus("Disconnected from game.", NetworkConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Assigns a side to a client
    /// </summary>
    [ClientRpc]
    private void AssignPlayerSideClientRpc(ulong clientId, int sideValue)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Side assignedSide = (Side)sideValue;
            Debug.Log($"I've been assigned side: {assignedSide}");

            // Add this client to the local players dictionary
            playerSides[clientId] = assignedSide;
            
            NotifyConnectionStatus($"Connected as {assignedSide}. Waiting for game to start.", NetworkConnectionState.Connected);
        }
    }

    /// <summary>
    /// Starts the game on all clients
    /// </summary>
    [ClientRpc]
    private void StartGameClientRpc()
    {
        isMultiplayerGameActive = true;

        // Tell the GameManager to start a new game
        GameManager.Instance.StartNewGame();

        // Ensure ChessMoveRelay is active
        EnsureMoveRelayIsActive();

        // Update pieces to only allow movement of own side
        UpdatePieceControl();

        NotifyConnectionStatus("Game started!", NetworkConnectionState.Connected);
        OnGameStarted?.Invoke();
    }

    private void EnsureMoveRelayIsActive()
    {
        // Find or create the ChessMoveRelay
        ChessMoveRelay relay = FindObjectOfType<ChessMoveRelay>();
        if (relay == null)
        {
            Debug.Log("Creating new ChessMoveRelay GameObject");
            GameObject relayObj = new GameObject("ChessMoveRelay");
            relay = relayObj.AddComponent<ChessMoveRelay>();

            // Assign the board reference
            GameObject board = GameObject.FindGameObjectWithTag("Board");
            if (board != null)
            {
                relay.chessBoard = board;
            }

            // Add NetworkObject component
            NetworkObject netObj = relayObj.AddComponent<NetworkObject>();

            // Spawn the relay object if we're the server
            if (NetworkManager.Singleton.IsServer && !netObj.IsSpawned)
            {
                netObj.Spawn();
                Debug.Log("ChessMoveRelay spawned successfully");
            }
        }
        else
        {
            Debug.Log("ChessMoveRelay already exists");
        }
    }

    /// <summary>
    /// Returns the side (White/Black) assigned to the specified client
    /// </summary>
    public Side GetPlayerSide(ulong clientId)
    {
        if (playerSides.TryGetValue(clientId, out Side side))
        {
            return side;
        }

        return Side.None;
    }

    /// <summary>
    /// Returns the player's own side
    /// </summary>
    public Side GetLocalPlayerSide()
    {
        return GetPlayerSide(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Checks if the local player is allowed to move pieces of the given side
    /// </summary>
    public bool CanControlSide(Side side)
    {
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("CanControlSide: Not in network mode, allowing all pieces");
            return true;
        }

        // Get the side of the local player
        Side localPlayerSide = GetPlayerSide(NetworkManager.Singleton.LocalClientId);
        Debug.Log($"CanControlSide check: Local player ID {NetworkManager.Singleton.LocalClientId} has side {localPlayerSide}, checking against {side}");

        // Check if the side matches the local player's side
        bool canControl = side == localPlayerSide;
        Debug.Log($"CanControlSide result: {canControl}");
        return canControl;
    }

    /// <summary>
    /// Updates piece controls to only allow movement of the player's side
    /// </summary>
    private void UpdatePieceControl()
    {
        if (!NetworkManager.Singleton.IsConnectedClient || !isMultiplayerGameActive)
            return;

        // Get all visual pieces
        VisualPiece[] allPieces = FindObjectsOfType<VisualPiece>(true);
        Side localPlayerSide = GetPlayerSide(NetworkManager.Singleton.LocalClientId);

        foreach (VisualPiece piece in allPieces)
        {
            // Only enable pieces that match the player's side
            if (piece.enabled)
            {
                // Don't override pieces that are already disabled for game logic reasons
                piece.enabled = piece.PieceColor == localPlayerSide;
            }
        }
    }

    /// <summary>
    /// Check if we're in a multiplayer game
    /// </summary>
    public bool IsInMultiplayerGame()
    {
        return isMultiplayerGameActive && NetworkManager.Singleton.IsConnectedClient;
    }

    /// <summary>
    /// Called when a move is executed in the game
    /// </summary>
    private void OnMoveExecuted()
    {
        // Update piece control after moves to ensure only the correct side can move
        if (isMultiplayerGameActive)
        {
            UpdatePieceControl();
        }
    }
}