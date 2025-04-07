using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityChess;
using System.Collections;

/// <summary>
/// Simple UI manager that provides basic buttons for networking functionality:
/// Host, Client, Leave, and Rejoin. This version automatically finds UI elements by name.
/// </summary>
public class NetworkUI : MonoBehaviour
{
    // UI Components - these will be found automatically
    private Button hostButton;
    private Button clientButton;
    private Button leaveButton;
    private Button rejoinButton;
    
    private TMP_InputField ipAddressInput;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI playerSideText;

    // Store the last IP used for rejoin functionality
    private string lastIpAddress = "127.0.0.1";
    
    // Flag to indicate if we're in a rejoin process
    private bool isRejoining = false;
    
    void Awake()
    {
        // Find all necessary UI components by name
        FindUIComponents();
    }
    
    void Start()
    {
        // Set up button listeners if components were found
        SetupButtonListeners();
        
        // Set initial UI state
        InitializeUIState();
        
        // Subscribe to ChessNetworkManager events when it becomes available
        StartCoroutine(WaitForNetworkManager());
        
        // Check if there's a saved IP address
        if (GameStateSerializer.Instance != null)
        {
            lastIpAddress = GameStateSerializer.Instance.GetSavedIPAddress();
            
            // Update IP input field with saved value
            if (ipAddressInput != null)
            {
                ipAddressInput.text = lastIpAddress;
            }
        }
    }

    private void FindUIComponents()
    {
        // Find buttons by name - looking in children of this GameObject
        hostButton = transform.Find("Host")?.GetComponent<Button>();
        clientButton = transform.Find("Client")?.GetComponent<Button>();
        leaveButton = transform.Find("Leave")?.GetComponent<Button>();
        rejoinButton = transform.Find("Rejoin")?.GetComponent<Button>();
        
        // If buttons aren't direct children, try to find them by name in all children
        if (hostButton == null) hostButton = GetComponentInChildren<Button>(true)?.transform.Find("Text (TMP)")?.parent.GetComponent<Button>();
        if (clientButton == null) clientButton = GetComponentInChildren<Button>(true)?.transform.Find("Text (TMP)")?.parent.GetComponent<Button>();
        if (leaveButton == null) leaveButton = GetComponentInChildren<Button>(true)?.transform.Find("Text (TMP)")?.parent.GetComponent<Button>();
        if (rejoinButton == null) rejoinButton = GetComponentInChildren<Button>(true)?.transform.Find("Text (TMP)")?.parent.GetComponent<Button>();
        
        // Alternative approach: find buttons by looking for text content
        if (hostButton == null) hostButton = FindButtonByText("Host");
        if (clientButton == null) clientButton = FindButtonByText("Client");
        if (leaveButton == null) leaveButton = FindButtonByText("Leave");
        if (rejoinButton == null) rejoinButton = FindButtonByText("Rejoin");
        
        // Find input field and text elements
        ipAddressInput = GetComponentInChildren<TMP_InputField>(true);
        
        // Find status and player side text components
        TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in textComponents)
        {
            if (text.transform.parent.name.Contains("Status") || text.name.Contains("Status"))
            {
                statusText = text;
            }
            else if (text.transform.parent.name.Contains("Player") || text.name.Contains("Player") || 
                    text.transform.parent.name.Contains("Side") || text.name.Contains("Side"))
            {
                playerSideText = text;
            }
        }
        
        // Log what was found/not found
        if (hostButton == null) Debug.LogWarning("Host button not found. Please create a button named 'Host'.");
        if (clientButton == null) Debug.LogWarning("Client button not found. Please create a button named 'Client'.");
        if (leaveButton == null) Debug.LogWarning("Leave button not found. Please create a button named 'Leave'.");
        if (rejoinButton == null) Debug.LogWarning("Rejoin button not found. Please create a button named 'Rejoin'.");
        if (ipAddressInput == null) Debug.LogWarning("IP Address input field not found.");
        if (statusText == null) Debug.LogWarning("Status text not found. Creating a temporary one.");
        if (playerSideText == null) Debug.LogWarning("Player side text not found. Creating a temporary one.");
        
