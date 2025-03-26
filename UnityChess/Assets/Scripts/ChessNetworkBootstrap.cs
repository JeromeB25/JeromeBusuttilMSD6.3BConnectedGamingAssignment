using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Transports.UTP;

public class ChessNetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManagerPrefab;

    private void Awake()
    {
        // Ensure NetworkManager exists
        EnsureNetworkManager();

        // Configure NetworkManager
        ConfigureNetworkManager();

        // Try to start the host
        TryStartHost();
    }

    private void EnsureNetworkManager()
    {
        if (FindObjectOfType<NetworkManager>() == null)
        {
            if (networkManagerPrefab != null)
            {
                Instantiate(networkManagerPrefab);
                Debug.Log("NetworkManager instantiated from prefab");
            }
            else
            {
                Debug.LogError("No NetworkManager prefab assigned!");
            }
        }
    }

    private void ConfigureNetworkManager()
    {
        if (NetworkManager.Singleton == null) return;

        // Ensure UnityTransport component
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
        }

        // Configure transport
        transport.SetConnectionData(
            "127.0.0.1",  // IP address
            7777,          // Port
            "0.0.0.0"      // Bind address
        );

        // Set up connection approval
        NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) =>
        {
            Debug.Log("Connection approval callback triggered");
            response.Approved = true;
            response.CreatePlayerObject = true;
        };

        // Add network callbacks for debugging
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectedCallback += OnClientDisconnected;
    }

    private void TryStartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is null. Cannot start host.");
            return;
        }

        try
        {
            Debug.Log("Attempting to start host...");
            bool success = NetworkManager.Singleton.StartHost();
            
            if (!success)
            {
                Debug.LogError("Failed to start host. Check network configuration.");
            }
            else
            {
                Debug.Log("Host started successfully");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception when starting host: {e.Message}");
            Debug.LogException(e);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectedCallback -= OnClientDisconnected;
        }
    }
}