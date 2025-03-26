using UnityChess;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Extends UIManager functionality to work with networking.
/// Should be added alongside the existing UIManager.
/// </summary>
public class UIManagerNetworkExtension : NetworkBehaviour
{
    [SerializeField] private Text networkStatusText;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject gamePanel;
    
    private UIManager uiManager;
    
    private void Start()
    {
        uiManager = UIManager.Instance;
        
        // Set initial UI state
        if (connectionPanel != null)
        {
            connectionPanel.SetActive(true);
        }
        
        if (gamePanel != null)
        {
            gamePanel.SetActive(false);
        }
        
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
        else
        {
            Debug.LogWarning("NetworkManager is null, cannot subscribe to network events");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        // Update UI when a client connects
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Side localPlayerSide = ChessNetworkManager.Instance.GetLocalPlayerSide();
            
            if (networkStatusText != null)
            {
                networkStatusText.text = NetworkManager.Singleton.IsHost 
                    ? "Connected as Host (White)" 
                    : "Connected as Client (Black)";
            }
            
            // Show game UI, hide connection UI
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(false);
            }
            
            if (gamePanel != null)
            {
                gamePanel.SetActive(true);
            }
        }
        else if (NetworkManager.Singleton.IsHost)
        {
            // If we're the host and another client connected
            if (networkStatusText != null)
            {
                networkStatusText.text = "Opponent Connected";
            }
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        // Update UI when a client disconnects
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (networkStatusText != null)
            {
                networkStatusText.text = "Disconnected";
            }
            
            // Show connection UI, hide game UI
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(true);
            }
            
            if (gamePanel != null)
            {
                gamePanel.SetActive(false);
            }
        }
        else if (NetworkManager.Singleton.IsHost)
        {
            // If we're the host and the other player disconnected
            if (networkStatusText != null)
            {
                networkStatusText.text = "Opponent Disconnected";
            }
        }
    }
    
    private void OnServerStarted()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            if (networkStatusText != null)
            {
                networkStatusText.text = "Hosting - Waiting for Opponent";
            }
        }
    }
    
    // This method can be called from UI buttons to start a new game
    public void OnNetworkNewGameButton()
    {
        // Only the host can start a new game
        if (NetworkManager.Singleton.IsHost)
        {
            GameManager.Instance.StartNewGame();
        }
        else
        {
            Debug.LogWarning("Only the host can start a new game");
            if (networkStatusText != null)
            {
                networkStatusText.text = "Only the host can start a new game";
            }
        }
    }
    
    // This method can be called when a player wants to reset the game to a specific half-move
    public void OnNetworkResetGameToHalfMove(int halfMoveIndex)
    {
        // Only the host can reset the game
        if (NetworkManager.Singleton.IsHost)
        {
            GameManager.Instance.ResetGameToHalfMoveIndex(halfMoveIndex);
            
            // Notify clients about the reset via RPC
            NotifyGameResetClientRpc(halfMoveIndex);
        }
        else
        {
            Debug.LogWarning("Only the host can reset the game");
            if (networkStatusText != null)
            {
                networkStatusText.text = "Only the host can reset the game";
            }
        }
    }
    
    [ClientRpc]
    private void NotifyGameResetClientRpc(int halfMoveIndex)
    {
        // Skip if this is the host (already handled)
        if (NetworkManager.Singleton.IsHost)
            return;
            
        // Reset the game to the specified half-move index
        GameManager.Instance.ResetGameToHalfMoveIndex(halfMoveIndex);
    }
}