        // Create default text components if needed
        if (statusText == null)
        {
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(transform, false);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Status: Disconnected";
            RectTransform rect = statusObj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -160);
            rect.sizeDelta = new Vector2(200, 30);
        }
        
        if (playerSideText == null)
        {
            GameObject sideObj = new GameObject("PlayerSideText");
            sideObj.transform.SetParent(transform, false);
            playerSideText = sideObj.AddComponent<TextMeshProUGUI>();
            playerSideText.text = "";
            RectTransform rect = sideObj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -190);
            rect.sizeDelta = new Vector2(200, 30);
        }
    }
    
    private Button FindButtonByText(string buttonText)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null && text.text.ToLower().Contains(buttonText.ToLower()))
            {
                return button;
            }
        }
        return null;
    }

    private void SetupButtonListeners()
    {
        // Set up button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostButtonClicked);
        
        if (clientButton != null)
            clientButton.onClick.AddListener(OnClientButtonClicked);
        
        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        
        if (rejoinButton != null)
            rejoinButton.onClick.AddListener(OnRejoinButtonClicked);
    }
    
    private void InitializeUIState()
    {
        // Initially disable leave and rejoin buttons
        if (leaveButton != null)
            leaveButton.interactable = false;
        
        if (rejoinButton != null)
        {
            // Check if there's a saved game state, enable rejoin if available
            bool hasSavedGame = GameStateSerializer.Instance != null && GameStateSerializer.Instance.HasSavedGameState();
            rejoinButton.interactable = hasSavedGame;
        }
        
        // Set status text
        if (statusText != null)
            statusText.text = "Disconnected";
        
        if (playerSideText != null)
            playerSideText.text = "";
        
        // Make sure ipAddressInput has a default value
        if (ipAddressInput != null && string.IsNullOrEmpty(ipAddressInput.text))
        {
            ipAddressInput.text = lastIpAddress;
        }
    }

    private IEnumerator WaitForNetworkManager()
    {
        // Wait until ChessNetworkManager instance is available
        while (ChessNetworkManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Subscribe to events once the manager is available
        ChessNetworkManager.OnConnectionStatusChanged += OnConnectionStatusChanged;
        
        // Also subscribe to game events
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        
        Debug.Log("Successfully connected to ChessNetworkManager!");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ChessNetworkManager.Instance != null)
        {
            ChessNetworkManager.OnConnectionStatusChanged -= OnConnectionStatusChanged;
        }
        
        // Unsubscribe from game events
        GameManager.GameEndedEvent -= OnGameEnded;
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
    }

    private void OnHostButtonClicked()
    {
        if (ChessNetworkManager.Instance != null)
        {
            if (statusText != null)
                statusText.text = "Starting host...";
            
            Debug.Log("Host button clicked - creating game");    
            ChessNetworkManager.Instance.CreateGame();
            
            // Update UI
            UpdateButtonState(true);
            
            // Clear any previously saved game state since we're starting a new game
            if (GameStateSerializer.Instance != null)
            {
                GameStateSerializer.Instance.ClearSavedGameState();
            }
        }
        else
        {
            Debug.LogError("ChessNetworkManager not found!");
            if (statusText != null)
                statusText.text = "Error: Network manager not found";
        }
    }

    private void OnClientButtonClicked()
    {
        if (ChessNetworkManager.Instance != null)
        {
            // Get IP from input field
            string ipAddress = "127.0.0.1"; // Default
            
            if (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text))
            {
                ipAddress = ipAddressInput.text;
            }
            
            // Save the IP for rejoin functionality
            lastIpAddress = ipAddress;
            
            // Save IP in GameStateSerializer
            if (GameStateSerializer.Instance != null)
            {
                GameStateSerializer.Instance.SaveIPAddress(ipAddress);
            }
            
            if (statusText != null)
                statusText.text = $"Connecting to {ipAddress}...";
            
            Debug.Log($"Client button clicked - joining game at {ipAddress}");    
            ChessNetworkManager.Instance.JoinGame(ipAddress);
            
            // Update UI
            UpdateButtonState(true);
            
            // Clear any previously saved game state since we're starting a new game
            if (GameStateSerializer.Instance != null && !isRejoining)
            {
                GameStateSerializer.Instance.ClearSavedGameState();
            }
        }
        else
        {
            Debug.LogError("ChessNetworkManager not found!");
            if (statusText != null)
                statusText.text = "Error: Network manager not found";
        }
    }

    private void OnLeaveButtonClicked()
    {
        if (ChessNetworkManager.Instance != null)
        {
            Debug.Log("Leave button clicked - leaving game");
            
            // Save the game state before leaving
            if (GameStateSerializer.Instance != null && NetworkManager.Singleton.IsConnectedClient)
            {
                GameStateSerializer.Instance.SaveGameState();
            }
            
            ChessNetworkManager.Instance.LeaveGame();
            
            if (statusText != null)
                statusText.text = "Disconnected";
            
            // Update UI
            UpdateButtonState(false);
            // Enable rejoin button
            if (rejoinButton != null)
                rejoinButton.interactable = true;
        }
    }

    private void OnRejoinButtonClicked()
    {
        if (ChessNetworkManager.Instance != null)
        {
            isRejoining = true;
            
            // Get the saved IP address
            if (GameStateSerializer.Instance != null)
            {
                lastIpAddress = GameStateSerializer.Instance.GetSavedIPAddress();
            }
            
            if (statusText != null)
                statusText.text = $"Reconnecting to {lastIpAddress}...";
            
            Debug.Log($"Rejoin button clicked - rejoining game at {lastIpAddress}");
            ChessNetworkManager.Instance.JoinGame(lastIpAddress);
            
            // Update UI
            UpdateButtonState(true);
        }
    }

    private void OnConnectionStatusChanged(string message, ChessNetworkManager.NetworkConnectionState state)
    {
        Debug.Log($"Connection status changed: {message} (State: {state})");
        
        // Update status text
        if (statusText != null)
            statusText.text = message;
        
        // Update side display if connected
        if (state == ChessNetworkManager.NetworkConnectionState.Connected || 
            state == ChessNetworkManager.NetworkConnectionState.HostWaiting)
        {
            UpdateSideDisplay();
        }
        
        // Update buttons based on connection state
        bool isConnected = (state == ChessNetworkManager.NetworkConnectionState.Connected || 
                           state == ChessNetworkManager.NetworkConnectionState.HostWaiting);
        
        UpdateButtonState(isConnected);
        
        // If connection failed, enable host/client buttons again
        if (state == ChessNetworkManager.NetworkConnectionState.Failed)
        {
            if (hostButton != null) hostButton.interactable = true;
            if (clientButton != null) clientButton.interactable = true;
            if (leaveButton != null) leaveButton.interactable = false;
            isRejoining = false;
        }
        
        // If connection succeeded and we're rejoining, restore the saved game state
        if (state == ChessNetworkManager.NetworkConnectionState.Connected && isRejoining && 
            GameStateSerializer.Instance != null && GameStateSerializer.Instance.HasSavedGameState())
        {
            // Wait a brief moment for the connection to fully establish
            StartCoroutine(RestoreGameStateAfterDelay(0.5f));
        }
    }
    
    private IEnumerator RestoreGameStateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log("Attempting to restore saved game state after rejoin");
        
        // First, restore the player's side
        Side savedSide = GameStateSerializer.Instance.GetSavedPlayerSide();
        if (savedSide != Side.None)
        {
            // Make sure we've been assigned the correct side on rejoin
            Side currentSide = ChessNetworkManager.Instance.GetPlayerSide(NetworkManager.Singleton.LocalClientId);
            if (currentSide != savedSide)
            {
                Debug.LogWarning($"Player rejoined with different side: Was {savedSide}, now {currentSide}");
            }
        }
        
        // Restore the game state itself
        GameStateSerializer.Instance.RestoreSavedGameState();
        
        // Notify the server that we've restored our game state
        if (ChessNetworkManager.Instance.GetComponent<NetworkObject>().IsSpawned)
        {
            // Only call the RPC if we've added it to the ChessNetworkManager
            try
            {
                // Try to get the AddGameStateRestoredNotification method through reflection
                System.Reflection.MethodInfo method = ChessNetworkManager.Instance.GetType().GetMethod("NotifyGameStateRestoredServerRpc");
                if (method != null)
                {
                    method.Invoke(ChessNetworkManager.Instance, null);
                    Debug.Log("Notified server about game state restoration");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Couldn't notify server about game state restoration: {e.Message}");
            }
        }
        
        // Reset the rejoining flag
        isRejoining = false;

        RestoreGameStateFromFirebase();

    }

    private void UpdateSideDisplay()
    {
        if (ChessNetworkManager.Instance != null && playerSideText != null)
        {
            Side playerSide = ChessNetworkManager.Instance.GetPlayerSide(NetworkManager.Singleton.LocalClientId);
            if (playerSide != Side.None)
            {
                playerSideText.text = $"Playing as: {playerSide}";
                Debug.Log($"Updated player side display: {playerSide}");
            }
        }
    }

    private void UpdateButtonState(bool isConnected)
    {
        // When connected: disable host/client, enable leave
        if (hostButton != null) hostButton.interactable = !isConnected;
        if (clientButton != null) clientButton.interactable = !isConnected;
        
        if (ipAddressInput != null)
            ipAddressInput.interactable = !isConnected;
            
        if (leaveButton != null)
            leaveButton.interactable = isConnected;
        
        // Rejoin button is only enabled after leaving
        if (isConnected && rejoinButton != null)
        {
            rejoinButton.interactable = false;
        }
        
        Debug.Log($"Updated button states. Connected: {isConnected}");
    }
    
    private void OnGameEnded()
    {
        // Clear saved game when a game ends naturally
        if (GameStateSerializer.Instance != null)
        {
            GameStateSerializer.Instance.ClearSavedGameState();
        }
    }
    
    private void OnNewGameStarted()
    {
        // Clear saved game when a new game starts
        if (GameStateSerializer.Instance != null && !isRejoining)
        {
            GameStateSerializer.Instance.ClearSavedGameState();
        }
    }

    private async void RestoreGameStateFromFirebase()
    {
        string userId = SystemInfo.deviceUniqueIdentifier;
        string state = await GameStateSaver.Instance.LoadMostRecentGameState(userId);
        if (!string.IsNullOrEmpty(state))
        {
            GameManager.Instance.LoadGame(state);
            Debug.Log("[GameState] Restored game from Firebase.");
        }
    }
}